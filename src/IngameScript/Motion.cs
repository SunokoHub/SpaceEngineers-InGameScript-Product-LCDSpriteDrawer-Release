using System;
using System.Collections.Generic;
using VRageMath;

namespace IngameScript
{
    #region mdk preserve
    public enum MotionProperty { MoveX, MoveY, Width, Height, Angle, TextScale, Orbit }
    public enum MotionMode { Clamp, Loop, PingPong }
    #endregion

    public sealed class MotionTrack
    {
        public MotionProperty Property;
        public MotionMode Mode;
        public double Rate, Initial, Min, Max;
        public bool Bounded;
        public Vector2 Anchor, Center;
        public MotionTrack Basis;
        public string Combine;
    }

    public sealed class MotionSet
    {
        public List<MotionTrack>[] Elements;
        public int Count;
    }

    public static class Motion
    {
        public static bool TryParse(string source, List<LayoutElement> layout, out MotionSet result, out string reason)
        {
            result = null; reason = "";
            if (source == null || source.Length > SurfaceInfo.MaxCustomData || layout == null)
                return Reject("Invalid Motion input.", out reason);
            var plan = new MotionSet { Elements = new List<MotionTrack>[layout.Count] };
            var ids = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < layout.Count; i++) { ids.Add(layout[i].Id, i); plan.Elements[i] = new List<MotionTrack>(); }
            int lineNumber = 0;
            foreach (string raw in source.Split('\n'))
            {
                lineNumber++;
                string line;
                if (!StaticLayout.TryLine(raw, out line))
                    return LineError(lineNumber, "Invalid physical line or more than 2048 characters.", out reason);
                if (string.IsNullOrWhiteSpace(line)) continue;
                Dictionary<string, StaticLayout.Attribute> attributes;
                string detail; int index; MotionTrack track;
                if (!StaticLayout.TryAttributes(line, out attributes, out detail) ||
                    !TryTrack(attributes, layout, ids, out index, out track, out detail))
                    return LineError(lineNumber, detail, out reason);
                foreach (var existing in plan.Elements[index])
                {
                    if (existing.Property == track.Property) return LineError(lineNumber, "Duplicate element/property.", out reason);
                    if (Dimension(existing.Property) && Dimension(track.Property) && existing.Anchor != track.Anchor)
                        return LineError(lineNumber, "Dimension anchors must match.", out reason);
                }
                plan.Elements[index].Add(track); plan.Count++;
            }
            List<SpriteDrawingElement> initial;
            if (plan.Count > 0 && !TryBuild(layout, plan, 0, out initial, out reason)) return false;
            result = plan;
            return true;
        }

