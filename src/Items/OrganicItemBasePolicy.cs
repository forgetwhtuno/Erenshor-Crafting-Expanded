using System;

namespace ErenshorCraftingExpanded
{
    // Pure name-ranking policy for choosing the native visual template inherited by custom forage
    // items. Runtime registration applies the stricter field-safety predicate first; this policy
    // only ranks already-safe Item entries. Each non-herb family requires explicit family evidence
    // so a flower/moss/root/fungus can never silently inherit a generic fern, food, or rock icon.
    public static class OrganicItemBasePolicy
    {
        public static int ScoreName(string itemName)
        {
            return ScoreName(itemName, CustomItemVisualKind.OrganicHerb);
        }

        public static int ScoreName(string itemName, CustomItemVisualKind kind)
        {
            if (string.IsNullOrWhiteSpace(itemName)) return int.MinValue;
            string text = itemName.Trim().ToLowerInvariant();

            if (ContainsRejectedToken(text, new string[]
            {
                "coral", "rock", "rocks", "stone", "stones", "ore", "ores", "ingot", "ingots",
                "mineral", "minerals", "crystal", "crystals", "gem", "gems", "metal", "coal", "sand", "brick"
            })) return int.MinValue;

            int score = 0;
            if (kind == CustomItemVisualKind.OrganicFungus)
            {
                if (text.IndexOf("mushroom", StringComparison.Ordinal) >= 0) score += 220;
                if (text.IndexOf("toadstool", StringComparison.Ordinal) >= 0) score += 210;
                if (text.IndexOf("fungus", StringComparison.Ordinal) >= 0) score += 200;
                if (text.IndexOf("fungi", StringComparison.Ordinal) >= 0) score += 200;
                if (text.IndexOf("spore", StringComparison.Ordinal) >= 0) score += 160;
                if (text.IndexOf("truffle", StringComparison.Ordinal) >= 0) score += 140;
                return score > 0 ? score : int.MinValue;
            }

            if (kind == CustomItemVisualKind.OrganicFlower)
            {
                if (text.IndexOf("flower", StringComparison.Ordinal) >= 0) score += 220;
                if (text.IndexOf("blossom", StringComparison.Ordinal) >= 0) score += 210;
                if (text.IndexOf("bloom", StringComparison.Ordinal) >= 0) score += 200;
                if (text.IndexOf("petal", StringComparison.Ordinal) >= 0) score += 180;
                return score > 0 ? score : int.MinValue;
            }

            if (kind == CustomItemVisualKind.OrganicMoss)
            {
                if (text.IndexOf("moss", StringComparison.Ordinal) >= 0) score += 220;
                if (text.IndexOf("lichen", StringComparison.Ordinal) >= 0) score += 200;
                return score > 0 ? score : int.MinValue;
            }

            if (kind == CustomItemVisualKind.OrganicRoot)
            {
                if (text.IndexOf("root", StringComparison.Ordinal) >= 0) score += 220;
                if (text.IndexOf("rhizome", StringComparison.Ordinal) >= 0) score += 210;
                if (text.IndexOf("briar", StringComparison.Ordinal) >= 0) score += 180;
                if (text.IndexOf("bramble", StringComparison.Ordinal) >= 0) score += 180;
                if (text.IndexOf("vine", StringComparison.Ordinal) >= 0) score += 160;
                if (text.IndexOf("thorn", StringComparison.Ordinal) >= 0) score += 150;
                return score > 0 ? score : int.MinValue;
            }

            if (text.IndexOf("herb", StringComparison.Ordinal) >= 0) score += 140;
            if (text.IndexOf("leaf", StringComparison.Ordinal) >= 0) score += 120;
            if (text.IndexOf("plant", StringComparison.Ordinal) >= 0) score += 110;
            if (text.IndexOf("fern", StringComparison.Ordinal) >= 0) score += 85;
            if (text.IndexOf("seed", StringComparison.Ordinal) >= 0) score += 75;
            if (text.IndexOf("berry", StringComparison.Ordinal) >= 0) score += 70;
            if (text.IndexOf("grain", StringComparison.Ordinal) >= 0) score += 60;
            if (text.IndexOf("vegetable", StringComparison.Ordinal) >= 0) score += 55;
            if (text.IndexOf("fruit", StringComparison.Ordinal) >= 0) score += 50;
            if (text.IndexOf("food", StringComparison.Ordinal) >= 0) score += 20;
            return score > 0 ? score : int.MinValue;
        }

