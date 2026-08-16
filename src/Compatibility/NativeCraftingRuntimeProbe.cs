using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace ErenshorCraftingExpanded
{
    internal sealed class NativeCraftingRuntimeEvidence
    {
        internal bool SmithingCombine;
        internal bool SmithingDoSuccess;
        internal bool SmithingTemplateSlot;
        internal bool SmithingFuelSlot;
        internal bool SmithingComponents;
        internal bool ItemTemplateFlag;
        internal bool ItemTemplateIngredients;
        internal bool ItemTemplateRewards;
        internal bool ItemIconQuickSmith;
        internal int OrdinaryTemplateCount;
        internal string ExampleTemplate = string.Empty;
        internal string ExampleRecipes = string.Empty;
        internal string CandidateOutputs = string.Empty;
        internal bool ItemDatabaseArray;
        internal bool ItemDictionary;
        internal bool ItemDatabaseList;
        internal bool ShapeSupported;
        internal string Failure = string.Empty;
    }

    // Read-only current-runtime capability probe. This deliberately gathers evidence without
    // inserting a recipe, changing a save, touching forge slots, or claiming that a verified field
    // shape alone proves safe custom-template lifecycle. It is run at ItemDatabase.Start postfix,
    // where the current native database is already populated.
    internal static class NativeCraftingRuntimeProbe
    {
        private const BindingFlags AllInstance = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        private static NativeCraftingRuntimeEvidence _last = new NativeCraftingRuntimeEvidence();

        internal static NativeCraftingRuntimeEvidence Last { get { return _last; } }

        internal static void Probe(object itemDatabaseInstance)
        {
            NativeCraftingRuntimeEvidence result = new NativeCraftingRuntimeEvidence();
            try
            {
                Type smithing = FindType("Smithing");
                Type item = FindType("Item");
                Type itemIcon = FindType("ItemIcon");
                if (smithing == null || item == null || itemIcon == null)
                {
                    result.Failure = "required native type missing";
                    _last = result;
                    return;
                }

                result.SmithingCombine = HasZeroArgMethod(smithing, "Combine");
                result.SmithingDoSuccess = HasZeroArgMethod(smithing, "DoSuccess");
                result.SmithingTemplateSlot = smithing.GetField("Template", AllInstance) != null;
                result.SmithingFuelSlot = smithing.GetField("FuelSource", AllInstance) != null;
                result.SmithingComponents = smithing.GetField("Components", AllInstance) != null;
                result.ItemTemplateFlag = IsFieldType(item, "Template", typeof(bool));
                result.ItemTemplateIngredients = IsEnumerableField(item, "TemplateIngredients");
                result.ItemTemplateRewards = IsEnumerableField(item, "TemplateRewards");
                result.ItemIconQuickSmith = HasZeroArgMethod(itemIcon, "QuickSmith");

                if (itemDatabaseInstance != null)
                {
                    Type dbType = itemDatabaseInstance.GetType();
                    FieldInfo itemDbField = dbType.GetField("ItemDB", AllInstance);
                    FieldInfo itemDictField = dbType.GetField("itemDict", AllInstance);
                    FieldInfo itemDbListField = dbType.GetField("ItemDBList", AllInstance);
                    result.ItemDatabaseArray = itemDbField != null && itemDbField.FieldType.IsArray;
                    result.ItemDictionary = itemDictField != null && typeof(IDictionary).IsAssignableFrom(itemDictField.FieldType);
                    result.ItemDatabaseList = itemDbListField != null && typeof(IEnumerable).IsAssignableFrom(itemDbListField.FieldType);

                    IEnumerable itemDb = itemDbField == null ? null : itemDbField.GetValue(itemDatabaseInstance) as IEnumerable;
                    if (itemDb != null)
                    {
                        StringBuilder examples = new StringBuilder(512);
                        StringBuilder outputs = new StringBuilder(768);
                        HashSet<string> outputIds = new HashSet<string>();
                        foreach (object candidate in itemDb)
                        {
                            if (!IsOrdinaryTemplate(candidate)) continue;
                            result.OrdinaryTemplateCount++;
                            if (string.IsNullOrEmpty(result.ExampleTemplate))
                            {
                                string id = ReadString(candidate, "Id");
                                string name = ReadString(candidate, "ItemName");
                                result.ExampleTemplate = (string.IsNullOrEmpty(name) ? "(unnamed)" : name) + "#" + id;
                            }
                            if (result.OrdinaryTemplateCount <= 3)
                            {
                                if (examples.Length > 0) examples.Append(" || ");
                                examples.Append(ReadRecipeSummary(candidate));
                            }

                            IList rewards = ReadField(candidate, "TemplateRewards") as IList;
                            object reward = rewards != null && rewards.Count > 0 ? rewards[0] : null;
                            string outputId = ReadString(reward, "Id");
                            if (reward != null && outputIds.Count < 12 && !string.IsNullOrEmpty(outputId) && outputIds.Add(outputId))
                            {
                                if (outputs.Length > 0) outputs.Append(" || ");
                                outputs.Append(ReadOutputSummary(reward));
                            }
                        }
                        result.ExampleRecipes = examples.ToString();
                        result.CandidateOutputs = outputs.ToString();
                    }
                }

                result.ShapeSupported = NativeCraftingEvidencePolicy.IsRecipeShapeSupported(
                    result.SmithingCombine,
                    result.SmithingDoSuccess,
                    result.SmithingTemplateSlot,
                    result.SmithingFuelSlot,
                    result.SmithingComponents,
                    result.ItemTemplateFlag,
                    result.ItemTemplateIngredients,
                    result.ItemTemplateRewards,
                    result.ItemIconQuickSmith,
                    result.OrdinaryTemplateCount);
                if (!result.ShapeSupported) result.Failure = "current runtime recipe shape probe incomplete";
            }
            catch (Exception ex)
            {
                result.Failure = "probe failed: " + ex.GetType().Name;
            }
            _last = result;
        }

        internal static string Describe()
        {
            NativeCraftingRuntimeEvidence e = _last;
            StringBuilder sb = new StringBuilder(256);
            sb.Append("shape=").Append(e.ShapeSupported ? "supported" : "not-proven");
            sb.Append(" templates=").Append(e.OrdinaryTemplateCount);
            sb.Append(" combine=").Append(e.SmithingCombine ? "yes" : "no");
            sb.Append(" success=").Append(e.SmithingDoSuccess ? "yes" : "no");
            sb.Append(" slots=").Append(e.SmithingTemplateSlot && e.SmithingFuelSlot && e.SmithingComponents ? "yes" : "no");
            sb.Append(" itemRecipeFields=").Append(e.ItemTemplateFlag && e.ItemTemplateIngredients && e.ItemTemplateRewards ? "yes" : "no");
            sb.Append(" quickSmith=").Append(e.ItemIconQuickSmith ? "yes" : "no");
            sb.Append(" itemDb=").Append(e.ItemDatabaseArray && e.ItemDictionary ? "yes" : "no");
            sb.Append(" itemDbList=").Append(e.ItemDatabaseList ? "yes" : "no");
            if (!string.IsNullOrEmpty(e.ExampleTemplate)) sb.Append(" example=").Append(e.ExampleTemplate);
            if (!string.IsNullOrEmpty(e.Failure)) sb.Append(" reason=").Append(e.Failure);
            return sb.ToString();
        }

        internal static string DescribeExamples()
        {
            string value = _last.ExampleRecipes ?? string.Empty;
            if (value.Length > 640) value = value.Substring(0, 640) + "...";
            return string.IsNullOrEmpty(value) ? "(none captured)" : value;
        }

        internal static string DescribeOutputs()
        {
            string value = _last.CandidateOutputs ?? string.Empty;
            if (value.Length > 1024) value = value.Substring(0, 1024) + "...";
            return string.IsNullOrEmpty(value) ? "(none captured)" : value;
        }

        private static string ReadOutputSummary(object reward)
        {
            if (reward == null) return "(null)";
            string name = ReadString(reward, "ItemName");
            string id = ReadString(reward, "Id");
            object slot = ReadField(reward, "RequiredSlot");
            object stackable = ReadField(reward, "Stackable");
            object click = ReadField(reward, "ItemEffectOnClick");
            return (string.IsNullOrEmpty(name) ? "(unnamed)" : name) + "#" + id +
                " slot=" + (slot == null ? "?" : slot.ToString()) +
                " stack=" + (stackable is bool ? ((bool)stackable ? "yes" : "no") : "?") +
                " click=" + (click == null ? "none" : click.GetType().Name);
        }

        private static string ReadRecipeSummary(object candidate)
        {
            if (candidate == null) return "(null)";
            StringBuilder sb = new StringBuilder(160);
            string name = ReadString(candidate, "ItemName");
            string id = ReadString(candidate, "Id");
            sb.Append(string.IsNullOrEmpty(name) ? "(unnamed)" : name).Append('#').Append(id).Append(": ");

            IList ingredients = ReadField(candidate, "TemplateIngredients") as IList;
            if (ingredients == null || ingredients.Count == 0) sb.Append("(no ingredients)");
            else
            {
                for (int i = 0; i < ingredients.Count && i < 8; i++)
                {
                    if (i > 0) sb.Append('+');
                    object ingredient = ingredients[i];
                    string ingredientName = ReadString(ingredient, "ItemName");
                    string ingredientId = ReadString(ingredient, "Id");
                    sb.Append(string.IsNullOrEmpty(ingredientName) ? "?" : ingredientName).Append('#').Append(ingredientId);
                }
                if (ingredients.Count > 8) sb.Append("+...");
            }

            IList rewards = ReadField(candidate, "TemplateRewards") as IList;
            sb.Append(" -> ");
            if (rewards == null || rewards.Count == 0 || rewards[0] == null) sb.Append("(no reward)");
            else
            {
                string rewardName = ReadString(rewards[0], "ItemName");
                string rewardId = ReadString(rewards[0], "Id");
                sb.Append(string.IsNullOrEmpty(rewardName) ? "?" : rewardName).Append('#').Append(rewardId);
            }
            return sb.ToString();
        }

        private static bool IsOrdinaryTemplate(object candidate)
        {
            if (candidate == null) return false;
            object template = ReadField(candidate, "Template");
            if (!(template is bool) || !(bool)template) return false;
            string id = ReadString(candidate, "Id");
            if (GameCraftingApi.IsSpecialCombineTemplate(id)) return false;
            IList ingredients = ReadField(candidate, "TemplateIngredients") as IList;
            IList rewards = ReadField(candidate, "TemplateRewards") as IList;
            return ingredients != null && ingredients.Count > 0 && rewards != null && rewards.Count > 0 && rewards[0] != null;
        }

        private static bool IsFieldType(Type type, string name, Type expected)
        {
            FieldInfo field = type.GetField(name, AllInstance);
            return field != null && field.FieldType == expected;
        }

        private static bool IsEnumerableField(Type type, string name)
        {
            FieldInfo field = type.GetField(name, AllInstance);
            return field != null && typeof(IEnumerable).IsAssignableFrom(field.FieldType);
        }

        private static bool HasZeroArgMethod(Type type, string name)
        {
            MethodInfo method = type.GetMethod(name, AllInstance, null, Type.EmptyTypes, null);
            return method != null;
        }

        private static object ReadField(object instance, string name)
        {
            try
            {
                FieldInfo field = instance.GetType().GetField(name, AllInstance);
                return field == null ? null : field.GetValue(instance);
            }
            catch { return null; }
        }

        private static string ReadString(object instance, string name)
        {
            object value = ReadField(instance, name);
            return value as string ?? string.Empty;
        }

        private static Type FindType(string name)
        {
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                try
                {
                    Type type = assemblies[i].GetType(name, false);
                    if (type != null) return type;
                }
                catch { }
            }
            return null;
        }
    }
}
