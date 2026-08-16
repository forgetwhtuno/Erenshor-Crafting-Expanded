namespace ErenshorCraftingExpanded
{
    public enum ProductionRecipeRegistrationPhase
    {
        Unbound = 0,
        InertIdentity = 1,
        Active = 2,
        Blocked = 3
    }

    // Pure lifecycle model used by deterministic tests and documentation. Runtime registration
    // follows the same rule: saved ids may exist inertly for save resolution; only current-session
    // native proof authorizes Active; disable/unload returns Active to inert instead of attempting
    // unproven destructive ItemDB removal.
    public static class ProductionRecipeRegistrationState
    {
        public static ProductionRecipeRegistrationPhase OnIdentityAvailable(ProductionRecipeRegistrationPhase current)
        {
            if (current == ProductionRecipeRegistrationPhase.Blocked) return current;
            return current == ProductionRecipeRegistrationPhase.Active ? current : ProductionRecipeRegistrationPhase.InertIdentity;
        }

        public static ProductionRecipeRegistrationPhase OnActivationProof(ProductionRecipeRegistrationPhase current, bool exactBindingProven)
        {
            if (current == ProductionRecipeRegistrationPhase.Blocked) return current;
            if (!exactBindingProven) return ProductionRecipeRegistrationPhase.Blocked;
            return current == ProductionRecipeRegistrationPhase.InertIdentity ? ProductionRecipeRegistrationPhase.Active : current;
        }

        public static ProductionRecipeRegistrationPhase OnDisableOrUnload(ProductionRecipeRegistrationPhase current)
        {
            return current == ProductionRecipeRegistrationPhase.Active ? ProductionRecipeRegistrationPhase.InertIdentity : current;
        }

        public static ProductionRecipeRegistrationPhase OnDatabaseRebuilt(ProductionRecipeRegistrationPhase current)
        {
            return current == ProductionRecipeRegistrationPhase.Blocked ? current : ProductionRecipeRegistrationPhase.Unbound;
        }

        internal static string RunSelfTests()
        {
            ProductionRecipeRegistrationPhase state = ProductionRecipeRegistrationPhase.Unbound;
            state = OnIdentityAvailable(state);
            if (state != ProductionRecipeRegistrationPhase.InertIdentity) return "FAIL production identity phase";
            if (OnIdentityAvailable(state) != ProductionRecipeRegistrationPhase.InertIdentity) return "FAIL production identity idempotence";
            state = OnActivationProof(state, true);
            if (state != ProductionRecipeRegistrationPhase.Active) return "FAIL production activation phase";
            state = OnDisableOrUnload(state);
            if (state != ProductionRecipeRegistrationPhase.InertIdentity) return "FAIL production unload neutralization";
            if (OnDatabaseRebuilt(state) != ProductionRecipeRegistrationPhase.Unbound) return "FAIL production database rebuild";
            if (OnActivationProof(ProductionRecipeRegistrationPhase.InertIdentity, false) != ProductionRecipeRegistrationPhase.Blocked) return "FAIL production failed proof block";
            if (OnDisableOrUnload(ProductionRecipeRegistrationPhase.Blocked) != ProductionRecipeRegistrationPhase.Blocked) return "FAIL production blocked state preserved";
            return "PASS production recipe registration state";
        }
    }
}
