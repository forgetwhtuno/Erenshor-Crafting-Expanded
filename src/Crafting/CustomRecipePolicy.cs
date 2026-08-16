using System;
using System.Collections.Generic;

namespace ErenshorCraftingExpanded
{
    public sealed class CustomRecipeIngredient
    {
        public string ItemId;
        public int Quantity;

        public CustomRecipeIngredient(string itemId, int quantity)
        {
            ItemId = itemId ?? string.Empty;
            Quantity = quantity;
        }
    }

    // Plain-data permanent-recipe definition. Production entries are allowed only when every
    // native item id is verified. Physical Template ownership is intentionally not represented
    // here; permanent recipe knowledge and physical-template ownership live in RecipeOwnershipController.
    public sealed class CustomRecipeDefinition
    {
        public string RecipeKey = string.Empty;
        public string TemplateItemId = string.Empty;
        public string DisplayName = string.Empty;
        public string OutputItemId = string.Empty;
        public int MinimumCraftingLevel = 1;
        public int MinimumForagingLevel = 1;
        public readonly List<CustomRecipeIngredient> Ingredients = new List<CustomRecipeIngredient>();
        public readonly List<string> RequiredDiscoveries = new List<string>();
    }

    public enum CustomRecipeRejectReason
    {
        None = 0,
        MissingKey,
        MissingTemplateItemId,
        TemplateItemIdOutsideReservedRange,
        MissingDisplayName,
        MissingOutputItemId,
        InvalidSkillLevel,
        InvalidForagingLevel,
        MissingIngredients,
        InvalidIngredient,
        DuplicateIngredient,
        InvalidDiscovery,
        DuplicateDiscovery,
        InconsistentDiscoverySkillRequirement,
        InconsistentResourceSkillRequirement,
        DuplicateRecipeKey,
        DuplicateTemplateItemId
    }

    public sealed class CustomRecipeCatalog
    {
        private readonly Dictionary<string, CustomRecipeDefinition> _byKey = new Dictionary<string, CustomRecipeDefinition>(StringComparer.Ordinal);
        private readonly Dictionary<string, CustomRecipeDefinition> _byTemplateId = new Dictionary<string, CustomRecipeDefinition>(StringComparer.Ordinal);
        private readonly List<CustomRecipeDefinition> _all = new List<CustomRecipeDefinition>();
        private readonly IList<CustomRecipeDefinition> _readOnly;

        public CustomRecipeCatalog()
        {
            _readOnly = _all.AsReadOnly();
        }

        public int Count { get { return _all.Count; } }
        public IList<CustomRecipeDefinition> All { get { return _readOnly; } }

        public CustomRecipeRejectReason TryAdd(CustomRecipeDefinition definition)
        {
            CustomRecipeRejectReason reason = ValidateShape(definition);
            if (reason != CustomRecipeRejectReason.None) return reason;
            if (_byKey.ContainsKey(definition.RecipeKey)) return CustomRecipeRejectReason.DuplicateRecipeKey;
            if (_byTemplateId.ContainsKey(definition.TemplateItemId)) return CustomRecipeRejectReason.DuplicateTemplateItemId;
            _byKey.Add(definition.RecipeKey, definition);
            _byTemplateId.Add(definition.TemplateItemId, definition);
            _all.Add(definition);
            return CustomRecipeRejectReason.None;
        }

        public CustomRecipeDefinition Get(string recipeKey)
        {
            CustomRecipeDefinition definition;
            return recipeKey != null && _byKey.TryGetValue(recipeKey, out definition) ? definition : null;
        }

        public CustomRecipeDefinition GetByTemplateId(string templateItemId)
        {
            CustomRecipeDefinition definition;
            return templateItemId != null && _byTemplateId.TryGetValue(templateItemId, out definition) ? definition : null;
        }

        internal void Clear()
        {
            _byKey.Clear();
            _byTemplateId.Clear();
            _all.Clear();
        }

