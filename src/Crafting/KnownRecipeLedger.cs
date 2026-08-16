using System;
using System.Collections.Generic;

namespace ErenshorCraftingExpanded
{
    public sealed class KnownRecipeRecord
    {
        public string StableRecipeId = string.Empty;
        public long LearnedUtcTicks;
        public int PendingTemplateEntitlements;
        public long LastManualRestoreUtcTicks;

        public KnownRecipeRecord Clone()
        {
            return new KnownRecipeRecord
            {
                StableRecipeId = StableRecipeId,
                LearnedUtcTicks = LearnedUtcTicks,
                PendingTemplateEntitlements = PendingTemplateEntitlements,
                LastManualRestoreUtcTicks = LastManualRestoreUtcTicks
            };
        }

        public void Normalize()
        {
            if (LearnedUtcTicks < 0) LearnedUtcTicks = 0;
            if (LastManualRestoreUtcTicks < 0) LastManualRestoreUtcTicks = 0;
            if (PendingTemplateEntitlements < 0) PendingTemplateEntitlements = 0;
            if (PendingTemplateEntitlements > KnownRecipeLedger.MaximumPendingTemplateEntitlements)
                PendingTemplateEntitlements = KnownRecipeLedger.MaximumPendingTemplateEntitlements;
        }
    }

    public sealed class KnownRecipeLedger
    {
        public const int MaximumPendingTemplateEntitlements = 32;
        private readonly Dictionary<string, KnownRecipeRecord> _records = new Dictionary<string, KnownRecipeRecord>(StringComparer.Ordinal);

        public int Count { get { return _records.Count; } }

        public bool IsKnown(string stableRecipeId)
        {
            return !string.IsNullOrEmpty(stableRecipeId) && _records.ContainsKey(stableRecipeId);
        }

        public KnownRecipeRecord Get(string stableRecipeId)
        {
            KnownRecipeRecord record;
            return !string.IsNullOrEmpty(stableRecipeId) && _records.TryGetValue(stableRecipeId, out record) ? record : null;
        }

        public bool LearnNew(string stableRecipeId, long nowUtcTicks)
        {
            if (string.IsNullOrEmpty(stableRecipeId)) return false;
            KnownRecipeRecord existing;
            if (_records.TryGetValue(stableRecipeId, out existing)) return false;
            KnownRecipeRecord record = new KnownRecipeRecord
            {
                StableRecipeId = stableRecipeId,
                LearnedUtcTicks = nowUtcTicks < 0 ? 0 : nowUtcTicks,
                PendingTemplateEntitlements = 1
            };
            record.Normalize();
            _records.Add(stableRecipeId, record);
            return true;
        }

        public bool ImportKnown(string stableRecipeId, long learnedUtcTicks)
        {
            if (string.IsNullOrEmpty(stableRecipeId)) return false;
            KnownRecipeRecord existing;
            if (_records.TryGetValue(stableRecipeId, out existing)) return false;
            KnownRecipeRecord record = new KnownRecipeRecord
            {
                StableRecipeId = stableRecipeId,
                LearnedUtcTicks = learnedUtcTicks < 0 ? 0 : learnedUtcTicks,
                PendingTemplateEntitlements = 0
            };
            record.Normalize();
            _records.Add(stableRecipeId, record);
            return true;
        }

        public bool MarkVerifiedTemplateConsumed(string stableRecipeId, long nowUtcTicks)
        {
            if (string.IsNullOrEmpty(stableRecipeId)) return false;
            KnownRecipeRecord record = Get(stableRecipeId);
            if (record == null)
            {
                ImportKnown(stableRecipeId, nowUtcTicks);
                record = Get(stableRecipeId);
            }
            if (record == null) return false;
            if (record.PendingTemplateEntitlements < MaximumPendingTemplateEntitlements)
                record.PendingTemplateEntitlements++;
            return true;
        }

        public bool ConsumeTemplateEntitlement(string stableRecipeId)
        {
            KnownRecipeRecord record = Get(stableRecipeId);
            if (record == null || record.PendingTemplateEntitlements <= 0) return false;
            record.PendingTemplateEntitlements--;
            return true;
        }

        public bool MarkManualRestoreSucceeded(string stableRecipeId, long nowUtcTicks)
        {
            KnownRecipeRecord record = Get(stableRecipeId);
            if (record == null) return false;
            record.LastManualRestoreUtcTicks = nowUtcTicks < 0 ? 0 : nowUtcTicks;
            return true;
        }

