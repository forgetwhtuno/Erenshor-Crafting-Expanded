namespace ErenshorCraftingExpanded
{
    // Global combat is advisory only: it may describe a Sim, a duel, or a stale combat state.
    // Gathering is denied only when the local player has an active native aggro target.
    public static class ForageCombatEligibilityPolicy
    {
        public static bool CanBeginOrContinue(bool localAggroKnown, bool localAggro)
        {
            return !localAggroKnown || !localAggro;
        }

        public static string DiagnosticToken(bool globalCombat, bool localAggroKnown, bool localAggro)
        {
            if (localAggroKnown && localAggro) return "local-hostile-aggro";
            if (globalCombat) return localAggroKnown ? "global-combat-ignored" : "global-combat-local-probe-unavailable";
            return localAggroKnown ? "local-clear" : "local-probe-unavailable";
        }

        internal static string RunSelfTests()
        {
            if (!CanBeginOrContinue(true, false)) return "FAIL local-clear gather denied";
            if (!CanBeginOrContinue(false, false)) return "FAIL unavailable local probe should fail open";
            if (CanBeginOrContinue(true, true)) return "FAIL local hostile aggro allowed";
            if (DiagnosticToken(true, true, false) != "global-combat-ignored") return "FAIL party/stale global combat token";
            if (DiagnosticToken(false, true, true) != "local-hostile-aggro") return "FAIL local hostile token";
            return "PASS forage combat eligibility policy";
        }
    }
}
