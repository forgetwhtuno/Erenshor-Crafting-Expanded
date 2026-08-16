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
        public string BaseItemName;
        public string BaseItemId;
        public string BaseSelectionReason;
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

        // Runtime lookup cache for item objects this plugin has verified/inserted. Ownership of an
        // existing live native entry is never inferred from this cache; the explicit Unity object
        // marker on that entry is authoritative so an ItemDatabase rebuild cannot turn a foreign
        // same-id item into an assumed-owned object.
        private static readonly HashSet<string> OwnedIds = new HashSet<string>();
        private static readonly Dictionary<string, object> ResolvedItemsById = new Dictionary<string, object>();

        internal static string LastBaseItemName = string.Empty;
        internal static string LastBaseItemId = string.Empty;
        internal static string LastBaseSelectionReason = string.Empty;

        private static FieldInfo _itemDictField;
        private static FieldInfo _itemDbField;
        private static FieldInfo _itemDbListField;
        private static bool _reflectionResolved;

        internal static void ResetSessionBindings()
        {
            // Do not remove anything from native ItemDatabase here. Existing marked entries may be
            // needed to resolve save inventory while the plugin is installed. Only discard stale
            // managed lookup bindings so the next registration pass must revalidate the live DB.
            OwnedIds.Clear();
            ResolvedItemsById.Clear();
            LastBaseItemName = string.Empty;
            LastBaseItemId = string.Empty;
            LastBaseSelectionReason = string.Empty;
        }

        internal static bool TryRegisterAll(object itemDatabaseInstance, IEnumerable<CustomItemDefinition> definitions, List<CustomItemRegistrationOutcome> outcomes)
        {
            if (itemDatabaseInstance == null) return false;
            ResolveReflectionOnce(itemDatabaseInstance.GetType());
            if (!_reflectionResolved) return false;

            // A late Lunaris load can observe GameData.ItemDB after the native singleton field is
            // assigned but before ItemDatabase.Start() has populated its backing collection. Do
            // not consume the one-shot registration attempt against that half-initialized state;
            // the caller will retry after the database contains ordinary native items.
            IList liveItemDb = _itemDbField.GetValue(itemDatabaseInstance) as IList;
            if (liveItemDb == null || liveItemDb.Count == 0) return false;

            foreach (CustomItemDefinition definition in definitions)
            {
                CustomItemRegistrationOutcome outcome = new CustomItemRegistrationOutcome { DefinitionId = definition == null ? string.Empty : definition.Id };
                outcomes.Add(outcome);

                CustomItemDefinitionRejectReason validation = CustomItemRegistry.Validate(definition, null);
                bool definitionShapeValid = validation == CustomItemDefinitionRejectReason.None;
                object existingEntry = definitionShapeValid ? TryGetExisting(itemDatabaseInstance, definition.Id) : null;
                bool nativeEntryExists = existingEntry != null;
                // Treat the explicit runtime marker on the actual live Item as ownership
                // authority. A static id cache alone can outlive an ItemDatabase recreation and
                // must never turn a foreign same-id entry into an assumed-owned object.
                bool ownedByUs = nativeEntryExists && HasOwnershipMarker(existingEntry, definition.Id);

                object baseItem = null;
                string baseReason = string.Empty;
                if (definitionShapeValid && !nativeEntryExists)
                    baseItem = FindSafeBaseItem(itemDatabaseInstance, definition.VisualKind, out baseReason);

                outcome.BaseItemName = baseItem != null ? ReadName(baseItem) : string.Empty;
                outcome.BaseItemId = baseItem != null ? ReadId(baseItem) : string.Empty;
                outcome.BaseSelectionReason = baseReason;

                if (definitionShapeValid && definition.Id == CraftingExpandedItemIds.WildHerbId)
                {
                    LastBaseItemName = baseItem != null ? ReadName(baseItem) : (ownedByUs ? ReadName(existingEntry) : "(none found)");
                    LastBaseItemId = baseItem != null ? ReadId(baseItem) : (ownedByUs ? ReadId(existingEntry) : string.Empty);
                    LastBaseSelectionReason = ownedByUs ? "existing owned Wild Herb registration reused" : baseReason;
                }

                bool canCreate = definitionShapeValid && baseItem != null;
                bool policyDefinitionReady = definitionShapeValid && (nativeEntryExists || canCreate);
                CustomItemRegistrationState state = CustomItemRegistrationPolicy.Evaluate(
                    policyDefinitionReady, nativeEntryExists, ownedByUs);
                outcome.State = state;

                if (state == CustomItemRegistrationState.Unavailable)
                {
                    outcome.FailureReason = !definitionShapeValid
                        ? ("Definition invalid: " + validation)
                        : "No safe native base item found for visual kind " + definition.VisualKind + " (" + baseReason + ").";
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
                    OwnedIds.Add(definition.Id);
                    ResolvedItemsById[definition.Id] = existingEntry;
                    outcome.BaseItemName = ReadName(existingEntry);
                    outcome.BaseItemId = ReadId(existingEntry);
                    outcome.BaseSelectionReason = "existing owned registration reused";
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

        // Historical/current project IL evidence establishes GameData.ItemDB as the live native
        // ItemDatabase back-reference set at the start of ItemDatabase.Start(). Reflection keeps
        // visibility assumptions out of the compile surface and enables safe late-plugin recovery
        // when Lunaris loads this mod after Start() already ran. Read-only.
        internal static object TryGetLiveItemDatabase()
        {
            try { return GetStaticField("GameData", "ItemDB"); }
            catch { return null; }
        }

        internal enum InventoryOnlyGrantResult
        {
            Success = 0,
            ItemUnavailable = 1,
            InventoryUnavailable = 2,
            NativeGrantUnavailable = 3,
            InventoryRejected = 4,
            Failed = 5
        }

        // Recovery grants deliberately never call ForceItemToInv. A replacement template must
        // respect native inventory capacity: if the normal AddItemToInv path rejects it, the
        // permanent recipe knowledge/entitlement remains and the player can retry after making
        // room. The existing generic resource-grant path below retains its historical behavior.
        internal static InventoryOnlyGrantResult GrantRegisteredItemToInventoryOnly(string id)
        {
            object item = TryResolveCustomItem(id);
            if (item == null || !OwnedIds.Contains(id)) return InventoryOnlyGrantResult.ItemUnavailable;
            try
            {
                object playerInv = GetStaticField("GameData", "PlayerInv");
                if (playerInv == null) return InventoryOnlyGrantResult.InventoryUnavailable;
                Type invType = playerInv.GetType();

                MethodInfo addWithQty = FindMethod(invType, "AddItemToInv", item.GetType(), typeof(int));
                if (addWithQty != null)
                {
                    object result = addWithQty.Invoke(playerInv, new object[] { item, 1 });
                    if (!(result is bool)) return InventoryOnlyGrantResult.NativeGrantUnavailable;
                    return (bool)result ? InventoryOnlyGrantResult.Success : InventoryOnlyGrantResult.InventoryRejected;
                }

                MethodInfo add = FindMethod(invType, "AddItemToInv", item.GetType());
                if (add == null) return InventoryOnlyGrantResult.NativeGrantUnavailable;
                object oneResult = add.Invoke(playerInv, new object[] { item });
                if (!(oneResult is bool)) return InventoryOnlyGrantResult.NativeGrantUnavailable;
                return (bool)oneResult ? InventoryOnlyGrantResult.Success : InventoryOnlyGrantResult.InventoryRejected;
            }
            catch { return InventoryOnlyGrantResult.Failed; }
        }

        // Foraging-specific strict grant. Exactly one native AddItemToInv overload is selected
        // before invocation and is invoked at most once. There is deliberately no ForceItemToInv
        // fallback and no second-overload retry after an invocation. If reflection/native code
        // throws after Invoke begins, mutation is ambiguous and the node must fail closed.
        internal static ForagingInventoryGrantResult GrantRegisteredItemForForaging(string id, int quantity, out bool nativeInvokeStarted)
        {
            nativeInvokeStarted = false;
            object item = TryResolveCustomItem(id);
            if (item == null || !OwnedIds.Contains(id) || quantity <= 0) return ForagingInventoryGrantResult.ItemUnavailable;

            object playerInv;
            try { playerInv = GetStaticField("GameData", "PlayerInv"); }
            catch { return ForagingInventoryGrantResult.NativeGrantUnavailable; }
            if (playerInv == null) return ForagingInventoryGrantResult.NativeGrantUnavailable;

            Type invType = playerInv.GetType();
            MethodInfo addWithQty = FindMethod(invType, "AddItemToInv", item.GetType(), typeof(int));
            if (addWithQty != null)
            {
                try
                {
                    nativeInvokeStarted = true;
                    object result = addWithQty.Invoke(playerInv, new object[] { item, quantity });
                    if (!(result is bool)) return ForagingInventoryGrantResult.UnknownAfterInvoke;
                    return (bool)result ? ForagingInventoryGrantResult.Success : ForagingInventoryGrantResult.InventoryRejected;
                }
                catch { return ForagingInventoryGrantResult.UnknownAfterInvoke; }
            }

            if (quantity != 1) return ForagingInventoryGrantResult.NativeGrantUnavailable;
            MethodInfo add = FindMethod(invType, "AddItemToInv", item.GetType());
            if (add == null) return ForagingInventoryGrantResult.NativeGrantUnavailable;
            try
            {
                nativeInvokeStarted = true;
                object result = add.Invoke(playerInv, new object[] { item });
                if (!(result is bool)) return ForagingInventoryGrantResult.UnknownAfterInvoke;
                return (bool)result ? ForagingInventoryGrantResult.Success : ForagingInventoryGrantResult.InventoryRejected;
            }
            catch { return ForagingInventoryGrantResult.UnknownAfterInvoke; }
        }

        // Applies only player-ownership presentation/safety fields to an already registered,
        // explicitly mod-owned recipe-template Item. Recipe mutation/ingredients/rewards remain
        // the native-recipe workstream's responsibility. Unique is intentionally not touched.
        internal static bool TryApplyRecipeTemplateSafety(string id, string recipeDisplayName)
        {
            if (!CraftingExpandedItemIds.IsInRecipeTemplateRange(id)) return false;
            object item = TryResolveCustomItem(id);
            if (item == null || !OwnedIds.Contains(id) || !HasOwnershipMarker(item, id)) return false;
            try
            {
                SetField(item, "ItemName", RecipeTemplateItemPolicy.FormatTemplateName(recipeDisplayName));
                SetField(item, "ItemValue", RecipeTemplateItemPolicy.SafeVendorValue);
                SetField(item, "PlayerCannotSell", RecipeTemplateItemPolicy.PlayerCannotSell);
                SetField(item, "NoTradeNoDestroy", RecipeTemplateItemPolicy.NoTradeNoDestroy);

                object value = GetRef(item.GetType(), item, "ItemValue");
                return ReadName(item) == RecipeTemplateItemPolicy.FormatTemplateName(recipeDisplayName) &&
                    GetBool(item.GetType(), item, "PlayerCannotSell") == true &&
                    GetBool(item.GetType(), item, "NoTradeNoDestroy") == true &&
                    value is int && (int)value == RecipeTemplateItemPolicy.SafeVendorValue;
            }
            catch { return false; }
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

        // Narrow internal primitives shared by the experimental recipe bridge. They reuse the
        // same verified ItemDB/itemDict/ItemDBList transaction as custom materials instead of
        // creating a second database mutation implementation.
        internal static object TryGetLiveItem(object itemDatabaseInstance, string id)
        {
            if (itemDatabaseInstance == null || string.IsNullOrEmpty(id)) return null;
            ResolveReflectionOnce(itemDatabaseInstance.GetType());
            return _reflectionResolved ? TryGetExisting(itemDatabaseInstance, id) : null;
        }

        internal static bool HasOwnedMarker(object item, string id) { return HasOwnershipMarker(item, id); }

        internal static void MarkOwned(object item, string id)
        {
            try
            {
                UnityEngine.Object unityItem = item as UnityEngine.Object;
                if (unityItem != null) unityItem.name = OwnershipNamePrefix + id;
            }
            catch { }
        }

        internal static bool TryInsertOwnedItem(object itemDatabaseInstance, string id, object item, out object liveItem)
        {
            liveItem = null;
            if (itemDatabaseInstance == null || item == null || string.IsNullOrEmpty(id)) return false;
            ResolveReflectionOnce(itemDatabaseInstance.GetType());
            if (!_reflectionResolved || !string.Equals(ReadId(item), id, StringComparison.Ordinal)) return false;

            object existing = TryGetExisting(itemDatabaseInstance, id);
            if (existing != null)
            {
                if (!HasOwnershipMarker(existing, id)) return false;
                OwnedIds.Add(id);
                ResolvedItemsById[id] = existing;
                liveItem = existing;
                return true;
            }

            MarkOwned(item, id);
            if (!HasOwnershipMarker(item, id) || !InsertIntoDatabase(itemDatabaseInstance, item)) return false;
            OwnedIds.Add(id);
            ResolvedItemsById[id] = item;
            liveItem = item;
            return true;
        }

        internal static string ReadLiveId(object item) { return item == null ? string.Empty : ReadId(item); }
        internal static string ReadLiveName(object item) { return item == null ? string.Empty : ReadName(item); }

        // Physical recipe templates need a truthful inventory-full signal. Unlike the historically
        // live-tested forage-material path above, this strict grant never calls ForceItemToInv.
        // Knowledge can therefore remain permanent while a failed physical delivery becomes
        // RestoreAvailable for an explicit later retry.
        internal static bool GrantRegisteredItemStrict(string id, int quantity)
        {
            object item = TryResolveCustomItem(id);
            if (item == null || quantity <= 0) return false;
            try
            {
                object playerInv = GetStaticField("GameData", "PlayerInv");
                if (playerInv == null) return false;
                Type invType = playerInv.GetType();
                MethodInfo addWithQty = FindMethod(invType, "AddItemToInv", item.GetType(), typeof(int));
                if (addWithQty != null)
                {
                    object result = addWithQty.Invoke(playerInv, new object[] { item, quantity });
                    if (result is bool && (bool)result) return true;
                }
                if (quantity != 1) return false;
                MethodInfo add = FindMethod(invType, "AddItemToInv", item.GetType());
                if (add == null) return false;
                object oneResult = add.Invoke(playerInv, new object[] { item });
                return oneResult is bool && (bool)oneResult;
            }
            catch { return false; }
        }

        private static object FindSafeBaseItem(object itemDatabaseInstance, CustomItemVisualKind visualKind, out string selectionReason)
        {
            selectionReason = string.Empty;
            try
            {
                IList itemDb = _itemDbField.GetValue(itemDatabaseInstance) as IList;
                if (itemDb == null)
                {
                    selectionReason = "live ItemDB unavailable";
                    return null;
                }

                object best = null;
                int bestScore = int.MinValue;
                string bestName = string.Empty;
                string bestId = string.Empty;

                foreach (object item in itemDb)
                {
                    if (item == null || !IsSafeBaseCandidate(item)) continue;
                    string name = ReadName(item);
                    int score = OrganicItemBasePolicy.ScoreName(name, visualKind);
                    if (score == int.MinValue) continue;

                    string id = ReadId(item);
                    bool better = score > bestScore;
                    if (!better && score == bestScore)
                    {
                        int byName = string.Compare(name, bestName, StringComparison.OrdinalIgnoreCase);
                        if (byName < 0 || (byName == 0 && string.Compare(id, bestId, StringComparison.Ordinal) < 0))
                            better = true;
                    }
                    if (!better) continue;

                    best = item;
                    bestScore = score;
                    bestName = name;
                    bestId = id;
                }

                if (best == null)
                {
                    selectionReason = "no safe native Item template matched " +
                        OrganicItemBasePolicy.EvidenceDescription(visualKind) +
                        "; unrelated plant/food/geological fallback refused";
                    return null;
                }

                selectionReason = "native visual template selected from live ItemDB kind=" + visualKind + " score=" + bestScore;
                return best;
            }
            catch (Exception ex)
            {
                selectionReason = "base selection failed: " + ex.GetType().Name;
                return null;
            }
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
                GetBool(t, item, "Template") == false &&
                GetBool(t, item, "FuelSource") == false &&
                IsListFieldEmpty(t, item, "TemplateIngredients") &&
                IsListFieldEmpty(t, item, "TemplateRewards") &&
                GetRef(t, item, "ItemIcon") != null &&
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

        private static bool IsListFieldEmpty(Type t, object instance, string name)
        {
            try
            {
                FieldInfo f = t.GetField(name, AllInstance);
                if (f == null) return false;
                object value = f.GetValue(instance);
                if (value == null) return true;
                IList list = value as IList;
                return list != null && list.Count == 0;
            }
            catch { return false; }
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
                SetField(clone, "Template", false); SetField(clone, "FuelSource", false);
                if (!ReplaceWithEmptyList(clone, "TemplateIngredients") ||
                    !ReplaceWithEmptyList(clone, "TemplateRewards") ||
                    ReadId(clone) != definition.Id || ReadName(clone) != definition.Name ||
                    GetBool(clone.GetType(), clone, "Template") != false ||
                    GetBool(clone.GetType(), clone, "FuelSource") != false ||
                    !IsListFieldEmpty(clone.GetType(), clone, "TemplateIngredients") ||
                    !IsListFieldEmpty(clone.GetType(), clone, "TemplateRewards"))
                {
                    try { UnityEngine.Object.Destroy(clone); } catch { }
                    return null;
                }

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

        private static bool ReplaceWithEmptyList(object clone, string fieldName)
        {
            try
            {
                FieldInfo field = clone.GetType().GetField(fieldName, AllInstance);
                if (field == null) return false;
                object empty = Activator.CreateInstance(field.FieldType);
                field.SetValue(clone, empty);
                IList list = field.GetValue(clone) as IList;
                return list != null && list.Count == 0;
            }
            catch { return false; }
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
