using System;
using System.Collections.Generic;
using Sandbox.ModAPI.Ingame;
using VRage.Game.GUI.TextPanel;
using VRageMath;

namespace IngameScript
{
    public enum SpriteDrawingKind { Texture, Text, ClipRect, ClearClipRect }

    // Nullable fields mean omitted attributes; inputs remain owned by the caller.
    public struct SpriteDrawingElement
    {
        public SpriteDrawingKind Kind;
        public string Data, FontId;
        public Vector2? Position, Size;
        public Color? Color;
        public TextAlignment? Alignment;
        public float? RotationOrScale;
    }

    public static partial class SpriteDrawing
    {
        public static bool TryDrawElements(IMyTerminalBlock block, int surfaceIndex,
            IList<SpriteDrawingElement> elements, out string reason, float scale = 1f)
        {
            reason = "";
            if (block == null) { reason = "Null block."; return false; }
            if (elements == null) { reason = "Null drawing elements."; return false; }
            if (!Finite(scale)) { reason = "Invalid scale."; return false; }
            IMyTextSurface surface;
            if (!ResolveSurface(block, surfaceIndex, out surface, out reason)) return false;
            List<MySprite> sprites;
            if (!BuildElements(elements, surface.TextureSize * 0.5f,
                surface.ScriptForegroundColor, scale, out sprites, out reason)) return false;
            Submit(surface, sprites);
            return true;
        }

        static bool BuildElements(IList<SpriteDrawingElement> elements, Vector2 center,
            Color color, float scale, out List<MySprite> sprites, out string reason)
        {
            sprites = new List<MySprite>();
            reason = "";
            if (elements == null || !Finite(scale) || !Finite(center))
            { reason = "Invalid drawing arguments."; return false; }
            for (int i = 0; i < elements.Count; i++)
            {
                var e = elements[i];
                var row = new Row { Fields = 1, Data = e.Data, Font = e.FontId ?? "Debug",
                    Color = e.Color ?? color, Alignment = e.Alignment ?? TextAlignment.CENTER,
                    Position = e.Position ?? Vector2.Zero, Size = e.Size ?? Vector2.Zero,
                    Rotation = e.RotationOrScale ?? 0 };
                switch (e.Kind)
                {
                    case SpriteDrawingKind.Texture: row.Type = "TEXTURE"; break;
                    case SpriteDrawingKind.Text: row.Type = "TEXT"; break;
                    case SpriteDrawingKind.ClipRect: row.Type = "CLIP_RECT"; break;
                    case SpriteDrawingKind.ClearClipRect: row.Type = "CLEAR_CLIP_RECT"; break;
                    default: reason = "Element " + (i + 1) + ": Invalid kind."; return false;
                }
                if (e.Data != null) row.Fields |= 2;
                if (e.Position.HasValue) row.Fields |= 4;
                if (e.Size.HasValue) row.Fields |= 8;
                if (e.Color.HasValue) row.Fields |= 16;
                if (e.FontId != null) row.Fields |= 32;
                if (e.Alignment.HasValue) row.Fields |= 64;
                if (e.RotationOrScale.HasValue) row.Fields |= 128;
                if (!Finite(row.Position) || !Finite(row.Size) || !Finite(row.Rotation) ||
                    row.Font.Length == 0 || (row.Alignment != TextAlignment.LEFT &&
                    row.Alignment != TextAlignment.CENTER && row.Alignment != TextAlignment.RIGHT))
                { reason = "Element " + (i + 1) + ": Invalid attribute value."; return false; }
                string error;
                if (!Add(row, center, scale, sprites, out error))
                { reason = "Element " + (i + 1) + ": " + error; return false; }
            }
            return true;
        }

        static bool ResolveSurface(IMyTerminalBlock block, int surfaceIndex,
            out IMyTextSurface surface, out string reason)
        {
            reason = "";
            surface = null;
            var provider = block as IMyTextSurfaceProvider;
            if (provider != null)
            {
                if (surfaceIndex < 0 || surfaceIndex >= provider.SurfaceCount)
                { reason = "Invalid surface index."; return false; }
                surface = provider.GetSurface(surfaceIndex);
            }
            else
            {
                surface = block as IMyTextSurface;
                if (surfaceIndex != 0) { reason = "Invalid surface index."; return false; }
            }
            if (surface == null) { reason = "No text surface."; return false; }
            return true;
        }

