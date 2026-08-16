using System;

namespace ErenshorCraftingExpanded
{
    // Small PoC cadence guard. Commission gameplay remains disabled by default; when explicitly
    // enabled, declining/completing/zoning cannot immediately produce another request spam loop.
    public static class CommissionCadencePolicy
    {
        public const int DeclineCooldownMinutes = 10;
        public const int CompleteCooldownMinutes = 15;
        public const int SceneInvalidationCooldownMinutes = 5;

        public static bool CanOffer(DateTime nowUtc, DateTime nextAllowedUtc)
        {
            return nextAllowedUtc == DateTime.MinValue || nowUtc >= nextAllowedUtc;
        }

        public static DateTime NextAllowed(DateTime nowUtc, int minutes)
        {
            if (minutes < 0) minutes = 0;
            return nowUtc.AddMinutes(minutes);
        }

        internal static string RunSelfTests()
        {
            DateTime now = new DateTime(2026, 8, 14, 0, 0, 0, DateTimeKind.Utc);
            if (!CanOffer(now, DateTime.MinValue)) return "FAIL fresh commission state should allow an offer";
            DateTime next = NextAllowed(now, DeclineCooldownMinutes);
            if (CanOffer(now, next)) return "FAIL commission cooldown did not suppress immediate re-offer";
            if (!CanOffer(next, next)) return "FAIL commission should become eligible exactly at cooldown boundary";
            if (NextAllowed(now, -5) != now) return "FAIL negative cooldown should clamp to zero";
            return "PASS commission cadence policy";
        }
    }
}
