using System;
using System.Collections.Generic;
using Sandbox.ModAPI.Ingame;

namespace IngameScript
{
    public sealed class SurfaceInfoRecord
    {
        public string DisplayName;
        public VRageMath.Vector2 TextureSize, SurfaceSize;
        public VRageMath.Color ForegroundColor, BackgroundColor;
    }

    public static class SurfaceInfo
    {
        public const string Tag = "LCDSpriteDrawer";
        public const int MaxCustomData = 32000, MaxOutput = 8000;

        public static bool TryParseInfoCommand(string argument, out long blockId)
        {
            blockId = 0;
            if (argument == null || !argument.StartsWith("info ", StringComparison.Ordinal)) return false;
            string number = argument.Substring(5);
            if (number.Length == 0 || number.Length > 19) return false;
            foreach (char c in number) if (c < '0' || c > '9') return false;
            return long.TryParse(number, System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture, out blockId) && blockId > 0;
        }

        public static BlockSelectionStatus Evaluate(string source, out string reason)
        {
            reason = "";
            if (source == null || source.Length > MaxCustomData)
            { reason = "Missing or oversized CustomData."; return BlockSelectionStatus.Invalid; }
            CustomDataDocument document;
            if (!CustomDataDocument.TryParse(source,
                new VRage.Game.ModAPI.Ingame.Utilities.MyIniKey[0], new[] { Tag }, out document, out reason))
                return BlockSelectionStatus.Invalid;
            return BlockSelection.Evaluate(document,
                new VRage.Game.ModAPI.Ingame.Utilities.MyIniKey("Tags", Tag), Tag, out reason);
        }

        public static bool TryReadBlock(IMyTerminalBlock block, IMyTerminalBlock pb,
            out string output, out string reason)
        {
            output = null; reason = "";
            if (block == null || pb == null) return Reject("Block not found.", out reason);
            if (!block.IsSameConstructAs(pb)) return Reject("Block is outside this construct.", out reason);
            if (Evaluate(block.CustomData, out reason) != BlockSelectionStatus.Selected)
            { if (reason.Length == 0) reason = "Block is not selected."; return false; }
            var provider = block as IMyTextSurfaceProvider;
            var direct = block as IMyTextSurface;
            int count = provider != null ? provider.SurfaceCount : direct != null ? 1 : 0;
            if (count < 1) return Reject("No text surfaces available.", out reason);
            var surfaces = new List<SurfaceInfoRecord>();
            for (int i = 0; i < count; i++)
            {
                var surface = provider != null ? provider.GetSurface(i) : direct;
                if (surface == null) return Reject("Surface is unavailable.", out reason);
                surfaces.Add(new SurfaceInfoRecord {
                    DisplayName = surface.DisplayName, TextureSize = surface.TextureSize,
                    SurfaceSize = surface.SurfaceSize, ForegroundColor = surface.ScriptForegroundColor,
                    BackgroundColor = surface.ScriptBackgroundColor
                });
            }
            return TryBuild(block.EntityId, block.CustomName, block.BlockDefinition.ToString(),
                surfaces, out output, out reason);
        }

        public static bool TryBuild(long blockId, string blockName, string blockType,
            List<SurfaceInfoRecord> surfaces, out string output, out string reason)
        {
            output = null; reason = "";
            if (blockId <= 0 || blockName == null || blockType == null || surfaces == null ||
                surfaces.Count < 1)
                return Reject("Invalid block metadata or surface count.", out reason);
            var ini = new VRage.Game.ModAPI.Ingame.Utilities.MyIni();
            ini.Set("SurfaceInfo", "SurfaceInfoFormatVersion", 1);
            ini.Set("SurfaceInfo", "BlockId", blockId);
            ini.Set("SurfaceInfo", "BlockName", blockName);
            ini.Set("SurfaceInfo", "BlockType", blockType);
            ini.Set("SurfaceInfo", "SurfaceCount", surfaces.Count);
            for (int i = 0; i < surfaces.Count; i++)
            {
                var surface = surfaces[i];
                if (surface == null || surface.DisplayName == null ||
                    !Finite(surface.TextureSize) || !Finite(surface.SurfaceSize))
                    return Reject("Invalid surface metadata.", out reason);
                string section = "SurfaceInfo.Surface." + i.ToString(System.Globalization.CultureInfo.InvariantCulture);
                ini.Set(section, "DisplayName", surface.DisplayName);
                ini.Set(section, "TextureSize", Vector(surface.TextureSize));
                ini.Set(section, "SurfaceSize", Vector(surface.SurfaceSize));
                ini.Set(section, "ForegroundColor", Color(surface.ForegroundColor));
                ini.Set(section, "BackgroundColor", Color(surface.BackgroundColor));
            }
            string candidate = ini.ToString();
            if (candidate.Length > MaxOutput) return Reject("Surface info exceeds 8000 characters.", out reason);
            output = candidate;
            return true;
        }

        static bool Finite(VRageMath.Vector2 value) => !float.IsNaN(value.X) && !float.IsInfinity(value.X) && !float.IsNaN(value.Y) && !float.IsInfinity(value.Y);

        static string Vector(VRageMath.Vector2 value) => value.X.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + "," +
            value.Y.ToString("R", System.Globalization.CultureInfo.InvariantCulture);

        static string Color(VRageMath.Color value) => value.R.ToString(System.Globalization.CultureInfo.InvariantCulture) + "," +
            value.G.ToString(System.Globalization.CultureInfo.InvariantCulture) + "," +
            value.B.ToString(System.Globalization.CultureInfo.InvariantCulture) + "," +
            value.A.ToString(System.Globalization.CultureInfo.InvariantCulture);

        static bool Reject(string message, out string reason)
        { reason = message; return false; }
    }
}
