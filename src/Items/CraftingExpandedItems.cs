using System.Collections.Generic;
using HarmonyLib;

namespace ErenshorCraftingExpanded
{
    // Central catalog for this mod's custom Foraging materials. Wild Herb is the live-verified
    // baseline. Every later family has a distinct safe ItemDB donor policy; covered families remain
    // behind ExperimentalCoveredResources and all specialized world nodes still require truthful
    // matching current-scene visual evidence before placement.
    internal static class CraftingExpandedItems
    {
        internal static readonly CustomItemRegistry Registry = new CustomItemRegistry();
        internal static readonly List<CustomItemRegistrationOutcome> LastOutcomes = new List<CustomItemRegistrationOutcome>();
        internal static bool AttemptedThisSession;

        static CraftingExpandedItems()
        {
            Define(new CustomItemDefinition
            {
                Id = CraftingExpandedItemIds.WildHerbId,
                Name = "Wild Herb",
                Lore = "A useful wild herb gathered while foraging.",
                Value = 1,
                DefaultGrantQuantity = 1,
                VisualKind = CustomItemVisualKind.OrganicHerb,
                BaseItemSelectionNote = "Runtime-selected from safe live ItemDB entries, preferring plant/organic-looking native visuals and refusing rock/ore/coral fallbacks."
            });

            Define(new CustomItemDefinition
            {
                Id = CraftingExpandedItemIds.CaveMushroomId,
                Name = "Cave Mushroom",
                Lore = "A pale fungus gathered from sheltered stone and cave growth.",
                Value = 1,
                DefaultGrantQuantity = 1,
                VisualKind = CustomItemVisualKind.OrganicFungus,
                BaseItemSelectionNote = "Runtime-selected only from safe live ItemDB entries whose native name is explicitly mushroom/fungus-like; no generic plant or rock fallback."
            });

            Define(new CustomItemDefinition
            {
                Id = CraftingExpandedItemIds.WildBloomId,
                Name = "Wild Bloom",
                Lore = "A hardy wildflower gathered from open country.",
                Value = 1,
                DefaultGrantQuantity = 1,
                VisualKind = CustomItemVisualKind.OrganicFlower,
                BaseItemSelectionNote = "Runtime-selected only from safe live ItemDB entries with explicit flower/blossom/bloom/petal evidence."
            });

            Define(new CustomItemDefinition
            {
                Id = CraftingExpandedItemIds.CaveMossId,
                Name = "Cave Moss",
                Lore = "A resilient moss gathered from sheltered stone.",
                Value = 1,
                DefaultGrantQuantity = 1,
                VisualKind = CustomItemVisualKind.OrganicMoss,
                BaseItemSelectionNote = "Runtime-selected only from safe live ItemDB entries with explicit moss/lichen evidence."
            });

            Define(new CustomItemDefinition
            {
                Id = CraftingExpandedItemIds.BlightrootId,
                Name = "Blightroot",
                Lore = "A twisted regional root gathered from the Blight.",
                Value = 1,
                DefaultGrantQuantity = 1,
                VisualKind = CustomItemVisualKind.OrganicRoot,
                BaseItemSelectionNote = "Runtime-selected only from safe live ItemDB entries with explicit root/rhizome/briar/bramble/vine/thorn evidence."
            });
        }

        private static void Define(CustomItemDefinition definition)
        {
            CustomItemDefinitionRejectReason reason = Registry.TryDefine(definition);
            if (reason != CustomItemDefinitionRejectReason.None)
                CraftingController.LogError("Custom item definition rejected id=" + definition.Id + ": " + reason);
        }

        internal static void BeginPluginSession()
        {
            AttemptedThisSession = false;
            LastOutcomes.Clear();
            GameItemRegistryApi.ResetSessionBindings();
            ProductionNativeRecipeRegistry.BeginSession();
            ExperimentalNativeRecipeRegistry.BeginSession();
        }

        internal static CustomItemRegistrationState State(string id)
        {
            for (int i = 0; i < LastOutcomes.Count; i++)
                if (LastOutcomes[i].DefinitionId == id) return LastOutcomes[i].State;
            return CustomItemRegistrationState.Uninitialized;
        }

        internal static string FailureReason(string id)
        {
            for (int i = 0; i < LastOutcomes.Count; i++)
                if (LastOutcomes[i].DefinitionId == id) return LastOutcomes[i].FailureReason;
            return string.Empty;
        }

        internal static string ConflictingItemName(string id)
        {
            for (int i = 0; i < LastOutcomes.Count; i++)
                if (LastOutcomes[i].DefinitionId == id) return LastOutcomes[i].ConflictingExistingItemName;
            return string.Empty;
        }

        internal static CustomItemRegistrationOutcome Outcome(string id)
        {
            for (int i = 0; i < LastOutcomes.Count; i++)
                if (LastOutcomes[i].DefinitionId == id) return LastOutcomes[i];
            return null;
        }

        internal static CustomItemRegistrationState WildHerbState() { return State(CraftingExpandedItemIds.WildHerbId); }
        internal static string LastFailureReason() { return FailureReason(CraftingExpandedItemIds.WildHerbId); }
        internal static string ConflictingItemName() { return ConflictingItemName(CraftingExpandedItemIds.WildHerbId); }

        internal static bool TryRegisterAgainstDatabase(object itemDatabaseInstance)
        {
            if (itemDatabaseInstance == null) return false;
            NativeCraftingRuntimeProbe.Probe(itemDatabaseInstance);
            LastOutcomes.Clear();
            bool reachedRegistry = GameItemRegistryApi.TryRegisterAll(itemDatabaseInstance, Registry.All, LastOutcomes);
            // An unavailable fungal visual is a per-definition fail-closed outcome, not a reason to
            // retry the entire stable ItemDB every frame. Once the registry was reached, diagnostics
            // own the individual outcomes and the session attempt is complete.
            if (reachedRegistry)
            {
                AttemptedThisSession = true;
                // Persisted production recipe ids are recreated as inert owned Items at the same
                // verified ItemDatabase.Start boundary as custom materials. They do not become
                // craftable here; ProductionNativeRecipeRegistry activates them later only after
                // the live forge/runtime proof succeeds.
                ProductionNativeRecipeRegistry.TryRegisterSavedIdentities(itemDatabaseInstance);
                if (CraftingConfig.ExperimentalNativeRecipeRegistration != null && CraftingConfig.ExperimentalNativeRecipeRegistration.Value)
                    ExperimentalNativeRecipeRegistry.TryRegister(itemDatabaseInstance);
            }
            return reachedRegistry;
        }

        internal static bool TryLateRegisterFromLiveDatabase()
        {
            if (AttemptedThisSession) return true;
            object itemDatabase = GameItemRegistryApi.TryGetLiveItemDatabase();
            return TryRegisterAgainstDatabase(itemDatabase);
        }
    }

    // Registration remains anchored to ItemDatabase.Start postfix, after the native ItemDB/dict are
    // populated. The read-only native recipe probe runs at this same verified timing boundary.
    [HarmonyPatch(typeof(ItemDatabase), "Start")]
    internal static class CraftingExpandedItemRegistrationPatch
    {
        [HarmonyPostfix]
        private static void Postfix(ItemDatabase __instance)
        {
            try
            {
                // Item identities remain registered even when the gameplay EnableMod switch is
                // off, so an installed-but-disabled mod can still resolve its existing save items.
                CraftingExpandedItems.TryRegisterAgainstDatabase(__instance);
            }
            catch (System.Exception ex)
            {
                CraftingController.LogError("Custom item registration failed: " + ex);
            }
        }
    }
}
