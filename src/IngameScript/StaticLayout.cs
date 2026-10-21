using System;
using System.Collections.Generic;

namespace IngameScript
{
    public sealed class LayoutElement
    {
        public string Id, Data, FontId = "Debug", Alignment = "CENTER";
        public bool IsText;
        public VRageMath.Vector2 Position, Size;
        public VRageMath.Color? Color;
        public float AngleDegrees, TextScale = 1;
    }

    public static class StaticLayout
    {
        public const int MaxLine = 2048, MaxData = 256;

        internal sealed class Attribute
        {
            public string Value;
            public bool Quoted;
        }

        public static bool TryParse(string source,
            out List<LayoutElement> elements, out string reason)
        {
            elements = null; reason = "";
            if (source == null || source.Length > SurfaceInfo.MaxCustomData)
                return Reject("Invalid Layout input.", out reason);
            var result = new List<LayoutElement>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            int lineNumber = 0;
            foreach (string raw in source.Split('\n'))
            {
                lineNumber++;
                string line;
                if (!StaticLayout.TryLine(raw, out line))
                    return LineError(lineNumber, "Invalid physical line or more than 2048 characters.", out reason);
                if (string.IsNullOrWhiteSpace(line)) continue;
                if (result.Count >= SurfacePass.MaxElements) return LineError(lineNumber, "Surface element limit exceeded.", out reason);
                Dictionary<string, Attribute> attributes;
                LayoutElement element;
                string detail;
                if (!TryAttributes(line, out attributes, out detail) ||
                    !TryElement(attributes, out element, out detail)) return LineError(lineNumber, detail, out reason);
                if (!ids.Add(element.Id)) return LineError(lineNumber, "Duplicate element Id.", out reason);
                result.Add(element);
            }
            elements = result;
            return true;
        }

        internal static bool TryLine(string raw, out string line)
        {
            line = raw.EndsWith("\r", StringComparison.Ordinal) ? raw.Substring(0, raw.Length - 1) : raw;
            return line.Length <= MaxLine && line.IndexOf('\r') < 0;
        }

        internal static bool TryAttributes(string line, out Dictionary<string, Attribute> attributes, out string reason)
        {
            attributes = new Dictionary<string, Attribute>(StringComparer.Ordinal); reason = "";
            int position = 0;
            while (position < line.Length)
            {
                int start = position;
                while (position < line.Length && line[position] != '=' && line[position] != ';') position++;
                if (position == line.Length || line[position] != '=') return Reject("Expected attribute=value.", out reason);
                string name = line.Substring(start, position - start).Trim();
                if (name.Length == 0 || attributes.ContainsKey(name)) return Reject("Empty or duplicate attribute.", out reason);
                position++;
                while (position < line.Length && char.IsWhiteSpace(line[position])) position++;
                bool quoted = position < line.Length && line[position] == '"';
                string value;
                if (quoted)
                {
                    position++;
                    var text = new System.Text.StringBuilder();
                    bool closed = false;
                    while (position < line.Length)
                    {
                        char c = line[position++];
                        if (c == '"') { closed = true; break; }
                        if (c == '\\')
                        {
                            if (position == line.Length) return Reject("Incomplete escape.", out reason);
                            c = line[position++];
                            switch (c)
                            {
                                case '"': case '\\': break;
                                case 'n': c = '\n'; break;
                                case 'r': c = '\r'; break;
                                case 't': c = '\t'; break;
                                default: return Reject("Unsupported escape.", out reason);
                            }
                        }
                        text.Append(c);
                    }
                    if (!closed) return Reject("Unclosed string.", out reason);
                    value = text.ToString();
                    while (position < line.Length && char.IsWhiteSpace(line[position])) position++;
                    if (position < line.Length && line[position] != ';') return Reject("Unexpected text after string.", out reason);
                }
                else
                {
                    start = position;
                    while (position < line.Length && line[position] != ';') position++;
                    value = line.Substring(start, position - start).Trim();
                    if (value.IndexOf('"') >= 0) return Reject("Unexpected quote in unquoted value.", out reason);
                }
                attributes.Add(name, new Attribute { Value = value, Quoted = quoted });
                if (position < line.Length) position++;
                // One trailing semicolon is allowed, including following whitespace.
                int remainder = position;
                while (remainder < line.Length && char.IsWhiteSpace(line[remainder])) remainder++;
                if (remainder == line.Length) break;
            }
            return true;
        }

