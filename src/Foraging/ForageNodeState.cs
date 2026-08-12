namespace ErenshorCraftingExpanded
{
    public enum ForageAvailability
    {
        Available = 0,
        Depleted = 1
    }

    // Pure in-memory state machine, modeled on vanilla MiningNode's own approach (toggle +
    // countdown timer, no save-file involvement - see docs/NATIVE_MINING_AND_FORAGING_FINDINGS.md
    // section 1). One instance per spawned node.
    public sealed class ForageNodeRuntimeState
    {
        public ForageAvailability Availability { get; private set; }
        public float RemainingRespawnSeconds { get; private set; }

        public ForageNodeRuntimeState()
        {
            Availability = ForageAvailability.Available;
            RemainingRespawnSeconds = 0f;
        }

        // Returns true and flips to Depleted exactly once per gather; a second call while
        // already Depleted returns false and changes nothing, so callers can never grant a
        // duplicate reward from one node instance.
        public bool TryGather(float respawnSeconds)
        {
            if (Availability != ForageAvailability.Available) return false;
            Availability = ForageAvailability.Depleted;
            RemainingRespawnSeconds = respawnSeconds > 0f ? respawnSeconds : 0f;
            return true;
        }

        public void Tick(float deltaSeconds)
        {
            if (Availability != ForageAvailability.Depleted) return;
            RemainingRespawnSeconds -= deltaSeconds;
            if (RemainingRespawnSeconds <= 0f)
            {
                RemainingRespawnSeconds = 0f;
                Availability = ForageAvailability.Available;
            }
        }

        internal static string RunSelfTests()
        {
            ForageNodeRuntimeState state = new ForageNodeRuntimeState();
            if (state.Availability != ForageAvailability.Available) return "FAIL initial state should be available";

            if (!state.TryGather(10f)) return "FAIL available node should allow gather";
            if (state.Availability != ForageAvailability.Depleted) return "FAIL gather should deplete node";

            int rewardCount = 0;
            if (state.TryGather(10f)) rewardCount++; // first (already counted above via TryGather return, so this call is the "second attempt")
            if (rewardCount != 0) return "FAIL depleted node rejected second gather but a reward was still granted";

            state.Tick(5f);
            if (state.Availability != ForageAvailability.Depleted) return "FAIL timer below threshold should remain depleted";
            if (state.RemainingRespawnSeconds <= 0f) return "FAIL remaining timer should still be positive below threshold";

            state.Tick(5f); // total 10s elapsed, matches the 10s respawn window
            if (state.Availability != ForageAvailability.Available) return "FAIL timer at threshold should become available";

            ForageNodeRuntimeState duplicateCheck = new ForageNodeRuntimeState();
            int granted = 0;
            if (duplicateCheck.TryGather(5f)) granted++;
            if (duplicateCheck.TryGather(5f)) granted++;
            if (duplicateCheck.TryGather(5f)) granted++;
            if (granted != 1) return "FAIL duplicate gather calls should grant exactly one reward";

            // "Respawn becomes available exactly once": once the timer crosses zero, further
            // Tick() calls must not re-trigger anything or let RemainingRespawnSeconds drift
            // negative - the node just stays Available.
            ForageNodeRuntimeState respawnOnce = new ForageNodeRuntimeState();
            respawnOnce.TryGather(3f);
            int availableTransitions = 0;
            ForageAvailability previous = respawnOnce.Availability;
            for (int i = 0; i < 10; i++)
            {
                respawnOnce.Tick(1f); // 10 ticks of 1s against a 3s respawn - crosses zero once, then idles
                if (respawnOnce.Availability == ForageAvailability.Available && previous == ForageAvailability.Depleted)
                    availableTransitions++;
                previous = respawnOnce.Availability;
            }
            if (availableTransitions != 1) return "FAIL respawn should become available exactly once, not " + availableTransitions + " times";
            if (respawnOnce.RemainingRespawnSeconds != 0f) return "FAIL remaining respawn timer should pin at zero once available, not drift negative";

            return "PASS forage node state";
        }
    }
}
