using System;

namespace ErenshorCraftingExpanded
{
    // Live GameData wiring around CraftingCharacterKey. This intentionally mirrors the already
    // live-used sibling-suite pattern instead of inventing a new save identifier.
    internal static class CraftingCharacterIdentity
    {
        internal static bool IsReady()
        {
            return SuiteUiPolicy.IsGameplayReady();
        }

        internal static string ResolveCharacterKey()
        {
            string name = PlayerName();
            if (string.IsNullOrWhiteSpace(name)) return string.Empty;
            int slot = ResolveSlotIndex();
            return CraftingCharacterKey.ResolveStrict(name, slot, CountMatchingRawNames(name), CountMatchingSafeKeys(name));
        }

        internal static string PlayerName()
        {
            try
            {
                string name = GameData.PlayerControl.Myself.MyStats.MyName;
                return string.IsNullOrWhiteSpace(name) ? string.Empty : name.Trim();
            }
            catch { return string.Empty; }
        }

        private static int ResolveSlotIndex()
        {
            try
            {
                SaveGameData active = GameData.CurrentCharacterSlot != null ? GameData.CurrentCharacterSlot : GameData.ActiveSaveSlot;
                if (active == null || active.index < 0) return -1;
                string recorded = (active.CharName ?? string.Empty).Trim();
                if (recorded.Length > 0 && !string.Equals(recorded, PlayerName(), StringComparison.OrdinalIgnoreCase)) return -1;
                return active.index;
            }
            catch { return -1; }
        }

        private static int CountMatchingRawNames(string playerName)
        {
            if (string.IsNullOrWhiteSpace(playerName)) return 0;
            try
            {
                if (GameData.SaveSlots == null) return 0;
                int count = 0;
                foreach (SaveGameData slot in GameData.SaveSlots)
                {
                    if (slot == null) continue;
                    string recorded = (slot.CharName ?? string.Empty).Trim();
                    if (string.Equals(recorded, playerName.Trim(), StringComparison.OrdinalIgnoreCase)) count++;
                }
                return count;
            }
            catch { return 0; }
        }

        private static int CountMatchingSafeKeys(string playerName)
        {
            if (string.IsNullOrWhiteSpace(playerName)) return 0;
            string target = CraftingCharacterKey.SafeKey(playerName);
            if (string.IsNullOrWhiteSpace(target)) return 0;
            try
            {
                if (GameData.SaveSlots == null) return 0;
                int count = 0;
                foreach (SaveGameData slot in GameData.SaveSlots)
                {
                    if (slot == null || string.IsNullOrWhiteSpace(slot.CharName)) continue;
                    if (string.Equals(CraftingCharacterKey.SafeKey(slot.CharName), target, StringComparison.Ordinal)) count++;
                }
                return count;
            }
            catch { return 0; }
        }
    }
}
