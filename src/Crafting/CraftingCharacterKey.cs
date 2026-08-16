using System;
using System.Linq;

namespace ErenshorCraftingExpanded
{
    // Pure character-key composition. The live resolver mirrors the slot-qualified identity shape
    // already used by current Journal/Contracts/Nemesis code in the same project snapshot; keeping
    // this part Unity-free makes collision/ambiguity behavior deterministic-testable.
    internal static class CraftingCharacterKey
    {
        internal static string ResolveStrict(string playerName, int slotIndex, int matchingRawNames, int matchingSafeKeys)
        {
            if (string.IsNullOrWhiteSpace(playerName)) return string.Empty;
            string safe = SafeKey(playerName);
            if (string.IsNullOrWhiteSpace(safe)) return string.Empty;
            if (slotIndex >= 0) return "slot" + slotIndex.ToString(System.Globalization.CultureInfo.InvariantCulture) + "_" + safe;
            if (matchingRawNames != 1 || matchingSafeKeys != 1) return string.Empty;
            return safe;
        }

        internal static string SafeKey(string value)
        {
            string source = string.IsNullOrWhiteSpace(value) ? "player" : value;
            return new string(source.ToLowerInvariant().Select(delegate(char c) { return char.IsLetterOrDigit(c) ? c : '_'; }).Take(48).ToArray());
        }

        internal static string RunSelfTests()
        {
            if (ResolveStrict("Bramblewick", 2, 2, 2) != "slot2_bramblewick") return "FAIL crafting character slot key";
            if (ResolveStrict("Bramblewick", -1, 1, 1) != "bramblewick") return "FAIL crafting unique name fallback";
            if (ResolveStrict("Bramblewick", -1, 2, 2) != string.Empty) return "FAIL crafting ambiguous raw name";
            if (ResolveStrict("A-B", -1, 1, 2) != string.Empty) return "FAIL crafting sanitized-name collision";
            if (ResolveStrict("", 0, 1, 1) != string.Empty) return "FAIL crafting blank live name";
            if (ResolveStrict("Same", 0, 2, 2) == ResolveStrict("Same", 1, 2, 2)) return "FAIL crafting character slot isolation";
            return "PASS crafting character identity policy";
        }
    }
}
