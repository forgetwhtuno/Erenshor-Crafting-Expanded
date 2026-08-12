using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace ErenshorCraftingExpanded
{
    // The only place in this mod that touches Smithing/ItemIcon/Item via reflection. Every
    // member accessed here is cited in docs/NATIVE_CRAFTING_FINDINGS.md - nothing is guessed.
    // Reflection (rather than typed access) is used because the findings pass didn't confirm
    // field/method visibility, only existence; this keeps the mod safe to compile and to patch
    // even if a member turns out to be non-public.
    internal static class GameCraftingApi
    {
        private const BindingFlags AllInstance = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        private const BindingFlags AllStatic = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;

        internal static object GetSmithingInstance()
        {
            try
            {
                Type gameData = Type.GetType("GameData, Assembly-CSharp") ?? FindType("GameData");
                if (gameData == null) return null;
                FieldInfo field = gameData.GetField("Smithing", AllStatic);
                return field == null ? null : field.GetValue(null);
            }
            catch { return null; }
        }

        internal static bool IsForgeOpen()
        {
            try
            {
                object smithing = GetSmithingInstance();
                if (smithing == null) return false;
                object window = GetField(smithing, "SmithingWindow");
                if (window == null) return false;
                PropertyInfo activeSelfProperty = window.GetType().GetProperty("activeSelf", AllInstance);
                MethodInfo activeSelf = activeSelfProperty == null ? null : activeSelfProperty.GetGetMethod(true);
                if (activeSelf == null) return false;
                object value = activeSelf.Invoke(window, null);
                return value is bool && (bool)value;
            }
            catch { return false; }
        }

        // Reads the recipe currently loaded into the forge's Template slot, expanding
        // TemplateIngredients' repeated-entry encoding into grouped RequirementLine counts.
        internal static CraftRecipeSnapshot TryGetActiveRecipe()
        {
            try
            {
                object smithing = GetSmithingInstance();
                if (smithing == null) return null;
                object templateIcon = GetField(smithing, "Template");
                object templateItem = templateIcon == null ? null : GetField(templateIcon, "MyItem");
                if (templateItem == null || IsEmptyItem(templateItem)) return null;
                object isTemplate = GetField(templateItem, "Template");
                if (!(isTemplate is bool) || !(bool)isTemplate) return null;

                CraftRecipeSnapshot snapshot = new CraftRecipeSnapshot
                {
                    TemplateItemId = ReadId(templateItem),
                    TemplateItemName = ReadName(templateItem)
                };

                IList ingredients = GetField(templateItem, "TemplateIngredients") as IList;
                Dictionary<string, RequirementLine> grouped = new Dictionary<string, RequirementLine>();
                if (ingredients != null)
                {
                    foreach (object ingredient in ingredients)
                    {
                        if (ingredient == null) continue;
                        string id = ReadId(ingredient);
                        string name = ReadName(ingredient);
                        RequirementLine existing;
                        if (grouped.TryGetValue(id, out existing))
                            grouped[id] = new RequirementLine(id, name, existing.Quantity + 1);
                        else
                            grouped[id] = new RequirementLine(id, name, 1);
                    }
                }
                snapshot.Requirements.AddRange(grouped.Values);

                IList rewards = GetField(templateItem, "TemplateRewards") as IList;
                if (rewards != null && rewards.Count > 0 && rewards[0] != null)
                {
                    snapshot.OutputItemId = ReadId(rewards[0]);
                    snapshot.OutputItemName = ReadName(rewards[0]);
                }
                return snapshot;
            }
            catch { return null; }
        }

        // Sums every ItemIcon in the player's inventory by Item id. Mirrors what Combine()
        // itself is willing to consume from (ordinary player inventory, per the user's v1 scope).
        internal static List<InventoryAvailability> ReadInventoryAvailability()
        {
            List<InventoryAvailability> result = new List<InventoryAvailability>();
            try
            {
                object playerInv = GetStaticField("GameData", "PlayerInv");
                if (playerInv == null) return result;
                IList allSlots = GetField(playerInv, "ALLSLOTS") as IList;
                if (allSlots == null) return result;
                foreach (object icon in allSlots)
                {
                    if (icon == null) continue;
                    object item = GetField(icon, "MyItem");
                    if (item == null || IsEmptyItem(item)) continue;
                    object quantityObj = GetField(icon, "Quantity");
                    int quantity = quantityObj is int ? (int)quantityObj : 1;
                    if (quantity <= 0) continue;
                    result.Add(new InventoryAvailability(ReadId(item), quantity));
                }
            }
            catch { }
            return result;
        }

        // Reads everything physically available to the current generic forge operation: player
        // inventory plus the already-loaded Template/FuelSource/Components slots. Fuel is counted
        // by the real Item.FuelSource flag rather than a hardcoded coal id, so mixed native fuel
        // types remain valid. This powers the UI's full craftable-count estimate.
        internal static List<InventoryAvailability> ReadTotalCraftingAvailability(out int fuelSourceUnits)
        {
            List<InventoryAvailability> result = new List<InventoryAvailability>();
            fuelSourceUnits = 0;
            try
            {
                object playerInv = GetStaticField("GameData", "PlayerInv");
                IList allSlots = playerInv == null ? null : GetField(playerInv, "ALLSLOTS") as IList;
                if (allSlots != null)
                    foreach (object icon in allSlots) AppendIconAvailability(icon, result, ref fuelSourceUnits);

                object smithing = GetSmithingInstance();
                if (smithing != null)
                {
                    AppendIconAvailability(GetField(smithing, "Template"), result, ref fuelSourceUnits);
                    AppendIconAvailability(GetField(smithing, "FuelSource"), result, ref fuelSourceUnits);
                    IList components = GetField(smithing, "Components") as IList;
                    if (components != null)
                        foreach (object icon in components) AppendIconAvailability(icon, result, ref fuelSourceUnits);
                }
            }
            catch { }
            return result;
        }

        private static void AppendIconAvailability(object icon, List<InventoryAvailability> result, ref int fuelSourceUnits)
        {
            if (icon == null || result == null) return;
            object item = GetField(icon, "MyItem");
            if (item == null || IsEmptyItem(item)) return;
            int quantity = ReadQuantity(icon);
            if (quantity <= 0) return;

            string id = ReadId(item);
            if (!string.IsNullOrEmpty(id)) result.Add(new InventoryAvailability(id, quantity));

            object isFuel = GetField(item, "FuelSource");
            if (isFuel is bool && (bool)isFuel)
            {
                long total = (long)fuelSourceUnits + quantity;
                fuelSourceUnits = total > int.MaxValue ? int.MaxValue : (int)total;
            }
        }

        // Finds one inventory ItemIcon holding the given item id and invokes the native
        // ItemIcon.QuickSmith() on it - the same one-unit-at-a-time mechanism the game's own
        // quick-action menu uses (see findings doc section 7). Success is detected from the
        // source slot's before/after state rather than assuming QuickSmith's return type.
        internal static bool QuickSmithOneUnit(string itemId)
        {
            try
            {
                object playerInv = GetStaticField("GameData", "PlayerInv");
                if (playerInv == null) return false;
                IList allSlots = GetField(playerInv, "ALLSLOTS") as IList;
                if (allSlots == null) return false;
                foreach (object icon in allSlots)
                {
                    if (icon == null) continue;
                    object item = GetField(icon, "MyItem");
                    if (item == null || IsEmptyItem(item)) continue;
                    if (!string.Equals(ReadId(item), itemId, StringComparison.Ordinal)) continue;

                    MethodInfo quickSmith = icon.GetType().GetMethod("QuickSmith", AllInstance);
                    if (quickSmith == null) return false;

                    int beforeQuantity = ReadQuantity(icon);
                    object beforeItem = item;
                    quickSmith.Invoke(icon, null);

                    object afterItem = GetField(icon, "MyItem");
                    int afterQuantity = ReadQuantity(icon);
                    if (afterItem == null || IsEmptyItem(afterItem)) return true;
                    if (!ReferenceEquals(beforeItem, afterItem)) return true;
                    return afterQuantity < beforeQuantity;
                }
            }
            catch { }
            return false;
        }

        // Automates repeated QuickSmith calls to fill every missing Components requirement for
        // one craft, using only the native single-unit mechanism. Returns how many units were
        // actually moved. Template/fuel remain native single-unit slots and are not fabricated
        // here; the player can QuickSmith those stacks once without manually splitting them.
        internal static int FillComponentsForOneCraft(CraftRecipeSnapshot recipe)
        {
            if (recipe == null || IsSpecialCombineTemplate(recipe.TemplateItemId)) return 0;
            int moved = 0;
            try
            {
                object smithing = GetSmithingInstance();
                if (smithing == null) return 0;
                // Do not rearrange inventory into a forge that is already invalid for this
                // recipe (unrelated component or too many copies of a required component).
                // Vanilla Combine remains the authority and will explain/reject that setup; QoL
                // only fills an otherwise compatible partial recipe.
                if (!ComponentsAreCompatibleForAutoFill(smithing, recipe)) return 0;
                foreach (RequirementLine line in recipe.Requirements)
                {
                    // QuickSmith routes Item.Template and Item.FuelSource into their dedicated
                    // forge slots. Only automate an ingredient when the live inventory copy is a
                    // normal component item; otherwise leave vanilla/manual handling untouched.
                    if (!CanQuickSmithAsGenericComponent(line.ItemId)) continue;
                    int already = CountInComponents(smithing, line.ItemId);
                    for (int i = already; i < line.Quantity; i++)
                    {
                        if (!QuickSmithOneUnit(line.ItemId)) break;
                        moved++;
                    }
                }
            }
            catch { }
            return moved;
        }


        private static bool ComponentsAreCompatibleForAutoFill(object smithing, CraftRecipeSnapshot recipe)
        {
            if (smithing == null || recipe == null) return false;
            Dictionary<string, int> required = new Dictionary<string, int>();
            foreach (RequirementLine line in recipe.Requirements)
            {
                if (string.IsNullOrEmpty(line.ItemId) || line.Quantity <= 0) continue;
                int existing;
                required.TryGetValue(line.ItemId, out existing);
                long sum = (long)existing + line.Quantity;
                required[line.ItemId] = sum > int.MaxValue ? int.MaxValue : (int)sum;
            }

            IList components = GetField(smithing, "Components") as IList;
            if (components == null) return false;
            Dictionary<string, int> loaded = new Dictionary<string, int>();
            foreach (object icon in components)
            {
                if (icon == null) continue;
                object item = GetField(icon, "MyItem");
                if (item == null || IsEmptyItem(item)) continue;
                string id = ReadId(item);
                int allowed;
                if (string.IsNullOrEmpty(id) || !required.TryGetValue(id, out allowed)) return false;
                int quantity = ReadQuantity(icon);
                if (quantity <= 0) continue;
                int already;
                loaded.TryGetValue(id, out already);
                long total = (long)already + quantity;
                if (total > allowed) return false;
                loaded[id] = (int)total;
            }
            return true;
        }


        private static bool CanQuickSmithAsGenericComponent(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return false;
            try
            {
                object playerInv = GetStaticField("GameData", "PlayerInv");
                IList allSlots = playerInv == null ? null : GetField(playerInv, "ALLSLOTS") as IList;
                if (allSlots == null) return false;
                foreach (object icon in allSlots)
                {
                    if (icon == null) continue;
                    object item = GetField(icon, "MyItem");
                    if (item == null || IsEmptyItem(item) || !string.Equals(ReadId(item), itemId, StringComparison.Ordinal)) continue;
                    object isTemplate = GetField(item, "Template");
                    object isFuel = GetField(item, "FuelSource");
                    return !(isTemplate is bool && (bool)isTemplate) && !(isFuel is bool && (bool)isFuel);
                }
            }
            catch { }
            return false;
        }

        // Current-build special quality/merge templates bypass the generic ingredient matching
        // path. Auto-filling generic Components for them would be an unsafe semantic change.
        internal static bool IsSpecialCombineTemplate(string templateItemId)
        {
            return string.Equals(templateItemId, "31377423", StringComparison.Ordinal) ||
                   string.Equals(templateItemId, "2298018", StringComparison.Ordinal) ||
                   string.Equals(templateItemId, "2265228", StringComparison.Ordinal);
        }

        private static int CountInComponents(object smithing, string itemId)
        {
            IList components = GetField(smithing, "Components") as IList;
            if (components == null) return 0;
            long total = 0;
            foreach (object icon in components)
            {
                if (icon == null) continue;
                object item = GetField(icon, "MyItem");
                if (item == null || IsEmptyItem(item)) continue;
                if (!string.Equals(ReadId(item), itemId, StringComparison.Ordinal)) continue;
                int quantity = ReadQuantity(icon);
                if (quantity > 0) total += quantity;
                if (total >= int.MaxValue) return int.MaxValue;
            }
            return (int)total;
        }

        private static int ReadQuantity(object icon)
        {
            object quantityObj = GetField(icon, "Quantity");
            return quantityObj is int ? (int)quantityObj : 0;
        }

        // Invokes the native Smithing.Combine() - the same authoritative path the in-game
        // Craft button uses. This mod never re-implements validation or consumption itself.
        internal static bool InvokeCombine()
        {
            try
            {
                object smithing = GetSmithingInstance();
                if (smithing == null) return false;
                MethodInfo combine = smithing.GetType().GetMethod("Combine", AllInstance);
                if (combine == null) return false;
                combine.Invoke(smithing, null);
                return true;
            }
            catch { return false; }
        }

        private static bool IsEmptyItem(object item)
        {
            try
            {
                object empty = GetStaticField("GameData", "PlayerInv");
                object emptyItem = empty == null ? null : GetField(empty, "Empty");
                return emptyItem != null && ReferenceEquals(item, emptyItem);
            }
            catch { return false; }
        }

        private static string ReadId(object baseScriptableObject)
        {
            object id = GetField(baseScriptableObject, "Id");
            return id as string ?? string.Empty;
        }

        private static string ReadName(object item)
        {
            object name = GetField(item, "ItemName");
            return name as string ?? string.Empty;
        }

        private static object GetField(object instance, string name)
        {
            if (instance == null) return null;
            FieldInfo field = instance.GetType().GetField(name, AllInstance);
            return field == null ? null : field.GetValue(instance);
        }

        private static object GetStaticField(string typeName, string fieldName)
        {
            Type type = FindType(typeName);
            if (type == null) return null;
            FieldInfo field = type.GetField(fieldName, AllStatic);
            return field == null ? null : field.GetValue(null);
        }

        private static Type FindType(string name)
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
                try { Type type = assembly.GetType(name, false); if (type != null) return type; } catch { }
            return null;
        }
    }
}
