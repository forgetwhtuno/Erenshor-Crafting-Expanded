using System.Collections.Generic;

namespace ErenshorCraftingExpanded
{
    // /craftdiag - concise, sectioned, paste-back-friendly output for a human tester. No other
    // mod in this repo registers that command string (checked before choosing it). Each line is
    // a separate UpdateSocialLog.LogAdd call so it reads as a short block in chat rather than
    // one giant line, and stays easy to copy out of LogOutput.log too (see startup logging in
    // CraftingController for the equivalent one-time boot summary).
    internal static class CraftingDiagnostics
    {
        internal static void Report(string modVersion)
        {
            List<string> lines = new List<string>();
            lines.Add("=== Erenshor Crafting Expanded v" + modVersion + " ===");
            lines.Add("Scene: " + SafeSceneName());

            bool forgeOpen = GameCraftingApi.IsForgeOpen();
            CraftRecipeSnapshot recipe = forgeOpen ? GameCraftingApi.TryGetActiveRecipe() : null;
            lines.Add("Forge: state=" + (forgeOpen ? "open" : "closed") +
                " recipe=" + (recipe != null ? recipe.TemplateItemName : "(none)") +
                " canCraft=" + CraftingController.LastCraftableCount +
                " hotkey=" + (CraftingConfig.CraftHotkey != null ? CraftingConfig.CraftHotkey.Value.ToString() : "(unset)") +
                " lastCraft=" + (CraftingController.LastCraft != null ? CraftingController.LastCraft.OutputItemName : "(none)") +
                " autoFillMoved=" + CraftingController.LastAutoFillMovedUnits +
                " lastRejection=" + (string.IsNullOrEmpty(CraftingController.LastRejectionReason) ? "(none)" : CraftingController.LastRejectionReason));

            lines.Add("Custom items: range=" + CraftingExpandedItemIds.RangeStart + "-" + CraftingExpandedItemIds.RangeEnd +
                " wildHerbId=" + CraftingExpandedItemIds.WildHerbId +
                " registered=" + (CraftingExpandedItems.AttemptedThisSession ? CraftingExpandedItems.WildHerbState().ToString() : "Uninitialized") +
                " baseItem=" + (string.IsNullOrEmpty(GameItemRegistryApi.LastBaseItemName) ? "(not yet resolved)" : GameItemRegistryApi.LastBaseItemName) +
                " baseItemId=" + (string.IsNullOrEmpty(GameItemRegistryApi.LastBaseItemId) ? "(unknown)" : GameItemRegistryApi.LastBaseItemId) +
                " inventoryCount=" + CountWildHerbInInventory());
            string conflict = CraftingExpandedItems.ConflictingItemName();
            string itemFailure = CraftingExpandedItems.LastFailureReason();
            if (!string.IsNullOrEmpty(conflict) || !string.IsNullOrEmpty(itemFailure))
                lines.Add("Custom items error: conflict=" + (string.IsNullOrEmpty(conflict) ? "(none)" : conflict) +
                    " reason=" + (string.IsNullOrEmpty(itemFailure) ? "(none)" : itemFailure));

            lines.Add("Progression: Smithing Lv" + CraftingController.Progress.Level +
                " " + CraftingController.Progress.Xp + "/" + SmithingXpCurve.XpToNextLevel(CraftingController.Progress.Level) +
                " XP scope=profile-wide" +
                (string.IsNullOrEmpty(CraftingProgressionStore.LastError) ? string.Empty : " persistenceError=" + CraftingProgressionStore.LastError));

            CraftingCommission commission = CommissionController.Current;
            lines.Add("Commission (PoC only): " + (commission != null ? (commission.SimName + " -> " + commission.RequestedItemName + " [" + commission.State + "]") : "(none)") +
                " coop=" + (CoopCompatibility.IsCoopSession() ? "yes" : "no"));

            lines.Add("Foraging: enabled=" + (ForagingConfig.EnableForaging.Value ? "yes" : "no") +
                " pocNodeEnabled=" + (ForagingConfig.EnablePoCNode.Value ? "yes" : "no") +
                " scene=" + ForageNodeController.SafeSceneName() +
                " defs=" + ForageNodeController.Catalog.Count +
                " spawned=" + ForageNodeController.SpawnedCount +
                " available=" + ForageNodeController.AvailableCount() +
                " depleted=" + ForageNodeController.DepletedCount());
            lines.Add("Primary node: " + ForageNodeController.DescribePrimaryNode());
            lines.Add("Last gather: item=" + (string.IsNullOrEmpty(ForageNodeController.LastGatherSummary) ? "(none)" : ForageNodeController.LastGatherSummary) +
                " lastFailure=" + (string.IsNullOrEmpty(ForageNodeController.LastFailureReason) ? "(none)" : ForageNodeController.LastFailureReason));
            lines.Add("Forage survey: /craftdiag forage pos | /craftdiag forage scan [filter] (see docs/FORAGING_ASSET_SURVEY.md)");

            lines.Add("UI: state=" + CraftingUiStateMachine.Current +
                " offset=(" + CraftingConfig.PanelOffsetX.Value.ToString("F0") + "," + CraftingConfig.PanelOffsetY.Value.ToString("F0") + ")" +
                " persist=" + (CraftingConfig.PersistWindowPosition.Value ? "on" : "off"));

            try { foreach (string line in lines) UpdateSocialLog.LogAdd(line, "yellow"); } catch { }
        }

        // Development/test-only - grants exactly one Wild Herb through the normal inventory
        // path, for controlled manual verification. Not a gameplay feature. One invocation
        // grants exactly one; running it again grants another one (no accidental double-grant
        // per invocation - GameItemRegistryApi.GrantRegisteredItem is called exactly once here).
        internal static void ReportGiveHerb()
        {
            bool granted = GameItemRegistryApi.GrantRegisteredItem(CraftingExpandedItemIds.WildHerbId, 1);
            string message = granted
                ? "[Erenshor Crafting Expanded] Granted 1x Wild Herb (dev/test)."
                : "[Erenshor Crafting Expanded] Could not grant Wild Herb - state=" + CraftingExpandedItems.WildHerbState() +
                  " error=" + CraftingExpandedItems.LastFailureReason();
            try { UpdateSocialLog.LogAdd(message, "yellow"); } catch { }
        }

        private static int CountWildHerbInInventory()
        {
            try
            {
                int total = 0;
                foreach (InventoryAvailability slot in GameCraftingApi.ReadInventoryAvailability())
                    if (slot.ItemId == CraftingExpandedItemIds.WildHerbId) total += slot.Quantity;
                return total;
            }
            catch { return 0; }
        }

        private static string SafeSceneName()
        {
            try { return GameData.SceneName ?? "(unknown)"; } catch { return "(unknown)"; }
        }
    }
}
