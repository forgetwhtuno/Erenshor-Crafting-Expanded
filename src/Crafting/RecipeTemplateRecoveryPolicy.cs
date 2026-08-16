using System;

namespace ErenshorCraftingExpanded
{
    public enum RecipeRestoreDecisionKind
    {
        NotKnown = 0,
        AlreadyPresent = 1,
        LocationUnknown = 2,
        Cooldown = 3,
        AllowedReplacementEntitlement = 4,
        AllowedConfirmedMissing = 5
    }

    public sealed class RecipeRestoreDecision
    {
        public RecipeRestoreDecisionKind Kind;
        public bool CanRestore;
        public string PlayerReason = string.Empty;
    }

    public static class RecipeTemplateRecoveryPolicy
    {
        public const int ManualRestoreCooldownSeconds = 30;
        public static readonly long ManualRestoreCooldownTicks = TimeSpan.FromSeconds(ManualRestoreCooldownSeconds).Ticks;

        public static RecipeRestoreDecision Evaluate(KnownRecipeRecord record, RecipeTemplateStorageSnapshot storage, long nowUtcTicks)
        {
            RecipeRestoreDecision result = new RecipeRestoreDecision();
            if (record == null)
            {
                result.Kind = RecipeRestoreDecisionKind.NotKnown;
                result.PlayerReason = "Recipe is not known.";
                return result;
            }

            RecipeTemplateLocationState location = storage == null ? RecipeTemplateLocationState.Unknown : storage.Location;
            if (RecipeTemplateStoragePolicy.IsKnownPresent(location))
            {
                result.Kind = RecipeRestoreDecisionKind.AlreadyPresent;
                result.PlayerReason = "Template already exists.";
                return result;
            }

            if (record.PendingTemplateEntitlements > 0)
            {
                result.Kind = RecipeRestoreDecisionKind.AllowedReplacementEntitlement;
                result.CanRestore = true;
                result.PlayerReason = "Replacement template available.";
                return result;
            }

            if (location == RecipeTemplateLocationState.Unknown)
            {
                result.Kind = RecipeRestoreDecisionKind.LocationUnknown;
                result.PlayerReason = "Template location unknown. Check storage before restoring.";
                return result;
            }

            if (record.LastManualRestoreUtcTicks > 0 && nowUtcTicks >= record.LastManualRestoreUtcTicks &&
                nowUtcTicks - record.LastManualRestoreUtcTicks < ManualRestoreCooldownTicks)
            {
                result.Kind = RecipeRestoreDecisionKind.Cooldown;
                result.PlayerReason = "Restore is cooling down.";
                return result;
            }

            result.Kind = RecipeRestoreDecisionKind.AllowedConfirmedMissing;
            result.CanRestore = true;
            result.PlayerReason = "Template confirmed missing.";
            return result;
        }

        public static bool ApplySuccessfulGrant(KnownRecipeRecord record, RecipeRestoreDecision decision, long nowUtcTicks)
        {
            if (record == null || decision == null || !decision.CanRestore) return false;
            if (decision.Kind == RecipeRestoreDecisionKind.AllowedReplacementEntitlement)
            {
                if (record.PendingTemplateEntitlements <= 0) return false;
                record.PendingTemplateEntitlements--;
                return true;
            }
            if (decision.Kind == RecipeRestoreDecisionKind.AllowedConfirmedMissing)
            {
                record.LastManualRestoreUtcTicks = nowUtcTicks < 0 ? 0 : nowUtcTicks;
                return true;
            }
            return false;
        }

        internal static string RunSelfTests()
        {
            long now = TimeSpan.FromMinutes(10).Ticks;
            KnownRecipeRecord known = new KnownRecipeRecord { StableRecipeId = "recipe.a" };
            RecipeTemplateStorageSnapshot present = new RecipeTemplateStorageSnapshot { Location = RecipeTemplateLocationState.Inventory, InventoryQuantity = 1 };
            if (Evaluate(null, present, now).Kind != RecipeRestoreDecisionKind.NotKnown) return "FAIL locked restore";
            if (Evaluate(known, present, now).Kind != RecipeRestoreDecisionKind.AlreadyPresent) return "FAIL present duplicate rejection";

            RecipeTemplateStorageSnapshot unknown = new RecipeTemplateStorageSnapshot { Location = RecipeTemplateLocationState.Unknown };
            if (Evaluate(known, unknown, now).Kind != RecipeRestoreDecisionKind.LocationUnknown) return "FAIL unknown bank should block blind restore";

            known.PendingTemplateEntitlements = 1;
            RecipeRestoreDecision entitlement = Evaluate(known, unknown, now);
            if (!entitlement.CanRestore || entitlement.Kind != RecipeRestoreDecisionKind.AllowedReplacementEntitlement) return "FAIL proven entitlement with unknown bank";
            // A failed native/inventory grant applies no transaction mutation: entitlement remains.
            if (known.PendingTemplateEntitlements != 1) return "FAIL inventory rejection changed entitlement";
            if (!ApplySuccessfulGrant(known, entitlement, now) || known.PendingTemplateEntitlements != 0) return "FAIL restore-once entitlement transaction";
            if (Evaluate(known, unknown, now).CanRestore) return "FAIL consumed entitlement restored twice with unknown bank";

            RecipeTemplateStorageSnapshot bankOnly = new RecipeTemplateStorageSnapshot { Location = RecipeTemplateLocationState.Unknown, BankInspectionAvailable = true, BankQuantity = 0, AuthoritativeAbsenceProbeAvailable = false };
            if (Evaluate(known, bankOnly, now).Kind != RecipeRestoreDecisionKind.LocationUnknown) return "FAIL bank-only probe claimed complete absence";
            RecipeTemplateStorageSnapshot banked = new RecipeTemplateStorageSnapshot { Location = RecipeTemplateLocationState.Bank, BankInspectionAvailable = true, BankQuantity = 1 };
            if (Evaluate(known, banked, now).Kind != RecipeRestoreDecisionKind.AlreadyPresent) return "FAIL banked duplicate rejection";
            RecipeTemplateStorageSnapshot elsewhere = new RecipeTemplateStorageSnapshot { Location = RecipeTemplateLocationState.OtherStorage, AuthoritativeAbsenceProbeAvailable = true, ExternalStorageQuantity = 1 };
            if (Evaluate(known, elsewhere, now).Kind != RecipeRestoreDecisionKind.AlreadyPresent) return "FAIL external-storage duplicate rejection";

            RecipeTemplateStorageSnapshot missing = new RecipeTemplateStorageSnapshot { Location = RecipeTemplateLocationState.ConfirmedMissing, BankInspectionAvailable = true, AuthoritativeAbsenceProbeAvailable = true };
            RecipeRestoreDecision manual = Evaluate(known, missing, now);
            if (!manual.CanRestore) return "FAIL confirmed missing restore";
            if (!ApplySuccessfulGrant(known, manual, now) || known.LastManualRestoreUtcTicks != now) return "FAIL manual restore transaction";
            known.LastManualRestoreUtcTicks = now - TimeSpan.FromSeconds(10).Ticks;
            if (Evaluate(known, missing, now).Kind != RecipeRestoreDecisionKind.Cooldown) return "FAIL restore cooldown";
            known.LastManualRestoreUtcTicks = now - TimeSpan.FromSeconds(31).Ticks;
            if (!Evaluate(known, missing, now).CanRestore) return "FAIL restore cooldown expiry";
            return "PASS recipe template recovery policy";
        }
    }
}
