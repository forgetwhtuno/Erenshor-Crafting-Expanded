using System;
using System.Collections.Generic;

namespace ErenshorCraftingExpanded
{
    public static class ForagingXpCurve
    {
        public const int MaxLevel = 50;

        public static int XpToNextLevel(int level)
        {
            if (level < 1) level = 1;
            if (level >= MaxLevel) return 0;
            int n = level - 1;
            return 45 + (5 * n) + ((n * n) / 8);
        }

        public static int TotalXpToLevel(int level)
        {
            if (level <= 1) return 0;
            if (level > MaxLevel) level = MaxLevel;
            int total = 0;
            for (int current = 1; current < level; current++) total += XpToNextLevel(current);
            return total;
        }
    }

    [Serializable]
    public sealed class ForagingProgress
    {
        public int Level = 1;
        public int Xp = 0;

        public void Normalize()
        {
            if (Level < 1) Level = 1;
            if (Level > ForagingXpCurve.MaxLevel) Level = ForagingXpCurve.MaxLevel;
            if (Level >= ForagingXpCurve.MaxLevel)
            {
                Xp = 0;
                return;
            }
            if (Xp < 0) Xp = 0;
            int next = ForagingXpCurve.XpToNextLevel(Level);
            if (next > 0 && Xp >= next) Xp = next - 1;
        }

        public ForagingXpAward AwardXp(int amount)
        {
            Normalize();
            int oldLevel = Level;
            int oldXp = Xp;
            if (amount <= 0 || Level >= ForagingXpCurve.MaxLevel)
                return new ForagingXpAward(oldLevel, Level, oldXp, Xp, 0);

            Xp += amount;
            while (Level < ForagingXpCurve.MaxLevel)
            {
                int need = ForagingXpCurve.XpToNextLevel(Level);
                if (need <= 0 || Xp < need) break;
                Xp -= need;
                Level++;
            }
            if (Level >= ForagingXpCurve.MaxLevel) Xp = 0;
            return new ForagingXpAward(oldLevel, Level, oldXp, Xp, amount);
        }
    }

    public sealed class ForagingXpAward
    {
        public readonly int OldLevel;
        public readonly int NewLevel;
        public readonly int OldXp;
        public readonly int NewXp;
        public readonly int AppliedXp;

        public ForagingXpAward(int oldLevel, int newLevel, int oldXp, int newXp, int appliedXp)
        {
            OldLevel = oldLevel;
            NewLevel = newLevel;
            OldXp = oldXp;
            NewXp = newXp;
            AppliedXp = appliedXp;
        }

        public bool LeveledUp { get { return NewLevel > OldLevel; } }
    }

    [Serializable]
    public sealed class ForagingKnowledgeState
    {
        public List<string> DiscoveredResourceKeys = new List<string>();

        public bool HasDiscovered(string resourceKey)
        {
            string key = NormalizeKey(resourceKey);
            if (key.Length == 0 || DiscoveredResourceKeys == null) return false;
            for (int i = 0; i < DiscoveredResourceKeys.Count; i++)
                if (string.Equals(DiscoveredResourceKeys[i], key, StringComparison.Ordinal)) return true;
            return false;
        }

        public bool Discover(string resourceKey)
        {
            string key = NormalizeKey(resourceKey);
            if (key.Length == 0) return false;
            Normalize();
            if (HasDiscovered(key)) return false;
            DiscoveredResourceKeys.Add(key);
            DiscoveredResourceKeys.Sort(StringComparer.Ordinal);
            return true;
        }

        public void Normalize()
        {
            if (DiscoveredResourceKeys == null) DiscoveredResourceKeys = new List<string>();
            List<string> clean = new List<string>();
            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < DiscoveredResourceKeys.Count; i++)
            {
                string key = NormalizeKey(DiscoveredResourceKeys[i]);
                if (key.Length == 0 || !seen.Add(key)) continue;
                clean.Add(key);
            }
            clean.Sort(StringComparer.Ordinal);
            DiscoveredResourceKeys = clean;
        }

