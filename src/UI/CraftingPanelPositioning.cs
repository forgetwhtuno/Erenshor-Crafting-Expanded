using System;

namespace ErenshorCraftingExpanded
{
    // Same offset-anchoring contract as Erenshor-PvP's PvpPanelPositioning / Erenshor-Party-
    // Tools' PanelPositioning, so all three mods' panels anchor consistently and never need to
    // agree on absolute screen coordinates - only a persisted offset from a fixed anchor.
    internal struct CraftingPanelPosition
    {
        internal readonly float X;
        internal readonly float Y;
        internal CraftingPanelPosition(float x, float y) { X = x; Y = y; }
    }

    internal struct CraftingPanelOffsets
    {
        internal readonly float X;
        internal readonly float Y;
        internal CraftingPanelOffsets(float x, float y) { X = x; Y = y; }
    }

    internal static class CraftingPanelPositioning
    {
        internal const float ScreenMargin = 8f;
        internal const float LeftMargin = 18f;
        internal const float DefaultTop = 336f;
        private const float PositionEpsilon = 0.01f;

        // Anchored to the left edge (PvP/Party Tools already own the upper-right area).
        internal static CraftingPanelPosition Resolve(
            float screenWidth, float screenHeight, float panelWidth, float panelHeight,
            float offsetX, float offsetY)
        {
            offsetX = FiniteOrDefault(offsetX, 0f);
            offsetY = FiniteOrDefault(offsetY, 0f);
            float desiredX = LeftMargin + offsetX;
            float desiredY = DefaultTop + offsetY;
            return Clamp(screenWidth, screenHeight, panelWidth, panelHeight, desiredX, desiredY);
        }

        internal static CraftingPanelPosition Clamp(
            float screenWidth, float screenHeight, float panelWidth, float panelHeight,
            float desiredX, float desiredY)
        {
            float maxX = Math.Max(ScreenMargin, screenWidth - panelWidth - ScreenMargin);
            float maxY = Math.Max(ScreenMargin, screenHeight - panelHeight - ScreenMargin);
            return new CraftingPanelPosition(
                ClampValue(desiredX, ScreenMargin, maxX),
                ClampValue(desiredY, ScreenMargin, maxY));
        }

        internal static CraftingPanelOffsets ToOffsets(CraftingPanelPosition position)
        {
            return new CraftingPanelOffsets(position.X - LeftMargin, position.Y - DefaultTop);
        }

        internal static bool NearlyEqual(float left, float right)
        {
            return Math.Abs(left - right) <= PositionEpsilon;
        }

        private static float ClampValue(float value, float minimum, float maximum)
        {
            if (!IsFinite(value)) return minimum;
            if (value < minimum) return minimum;
            if (value > maximum) return maximum;
            return value;
        }

        private static float FiniteOrDefault(float value, float fallback) { return IsFinite(value) ? value : fallback; }
        private static bool IsFinite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }

        internal static string RunSelfTests()
        {
            CraftingPanelPosition resolved = Resolve(1920f, 1080f, 300f, 260f, 0f, 0f);
            if (!NearlyEqual(resolved.X, LeftMargin)) return "FAIL default x";
            if (!NearlyEqual(resolved.Y, DefaultTop)) return "FAIL default y";

            CraftingPanelPosition tall = Resolve(800f, 300f, 300f, 900f, 0f, 0f);
            if (tall.Y < ScreenMargin) return "FAIL tall clamp";

            CraftingPanelPosition moved = Clamp(1920f, 1080f, 300f, 260f, 500f, 400f);
            CraftingPanelOffsets offsets = ToOffsets(moved);
            CraftingPanelPosition restored = Resolve(1920f, 1080f, 300f, 260f, offsets.X, offsets.Y);
            if (!NearlyEqual(restored.X, moved.X) || !NearlyEqual(restored.Y, moved.Y)) return "FAIL offset round trip";

            CraftingPanelPosition garbage = Resolve(1920f, 1080f, 300f, 260f, float.NaN, float.PositiveInfinity);
            if (!NearlyEqual(garbage.X, LeftMargin)) return "FAIL non-finite offset recovery";
            return "PASS crafting panel positioning";
        }
    }

    internal sealed class CraftingPanelPositionState
    {
        private readonly Action<float, float> _persist;
        private float _offsetX;
        private float _offsetY;
        private bool _dirty;

        internal CraftingPanelPositionState(float offsetX, float offsetY, Action<float, float> persist)
        {
            _offsetX = offsetX;
            _offsetY = offsetY;
            _persist = persist;
        }

        internal CraftingPanelPosition ResolveAndRecover(float screenWidth, float screenHeight, float panelWidth, float panelHeight)
        {
            CraftingPanelPosition position = CraftingPanelPositioning.Resolve(screenWidth, screenHeight, panelWidth, panelHeight, _offsetX, _offsetY);
            CraftingPanelOffsets normalized = CraftingPanelPositioning.ToOffsets(position);
            if (SetOffsets(normalized.X, normalized.Y)) { _dirty = false; Persist(); }
            return position;
        }

        internal CraftingPanelPosition MoveTo(float screenWidth, float screenHeight, float panelWidth, float panelHeight, float desiredX, float desiredY)
        {
            CraftingPanelPosition position = CraftingPanelPositioning.Clamp(screenWidth, screenHeight, panelWidth, panelHeight, desiredX, desiredY);
            CraftingPanelOffsets offsets = CraftingPanelPositioning.ToOffsets(position);
            if (SetOffsets(offsets.X, offsets.Y)) _dirty = true;
            return position;
        }

        internal void CommitIfMoved()
        {
            if (!_dirty) return;
            _dirty = false;
            Persist();
        }

        private bool SetOffsets(float offsetX, float offsetY)
        {
            if (CraftingPanelPositioning.NearlyEqual(_offsetX, offsetX) && CraftingPanelPositioning.NearlyEqual(_offsetY, offsetY)) return false;
            _offsetX = offsetX;
            _offsetY = offsetY;
            return true;
        }

        private void Persist()
        {
            if (_persist != null) _persist(_offsetX, _offsetY);
        }
    }
}
