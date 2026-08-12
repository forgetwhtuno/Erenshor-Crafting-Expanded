using System.Collections.Generic;
using HarmonyLib;

namespace ErenshorCraftingExpanded
{
    // Central place this mod's own custom item definitions are authored - mirrors
    // ForageNodeController's static-constructor registration pattern for consistency. Exactly
    // one prototype item per the plan's explicit scope ("do not create a full ingredient
    // catalog yet").
    internal static class CraftingExpandedItems
    {
        internal static readonly CustomItemRegistry Registry = new CustomItemRegistry();
        internal static readonly List<CustomItemRegistrationOutcome> LastOutcomes = new List<CustomItemRegistrationOutcome>();
        internal static bool AttemptedThisSession;

        static CraftingExpandedItems()
        {
            CustomItemDefinition wildHerb = new CustomItemDefinition
            {
                Id = CraftingExpandedItemIds.WildHerbId,
                Name = "Wild Herb",
                Lore = "A useful wild herb gathered while foraging.",
                Value = 1,
                DefaultGrantQuantity = 1,
                BaseItemSelectionNote = "Runtime-selected: first stackable, non-equipment, non-quest, non-clickable, non-unique ItemDB entry - see docs/NATIVE_ITEM_REGISTRY_FINDINGS.md section 5-6."
            };
            CustomItemDefinitionRejectReason reason = Registry.TryDefine(wildHerb);
            if (reason != CustomItemDefinitionRejectReason.None)
                CraftingController.LogError("Wild Herb definition rejected: " + reason);
        }

        internal static CustomItemRegistrationState WildHerbState()
        {
            foreach (CustomItemRegistrationOutcome outcome in LastOutcomes)
                if (outcome.DefinitionId == CraftingExpandedItemIds.WildHerbId) return outcome.State;
            return CustomItemRegistrationState.Uninitialized;
        }

        internal static string LastFailureReason()
        {
            foreach (CustomItemRegistrationOutcome outcome in LastOutcomes)
                if (outcome.DefinitionId == CraftingExpandedItemIds.WildHerbId) return outcome.FailureReason;
            return string.Empty;
        }

        internal static string ConflictingItemName()
        {
            foreach (CustomItemRegistrationOutcome outcome in LastOutcomes)
                if (outcome.DefinitionId == CraftingExpandedItemIds.WildHerbId) return outcome.ConflictingExistingItemName;
            return string.Empty;
        }
    }

    // Postfix on ItemDatabase.Start - the timing this mod's own findings doc (and current
    // cammaron/Arcanism source, revalidated live) confirms is correct: after Resources.LoadAll
    // + itemDict have been built, and early enough for every other system to see the result.
    [HarmonyPatch(typeof(ItemDatabase), "Start")]
    internal static class CraftingExpandedItemRegistrationPatch
    {
        [HarmonyPostfix]
        private static void Postfix(ItemDatabase __instance)
        {
            try
            {
                if (!CraftingConfig.EnableMod.Value) return;
                CraftingExpandedItems.AttemptedThisSession = true;
                CraftingExpandedItems.LastOutcomes.Clear();
                GameItemRegistryApi.TryRegisterAll(__instance, CraftingExpandedItems.Registry.All, CraftingExpandedItems.LastOutcomes);
            }
            catch (System.Exception ex)
            {
                CraftingController.LogError("Custom item registration failed: " + ex);
            }
        }
    }
}