        public static string NormalizeKey(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            string text = value.Trim().ToLowerInvariant();
            char[] chars = new char[text.Length];
            int count = 0;
            bool separator = false;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9'))
                {
                    chars[count++] = c;
                    separator = false;
                }
                else if (!separator && count > 0)
                {
                    chars[count++] = '_';
                    separator = true;
                }
            }
            while (count > 0 && chars[count - 1] == '_') count--;
            return count <= 0 ? string.Empty : new string(chars, 0, count);
        }
    }

    public sealed class ForagingGatherProgressionResult
    {
        public bool Applied;
        public bool NewlyDiscovered;
        public ForagingXpAward XpAward;
    }

    public static class ForagingProgressionEngine
    {
        public static ForagingGatherProgressionResult ApplySuccessfulGather(
            ForagingProgress progress,
            ForagingKnowledgeState knowledge,
            ForageResourceDefinition resource)
        {
            ForagingGatherProgressionResult result = new ForagingGatherProgressionResult();
            if (progress == null || knowledge == null || resource == null) return result;
            progress.Normalize();
            knowledge.Normalize();
            result.NewlyDiscovered = knowledge.Discover(resource.KnowledgeKey);
            result.XpAward = progress.AwardXp(resource.GatherXp);
            result.Applied = true;
            return result;
        }

        public static bool ShouldCommitSuccessfulGather(bool reservationSucceeded, bool rewardGranted)
        {
            return reservationSucceeded && rewardGranted;
        }

        internal static string RunSelfTests()
        {
            for (int level = 1; level < ForagingXpCurve.MaxLevel - 1; level++)
                if (ForagingXpCurve.XpToNextLevel(level + 1) <= ForagingXpCurve.XpToNextLevel(level)) return "FAIL foraging curve monotonicity";
            if (ForagingXpCurve.XpToNextLevel(1) != 45) return "FAIL foraging level 1 curve";
            if (ForagingXpCurve.XpToNextLevel(8) != 86) return "FAIL foraging level 8 curve";
            if (ForagingXpCurve.TotalXpToLevel(50) != 12829) return "FAIL foraging total curve";
            if (ForagingXpCurve.XpToNextLevel(50) != 0) return "FAIL foraging max curve";

            ForagingProgress p = new ForagingProgress();
            ForagingKnowledgeState k = new ForagingKnowledgeState();
            ForageResourceDefinition herb = ForageResourceCatalog.FindByKnowledgeKey("wild_herb");
            ForagingGatherProgressionResult first = ApplySuccessfulGather(p, k, herb);
            if (!first.Applied || !first.NewlyDiscovered || p.Xp != herb.GatherXp || p.Level != 1)
                return "FAIL first successful gather progression";
            ForagingGatherProgressionResult second = ApplySuccessfulGather(p, k, herb);
            if (!second.Applied || second.NewlyDiscovered) return "FAIL discovery repeated";
            if (p.Level != 1 || p.Xp != herb.GatherXp * 2) return "FAIL repeated successful gather XP";

            ForagingProgress boundary = new ForagingProgress();
            boundary.Xp = 40;
            ForagingXpAward award = boundary.AwardXp(20);
            if (!award.LeveledUp || boundary.Level != 2 || boundary.Xp != 15) return "FAIL foraging level boundary";

            ForagingProgress capped = new ForagingProgress();
            capped.Level = 50;
            capped.Xp = 999;
            capped.Normalize();
            if (capped.Level != 50 || capped.Xp != 0 || capped.AwardXp(999).AppliedXp != 0)
                return "FAIL foraging max level";

            ForagingProgress malformed = new ForagingProgress();
            malformed.Level = -12;
            malformed.Xp = -99;
            malformed.Normalize();
            if (malformed.Level != 1 || malformed.Xp != 0) return "FAIL malformed low progression recovery";
            malformed.Level = 999;
            malformed.Xp = 999999;
            malformed.Normalize();
            if (malformed.Level != 50 || malformed.Xp != 0) return "FAIL malformed high progression recovery";

            ForagingKnowledgeState knowledge = new ForagingKnowledgeState();
            knowledge.DiscoveredResourceKeys.Add(" Wild Herb ");
            knowledge.DiscoveredResourceKeys.Add("wild-herb");
            knowledge.DiscoveredResourceKeys.Add("");
            knowledge.Normalize();
            if (knowledge.DiscoveredResourceKeys.Count != 1 || knowledge.DiscoveredResourceKeys[0] != "wild_herb")
                return "FAIL knowledge normalization";

            if (!ShouldCommitSuccessfulGather(true, true)) return "FAIL successful gather commit policy";
            if (ShouldCommitSuccessfulGather(true, false)) return "FAIL failed reward must not progress";
            if (ShouldCommitSuccessfulGather(false, true)) return "FAIL duplicate reservation must not progress";

            // Pure transaction mirror of the runtime order: begin -> grant pending -> reward ->
            // progression. A definitive failed reward returns the node to Available and awards no
            // XP; a successful grant depletes before progression so presentation failure cannot
            // expose a duplicate reservation.
            ForageNodeRuntimeState node = new ForageNodeRuntimeState();
            ForagingProgress txnProgress = new ForagingProgress();
            ForagingKnowledgeState txnKnowledge = new ForagingKnowledgeState();
            bool reserved = node.TryBeginGather(101, 1f);
            if (!reserved) return "FAIL gather transaction begin";
            node.Tick(1f);
            if (!node.TryEnterGrantPending(101)) return "FAIL gather transaction grant pending";
            if (ShouldCommitSuccessfulGather(true, false)) ApplySuccessfulGather(txnProgress, txnKnowledge, herb);
            if (!node.RejectGrant(101)) return "FAIL failed gather grant rollback";
            if (txnProgress.Xp != 0 || txnKnowledge.HasDiscovered("wild_herb")) return "FAIL failed gather progression rollback";

            reserved = node.TryBeginGather(102, 1f);
            if (!reserved) return "FAIL successful gather transaction begin";
            node.Tick(1f);
            if (!node.TryEnterGrantPending(102)) return "FAIL successful gather grant pending";
            if (!node.CompleteGrantSuccess(102, 300f)) return "FAIL successful gather depletion commit";
            if (ShouldCommitSuccessfulGather(true, true)) ApplySuccessfulGather(txnProgress, txnKnowledge, herb);
            int committedXp = txnProgress.Xp;
            bool duplicateReserved = node.TryBeginGather(103, 1f);
            if (duplicateReserved) return "FAIL duplicate gather begin";
            if (ShouldCommitSuccessfulGather(duplicateReserved, true)) ApplySuccessfulGather(txnProgress, txnKnowledge, herb);
            if (txnProgress.Xp != committedXp) return "FAIL duplicate gather awarded XP";
            return "PASS foraging progression";
        }
    }
}
