using System;
using System.Collections.Generic;

namespace IngameScript
{
    public enum CustomDataSaveStatus { Unchanged, Saved, Conflict, Failed }

    public sealed class CustomDataDocument
    {
        readonly string source;
        readonly VRage.Game.ModAPI.Ingame.Utilities.MyIni original;
        readonly VRage.Game.ModAPI.Ingame.Utilities.MyIni edited;
        readonly HashSet<VRage.Game.ModAPI.Ingame.Utilities.MyIniKey> owned;
        readonly HashSet<string> allowed;
        readonly List<string> tokenLines;
        readonly HashSet<string> consumed = new HashSet<string>(StringComparer.Ordinal);
        readonly string tail;
        bool dirty, ended;

        CustomDataDocument(string source,
            VRage.Game.ModAPI.Ingame.Utilities.MyIni original,
            VRage.Game.ModAPI.Ingame.Utilities.MyIni edited,
            HashSet<VRage.Game.ModAPI.Ingame.Utilities.MyIniKey> owned,
            HashSet<string> allowed, List<string> tokenLines, string tail)
        {
            this.source = source; this.original = original; this.edited = edited;
            this.owned = owned; this.allowed = allowed;
            this.tokenLines = tokenLines; this.tail = tail;
        }

        public static bool TryParse(string source,
            VRage.Game.ModAPI.Ingame.Utilities.MyIniKey[] ownedKeys, string[] setupTokens,
            out CustomDataDocument document, out string reason)
        {
            document = null; reason = "";
            if (source == null || ownedKeys == null || setupTokens == null)
                return Fail("Null document input.", out reason);
            var owned = new HashSet<VRage.Game.ModAPI.Ingame.Utilities.MyIniKey>();
            foreach (var key in ownedKeys)
            {
                if (key.IsEmpty) return Fail("Empty owned key.", out reason);
                owned.Add(key);
            }
            var allowed = new HashSet<string>(StringComparer.Ordinal);
            foreach (var token in setupTokens)
            {
                if (string.IsNullOrWhiteSpace(token) || token != token.Trim() ||
                    token.IndexOfAny(new[] { '\r', '\n', '=', '[', ']', ';', '|' }) >= 0 || token == "---")
                    return Fail("Invalid setup token.", out reason);
                allowed.Add(token);
            }
            var tokens = new List<string>();
            var clean = new System.Text.StringBuilder();
            var sections = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var keys = new HashSet<VRage.Game.ModAPI.Ingame.Utilities.MyIniKey>();
            string section = "", tail = "";
            for (int start = 0; start < source.Length;)
            {
                int end = source.IndexOf('\n', start);
                if (end < 0) end = source.Length;
                int next = end < source.Length ? end + 1 : end;
                string line = source.Substring(start, end - start).Trim();
                if (line == "---") { tail = source.Substring(start); break; }
                if (allowed.Contains(line))
                {
                    tokens.Add(source.Substring(start, next - start));
                    clean.Append('\n');
                }
                else
                {
                    if (line.StartsWith("[", StringComparison.Ordinal) && line.EndsWith("]", StringComparison.Ordinal))
                    {
                        section = line.Substring(1, line.Length - 2);
                        if (!sections.Add(section)) return Fail("Duplicate INI section.", out reason);
                    }
                    else if (line.Length > 0 && line[0] != ';' && line[0] != '|')
                    {
                        int equals = line.IndexOf('=');
                        if (equals >= 0 && section.Length > 0)
                        {
                            VRage.Game.ModAPI.Ingame.Utilities.MyIniKey key;
                            try { key = new VRage.Game.ModAPI.Ingame.Utilities.MyIniKey(section, line.Substring(0, equals).Trim()); }
                            catch (Exception) { return Fail("Invalid INI key.", out reason); }
                            if (!keys.Add(key)) return Fail("Duplicate INI key.", out reason);
                        }
                    }
                    clean.Append(source, start, next - start);
                }
                start = next;
            }
            var original = new VRage.Game.ModAPI.Ingame.Utilities.MyIni();
            var edited = new VRage.Game.ModAPI.Ingame.Utilities.MyIni();
            if (!original.TryParse(clean.ToString()) || !edited.TryParse(clean.ToString()))
                return Fail("Invalid INI document.", out reason);
            document = new CustomDataDocument(source, original, edited, owned, allowed, tokens, tail);
            return true;
        }

        public bool TryGet(VRage.Game.ModAPI.Ingame.Utilities.MyIniKey key, out string value)
        {
            value = null;
            if (key.IsEmpty || !edited.ContainsKey(key)) return false;
            value = edited.Get(key).ToString(); return true;
        }

        public bool HasSetupToken(string token)
        {
            if (token == null || !allowed.Contains(token) || consumed.Contains(token)) return false;
            foreach (var line in tokenLines) if (line.Trim() == token) return true;
            return false;
        }

        public bool TrySeedMissing(VRage.Game.ModAPI.Ingame.Utilities.MyIniKey key,
            string value, string comment, out string reason)
        {
            if (!CanEdit(key, value, out reason)) return false;
            if (edited.ContainsKey(key)) return true;
            edited.Set(key, value);
            if (comment != null) edited.SetComment(key, comment);
            dirty = true; return true;
        }

