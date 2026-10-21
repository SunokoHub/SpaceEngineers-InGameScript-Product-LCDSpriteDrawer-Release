using System;
using System.Collections.Generic;
using Sandbox.ModAPI.Ingame;
using VRageMath;

namespace IngameScript
{
    public sealed class BindingInput
    {
        public long BlockId;
        public BlockValueKind Source;
        public double Value;
        public bool Enabled;
        public int Users;
        public bool Lost;
    }

    public sealed class BindingTrack
    {
        public int Element, Decimals = 1;
        public string Property, BlockName, Prefix = "", Suffix = "", Combine;
        public BlockValueKind Source;
        public MotionMode Mode;
        public double InputMin, InputMax = 1, OutputMin, OutputMax;
        public Color MinColor, MaxColor;
        public double[] Thresholds;
        public string[] Cases;
        public MotionTrack Numeric;
        public BindingInput Input;
    }

    public sealed class BindingSet
    {
        public List<BindingTrack> Tracks = new List<BindingTrack>();
        public MotionSet Combined;
        public HashSet<BindingInput> Inputs = new HashSet<BindingInput>();
        public bool SourceLost
        {
            get { foreach (var input in Inputs) if (input.Lost) return true; return false; }
        }
    }

    public static class Binding
    {
        public static void IndexName(Dictionary<string, long> names, string name, long id)
        { if (names.ContainsKey(name)) names[name] = 0; else names.Add(name, id); }
        public static long FindName(Dictionary<string, long> names, string name)
        { long id; return names.TryGetValue(name, out id) ? id : 0; }
        public static bool TryParse(string source, List<LayoutElement> layout, MotionSet motion,
            out BindingSet result, out string reason)
        {
            result = null; reason = "";
            if (source == null || source.Length > SurfaceInfo.MaxCustomData || layout == null || motion == null)
                return Reject("Invalid Binding input.", out reason);
            if (source.Length == 0) { result = new BindingSet { Combined = motion }; return true; }
            var plan = new BindingSet { Combined = new MotionSet { Elements = new List<MotionTrack>[layout.Count], Count = motion.Count } };
            var ids = new Dictionary<string, int>(StringComparer.Ordinal);
            var properties = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < layout.Count; i++)
            { ids.Add(layout[i].Id, i); plan.Combined.Elements[i] = new List<MotionTrack>(motion.Elements[i]); }
            int lineNumber = 0;
            foreach (string raw in source.Split('\n'))
            {
                lineNumber++;
                string line;
                if (!StaticLayout.TryLine(raw, out line))
                    return Reject("Binding line " + lineNumber + ": Invalid physical line.", out reason);
                if (string.IsNullOrWhiteSpace(line)) continue;
                Dictionary<string, StaticLayout.Attribute> attributes;
                BindingTrack track;
                if (!StaticLayout.TryAttributes(line, out attributes, out reason) ||
                    !TryTrack(attributes, ids, layout, out track, out reason))
                { reason = "Binding line " + lineNumber + ": " + reason; return false; }
                if (!properties.Add(track.Element + ":" + track.Property))
                    return Reject("Duplicate Binding element/property.", out reason);
                if (track.Numeric != null)
                {
                    var tracks = plan.Combined.Elements[track.Element];
                    int duplicate = -1;
                    for (int i = 0; i < tracks.Count; i++)
                    {
                        var previous = tracks[i];
                        if (previous.Property == track.Numeric.Property) duplicate = i;
                        if (Motion.Dimension(previous.Property) && Motion.Dimension(track.Numeric.Property) && previous.Anchor != track.Numeric.Anchor)
                            return Reject("Dimension anchors must match.", out reason);
                    }
                    if (duplicate >= 0)
                    {
                        var previous = tracks[duplicate];
                        if (track.Combine == null) return Reject("Duplicate Motion/Binding property requires Combine.", out reason);
                        if (previous.Anchor != track.Numeric.Anchor || (track.Numeric.Property == MotionProperty.Orbit &&
                            track.Combine != "Replace" && previous.Center != track.Numeric.Center))
                            return Reject("Combined anchors and orbit centers must match.", out reason);
                        track.Numeric.Basis = previous;
                        track.Numeric.Combine = track.Combine;
                        tracks[duplicate] = track.Numeric;
                    }
                    else
                    {
                        if (track.Combine != null) return Reject("Combine requires matching Motion.", out reason);
                        tracks.Add(track.Numeric); plan.Combined.Count++;
                    }
                }
                plan.Tracks.Add(track);
            }
            result = plan; return true;
        }

