using System;
using System.Collections.Generic;
using System.IO;

namespace ErenshorCraftingExpanded
{
    public enum RecipeOwnershipActionKind
    {
        Success = 0,
        NoChange = 1,
        UnknownRecipe = 2,
        UnsafeRestore = 3,
        TemplateUnavailable = 4,
        InventoryRejected = 5,
        PersistenceUnavailable = 6,
        Failed = 7
    }

    public sealed class RecipeOwnershipActionResult
    {
        public RecipeOwnershipActionKind Kind;
        public bool Success;
        public bool KnowledgeKnown;
        public bool TemplateGranted;
        public string Message = string.Empty;
    }

    internal static class RecipeOwnershipController
    {
        private static readonly RecipeOwnershipCatalog Catalog = new RecipeOwnershipCatalog();
        private static KnownRecipeDocument _document = new KnownRecipeDocument();
        private static KnownRecipeLedger _unboundSessionLedger = new KnownRecipeLedger();
        private static KnownRecipeLedger _activeLedger = _unboundSessionLedger;
        private static string _currentCharacterHash = string.Empty;
        private static string _savePath = string.Empty;
        private static string _lastMessage = string.Empty;
        private static string _lastError = string.Empty;
        private static long _nextBindingProbeUtcTicks;
        private static long _nextSafetyRefreshUtcTicks;
        private static bool _initialized;
        private static bool _lateBindingDeferred;

        internal static int RegisteredRecipeCount { get { return Catalog.Count; } }
        internal static bool PersistenceAvailable { get { return !string.IsNullOrEmpty(_currentCharacterHash) && IsDocumentWritable(); } }
        internal static string LastMessage { get { return _lastMessage; } }
        internal static string LastError { get { return _lastError; } }
        internal static bool IsKnown(string stableRecipeId) { return _activeLedger != null && _activeLedger.IsKnown(stableRecipeId); }

        internal static void Initialize(string pluginDataDir)
        {
            // Hot reload can preserve static state in some loader configurations. Definitions are
            // session-owned and are re-registered incrementally from the current runtime catalog;
            // permanent knowledge lives in the ledger below, not in this catalog.
            Catalog.Clear();
            _savePath = Path.Combine(pluginDataDir ?? string.Empty, "known-recipes.v1.txt");
            _document = KnownRecipeStore.Load(_savePath);
            _lastError = KnownRecipeStore.LastError;
            _unboundSessionLedger = new KnownRecipeLedger();
            _activeLedger = _unboundSessionLedger;
            _currentCharacterHash = string.Empty;
            _lastMessage = string.Empty;
            _nextBindingProbeUtcTicks = 0;
            _nextSafetyRefreshUtcTicks = 0;
            _lateBindingDeferred = false;
            _initialized = true;
            EnsureCharacterBinding(true);
        }

        internal static void Tick()
        {
            if (!_initialized) return;
            long now = DateTime.UtcNow.Ticks;
            if (now >= _nextBindingProbeUtcTicks)
            {
                _nextBindingProbeUtcTicks = now + TimeSpan.FromSeconds(1).Ticks;
                EnsureCharacterBinding(false);
            }
            if (now >= _nextSafetyRefreshUtcTicks)
            {
                _nextSafetyRefreshUtcTicks = now + TimeSpan.FromSeconds(2).Ticks;
                ApplyTemplateSafetyForRegisteredRecipes();
            }
        }

        internal static RecipeOwnershipDefinitionRejectReason RegisterDefinition(RecipeOwnershipDefinition definition)
        {
            RecipeOwnershipDefinitionRejectReason result = Catalog.Register(definition);
            if (result == RecipeOwnershipDefinitionRejectReason.None && definition != null)
                GameItemRegistryApi.TryApplyRecipeTemplateSafety(definition.TemplateItemId, definition.DisplayName);
            return result;
        }

