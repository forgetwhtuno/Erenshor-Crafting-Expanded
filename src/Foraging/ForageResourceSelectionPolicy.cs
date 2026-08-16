using System;
using System.Collections.Generic;

namespace ErenshorCraftingExpanded
{
    // Pure deterministic density selector. Runtime code supplies only candidates whose native item
    // and exact visual-family evidence actually exist. The selector cannot promote an unavailable
    // family and never changes placement coordinates.
    public static class ForageResourceSelectionPolicy
    {
        public static ForageResourceDefinition Select(
            IList<ForageResourceDefinition> candidates,
            IDictionary<string, int> currentCounts,
            string scene,
            int generation,
            int pointIndex)
        {
            List<ForageResourceDefinition> eligible = new List<ForageResourceDefinition>();
            if (candidates == null) return null;

            for (int i = 0; i < candidates.Count; i++)
            {
                ForageResourceDefinition resource = candidates[i];
                if (resource == null) continue;
                int count = CountFor(currentCounts, resource.KnowledgeKey);
                if (count >= resource.MaxAutoNodesPerScene) continue;
                if (resource.DensityWeight <= 0) continue;
                eligible.Add(resource);
            }
            if (eligible.Count == 0) return null;

            // The first point always prefers the densest proven family. This preserves a reliable
            // Wild Herb baseline where it exists while later points can introduce uncommon families.
            if (pointIndex <= 0)
            {
                ForageResourceDefinition best = eligible[0];
                for (int i = 1; i < eligible.Count; i++)
                {
                    ForageResourceDefinition candidate = eligible[i];
                    if (candidate.DensityWeight > best.DensityWeight ||
                        (candidate.DensityWeight == best.DensityWeight &&
                         string.Compare(candidate.KnowledgeKey, best.KnowledgeKey, StringComparison.Ordinal) < 0))
                        best = candidate;
                }
                return best;
            }

            eligible.Sort(CompareStable);
            int totalWeight = 0;
            for (int i = 0; i < eligible.Count; i++) totalWeight += eligible[i].DensityWeight;
            if (totalWeight <= 0) return null;

            int seed = StableHash((scene ?? string.Empty) + "|" + generation + "|" + pointIndex);
            int pick = PositiveMod(seed, totalWeight);
            int cursor = 0;
            for (int i = 0; i < eligible.Count; i++)
            {
                cursor += eligible[i].DensityWeight;
                if (pick < cursor) return eligible[i];
            }
            return eligible[eligible.Count - 1];
        }

        public static void Record(IDictionary<string, int> counts, ForageResourceDefinition resource)
        {
            if (counts == null || resource == null || string.IsNullOrEmpty(resource.KnowledgeKey)) return;
            int existing = CountFor(counts, resource.KnowledgeKey);
            counts[resource.KnowledgeKey] = existing + 1;
        }

        private static int CountFor(IDictionary<string, int> counts, string key)
        {
            if (counts == null || string.IsNullOrEmpty(key)) return 0;
            int value;
            return counts.TryGetValue(key, out value) ? value : 0;
        }

        private static int CompareStable(ForageResourceDefinition a, ForageResourceDefinition b)
        {
            return string.Compare(a == null ? string.Empty : a.KnowledgeKey,
                b == null ? string.Empty : b.KnowledgeKey,
                StringComparison.Ordinal);
        }

        private static int StableHash(string value)
        {
            unchecked
            {
                int hash = 17;
                string text = value ?? string.Empty;
                for (int i = 0; i < text.Length; i++) hash = hash * 31 + text[i];
                return hash;
            }
        }

        private static int PositiveMod(int value, int divisor)
        {
            int result = value % divisor;
            return result < 0 ? result + divisor : result;
        }

        internal static string RunSelfTests()
        {
            ForageResourceDefinition herb = ForageResourceCatalog.FindByKnowledgeKey("wild_herb");
            ForageResourceDefinition bloom = ForageResourceCatalog.FindByKnowledgeKey("wild_bloom");
            if (herb == null || bloom == null) return "FAIL selection test catalog";

            List<ForageResourceDefinition> candidates = new List<ForageResourceDefinition> { bloom, herb };
            Dictionary<string, int> counts = new Dictionary<string, int>(StringComparer.Ordinal);
            ForageResourceDefinition first = Select(candidates, counts, "Hidden Hills", 3, 0);
            if (first != herb) return "FAIL first node should prefer densest proven family";

            Record(counts, herb);
            if (counts["wild_herb"] != 1) return "FAIL resource count recording";

            // Density cap is authority: once Herb reaches its cap, the remaining proven family wins.
            counts["wild_herb"] = herb.MaxAutoNodesPerScene;
            ForageResourceDefinition capped = Select(candidates, counts, "Hidden Hills", 3, 1);
            if (capped != bloom) return "FAIL capped resource remained selectable";

            counts["wild_bloom"] = bloom.MaxAutoNodesPerScene;
            if (Select(candidates, counts, "Hidden Hills", 3, 2) != null)
                return "FAIL all capped resources should produce no selection";

            Dictionary<string, int> none = new Dictionary<string, int>(StringComparer.Ordinal);
            ForageResourceDefinition a = Select(candidates, none, "Hidden Hills", 77, 2);
            ForageResourceDefinition b = Select(candidates, none, "Hidden Hills", 77, 2);
            if (a != b) return "FAIL deterministic selection changed for identical inputs";

            return "PASS forage resource selection policy";
        }
    }
}
