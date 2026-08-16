using System.Collections.Generic;

namespace ErenshorCraftingExpanded
{
    internal static class CraftingRecipeCatalog
    {
        internal const string ExperimentalHerbalTemplateId = "910100001";
        internal const string ExperimentalHerbalRecipeKey = "experimental.herbal_preparation";

        // Production definitions are populated only after ProductionNativeRecipeRegistry binds a
        // stable recipe slot to a packaged native donor/output proven by the current live ItemDB.
        // Stable recipe/template ids live in ProductionRecipePlan; runtime native ids are persisted
        // separately so a future game update cannot silently remap a saved mod template.
        internal static readonly CustomRecipeCatalog Production = new CustomRecipeCatalog();

        internal static void ResetRuntimeProduction()
        {
            Production.Clear();
        }

        internal static CustomRecipeRejectReason TryAddRuntimeProduction(CustomRecipeDefinition definition)
        {
            return Production.TryAdd(definition);
        }

        internal static string RunSelfTests()
        {
            if (!CraftingExpandedItemIds.IsInRecipeTemplateRange(ExperimentalHerbalTemplateId)) return "FAIL experimental template id range";
            IList<CustomRecipeDefinition> recipes = Production.All;
            for (int i = 0; i < recipes.Count; i++)
                if (CustomRecipeCatalog.ValidateShape(recipes[i]) != CustomRecipeRejectReason.None) return "FAIL production recipe catalog shape";
            if (Production.Count != 0) return "FAIL production catalog should be runtime-bound in deterministic tests";
            if (ProductionRecipePlan.All.Count < 5 || ProductionRecipePlan.All.Count > 12) return "FAIL production plan size";
            return "PASS crafting recipe catalog";
        }
    }
}
