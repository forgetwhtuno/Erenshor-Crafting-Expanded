namespace ErenshorCraftingExpanded
{
    public enum ForageAvailability
    {
        Available = 0,
        Gathering = 1,
        GrantPending = 2,
        Depleted = 3
    }

    // Pure token-aware gather state. Inventory/progression/presentation remain controller concerns;
    // this object only guarantees that one node cannot transition through two grants for one token.
    public sealed class ForageNodeRuntimeState
    {
        public ForageAvailability Availability { get; private set; }
        public float RemainingRespawnSeconds { get; private set; }
        public long ActiveGatherToken { get; private set; }
        public float GatherDurationSeconds { get; private set; }
        public float GatherElapsedSeconds { get; private set; }

        public float GatherProgress01
        {
            get
            {
                if (Availability == ForageAvailability.GrantPending || Availability == ForageAvailability.Depleted) return 1f;
                if (Availability != ForageAvailability.Gathering || GatherDurationSeconds <= 0f) return 0f;
                float value = GatherElapsedSeconds / GatherDurationSeconds;
                if (value < 0f) return 0f;
                if (value > 1f) return 1f;
                return value;
            }
        }

        public ForageNodeRuntimeState()
        {
            ResetAvailable();
        }

        public bool TryBeginGather(long token, float durationSeconds)
        {
            if (Availability != ForageAvailability.Available || token <= 0 || !IsFinitePositive(durationSeconds)) return false;
            Availability = ForageAvailability.Gathering;
            ActiveGatherToken = token;
            GatherDurationSeconds = durationSeconds;
            GatherElapsedSeconds = 0f;
            RemainingRespawnSeconds = 0f;
            return true;
        }

        public bool IsTokenActive(long token)
        {
            return token > 0 && ActiveGatherToken == token &&
                (Availability == ForageAvailability.Gathering || Availability == ForageAvailability.GrantPending);
        }

        public bool IsGatherReady(long token)
        {
            return Availability == ForageAvailability.Gathering && ActiveGatherToken == token &&
                GatherDurationSeconds > 0f && GatherElapsedSeconds >= GatherDurationSeconds;
        }

        public bool TryEnterGrantPending(long token)
        {
            if (!IsGatherReady(token)) return false;
            Availability = ForageAvailability.GrantPending;
            GatherElapsedSeconds = GatherDurationSeconds;
            return true;
        }

        public bool CancelGather(long token)
        {
            if (Availability != ForageAvailability.Gathering || ActiveGatherToken != token) return false;
            ResetAvailable();
            return true;
        }

        public bool RejectGrant(long token)
        {
            if (Availability != ForageAvailability.GrantPending || ActiveGatherToken != token) return false;
            ResetAvailable();
            return true;
        }

        public bool CompleteGrantSuccess(long token, float respawnSeconds)
        {
            if (Availability != ForageAvailability.GrantPending || ActiveGatherToken != token) return false;
            EnterDepleted(respawnSeconds);
            return true;
        }

        // An exception after AddItemToInv was invoked is ambiguous: the item may already exist.
        // Fail closed by consuming this runtime node for its normal cooldown, but do not let the
        // caller claim XP/discovery/depletion-ledger authority without a verified Success result.
        public bool FailClosedUnknownAfterInvoke(long token, float respawnSeconds)
        {
            if (Availability != ForageAvailability.GrantPending || ActiveGatherToken != token) return false;
            EnterDepleted(respawnSeconds);
            return true;
        }

        // Persistence restoration is not a gather transaction. It directly recreates a previously
        // committed successful depletion with its remaining cooldown.
        public bool RestorePersistedDepletion(float remainingSeconds)
        {
            if (Availability != ForageAvailability.Available || !IsFiniteNonNegative(remainingSeconds)) return false;
            if (remainingSeconds <= 0f) return false;
            EnterDepleted(remainingSeconds);
            return true;
        }

        public void Tick(float deltaSeconds)
        {
            if (!IsFiniteNonNegative(deltaSeconds)) deltaSeconds = 0f;
            if (Availability == ForageAvailability.Gathering)
            {
                GatherElapsedSeconds += deltaSeconds;
                if (GatherElapsedSeconds > GatherDurationSeconds) GatherElapsedSeconds = GatherDurationSeconds;
                return;
            }
            if (Availability != ForageAvailability.Depleted) return;
            RemainingRespawnSeconds -= deltaSeconds;
            if (RemainingRespawnSeconds <= 0f) ResetAvailable();
        }

        private void EnterDepleted(float respawnSeconds)
        {
            Availability = ForageAvailability.Depleted;
            RemainingRespawnSeconds = IsFiniteNonNegative(respawnSeconds) ? respawnSeconds : 0f;
            ActiveGatherToken = 0;
            GatherElapsedSeconds = 0f;
            GatherDurationSeconds = 0f;
            if (RemainingRespawnSeconds <= 0f) ResetAvailable();
        }

        private void ResetAvailable()
        {
            Availability = ForageAvailability.Available;
            RemainingRespawnSeconds = 0f;
            ActiveGatherToken = 0;
            GatherElapsedSeconds = 0f;
            GatherDurationSeconds = 0f;
        }

        private static bool IsFinitePositive(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;
        }

        private static bool IsFiniteNonNegative(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f;
        }

        internal static string RunSelfTests()
        {
            ForageNodeRuntimeState state = new ForageNodeRuntimeState();
            if (state.Availability != ForageAvailability.Available) return "FAIL initial state should be available";
            if (!state.TryBeginGather(11, 1.25f)) return "FAIL available -> gathering";
            if (state.Availability != ForageAvailability.Gathering || state.ActiveGatherToken != 11) return "FAIL gathering token state";
            if (state.TryBeginGather(12, 1.25f)) return "FAIL double begin accepted";
            if (state.GatherProgress01 != 0f) return "FAIL initial gather progress";
            state.Tick(0.625f);
            if (state.GatherProgress01 < 0.49f || state.GatherProgress01 > 0.51f) return "FAIL gather progress midpoint";
            if (state.TryEnterGrantPending(11)) return "FAIL grant pending before duration";
            state.Tick(9f);
            if (state.GatherProgress01 != 1f) return "FAIL gather progress should clamp to one";
            if (state.TryEnterGrantPending(12)) return "FAIL stale token entered grant pending";
            if (!state.TryEnterGrantPending(11) || state.Availability != ForageAvailability.GrantPending) return "FAIL gathering -> grant pending";
            if (state.CompleteGrantSuccess(12, 10f)) return "FAIL stale token completed grant";
            if (!state.CompleteGrantSuccess(11, 10f) || state.Availability != ForageAvailability.Depleted) return "FAIL grant pending -> depleted";
            if (state.ActiveGatherToken != 0) return "FAIL successful grant retained token";
            state.Tick(5f);
            if (state.Availability != ForageAvailability.Depleted) return "FAIL depleted respawn early";
            state.Tick(5f);
            if (state.Availability != ForageAvailability.Available || state.RemainingRespawnSeconds != 0f) return "FAIL depleted respawn completion";

            ForageNodeRuntimeState cancel = new ForageNodeRuntimeState();
            if (!cancel.TryBeginGather(21, 1.25f)) return "FAIL cancel begin";
            cancel.Tick(0.8f);
            if (cancel.CancelGather(22)) return "FAIL stale cancel token";
            if (!cancel.CancelGather(21)) return "FAIL active cancel";
            if (cancel.Availability != ForageAvailability.Available || cancel.GatherProgress01 != 0f) return "FAIL cancel should restore full bar/available";

            ForageNodeRuntimeState rejected = new ForageNodeRuntimeState();
            rejected.TryBeginGather(31, 1f);
            rejected.Tick(1f);
            rejected.TryEnterGrantPending(31);
            if (!rejected.RejectGrant(31) || rejected.Availability != ForageAvailability.Available) return "FAIL rejected grant rollback";

            ForageNodeRuntimeState unknown = new ForageNodeRuntimeState();
            unknown.TryBeginGather(41, 1f);
            unknown.Tick(1f);
            unknown.TryEnterGrantPending(41);
            if (!unknown.FailClosedUnknownAfterInvoke(41, 3f) || unknown.Availability != ForageAvailability.Depleted) return "FAIL unknown invoked grant should fail closed";
            if (unknown.TryBeginGather(42, 1f)) return "FAIL fail-closed node offered immediate retry";

            ForageNodeRuntimeState persisted = new ForageNodeRuntimeState();
            if (!persisted.RestorePersistedDepletion(4f) || persisted.Availability != ForageAvailability.Depleted) return "FAIL persisted depletion restore";
            if (persisted.RestorePersistedDepletion(4f)) return "FAIL duplicate persisted depletion restore";

            return "PASS forage node state";
        }
    }
}
