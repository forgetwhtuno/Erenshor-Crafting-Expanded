using System.Collections.Generic;

namespace ErenshorCraftingExpanded
{
    public enum CustomItemDefinitionRejectReason
    {
        None = 0,
        MissingId,
        IdOutsideOwnedRange,
        DuplicateDefinition,
        MissingName,
        InvalidDefaultQuantity
    }

    public enum CustomItemRegistrationState
    {
        Uninitialized = 0,
        Registered = 1,
        Collision = 2,
        Unavailable = 3
    }

    // Pure-logic decision for what happens when a definition is registered against the live
    // native ItemDatabase. Kept separate from GameItemRegistryApi (which supplies the three
    // booleans from real reflection calls) so the decision itself is unit-testable without Unity.
    public static class CustomItemRegistrationPolicy
    {
        public static CustomItemRegistrationState Evaluate(bool definitionValid, bool nativeEntryExists, bool nativeEntryIsOwnedByUs)
        {
            if (!definitionValid) return CustomItemRegistrationState.Unavailable;
            if (!nativeEntryExists) return CustomItemRegistrationState.Registered;
            if (nativeEntryIsOwnedByUs) return CustomItemRegistrationState.Registered; // idempotent re-registration
            return CustomItemRegistrationState.Collision;
        }
    }

    // Pure catalog of this mod's own item definitions - the "what we intend to register" side,
    // as distinct from CustomItemRegistrationPolicy's "what happened when we tried" decision.
    // Mirrors ForageNodeCatalog's shape deliberately for consistency.
    public sealed class CustomItemRegistry
    {
        private readonly Dictionary<string, CustomItemDefinition> _byId = new Dictionary<string, CustomItemDefinition>();

        public static CustomItemDefinitionRejectReason Validate(CustomItemDefinition definition, CustomItemRegistry existing)
        {
            if (definition == null || string.IsNullOrEmpty(definition.Id)) return CustomItemDefinitionRejectReason.MissingId;
            if (!CraftingExpandedItemIds.IsInOwnedRange(definition.Id)) return CustomItemDefinitionRejectReason.IdOutsideOwnedRange;
            if (existing != null && existing._byId.ContainsKey(definition.Id)) return CustomItemDefinitionRejectReason.DuplicateDefinition;
            if (string.IsNullOrEmpty(definition.Name)) return CustomItemDefinitionRejectReason.MissingName;
            if (definition.DefaultGrantQuantity <= 0) return CustomItemDefinitionRejectReason.InvalidDefaultQuantity;
            return CustomItemDefinitionRejectReason.None;
        }

        public CustomItemDefinitionRejectReason TryDefine(CustomItemDefinition definition)
        {
            CustomItemDefinitionRejectReason reason = Validate(definition, this);
            if (reason != CustomItemDefinitionRejectReason.None) return reason;
            _byId[definition.Id] = definition;
            return CustomItemDefinitionRejectReason.None;
        }

        public CustomItemDefinition Get(string id)
        {
            CustomItemDefinition def;
            return _byId.TryGetValue(id, out def) ? def : null;
        }

        public IEnumerable<CustomItemDefinition> All { get { return _byId.Values; } }
        public int Count { get { return _byId.Count; } }