        internal static RecipeOwnershipActionResult LearnNewRecipe(string stableRecipeId)
        {
            RecipeOwnershipDefinition definition = Catalog.GetByRecipeId(stableRecipeId);
            if (definition == null) return Result(RecipeOwnershipActionKind.UnknownRecipe, false, "Recipe definition is unavailable.");
            EnsureCharacterBinding(true);
            long now = DateTime.UtcNow.Ticks;
            bool learned = _activeLedger.LearnNew(stableRecipeId, now);
            if (!learned) return Result(RecipeOwnershipActionKind.NoChange, true, "Recipe already known.");

            // Defensive merge-workstream reconciliation: the ownership layer should normally be
            // the sole physical grant authority, but if a proven existing copy is already in an
            // authoritative storage location, treat that copy as satisfying the initial unlock
            // entitlement instead of leaving a deferred second copy behind.
            RecipeTemplateStorageSnapshot existing = RecipeTemplateStorageApi.Probe(definition.TemplateItemId);
            if (RecipeTemplateStoragePolicy.IsKnownPresent(existing.Location))
            {
                _activeLedger.ConsumeTemplateEntitlement(stableRecipeId);
                PersistIfPossible();
                _lastMessage = "Recipe learned. Existing physical template recognized.";
                RecipeOwnershipActionResult alreadyPresent = Result(RecipeOwnershipActionKind.Success, true, _lastMessage);
                alreadyPresent.KnowledgeKnown = true;
                return alreadyPresent;
            }

            PersistIfPossible();
            RecipeOwnershipActionResult grant = RestoreInternal(definition, true, now);
            grant.KnowledgeKnown = true;
            if (!grant.Success)
            {
                // Earning knowledge is the irreversible progression event. A full/unavailable
                // inventory defers only the physical tool grant and must never roll knowledge back
                // or invite the unlock caller to award the recipe again.
                grant.Success = true;
                if (grant.Kind == RecipeOwnershipActionKind.InventoryRejected)
                    grant.Message = "Inventory could not accept the template. Recipe remains known; restore it later.";
                else if (grant.Kind == RecipeOwnershipActionKind.UnsafeRestore)
                    grant.Message = "Recipe learned. " + grant.Message;
                else
                    grant.Message = "Recipe learned. Physical template can be restored when its native item is available.";
                _lastMessage = grant.Message;
            }
            return grant;
        }

        internal static RecipeOwnershipActionResult ImportKnownRecipe(string stableRecipeId, long learnedUtcTicks)
        {
            RecipeOwnershipDefinition definition = Catalog.GetByRecipeId(stableRecipeId);
            if (definition == null) return Result(RecipeOwnershipActionKind.UnknownRecipe, false, "Recipe definition is unavailable.");
            EnsureCharacterBinding(true);
            bool added = _activeLedger.ImportKnown(stableRecipeId, learnedUtcTicks);
            if (!added) return Result(RecipeOwnershipActionKind.NoChange, true, "Recipe already known.");
            PersistIfPossible();
            _lastMessage = "Recipe knowledge imported. Physical template was not duplicated automatically.";
            RecipeOwnershipActionResult imported = Result(RecipeOwnershipActionKind.Success, true, _lastMessage);
            imported.KnowledgeKnown = true;
            return imported;
        }

        internal static void OnVerifiedCraftSuccess(string templateItemId)
        {
            RecipeOwnershipDefinition definition = Catalog.GetByTemplateId(templateItemId);
            if (definition == null || definition.Deprecated) return;
            EnsureCharacterBinding(true);
            long now = DateTime.UtcNow.Ticks;
            _activeLedger.MarkVerifiedTemplateConsumed(definition.StableRecipeId, now);
            PersistIfPossible();
            RecipeOwnershipActionResult result = RestoreInternal(definition, true, now);
            if (!result.Success && result.Kind == RecipeOwnershipActionKind.InventoryRejected)
                _lastMessage = "Craft succeeded, but inventory could not accept the replacement template. Recipe remains known; restore it later.";
        }

        internal static RecipeOwnershipActionResult Restore(string stableRecipeId)
        {
            RecipeOwnershipDefinition definition = Catalog.GetByRecipeId(stableRecipeId);
            if (definition == null) return Result(RecipeOwnershipActionKind.UnknownRecipe, false, "Recipe definition is unavailable.");
            EnsureCharacterBinding(true);
            return RestoreInternal(definition, false, DateTime.UtcNow.Ticks);
        }