        public static CustomRecipeRejectReason ValidateShape(CustomRecipeDefinition definition)
        {
            if (definition == null || string.IsNullOrEmpty(definition.RecipeKey)) return CustomRecipeRejectReason.MissingKey;
            if (string.IsNullOrEmpty(definition.TemplateItemId)) return CustomRecipeRejectReason.MissingTemplateItemId;
            if (!CraftingExpandedItemIds.IsInRecipeTemplateRange(definition.TemplateItemId)) return CustomRecipeRejectReason.TemplateItemIdOutsideReservedRange;
            if (string.IsNullOrEmpty(definition.DisplayName)) return CustomRecipeRejectReason.MissingDisplayName;
            if (string.IsNullOrEmpty(definition.OutputItemId)) return CustomRecipeRejectReason.MissingOutputItemId;
            if (definition.MinimumCraftingLevel < 1 || definition.MinimumCraftingLevel > SmithingXpCurve.MaxLevel)
                return CustomRecipeRejectReason.InvalidSkillLevel;
            if (definition.MinimumForagingLevel < 1 || definition.MinimumForagingLevel > ForagingXpCurve.MaxLevel)
                return CustomRecipeRejectReason.InvalidForagingLevel;
            if (definition.Ingredients == null || definition.Ingredients.Count == 0) return CustomRecipeRejectReason.MissingIngredients;

            HashSet<string> ingredientIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < definition.Ingredients.Count; i++)
            {
                CustomRecipeIngredient ingredient = definition.Ingredients[i];
                if (ingredient == null || string.IsNullOrEmpty(ingredient.ItemId) || ingredient.Quantity <= 0)
                    return CustomRecipeRejectReason.InvalidIngredient;
                if (!ingredientIds.Add(ingredient.ItemId)) return CustomRecipeRejectReason.DuplicateIngredient;
                ForageResourceDefinition ingredientResource = ForageResourceCatalog.FindByRewardItemId(ingredient.ItemId);
                if (ingredientResource != null && definition.MinimumForagingLevel < ingredientResource.MinimumSkill)
                    return CustomRecipeRejectReason.InconsistentResourceSkillRequirement;
            }

