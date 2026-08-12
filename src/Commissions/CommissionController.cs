using System;

namespace ErenshorCraftingExpanded
{
    // Ties CommissionPolicy (pure) to live Sim/recipe state. Keeps exactly one active
    // commission, per the user's v1 scope. Generation is deliberately conservative: a
    // commission can only ever request the item that is the reward of whatever recipe the
    // player currently has loaded in the forge - a real, verified, currently-known recipe - not
    // an invented item or lore. See docs/NATIVE_CRAFTING_FINDINGS.md for why a broader
    // "known recipe catalog" isn't attempted in v1 (no native recipe list was found).
    internal static class CommissionController
    {
        internal static CraftingCommission Current;
        internal static string LastRejectionReason = string.Empty;
        private static string _lastOfferedTemplateId = string.Empty;

        internal static bool HasActiveCommission()
        {
            return Current != null &&
                (Current.State == CommissionState.Offered || Current.State == CommissionState.Accepted);
        }

        internal static void TryOfferFromCurrentRecipe()
        {
            if (!CraftingConfig.EnableCraftingRequests.Value) return;
            if (HasActiveCommission()) return;

            CraftRecipeSnapshot recipe = GameCraftingApi.TryGetActiveRecipe();
            if (recipe == null || string.IsNullOrEmpty(recipe.OutputItemId)) return;
            if (string.Equals(recipe.TemplateItemId, _lastOfferedTemplateId, StringComparison.Ordinal)) return;

            var candidates = SimIdentityApi.GetEligibleLocalSims();
            foreach (SimIdentitySnapshot sim in candidates)
            {
                // Native per-class item-usability was not confirmed this research pass (see
                // findings doc "Open / unverified items") - documented limitation, defaults to
                // true rather than inventing a check. Level gate still applies.
                bool itemUsableByClass = true;
                int requiredLevel = 1;
                CommissionRejectReason reason = CommissionPolicy.Evaluate(
                    sim, isRemoteHuman: false, hasActiveCommission: HasActiveCommission(),
                    itemUsableByClass: itemUsableByClass, recipeRequiredLevel: requiredLevel);
                if (reason != CommissionRejectReason.None) { LastRejectionReason = reason.ToString(); continue; }

                Current = new CraftingCommission
                {
                    RequestId = Guid.NewGuid().ToString("N"),
                    SimRuntimeKey = sim.RuntimeKey,
                    SimName = sim.Name,
                    RequestedItemId = recipe.OutputItemId,
                    RequestedItemName = recipe.OutputItemName,
                    State = CommissionState.Offered,
                    OfferedUtc = DateTime.UtcNow
                };
                _lastOfferedTemplateId = recipe.TemplateItemId;
                LastRejectionReason = string.Empty;
                return;
            }
        }

        internal static void Accept()
        {
            if (Current != null && Current.State == CommissionState.Offered) Current.State = CommissionState.Accepted;
        }

        internal static void Decline()
        {
            if (Current != null && (Current.State == CommissionState.Offered || Current.State == CommissionState.Accepted))
            {
                Current.State = CommissionState.Declined;
                Current = null;
            }
        }

        // Called once per verified native craft success (see CraftSuccessPatch). Completion
        // happens exactly once because State is checked-and-flipped atomically here before any
        // reward is granted, and a completed/declined/invalidated commission is never re-checked.
        internal static bool TryCompleteFromCraft(CraftRecipeSnapshot recipe)
        {
            if (Current == null || Current.State != CommissionState.Accepted) return false;
            if (recipe == null || string.IsNullOrEmpty(recipe.OutputItemId)) return false;
            if (!string.Equals(recipe.OutputItemId, Current.RequestedItemId, StringComparison.Ordinal)) return false;

            Current.State = CommissionState.Completed;
            Current.CompletedUtc = DateTime.UtcNow;
            return true;
        }

        // Scene-local safety: drop any PoC commission whose Sim can no longer be resolved among
        // currently active local Sims. Never keeps a scene-bound reference around - only the
        // loaded-instance runtime key captured at offer time is compared. This is intentionally
        // not presented as persistent Sim identity; SceneTransition invalidates all active PoC
        // commissions rather than trying to rebind it across zones.
        internal static void RevalidateAgainstLiveSims()
        {
            if (Current == null) return;
            if (Current.State != CommissionState.Offered && Current.State != CommissionState.Accepted) return;

            var live = SimIdentityApi.GetEligibleLocalSims();
            foreach (SimIdentitySnapshot sim in live)
                if (string.Equals(sim.RuntimeKey, Current.SimRuntimeKey, StringComparison.Ordinal)) return;

            Current.State = CommissionState.Invalidated;
            Current = null;
        }

        internal static void OnForgeClosed()
        {
            // Declining/completing a PoC request should not cause the exact same recipe to
            // re-offer on the very next frame. Closing/reopening the forge is an explicit reset.
            _lastOfferedTemplateId = string.Empty;
        }

        internal static void SceneTransition()
        {
            // The current identity adapter uses a loaded-instance key (GameObject instance id +
            // name), not a proven persistent Sim id. Therefore *all* active PoC commissions are
            // scene-local and are invalidated on zoning rather than pretending an accepted one
            // can be safely rebound to a new runtime Sim object.
            if (Current != null && (Current.State == CommissionState.Offered || Current.State == CommissionState.Accepted))
            {
                Current.State = CommissionState.Invalidated;
                Current = null;
            }
            _lastOfferedTemplateId = string.Empty;
        }
    }
}