        internal static RecipeOwnershipActionResult RestoreAllSafe()
        {
            EnsureCharacterBinding(true);
            List<RecipeOwnershipDefinition> definitions = Catalog.Snapshot();
            int restored = 0;
            for (int i = 0; i < definitions.Count; i++)
            {
                RecipeOwnershipDefinition definition = definitions[i];
                if (definition == null || definition.Deprecated || !_activeLedger.IsKnown(definition.StableRecipeId)) continue;
                RecipeRestoreDecision decision = RecipeTemplateRecoveryPolicy.Evaluate(
                    _activeLedger.Get(definition.StableRecipeId), RecipeTemplateStorageApi.Probe(definition.TemplateItemId), DateTime.UtcNow.Ticks);
                if (!decision.CanRestore) continue;
                RecipeOwnershipActionResult result = RestoreInternal(definition, false, DateTime.UtcNow.Ticks);
                if (result.Success) restored++;
                else if (result.Kind == RecipeOwnershipActionKind.InventoryRejected || result.Kind == RecipeOwnershipActionKind.TemplateUnavailable || result.Kind == RecipeOwnershipActionKind.Failed)
                    return Result(result.Kind, false, restored > 0 ? ("Restored " + restored.ToString() + " template(s). " + result.Message) : result.Message);
            }
            string message = restored > 0 ? "Restored " + restored.ToString() + " missing template(s)." : "No safely restorable templates found.";
            _lastMessage = message;
            return Result(restored > 0 ? RecipeOwnershipActionKind.Success : RecipeOwnershipActionKind.NoChange, true, message);
        }

        internal static RecipeBookSnapshot BuildBookSnapshot(int craftingLevel)
        {
            EnsureCharacterBinding(false);
            RecipeBookSnapshot book = new RecipeBookSnapshot();
            book.TotalCount = Catalog.Count;
            book.CharacterPersistenceAvailable = PersistenceAvailable;
            book.PersistenceStatus = DescribePersistenceStatus();
            book.LastPlayerMessage = _lastMessage;
            List<RecipeOwnershipDefinition> definitions = Catalog.Snapshot();
            for (int i = 0; i < definitions.Count; i++)
            {
                RecipeOwnershipDefinition definition = definitions[i];
                if (definition == null) continue;
                bool known = _activeLedger.IsKnown(definition.StableRecipeId);
                if (known)
                {
                    KnownRecipeRecord record = _activeLedger.Get(definition.StableRecipeId);
                    RecipeTemplateStorageSnapshot storage = RecipeTemplateStorageApi.Probe(definition.TemplateItemId);
                    RecipeRestoreDecision decision = RecipeTemplateRecoveryPolicy.Evaluate(record, storage, DateTime.UtcNow.Ticks);
                    RecipeBookRowModel row = new RecipeBookRowModel
                    {
                        StableRecipeId = definition.StableRecipeId,
                        DisplayName = definition.DisplayName,
                        TemplateItemId = definition.TemplateItemId,
                        KnowledgeState = RecipeKnowledgeState.Known,
                        TemplateLocation = storage.Location,
                        StatusText = definition.Deprecated ? "Recipe deprecated" : RecipeBookViewPolicy.BuildTemplateStatus(storage.Location, record == null ? 0 : record.PendingTemplateEntitlements),
                        CanRestore = !definition.Deprecated && decision.CanRestore,
                        HasReplacementEntitlement = record != null && record.PendingTemplateEntitlements > 0,
                        Deprecated = definition.Deprecated
                    };
                    book.Known.Add(row);
                    book.KnownCount++;
                }
                else if (!definition.Deprecated)
                {
                    book.Locked.Add(new RecipeBookRowModel
                    {
                        StableRecipeId = definition.StableRecipeId,
                        DisplayName = definition.DisplayName,
                        TemplateItemId = definition.TemplateItemId,
                        KnowledgeState = RecipeKnowledgeState.Locked,
                        LockReason = RecipeBookViewPolicy.BuildLockedReason(definition, craftingLevel)
                    });
                }
            }
            return book;
        }

        internal static string DescribePersistenceStatus()
        {
            if (!_initialized) return "recipe knowledge not initialized";
            if (_document != null && _document.LoadState == KnownRecipeDocumentLoadState.UnsupportedVersion)
                return "known-recipe file is newer than this mod; read/write disabled for safety";
            if (!string.IsNullOrEmpty(_currentCharacterHash))
                return "per-character recipe knowledge active" +
                    (string.IsNullOrEmpty(KnownRecipeStore.LastRecovery) ? string.Empty : " recoveredFrom=" + KnownRecipeStore.LastRecovery);
            if (_lateBindingDeferred) return "session-only recipe knowledge retained; late character binding deferred for isolation";
            return "session-only recipe knowledge; stable character identity not verified";
        }

        internal static string DescribeBankStatus()
        {
            return RecipeOwnershipIntegration.DescribeBankCapability();
        }

