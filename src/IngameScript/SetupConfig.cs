using System;
using System.Collections.Generic;

namespace IngameScript
{
    public sealed class SetupPlan
    {
        public CustomDataDocument Document;
        public int SurfaceCount;
        public Dictionary<int, List<LayoutElement>> Layouts;
        public Dictionary<int, MotionSet> Motions;
        public Dictionary<int, BindingSet> Bindings;
        public Func<string> Read;
        public Action<string> Write;
        internal List<int> Surfaces;
        internal List<VRage.Game.ModAPI.Ingame.Utilities.MyIniKey> Keys;
        internal string Source;
        internal bool Setup;
        internal int ParseStep;
        public bool Complete;
    }

    public static class SetupConfig
    {
        const string Settings = "Settings.LCDSpriteDrawer";

        public static bool PreservePbDisplay(string source)
        {
            if (!HasSelectionIntent(source)) return false;
            CustomDataDocument document;
            string reason, value;
            if (!CustomDataDocument.TryParse(source, new VRage.Game.ModAPI.Ingame.Utilities.MyIniKey[0],
                new[] { SurfaceInfo.Tag }, out document, out reason)) return true;
            var selection = BlockSelection.Evaluate(document, Key("Tags", SurfaceInfo.Tag), SurfaceInfo.Tag, out reason);
            if (selection == BlockSelectionStatus.NotSelected) return false;
            if (selection == BlockSelectionStatus.Invalid || !document.TryGet(Key(Settings, "Surfaces"), out value)) return true;
            foreach (string item in value.Split(','))
            {
                long index;
                if (!NonnegativeInteger(item, out index) || index == 0) return true;
            }
            return false;
        }

        public static bool ValidatePbDocument(string source, out string reason)
        {
            reason = "";
            if (source == null || source.Length > SurfaceInfo.MaxCustomData)
                return Reject("Missing or oversized PB CustomData.", out reason);
            CustomDataDocument document;
            return CustomDataDocument.TryParse(source,
                new VRage.Game.ModAPI.Ingame.Utilities.MyIniKey[0], new[] { SurfaceInfo.Tag },
                out document, out reason);
        }

        // Candidate discovery only. Selection still uses the pinned module.
        public static bool HasSelectionIntent(string source)
        {
            if (source == null) return false;
            string section = "";
            foreach (string raw in source.Split('\n'))
            {
                string line = raw.Trim();
                if (line == "---") break;
                if (line == SurfaceInfo.Tag) return true;
                if (line.Length == 0 || line[0] == ';' || line[0] == '|') continue;
                if (line[0] == '[' && line.EndsWith("]", StringComparison.Ordinal))
                    section = line.Substring(1, line.Length - 2);
                else if (string.Equals(section, "Tags", StringComparison.OrdinalIgnoreCase))
                {
                    int equals = line.IndexOf('=');
                    if (equals >= 0 && string.Equals(line.Substring(0, equals).Trim(),
                        SurfaceInfo.Tag, StringComparison.OrdinalIgnoreCase)) return true;
                }
            }
            return false;
        }

        public static bool TryPlan(string source, long owner, int availableSurfaces,
            out SetupPlan plan, out string reason, bool setup = true)
        {
            if (!TryBeginPlan(source, owner, availableSurfaces, out plan, out reason, setup)) return false;
            while (plan != null && !plan.Complete)
                if (!TryContinuePlan(plan, out reason)) { plan = null; return false; }
            return true;
        }

