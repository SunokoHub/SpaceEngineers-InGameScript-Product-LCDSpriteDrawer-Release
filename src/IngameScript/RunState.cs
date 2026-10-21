using System;

namespace IngameScript
{
    // Private PB Storage contract. Configuration versions are independent.
    public sealed class RunStateStore
    {
        const string Header = "LCDSpriteDrawer\nStateVersion=1\nRunning=";
        public bool Usable { get; private set; }
        public bool Running { get; private set; }
        public bool StatusColorsInitialized { get; private set; }

        public bool Load(string source, out string reason)
        {
            Usable = false; Running = false; StatusColorsInitialized = false; reason = "";
            if (source == "") { Usable = true; return true; }
            if (source == null || source.Length > 256)
            { reason = "Invalid saved run state. Use stop to reset private Storage."; return false; }
            string value = source.Replace("\r\n", "\n");
            const string colors = "StatusColorsInitialized=true\n";
            bool initialized = value.EndsWith(colors, StringComparison.Ordinal);
            if (initialized) value = value.Substring(0, value.Length - colors.Length);
            if (value != Encode(false) && value != Encode(true))
            { reason = "Unsupported or corrupt saved run state. Use stop to reset private Storage."; return false; }
            Running = value == Encode(true); StatusColorsInitialized = initialized; Usable = true;
            return true;
        }

        public bool InitializeStatusColors(Func<string> read, Action<string> write, out string reason)
        {
            if (!Usable) { reason = "Saved run state is locked."; return false; }
            StatusColorsInitialized = true;
            return Commit(Running, read, write, out reason);
        }

        public bool Commit(bool running, Func<string> read, Action<string> write,
            out string reason, bool reset = false)
        {
            reason = "";
            if (!Usable && !reset)
            { reason = "Saved run state is locked. Use stop to reset private Storage."; return false; }
            if (reset && running)
            { reason = "Only Stopped can reset private Storage."; return false; }
            try
            {
                string candidate = Encode(running) + (StatusColorsInitialized ? "StatusColorsInitialized=true\n" : "");
                if (read() != candidate)
                {
                    write(candidate);
                    if (read() != candidate) throw new Exception();
                }
                Running = running; Usable = true;
                return true;
            }
            catch (Exception)
            {
                Running = false; Usable = false;
                reason = "Run state save failed; previous Storage may remain. Use stop before retrying.";
                return false;
            }
        }

        public static string Encode(bool running) => Header + (running ? "true\n" : "false\n");
    }
}