        public static string EvidenceDescription(CustomItemVisualKind kind)
        {
            if (kind == CustomItemVisualKind.OrganicFungus) return "explicit mushroom/fungus/spore native Item name";
            if (kind == CustomItemVisualKind.OrganicFlower) return "explicit flower/blossom/bloom/petal native Item name";
            if (kind == CustomItemVisualKind.OrganicMoss) return "explicit moss/lichen native Item name";
            if (kind == CustomItemVisualKind.OrganicRoot) return "explicit root/rhizome/briar/bramble/vine/thorn native Item name";
            return "safe plant/organic native Item name";
        }

        private static bool ContainsRejectedToken(string value, string[] tokens)
        {
            for (int i = 0; i < tokens.Length; i++)
            {
                string token = tokens[i];
                int start = 0;
                while (start < value.Length)
                {
                    int index = value.IndexOf(token, start, StringComparison.Ordinal);
                    if (index < 0) break;
                    int end = index + token.Length;
                    bool leftBoundary = index == 0 || !char.IsLetterOrDigit(value[index - 1]);
                    bool rightBoundary = end >= value.Length || !char.IsLetterOrDigit(value[end]);
                    if (leftBoundary && rightBoundary) return true;
                    start = index + 1;
                }
            }
            return false;
        }

        internal static string RunSelfTests()
        {
            if (ScoreName("Wild Herb") <= ScoreName("Red Berry")) return "FAIL herb should outrank generic berry";
            if (ScoreName("Forest Leaf") <= 0) return "FAIL leaf-like organic item rejected";
            if (ScoreName("Forest Herb") <= 0) return "FAIL word fragment 'ore' inside Forest must not reject organic item";
            if (ScoreName("Coral") != int.MinValue) return "FAIL Coral must not remain a Wild Herb base visual";
            if (ScoreName("Iron Ore") != int.MinValue) return "FAIL ore visual must not be selected";
            if (ScoreName("Plain Token") != int.MinValue) return "FAIL unrelated safe item must not become herb visual fallback";

            if (ScoreName("Cave Mushroom", CustomItemVisualKind.OrganicFungus) <= 0) return "FAIL mushroom donor should satisfy fungus family";
            if (ScoreName("Fernallan Willow Seed", CustomItemVisualKind.OrganicFungus) != int.MinValue) return "FAIL fungus family must not use generic plant donor";
            if (ScoreName("Stone Mushroom", CustomItemVisualKind.OrganicFungus) != int.MinValue) return "FAIL geological fungus-looking donor should remain rejected";

            if (ScoreName("Moon Blossom", CustomItemVisualKind.OrganicFlower) <= 0) return "FAIL flower donor evidence";
            if (ScoreName("Forest Leaf", CustomItemVisualKind.OrganicFlower) != int.MinValue) return "FAIL flower family must not use leaf donor";
            if (ScoreName("Cave Lichen", CustomItemVisualKind.OrganicMoss) <= 0) return "FAIL moss/lichen donor evidence";
            if (ScoreName("Cave Mushroom", CustomItemVisualKind.OrganicMoss) != int.MinValue) return "FAIL moss family must not use fungus donor";
            if (ScoreName("Blighted Root", CustomItemVisualKind.OrganicRoot) <= 0) return "FAIL root donor evidence";
            if (ScoreName("Fernallan Willow Seed", CustomItemVisualKind.OrganicRoot) != int.MinValue) return "FAIL root family must not use seed donor";
            return "PASS organic item base policy";
        }
    }
}
