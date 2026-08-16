using System;
using System.Collections.Generic;

namespace ErenshorCraftingExpanded
{
    public sealed class ProductionRecipeCandidateDescriptor
    {
        public string DonorTemplateId = string.Empty;
        public string OutputItemId = string.Empty;
        public int OutputValue;
        public ProductionRecipeContentKind ContentKind;
    }

    public static class ProductionRecipeSelectionPolicy
    {
        // Tier ordinals choose low/middle/high value examples within the already conservative
        // category, then walk outward deterministically if that donor/output was already bound.
        public static int SelectIndex(IList<ProductionRecipeCandidateDescriptor> candidates, ProductionRecipeContentKind kind,
            int tierOrdinal, ISet<string> usedDonors, ISet<string> usedOutputs)
        {
            if (candidates == null) return -1;
            List<int> eligible = new List<int>();
            for (int i = 0; i < candidates.Count; i++)
            {
                ProductionRecipeCandidateDescriptor c = candidates[i];
                if (c == null || c.ContentKind != kind || string.IsNullOrEmpty(c.DonorTemplateId) || string.IsNullOrEmpty(c.OutputItemId)) continue;
                if (usedDonors != null && usedDonors.Contains(c.DonorTemplateId)) continue;
                if (usedOutputs != null && usedOutputs.Contains(c.OutputItemId)) continue;
                eligible.Add(i);
            }
            if (eligible.Count == 0) return -1;
            if (tierOrdinal <= 0) return eligible[0];
            if (tierOrdinal == 1) return eligible[(eligible.Count - 1) / 2];
            return eligible[eligible.Count - 1];
        }

        internal static string RunSelfTests()
        {
            List<ProductionRecipeCandidateDescriptor> candidates = new List<ProductionRecipeCandidateDescriptor>();
            for (int i = 0; i < 5; i++) candidates.Add(Candidate("d" + i.ToString(), "o" + i.ToString(), i, ProductionRecipeContentKind.Foundation));
            if (SelectIndex(candidates, ProductionRecipeContentKind.Foundation, 0, null, null) != 0) return "FAIL low-tier selection";
            if (SelectIndex(candidates, ProductionRecipeContentKind.Foundation, 1, null, null) != 2) return "FAIL mid-tier selection";
            if (SelectIndex(candidates, ProductionRecipeContentKind.Foundation, 2, null, null) != 4) return "FAIL high-tier selection";
            HashSet<string> used = new HashSet<string>(StringComparer.Ordinal); used.Add("d0");
            if (SelectIndex(candidates, ProductionRecipeContentKind.Foundation, 0, used, null) != 1) return "FAIL donor dedupe selection";
            HashSet<string> outputs = new HashSet<string>(StringComparer.Ordinal); outputs.Add("o4");
            if (SelectIndex(candidates, ProductionRecipeContentKind.Foundation, 2, null, outputs) != 3) return "FAIL output dedupe selection";
            return "PASS production recipe selection policy";
        }

        private static ProductionRecipeCandidateDescriptor Candidate(string donor, string output, int value, ProductionRecipeContentKind kind)
        {
            ProductionRecipeCandidateDescriptor c = new ProductionRecipeCandidateDescriptor();
            c.DonorTemplateId = donor; c.OutputItemId = output; c.OutputValue = value; c.ContentKind = kind;
            return c;
        }
    }
}
