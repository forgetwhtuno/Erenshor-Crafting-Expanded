using System;
using System.Collections.Generic;

namespace ErenshorCraftingExpanded
{
    public sealed class ForageDepletionSnapshot
    {
        public string Scene;
        public string ItemId;
        public float RemainingSeconds;

        public ForageDepletionSnapshot() { }
        public ForageDepletionSnapshot(string scene, string itemId, float remainingSeconds)
        {
            Scene = scene;
            ItemId = itemId;
            RemainingSeconds = remainingSeconds;
        }
    }

    // Runtime cooldown ledger. Transient nodes are keyed logically by scene + resource family,
    // while ForagingProgressionController exports/imports REMAINING cooldown seconds to the
    // current character's mod-owned sidecar. No Erenshor save field and no wall-clock timestamp is
    // used, so zone hops/process restarts cannot reset nodes and offline time cannot advance them.
    public static class ForageDepletionLedger
    {
        private sealed class Entry
        {
            internal string Scene;
            internal string ItemId;
            internal float ExpiresAt;
        }

        private const int MaximumEntries = 64;
        private static readonly List<Entry> Entries = new List<Entry>();

        public static void Record(string scene, string itemId, float now, float respawnSeconds)
        {
            if (string.IsNullOrEmpty(scene) || string.IsNullOrEmpty(itemId)) return;
            if (!IsFinite(now) || !IsFinite(respawnSeconds) || respawnSeconds <= 0f) return;
            Prune(now);
            AddBounded(scene, itemId, now + respawnSeconds);
        }

        public static List<float> GetActiveRemainingSeconds(string scene, string itemId, float now)
        {
            List<float> result = new List<float>();
            if (string.IsNullOrEmpty(scene) || string.IsNullOrEmpty(itemId) || !IsFinite(now)) return result;
            Prune(now);
            for (int i = 0; i < Entries.Count; i++)
            {
                Entry entry = Entries[i];
                if (!string.Equals(entry.Scene, scene, StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(entry.ItemId, itemId, StringComparison.Ordinal)) continue;
                float remaining = entry.ExpiresAt - now;
                if (remaining > 0f && IsFinite(remaining)) result.Add(remaining);
            }
            result.Sort();
            return result;
        }

        public static List<ForageDepletionSnapshot> ExportActive(float now)
        {
            List<ForageDepletionSnapshot> result = new List<ForageDepletionSnapshot>();
            if (!IsFinite(now)) return result;
            Prune(now);
            for (int i = 0; i < Entries.Count; i++)
            {
                Entry entry = Entries[i];
                float remaining = entry.ExpiresAt - now;
                if (remaining <= 0f || !IsFinite(remaining)) continue;
                result.Add(new ForageDepletionSnapshot(entry.Scene, entry.ItemId, remaining));
            }
            result.Sort(CompareSnapshots);
            return result;
        }

        public static void ImportRemaining(IEnumerable<ForageDepletionSnapshot> snapshots, float now)
        {
            Clear();
            if (snapshots == null || !IsFinite(now)) return;
            foreach (ForageDepletionSnapshot snapshot in snapshots)
            {
                if (snapshot == null || string.IsNullOrWhiteSpace(snapshot.Scene) || string.IsNullOrWhiteSpace(snapshot.ItemId)) continue;
                float remaining = snapshot.RemainingSeconds;
                if (!IsFinite(remaining) || remaining <= 0f) continue;
                if (remaining > 86400f) remaining = 86400f;
                AddBounded(snapshot.Scene.Trim(), snapshot.ItemId.Trim(), now + remaining);
            }
        }

        public static int Count { get { return Entries.Count; } }

        public static void Clear()
        {
            Entries.Clear();
        }

        private static void AddBounded(string scene, string itemId, float expiresAt)
        {
            if (Entries.Count >= MaximumEntries)
            {
                int earliest = 0;
                for (int i = 1; i < Entries.Count; i++)
                    if (Entries[i].ExpiresAt < Entries[earliest].ExpiresAt) earliest = i;
                Entries.RemoveAt(earliest);
            }
            Entries.Add(new Entry { Scene = scene, ItemId = itemId, ExpiresAt = expiresAt });
        }

        private static void Prune(float now)
        {
            for (int i = Entries.Count - 1; i >= 0; i--)
                if (!IsFinite(Entries[i].ExpiresAt) || Entries[i].ExpiresAt <= now) Entries.RemoveAt(i);
        }

        private static int CompareSnapshots(ForageDepletionSnapshot left, ForageDepletionSnapshot right)
        {
            int scene = string.Compare(left == null ? string.Empty : left.Scene, right == null ? string.Empty : right.Scene, StringComparison.OrdinalIgnoreCase);
            if (scene != 0) return scene;
            int item = string.Compare(left == null ? string.Empty : left.ItemId, right == null ? string.Empty : right.ItemId, StringComparison.Ordinal);
            if (item != 0) return item;
            float a = left == null ? 0f : left.RemainingSeconds;
            float b = right == null ? 0f : right.RemainingSeconds;
            return a.CompareTo(b);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        internal static string RunSelfTests()
        {
            Clear();
            Record("Hidden Hills", CraftingExpandedItemIds.WildHerbId, 100f, 300f);
            List<float> first = GetActiveRemainingSeconds("Hidden Hills", CraftingExpandedItemIds.WildHerbId, 110f);
            if (first.Count != 1 || Math.Abs(first[0] - 290f) > 0.001f) return "FAIL depletion remaining time";
            if (GetActiveRemainingSeconds("Other Zone", CraftingExpandedItemIds.WildHerbId, 110f).Count != 0)
                return "FAIL depletion leaked across scenes";
            if (GetActiveRemainingSeconds("Hidden Hills", CraftingExpandedItemIds.CaveMushroomId, 110f).Count != 0)
                return "FAIL depletion leaked across resources";

            Record("Hidden Hills", CraftingExpandedItemIds.WildHerbId, 120f, 300f);
            List<ForageDepletionSnapshot> saved = ExportActive(130f);
            if (saved.Count != 2) return "FAIL depletion export count";
            Clear();
            ImportRemaining(saved, 5000f);
            List<float> restored = GetActiveRemainingSeconds("hidden hills", CraftingExpandedItemIds.WildHerbId, 5000f);
            if (restored.Count != 2 || Math.Abs(restored[0] - saved[0].RemainingSeconds) > 0.01f)
                return "FAIL depletion process-restart roundtrip";

            if (GetActiveRemainingSeconds("Hidden Hills", CraftingExpandedItemIds.WildHerbId, 5400f).Count != 0)
                return "FAIL expired depletion not pruned";
            if (Count != 0) return "FAIL expired ledger entries retained";

            Record("Hidden Hills", CraftingExpandedItemIds.WildHerbId, float.NaN, 300f);
            Record("Hidden Hills", CraftingExpandedItemIds.WildHerbId, 1f, 0f);
            if (Count != 0) return "FAIL invalid depletion record accepted";

            for (int i = 0; i < MaximumEntries + 10; i++)
                Record("Zone", CraftingExpandedItemIds.WildHerbId, 1000f + i, 5000f);
            if (Count != MaximumEntries) return "FAIL depletion ledger bound";
            Clear();
            if (Count != 0) return "FAIL depletion ledger clear";
            return "PASS forage depletion ledger";
        }
    }
}