        static bool TryElement(Dictionary<string, Attribute> attributes, out LayoutElement element, out string reason)
        {
            element = null; reason = "";
            Attribute id, type, data;
            if (!attributes.TryGetValue("Id", out id) || !attributes.TryGetValue("Type", out type) ||
                !attributes.TryGetValue("Data", out data)) return Reject("Id, Type and Data are required.", out reason);
            if (id.Quoted || !ValidId(id.Value)) return Reject("Invalid Id.", out reason);
            if (type.Quoted || (type.Value != "TEXT" && type.Value != "TEXTURE")) return Reject("Unsupported Type.", out reason);
            bool isText = type.Value == "TEXT";
            if (!data.Quoted || data.Value.Length > MaxData || (!isText && data.Value.Length == 0))
                return Reject("Data must be a quoted string of at most 256 characters.", out reason);
            var result = new LayoutElement { Id = id.Value, IsText = isText, Data = data.Value };
            bool hasSize = false;
            foreach (var pair in attributes)
            {
                string name = pair.Key;
                var attribute = pair.Value;
                string value = attribute.Value;
                if (name == "Id" || name == "Type" || name == "Data") continue;
                if (attribute.Quoted != (name == "FontId")) return Reject("Incorrect attribute value type.", out reason);
                switch (name)
                {
                    case "Position":
                        if (!Vector(value, out result.Position)) return Reject("Invalid Position.", out reason);
                        break;
                    case "Size":
                        if (isText || !Vector(value, out result.Size)) return Reject("Invalid Size or Size on TEXT.", out reason);
                        hasSize = true; break;
                    case "AngleDegrees":
                        if (isText || !Number(value, out result.AngleDegrees)) return Reject("Invalid AngleDegrees or angle on TEXT.", out reason);
                        break;
                    case "TextScale":
                        if (!isText || !Number(value, out result.TextScale) || result.TextScale <= 0)
                            return Reject("TextScale must be positive on TEXT.", out reason);
                        break;
                    case "FontId":
                        if (!isText || value.Length == 0) return Reject("FontId must be nonempty on TEXT.", out reason);
                        result.FontId = value; break;
                    case "Alignment":
                        if (value != "CENTER" && (!isText || (value != "LEFT" && value != "RIGHT")))
                            return Reject("Invalid Alignment.", out reason);
                        result.Alignment = value; break;
                    case "Color":
                        VRageMath.Color color;
                        if (!TryColor(value, out color)) return Reject("Color requires four integers from 0 through 255.", out reason);
                        result.Color = color; break;
                    default: return Reject("Unknown Layout attribute.", out reason);
                }
            }
            if (!isText && !hasSize) return Reject("Size is required on TEXTURE.", out reason);
            element = result;
            return true;
        }

        static bool ValidId(string value)
        {
            if (value.Length < 1 || value.Length > 32 || !Letter(value[0])) return false;
            foreach (char c in value) if (!Letter(c) && (c < '0' || c > '9') && c != '_') return false;
            return true;
        }

        static bool Letter(char c) => (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z');

        static bool Number(string value, out float number) => float.TryParse(value, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out number) && !float.IsNaN(number) && !float.IsInfinity(number);

        internal static bool Vector(string value, out VRageMath.Vector2 vector)
        {
            vector = default(VRageMath.Vector2);
            var parts = value.Split(',');
            float x, y;
            if (parts.Length != 2 || !Number(parts[0], out x) || !Number(parts[1], out y)) return false;
            vector = new VRageMath.Vector2(x, y); return true;
        }

        internal static bool TryColor(string value, out VRageMath.Color color)
        {
            color = default(VRageMath.Color);
            var parts = value.Split(',');
            if (parts.Length != 4) return false;
            var components = new int[4];
            for (int i = 0; i < 4; i++)
                if (!int.TryParse(parts[i], System.Globalization.NumberStyles.Integer,
                    System.Globalization.CultureInfo.InvariantCulture, out components[i]) || components[i] < 0 || components[i] > 255) return false;
            color = new VRageMath.Color(components[0], components[1], components[2], components[3]); return true;
        }

        static bool LineError(int line, string detail, out string reason) => Reject("Layout line " + line.ToString(System.Globalization.CultureInfo.InvariantCulture) + ": " + detail, out reason);

        static bool Reject(string message, out string reason)
        { reason = message; return false; }
    }
}