        static bool TryTrack(Dictionary<string, StaticLayout.Attribute> attributes, Dictionary<string, int> ids,
            List<LayoutElement> layout, out BindingTrack result, out string reason)
        {
            result = null; reason = "";
            var track = new BindingTrack();
            string element, property, source, name, value;
            if (!Take(attributes, "Element", false, out element) || !ids.TryGetValue(element, out track.Element) ||
                !Take(attributes, "Property", false, out property) || !Take(attributes, "Source", false, out source) ||
                !Take(attributes, "BlockName", true, out name) || name.Length == 0 || !BlockValues.TryKind(source, out track.Source))
                return Reject("Element, Property, Source and quoted BlockName are required.", out reason);
            track.Property = property; track.BlockName = name;
            var baseline = layout[track.Element];
            bool text = property == "Data" && baseline.IsText, color = property == "Color", visible = property == "Visible", data = property == "Data" && !baseline.IsText;
            MotionProperty numeric;
            if (!text && !color && !visible && !data)
            {
                if (!Enum.TryParse(property, out numeric) || (int)numeric < 0 || (int)numeric > (int)MotionProperty.Orbit ||
                    numeric.ToString() != property || !Motion.Applies(numeric, baseline.IsText))
                    return Reject("Invalid Binding Property for Type.", out reason);
                track.Numeric = new MotionTrack { Property = numeric };
                if (attributes.ContainsKey("Combine"))
                {
                    if (!Take(attributes, "Combine", false, out track.Combine) ||
                        (track.Combine != "Replace" && track.Combine != "Add" && track.Combine != "Multiply"))
                        return Reject("Invalid Combine.", out reason);
                }
                if (!Number(attributes, "OutputMin", out track.OutputMin) || !Number(attributes, "OutputMax", out track.OutputMax) ||
                    !Motion.Finite(track.OutputMax - track.OutputMin)) return Reject("Invalid Binding output range.", out reason);
                if (numeric == MotionProperty.TextScale && track.Combine != "Add" &&
                    (!PositiveScale(track.OutputMin) || !PositiveScale(track.OutputMax)))
                    return Reject("TextScale endpoints must be positive finite API values.", out reason);
                track.Numeric.Initial = track.OutputMin;
                if (numeric == MotionProperty.Orbit &&
                    (!Take(attributes, "Center", false, out value) || !StaticLayout.Vector(value, out track.Numeric.Center)))
                    return Reject("Center is required for Orbit.", out reason);
                if (attributes.ContainsKey("Anchor") && Motion.Dimension(numeric) &&
                    (!Take(attributes, "Anchor", false, out value) || !Motion.TryAnchor(value, out track.Numeric.Anchor)))
                    return Reject("Invalid Anchor.", out reason);
            }
            if (color && (!ReadColor(attributes, "MinColor", out track.MinColor) || !ReadColor(attributes, "MaxColor", out track.MaxColor)))
                return Reject("MinColor/MaxColor require RGBA integers.", out reason);
            if (track.Numeric != null || color)
            {
                if (!BlockValues.IsBoolean(track.Source) &&
                    (!Number(attributes, "InputMin", out track.InputMin) || !Number(attributes, "InputMax", out track.InputMax) ||
                    track.InputMin >= track.InputMax || !Motion.Finite(track.InputMax - track.InputMin)))
                    return Reject("Invalid Binding input range.", out reason);
                if (attributes.ContainsKey("Mode") &&
                    (!Take(attributes, "Mode", false, out value) || !Enum.TryParse(value, out track.Mode) ||
                    (int)track.Mode < 0 || (int)track.Mode > (int)MotionMode.PingPong || track.Mode.ToString() != value))
                    return Reject("Invalid Binding Mode.", out reason);
                if (BlockValues.IsBoolean(track.Source) && track.Mode != MotionMode.Clamp)
                    return Reject("Boolean mapping uses Clamp.", out reason);
            }
            else if (data || visible || attributes.ContainsKey("Thresholds"))
            {
                if (!Take(attributes, "Thresholds", false, out value)) return Reject("Thresholds are required for switching.", out reason);
                var parts = value.Split(',');
                track.Thresholds = new double[parts.Length]; track.Cases = new string[parts.Length + 1];
                for (int i = 0; i < parts.Length; i++)
                    if (!Motion.Number(parts[i], out track.Thresholds[i]) || (i > 0 && track.Thresholds[i] <= track.Thresholds[i - 1]))
                        return Reject("Thresholds must be finite and strictly increasing.", out reason);
                for (int i = 0; i < track.Cases.Length; i++)
                {
                    if (!Take(attributes, "Case" + i, !visible, out value) ||
                        (visible ? value != "true" && value != "false" : value.Length > StaticLayout.MaxData || (data && value.Length == 0)))
                        return Reject("Invalid or missing switch Case.", out reason);
                    track.Cases[i] = value;
                }
            }
            else if (text)
            {
                if (attributes.ContainsKey("Decimals"))
                {
                    if (!Take(attributes, "Decimals", false, out value) || value.Length != 1 || value[0] < '0' || value[0] > '3')
                        return Reject("Decimals must be 0 through 3.", out reason);
                    track.Decimals = value[0] - '0';
                }
                if (attributes.ContainsKey("Prefix") && (!Take(attributes, "Prefix", true, out track.Prefix) || track.Prefix.Length > StaticLayout.MaxData))
                    return Reject("Invalid Prefix.", out reason);
                if (attributes.ContainsKey("Suffix") && (!Take(attributes, "Suffix", true, out track.Suffix) || track.Suffix.Length > StaticLayout.MaxData))
                    return Reject("Invalid Suffix.", out reason);
            }
            if (attributes.Count != 0) return Reject("Unknown or inapplicable Binding attribute.", out reason);
            result = track; return true;
        }

