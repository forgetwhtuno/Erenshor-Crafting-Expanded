namespace ErenshorCraftingExpanded
{
    public enum CommissionRejectReason
    {
        None = 0,
        InvalidSim,
        RemoteHuman,
        AlreadyHaveActiveCommission,
        ItemNotUsableByClass,
        RecipeAboveAllowedLevel
    }

    // Pure eligibility rules - no Unity/game calls, fully unit-testable. The caller
    // (CommissionController) is responsible for supplying already-verified facts (native item
    // usability, Sim locality) rather than this policy re-deriving them, per the
    // compatibility-API/domain-logic layering boundary in the plan.
    public static class CommissionPolicy
    {
        public static CommissionRejectReason Evaluate(
            SimIdentitySnapshot sim,
            bool isRemoteHuman,
            bool hasActiveCommission,
            bool itemUsableByClass,
            int recipeRequiredLevel)
        {
            if (string.IsNullOrEmpty(sim.RuntimeKey)) return CommissionRejectReason.InvalidSim;
            if (isRemoteHuman) return CommissionRejectReason.RemoteHuman;
            if (hasActiveCommission) return CommissionRejectReason.AlreadyHaveActiveCommission;
            if (!itemUsableByClass) return CommissionRejectReason.ItemNotUsableByClass;
            if (recipeRequiredLevel > sim.Level) return CommissionRejectReason.RecipeAboveAllowedLevel;
            return CommissionRejectReason.None;
        }

        public static bool IsEligible(
            SimIdentitySnapshot sim,
            bool isRemoteHuman,
            bool hasActiveCommission,
            bool itemUsableByClass,
            int recipeRequiredLevel)
        {
            return Evaluate(sim, isRemoteHuman, hasActiveCommission, itemUsableByClass, recipeRequiredLevel) == CommissionRejectReason.None;
        }

        internal static string RunSelfTests()
        {
            SimIdentitySnapshot valid = new SimIdentitySnapshot("1:Baetil", "Baetil", 10);
            SimIdentitySnapshot invalid = new SimIdentitySnapshot(string.Empty, string.Empty, 0);

            if (Evaluate(valid, false, false, true, 5) != CommissionRejectReason.None) return "FAIL valid sim rejected";
            if (Evaluate(invalid, false, false, true, 5) != CommissionRejectReason.InvalidSim) return "FAIL dead/unavailable sim accepted";
            if (Evaluate(valid, true, false, true, 5) != CommissionRejectReason.RemoteHuman) return "FAIL remote coop human accepted";
            if (Evaluate(valid, false, true, true, 5) != CommissionRejectReason.AlreadyHaveActiveCommission) return "FAIL duplicate request accepted";
            if (Evaluate(valid, false, false, false, 5) != CommissionRejectReason.ItemNotUsableByClass) return "FAIL wrong class/item accepted";
            if (Evaluate(valid, false, false, true, 20) != CommissionRejectReason.RecipeAboveAllowedLevel) return "FAIL over-level recipe accepted";

            return "PASS commission policy";
        }
    }
}
