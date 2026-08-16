using System;

namespace ErenshorCraftingExpanded
{
    public static class RecipeBookViewPolicy
    {
        public static string BuildLockedReason(RecipeOwnershipDefinition definition, int craftingLevel)
        {
            if (definition == null) return "Unavailable";
            if (definition.Deprecated) return "Deprecated";
            string levelReason = craftingLevel < definition.MinimumCraftingLevel
                ? "Requires Crafting " + definition.MinimumCraftingLevel.ToString()
                : string.Empty;
            string additional = (definition.AdditionalLockReason ?? string.Empty).Trim();
            if (!string.IsNullOrEmpty(levelReason) && !string.IsNullOrEmpty(additional)) return levelReason + " • " + additional;
            if (!string.IsNullOrEmpty(levelReason)) return levelReason;
            if (!string.IsNullOrEmpty(additional)) return additional;
            return "Not yet learned";
        }

        public static string BuildTemplateStatus(RecipeTemplateLocationState location, int pendingEntitlements)
        {
            string status;
            if (location == RecipeTemplateLocationState.Inventory) status = "Template: Present";
            else if (location == RecipeTemplateLocationState.Forge) status = "Template: In Forge";
            else if (location == RecipeTemplateLocationState.Bank) status = "Template: In Bank";
            else if (location == RecipeTemplateLocationState.OtherStorage) status = "Template: Stored Elsewhere";
            else if (location == RecipeTemplateLocationState.ConfirmedMissing) status = "Template: Missing";
            else status = "Template: Location Unknown";

            if (pendingEntitlements > 0 &&
                location != RecipeTemplateLocationState.Inventory &&
                location != RecipeTemplateLocationState.Forge &&
                location != RecipeTemplateLocationState.Bank &&
                location != RecipeTemplateLocationState.OtherStorage)
                status += " • Replacement Ready";
            return status;
        }

        internal static string RunSelfTests()
        {
            RecipeOwnershipDefinition definition = new RecipeOwnershipDefinition
            {
                StableRecipeId = "recipe.a",
                TemplateItemId = "910100001",
                DisplayName = "Test",
                MinimumCraftingLevel = 8,
                AdditionalLockReason = "Discover Cave Mushroom"
            };
            string locked = BuildLockedReason(definition, 6);
            if (locked != "Requires Crafting 8 • Discover Cave Mushroom") return "FAIL combined locked reason";
            if (BuildLockedReason(definition, 8) != "Discover Cave Mushroom") return "FAIL discovery locked reason";
            if (BuildTemplateStatus(RecipeTemplateLocationState.Inventory, 0) != "Template: Present") return "FAIL present row";
            if (BuildTemplateStatus(RecipeTemplateLocationState.Bank, 0) != "Template: In Bank") return "FAIL bank row";
            if (BuildTemplateStatus(RecipeTemplateLocationState.OtherStorage, 0) != "Template: Stored Elsewhere") return "FAIL external storage row";
            if (BuildTemplateStatus(RecipeTemplateLocationState.ConfirmedMissing, 0) != "Template: Missing") return "FAIL missing row";
            if (BuildTemplateStatus(RecipeTemplateLocationState.Unknown, 1) != "Template: Location Unknown • Replacement Ready") return "FAIL pending unknown row";
            return "PASS recipe book view policy";
        }
    }
}
