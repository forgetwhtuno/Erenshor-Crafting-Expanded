using System;
using HarmonyLib;

namespace ErenshorCraftingExpanded
{
    // Smithing.DoSuccess() is the verified generic-recipe success method. The recipe identity is
    // still available at method entry but native DoSuccess clears/reuses forge slots while it
    // runs, so capture a plain-data snapshot in Prefix and award XP only in Postfix. This avoids
    // granting progression if DoSuccess itself throws before completing.
    [HarmonyPatch(typeof(Smithing), "DoSuccess")]
    internal static class CraftSuccessPatch
    {
        [HarmonyPrefix]
        private static void Prefix(out CraftRecipeSnapshot __state)
        {
            __state = null;
            try
            {
                if (!CraftingConfig.EnableMod.Value) return;
                __state = GameCraftingApi.TryGetActiveRecipe();
            }
            catch (Exception ex)
            {
                CraftingController.LogError("Craft success capture failed: " + ex);
            }
        }

        [HarmonyPostfix]
        private static void Postfix(CraftRecipeSnapshot __state)
        {
            try
            {
                if (!CraftingConfig.EnableMod.Value || __state == null) return;
                CraftingController.OnVerifiedCraftSuccess(__state);
            }
            catch (Exception ex)
            {
                CraftingController.LogError("Craft success handling failed: " + ex);
            }
        }
    }
}