        static bool TryTrack(Dictionary<string, StaticLayout.Attribute> attributes, List<LayoutElement> layout,
            Dictionary<string, int> ids, out int index, out MotionTrack track, out string reason)
        {
            index = 0; track = null; reason = "";
            StaticLayout.Attribute element, property, rate;
            if (!attributes.TryGetValue("Element", out element) || !attributes.TryGetValue("Property", out property) ||
                !attributes.TryGetValue("Rate", out rate) || element.Quoted || !ids.TryGetValue(element.Value, out index))
                return Reject("Element, Property and Rate are required; Element must exist.", out reason);
            foreach (var pair in attributes)
                if (pair.Value.Quoted || (pair.Key != "Element" && pair.Key != "Property" && pair.Key != "Rate" &&
                    pair.Key != "Mode" && pair.Key != "Min" && pair.Key != "Max" && pair.Key != "Initial" &&
                    pair.Key != "Anchor" && pair.Key != "Center")) return Reject("Unknown or quoted Motion attribute.", out reason);
            MotionProperty kind;
            if (!Enum.TryParse(property.Value, out kind) || (int)kind < 0 ||
                (int)kind > (int)MotionProperty.Orbit || kind.ToString() != property.Value)
                return Reject("Invalid Motion Property.", out reason);
            var baseline = layout[index];
            if (!Applies(kind, baseline.IsText)) return Reject("Motion Property does not apply to this Type.", out reason);
            var value = new MotionTrack { Property = kind };
            if (!Number(rate.Value, out value.Rate)) return Reject("Invalid Motion Rate.", out reason);
            value.Initial = kind == MotionProperty.Width ? baseline.Size.X : kind == MotionProperty.Height ? baseline.Size.Y :
                kind == MotionProperty.TextScale ? baseline.TextScale : 0;
            StaticLayout.Attribute input;
            if (attributes.TryGetValue("Initial", out input) && !Number(input.Value, out value.Initial)) return Reject("Invalid Initial.", out reason);
            if (attributes.TryGetValue("Mode", out input) &&
                (!Enum.TryParse(input.Value, out value.Mode) || (int)value.Mode < 0 ||
                    (int)value.Mode > (int)MotionMode.PingPong || value.Mode.ToString() != input.Value)) return Reject("Invalid Motion Mode.", out reason);
            bool minimum = attributes.TryGetValue("Min", out input);
            if (minimum && !Number(input.Value, out value.Min)) return Reject("Invalid Min.", out reason);
            bool maximum = attributes.TryGetValue("Max", out input);
            if (maximum && !Number(input.Value, out value.Max)) return Reject("Invalid Max.", out reason);
            value.Bounded = minimum && maximum;
            if (minimum != maximum || (value.Mode != MotionMode.Clamp && !value.Bounded) ||
                (value.Bounded && (!(value.Min < value.Max) || !Finite(value.Max - value.Min) ||
                    value.Initial < value.Min || value.Initial > value.Max))) return Reject("Invalid Motion bounds or Initial.", out reason);
            if (kind == MotionProperty.TextScale && (value.Initial <= 0 || (value.Bounded && value.Min <= 0)))
                return Reject("TextScale values and bounds must be positive.", out reason);
            if (attributes.TryGetValue("Anchor", out input) && !TryAnchor(input.Value, out value.Anchor)) return Reject("Invalid Anchor.", out reason);
            bool center = attributes.TryGetValue("Center", out input);
            if (center != (kind == MotionProperty.Orbit) || (center && !StaticLayout.Vector(input.Value, out value.Center)))
                return Reject("Center is required only for Orbit.", out reason);
            track = value;
            return true;
        }

        internal static bool TryAnchor(string value, out Vector2 anchor)
        {
            string[] names = { "TopLeft", "TopCenter", "TopRight", "CenterLeft", "Center", "CenterRight", "BottomLeft", "BottomCenter", "BottomRight" };
            for (int i = 0; i < names.Length; i++)
                if (value == names[i]) { anchor = new Vector2((i % 3 - 1) * 0.5f, (i / 3 - 1) * 0.5f); return true; }
            anchor = Vector2.Zero;
            return false;
        }

        public static bool TryValue(MotionTrack track, double time, out double value)
        {
            if (track.Basis != null)
            {
                value = track.Initial;
                if (!Finite(time) || time < 0 || !Finite(value)) return false;
                if (track.Combine != "Replace")
                {
                    double basis;
                    if (!TryValue(track.Basis, time, out basis)) return false;
                    value = track.Combine == "Add" ? basis + value : basis * value;
                }
                return Finite(value) && (track.Property != MotionProperty.TextScale || value > 0);
            }
            value = track.Initial + track.Rate * time;
            if (!Finite(time) || time < 0 || !Finite(value)) return false;
            if (track.Mode == MotionMode.Clamp)
            { if (track.Bounded) value = Math.Max(track.Min, Math.Min(track.Max, value)); }
            else
            {
                double width = track.Max - track.Min, period = track.Mode == MotionMode.Loop ? width : 2 * width;
                double z = track.Initial - track.Min + track.Rate * time;
                if (!Finite(width) || width <= 0 || !Finite(period) || !Finite(z)) return false;
                double r = z - period * Math.Floor(z / period);
                value = track.Min + (track.Mode == MotionMode.Loop || r <= width ? r : 2 * width - r);
            }
            return Finite(value) && (track.Property != MotionProperty.TextScale || value > 0);
        }