        public static bool TryBeginPlan(string source, long owner, int availableSurfaces,
            out SetupPlan plan, out string reason, bool setup = true, string surfaceInfo = null)
        {
            plan = null; reason = "";
            if (source == null || source.Length > SurfaceInfo.MaxCustomData || owner <= 0)
                return Reject("Missing/oversized CustomData or invalid PB ID.", out reason);
            var keys = new List<VRage.Game.ModAPI.Ingame.Utilities.MyIniKey>();
            keys.Add(Key("Tags", SurfaceInfo.Tag));
            foreach (string name in new[] { "Owner", "ConfigFormatVersion", "Surfaces" })
                keys.Add(Key(Settings, name));
            for (int i = 0; i < availableSurfaces; i++)
                foreach (string name in new[] { "Layout", "Motion", "Binding" })
                    keys.Add(Key(Section(i), name));
            var metadata = new VRage.Game.ModAPI.Ingame.Utilities.MyIni();
            var metadataKeys = new List<VRage.Game.ModAPI.Ingame.Utilities.MyIniKey>();
            if (setup && surfaceInfo != null)
            {
                if (!metadata.TryParse(surfaceInfo)) return Reject("Invalid surface metadata.", out reason);
                metadata.GetKeys(metadataKeys);
                keys.AddRange(metadataKeys);
            }
            CustomDataDocument document;
            if (!CustomDataDocument.TryParse(source, setup ? keys.ToArray() : new VRage.Game.ModAPI.Ingame.Utilities.MyIniKey[0], new[] { SurfaceInfo.Tag },
                out document, out reason)) return false;
            var selection = BlockSelection.Evaluate(document, Key("Tags", SurfaceInfo.Tag), SurfaceInfo.Tag, out reason);
            if (selection == BlockSelectionStatus.Invalid) return false;
            if (selection == BlockSelectionStatus.NotSelected) return true;
            if (availableSurfaces < 1)
                return Reject("No text surfaces available.", out reason);
            string value;
            long parsedOwner;
            bool present = document.TryGet(Key(Settings, "Owner"), out value);
            if ((!present && !setup) || (present && (!PositiveInteger(value, out parsedOwner) || parsedOwner != owner)))
                return Reject("Owner is invalid or belongs to another PB.", out reason);
            long version;
            present = document.TryGet(Key(Settings, "ConfigFormatVersion"), out value);
            if ((!present && !setup) || (present && (!PositiveInteger(value, out version) || version != 1)))
                return Reject("ConfigFormatVersion must be 1.", out reason);
            var surfaces = new List<int>();
            if (!document.TryGet(Key(Settings, "Surfaces"), out value))
            {
                if (!setup) return Reject("Surfaces is required. Run setup first.", out reason);
                value = "0";
            }
            foreach (string item in value.Split(','))
            {
                long index;
                if (!NonnegativeInteger(item, out index) || index >= availableSurfaces ||
                    surfaces.Contains((int)index)) return Reject("Invalid or duplicate surface index.", out reason);
                surfaces.Add((int)index);
            }
            if (setup && (!BlockSelection.PlanSetup(document, Key("Tags", SurfaceInfo.Tag), SurfaceInfo.Tag, out reason) ||
                !document.TrySeedMissing(Key(Settings, "Owner"), owner.ToString(System.Globalization.CultureInfo.InvariantCulture), null, out reason) ||
                !document.TrySeedMissing(Key(Settings, "ConfigFormatVersion"), "1", null, out reason) ||
                !document.TrySeedMissing(Key(Settings, "Surfaces"), "0", null, out reason))) return false;
            foreach (var key in metadataKeys)
                if (!document.TrySetOwned(key, metadata.Get(key).ToString(), out reason)) return false;
            plan = new SetupPlan { Document = document, SurfaceCount = surfaces.Count,
                Layouts = new Dictionary<int, List<LayoutElement>>(), Motions = new Dictionary<int, MotionSet>(),
                Bindings = new Dictionary<int, BindingSet>(), Surfaces = surfaces, Keys = keys, Source = source, Setup = setup };
            return true;
        }

