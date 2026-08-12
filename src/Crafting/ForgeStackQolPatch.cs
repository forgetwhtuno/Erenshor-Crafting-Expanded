using System;
using HarmonyLib;

namespace ErenshorCraftingExpanded
{
    // Makes the native Craft button stack-friendly without replacing Smithing.Combine(): just
    // before vanilla validates the recipe, move only the missing generic material units from
    // inventory by repeatedly invoking ItemIcon.QuickSmith(), then let Combine remain fully
    // authoritative for exact-match validation, consumption, fuel rules, and output.
    //
    // The three current-build special quality/merge templates are explicitly excluded because
    // they bypass the generic TemplateIngredients path (see NATIVE_CRAFTING_FINDINGS.md).
    [HarmonyPatch(typeof(Smithing), "Combine")]
    internal static class ForgeStackQolPatch
    {
        [HarmonyPrefix]
        private static void Prefix()
        {
            try
            {
                if (!CraftingConfig.EnableMod.Value) return;
                CraftRecipeSnapshot recipe = GameCraftingApi.TryGetActiveRecipe();
                if (recipe == null || GameCraftingApi.IsSpecialCombineTemplate(recipe.TemplateItemId)) return;
                int moved = GameCraftingApi.FillComponentsForOneCraft(recipe);
                CraftingController.OnAutoFillAttempt(moved);
            }
            catch (Exception ex)
            {
                // Fail open: the original Combine still runs untouched, so a QoL failure never
                // prevents vanilla crafting.
                CraftingController.LogError("Forge stack QoL autofill failed: " + ex);
            }
        }
    }
}
