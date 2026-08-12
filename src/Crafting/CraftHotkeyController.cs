using UnityEngine;

namespace ErenshorCraftingExpanded
{
    // Gates the configurable Craft hotkey. Every gate here maps to a specific requirement from
    // the user's spec: never fire while typing (GameData.PlayerTyping is the native chat-focus
    // flag, confirmed in docs/NATIVE_CRAFTING_FINDINGS.md), only while the forge is open, and
    // always through the same Smithing.Combine() path normal crafting uses - so an invalid
    // recipe still can't be crafted; this controller never bypasses that validation.
    internal static class CraftHotkeyController
    {
        internal static void Tick()
        {
            if (CraftingConfig.CraftHotkey == null) return;
            KeyCode key = CraftingConfig.CraftHotkey.Value;
            if (key == KeyCode.None) return;
            if (!Input.GetKeyDown(key)) return;
            if (IsChatFocused()) return;
            if (!GameCraftingApi.IsForgeOpen()) return;

            bool invoked = GameCraftingApi.InvokeCombine();
            CraftingController.OnHotkeyCraftAttempt(invoked);
        }

        private static bool IsChatFocused()
        {
            try { return GameData.PlayerTyping; }
            catch { return true; }
        }
    }
}
