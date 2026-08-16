using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace ErenshorCraftingExpanded
{
    // Production native-template coordinator. Stable mod recipe ids are fixed by
    // ProductionRecipePlan, while each slot binds once to a packaged native donor/output proven
    // by the current live ItemDB. The binding is persisted so a later game update can fail closed
    // instead of silently remapping a saved Template id to a different output.
    internal static class ProductionNativeRecipeRegistry
    {
        private const int MaxAutomaticAttempts = 10;
        private static readonly HashSet<string> ActiveTemplateIds = new HashSet<string>(StringComparer.Ordinal);
        private static readonly HashSet<string> IdentityTemplateIds = new HashSet<string>(StringComparer.Ordinal);
        private static ProductionRecipeBindingDocument _bindings = new ProductionRecipeBindingDocument();
        private static string _bindingPath = string.Empty;
        private static object _lastItemDatabase;
        private static bool _initialized;
        private static float _nextAttemptAt;
        private static int _attemptCount;
        private static string _lastFailure = string.Empty;
        private static string _lastCatalogSummary = string.Empty;

        internal static int ActiveCount { get { return ActiveTemplateIds.Count; } }
        internal static int BoundCount { get { return _bindings == null ? 0 : _bindings.Bindings.Count; } }
        internal static int AttemptCount { get { return _attemptCount; } }
        internal static bool CanAutoRetry { get { return _attemptCount < MaxAutomaticAttempts; } }
        internal static string LastFailure { get { return _lastFailure; } }

        internal static void BeginSession()
        {
            ActiveTemplateIds.Clear();
            IdentityTemplateIds.Clear();
            _bindings = new ProductionRecipeBindingDocument();
            _bindingPath = string.Empty;
            _lastItemDatabase = null;
            _initialized = false;
            _nextAttemptAt = 0f;
            _attemptCount = 0;
            _lastFailure = string.Empty;
            _lastCatalogSummary = string.Empty;
            CraftingRecipeCatalog.ResetRuntimeProduction();
        }

        internal static void Initialize(string pluginDataDir)
        {
            _bindingPath = Path.Combine(pluginDataDir ?? string.Empty, "native-recipe-bindings.v1.txt");
            _bindings = ProductionRecipeBindingStore.Load(_bindingPath);
            _initialized = true;
            if (!string.IsNullOrEmpty(ProductionRecipeBindingStore.LastError)) _lastFailure = ProductionRecipeBindingStore.LastError;
        }

        // Item identities must exist before a player's saved physical Templates are resolved. This
        // path therefore recreates persisted recipe ids as INERT owned Items at ItemDatabase.Start
        // (or late-load recovery) without authorizing Smithing. Gameplay activation happens later,
        // after the live forge and current recipe shape are proven.
        internal static bool TryRegisterSavedIdentities(object itemDatabaseInstance)
        {
            if (!_initialized || itemDatabaseInstance == null) return false;
            ObserveDatabase(itemDatabaseInstance);
            if (_bindings.LoadState == ProductionRecipeBindingLoadState.Malformed || _bindings.LoadState == ProductionRecipeBindingLoadState.UnsupportedVersion)
            {
                _lastFailure = string.IsNullOrEmpty(ProductionRecipeBindingStore.LastError) ? "native recipe bindings unavailable" : ProductionRecipeBindingStore.LastError;
                return false;
            }
            if (_bindings.Bindings.Count == 0) return true;

            List<ProductionNativeRecipeCandidate> candidates = GameNativeRecipeRegistryApi.FindProductionCandidates(itemDatabaseInstance, false);
            bool all = true;
            for (int i = 0; i < _bindings.Bindings.Count; i++)
            {
                ProductionRecipeBinding binding = _bindings.Bindings[i];
                ProductionRecipePlanEntry plan = binding == null ? null : ProductionRecipePlan.Get(binding.RecipeKey);
                if (plan == null)
                {
                    all = false;
                    _lastFailure = "saved recipe binding references an unknown stable recipe: " + (binding == null ? "(unknown)" : binding.RecipeKey);
                    continue;
                }
                ProductionNativeRecipeCandidate candidate = FindExactCandidate(binding, candidates);
                if (candidate == null)
                {
                    // Save resolution and craft authorization are separate. If an update changes an
                    // output's effect/value classification, preserve the stable physical Template
                    // identity as inert when the exact packaged donor still exists. Do not expose a
                    // recipe definition or allow Combine until the full binding proves again.
                    object donorIdentity = GameNativeRecipeRegistryApi.TryResolvePackagedTemplateIdentity(itemDatabaseInstance, binding.DonorTemplateId);
                    if (donorIdentity == null)
                    {
                        all = false;
                        _lastFailure = "saved recipe donor identity is unavailable for " + binding.RecipeKey;
                        continue;
                    }
                    ProductionNativeRecipeCandidate inertDonor = new ProductionNativeRecipeCandidate();
                    inertDonor.TemplateItem = donorIdentity;
                    inertDonor.TemplateId = binding.DonorTemplateId;
                    inertDonor.TemplateName = plan.DisplayPrefix;
                    if (!EnsureIdentity(itemDatabaseInstance, plan, inertDonor, plan.DisplayPrefix))
                    {
                        all = false;
                        if (string.IsNullOrEmpty(_lastFailure)) _lastFailure = "saved recipe identity could not be restored inert for " + plan.RecipeKey;
                        continue;
                    }
                    all = false;
                    _lastFailure = "saved recipe identity restored inert; current native content proof is blocked for " + binding.RecipeKey;
                    continue;
                }
                CustomRecipeDefinition definition = BuildDefinition(plan, candidate);
                if (definition == null || !EnsureIdentity(itemDatabaseInstance, plan, candidate, definition.DisplayName))
                {
                    all = false;
                    if (string.IsNullOrEmpty(_lastFailure)) _lastFailure = "saved recipe identity could not be restored for " + plan.RecipeKey;
                    continue;
                }
                AddDefinition(definition);
            }
            return all;
        }

        internal static void Tick(bool gameplayEnabled)
        {
            if (!_initialized) return;
            if (!gameplayEnabled)
            {
                // Reset the bounded activation budget even if ItemDB is temporarily unavailable
                // during a scene/load transition. If a live database exists, neutralize active
                // Templates immediately; otherwise the next disabled tick with a database will do it.
                _attemptCount = ProductionRecipeRetryPolicy.ResetAfterGameplayDisable(_attemptCount);
                _nextAttemptAt = 0f;
                object disabledDb = GameItemRegistryApi.TryGetLiveItemDatabase();
                if (disabledDb != null)
                {
                    // Use the remembered stable Template ids against the currently live database
                    // before observing a database identity change clears the runtime-active set.
                    DeactivateActiveTemplates(disabledDb);
                    ObserveDatabase(disabledDb);
                }
                return;
            }

            object db = GameItemRegistryApi.TryGetLiveItemDatabase();
            if (db == null) return;
            ObserveDatabase(db);
            if (Time.unscaledTime < _nextAttemptAt || !CanAutoRetry) return;
            _nextAttemptAt = Time.unscaledTime + 2f;

            int componentSlots = GameNativeRecipeRegistryApi.ReadLiveComponentSlotCapacity();
            bool actionable = ProductionRecipeRetryPolicy.IsActionable(NativeCraftingRuntimeProbe.Last.ShapeSupported, componentSlots);
            if (!actionable)
            {
                // The old code burned all ten activation attempts during the first ~20 seconds of
                // a session if the player had not opened a forge yet. Waiting for native evidence
                // is not a failure and must not consume the bounded retry budget.
                _lastFailure = "waiting for a live Smithing forge before production recipe activation";
                return;
            }

            _attemptCount = ProductionRecipeRetryPolicy.ConsumeAttempt(_attemptCount, MaxAutomaticAttempts, true);
            TryActivateAndBind(db);
        }

        internal static bool IsRegisteredCurrentSession(string templateItemId)
        {
            return !string.IsNullOrEmpty(templateItemId) && ActiveTemplateIds.Contains(templateItemId);
        }

        internal static void Shutdown()
        {
            try
            {
                object db = GameItemRegistryApi.TryGetLiveItemDatabase();
                if (db != null) DeactivateActiveTemplates(db);
            }
            catch { }
            ActiveTemplateIds.Clear();
            IdentityTemplateIds.Clear();
            _lastItemDatabase = null;
            _initialized = false;
        }

        internal static string Describe()
        {
            string state = _bindings == null ? "uninitialized" : _bindings.LoadState.ToString();
            string text = "bindings=" + state + " bound=" + BoundCount + "/" + ProductionRecipePlan.All.Count +
                " active=" + ActiveCount + " attempts=" + _attemptCount +
                (string.IsNullOrEmpty(ProductionRecipeBindingStore.LastRecovery) ? string.Empty : " recoveredFrom=" + ProductionRecipeBindingStore.LastRecovery);
            if (!string.IsNullOrEmpty(_lastCatalogSummary)) text += " catalog={" + _lastCatalogSummary + "}";
            if (!string.IsNullOrEmpty(_lastFailure)) text += " reason={" + _lastFailure + "}";
            return text;
        }

        internal static string DescribeCandidates()
        {
            object db = GameItemRegistryApi.TryGetLiveItemDatabase();
            int componentSlots = GameNativeRecipeRegistryApi.ReadLiveComponentSlotCapacity();
            if (db == null) return "ItemDB unavailable";
            if (componentSlots <= 0) return "open a live Smithing forge to preview exact production bindings";

            List<ProductionNativeRecipeCandidate> candidates = GameNativeRecipeRegistryApi.FindProductionCandidates(db, true);
            if (candidates.Count == 0) return "no production-safe candidates fit the current forge";
            List<ProductionRecipeCandidateDescriptor> descriptors = BuildDescriptors(candidates);
            HashSet<string> usedDonors = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> usedOutputs = new HashSet<string>(StringComparer.Ordinal);
            if (_bindings != null)
            {
                for (int i = 0; i < _bindings.Bindings.Count; i++)
                {
                    ProductionRecipeBinding existingBinding = _bindings.Bindings[i];
                    if (existingBinding == null) continue;
                    usedDonors.Add(existingBinding.DonorTemplateId);
                    usedOutputs.Add(existingBinding.OutputItemId);
                }
            }
            System.Text.StringBuilder sb = new System.Text.StringBuilder(900);
            IList<ProductionRecipePlanEntry> plans = ProductionRecipePlan.All;
            for (int i = 0; i < plans.Count; i++)
            {
                ProductionRecipePlanEntry plan = plans[i];
                ProductionRecipeBinding saved = _bindings == null ? null : _bindings.Get(plan.RecipeKey);
                ProductionNativeRecipeCandidate candidate = saved == null
                    ? null
                    : FindExactCandidate(saved, candidates);
                if (candidate == null && saved == null)
                {
                    int selected = ProductionRecipeSelectionPolicy.SelectIndex(descriptors, plan.ContentKind, plan.TierOrdinal, usedDonors, usedOutputs);
                    if (selected >= 0 && selected < candidates.Count) candidate = candidates[selected];
                }
                if (i > 0) sb.Append(" || ");
                sb.Append(plan.RecipeKey).Append(" Lv").Append(plan.MinimumCraftingLevel).Append(" => ");
                if (saved != null && candidate == null)
                {
                    sb.Append("BLOCKED saved binding no longer proves");
                    usedDonors.Add(saved.DonorTemplateId);
                    usedOutputs.Add(saved.OutputItemId);
                    continue;
                }
                if (candidate == null)
                {
                    sb.Append("no safe candidate");
                    continue;
                }
                usedDonors.Add(candidate.TemplateId);
                usedOutputs.Add(candidate.OutputId);
                sb.Append(candidate.TemplateName).Append('#').Append(candidate.TemplateId)
                    .Append(" -> ").Append(candidate.OutputName).Append('#').Append(candidate.OutputId)
                    .Append(" value=").Append(candidate.OutputValue)
                    .Append(" ingredients=").Append(candidate.IngredientFingerprint);
                if (!string.IsNullOrEmpty(candidate.EffectTypeName)) sb.Append(" effect=").Append(candidate.EffectTypeName);
                if (saved != null) sb.Append(" [BOUND]");
            }
            return sb.ToString();
        }

        private static void TryActivateAndBind(object db)
        {
            _lastFailure = string.Empty;
            if (!NativeCraftingRuntimeProbe.Last.ShapeSupported)
            { _lastFailure = "current runtime recipe shape not proven"; return; }
            if (_bindings.LoadState == ProductionRecipeBindingLoadState.Malformed || _bindings.LoadState == ProductionRecipeBindingLoadState.UnsupportedVersion)
            { _lastFailure = string.IsNullOrEmpty(ProductionRecipeBindingStore.LastError) ? "native recipe binding file is unsafe" : ProductionRecipeBindingStore.LastError; return; }

            List<ProductionNativeRecipeCandidate> candidates = GameNativeRecipeRegistryApi.FindProductionCandidates(db, true);
            if (candidates.Count == 0) { _lastFailure = "no conservative packaged native recipe donors available while a live forge shape is present"; return; }
            object herb = GameItemRegistryApi.TryResolveCustomItem(CraftingExpandedItemIds.WildHerbId);
            int componentSlots = GameNativeRecipeRegistryApi.ReadLiveComponentSlotCapacity();

            HashSet<string> usedDonors = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> usedOutputs = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < _bindings.Bindings.Count; i++)
            {
                ProductionRecipeBinding existingBinding = _bindings.Bindings[i];
                if (existingBinding == null) continue;
                usedDonors.Add(existingBinding.DonorTemplateId);
                usedOutputs.Add(existingBinding.OutputItemId);
            }

            // Existing stable bindings are always re-proven first. No remapping is allowed.
            for (int i = 0; i < _bindings.Bindings.Count; i++)
            {
                ProductionRecipeBinding binding = _bindings.Bindings[i];
                ProductionRecipePlanEntry plan = ProductionRecipePlan.Get(binding.RecipeKey);
                ProductionNativeRecipeCandidate candidate = FindExactCandidate(binding, candidates);
                if (plan == null || candidate == null)
                {
                    _lastFailure = "saved production binding no longer matches current packaged native data: " + (binding == null ? "(unknown)" : binding.RecipeKey);
                    continue;
                }
                if (!NativeRecipeContentPolicy.FitsForge(candidate.DistinctIngredients, componentSlots, plan.ContentKind))
                { _lastFailure = "live forge component capacity cannot fit " + plan.RecipeKey; continue; }
                if (plan.WildHerbQuantity > 0 && herb == null)
                { _lastFailure = "Wild Herb is unavailable for " + plan.RecipeKey; continue; }
                CustomRecipeDefinition definition = BuildDefinition(plan, candidate);
                if (definition == null || !Activate(db, plan, candidate, herb, definition.DisplayName)) continue;
                AddDefinition(definition);
            }

            // First-run binding: each stable slot selects a distinct conservative native donor.
            List<ProductionRecipeCandidateDescriptor> descriptors = BuildDescriptors(candidates);
            IList<ProductionRecipePlanEntry> plans = ProductionRecipePlan.All;
            for (int i = 0; i < plans.Count; i++)
            {
                ProductionRecipePlanEntry plan = plans[i];
                if (_bindings.Get(plan.RecipeKey) != null) continue;
                int selected = ProductionRecipeSelectionPolicy.SelectIndex(descriptors, plan.ContentKind, plan.TierOrdinal, usedDonors, usedOutputs);
                if (selected < 0 || selected >= candidates.Count) continue;
                ProductionNativeRecipeCandidate candidate = candidates[selected];
                if (plan.WildHerbQuantity > 0 && herb == null) continue;
                CustomRecipeDefinition definition = BuildDefinition(plan, candidate);
                if (definition == null) continue;
                if (!Activate(db, plan, candidate, herb, definition.DisplayName)) continue;

                ProductionRecipeBinding binding = CreateBinding(plan, candidate);
                _bindings.Bindings.Add(binding);
                if (!ProductionRecipeBindingStore.Save(_bindingPath, _bindings))
                {
                    _bindings.Bindings.Remove(binding);
                    DeactivateOne(db, plan.TemplateItemId);
                    _lastFailure = ProductionRecipeBindingStore.LastError;
                    continue;
                }
                usedDonors.Add(candidate.TemplateId);
                usedOutputs.Add(candidate.OutputId);
                AddDefinition(definition);
            }

            _lastCatalogSummary = BuildCatalogSummary();
            if (ActiveTemplateIds.Count == ProductionRecipePlan.All.Count) _attemptCount = MaxAutomaticAttempts;
        }

        private static bool EnsureIdentity(object db, ProductionRecipePlanEntry plan, ProductionNativeRecipeCandidate candidate, string displayName)
        {
            object existing = GameItemRegistryApi.TryGetLiveItem(db, plan.TemplateItemId);
            if (existing != null)
            {
                if (!GameItemRegistryApi.HasOwnedMarker(existing, plan.TemplateItemId))
                { _lastFailure = "recipe template id collision: " + plan.TemplateItemId; return false; }
                if (!GameNativeRecipeRegistryApi.DeactivateOwnedProductionTemplate(existing, plan.TemplateItemId))
                { _lastFailure = "owned recipe identity could not be neutralized: " + plan.TemplateItemId; return false; }
                object liveExisting;
                if (!GameItemRegistryApi.TryInsertOwnedItem(db, plan.TemplateItemId, existing, out liveExisting))
                { _lastFailure = "owned recipe identity could not be rebound: " + plan.TemplateItemId; return false; }
                GameItemRegistryApi.TryApplyRecipeTemplateSafety(plan.TemplateItemId, displayName);
                IdentityTemplateIds.Add(plan.TemplateItemId);
                ActiveTemplateIds.Remove(plan.TemplateItemId);
                return true;
            }

            string failure;
            object clone = GameNativeRecipeRegistryApi.CloneProductionTemplateIdentity(candidate, plan, displayName, out failure);
            if (clone == null) { _lastFailure = failure; return false; }
            object live;
            if (!GameItemRegistryApi.TryInsertOwnedItem(db, plan.TemplateItemId, clone, out live))
            {
                DestroyIfClone(clone);
                _lastFailure = "recipe identity insertion rejected or collided: " + plan.TemplateItemId;
                return false;
            }
            if (live != clone) DestroyIfClone(clone);
            if (!GameNativeRecipeRegistryApi.DeactivateOwnedProductionTemplate(live, plan.TemplateItemId))
            { _lastFailure = "inserted recipe identity could not be neutralized: " + plan.TemplateItemId; return false; }
            GameItemRegistryApi.TryApplyRecipeTemplateSafety(plan.TemplateItemId, displayName);
            IdentityTemplateIds.Add(plan.TemplateItemId);
            ActiveTemplateIds.Remove(plan.TemplateItemId);
            return true;
        }

        private static bool Activate(object db, ProductionRecipePlanEntry plan, ProductionNativeRecipeCandidate candidate, object herb, string displayName)
        {
            object existing = GameItemRegistryApi.TryGetLiveItem(db, plan.TemplateItemId);
            if (existing != null)
            {
                if (!GameItemRegistryApi.HasOwnedMarker(existing, plan.TemplateItemId))
                { _lastFailure = "recipe template id collision: " + plan.TemplateItemId; return false; }
                string configureFailure;
                if (!GameNativeRecipeRegistryApi.ConfigureOwnedProductionTemplate(existing, candidate, plan, herb, displayName, true, out configureFailure))
                { _lastFailure = configureFailure; return false; }
                object liveExisting;
                if (!GameItemRegistryApi.TryInsertOwnedItem(db, plan.TemplateItemId, existing, out liveExisting))
                { _lastFailure = "owned production recipe could not be rebound: " + plan.RecipeKey; return false; }
                if (liveExisting != null) existing = liveExisting;
                if (!GameNativeRecipeRegistryApi.MatchesProductionRecipe(existing, candidate, plan, plan.TemplateItemId))
                { _lastFailure = "owned recipe failed exact activation proof: " + plan.RecipeKey; return false; }
            }
            else
            {
                string failure;
                object clone = GameNativeRecipeRegistryApi.CloneProductionTemplate(candidate, plan, herb, displayName, out failure);
                if (clone == null) { _lastFailure = failure; return false; }
                object live;
                if (!GameItemRegistryApi.TryInsertOwnedItem(db, plan.TemplateItemId, clone, out live))
                {
                    DestroyIfClone(clone);
                    _lastFailure = "production recipe insertion rejected or collided: " + plan.TemplateItemId;
                    return false;
                }
                if (live != clone) DestroyIfClone(clone);
                existing = live;
                if (!GameNativeRecipeRegistryApi.MatchesProductionRecipe(existing, candidate, plan, plan.TemplateItemId))
                { _lastFailure = "inserted production recipe failed exact donor proof: " + plan.RecipeKey; return false; }
            }
            if (!GameItemRegistryApi.TryApplyRecipeTemplateSafety(plan.TemplateItemId, displayName))
            { _lastFailure = "recipe template safety could not be applied: " + plan.RecipeKey; return false; }
            IdentityTemplateIds.Add(plan.TemplateItemId);
            ActiveTemplateIds.Add(plan.TemplateItemId);
            return true;
        }

        private static void DeactivateActiveTemplates(object db)
        {
            if (ActiveTemplateIds.Count == 0) return;
            List<string> ids = new List<string>(ActiveTemplateIds);
            for (int i = 0; i < ids.Count; i++) DeactivateOne(db, ids[i]);
            ActiveTemplateIds.Clear();
        }

        private static void DeactivateOne(object db, string templateId)
        {
            object item = GameItemRegistryApi.TryGetLiveItem(db, templateId);
            if (item != null && GameItemRegistryApi.HasOwnedMarker(item, templateId))
                GameNativeRecipeRegistryApi.DeactivateOwnedProductionTemplate(item, templateId);
            ActiveTemplateIds.Remove(templateId);
        }

        private static ProductionNativeRecipeCandidate FindExactCandidate(ProductionRecipeBinding binding, IList<ProductionNativeRecipeCandidate> candidates)
        {
            if (binding == null || candidates == null) return null;
            for (int i = 0; i < candidates.Count; i++)
            {
                ProductionNativeRecipeCandidate c = candidates[i];
                if (c != null && c.ContentKind == binding.ContentKind &&
                    string.Equals(c.TemplateId, binding.DonorTemplateId, StringComparison.Ordinal) &&
                    string.Equals(c.OutputId, binding.OutputItemId, StringComparison.Ordinal) &&
                    c.OutputValue == binding.OutputItemValue &&
                    string.Equals(c.EffectTypeName, binding.OutputEffectType, StringComparison.Ordinal) &&
                    string.Equals(c.IngredientFingerprint, binding.DonorIngredientFingerprint, StringComparison.Ordinal)) return c;
            }
            return null;
        }

        private static CustomRecipeDefinition BuildDefinition(ProductionRecipePlanEntry plan, ProductionNativeRecipeCandidate candidate)
        {
            return ProductionRecipeDefinitionFactory.Create(plan, candidate.OutputId, candidate.OutputName, candidate.IngredientIds, candidate.ExtraIngredientId);
        }

        private static ProductionRecipeBinding CreateBinding(ProductionRecipePlanEntry plan, ProductionNativeRecipeCandidate candidate)
        {
            ProductionRecipeBinding binding = new ProductionRecipeBinding();
            binding.RecipeKey = plan.RecipeKey;
            binding.TemplateItemId = plan.TemplateItemId;
            binding.ContentKind = plan.ContentKind;
            binding.DonorTemplateId = candidate.TemplateId;
            binding.OutputItemId = candidate.OutputId;
            binding.OutputItemValue = candidate.OutputValue;
            binding.OutputEffectType = candidate.EffectTypeName;
            binding.DonorIngredientFingerprint = candidate.IngredientFingerprint;
            return binding;
        }

        private static List<ProductionRecipeCandidateDescriptor> BuildDescriptors(IList<ProductionNativeRecipeCandidate> candidates)
        {
            List<ProductionRecipeCandidateDescriptor> result = new List<ProductionRecipeCandidateDescriptor>();
            for (int i = 0; i < candidates.Count; i++)
            {
                ProductionNativeRecipeCandidate c = candidates[i];
                ProductionRecipeCandidateDescriptor d = new ProductionRecipeCandidateDescriptor();
                d.DonorTemplateId = c.TemplateId; d.OutputItemId = c.OutputId; d.OutputValue = c.OutputValue; d.ContentKind = c.ContentKind;
                result.Add(d);
            }
            return result;
        }

        private static void AddDefinition(CustomRecipeDefinition definition)
        {
            if (definition == null) return;
            CustomRecipeRejectReason result = CraftingRecipeCatalog.TryAddRuntimeProduction(definition);
            if (result != CustomRecipeRejectReason.None && result != CustomRecipeRejectReason.DuplicateRecipeKey && result != CustomRecipeRejectReason.DuplicateTemplateItemId)
                _lastFailure = "production definition rejected: " + definition.RecipeKey + " / " + result;
        }

        private static string BuildCatalogSummary()
        {
            IList<CustomRecipeDefinition> recipes = CraftingRecipeCatalog.Production.All;
            if (recipes.Count == 0) return "none";
            System.Text.StringBuilder sb = new System.Text.StringBuilder(400);
            for (int i = 0; i < recipes.Count; i++)
            {
                if (i > 0) sb.Append(" | ");
                CustomRecipeDefinition r = recipes[i];
                sb.Append(r.DisplayName).Append(" [").Append(r.OutputItemId).Append("] Lv").Append(r.MinimumCraftingLevel);
            }
            return sb.ToString();
        }

        private static void ObserveDatabase(object db)
        {
            if (ReferenceEquals(_lastItemDatabase, db)) return;
            _lastItemDatabase = db;
            ActiveTemplateIds.Clear();
            IdentityTemplateIds.Clear();
            _attemptCount = 0;
            _nextAttemptAt = 0f;
        }

        private static void DestroyIfClone(object value)
        {
            try
            {
                UnityEngine.Object unity = value as UnityEngine.Object;
                if (unity != null) UnityEngine.Object.Destroy(unity);
            }
            catch { }
        }
    }
}
