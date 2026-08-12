using System;
using UnityEngine;

namespace ErenshorCraftingExpanded
{
    // Compact IMGUI panel drawn from OnGUI, matching the mockup in the user's spec: Smithing
    // level/XP, current commission with Accept/Decline, forge Can-Craft count, hotkey display.
    // No GameObject/Canvas is created - state lives in static fields, so nothing can duplicate
    // across scene reloads (same property Erenshor-PvP's PvpPanel relies on).
    internal static class CraftingWindow
    {
        private const int WindowId = 764412;
        private const float Width = 260f;
        private const float HeaderHeight = 22f;

        private static CraftingPanelPositionState _positionState;
        private static Rect _window = new Rect(0f, 0f, Width, 150f);
        private static Rect _toggleRect = new Rect(0f, 0f, 0f, 0f);
        private static float _windowHeight = 150f;
        private static bool _dragging;
        private static Vector2 _dragOffset;
        private static GUIStyle _windowStyle;
        private static GUIStyle _headerStyle;
        private static bool _stylesReady;

        internal static void ConfigurePosition(float offsetX, float offsetY, Action<float, float> persist)
        {
            _positionState = new CraftingPanelPositionState(offsetX, offsetY, persist);
        }

        internal static bool PointerIsOverUi(Vector2 screenPoint)
        {
            if (CraftingUiStateMachine.IsPanelVisible())
            {
                if (_dragging) return true;
                if (_window.width > 0f && _window.Contains(screenPoint)) return true;
            }
            return CraftingUiStateMachine.IsToggleVisible() && _toggleRect.width > 0f && _toggleRect.Contains(screenPoint);
        }

        internal static void ResetTransientState()
        {
            _dragging = false;
            _toggleRect = new Rect(0f, 0f, 0f, 0f);
        }

        internal static void DrawToggleButton()
        {
            if (!CraftingUiStateMachine.IsToggleVisible())
            {
                _toggleRect = new Rect(0f, 0f, 0f, 0f);
                return;
            }
            const float w = 84f, h = 22f;
            _toggleRect = new Rect(CraftingPanelPositioning.LeftMargin, CraftingPanelPositioning.DefaultTop - h - 4f, w, h);
            if (GUI.Button(_toggleRect, "Crafting")) CraftingUiStateMachine.ToggleOpen();
        }

        internal static void Draw()
        {
            if (!CraftingUiStateMachine.IsPanelVisible())
            {
                _dragging = false;
                return;
            }
            EnsureStyles();
            if (_positionState == null) ConfigurePosition(0f, 0f, null);

            float height = Mathf.Max(60f, _windowHeight);
            CraftingPanelPosition anchored = _positionState.ResolveAndRecover(Screen.width, Screen.height, Width, height);
            _window = new Rect(anchored.X, anchored.Y, Width, height);

            HandleDrag(height);

            int previousDepth = GUI.depth;
            try
            {
                GUI.depth = -45;
                Rect drawn = GUILayout.Window(WindowId, _window, DrawWindow, GUIContent.none, _windowStyle, new[] { GUILayout.Width(Width) });
                _windowHeight = drawn.height;
                _window.height = drawn.height;
            }
            finally { GUI.depth = previousDepth; }
        }

        private static void HandleDrag(float height)
        {
            Event e = Event.current;
            if (e == null) return;
            Rect header = new Rect(_window.x, _window.y, _window.width, HeaderHeight);

            if (e.type == EventType.MouseDown && e.button == 0 && header.Contains(e.mousePosition))
            {
                _dragging = true;
                _dragOffset = new Vector2(e.mousePosition.x - _window.x, e.mousePosition.y - _window.y);
                e.Use();
            }
            else if (e.type == EventType.MouseDrag && _dragging)
            {
                float desiredX = e.mousePosition.x - _dragOffset.x;
                float desiredY = e.mousePosition.y - _dragOffset.y;
                _positionState.MoveTo(Screen.width, Screen.height, Width, height, desiredX, desiredY);
                e.Use();
            }
            else if (e.type == EventType.MouseUp && _dragging)
            {
                _dragging = false;
                _positionState.CommitIfMoved();
                e.Use();
            }
        }

        private static void DrawWindow(int id)
        {
            GUILayout.Label("<b>CRAFTING</b>", _headerStyle);

            CraftingProgress progress = CraftingController.Progress;
            int need = SmithingXpCurve.XpToNextLevel(progress.Level);
            GUILayout.Label(progress.Profession + " Lv " + progress.Level);
            GUILayout.Label(need > 0 ? (progress.Xp + " / " + need + " XP") : "Max level");

            GUILayout.Space(4f);
            CraftingCommission commission = CommissionController.Current;
            if (commission != null && (commission.State == CommissionState.Offered || commission.State == CommissionState.Accepted))
            {
                GUILayout.Label("Current Request");
                GUILayout.Label(commission.SimName + " needs:");
                GUILayout.Label(commission.RequestedItemName);
                if (commission.State == CommissionState.Offered)
                {
                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button("Accept")) CommissionController.Accept();
                    if (GUILayout.Button("Decline")) CommissionController.Decline();
                    GUILayout.EndHorizontal();
                }
                else
                {
                    GUILayout.Label("(accepted)");
                }
            }
            else
            {
                GUILayout.Label("No active request.");
            }

            GUILayout.Space(4f);
            GUILayout.Label("Forge");
            GUILayout.Label("Can Craft: " + CraftingController.LastCraftableCount);
            string hotkeyLabel = CraftingConfig.CraftHotkey != null && CraftingConfig.CraftHotkey.Value != KeyCode.None
                ? CraftingConfig.CraftHotkey.Value.ToString()
                : "(unbound)";
            GUILayout.Label("Hotkey: " + hotkeyLabel);

            GUILayout.Space(4f);
            GUILayout.BeginHorizontal();
            bool pinned = CraftingUiStateMachine.Current == CraftingUiState.PinnedOpen;
            bool newPinned = GUILayout.Toggle(pinned, "Pin");
            if (newPinned != pinned) CraftingUiStateMachine.SetPinned(newPinned);
            if (GUILayout.Button("Close")) CraftingUiStateMachine.ToggleOpen();
            GUILayout.EndHorizontal();

            GUI.DragWindow(new Rect(0f, 0f, 0f, 0f)); // drag handled manually above; keeps window non-interactive to native DragWindow.
        }

        private static void EnsureStyles()
        {
            if (_stylesReady) return;
            _windowStyle = new GUIStyle(GUI.skin.window);
            _headerStyle = new GUIStyle(GUI.skin.label) { richText = true, fontStyle = FontStyle.Bold };
            _stylesReady = true;
        }
    }
}