        public List<KnownRecipeRecord> Snapshot()
        {
            List<KnownRecipeRecord> result = new List<KnownRecipeRecord>();
            foreach (KnownRecipeRecord record in _records.Values) result.Add(record.Clone());
            result.Sort(delegate(KnownRecipeRecord a, KnownRecipeRecord b)
            {
                return string.Compare(a == null ? string.Empty : a.StableRecipeId, b == null ? string.Empty : b.StableRecipeId, StringComparison.Ordinal);
            });
            return result;
        }

        public void ReplaceWith(IEnumerable<KnownRecipeRecord> records)
        {
            _records.Clear();
            if (records == null) return;
            foreach (KnownRecipeRecord source in records)
            {
                if (source == null || string.IsNullOrEmpty(source.StableRecipeId) || _records.ContainsKey(source.StableRecipeId)) continue;
                KnownRecipeRecord copy = source.Clone();
                copy.Normalize();
                _records.Add(copy.StableRecipeId, copy);
            }
        }

        public void MergeFrom(KnownRecipeLedger other)
        {
            if (other == null) return;
            List<KnownRecipeRecord> incoming = other.Snapshot();
            for (int i = 0; i < incoming.Count; i++)
            {
                KnownRecipeRecord source = incoming[i];
                KnownRecipeRecord target = Get(source.StableRecipeId);
                if (target == null)
                {
                    _records.Add(source.StableRecipeId, source.Clone());
                    continue;
                }
                if (target.LearnedUtcTicks == 0 || (source.LearnedUtcTicks > 0 && source.LearnedUtcTicks < target.LearnedUtcTicks))
                    target.LearnedUtcTicks = source.LearnedUtcTicks;
                // Merge is migration/recovery-oriented, not an item-duplication transaction.
                // Preserve the stronger outstanding entitlement count rather than adding two
                // snapshots that may describe the same physical loss/consumption event.
                if (source.PendingTemplateEntitlements > target.PendingTemplateEntitlements)
                    target.PendingTemplateEntitlements = source.PendingTemplateEntitlements;
                if (source.LastManualRestoreUtcTicks > target.LastManualRestoreUtcTicks)
                    target.LastManualRestoreUtcTicks = source.LastManualRestoreUtcTicks;
                target.Normalize();
            }
        }

        internal static string RunSelfTests()
        {
            KnownRecipeLedger a = new KnownRecipeLedger();
            if (!a.LearnNew("recipe.a", 100)) return "FAIL learn new";
            if (a.LearnNew("recipe.a", 200)) return "FAIL duplicate learn";
            KnownRecipeRecord r = a.Get("recipe.a");
            if (r == null || r.PendingTemplateEntitlements != 1 || r.LearnedUtcTicks != 100) return "FAIL new entitlement";
            if (!a.ConsumeTemplateEntitlement("recipe.a") || r.PendingTemplateEntitlements != 0) return "FAIL entitlement consume";
            if (!a.MarkVerifiedTemplateConsumed("recipe.a", 300) || r.PendingTemplateEntitlements != 1) return "FAIL craft replacement entitlement";
            if (!a.MarkManualRestoreSucceeded("recipe.a", 400) || r.LastManualRestoreUtcTicks != 400) return "FAIL manual restore timestamp";

            KnownRecipeLedger imported = new KnownRecipeLedger();
            if (!imported.ImportKnown("recipe.old", 50)) return "FAIL import known";
            if (imported.Get("recipe.old").PendingTemplateEntitlements != 0) return "FAIL import created blind entitlement";
            if (!imported.MarkVerifiedTemplateConsumed("recipe.unknown", 500)) return "FAIL craft should import verified recipe";
            if (!imported.IsKnown("recipe.unknown") || imported.Get("recipe.unknown").PendingTemplateEntitlements != 1) return "FAIL verified craft import entitlement";

            KnownRecipeLedger merge = new KnownRecipeLedger();
            merge.ImportKnown("recipe.a", 90);
            merge.MarkVerifiedTemplateConsumed("recipe.a", 100);
            a.MergeFrom(merge);
            r = a.Get("recipe.a");
            if (r.LearnedUtcTicks != 90 || r.PendingTemplateEntitlements != 1) return "FAIL ledger merge must not add duplicate entitlements";
            return "PASS known recipe ledger";
        }
    }
}
