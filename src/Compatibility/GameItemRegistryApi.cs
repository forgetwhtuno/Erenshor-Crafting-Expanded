using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace ErenshorCraftingExpanded
{
    public sealed class CustomItemRegistrationOutcome
    {
        public string DefinitionId;
        public CustomItemRegistrationState State;
        public string ConflictingExistingItemName;
        public string FailureReason;
    }

    // The only place custom-item registration touches ItemDatabase internals via reflection -
    // matches cammaron/Arcanism's proven-in-production approach (Harmony Traverse there, plain
    // cached reflection here), revalidated against this build's actual field layout in
    // docs/NATIVE_ITEM_REGISTRY_FINDINGS.md. Reflection lookups are resolved once and cached,
    // never repeated in Update() (per the plan's architecture instruction).
    internal static class GameItemRegistryApi
    {
        private const BindingFlags AllInstance = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        private const BindingFlags AllStatic = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
        private const string OwnershipNamePrefix = "ErenshorCraftingExpanded::";

        // Ids this mod itself has successfully inserted into the live itemDict this session -
        // the only way "nativeEntryIsOwnedByUs" can be answered truthfully, since a native Item
        // instance carries no "who registered me" marker of its own.
        private static readonly HashSet<string> OwnedIds = new HashSet<string>();
        private static readonly Dictionary<string, object> ResolvedItemsById = new Dictionary<string, object>();

        internal static string LastBaseItemName = string.Empty;
        internal static string LastBaseItemId = string.Empty;

        private static FieldInfo _itemDictField;
        private static FieldInfo _itemDbField;
        private static FieldInfo _itemDbListField;
        private static bool _reflectionResolved;

        internal static bool TryRegisterAll(object itemDatabaseInstance, IEnumerable<CustomItemDefinition> definitions, List<CustomItemRegistrationOutcome> outcomes)
        {
            if (itemDatabaseInstance == null) return false;
            ResolveReflectionOnce(itemDatabaseInstance.GetType());
            if (!_reflectionResolved) return false;

            object baseItem = FindSafeBaseItem(itemDatabaseInstance);
            LastBaseItemName = baseItem != null ? ReadName(baseItem) : "(none found)";
            LastBaseItemId = baseItem != null ? ReadId(baseItem) : string.Empty;
            foreach (CustomItemDefinition definition in definitions)
            {
                CustomItemRegistrationOutcome outcome = new CustomItemRegistrationOutcome { DefinitionId = definition.Id };
                outcomes.Add(outcome);

                CustomItemDefinitionRejectReason validation = CustomItemRegistry.Validate(definition, null);
                bool definitionShapeValid = validation == CustomItemDefinitionRejectReason.None;

                object existingEntry = definitionShapeValid ? TryGetExisting(itemDatabaseInstance, definition.Id) : null;
                bool nativeEntryExists = existingEntry != null;
                bool ownedByUs = nativeEntryExists &&
                    (OwnedIds.Contains(definition.Id) || HasOwnershipMarker(existingEntry, definition.Id));

                CustomItemRegistrationState state = CustomItemRegistrationPolicy.Evaluate(
                    definitionShapeValid && baseItem != null, nativeEntryExists, ownedByUs);
                outcome.State = state;

                if (state == CustomItemRegistrationState.Unavailable)
                {
                    outcome.FailureReason = !definitionShapeValid
                        ? ("Definition invalid: " + validation)
                        : "No safe base item found in live ItemDB to clone from.";
                    continue;
                }
                if (state == CustomItemRegistrationState.Collision)
                {
                    outcome.ConflictingExistingItemName = ReadName(existingEntry);
                    outcome.FailureReason = "Id already occupied by an existing item not owned by this mod.";
                    continue;
                }
                if (ownedByUs)
                {
                    // Idempotent across repeated ItemDatabase.Start/registration attempts in the
                    // same process. The Unity Object.name marker is runtime provenance only; the
                    // user-facing ItemName remains unchanged. This does not claim arbitrary
                    // late hot-load support after ItemDatabase.Start has already finished.
                    OwnedIds.Add(definition.Id);
                    ResolvedItemsById[definition.Id] = existingEntry;
                    continue;
                }

                object clone = CloneAndConfigure(baseItem, definition);
                if (clone == null)
                {
                    outcome.State = CustomItemRegistrationState.Unavailable;
                    outcome.FailureReason = "Clone/configure failed.";
                    continue;
                }

                if (!InsertIntoDatabase(itemDatabaseInstance, clone))
                {
                    try { UnityEngine.Object unityClone = clone as UnityEngine.Object; if (unityClone != null) UnityEngine.Object.Destroy(unityClone); } catch { }
                    outcome.State = CustomItemRegistrationState.Unavailable;
                    outcome.FailureReason = "Native ItemDB/itemDict insertion failed.";
                    continue;
                }

                OwnedIds.Add(definition.Id);
                ResolvedItemsById[definition.Id] = clone;
            }
            return true;
        }

        internal static object TryResolveCustomItem(string id)
        {
            object item;
            return ResolvedItemsById.TryGetValue(id, out item) ? item : null;
        }

        internal static bool IsCustomItemAvailable(string id)
        {
            return OwnedIds.Contains(id);
        }

        internal static bool GrantRegisteredItem(string id, int quantity)
        {
            object item = TryResolveCustomItem(id);
            if (item == null || quantity <= 0) return false;
            try
            {
                object playerInv = GetStaticField("GameData", "PlayerInv");
                if (playerInv == null) return false;
                Type invType = playerInv.GetType();

                MethodInfo addWithQty = FindMethod(invType, "AddItemToInv", item.GetType(), typeof(int));
                bool added = false;
                if (addWithQty != null)
                {
                    object result = addWithQty.Invoke(playerInv, new object[] { item, quantity });
                    added = result is bool && (bool)result;
                }
                if (!added)
                {
                    // The one-item native overload cannot truthfully satisfy a multi-item grant.
                    // Fail closed rather than partially grant N=1 and then let a caller retry the
                    // node/command and accidentally duplicate. Current Wild Herb nodes request 1;
                    // future >1 rewards must prove the native quantity overload in the live build.
                    if (quantity != 1) return false;
                    MethodInfo add = FindMethod(invType, "AddItemToInv", item.GetType());
                    if (add != null)
                    {
                        object result = add.Invoke(playerInv, new object[] { item });
                        added = result is bool && (bool)result;
                    }
                }
                if (added) return true;

                if (quantity != 1) return false;
                MethodInfo force = FindMethod(invType, "ForceItemToInv", item.GetType());
                if (force == null) return false;
                force.Invoke(playerInv, new object[] { item });
                return true;
            }
            catch { return false; }
        }

        private static object FindSafeBaseItem(object itemDatabaseInstance)
        {
            try
            {
                IList itemDb = _itemDbField.GetValue(itemDatabaseInstance) as IList;
                if (itemDb == null) return null;
                foreach (object item in itemDb)
                {
                    if (item == null) continue;
                    if (!IsSafeBaseCandidate(item)) continue;
                    return item;
                }
            }
            catch { }
            return null;
        }

        // Every field checked here is a confirmed real Item field (see
        // NATIVE_CRAFTING_FINDINGS.md's Item member dump) - no guessed field names.
        private static bool IsSafeBaseCandidate(object item)
        {
            Type t = item.GetType();
            return
                GetBool(t, item, "Stackable") == true &&
                IsGeneralSlot(t, item) &&
                GetBool(t, item, "Unique") == false &&
                GetRef(t, item, "TeachSpell") == null &&
                GetRef(t, item, "TeachSkill") == null &&
                GetRef(t, item, "ItemEffectOnClick") == null &&
                GetRef(t, item, "AssignQuestOnRead") == null &&
                GetRef(t, item, "CompleteOnRead") == null &&
                GetBool(t, item, "Disposable") == false &&
                GetBool(t, item, "MustBeEquippedToClick") == false &&
                GetRef(t, item, "WornEffect") == null &&
                GetRef(t, item, "Aura") == null;
        }

        private static bool IsGeneralSlot(Type t, object item)
        {
            try
            {
                FieldInfo f = t.GetField("RequiredSlot", AllInstance);
                object value = f == null ? null : f.GetValue(item);
                return value != null && value.ToString() == "General";
            }
            catch { return false; }
        }

        private static bool? GetBool(Type t, object instance, string name)
        {
            try
            {
                FieldInfo f = t.GetField(name, AllInstance);
                object value = f == null ? null : f.GetValue(instance);
                return value is bool ? (bool)value : (bool?)null;
            }
            catch { return null; }
        }

        private static object GetRef(Type t, object instance, string name)
        {
            try
            {
                FieldInfo f = t.GetField(name, AllInstance);
                return f == null ? null : f.GetValue(instance);
            }
            catch { return null; }
        }

        private static void SetField(object instance, string name, object value)
        {
            try
            {
                FieldInfo f = instance.GetType().GetField(name, AllInstance);
                if (f != null) f.SetValue(instance, value);
            }
            catch { }
        }

        // UnityEngine.Object.Instantiate produces an independent ScriptableObject copy - the
        // source asset is never mutated. See findings doc §5-6 for exactly which fields are
        // overridden vs. intentionally inherited.
        private static object CloneAndConfigure(object baseItem, CustomItemDefinition definition)
        {
            try
            {
                UnityEngine.Object source = baseItem as UnityEngine.Object;
                if (source == null) return null;
                UnityEngine.Object clone = UnityEngine.Object.Instantiate(source);
                clone.name = OwnershipNamePrefix + definition.Id;

                SetField(clone, "Id", definition.Id);
                SetField(clone, "ItemName", definition.Name);
                SetField(clone, "Lore", definition.Lore ?? string.Empty);
                SetField(clone, "ItemValue", definition.Value);
                ClearClassRestrictions(clone); // no class restriction - built with Classes' real generic List<Class> type

                // Deterministic vendor/trade/AH safeguard - reuses real native fields the game
                // already gates those interactions on, rather than inventing new blocking logic.
                // See findings doc's persistence table for why this is a defensive lever, not a
                // proven guarantee (native vendor/AH code wasn't itself disassembled this pass).
                SetField(clone, "PlayerCannotSell", true);
                SetField(clone, "NoTradeNoDestroy", true);

                // Defense in depth: zero out anything that could grant combat/utility value even
                // though the base-item predicate should already exclude items with these set.
                SetField(clone, "WeaponDmg", 0); SetField(clone, "HP", 0); SetField(clone, "AC", 0); SetField(clone, "Mana", 0);
                SetField(clone, "Str", 0); SetField(clone, "End", 0); SetField(clone, "Dex", 0); SetField(clone, "Agi", 0);
                SetField(clone, "Int", 0); SetField(clone, "Wis", 0); SetField(clone, "Cha", 0); SetField(clone, "Res", 0);
                SetField(clone, "WeaponProcOnHit", null); SetField(clone, "ItemEffectOnClick", null);
                SetField(clone, "TeachSpell", null); SetField(clone, "TeachSkill", null);
                SetField(clone, "Aura", null); SetField(clone, "WornEffect", null);
                SetField(clone, "WandEffect", null); SetField(clone, "BowEffect", null);
                SetField(clone, "IsWand", false); SetField(clone, "IsBow", false);
                SetField(clone, "Relic", false); SetField(clone, "RareItem", false);
                SetField(clone, "Unique", false); SetField(clone, "SimPlayersCantGet", false);

                return clone;
            }
            catch { return null; }
        }

        private static void ClearClassRestrictions(object clone)
        {
            try
            {
                FieldInfo f = clone.GetType().GetField("Classes", AllInstance);
                if (f == null) return;
                object listInstance = Activator.CreateInstance(f.FieldType);
                f.SetValue(clone, listInstance);
            }
            catch { }
        }

        private static bool InsertIntoDatabase(object itemDatabaseInstance, object item)
        {
            Array oldArray = null;
            object oldList = null;
            IDictionary dict = null;
            string id = null;
            bool itemDbChanged = false;
            bool dictChanged = false;
            bool listChanged = false;
            try
            {
                oldArray = (Array)_itemDbField.GetValue(itemDatabaseInstance);
                if (oldArray == null) return false;
                dict = (IDictionary)_itemDictField.GetValue(itemDatabaseInstance);
                if (dict == null) return false;

                FieldInfo idField = item.GetType().GetField("Id", AllInstance);
                id = idField == null ? null : idField.GetValue(item) as string;
                if (string.IsNullOrEmpty(id) || dict.Contains(id)) return false;

                Type elementType = oldArray.GetType().GetElementType();
                Array newArray = Array.CreateInstance(elementType, oldArray.Length + 1);
                Array.Copy(oldArray, newArray, oldArray.Length);
                newArray.SetValue(item, oldArray.Length);

                object newList = null;
                if (_itemDbListField != null)
                {
                    oldList = _itemDbListField.GetValue(itemDatabaseInstance);
                    Type listType = _itemDbListField.FieldType;
                    newList = Activator.CreateInstance(listType, (object)newArray);
                }

                // Commit only after every object required for the new state has been built. If
                // any setter/add below throws, the catch block restores the previous structures.
                _itemDbField.SetValue(itemDatabaseInstance, newArray);
                itemDbChanged = true;
                dict.Add(id, item);
                dictChanged = true;
                if (_itemDbListField != null)
                {
                    _itemDbListField.SetValue(itemDatabaseInstance, newList);
                    listChanged = true;
                }
                return true;
            }
            catch
            {
                try { if (listChanged && _itemDbListField != null) _itemDbListField.SetValue(itemDatabaseInstance, oldList); } catch { }
                try { if (dictChanged && dict != null && id != null) dict.Remove(id); } catch { }
                try { if (itemDbChanged && oldArray != null) _itemDbField.SetValue(itemDatabaseInstance, oldArray); } catch { }
                return false;
            }
        }

        private static bool HasOwnershipMarker(object item, string id)
        {
            try
            {
                UnityEngine.Object unityItem = item as UnityEngine.Object;
                return unityItem != null && string.Equals(unityItem.name, OwnershipNamePrefix + id, StringComparison.Ordinal);
            }
            catch { return false; }
        }

        private static object TryGetExisting(object itemDatabaseInstance, string id)
        {
            try
            {
                MethodInfo method = itemDatabaseInstance.GetType().GetMethod("GetItemByID", AllInstance);
                object result = method == null ? null : method.Invoke(itemDatabaseInstance, new object[] { id });
                if (result == null) return null;
                // GetItemByID falls back to Inventory.Empty rather than null on a miss (see
                // findings doc §1) - treat that sentinel the same as "not found".
                object emptyItem = GetEmptyItemSentinel();
                return ReferenceEquals(result, emptyItem) ? null : result;
            }
            catch { return null; }
        }

        private static object GetEmptyItemSentinel()
        {
            try
            {
                object playerInv = GetStaticField("GameData", "PlayerInv");
                return playerInv == null ? null : GetRef(playerInv.GetType(), playerInv, "Empty");
            }
            catch { return null; }
        }

        private static string ReadId(object item)
        {
            try
            {
                FieldInfo f = item.GetType().GetField("Id", AllInstance);
                return f == null ? string.Empty : (f.GetValue(item) as string) ?? string.Empty;
            }
            catch { return string.Empty; }
        }

        private static string ReadName(object item)
        {
            try
            {
                FieldInfo f = item.GetType().GetField("ItemName", AllInstance);
                return f == null ? "(unknown)" : (f.GetValue(item) as string) ?? "(unknown)";
            }
            catch { return "(unknown)"; }
        }

        private static void ResolveReflectionOnce(Type itemDatabaseType)
        {
            if (_reflectionResolved) return;
            try
            {
                _itemDictField = itemDatabaseType.GetField("itemDict", AllInstance);
                _itemDbField = itemDatabaseType.GetField("ItemDB", AllInstance);
                _itemDbListField = itemDatabaseType.GetField("ItemDBList", AllInstance);
                _reflectionResolved = _itemDictField != null && _itemDbField != null;
            }
            catch { _reflectionResolved = false; }
        }

        private static MethodInfo FindMethod(Type declaringType, string name, params Type[] argTypes)
        {
            foreach (MethodInfo candidate in declaringType.GetMethods(AllInstance))
            {
                if (candidate.Name != name) continue;
                ParameterInfo[] parameters = candidate.GetParameters();
                if (parameters.Length != argTypes.Length) continue;
                bool match = true;
                for (int i = 0; i < parameters.Length; i++)
                    if (!parameters[i].ParameterType.IsAssignableFrom(argTypes[i])) { match = false; break; }
                if (match) return candidate;
            }
            return null;
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