        internal static string RunSelfTests()
        {
            // --- CustomItemRegistrationPolicy: id collision policy ---
            if (CustomItemRegistrationPolicy.Evaluate(true, false, false) != CustomItemRegistrationState.Registered)
                return "FAIL free id should allow registration";
            if (CustomItemRegistrationPolicy.Evaluate(true, true, true) != CustomItemRegistrationState.Registered)
                return "FAIL existing owned item should be idempotent success";
            if (CustomItemRegistrationPolicy.Evaluate(true, true, false) != CustomItemRegistrationState.Collision)
                return "FAIL existing foreign item should be rejected as collision";
            if (CustomItemRegistrationPolicy.Evaluate(false, false, false) != CustomItemRegistrationState.Unavailable)
                return "FAIL invalid definition should be unavailable regardless of native state";

            // --- CustomItemRegistry: definition validation ---
            CustomItemRegistry registry = new CustomItemRegistry();
            CustomItemDefinition valid = new CustomItemDefinition { Id = CraftingExpandedItemIds.WildHerbId, Name = "Wild Herb", DefaultGrantQuantity = 1 };
            if (registry.TryDefine(valid) != CustomItemDefinitionRejectReason.None) return "FAIL valid definition rejected";

            CustomItemDefinition duplicate = new CustomItemDefinition { Id = CraftingExpandedItemIds.WildHerbId, Name = "Wild Herb Again", DefaultGrantQuantity = 1 };
            if (registry.TryDefine(duplicate) != CustomItemDefinitionRejectReason.DuplicateDefinition) return "FAIL duplicate definition accepted";

            CustomItemDefinition emptyId = new CustomItemDefinition { Id = "", Name = "X", DefaultGrantQuantity = 1 };
            if (registry.TryDefine(emptyId) != CustomItemDefinitionRejectReason.MissingId) return "FAIL empty id accepted";

            CustomItemDefinition outsideRange = new CustomItemDefinition { Id = "12345", Name = "X", DefaultGrantQuantity = 1 };
            if (registry.TryDefine(outsideRange) != CustomItemDefinitionRejectReason.IdOutsideOwnedRange) return "FAIL id outside owned range accepted";

            CustomItemDefinition emptyName = new CustomItemDefinition { Id = "910000002", Name = "", DefaultGrantQuantity = 1 };
            if (registry.TryDefine(emptyName) != CustomItemDefinitionRejectReason.MissingName) return "FAIL empty name accepted";

            CustomItemDefinition badQty = new CustomItemDefinition { Id = "910000003", Name = "X", DefaultGrantQuantity = 0 };
            if (registry.TryDefine(badQty) != CustomItemDefinitionRejectReason.InvalidDefaultQuantity) return "FAIL invalid default quantity accepted";

            CustomItemDefinition cave = new CustomItemDefinition { Id = CraftingExpandedItemIds.CaveMushroomId, Name = "Cave Mushroom", DefaultGrantQuantity = 1, VisualKind = CustomItemVisualKind.OrganicFungus };
            if (registry.TryDefine(cave) != CustomItemDefinitionRejectReason.None) return "FAIL Cave Mushroom definition rejected";
            CustomItemDefinition bloom = new CustomItemDefinition { Id = CraftingExpandedItemIds.WildBloomId, Name = "Wild Bloom", DefaultGrantQuantity = 1, VisualKind = CustomItemVisualKind.OrganicFlower };
            if (registry.TryDefine(bloom) != CustomItemDefinitionRejectReason.None) return "FAIL Wild Bloom definition rejected";
            CustomItemDefinition moss = new CustomItemDefinition { Id = CraftingExpandedItemIds.CaveMossId, Name = "Cave Moss", DefaultGrantQuantity = 1, VisualKind = CustomItemVisualKind.OrganicMoss };
            if (registry.TryDefine(moss) != CustomItemDefinitionRejectReason.None) return "FAIL Cave Moss definition rejected";
            CustomItemDefinition root = new CustomItemDefinition { Id = CraftingExpandedItemIds.BlightrootId, Name = "Blightroot", DefaultGrantQuantity = 1, VisualKind = CustomItemVisualKind.OrganicRoot };
            if (registry.TryDefine(root) != CustomItemDefinitionRejectReason.None) return "FAIL Blightroot definition rejected";

            if (registry.Get(CraftingExpandedItemIds.CaveMushroomId) == null ||
                registry.Get(CraftingExpandedItemIds.CaveMushroomId).VisualKind != CustomItemVisualKind.OrganicFungus)
                return "FAIL Cave Mushroom visual kind";
            if (registry.Get(CraftingExpandedItemIds.WildBloomId).VisualKind != CustomItemVisualKind.OrganicFlower)
                return "FAIL Wild Bloom visual kind";
            if (registry.Get(CraftingExpandedItemIds.CaveMossId).VisualKind != CustomItemVisualKind.OrganicMoss)
                return "FAIL Cave Moss visual kind";
            if (registry.Get(CraftingExpandedItemIds.BlightrootId).VisualKind != CustomItemVisualKind.OrganicRoot)
                return "FAIL Blightroot visual kind";

            if (registry.Count != 5) return "FAIL expected five owned resource definitions";

            return "PASS custom item registry";
        }
    }
}