        internal static string DescribeAbsenceAuthorityStatus()
        {
            return RecipeOwnershipIntegration.DescribeAbsenceAuthorityCapability();
        }

        internal static KnownRecipeRecord GetKnownRecordForDiagnostics(string stableRecipeId)
        {
            return _activeLedger == null ? null : _activeLedger.Get(stableRecipeId);
        }

        internal static void Persist()
        {
            PersistIfPossible();
        }

        internal static void SceneTransition()
        {
            // Actor/inventory objects are re-read on every storage probe. Only identity binding is
            // invalidated so the next stable frame cannot accidentally continue writing under a
            // character key that belonged to the previous scene/session.
            PersistIfPossible();
            _currentCharacterHash = string.Empty;
            _activeLedger = _unboundSessionLedger;
            _nextBindingProbeUtcTicks = 0;
        }

        internal static void Shutdown()
        {
            PersistIfPossible();
            _currentCharacterHash = string.Empty;
            _activeLedger = _unboundSessionLedger;
            _lateBindingDeferred = false;
            _initialized = false;
            RecipeOwnershipIntegration.ResetForPluginUnload();
        }

        private static RecipeOwnershipActionResult RestoreInternal(RecipeOwnershipDefinition definition, bool automatic, long nowUtcTicks)
        {
            if (definition == null || definition.Deprecated) return Result(RecipeOwnershipActionKind.UnknownRecipe, false, "Recipe cannot be restored.");
            KnownRecipeRecord record = _activeLedger.Get(definition.StableRecipeId);
            RecipeTemplateStorageSnapshot storage = RecipeTemplateStorageApi.Probe(definition.TemplateItemId);
            RecipeRestoreDecision decision = RecipeTemplateRecoveryPolicy.Evaluate(record, storage, nowUtcTicks);
            if (!decision.CanRestore)
            {
                _lastMessage = decision.PlayerReason;
                return Result(RecipeOwnershipActionKind.UnsafeRestore, false, decision.PlayerReason);
            }

            if (!GameItemRegistryApi.TryApplyRecipeTemplateSafety(definition.TemplateItemId, definition.DisplayName))
            {
                _lastMessage = "Recipe remains known, but its physical template is unavailable right now.";
                return Result(RecipeOwnershipActionKind.TemplateUnavailable, false, _lastMessage);
            }

            GameItemRegistryApi.InventoryOnlyGrantResult grant = GameItemRegistryApi.GrantRegisteredItemToInventoryOnly(definition.TemplateItemId);
            if (grant != GameItemRegistryApi.InventoryOnlyGrantResult.Success)
            {
                if (grant == GameItemRegistryApi.InventoryOnlyGrantResult.InventoryRejected)
                    _lastMessage = "Inventory could not accept the template. Recipe remains known.";
                else if (grant == GameItemRegistryApi.InventoryOnlyGrantResult.ItemUnavailable || grant == GameItemRegistryApi.InventoryOnlyGrantResult.NativeGrantUnavailable)
                    _lastMessage = "Recipe remains known, but template restore is unavailable right now.";
                else
                    _lastMessage = "Recipe remains known; template restore failed safely.";
                return Result(grant == GameItemRegistryApi.InventoryOnlyGrantResult.InventoryRejected ? RecipeOwnershipActionKind.InventoryRejected : RecipeOwnershipActionKind.Failed, false, _lastMessage);
            }

            if (!RecipeTemplateRecoveryPolicy.ApplySuccessfulGrant(record, decision, nowUtcTicks))
            {
                _lastMessage = "Template grant completed but ownership transaction could not be finalized; restore is disabled for safety this session.";
                return Result(RecipeOwnershipActionKind.Failed, false, _lastMessage);
            }
            PersistIfPossible();
            _lastMessage = automatic
                ? "Recipe known. Physical template placed in inventory."
                : "Restored " + RecipeTemplateItemPolicy.FormatTemplateName(definition.DisplayName) + ".";
            RecipeOwnershipActionResult success = Result(RecipeOwnershipActionKind.Success, true, _lastMessage);
            success.KnowledgeKnown = true;
            success.TemplateGranted = true;
            return success;
        }

