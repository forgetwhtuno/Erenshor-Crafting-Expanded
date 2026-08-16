namespace ErenshorCraftingExpanded
{
    // One-use token for a single native DoSuccess invocation. Native Smithing remains the source
    // of truth for whether success occurred; this only prevents duplicate mod-side progression if
    // the same captured callback state is ever presented more than once.
    public sealed class CraftSuccessAwardToken
    {
        internal bool Consumed;
    }

    public static class CraftSuccessAwardPolicy
    {
        public static bool TryConsume(CraftSuccessAwardToken token, bool nativeSuccessObserved)
        {
            if (token == null || !nativeSuccessObserved || token.Consumed) return false;
            token.Consumed = true;
            return true;
        }

        internal static string RunSelfTests()
        {
            CraftSuccessAwardToken failed = new CraftSuccessAwardToken();
            if (TryConsume(failed, false) || failed.Consumed) return "FAIL failed native craft awarded progression";
            if (!TryConsume(failed, true)) return "FAIL later native success rejected after failure";
            if (TryConsume(failed, true)) return "FAIL duplicate native success progression";
            if (TryConsume(null, true)) return "FAIL null success token";
            return "PASS native craft success award policy";
        }
    }
}
