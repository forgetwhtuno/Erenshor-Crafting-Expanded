using HarmonyLib;

namespace ErenshorCraftingExpanded
{
    // Current-suite source already proves PlayerControl.LeftClick as Erenshor's native world-click
    // boundary (the standalone Follow mod uses the same method). Consume it only when the pointer
    // hits this mod's forage target; otherwise allow vanilla and all sibling behavior unchanged.
    [HarmonyPatch(typeof(PlayerControl), "LeftClick")]
    internal static class ForageNativeClickPatch
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        private static bool Prefix()
        {
            try
            {
                return !ForageNodeController.TryHandleNativeLeftClick();
            }
            catch
            {
                // Failure-open for the native click path. A broken forage interaction must never
                // disable ordinary Erenshor left-click behavior, and any partially started gather
                // (including optional StartLoot) must be terminated before native input resumes.
                try { ForageNodeController.RuntimeExceptionCleanup(); } catch { }
                return true;
            }
        }
    }
}
