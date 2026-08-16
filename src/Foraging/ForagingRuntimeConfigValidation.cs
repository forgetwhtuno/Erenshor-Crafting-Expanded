namespace ErenshorCraftingExpanded
{
    // Pure guards for numeric Foraging config values. These protect against hand-edited cfg
    // values such as NaN/Infinity/negative ranges without relying on BepInEx validation support.
    public static class ForagingRuntimeConfigValidation
    {
        public static bool IsValidInteractionRange(float meters)
        {
            return IsFinite(meters) && meters > 0f && meters <= 50f;
        }

        public static bool IsValidScanRadius(float meters)
        {
            return IsFinite(meters) && meters > 0f && meters <= 100f;
        }

        public const float MinimumGatherDurationSeconds = 1.0f;
        public const float MaximumGatherDurationSeconds = 1.5f;
        public const float DefaultGatherDurationSeconds = 1.25f;

        public static bool IsValidGatherDuration(float seconds)
        {
            return IsFinite(seconds) && seconds >= MinimumGatherDurationSeconds && seconds <= MaximumGatherDurationSeconds;
        }

        public static float NormalizeGatherDuration(float seconds)
        {
            if (!IsFinite(seconds)) return DefaultGatherDurationSeconds;
            if (seconds < MinimumGatherDurationSeconds) return MinimumGatherDurationSeconds;
            if (seconds > MaximumGatherDurationSeconds) return MaximumGatherDurationSeconds;
            return seconds;
        }

        // 0 means "disabled". Positive development overrides are bounded to one day so a typo
        // cannot silently leave a test node depleted for an absurd amount of time.
        public static bool IsValidDebugRespawnOverride(float seconds)
        {
            return IsFinite(seconds) && seconds >= 0f && seconds <= 86400f;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        internal static string RunSelfTests()
        {
            if (IsValidInteractionRange(0f)) return "FAIL zero interaction range accepted";
            if (IsValidInteractionRange(-1f)) return "FAIL negative interaction range accepted";
            if (IsValidInteractionRange(float.NaN)) return "FAIL NaN interaction range accepted";
            if (!IsValidInteractionRange(3f)) return "FAIL reasonable interaction range rejected";

            if (IsValidScanRadius(0f)) return "FAIL zero scan radius accepted";
            if (IsValidScanRadius(-5f)) return "FAIL negative scan radius accepted";
            if (IsValidScanRadius(float.PositiveInfinity)) return "FAIL infinite scan radius accepted";
            if (!IsValidScanRadius(12f)) return "FAIL reasonable scan radius rejected";

            if (!IsValidGatherDuration(1f) || !IsValidGatherDuration(1.25f) || !IsValidGatherDuration(1.5f)) return "FAIL production gather duration range";
            if (IsValidGatherDuration(0.99f) || IsValidGatherDuration(1.51f) || IsValidGatherDuration(float.NaN)) return "FAIL invalid gather duration accepted";
            if (NormalizeGatherDuration(0.1f) != MinimumGatherDurationSeconds) return "FAIL low gather duration clamp";
            if (NormalizeGatherDuration(9f) != MaximumGatherDurationSeconds) return "FAIL high gather duration clamp";
            if (NormalizeGatherDuration(float.PositiveInfinity) != DefaultGatherDurationSeconds) return "FAIL invalid gather duration default";

            if (!IsValidDebugRespawnOverride(0f)) return "FAIL zero debug respawn should disable override";
            if (!IsValidDebugRespawnOverride(45f)) return "FAIL reasonable debug respawn rejected";
            if (IsValidDebugRespawnOverride(-1f)) return "FAIL negative debug respawn accepted";
            if (IsValidDebugRespawnOverride(90000f)) return "FAIL unreasonable debug respawn accepted";

            return "PASS foraging runtime config validation";
        }
    }
}