        static bool TryUnit(BindingTrack track, double value, out double unit)
        {
            unit = 0;
            if (!Motion.Finite(value)) return false;
            if (track.Mode == MotionMode.Clamp)
                unit = value <= track.InputMin ? 0 : value >= track.InputMax ? 1 : (value - track.InputMin) / (track.InputMax - track.InputMin);
            else
            {
                double z = (value - track.InputMin) / (track.InputMax - track.InputMin);
                if (!Motion.Finite(z)) return false;
                double period = track.Mode == MotionMode.Loop ? 1 : 2;
                unit = z - period * Math.Floor(z / period);
                if (track.Mode == MotionMode.PingPong && unit > 1) unit = 2 - unit;
            }
            return Motion.Finite(unit) && unit >= 0 && unit <= 1;
        }

        public static bool TryMap(BindingTrack track, double value, out double result)
        {
            result = 0; double unit;
            if (!TryUnit(track, value, out unit)) return false;
            result = unit == 0 ? track.OutputMin : unit == 1 ? track.OutputMax : track.OutputMin + (track.OutputMax - track.OutputMin) * unit;
            return Motion.Finite(result) && (track.Numeric.Property != MotionProperty.TextScale || track.Combine == "Add" || PositiveScale(result));
        }

        public static bool TryText(BindingTrack track, double value, out string text)
        {
            text = null;
            if (!Motion.Finite(value)) return false;
            text = track.Prefix + value.ToString("F" + track.Decimals, System.Globalization.CultureInfo.InvariantCulture) + track.Suffix;
            return text.Length <= StaticLayout.MaxData;
        }

