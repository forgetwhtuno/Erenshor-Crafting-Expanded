namespace ErenshorCraftingExpanded
{
    public static class CraftingPersistencePolicy
    {
        // Pure mirror of the one-time legacy claim decision used by CraftingProgressionStore.
        // Once any character owns the marker, only that exact character may retry a failed copy.
        public static bool MayClaimLegacy(bool characterSidecarExists, bool legacySidecarExists,
            string markerOwner, string characterKey)
        {
            if (characterSidecarExists || !legacySidecarExists || string.IsNullOrEmpty(characterKey)) return false;
            if (string.IsNullOrEmpty(markerOwner)) return true;
            return string.Equals(markerOwner, characterKey, System.StringComparison.Ordinal);
        }

        internal static string RunSelfTests()
        {
            if (!MayClaimLegacy(false, true, string.Empty, "slot0_a")) return "FAIL first legacy progression claim";
            if (!MayClaimLegacy(false, true, "slot0_a", "slot0_a")) return "FAIL same-character legacy retry";
            if (MayClaimLegacy(false, true, "slot0_a", "slot1_b")) return "FAIL cross-character legacy duplication";
            if (MayClaimLegacy(true, true, string.Empty, "slot0_a")) return "FAIL existing character sidecar overwrite";
            if (MayClaimLegacy(false, false, string.Empty, "slot0_a")) return "FAIL absent legacy progression claim";
            return "PASS crafting persistence policy";
        }
    }
}
