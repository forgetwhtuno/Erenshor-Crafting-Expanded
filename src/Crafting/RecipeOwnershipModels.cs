using System;
using System.Collections.Generic;

namespace ErenshorCraftingExpanded
{
    public enum RecipeKnowledgeState
    {
        Locked = 0,
        Known = 1
    }

    public enum RecipeTemplateLocationState
    {
        Unknown = 0,
        Inventory = 1,
        Forge = 2,
        Bank = 3,
        OtherStorage = 4,
        ConfirmedMissing = 5
    }

    public sealed class RecipeOwnershipDefinition
    {
        public string StableRecipeId = string.Empty;
        public string TemplateItemId = string.Empty;
        public string DisplayName = string.Empty;
        public int MinimumCraftingLevel = 1;
        public string AdditionalLockReason = string.Empty;
        public bool Deprecated;
    }

    public enum RecipeOwnershipDefinitionRejectReason
    {
        None = 0,
        MissingStableRecipeId,
        MissingTemplateItemId,
        TemplateItemIdOutsideReservedRange,
        MissingDisplayName,
        InvalidMinimumLevel,
        DuplicateStableRecipeId,
        DuplicateTemplateItemId
    }

    public sealed class RecipeOwnershipCatalog
    {
        private readonly Dictionary<string, RecipeOwnershipDefinition> _byRecipeId = new Dictionary<string, RecipeOwnershipDefinition>(StringComparer.Ordinal);
        private readonly Dictionary<string, RecipeOwnershipDefinition> _byTemplateId = new Dictionary<string, RecipeOwnershipDefinition>(StringComparer.Ordinal);

        public int Count { get { return _byRecipeId.Count; } }

        public RecipeOwnershipDefinitionRejectReason Register(RecipeOwnershipDefinition definition)
        {
            RecipeOwnershipDefinitionRejectReason reason = Validate(definition);
            if (reason != RecipeOwnershipDefinitionRejectReason.None) return reason;
            if (_byRecipeId.ContainsKey(definition.StableRecipeId)) return RecipeOwnershipDefinitionRejectReason.DuplicateStableRecipeId;
            if (_byTemplateId.ContainsKey(definition.TemplateItemId)) return RecipeOwnershipDefinitionRejectReason.DuplicateTemplateItemId;
            _byRecipeId.Add(definition.StableRecipeId, definition);
            _byTemplateId.Add(definition.TemplateItemId, definition);
            return RecipeOwnershipDefinitionRejectReason.None;
        }

        public RecipeOwnershipDefinition GetByRecipeId(string stableRecipeId)
        {
            RecipeOwnershipDefinition definition;
            return !string.IsNullOrEmpty(stableRecipeId) && _byRecipeId.TryGetValue(stableRecipeId, out definition) ? definition : null;
        }

        public RecipeOwnershipDefinition GetByTemplateId(string templateItemId)
        {
            RecipeOwnershipDefinition definition;
            return !string.IsNullOrEmpty(templateItemId) && _byTemplateId.TryGetValue(templateItemId, out definition) ? definition : null;
        }

        internal void Clear()
        {
            _byRecipeId.Clear();
            _byTemplateId.Clear();
        }

        public List<RecipeOwnershipDefinition> Snapshot()
        {
            List<RecipeOwnershipDefinition> result = new List<RecipeOwnershipDefinition>(_byRecipeId.Values);
            result.Sort(delegate(RecipeOwnershipDefinition a, RecipeOwnershipDefinition b)
            {
                int byName = string.Compare(a == null ? string.Empty : a.DisplayName, b == null ? string.Empty : b.DisplayName, StringComparison.OrdinalIgnoreCase);
                if (byName != 0) return byName;
                return string.Compare(a == null ? string.Empty : a.StableRecipeId, b == null ? string.Empty : b.StableRecipeId, StringComparison.Ordinal);
            });
            return result;
        }

        public static RecipeOwnershipDefinitionRejectReason Validate(RecipeOwnershipDefinition definition)
        {
            if (definition == null || string.IsNullOrEmpty(definition.StableRecipeId)) return RecipeOwnershipDefinitionRejectReason.MissingStableRecipeId;
            if (string.IsNullOrEmpty(definition.TemplateItemId)) return RecipeOwnershipDefinitionRejectReason.MissingTemplateItemId;
            if (!CraftingExpandedItemIds.IsInRecipeTemplateRange(definition.TemplateItemId)) return RecipeOwnershipDefinitionRejectReason.TemplateItemIdOutsideReservedRange;
            if (string.IsNullOrEmpty(definition.DisplayName)) return RecipeOwnershipDefinitionRejectReason.MissingDisplayName;
            if (definition.MinimumCraftingLevel < 1 || definition.MinimumCraftingLevel > SmithingXpCurve.MaxLevel) return RecipeOwnershipDefinitionRejectReason.InvalidMinimumLevel;
            return RecipeOwnershipDefinitionRejectReason.None;
        }

