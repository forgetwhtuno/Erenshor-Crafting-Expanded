using System;

namespace ErenshorCraftingExpanded
{
    public enum ForageGatherCancelReason
    {
        None = 0,
        GameplayDisabled = 1,
        Typing = 2,
        ZoneChanged = 3,
        CharacterChanged = 4,
        JumpOrFall = 5,
        Movement = 6,
        OutOfRange = 7,
        Occluded = 8,
        Damaged = 9,
        DifferentNodeClick = 10,
        WorldInteraction = 11,
        PluginUnload = 12,
        RuntimeException = 13,
        LocalHostileAggro = 14
    }

    // Pure frame policy so cancellation precedence remains deterministic even on the exact frame
    // the gather duration reaches completion. The controller evaluates this before GrantPending.
    public static class ForageGatherCancellationPolicy
    {
        public const float MovementCancelDistance = 0.20f;
        public const float VerticalCancelDistance = 0.12f;

        public static ForageGatherCancelReason EvaluateFrame(
            bool gameplayEnabled,
            bool typing,
            bool zoneChanged,
            bool characterChanged,
            float deltaX,
            float deltaY,
            float deltaZ,
            float nodeDistance,
            float interactionRange,
            bool occluded,
            int startHp,
            int currentHp)
        {
            if (!gameplayEnabled) return ForageGatherCancelReason.GameplayDisabled;
            if (typing) return ForageGatherCancelReason.Typing;
            if (zoneChanged) return ForageGatherCancelReason.ZoneChanged;
            if (characterChanged) return ForageGatherCancelReason.CharacterChanged;
            if (!IsFinite(deltaY) || Math.Abs(deltaY) > VerticalCancelDistance) return ForageGatherCancelReason.JumpOrFall;
            if (HasMoved(deltaX, deltaY, deltaZ, MovementCancelDistance)) return ForageGatherCancelReason.Movement;
            if (!IsFinitePositive(interactionRange) || !IsFiniteNonNegative(nodeDistance) || nodeDistance > interactionRange)
                return ForageGatherCancelReason.OutOfRange;
            if (occluded) return ForageGatherCancelReason.Occluded;
            if (startHp >= 0 && currentHp >= 0 && currentHp < startHp) return ForageGatherCancelReason.Damaged;
            return ForageGatherCancelReason.None;
        }

        public static bool HasMoved(float deltaX, float deltaY, float deltaZ, float threshold)
        {
            if (!IsFinite(deltaX) || !IsFinite(deltaY) || !IsFinite(deltaZ) || !IsFinitePositive(threshold)) return true;
            float squared = deltaX * deltaX + deltaY * deltaY + deltaZ * deltaZ;
            return squared > threshold * threshold;
        }

        public static string Describe(ForageGatherCancelReason reason)
        {
            switch (reason)
            {
                case ForageGatherCancelReason.GameplayDisabled: return "gameplay-disabled";
                case ForageGatherCancelReason.Typing: return "typing";
                case ForageGatherCancelReason.ZoneChanged: return "zone";
                case ForageGatherCancelReason.CharacterChanged: return "character";
                case ForageGatherCancelReason.JumpOrFall: return "jump-fall";
                case ForageGatherCancelReason.Movement: return "movement";
                case ForageGatherCancelReason.OutOfRange: return "range";
                case ForageGatherCancelReason.Occluded: return "occlusion";
                case ForageGatherCancelReason.Damaged: return "damage";
                case ForageGatherCancelReason.DifferentNodeClick: return "different-node";
                case ForageGatherCancelReason.WorldInteraction: return "world-interaction";
                case ForageGatherCancelReason.PluginUnload: return "plugin-unload";
                case ForageGatherCancelReason.RuntimeException: return "exception";
                case ForageGatherCancelReason.LocalHostileAggro: return "local-hostile-aggro";
                default: return "none";
            }
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static bool IsFinitePositive(float value)
        {
            return IsFinite(value) && value > 0f;
        }

        private static bool IsFiniteNonNegative(float value)
        {
            return IsFinite(value) && value >= 0f;
        }

        internal static string RunSelfTests()
        {
            ForageGatherCancelReason none = EvaluateFrame(true, false, false, false, 0f, 0f, 0f, 2f, 3.5f, false, 100, 100);
            if (none != ForageGatherCancelReason.None) return "FAIL stationary gather cancelled";
            if (EvaluateFrame(false, false, false, false, 0f, 0f, 0f, 2f, 3.5f, false, 100, 100) != ForageGatherCancelReason.GameplayDisabled) return "FAIL disable cancel";
            if (EvaluateFrame(true, true, false, false, 0f, 0f, 0f, 2f, 3.5f, false, 100, 100) != ForageGatherCancelReason.Typing) return "FAIL typing cancel";
            if (EvaluateFrame(true, false, true, false, 0f, 0f, 0f, 2f, 3.5f, false, 100, 100) != ForageGatherCancelReason.ZoneChanged) return "FAIL zone cancel";
            if (EvaluateFrame(true, false, false, true, 0f, 0f, 0f, 2f, 3.5f, false, 100, 100) != ForageGatherCancelReason.CharacterChanged) return "FAIL character cancel";
            if (EvaluateFrame(true, false, false, false, 0f, 0.13f, 0f, 2f, 3.5f, false, 100, 100) != ForageGatherCancelReason.JumpOrFall) return "FAIL jump/fall cancel";
            if (EvaluateFrame(true, false, false, false, 0.21f, 0f, 0f, 2f, 3.5f, false, 100, 100) != ForageGatherCancelReason.Movement) return "FAIL movement cancel";
            if (EvaluateFrame(true, false, false, false, 0f, 0f, 0f, 3.6f, 3.5f, false, 100, 100) != ForageGatherCancelReason.OutOfRange) return "FAIL range cancel";
            if (EvaluateFrame(true, false, false, false, 0f, 0f, 0f, 2f, 3.5f, true, 100, 100) != ForageGatherCancelReason.Occluded) return "FAIL occlusion cancel";
            if (EvaluateFrame(true, false, false, false, 0f, 0f, 0f, 2f, 3.5f, false, 100, 99) != ForageGatherCancelReason.Damaged) return "FAIL damage cancel";
            if (Describe(ForageGatherCancelReason.PluginUnload) != "plugin-unload" || Describe(ForageGatherCancelReason.RuntimeException) != "exception" || Describe(ForageGatherCancelReason.LocalHostileAggro) != "local-hostile-aggro") return "FAIL terminal cancel descriptions";
            if (!HasMoved(0.2f, 0f, 0.001f, MovementCancelDistance)) return "FAIL 3D movement threshold";
            if (HasMoved(0.19f, 0f, 0f, MovementCancelDistance)) return "FAIL sub-threshold movement";
            // Completion-boundary requirement: a guard failure wins even when elapsed time has
            // already reached duration because EvaluateFrame runs before TryEnterGrantPending.
            if (EvaluateFrame(true, false, false, false, 0.21f, 0f, 0f, 2f, 3.5f, false, 100, 100) == ForageGatherCancelReason.None)
                return "FAIL completion-boundary cancellation precedence";
            return "PASS forage gather cancellation policy";
        }
    }
}
