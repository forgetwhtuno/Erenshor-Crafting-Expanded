namespace ErenshorCraftingExpanded
{
    // Pure truth table for the verified CameraController.UsingUI postfix.
    // Native true is never demoted; Crafting only promotes false while it owns a gesture.
    internal static class CraftingCameraUiPolicy
    {
        internal static bool PromoteUsingUi(bool nativeResult, bool craftingOwnsGesture)
        {
            return nativeResult || craftingOwnsGesture;
        }

        internal static string RunSelfTests()
        {
            if (PromoteUsingUi(false, false)) return "FAIL no UI/no gesture should remain false";
            if (!PromoteUsingUi(true, false)) return "FAIL native UI true was demoted";
            if (!PromoteUsingUi(false, true)) return "FAIL owned gesture did not promote UsingUI";
            if (!PromoteUsingUi(true, true)) return "FAIL native true + owned gesture should remain true";
            return "PASS crafting camera UI policy";
        }
    }
}