        internal static string RunSelfTests()
        {
            RecipeOwnershipCatalog catalog = new RecipeOwnershipCatalog();
            RecipeOwnershipDefinition first = new RecipeOwnershipDefinition
            {
                StableRecipeId = "crafting.herbal_preparation",
                TemplateItemId = "910100101",
                DisplayName = "Herbal Preparation",
                MinimumCraftingLevel = 4
            };
            if (catalog.Register(first) != RecipeOwnershipDefinitionRejectReason.None) return "FAIL recipe ownership valid definition";
            if (catalog.GetByRecipeId("crafting.herbal_preparation") != first) return "FAIL recipe ownership id lookup";
            if (catalog.GetByTemplateId("910100101") != first) return "FAIL recipe ownership template lookup";

            RecipeOwnershipDefinition renamed = new RecipeOwnershipDefinition
            {
                StableRecipeId = "crafting.herbal_preparation",
                TemplateItemId = "910100102",
                DisplayName = "Renamed Presentation",
                MinimumCraftingLevel = 4
            };
            if (catalog.Register(renamed) != RecipeOwnershipDefinitionRejectReason.DuplicateStableRecipeId) return "FAIL stable recipe id duplicate";

            RecipeOwnershipDefinition templateCollision = new RecipeOwnershipDefinition
            {
                StableRecipeId = "crafting.other",
                TemplateItemId = "910100101",
                DisplayName = "Other",
                MinimumCraftingLevel = 1
            };
            if (catalog.Register(templateCollision) != RecipeOwnershipDefinitionRejectReason.DuplicateTemplateItemId) return "FAIL template id duplicate";

            RecipeOwnershipDefinition outside = new RecipeOwnershipDefinition
            {
                StableRecipeId = "crafting.bad",
                TemplateItemId = CraftingExpandedItemIds.WildHerbId,
                DisplayName = "Bad",
                MinimumCraftingLevel = 1
            };
            if (RecipeOwnershipCatalog.Validate(outside) != RecipeOwnershipDefinitionRejectReason.TemplateItemIdOutsideReservedRange) return "FAIL template ownership range";

            RecipeTemplateStorageSnapshot storage = new RecipeTemplateStorageSnapshot
            {
                InventoryQuantity = 1,
                BankInspectionAvailable = true,
                BankQuantity = 2,
                AuthoritativeAbsenceProbeAvailable = true,
                ExternalStorageQuantity = 2
            };
            if (storage.VisibleQuantity != 3) return "FAIL storage quantity double counted bank inside comprehensive external count";
            return "PASS recipe ownership catalog";
        }
    }

    public sealed class RecipeTemplateStorageSnapshot
    {
        public string TemplateItemId = string.Empty;
        public int InventoryQuantity;
        public int ForgeQuantity;
        public int BankQuantity;
        public bool BankInspectionAvailable;
        public int ExternalStorageQuantity;
        public bool AuthoritativeAbsenceProbeAvailable;
        public RecipeTemplateLocationState Location;

        public int VisibleQuantity
        {
            get
            {
                long outside = AuthoritativeAbsenceProbeAvailable
                    ? ExternalStorageQuantity
                    : (BankInspectionAvailable ? BankQuantity : 0);
                long total = (long)InventoryQuantity + ForgeQuantity + outside;
                return total > int.MaxValue ? int.MaxValue : (int)total;
            }
        }
    }

    public sealed class RecipeBookRowModel
    {
        public string StableRecipeId = string.Empty;
        public string DisplayName = string.Empty;
        public string TemplateItemId = string.Empty;
        public RecipeKnowledgeState KnowledgeState;
        public RecipeTemplateLocationState TemplateLocation;
        public string StatusText = string.Empty;
        public string LockReason = string.Empty;
        public bool CanRestore;
        public bool HasReplacementEntitlement;
        public bool Deprecated;
    }

    public sealed class RecipeBookSnapshot
    {
        public readonly List<RecipeBookRowModel> Known = new List<RecipeBookRowModel>();
        public readonly List<RecipeBookRowModel> Locked = new List<RecipeBookRowModel>();
        public int KnownCount;
        public int TotalCount;
        public bool CharacterPersistenceAvailable;
        public string PersistenceStatus = string.Empty;
        public string LastPlayerMessage = string.Empty;
    }
}