        private static void EnsureCharacterBinding(bool immediate)
        {
            if (!_initialized) return;
            if (_document != null && _document.LoadState == KnownRecipeDocumentLoadState.UnsupportedVersion)
            {
                _currentCharacterHash = string.Empty;
                _activeLedger = _unboundSessionLedger;
                return;
            }

            string stableIdentity;
            if (!RecipeOwnershipIntegration.TryGetCharacterIdentity(out stableIdentity))
            {
                if (!string.IsNullOrEmpty(_currentCharacterHash)) PersistIfPossible();
                _currentCharacterHash = string.Empty;
                _activeLedger = _unboundSessionLedger;
                return;
            }
            string hash = RecipeCharacterIdentityKey.HashStableIdentity(stableIdentity);
            if (string.IsNullOrEmpty(hash))
            {
                _currentCharacterHash = string.Empty;
                _activeLedger = _unboundSessionLedger;
                return;
            }
            if (string.Equals(hash, _currentCharacterHash, StringComparison.Ordinal) && _activeLedger != null) return;

            if (!string.IsNullOrEmpty(_currentCharacterHash)) PersistIfPossible();

            // Never guess which character owns knowledge created while identity was unavailable.
            // If an unbound session has already earned/imported a recipe, retain it session-only
            // for the remainder of this process. A future provider must be present before recipe
            // progression starts, or its workstream must explicitly migrate known recipe IDs.
            if (string.IsNullOrEmpty(_currentCharacterHash) && _activeLedger == _unboundSessionLedger && _unboundSessionLedger.Count > 0)
            {
                _lateBindingDeferred = true;
                return;
            }

            KnownRecipeLedger target = _document.GetOrCreateCharacter(hash);
            if (target == null)
            {
                _currentCharacterHash = string.Empty;
                _activeLedger = _unboundSessionLedger;
                return;
            }
            _lateBindingDeferred = false;
            _currentCharacterHash = hash;
            _activeLedger = target;
            PersistIfPossible();
        }

        private static bool IsDocumentWritable()
        {
            return _document != null && _document.LoadState != KnownRecipeDocumentLoadState.UnsupportedVersion && _document.LoadState != KnownRecipeDocumentLoadState.IoFailure;
        }

        private static void PersistIfPossible()
        {
            if (!_initialized || string.IsNullOrEmpty(_savePath) || string.IsNullOrEmpty(_currentCharacterHash) || !IsDocumentWritable()) return;
            if (!KnownRecipeStore.Save(_savePath, _document))
            {
                _lastError = KnownRecipeStore.LastError;
                return;
            }
            _lastError = string.Empty;
            if (_document.LoadState == KnownRecipeDocumentLoadState.NewDocument || _document.LoadState == KnownRecipeDocumentLoadState.MigratedLegacyV0)
                _document.LoadState = KnownRecipeDocumentLoadState.Loaded;
        }

        private static void ApplyTemplateSafetyForRegisteredRecipes()
        {
            List<RecipeOwnershipDefinition> definitions = Catalog.Snapshot();
            for (int i = 0; i < definitions.Count; i++)
            {
                RecipeOwnershipDefinition definition = definitions[i];
                if (definition == null || definition.Deprecated) continue;
                GameItemRegistryApi.TryApplyRecipeTemplateSafety(definition.TemplateItemId, definition.DisplayName);
            }
        }

        private static RecipeOwnershipActionResult Result(RecipeOwnershipActionKind kind, bool success, string message)
        {
            return new RecipeOwnershipActionResult { Kind = kind, Success = success, Message = message ?? string.Empty };
        }
    }

    // Public, intentionally small integration surface for the parallel native-recipe/catalog pass.
    // It does not register ItemDB recipes itself. The recipe workstream registers a stable identity
    // here after its own native recipe mutation has been proven safe.
    public static class RecipeOwnershipApi
    {
        public static RecipeOwnershipDefinitionRejectReason RegisterRecipe(RecipeOwnershipDefinition definition)
        {
            return RecipeOwnershipController.RegisterDefinition(definition);
        }

        public static bool IsKnown(string stableRecipeId)
        {
            return RecipeOwnershipController.IsKnown(stableRecipeId);
        }

        public static RecipeOwnershipActionResult LearnRecipe(string stableRecipeId)
        {
            return RecipeOwnershipController.LearnNewRecipe(stableRecipeId);
        }

        public static RecipeOwnershipActionResult ImportKnownRecipe(string stableRecipeId, long learnedUtcTicks)
        {
            return RecipeOwnershipController.ImportKnownRecipe(stableRecipeId, learnedUtcTicks);
        }

        public static RecipeOwnershipActionResult RestoreTemplate(string stableRecipeId)
        {
            return RecipeOwnershipController.Restore(stableRecipeId);
        }
    }
}
