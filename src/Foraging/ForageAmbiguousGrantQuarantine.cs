using System;
using System.Collections.Generic;

namespace ErenshorCraftingExpanded
{
    // Separate from the successful depletion ledger by design. An UnknownAfterInvoke result may
    // already have inserted the item, so the resource is quarantined for one normal respawn window
    // without claiming that a verified successful gather occurred. This state is character-scoped
    // and persisted only to prevent an automatic retry after zoning/restart from duplicating an
    // ambiguously inserted item.
    public static class ForageAmbiguousGrantQuarantine
    {
        private sealed class Entry
        {
            internal string Scene;
            internal string ItemId;
            internal float ExpiresAt;
        }

        private const int MaximumEntries = 16;
        private static readonly List<Entry> Entries = new List<Entry>();

        public static void Record(string scene, string itemId, float now, float cooldownSeconds)
        {
            if (string.IsNullOrEmpty(scene) || string.IsNullOrEmpty(itemId)) return;
            if (!IsFinite(now) || !IsFinite(cooldownSeconds) || cooldownSeconds <= 0f) return;
            Prune(now);
            if (Entries.Count >= MaximumEntries)
            {
                int earliest = 0;
                for (int i = 1; i < Entries.Count; i++)
                    if (Entries[i].ExpiresAt < Entries[earliest].ExpiresAt) earliest = i;
                Entries.RemoveAt(earliest);
            }
            Entries.Add(new Entry { Scene = scene, ItemId = itemId, ExpiresAt = now + cooldownSeconds });
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
                if (remaining > 0f && IsFinite(remaining))
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
                Record(snapshot.Scene.Trim(), snapshot.ItemId.Trim(), now, remaining);
            }
        }

        public static int Count { get { return Entries.Count; } }
        public static void Clear() { Entries.Clear(); }

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

        private static bool IsFinite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }

        internal static string RunSelfTests()
        {
            Clear();
            ForageDepletionLedger.Clear();
            Record("Hidden Hills", CraftingExpandedItemIds.WildHerbId, 100f, 300f);
            if (Count != 1) return "FAIL quarantine record";
            List<float> remaining = GetActiveRemainingSeconds("Hidden Hills", CraftingExpandedItemIds.WildHerbId, 110f);
            if (remaining.Count != 1 || Math.Abs(remaining[0] - 290f) > 0.01f) return "FAIL quarantine remaining";
            if (ForageDepletionLedger.Count != 0) return "FAIL quarantine polluted success ledger";
            List<ForageDepletionSnapshot> saved = ExportActive(120f);
            Clear();
            ImportRemaining(saved, 5000f);
            remaining = GetActiveRemainingSeconds("hidden hills", CraftingExpandedItemIds.WildHerbId, 5000f);
            if (remaining.Count != 1 || Math.Abs(remaining[0] - saved[0].RemainingSeconds) > 0.01f) return "FAIL quarantine roundtrip";
            if (GetActiveRemainingSeconds("Hidden Hills", CraftingExpandedItemIds.WildHerbId, 5400f).Count != 0) return "FAIL quarantine expiry";
            Clear();
            ForageDepletionLedger.Clear();
            return "PASS ambiguous grant quarantine";
        }
    }
}
