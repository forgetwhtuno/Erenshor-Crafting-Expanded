using System;
using HarmonyLib;

namespace ErenshorCraftingExpanded
{
    internal sealed class CraftSuccessPatchState
    {
        internal CraftRecipeSnapshot Recipe;
        internal CraftSuccessAwardToken AwardToken = new CraftSuccessAwardToken();
    }

    // Smithing.DoSuccess() is the verified generic-recipe success method. The recipe identity is
    // still available at method entry but native DoSuccess clears/reuses forge slots while it
    // runs, so capture a plain-data snapshot in Prefix and award XP only in Postfix. This avoids
    // granting progression if DoSuccess itself throws before completing. A one-use token also
    // makes the mod observer idempotent if the same callback state is ever presented twice.
    [HarmonyPatch(typeof(Smithing), "DoSuccess")]
    internal static class CraftSuccessPatch
    {
        [HarmonyPrefix]
        private static void Prefix(out CraftSuccessPatchState __state)
        {
            __state = null;
            try
            {
                if (!CraftingConfig.EnableMod.Value) return;
                CraftRecipeSnapshot recipe = GameCraftingApi.TryGetActiveRecipe();
                if (recipe == null) return;
                __state = new CraftSuccessPatchState { Recipe = recipe };
            }
            catch (Exception ex)
            {
                CraftingController.LogError("Craft success capture failed: " + ex);
            }
        }

        [HarmonyPostfix]
        private static void Postfix(CraftSuccessPatchState __state)
        {
            try
            {
                if (!CraftingConfig.EnableMod.Value || __state == null || __state.Recipe == null) return;
                if (!CraftSuccessAwardPolicy.TryConsume(__state.AwardToken, true)) return;
                CraftingController.OnVerifiedCraftSuccess(__state.Recipe);
            }
            catch (Exception ex)
            {
                CraftingController.LogError("Craft success handling failed: " + ex);
            }
        }
    }
}
