using System;

namespace IngameScript
{
    public enum BlockSelectionStatus { Selected, NotSelected, Invalid }

    public static class BlockSelection
    {
        public static BlockSelectionStatus Evaluate(CustomDataDocument document,
            VRage.Game.ModAPI.Ingame.Utilities.MyIniKey tagKey, string bareTag, out string reason)
        {
            reason = "";
            if (document == null || tagKey.IsEmpty || string.IsNullOrWhiteSpace(bareTag) ||
                bareTag != bareTag.Trim() || bareTag == "---" ||
                bareTag.IndexOfAny(new[] { '\r', '\n', '=', '[', ']', ';', '|' }) >= 0)
            { reason = "Invalid selection input."; return BlockSelectionStatus.Invalid; }
            string value;
            if (document.TryGet(tagKey, out value))
            {
                bool selected;
                if (!bool.TryParse(value, out selected))
                { reason = "Invalid tag value: expected true or false."; return BlockSelectionStatus.Invalid; }
                return selected ? BlockSelectionStatus.Selected : BlockSelectionStatus.NotSelected;
            }
            return document.HasSetupToken(bareTag) ? BlockSelectionStatus.Selected : BlockSelectionStatus.NotSelected;
        }

        public static bool PlanSetup(CustomDataDocument document,
            VRage.Game.ModAPI.Ingame.Utilities.MyIniKey tagKey, string bareTag, out string reason)
        {
            var status = Evaluate(document, tagKey, bareTag, out reason);
            if (status != BlockSelectionStatus.Selected)
            { if (reason.Length == 0) reason = "Tag is not selected."; return false; }
            return document.TrySeedMissing(tagKey, "true", null, out reason) &&
                document.TryConsumeSetupToken(bareTag, out reason);
        }
    }
}