        public static bool TryBuild(List<LayoutElement> layout, BindingSet binding, double time,
            out List<SpriteDrawingElement> elements, out string reason)
        {
            elements = null; reason = "";
            foreach (var track in binding.Tracks)
            {
                if (track.Input == null) return Reject("Binding reference was not resolved.", out reason);
                double value = InputValue(track);
                if (!Motion.Finite(value)) return Reject("Invalid Binding input value.", out reason);
                if (track.Numeric != null)
                {
                    double mapped;
                    if (!TryMap(track, value, out mapped)) return Reject("Invalid numeric mapping.", out reason);
                    track.Numeric.Initial = mapped;
                }
            }
            if (!Motion.TryBuild(layout, binding.Combined, time, out elements, out reason)) return false;
            var hidden = new HashSet<int>();
            foreach (var track in binding.Tracks)
            {
                var sprite = elements[track.Element];
                double value = InputValue(track);
                if (track.Cases != null)
                {
                    int index = 0;
                    while (index < track.Thresholds.Length && value >= track.Thresholds[index]) index++;
                    if (track.Property == "Visible") { if (track.Cases[index] == "false") hidden.Add(track.Element); }
                    else sprite.Data = track.Cases[index];
                }
                else if (track.Property == "Data")
                {
                    string text;
                    if (!TryText(track, value, out text)) { elements = null; return Reject("Invalid numeric text or more than 256 characters.", out reason); }
                    sprite.Data = text;
                }
                else if (track.Property == "Color")
                {
                    double unit;
                    if (!TryUnit(track, value, out unit)) { elements = null; return Reject("Invalid color mapping.", out reason); }
                    sprite.Color = new Color(Channel(track.MinColor.R, track.MaxColor.R, unit), Channel(track.MinColor.G, track.MaxColor.G, unit),
                        Channel(track.MinColor.B, track.MaxColor.B, unit), Channel(track.MinColor.A, track.MaxColor.A, unit));
                }
                elements[track.Element] = sprite;
            }
            for (int i = elements.Count - 1; i >= 0; i--) if (hidden.Contains(i)) elements.RemoveAt(i);
            return true;
        }

        static int Channel(byte low, byte high, double unit) => (int)Math.Floor(low + (high - low) * unit + 0.5);
        static double InputValue(BindingTrack track) => BlockValues.IsBoolean(track.Source) ? (track.Input.Enabled ? 1 : 0) : track.Input.Value;
        public static bool Matches(IMyTerminalBlock block, IMyTerminalBlock me, BlockValueKind source) =>
            block != null && block.IsSameConstructAs(me) && BlockValues.Accepts(block, source);
        static bool PositiveScale(double value) => value > 0 && Motion.Finite(value) && (float)value > 0 && !float.IsInfinity((float)value);
        static bool Take(Dictionary<string, StaticLayout.Attribute> attributes, string key, bool quoted, out string value)
        {
            value = null; StaticLayout.Attribute attribute;
            if (!attributes.TryGetValue(key, out attribute) || attribute.Quoted != quoted) return false;
            value = attribute.Value; attributes.Remove(key); return true;
        }
        static bool Number(Dictionary<string, StaticLayout.Attribute> attributes, string key, out double value)
        { value = 0; string text; return Take(attributes, key, false, out text) && Motion.Number(text, out value); }
        static bool ReadColor(Dictionary<string, StaticLayout.Attribute> attributes, string key, out Color value)
        { value = default(Color); string text; return Take(attributes, key, false, out text) && StaticLayout.TryColor(text, out value); }
        static bool Reject(string message, out string reason) { reason = message; return false; }
    }
}
