using System;

namespace ErenshorCraftingExpanded
{
    // Uses the current suite's assembly-backed/live-tested local-character identity fields:
    // PlayerControl.Myself.MyStats.MyName + CurrentCharacterSlot/ActiveSaveSlot.index, guarded by
    // the save slot's CharName. No reflection, profile-wide fallback, or invented save field.
    internal static class ForagingCharacterIdentity
    {
        internal static bool TryResolve(out string key, out string playerName, out int slotIndex)
        {
            key = string.Empty;
            playerName = string.Empty;
            slotIndex = -1;
            try
            {
                // Character identity is a Foraging/save concern, not a Suite UI/group-manager concern.
                // The previous gate delegated to SuiteUiPolicy.IsGameplayReady(), whose stronger UI
                // readiness contract also requires Sim/grouping managers. Auto-placement can be fully
                // active in a zone while that unrelated manager gate is false, leaving progression
                // permanently "waiting" and making every visible resource ungatherable. The proven
                // local character + slot fields below are sufficient; only native char-select/zoning
                // transitions suppress identity acquisition.
                if (!ForagingCharacterReadinessPolicy.CanProbeIdentity(GameData.InCharSelect, GameData.Zoning)) return false;
                if (GameData.PlayerControl == null || GameData.PlayerControl.Myself == null || GameData.PlayerControl.Myself.MyStats == null) return false;
                playerName = (GameData.PlayerControl.Myself.MyStats.MyName ?? string.Empty).Trim();
                if (playerName.Length == 0) return false;

                SaveGameData active = GameData.CurrentCharacterSlot != null ? GameData.CurrentCharacterSlot : GameData.ActiveSaveSlot;
                if (active == null || active.index < 0) return false;
                string recorded = (active.CharName ?? string.Empty).Trim();
                if (recorded.Length > 0 && !string.Equals(recorded, playerName, StringComparison.OrdinalIgnoreCase)) return false;
                slotIndex = active.index;
                key = ForagingCharacterKey.Compose(playerName, slotIndex);
                return key.Length > 0;
            }
            catch
            {
                key = string.Empty;
                playerName = string.Empty;
                slotIndex = -1;
                return false;
            }
        }
    }
}