        public bool TrySetOwned(VRage.Game.ModAPI.Ingame.Utilities.MyIniKey key,
            string value, out string reason)
        {
            if (!CanEdit(key, value, out reason)) return false;
            if (edited.ContainsKey(key) && edited.Get(key).ToString() == value) return true;
            edited.Set(key, value); dirty = true; return true;
        }

        public bool TryConsumeSetupToken(string token, out string reason)
        {
            reason = "";
            if (ended) return Fail("Document is closed. Reparse current data.", out reason);
            if (token == null || !allowed.Contains(token)) return Fail("Unknown setup token.", out reason);
            if (HasSetupToken(token)) { consumed.Add(token); dirty = true; }
            return true;
        }

        public bool TryBuild(out string candidate, out string reason)
        {
            candidate = null; reason = "";
            if (ended) return Fail("Document is closed. Reparse current data.", out reason);
            if (!dirty || (consumed.Count == 0 && SameDocument(original, edited)))
            { candidate = source; return true; }
            if (!Preserved(original, edited, owned)) return Fail("Document preservation failed.", out reason);
            var text = new System.Text.StringBuilder();
            foreach (var line in tokenLines)
                if (!consumed.Contains(line.Trim()))
                {
                    text.Append(line);
                    if (!line.EndsWith("\n", StringComparison.Ordinal)) text.Append('\n');
                }
            string iniText = edited.ToString();
            text.Append(iniText);
            if (tail.Length > 0)
            {
                if (text.Length > 0 && text[text.Length - 1] != '\n') text.Append('\n');
                text.Append(tail);
            }
            var roundTrip = new VRage.Game.ModAPI.Ingame.Utilities.MyIni();
            if (!roundTrip.TryParse(iniText) || !Preserved(edited, roundTrip,
                new HashSet<VRage.Game.ModAPI.Ingame.Utilities.MyIniKey>()))
                return Fail("Output preservation failed.", out reason);
            candidate = text.ToString(); return true;
        }

        public CustomDataSaveStatus TrySave(Func<string> read, Action<string> write, out string reason)
        {
            string candidate;
            if (!TryBuild(out candidate, out reason)) return CustomDataSaveStatus.Failed;
            if (candidate == source) return CustomDataSaveStatus.Unchanged;
            ended = true;
            if (read == null || write == null)
            { reason = "Null save callback."; return CustomDataSaveStatus.Failed; }
            string operation = "CustomData read failed before save.";
            try
            {
                if (read() != source)
                { reason = "CustomData changed before save."; return CustomDataSaveStatus.Conflict; }
                operation = "CustomData write failed.";
                write(candidate);
                operation = "CustomData readback failed.";
                if (read() != candidate)
                { reason = "CustomData readback mismatch."; return CustomDataSaveStatus.Failed; }
                return CustomDataSaveStatus.Saved;
            }
            catch (Exception)
            { reason = operation; return CustomDataSaveStatus.Failed; }
        }

        bool CanEdit(VRage.Game.ModAPI.Ingame.Utilities.MyIniKey key, string value, out string reason)
        {
            reason = "";
            if (ended) return Fail("Document is closed. Reparse current data.", out reason);
            if (key.IsEmpty || !owned.Contains(key)) return Fail("Key is not owned.", out reason);
            if (value == null) return Fail("Null value.", out reason);
            return true;
        }

        static bool Preserved(VRage.Game.ModAPI.Ingame.Utilities.MyIni before,
            VRage.Game.ModAPI.Ingame.Utilities.MyIni after,
            HashSet<VRage.Game.ModAPI.Ingame.Utilities.MyIniKey> mutable)
        {
            var sections = new List<string>(); before.GetSections(sections);
            foreach (var section in sections)
                if (!after.ContainsSection(section) || before.GetSectionComment(section) != after.GetSectionComment(section))
                    return false;
            var keys = new List<VRage.Game.ModAPI.Ingame.Utilities.MyIniKey>(); before.GetKeys(keys);
            foreach (var key in keys)
                if (!after.ContainsKey(key) || before.GetComment(key) != after.GetComment(key) ||
                    (!mutable.Contains(key) && before.Get(key).ToString() != after.Get(key).ToString()))
                    return false;
            return (before.EndComment ?? "").TrimEnd('\r', '\n') ==
                (after.EndComment ?? "").TrimEnd('\r', '\n') && before.EndContent == after.EndContent;
        }

        static bool SameDocument(VRage.Game.ModAPI.Ingame.Utilities.MyIni before,
            VRage.Game.ModAPI.Ingame.Utilities.MyIni after)
        {
            var first = new List<VRage.Game.ModAPI.Ingame.Utilities.MyIniKey>();
            var second = new List<VRage.Game.ModAPI.Ingame.Utilities.MyIniKey>();
            before.GetKeys(first); after.GetKeys(second);
            var firstSections = new List<string>(); var secondSections = new List<string>();
            before.GetSections(firstSections); after.GetSections(secondSections);
            return first.Count == second.Count && firstSections.Count == secondSections.Count &&
                Preserved(before, after, new HashSet<VRage.Game.ModAPI.Ingame.Utilities.MyIniKey>());
        }

        static bool Fail(string message, out string reason) { reason = message; return false; }
    }
}