        // One Layout, Motion, Binding or final document validation per invocation.
        public static bool TryContinuePlan(SetupPlan plan, out string reason)
        {
            reason = "";
            if (plan == null || plan.Complete) return Reject("No pending configuration plan.", out reason);
            var document = plan.Document;
            bool setup = plan.Setup;
            if (plan.ParseStep < plan.Surfaces.Count * 3)
            {
                int index = plan.Surfaces[plan.ParseStep / 3];
                string name = new[] { "Layout", "Motion", "Binding" }[plan.ParseStep % 3];
                var key = Key(Section(index), name);
                string value;
                bool present = document.TryGet(key, out value);
                if (name == "Layout")
                {
                    if (!present && !setup) return Reject("Layout is required. Run setup first.", out reason);
                    if (!present) value = "";
                    List<LayoutElement> elements;
                    if (!StaticLayout.TryParse(value, out elements, out reason))
                    { reason = "Surface " + index.ToString(System.Globalization.CultureInfo.InvariantCulture) + ": " + reason; return false; }
                    plan.Layouts.Add(index, elements);
                }
                else if (name == "Motion")
                {
                    MotionSet motion;
                    if (!Motion.TryParse(value ?? "", plan.Layouts[index], out motion, out reason))
                    { reason = "Surface " + index + ": " + reason; return false; }
                    plan.Motions.Add(index, motion);
                }
                else
                {
                    BindingSet binding;
                    if (!Binding.TryParse(value ?? "", plan.Layouts[index], plan.Motions[index], out binding, out reason))
                    { reason = "Surface " + index + ": " + reason; return false; }
                    plan.Bindings.Add(index, binding);
                }
                if (setup && !document.TrySeedMissing(key, "", null, out reason)) return false;
                plan.ParseStep++;
                return true;
            }
            string candidate;
            if (setup)
            { if (!document.TryBuild(out candidate, out reason)) return false; }
            else candidate = WithoutBareTags(plan.Source);
            if (candidate.Length > SurfaceInfo.MaxCustomData)
                return Reject("Setup output exceeds 32000 characters.", out reason);
            var ini = new VRage.Game.ModAPI.Ingame.Utilities.MyIni();
            if (!ini.TryParse(candidate)) return Reject("Invalid setup candidate.", out reason);
            var candidateKeys = new List<VRage.Game.ModAPI.Ingame.Utilities.MyIniKey>();
            ini.GetKeys(candidateKeys);
            foreach (var key in candidateKeys)
            {
                bool active = string.Equals(key.Section, Settings, StringComparison.OrdinalIgnoreCase);
                foreach (int index in plan.Surfaces)
                    active |= string.Equals(key.Section, Section(index), StringComparison.OrdinalIgnoreCase);
                if (active && !plan.Keys.Contains(key)) return Reject("Unknown key in an active settings section.", out reason);
            }
            plan.Complete = true;
            plan.Source = null; plan.Keys = null; plan.Surfaces = null;
            return true;
        }

        // Parsing view only; the original document and opaque tail stay untouched.
        static string WithoutBareTags(string source)
        {
            var text = new System.Text.StringBuilder();
            bool tail = false;
            var lines = source.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                if (i > 0) text.Append('\n');
                if (line.Trim() == "---") tail = true;
                if (tail || line.Trim() != SurfaceInfo.Tag) text.Append(line);
            }
            return text.ToString();
        }

        // The caller validates each block immediately before saving it.
        public static bool TrySaveAll(List<SetupPlan> plans, out int saved, out string reason)
        {
            saved = 0; reason = "";
            foreach (var plan in plans)
            {
                if (plan == null || !plan.Complete) return Reject("Configuration validation is incomplete.", out reason);
                string candidate;
                if (!plan.Document.TryBuild(out candidate, out reason)) return false;
                if (candidate.Length > SurfaceInfo.MaxCustomData)
                    return Reject("Setup output exceeds 32000 characters.", out reason);
                var status = plan.Document.TrySave(plan.Read, plan.Write, out reason);
                if (status == CustomDataSaveStatus.Failed || status == CustomDataSaveStatus.Conflict) return false;
                if (status == CustomDataSaveStatus.Saved) saved++;
            }
            return true;
        }
        static bool PositiveInteger(string value, out long number) => NonnegativeInteger(value, out number) && number > 0;

        static bool NonnegativeInteger(string value, out long number)
        {
            number = 0;
            if (value == null) return false;
            value = value.Trim();
            if (value.Length == 0) return false;
            foreach (char c in value) if (c < '0' || c > '9') return false;
            return long.TryParse(value, System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture, out number);
        }

        static VRage.Game.ModAPI.Ingame.Utilities.MyIniKey Key(string section, string name) => new VRage.Game.ModAPI.Ingame.Utilities.MyIniKey(section, name);

        static string Section(int index) => Settings + ".Surface." + index.ToString(System.Globalization.CultureInfo.InvariantCulture);

        static bool Reject(string message, out string reason)
        { reason = message; return false; }
    }
}