        static void Submit(IMyTextSurface surface, List<MySprite> sprites)
        {
            surface.ContentType = ContentType.SCRIPT;
            surface.Script = "";
            using (var frame = surface.DrawFrame())
                foreach (var sprite in sprites) frame.Add(sprite);
        }

        struct Row
        {
            public int Fields;
            public string Type, Data, Font;
            public Vector2 Position, Size;
            public Color Color;
            public TextAlignment Alignment;
            public float Rotation;
        }

        static bool Add(Row row, Vector2 center, float scale, List<MySprite> sprites, out string error)
        {
            error = "Missing or inapplicable attributes.";
            int required, allowed;
            switch (row.Type)
            {
                case "TEXTURE": required = 11; allowed = 223; break;
                case "TEXT": required = 3; allowed = 247; break;
                case "CLIP_RECT": required = allowed = 13; break;
                case "CLEAR_CLIP_RECT": required = allowed = 1; break;
                default: error = "Missing or invalid Type."; return false;
            }
            if ((row.Fields & required) != required || (row.Fields & ~allowed) != 0) return false;
            if (row.Type == "CLEAR_CLIP_RECT")
            { sprites.Add(MySprite.CreateClearClipRect()); error = ""; return true; }
            if (row.Type == "TEXTURE" && row.Data.Length == 0)
            { error = "Empty texture name."; return false; }
            if (row.Type == "TEXT")
            {
                if ((row.Fields & 128) == 0) row.Rotation = 1;
                if (row.Rotation <= 0) { error = "Invalid text scale."; return false; }
                row.Rotation *= scale;
            }
            if (row.Type == "CLIP_RECT" && (row.Size.X < 0 || row.Size.Y < 0))
            { error = "Negative input clip size."; return false; }
            bool skipClip = row.Type == "CLIP_RECT" && (row.Size.X == 0 || row.Size.Y == 0);
            row.Position = center + row.Position * scale;
            row.Size *= scale;
            if (!Finite(row.Position) || !Finite(row.Size) || !Finite(row.Rotation))
            { error = "Non-finite transformed value."; return false; }
            // Only an input zero skips a clip; computed zeros are passed through.
            if (skipClip) { error = ""; return true; }
            if (row.Type == "CLIP_RECT")
            {
                int x, y, w, h;
                if (!Integer(row.Position.X, out x) || !Integer(row.Position.Y, out y) ||
                    !Integer(row.Size.X, out w) || !Integer(row.Size.Y, out h) ||
                    (long)x + w < int.MinValue || (long)x + w > int.MaxValue ||
                    (long)y + h < int.MinValue || (long)y + h > int.MaxValue)
                { error = "Clip rectangle out of range."; return false; }
                sprites.Add(MySprite.CreateClipRect(new Rectangle(x, y, w, h)));
            }
            else
            {
                sprites.Add(new MySprite {
                    Type = row.Type == "TEXT" ? SpriteType.TEXT : SpriteType.TEXTURE,
                    Data = row.Data, Position = row.Position,
                    Size = row.Type == "TEXT" ? (Vector2?)null : row.Size,
                    Color = row.Color, FontId = row.Type == "TEXT" ? row.Font : null,
                    Alignment = row.Alignment, RotationOrScale = row.Rotation
                });
            }
            error = "";
            return true;
        }

        static bool Integer(float value, out int result)
        {
            double rounded = Math.Round((double)value, MidpointRounding.AwayFromZero);
            result = 0;
            if (rounded < int.MinValue || rounded > int.MaxValue) return false;
            result = (int)rounded;
            return true;
        }

        static bool Finite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }
        static bool Finite(Vector2 value) { return Finite(value.X) && Finite(value.Y); }
    }
}
