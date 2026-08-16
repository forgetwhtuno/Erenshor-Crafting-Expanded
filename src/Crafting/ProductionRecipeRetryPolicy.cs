namespace ErenshorCraftingExpanded
{
    // Pure retry accounting for production native recipes. Waiting for the player to open a live
    // Smithing forge is not a failed activation attempt, and disabling/re-enabling gameplay must
    // restore an attempt budget after active Templates were neutralized.
    public static class ProductionRecipeRetryPolicy
    {
        public static bool IsActionable(bool nativeShapeSupported, int liveComponentSlots)
        {
            return nativeShapeSupported && liveComponentSlots > 0;
        }

        public static int ConsumeAttempt(int current, int maximum, bool actionable)
        {
            if (current < 0) current = 0;
            if (maximum < 0) maximum = 0;
            if (!actionable || current >= maximum) return current;
            return current + 1;
        }

        public static int ResetAfterGameplayDisable(int current)
        {
            return 0;
        }

        internal static string RunSelfTests()
        {
            if (IsActionable(false, 4)) return "FAIL recipe retry consumed without proven forge shape";
            if (IsActionable(true, 0)) return "FAIL recipe retry consumed without live component slots";
            if (!IsActionable(true, 4)) return "FAIL recipe retry rejected actionable forge";
            if (ConsumeAttempt(0, 10, false) != 0) return "FAIL recipe retry burned while waiting";
            if (ConsumeAttempt(0, 10, true) != 1) return "FAIL recipe retry did not consume actionable attempt";
            if (ConsumeAttempt(10, 10, true) != 10) return "FAIL recipe retry exceeded bound";
            if (ResetAfterGameplayDisable(10) != 0) return "FAIL recipe retry disable reset";
            return "PASS production recipe retry policy";
        }
    }
}