            HashSet<string> discoveries = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < definition.RequiredDiscoveries.Count; i++)
            {
                string key = definition.RequiredDiscoveries[i];
                if (string.IsNullOrEmpty(key)) return CustomRecipeRejectReason.InvalidDiscovery;
                if (!discoveries.Add(key)) return CustomRecipeRejectReason.DuplicateDiscovery;
                ForageResourceDefinition resource = ForageResourceCatalog.FindByKnowledgeKey(key);
                if (resource != null && definition.MinimumForagingLevel < resource.MinimumSkill)
                    return CustomRecipeRejectReason.InconsistentDiscoverySkillRequirement;
            }
            return CustomRecipeRejectReason.None;
        }
    }

    public static class RecipeUnlockPolicy
    {
        public static bool IsUnlocked(int smithingLevel, int minimumSmithingLevel)
        {
            if (smithingLevel < 1) smithingLevel = 1;
            if (smithingLevel > SmithingXpCurve.MaxLevel) smithingLevel = SmithingXpCurve.MaxLevel;
            if (minimumSmithingLevel < 1) minimumSmithingLevel = 1;
            if (minimumSmithingLevel > SmithingXpCurve.MaxLevel) minimumSmithingLevel = SmithingXpCurve.MaxLevel;
            return smithingLevel >= minimumSmithingLevel;
        }

        public static bool IsUnlocked(int smithingLevel, CustomRecipeDefinition definition, IRecipeDiscoverySource discoveries)
        {
            return IsUnlocked(smithingLevel, ForagingXpCurve.MaxLevel, definition, discoveries);
        }

        public static bool IsUnlocked(int craftingLevel, int foragingLevel, CustomRecipeDefinition definition, IRecipeDiscoverySource discoveries)
        {
            if (definition == null || !IsUnlocked(craftingLevel, definition.MinimumCraftingLevel)) return false;
            if (foragingLevel < 1) foragingLevel = 1;
            if (foragingLevel > ForagingXpCurve.MaxLevel) foragingLevel = ForagingXpCurve.MaxLevel;
            if (foragingLevel < definition.MinimumForagingLevel) return false;
            if (definition.RequiredDiscoveries.Count == 0) return true;
            if (discoveries == null) return false;
            for (int i = 0; i < definition.RequiredDiscoveries.Count; i++)
                if (!discoveries.HasDiscovery(definition.RequiredDiscoveries[i])) return false;
            return true;
        }
    }


    public static class RecipeAccessPolicy
    {
        public static bool CanLearn(int craftingLevel, int foragingLevel, CustomRecipeDefinition definition, IRecipeDiscoverySource discoveries)
        {
            return RecipeUnlockPolicy.IsUnlocked(craftingLevel, foragingLevel, definition, discoveries);
        }

        // A recipe may be known while its physical Template is in storage or missing. Native
        // Smithing still requires the actual Template item in the forge, so use requires both
        // permanent knowledge and a present physical template in addition to progression gates.
        public static bool CanUse(bool recipeKnown, bool physicalTemplatePresent, int craftingLevel, int foragingLevel,
            CustomRecipeDefinition definition, IRecipeDiscoverySource discoveries)
        {
            return recipeKnown && physicalTemplatePresent && CanLearn(craftingLevel, foragingLevel, definition, discoveries);
        }
    }

    public enum RecipeRegistrationTransition
    {
        Registered = 0,
        AlreadyRegistered = 1,
        Removed = 2,
        NotRegistered = 3
    }

    public sealed class RecipeRegistrationLedger
    {
        private readonly HashSet<string> _registeredTemplateIds = new HashSet<string>(StringComparer.Ordinal);
        public int Count { get { return _registeredTemplateIds.Count; } }

        public RecipeRegistrationTransition Register(string templateItemId)
        {
            if (string.IsNullOrEmpty(templateItemId)) return RecipeRegistrationTransition.AlreadyRegistered;
            return _registeredTemplateIds.Add(templateItemId) ? RecipeRegistrationTransition.Registered : RecipeRegistrationTransition.AlreadyRegistered;
        }

        public RecipeRegistrationTransition Remove(string templateItemId)
        {
            if (string.IsNullOrEmpty(templateItemId) || !_registeredTemplateIds.Remove(templateItemId)) return RecipeRegistrationTransition.NotRegistered;
            return RecipeRegistrationTransition.Removed;
        }
    }

    public static class NativeRecipeMutationGate
    {
        public static bool CanMutate(bool currentRecipeShapeProven, bool templateAcquisitionProven, bool usefulOutputProven,
            bool insertRemoveLifecycleProven, bool disableUnloadBehaviorProven)
        {
            return currentRecipeShapeProven && templateAcquisitionProven && usefulOutputProven &&
                insertRemoveLifecycleProven && disableUnloadBehaviorProven;
        }

        internal static string RunSelfTests()
        {
            CustomRecipeCatalog catalog = new CustomRecipeCatalog();
            CustomRecipeDefinition herbRecipe = new CustomRecipeDefinition
            {
                RecipeKey = "test.herb.aid", TemplateItemId = "910100001", DisplayName = "Test Herb Aid",
                OutputItemId = "native-output", MinimumCraftingLevel = 3, MinimumForagingLevel = 2
            };
            herbRecipe.Ingredients.Add(new CustomRecipeIngredient(CraftingExpandedItemIds.WildHerbId, 2));
            herbRecipe.Ingredients.Add(new CustomRecipeIngredient("native-common", 1));
            herbRecipe.RequiredDiscoveries.Add(RecipeDiscoveryKeys.WildHerb);
            if (catalog.TryAdd(herbRecipe) != CustomRecipeRejectReason.None) return "FAIL valid custom recipe definition";
            if (catalog.Count != 1 || catalog.Get("test.herb.aid") == null || catalog.GetByTemplateId("910100001") == null) return "FAIL custom recipe lookup";

            MutableRecipeDiscoverySource knowledge = new MutableRecipeDiscoverySource();
            if (RecipeUnlockPolicy.IsUnlocked(3, 2, herbRecipe, knowledge)) return "FAIL discovery gate ignored";
            knowledge.MarkDiscovery(RecipeDiscoveryKeys.WildHerb);
            if (RecipeUnlockPolicy.IsUnlocked(3, 1, herbRecipe, knowledge)) return "FAIL foraging gate unlocked too early";
            if (!RecipeUnlockPolicy.IsUnlocked(3, 2, herbRecipe, knowledge)) return "FAIL combined unlock boundary";
            if (RecipeUnlockPolicy.IsUnlocked(2, 2, herbRecipe, knowledge)) return "FAIL crafting skill gate unlocked too early";
            if (RecipeAccessPolicy.CanUse(false, true, 3, 2, herbRecipe, knowledge)) return "FAIL unknown recipe usable";
            if (RecipeAccessPolicy.CanUse(true, false, 3, 2, herbRecipe, knowledge)) return "FAIL missing physical template usable";
            if (!RecipeAccessPolicy.CanUse(true, true, 3, 2, herbRecipe, knowledge)) return "FAIL complete recipe access rejected";

            CustomRecipeDefinition duplicateKey = new CustomRecipeDefinition { RecipeKey = "test.herb.aid", TemplateItemId = "910100002", DisplayName = "Other", OutputItemId = "native-output", MinimumCraftingLevel = 1 };
            duplicateKey.Ingredients.Add(new CustomRecipeIngredient(CraftingExpandedItemIds.WildHerbId, 1));
            if (catalog.TryAdd(duplicateKey) != CustomRecipeRejectReason.DuplicateRecipeKey) return "FAIL recipe-key dedupe";

            CustomRecipeDefinition duplicateTemplate = new CustomRecipeDefinition { RecipeKey = "test.other", TemplateItemId = "910100001", DisplayName = "Other", OutputItemId = "native-output", MinimumCraftingLevel = 1 };
            duplicateTemplate.Ingredients.Add(new CustomRecipeIngredient(CraftingExpandedItemIds.WildHerbId, 1));
            if (catalog.TryAdd(duplicateTemplate) != CustomRecipeRejectReason.DuplicateTemplateItemId) return "FAIL template-id dedupe";

            CustomRecipeDefinition duplicateIngredient = new CustomRecipeDefinition { RecipeKey = "test.bad.ingredients", TemplateItemId = "910100003", DisplayName = "Bad", OutputItemId = "native-output", MinimumCraftingLevel = 1 };
            duplicateIngredient.Ingredients.Add(new CustomRecipeIngredient(CraftingExpandedItemIds.WildHerbId, 1));
            duplicateIngredient.Ingredients.Add(new CustomRecipeIngredient(CraftingExpandedItemIds.WildHerbId, 1));
            if (CustomRecipeCatalog.ValidateShape(duplicateIngredient) != CustomRecipeRejectReason.DuplicateIngredient) return "FAIL duplicate ingredient validation";
            CustomRecipeDefinition duplicateDiscovery = new CustomRecipeDefinition { RecipeKey = "test.bad.discovery", TemplateItemId = "910100004", DisplayName = "Bad Discovery", OutputItemId = "native-output", MinimumCraftingLevel = 1 };
            duplicateDiscovery.Ingredients.Add(new CustomRecipeIngredient(CraftingExpandedItemIds.WildHerbId, 1));
            duplicateDiscovery.RequiredDiscoveries.Add(RecipeDiscoveryKeys.WildHerb);
            duplicateDiscovery.RequiredDiscoveries.Add(RecipeDiscoveryKeys.WildHerb);
            if (CustomRecipeCatalog.ValidateShape(duplicateDiscovery) != CustomRecipeRejectReason.DuplicateDiscovery) return "FAIL duplicate discovery validation";

            CustomRecipeDefinition badForaging = new CustomRecipeDefinition { RecipeKey = "test.bad.foraging", TemplateItemId = "910100005", DisplayName = "Bad Foraging", OutputItemId = "native-output", MinimumCraftingLevel = 1, MinimumForagingLevel = 51 };
            badForaging.Ingredients.Add(new CustomRecipeIngredient(CraftingExpandedItemIds.WildHerbId, 1));
            if (CustomRecipeCatalog.ValidateShape(badForaging) != CustomRecipeRejectReason.InvalidForagingLevel) return "FAIL invalid Foraging level accepted";

            CustomRecipeDefinition implicitCaveIngredientGate = new CustomRecipeDefinition { RecipeKey = "test.bad.caveingredient", TemplateItemId = "910100006", DisplayName = "Bad Cave Ingredient Gate", OutputItemId = "native-output", MinimumCraftingLevel = 1, MinimumForagingLevel = 1 };
            implicitCaveIngredientGate.Ingredients.Add(new CustomRecipeIngredient(CraftingExpandedItemIds.CaveMushroomId, 1));
            if (CustomRecipeCatalog.ValidateShape(implicitCaveIngredientGate) != CustomRecipeRejectReason.InconsistentResourceSkillRequirement) return "FAIL implicit ingredient skill gate accepted";

            CustomRecipeDefinition implicitCaveDiscoveryGate = new CustomRecipeDefinition { RecipeKey = "test.bad.cavediscovery", TemplateItemId = "910100007", DisplayName = "Bad Cave Discovery Gate", OutputItemId = "native-output", MinimumCraftingLevel = 1, MinimumForagingLevel = 1 };
            implicitCaveDiscoveryGate.Ingredients.Add(new CustomRecipeIngredient(CraftingExpandedItemIds.WildHerbId, 1));
            implicitCaveDiscoveryGate.RequiredDiscoveries.Add(RecipeDiscoveryKeys.CaveMushroom);
            if (CustomRecipeCatalog.ValidateShape(implicitCaveDiscoveryGate) != CustomRecipeRejectReason.InconsistentDiscoverySkillRequirement) return "FAIL implicit discovery skill gate accepted";

            RecipeRegistrationLedger ledger = new RecipeRegistrationLedger();
            if (ledger.Register("910100001") != RecipeRegistrationTransition.Registered) return "FAIL lifecycle first register";
            if (ledger.Register("910100001") != RecipeRegistrationTransition.AlreadyRegistered || ledger.Count != 1) return "FAIL lifecycle duplicate register";
            if (ledger.Remove("910100001") != RecipeRegistrationTransition.Removed || ledger.Remove("910100001") != RecipeRegistrationTransition.NotRegistered) return "FAIL lifecycle remove";

            if (CanMutate(true, false, false, false, false)) return "FAIL runtime shape alone authorized production mutation";
            if (!CanMutate(true, true, true, true, true)) return "FAIL complete production evidence rejected";
            return "PASS custom recipe policies";
        }
    }
}
