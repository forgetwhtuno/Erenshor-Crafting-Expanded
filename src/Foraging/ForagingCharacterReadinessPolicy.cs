namespace ErenshorCraftingExpanded
{
    // Pure transition gate for Foraging character identity. Resource progression needs a local
    // character/save slot; it does not depend on Suite UI readiness, Sim managers, grouping, or
    // any sibling module. The runtime identity resolver performs the concrete null/name/slot checks.
    public static class ForagingCharacterReadinessPolicy
    {
        public static bool CanProbeIdentity(bool inCharacterSelect, bool zoning)
        {
            return !inCharacterSelect && !zoning;
        }

        internal static string RunSelfTests()
        {
            if (!CanProbeIdentity(false, false)) return "FAIL ordinary gameplay should permit Foraging identity probe";
            if (CanProbeIdentity(true, false)) return "FAIL character select should suppress Foraging identity probe";
            if (CanProbeIdentity(false, true)) return "FAIL zoning should suppress Foraging identity probe";
            if (CanProbeIdentity(true, true)) return "FAIL transition overlap should suppress Foraging identity probe";
            return "PASS foraging character readiness policy";
        }
    }
}
