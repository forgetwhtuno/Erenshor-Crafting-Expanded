using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace ErenshorCraftingExpanded
{
    internal sealed class ExperimentalRecipeCandidate
    {
        internal object TemplateItem;
        internal object OutputItem;
        internal string TemplateId = string.Empty;
        internal string TemplateName = string.Empty;
        internal string OutputId = string.Empty;
        internal string OutputName = string.Empty;
        internal int OutputValue;
        internal int IngredientEntries;
        internal int DistinctIngredients;
    }

    internal sealed class ProductionNativeRecipeCandidate
    {
        internal object TemplateItem;
        internal object OutputItem;
        internal object ExtraIngredientItem;
        internal string TemplateId = string.Empty;
        internal string TemplateName = string.Empty;
        internal string OutputId = string.Empty;
        internal string OutputName = string.Empty;
        internal int OutputValue;
        internal readonly List<string> IngredientIds = new List<string>();
        internal string IngredientFingerprint = string.Empty;
        internal int DistinctIngredients;
        internal ProductionRecipeContentKind ContentKind;
        internal string EffectTypeName = string.Empty;
        internal string ExtraIngredientId = string.Empty;
    }

    // Native recipe compatibility bridge. Both the developer verification recipe and the bounded
    // production catalog use current-runtime reflection plus the same ItemDB insertion primitive
    // as verified custom materials. Native Smithing still owns ingredient matching, consumption,
    // and reward production; this class only proves/configures physical Template definitions.
    internal static class GameNativeRecipeRegistryApi
    {
        private const BindingFlags AllInstance = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        private const BindingFlags AllStatic = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;

        internal static List<ExperimentalRecipeCandidate> FindCandidates(object itemDatabaseInstance)
        {
            List<ExperimentalRecipeCandidate> result = new List<ExperimentalRecipeCandidate>();
            if (itemDatabaseInstance == null) return result;
            try
            {
                Type itemType = FindType("Item");
                UnityEngine.Object[] nativeItems = itemType == null ? null : Resources.LoadAll("Items", itemType);
                int componentSlots = ReadComponentSlotCapacity();
                if (nativeItems == null || nativeItems.Length == 0 || componentSlots <= 0) return result;
                for (int i = 0; i < nativeItems.Length; i++)
                {
                    object candidate = nativeItems[i];
                    if (candidate == null) continue;
                    bool template = ReadBool(candidate, "Template");
                    object fuelSourceObject = ReadField(candidate, "FuelSource");
                    string templateId = ReadString(candidate, "Id");
                    string templateName = ReadString(candidate, "ItemName");
                    if (!(fuelSourceObject is bool) || (bool)fuelSourceObject || string.IsNullOrEmpty(templateId) || string.IsNullOrEmpty(templateName) || CraftingExpandedItemIds.IsInOwnedRange(templateId)) continue;
                    object liveNative = GameItemRegistryApi.TryGetLiveItem(itemDatabaseInstance, templateId);
                    if (liveNative == null || !ReferenceEquals(liveNative, candidate)) continue;
                    IList ingredients = ReadField(candidate, "TemplateIngredients") as IList;
                    IList rewards = ReadField(candidate, "TemplateRewards") as IList;
                    if (rewards == null || rewards.Count != 1) continue;
                    object output = rewards[0];
                    if (output == null) continue;
                    string outputId = ReadString(output, "Id");
                    string outputName = ReadString(output, "ItemName");
                    if (string.IsNullOrEmpty(outputId) || string.IsNullOrEmpty(outputName)) continue;
                    object liveOutput = GameItemRegistryApi.TryGetLiveItem(itemDatabaseInstance, outputId);
                    if (liveOutput == null || !ReferenceEquals(liveOutput, output)) continue;
                    object outputValueObject = ReadField(output, "ItemValue");
                    object stackableObject = ReadField(output, "Stackable");
                    object uniqueObject = ReadField(output, "Unique");
                    object rareObject = ReadField(output, "RareItem");
                    if (!(outputValueObject is int) || (int)outputValueObject < 0 || !(stackableObject is bool) || !(uniqueObject is bool) || !(rareObject is bool)) continue;
                    bool hasOwnedIngredient = false;
                    bool invalidIngredient = ingredients == null;
                    HashSet<string> distinctIngredients = new HashSet<string>(StringComparer.Ordinal);
                    if (ingredients != null)
                    {
                        for (int j = 0; j < ingredients.Count; j++)
                        {
                            string ingredientId = ReadString(ingredients[j], "Id");
                            if (string.IsNullOrEmpty(ingredientId)) { invalidIngredient = true; break; }
                            if (CraftingExpandedItemIds.IsInOwnedRange(ingredientId)) { hasOwnedIngredient = true; break; }
                            distinctIngredients.Add(ingredientId);
                        }
                    }
                    if (hasOwnedIngredient || invalidIngredient) continue;
                    object requiredSlot = ReadField(output, "RequiredSlot");
                    bool eligible = ExperimentalRecipeDonorPolicy.IsEligible(
                        template,
                        GameCraftingApi.IsSpecialCombineTemplate(templateId),
                        distinctIngredients.Count,
                        componentSlots,
                        true,
                        string.Equals(requiredSlot == null ? string.Empty : requiredSlot.ToString(), "General", StringComparison.Ordinal),
                        (bool)stackableObject,
                        (bool)uniqueObject,
                        (bool)rareObject,
                        CraftingExpandedItemIds.IsInOwnedRange(outputId));
                    if (!eligible) continue;

                    ExperimentalRecipeCandidate found = new ExperimentalRecipeCandidate();
                    found.TemplateItem = candidate;
                    found.OutputItem = output;
                    found.TemplateId = templateId;
                    found.TemplateName = templateName;
                    found.OutputId = outputId;
                    found.OutputName = outputName;
                    found.OutputValue = (int)outputValueObject;
                    found.IngredientEntries = ingredients.Count;
                    found.DistinctIngredients = distinctIngredients.Count;
                    result.Add(found);
                }
                result.Sort(CompareCandidates);
            }
            catch { result.Clear(); }
            return result;
        }

        internal static object CloneVerificationTemplate(object donorTemplate, object wildHerb, string newId, string displayName, out string failure)
        {
            failure = string.Empty;
            try
            {
                UnityEngine.Object donorUnity = donorTemplate as UnityEngine.Object;
                if (donorUnity == null || wildHerb == null) { failure = "donor or Wild Herb object unavailable"; return null; }
                IList donorIngredients = ReadField(donorTemplate, "TemplateIngredients") as IList;
                IList donorRewards = ReadField(donorTemplate, "TemplateRewards") as IList;
                if (donorIngredients == null || donorIngredients.Count == 0 || donorRewards == null || donorRewards.Count == 0 || donorRewards[0] == null)
                { failure = "donor recipe shape invalid"; return null; }

                UnityEngine.Object clone = UnityEngine.Object.Instantiate(donorUnity);
                SetField(clone, "Id", newId);
                SetField(clone, "ItemName", displayName);
                SetField(clone, "Lore", "Experimental Crafting Expanded verification template. Uses the donor recipe plus one Wild Herb.");
                SetField(clone, "ItemValue", 0);
                SetField(clone, "Template", true);
                SetField(clone, "FuelSource", false);
                SetField(clone, "PlayerCannotSell", true);
                SetField(clone, "NoTradeNoDestroy", true);
                SetField(clone, "ItemEffectOnClick", null);
                SetField(clone, "TeachSpell", null);
                SetField(clone, "TeachSkill", null);
                SetField(clone, "AssignQuestOnRead", null);
                SetField(clone, "CompleteOnRead", null);
                SetField(clone, "Aura", null);
                SetField(clone, "WornEffect", null);
                SetField(clone, "WeaponProcOnHit", null);
                ClearClasses(clone);

                IList ingredients = NewListLike(clone, "TemplateIngredients");
                IList rewards = NewListLike(clone, "TemplateRewards");
                if (ingredients == null || rewards == null) { UnityEngine.Object.Destroy(clone); failure = "recipe list construction failed"; return null; }
                for (int i = 0; i < donorIngredients.Count; i++) ingredients.Add(donorIngredients[i]);
                ingredients.Add(wildHerb);
                rewards.Add(donorRewards[0]);

                if (!string.Equals(ReadString(clone, "Id"), newId, StringComparison.Ordinal) || !ReadBool(clone, "Template") ||
                    ingredients.Count != donorIngredients.Count + 1 || rewards.Count != 1 ||
                    !string.Equals(ReadString(ingredients[ingredients.Count - 1], "Id"), CraftingExpandedItemIds.WildHerbId, StringComparison.Ordinal) ||
                    !string.Equals(ReadString(rewards[0], "Id"), ReadString(donorRewards[0], "Id"), StringComparison.Ordinal))
                {
                    UnityEngine.Object.Destroy(clone); failure = "configured verification template failed shape re-read"; return null;
                }
                GameItemRegistryApi.MarkOwned(clone, newId);
                return clone;
            }
            catch (Exception ex) { failure = "verification clone failed: " + ex.GetType().Name; return null; }
        }

        internal static bool ValidateOwnedVerificationTemplate(object item, string templateId, string expectedOutputId)
        {
            if (item == null || !GameItemRegistryApi.HasOwnedMarker(item, templateId) || !ReadBool(item, "Template")) return false;
            IList ingredients = ReadField(item, "TemplateIngredients") as IList;
            IList rewards = ReadField(item, "TemplateRewards") as IList;
            if (ingredients == null || rewards == null || rewards.Count != 1 || rewards[0] == null) return false;
            bool herb = false;
            for (int i = 0; i < ingredients.Count; i++)
                if (string.Equals(ReadString(ingredients[i], "Id"), CraftingExpandedItemIds.WildHerbId, StringComparison.Ordinal)) herb = true;
            return herb && (string.IsNullOrEmpty(expectedOutputId) || string.Equals(ReadString(rewards[0], "Id"), expectedOutputId, StringComparison.Ordinal));
        }


        internal static bool MatchesVerificationDonor(object item, ExperimentalRecipeCandidate donor, string templateId)
        {
            if (item == null || donor == null || donor.TemplateItem == null || donor.OutputItem == null) return false;
            if (!ValidateOwnedVerificationTemplate(item, templateId, donor.OutputId)) return false;
            IList actualIngredients = ReadField(item, "TemplateIngredients") as IList;
            IList donorIngredients = ReadField(donor.TemplateItem, "TemplateIngredients") as IList;
            IList rewards = ReadField(item, "TemplateRewards") as IList;
            if (actualIngredients == null || donorIngredients == null || rewards == null || rewards.Count != 1) return false;
            if (actualIngredients.Count != donorIngredients.Count + 1) return false;
            if (!ReferenceEquals(rewards[0], donor.OutputItem)) return false;

            List<string> actualIds = new List<string>();
            List<string> donorIds = new List<string>();
            int herbCount = 0;
            for (int i = 0; i < actualIngredients.Count; i++)
            {
                string id = ReadString(actualIngredients[i], "Id");
                if (string.Equals(id, CraftingExpandedItemIds.WildHerbId, StringComparison.Ordinal)) herbCount++;
                else actualIds.Add(id);
            }
            for (int i = 0; i < donorIngredients.Count; i++) donorIds.Add(ReadString(donorIngredients[i], "Id"));
            if (herbCount != 1 || actualIds.Count != donorIds.Count) return false;
            actualIds.Sort(StringComparer.Ordinal);
            donorIds.Sort(StringComparer.Ordinal);
            for (int i = 0; i < actualIds.Count; i++)
                if (string.IsNullOrEmpty(actualIds[i]) || !string.Equals(actualIds[i], donorIds[i], StringComparison.Ordinal)) return false;
            return true;
        }

        internal static List<ProductionNativeRecipeCandidate> FindProductionCandidates(object itemDatabaseInstance, bool requireForgeCapacity)
        {
            List<ProductionNativeRecipeCandidate> result = new List<ProductionNativeRecipeCandidate>();
            if (itemDatabaseInstance == null) return result;
            try
            {
                Type itemType = FindType("Item");
                UnityEngine.Object[] nativeItems = itemType == null ? null : Resources.LoadAll("Items", itemType);
                int componentSlots = requireForgeCapacity ? ReadComponentSlotCapacity() : int.MaxValue;
                if (nativeItems == null || nativeItems.Length == 0 || componentSlots <= 0) return result;
                for (int i = 0; i < nativeItems.Length; i++)
                {
                    object donor = nativeItems[i];
                    if (donor == null) continue;
                    bool template;
                    bool fuelSource;
                    if (!TryReadBool(donor, "Template", out template) || !template || !TryReadBool(donor, "FuelSource", out fuelSource) || fuelSource) continue;
                    string donorId = ReadString(donor, "Id");
                    string donorName = ReadString(donor, "ItemName");
                    if (string.IsNullOrEmpty(donorId) || string.IsNullOrEmpty(donorName) || CraftingExpandedItemIds.IsInOwnedRange(donorId) || GameCraftingApi.IsSpecialCombineTemplate(donorId)) continue;
                    object liveDonor = GameItemRegistryApi.TryGetLiveItem(itemDatabaseInstance, donorId);
                    if (liveDonor == null || !ReferenceEquals(liveDonor, donor)) continue;

                    IList ingredients = ReadField(donor, "TemplateIngredients") as IList;
                    IList rewards = ReadField(donor, "TemplateRewards") as IList;
                    if (ingredients == null || ingredients.Count == 0 || rewards == null || rewards.Count != 1 || rewards[0] == null) continue;
                    object output = rewards[0];
                    string outputId = ReadString(output, "Id");
                    string outputName = ReadString(output, "ItemName");
                    if (string.IsNullOrEmpty(outputId) || string.IsNullOrEmpty(outputName) || CraftingExpandedItemIds.IsInOwnedRange(outputId)) continue;
                    object liveOutput = GameItemRegistryApi.TryGetLiveItem(itemDatabaseInstance, outputId);
                    if (liveOutput == null || !ReferenceEquals(liveOutput, output)) continue;

                    NativeRecipeOutputFacts facts = ReadOutputFacts(output);
                    ProductionRecipeContentKind kind;
                    if (NativeRecipeContentPolicy.Matches(ProductionRecipeContentKind.ActivatedUtility, facts)) kind = ProductionRecipeContentKind.ActivatedUtility;
                    else if (NativeRecipeContentPolicy.Matches(ProductionRecipeContentKind.Foundation, facts)) kind = ProductionRecipeContentKind.Foundation;
                    else continue;

                    List<string> ingredientIds = new List<string>();
                    HashSet<string> distinct = new HashSet<string>(StringComparer.Ordinal);
                    object extraIngredient = null;
                    string extraIngredientId = string.Empty;
                    int extraValue = int.MaxValue;
                    bool invalid = false;
                    for (int j = 0; j < ingredients.Count; j++)
                    {
                        object ingredient = ingredients[j];
                        string ingredientId = ReadString(ingredient, "Id");
                        if (string.IsNullOrEmpty(ingredientId) || CraftingExpandedItemIds.IsInOwnedRange(ingredientId)) { invalid = true; break; }
                        object liveIngredient = GameItemRegistryApi.TryGetLiveItem(itemDatabaseInstance, ingredientId);
                        if (liveIngredient == null || !ReferenceEquals(liveIngredient, ingredient)) { invalid = true; break; }
                        object ingredientValueObject = ReadField(ingredient, "ItemValue");
                        if (!(ingredientValueObject is int) || (int)ingredientValueObject < 0) { invalid = true; break; }
                        ingredientIds.Add(ingredientId);
                        distinct.Add(ingredientId);
                        int ingredientValue = (int)ingredientValueObject;
                        if (extraIngredient == null || ingredientValue < extraValue || (ingredientValue == extraValue && string.Compare(ingredientId, extraIngredientId, StringComparison.Ordinal) < 0))
                        {
                            extraIngredient = ingredient;
                            extraIngredientId = ingredientId;
                            extraValue = ingredientValue;
                        }
                    }
                    if (invalid || ingredientIds.Count == 0 || extraIngredient == null) continue;
                    if (requireForgeCapacity && !NativeRecipeContentPolicy.FitsForge(distinct.Count, componentSlots, kind)) continue;

                    ProductionNativeRecipeCandidate found = new ProductionNativeRecipeCandidate();
                    found.TemplateItem = donor;
                    found.OutputItem = output;
                    found.ExtraIngredientItem = extraIngredient;
                    found.TemplateId = donorId;
                    found.TemplateName = donorName;
                    found.OutputId = outputId;
                    found.OutputName = outputName;
                    found.OutputValue = facts.ItemValue;
                    found.IngredientIds.AddRange(ingredientIds);
                    found.IngredientFingerprint = BuildIngredientFingerprint(ingredientIds);
                    found.DistinctIngredients = distinct.Count;
                    found.ContentKind = kind;
                    object clickEffect = ReadField(output, "ItemEffectOnClick");
                    found.EffectTypeName = clickEffect == null ? string.Empty : clickEffect.GetType().Name;
                    found.ExtraIngredientId = extraIngredientId;
                    result.Add(found);
                }
                result.Sort(CompareProductionCandidates);
            }
            catch { result.Clear(); }
            return result;
        }

        internal static object TryResolvePackagedTemplateIdentity(object itemDatabaseInstance, string templateId)
        {
            if (itemDatabaseInstance == null || string.IsNullOrEmpty(templateId) || CraftingExpandedItemIds.IsInOwnedRange(templateId)) return null;
            try
            {
                Type itemType = FindType("Item");
                UnityEngine.Object[] nativeItems = itemType == null ? null : Resources.LoadAll("Items", itemType);
                if (nativeItems == null) return null;
                for (int i = 0; i < nativeItems.Length; i++)
                {
                    object item = nativeItems[i];
                    if (item == null || !string.Equals(ReadString(item, "Id"), templateId, StringComparison.Ordinal)) continue;
                    bool template;
                    if (!TryReadBool(item, "Template", out template) || !template) return null;
                    object live = GameItemRegistryApi.TryGetLiveItem(itemDatabaseInstance, templateId);
                    return live != null && ReferenceEquals(live, item) ? item : null;
                }
            }
            catch { }
            return null;
        }

        internal static object CloneProductionTemplateIdentity(ProductionNativeRecipeCandidate donor, ProductionRecipePlanEntry plan,
            string displayName, out string failure)
        {
            failure = string.Empty;
            if (donor == null || plan == null || donor.TemplateItem == null) { failure = "production donor unavailable"; return null; }
            try
            {
                UnityEngine.Object donorUnity = donor.TemplateItem as UnityEngine.Object;
                if (donorUnity == null) { failure = "native donor is not a Unity item asset"; return null; }
                UnityEngine.Object clone = UnityEngine.Object.Instantiate(donorUnity);
                if (!ConfigureOwnedProductionTemplate(clone, donor, plan, null, displayName, false, out failure))
                { UnityEngine.Object.Destroy(clone); return null; }
                GameItemRegistryApi.MarkOwned(clone, plan.TemplateItemId);
                return clone;
            }
            catch (Exception ex) { failure = "production identity clone failed: " + ex.GetType().Name; return null; }
        }

        internal static object CloneProductionTemplate(ProductionNativeRecipeCandidate donor, ProductionRecipePlanEntry plan,
            object wildHerb, string displayName, out string failure)
        {
            failure = string.Empty;
            if (donor == null || plan == null || donor.TemplateItem == null || donor.OutputItem == null)
            { failure = "production donor unavailable"; return null; }
            if (plan.ContentKind == ProductionRecipeContentKind.ActivatedUtility && wildHerb == null)
            { failure = "Wild Herb object unavailable"; return null; }
            try
            {
                UnityEngine.Object donorUnity = donor.TemplateItem as UnityEngine.Object;
                if (donorUnity == null) { failure = "native donor is not a Unity item asset"; return null; }
                UnityEngine.Object clone = UnityEngine.Object.Instantiate(donorUnity);
                if (!ConfigureOwnedProductionTemplate(clone, donor, plan, wildHerb, displayName, true, out failure))
                { UnityEngine.Object.Destroy(clone); return null; }
                GameItemRegistryApi.MarkOwned(clone, plan.TemplateItemId);
                return clone;
            }
            catch (Exception ex) { failure = "production clone failed: " + ex.GetType().Name; return null; }
        }

        internal static bool ConfigureOwnedProductionTemplate(object item, ProductionNativeRecipeCandidate donor, ProductionRecipePlanEntry plan,
            object wildHerb, string displayName, bool active, out string failure)
        {
            failure = string.Empty;
            if (item == null || donor == null || plan == null) { failure = "production template inputs unavailable"; return false; }
            try
            {
                SetField(item, "Id", plan.TemplateItemId);
                SetField(item, "ItemName", RecipeTemplateItemPolicy.FormatTemplateName(displayName));
                SetField(item, "Lore", "Crafting Expanded recipe using a verified native Smithing output and conservative additional ingredients.");
                SetField(item, "ItemValue", 0);
                SetField(item, "FuelSource", false);
                SetField(item, "PlayerCannotSell", true);
                SetField(item, "NoTradeNoDestroy", true);
                SetField(item, "ItemEffectOnClick", null);
                SetField(item, "TeachSpell", null);
                SetField(item, "TeachSkill", null);
                SetField(item, "AssignQuestOnRead", null);
                SetField(item, "CompleteOnRead", null);
                SetField(item, "Aura", null);
                SetField(item, "WornEffect", null);
                SetField(item, "WeaponProcOnHit", null);
                ClearClasses(item);
                IList ingredients = NewListLike(item, "TemplateIngredients");
                IList rewards = NewListLike(item, "TemplateRewards");
                if (ingredients == null || rewards == null) { failure = "production recipe lists unavailable"; return false; }
                if (!active)
                {
                    SetField(item, "Template", false);
                    return true;
                }

                IList donorIngredients = ReadField(donor.TemplateItem, "TemplateIngredients") as IList;
                if (donorIngredients == null || donorIngredients.Count == 0) { failure = "donor ingredients unavailable"; return false; }
                for (int i = 0; i < donorIngredients.Count; i++) ingredients.Add(donorIngredients[i]);
                if (plan.ContentKind == ProductionRecipeContentKind.ActivatedUtility)
                {
                    for (int i = 0; i < plan.WildHerbQuantity; i++) ingredients.Add(wildHerb);
                }
                else if (plan.AddExtraNativeIngredient)
                {
                    if (donor.ExtraIngredientItem == null) { failure = "extra native ingredient unavailable"; return false; }
                    ingredients.Add(donor.ExtraIngredientItem);
                }
                rewards.Add(donor.OutputItem);
                SetField(item, "Template", true);
                if (!MatchesProductionRecipe(item, donor, plan, plan.TemplateItemId))
                { failure = "production recipe failed exact post-configuration validation"; return false; }
                return true;
            }
            catch (Exception ex) { failure = "production configuration failed: " + ex.GetType().Name; return false; }
        }

        internal static bool DeactivateOwnedProductionTemplate(object item, string templateId)
        {
            if (item == null || !GameItemRegistryApi.HasOwnedMarker(item, templateId)) return false;
            try
            {
                SetField(item, "Template", false);
                SetField(item, "FuelSource", false);
                IList ingredients = NewListLike(item, "TemplateIngredients");
                IList rewards = NewListLike(item, "TemplateRewards");
                return ingredients != null && rewards != null && !ReadBool(item, "Template") && ingredients.Count == 0 && rewards.Count == 0;
            }
            catch { return false; }
        }

        internal static bool MatchesProductionRecipe(object item, ProductionNativeRecipeCandidate donor, ProductionRecipePlanEntry plan, string templateId)
        {
            if (item == null || donor == null || plan == null || !GameItemRegistryApi.HasOwnedMarker(item, templateId) || !ReadBool(item, "Template")) return false;
            IList actualIngredients = ReadField(item, "TemplateIngredients") as IList;
            IList rewards = ReadField(item, "TemplateRewards") as IList;
            if (actualIngredients == null || rewards == null || rewards.Count != 1 || !ReferenceEquals(rewards[0], donor.OutputItem)) return false;
            List<string> expected = new List<string>(donor.IngredientIds);
            if (plan.ContentKind == ProductionRecipeContentKind.ActivatedUtility)
            {
                for (int i = 0; i < plan.WildHerbQuantity; i++) expected.Add(CraftingExpandedItemIds.WildHerbId);
            }
            else if (plan.AddExtraNativeIngredient) expected.Add(donor.ExtraIngredientId);
            if (actualIngredients.Count != expected.Count) return false;
            List<string> actual = new List<string>();
            for (int i = 0; i < actualIngredients.Count; i++)
            {
                string id = ReadString(actualIngredients[i], "Id");
                if (string.IsNullOrEmpty(id)) return false;
                actual.Add(id);
            }
            actual.Sort(StringComparer.Ordinal);
            expected.Sort(StringComparer.Ordinal);
            for (int i = 0; i < actual.Count; i++) if (!string.Equals(actual[i], expected[i], StringComparison.Ordinal)) return false;
            return string.Equals(ReadString(rewards[0], "Id"), donor.OutputId, StringComparison.Ordinal);
        }

        internal static int ReadLiveComponentSlotCapacity()
        {
            return ReadComponentSlotCapacity();
        }

        internal static string BuildIngredientFingerprint(IList<string> ingredientIds)
        {
            if (ingredientIds == null || ingredientIds.Count == 0) return string.Empty;
            Dictionary<string, int> counts = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < ingredientIds.Count; i++)
            {
                string id = ingredientIds[i];
                if (string.IsNullOrEmpty(id)) return string.Empty;
                int count; counts.TryGetValue(id, out count); counts[id] = count + 1;
            }
            List<string> ids = new List<string>(counts.Keys); ids.Sort(StringComparer.Ordinal);
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            for (int i = 0; i < ids.Count; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(ids[i]).Append('=').Append(counts[ids[i]]);
            }
            return sb.ToString();
        }

        private static NativeRecipeOutputFacts ReadOutputFacts(object output)
        {
            NativeRecipeOutputFacts facts = new NativeRecipeOutputFacts();
            object requiredSlot = ReadField(output, "RequiredSlot");
            facts.RequiredSlotGeneral = string.Equals(requiredSlot == null ? string.Empty : requiredSlot.ToString(), "General", StringComparison.Ordinal);
            bool stackable = false; bool unique = false; bool rare = false; bool template = false; bool fuel = false; bool disposable = false; bool mustEquip = false;
            bool boolShape = TryReadBool(output, "Stackable", out stackable) && TryReadBool(output, "Unique", out unique) &&
                TryReadBool(output, "RareItem", out rare) && TryReadBool(output, "Template", out template) &&
                TryReadBool(output, "FuelSource", out fuel) && TryReadBool(output, "Disposable", out disposable) &&
                TryReadBool(output, "MustBeEquippedToClick", out mustEquip);
            object itemValue = ReadField(output, "ItemValue");
            bool referenceShape = HasField(output, "RequiredSlot") && HasField(output, "ItemEffectOnClick") && HasField(output, "TeachSpell") &&
                HasField(output, "TeachSkill") && HasField(output, "AssignQuestOnRead") && HasField(output, "CompleteOnRead") &&
                HasField(output, "Aura") && HasField(output, "WornEffect") && HasField(output, "WeaponProcOnHit");
            facts.ShapeKnown = boolShape && referenceShape && itemValue is int;
            facts.Stackable = stackable;
            facts.Unique = unique;
            facts.Rare = rare;
            facts.Template = template;
            facts.FuelSource = fuel;
            facts.ItemValue = itemValue is int ? (int)itemValue : -1;
            facts.Disposable = disposable;
            facts.MustBeEquippedToClick = mustEquip;
            facts.HasClickEffect = IsActiveField(ReadField(output, "ItemEffectOnClick"));
            facts.HasTeachSpell = IsActiveField(ReadField(output, "TeachSpell"));
            facts.HasTeachSkill = IsActiveField(ReadField(output, "TeachSkill"));
            facts.HasQuestReadBehavior = IsActiveField(ReadField(output, "AssignQuestOnRead")) || IsActiveField(ReadField(output, "CompleteOnRead"));
            facts.HasAura = IsActiveField(ReadField(output, "Aura"));
            facts.HasWornEffect = IsActiveField(ReadField(output, "WornEffect"));
            facts.HasWeaponProc = IsActiveField(ReadField(output, "WeaponProcOnHit"));
            facts.OwnedByMod = CraftingExpandedItemIds.IsInOwnedRange(ReadString(output, "Id"));
            return facts;
        }

        private static bool HasField(object instance, string name)
        {
            try { return instance != null && instance.GetType().GetField(name, AllInstance) != null; }
            catch { return false; }
        }

        private static bool TryReadBool(object instance, string name, out bool value)
        {
            value = false;
            object raw = ReadField(instance, name);
            if (!(raw is bool)) return false;
            value = (bool)raw;
            return true;
        }

        private static bool IsActiveField(object value)
        {
            if (value == null) return false;
            if (value is bool) return (bool)value;
            if (value is int) return (int)value != 0;
            string text = value as string;
            if (text != null) return !string.IsNullOrEmpty(text);
            return true;
        }

        private static int CompareProductionCandidates(ProductionNativeRecipeCandidate a, ProductionNativeRecipeCandidate b)
        {
            int byKind = ((int)a.ContentKind).CompareTo((int)b.ContentKind);
            if (byKind != 0) return byKind;
            int byValue = a.OutputValue.CompareTo(b.OutputValue);
            if (byValue != 0) return byValue;
            int byOutput = string.Compare(a.OutputName, b.OutputName, StringComparison.OrdinalIgnoreCase);
            if (byOutput != 0) return byOutput;
            return string.Compare(a.TemplateId, b.TemplateId, StringComparison.Ordinal);
        }

        private static int CompareCandidates(ExperimentalRecipeCandidate a, ExperimentalRecipeCandidate b)
        {
            int value = a.OutputValue.CompareTo(b.OutputValue);
            if (value != 0) return value;
            int byOutput = string.Compare(a.OutputName, b.OutputName, StringComparison.OrdinalIgnoreCase);
            if (byOutput != 0) return byOutput;
            return string.Compare(a.TemplateId, b.TemplateId, StringComparison.Ordinal);
        }

        private static int ReadComponentSlotCapacity()
        {
            try
            {
                object smithing = ReadStaticField("GameData", "Smithing");
                IList components = smithing == null ? null : ReadField(smithing, "Components") as IList;
                return components == null ? 0 : components.Count;
            }
            catch { return 0; }
        }

        private static IList NewListLike(object instance, string fieldName)
        {
            FieldInfo field = instance.GetType().GetField(fieldName, AllInstance);
            if (field == null) return null;
            object value = Activator.CreateInstance(field.FieldType);
            field.SetValue(instance, value);
            return value as IList;
        }

        private static void ClearClasses(object instance)
        {
            try
            {
                FieldInfo field = instance.GetType().GetField("Classes", AllInstance);
                if (field != null) field.SetValue(instance, Activator.CreateInstance(field.FieldType));
            }
            catch { }
        }

        private static object ReadField(object instance, string name)
        {
            try { FieldInfo field = instance == null ? null : instance.GetType().GetField(name, AllInstance); return field == null ? null : field.GetValue(instance); }
            catch { return null; }
        }
        private static void SetField(object instance, string name, object value)
        {
            try { FieldInfo field = instance == null ? null : instance.GetType().GetField(name, AllInstance); if (field != null) field.SetValue(instance, value); }
            catch { }
        }
        private static string ReadString(object instance, string name) { object value = ReadField(instance, name); return value as string ?? string.Empty; }
        private static bool ReadBool(object instance, string name) { object value = ReadField(instance, name); return value is bool && (bool)value; }
        private static int ReadInt(object instance, string name) { object value = ReadField(instance, name); return value is int ? (int)value : 0; }

        private static object ReadStaticField(string typeName, string fieldName)
        {
            Type type = FindType(typeName); if (type == null) return null;
            FieldInfo field = type.GetField(fieldName, AllStatic); return field == null ? null : field.GetValue(null);
        }
        private static Type FindType(string name)
        {
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++) try { Type t = assemblies[i].GetType(name, false); if (t != null) return t; } catch { }
            return null;
        }
    }
}
