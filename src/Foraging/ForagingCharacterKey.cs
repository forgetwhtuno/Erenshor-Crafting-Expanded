using System;
using System.Text;

namespace ErenshorCraftingExpanded
{
    // Pure portion of the current suite's live-tested slot + character-name identity pattern.
    // Foraging deliberately requires BOTH fields for persistence; it never falls back to a
    // profile-wide/name-only file because cross-character leakage is worse than temporarily
    // withholding progression while character identity is unresolved.
    public static class ForagingCharacterKey
    {
        public static string Compose(string playerName, int slotIndex)
        {
            if (slotIndex < 0 || string.IsNullOrWhiteSpace(playerName)) return string.Empty;
            string safe = SafeName(playerName);
            if (safe.Length == 0) return string.Empty;
            return "slot" + slotIndex.ToString() + "_" + safe;
        }

        public static string SafeName(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            string text = value.Trim().ToLowerInvariant();
            StringBuilder sb = new StringBuilder();
            bool separator = false;
            for (int i = 0; i < text.Length && sb.Length < 48; i++)
            {
                char c = text[i];
                if ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9'))
                {
                    sb.Append(c);
                    separator = false;
                }
                else if (!separator && sb.Length > 0)
                {
                    sb.Append('_');
                    separator = true;
                }
            }
            while (sb.Length > 0 && sb[sb.Length - 1] == '_') sb.Length--;
            return sb.ToString();
        }

        internal static string RunSelfTests()
        {
            if (Compose("Aria", 0) != "slot0_aria") return "FAIL character key basic";
            if (Compose("Aria", 1) == Compose("Aria", 0)) return "FAIL same-name slot isolation";
            if (Compose("Aria", 0) == Compose("Borin", 0)) return "FAIL reused-slot name isolation";
            if (Compose("", 0).Length != 0 || Compose("Aria", -1).Length != 0) return "FAIL unstable identity accepted";
            if (SafeName(" A B / C ") != "a_b_c") return "FAIL character key sanitization";
            return "PASS foraging character key";
        }
    }
}