        public static bool TryBuild(List<LayoutElement> layout, MotionSet motion, double time,
            out List<SpriteDrawingElement> result, out string reason)
        {
            result = null; reason = "";
            var sprites = DrawingData.Build(layout);
            for (int i = 0; i < layout.Count; i++)
            {
                if (motion.Elements[i].Count == 0) continue;
                var baseline = layout[i];
                double dx = 0, dy = 0, width = baseline.Size.X, height = baseline.Size.Y;
                double angle = 0, orbit = 0, textScale = baseline.TextScale;
                Vector2 anchor = Vector2.Zero, angleAnchor = Vector2.Zero, center = Vector2.Zero;
                bool dimension = false;
                foreach (var track in motion.Elements[i])
                {
                    double value;
                    if (!TryValue(track, time, out value)) return Reject("Motion value is non-finite or invalid. Element=" + baseline.Id, out reason);
                    switch (track.Property)
                    {
                        case MotionProperty.MoveX: dx = value; break;
                        case MotionProperty.MoveY: dy = value; break;
                        case MotionProperty.Width: width = value; anchor = track.Anchor; dimension = true; break;
                        case MotionProperty.Height: height = value; anchor = track.Anchor; dimension = true; break;
                        case MotionProperty.Angle: angle = value; angleAnchor = track.Anchor; break;
                        case MotionProperty.TextScale: textScale = value; break;
                        case MotionProperty.Orbit: orbit = value; center = track.Center; break;
                    }
                }
                if (!dimension) anchor = angleAnchor;
                const double radians = Math.PI / 180;
                double a0 = baseline.AngleDegrees * radians, a1 = a0 + angle * radians, b = orbit * radians;
                if (!Finite(a1) || !Finite(b)) return Reject("Motion angle overflow. Element=" + baseline.Id, out reason);
                double x = baseline.Position.X, y = baseline.Position.Y;
                if (anchor != Vector2.Zero)
                {
                    double c0 = a0 == 0 ? 1 : Math.Cos(a0), s0 = a0 == 0 ? 0 : Math.Sin(a0);
                    double c1 = a1 == 0 ? 1 : Math.Cos(a1), s1 = a1 == 0 ? 0 : Math.Sin(a1);
                    double oldX = anchor.X * baseline.Size.X, oldY = anchor.Y * baseline.Size.Y;
                    double newX = anchor.X * width, newY = anchor.Y * height;
                    x += c0 * oldX - s0 * oldY - c1 * newX + s1 * newY;
                    y += s0 * oldX + c0 * oldY - s1 * newX - c1 * newY;
                }
                if (b != 0)
                {
                    double cb = Math.Cos(b), sb = Math.Sin(b), relativeX = x - center.X, relativeY = y - center.Y;
                    x = center.X + cb * relativeX - sb * relativeY;
                    y = center.Y + sb * relativeX + cb * relativeY;
                }
                var sprite = sprites[i];
                sprite.Position = new Vector2((float)(x + dx), (float)(y + dy));
                sprite.Size = baseline.IsText ? (Vector2?)null : new Vector2((float)width, (float)height);
                sprite.RotationOrScale = (float)(baseline.IsText ? textScale : a1);
                if (!Finite(sprite.Position.Value.X) || !Finite(sprite.Position.Value.Y) ||
                    (!baseline.IsText && (!Finite(sprite.Size.Value.X) || !Finite(sprite.Size.Value.Y))) ||
                    !Finite(sprite.RotationOrScale.Value) || (baseline.IsText && sprite.RotationOrScale.Value <= 0))
                    return Reject("Motion API value overflow or invalid TextScale. Element=" + baseline.Id, out reason);
                sprites[i] = sprite;
            }
            result = sprites;
            return true;
        }

        public static bool TryAdvance(double current, double delta, out double elapsed)
        { elapsed = current + delta; return Finite(current) && current >= 0 && Finite(delta) && delta >= 0 && Finite(elapsed); }
        internal static bool Applies(MotionProperty property, bool text) =>
            !(text && (Dimension(property) || property == MotionProperty.Angle)) && (text || property != MotionProperty.TextScale);
        internal static bool Dimension(MotionProperty property) => property == MotionProperty.Width || property == MotionProperty.Height;
        internal static bool Number(string source, out double value) => double.TryParse(source,
            System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out value) && Finite(value);
        internal static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
        static bool LineError(int line, string detail, out string reason) => Reject("Motion line " + line + ": " + detail, out reason);
        static bool Reject(string message, out string reason) { reason = message; return false; }
    }
}
