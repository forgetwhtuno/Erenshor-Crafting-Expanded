using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ErenshorCraftingExpanded
{
    internal static class CraftingWindow
    {
        private const float Width = 320f;
        private const float Height = 400f;
        private static GameObject _root;
        private static RectTransform _panel;
        private static RectTransform _commissionRoot;
        private static TextMeshProUGUI _level;
        private static TextMeshProUGUI _xp;
        private static TextMeshProUGUI _forge;
        private static TextMeshProUGUI _hotkey;
        private static TextMeshProUGUI _commissionText;
        private static Button _accept;
        private static Button _decline;
        private static Button _pin;
        private static TextMeshProUGUI _pinLabel;
        private static Button _enabledButton;
        private static TextMeshProUGUI _enabledLabel;
        private static Button _foragingButton;
        private static TextMeshProUGUI _foragingLabel;
        private static RetainedPosition _position;
        private static string _commissionSignature = string.Empty;

        internal static void Initialize(float x, float y, Action<float, float> persist)
        {
            Dispose();
            _root = RetainedUiKit.CreateCanvas("ErenshorCraftingCanvas", 523);
            RectTransform canvas = _root.GetComponent<RectTransform>();
            _panel = RetainedUiKit.CreateRect("CraftingPanel", canvas);
            RetainedUiKit.AnchorBottomLeft(_panel, 0f, 0f, Width, Height);
            RetainedUiKit.AddImage(_panel, RetainedUiKit.Panel);
            _panel.gameObject.AddComponent<CanvasGroup>();

            RectTransform header = RetainedUiKit.CreateRect("Header", _panel);
            RetainedUiKit.AnchorTopStretch(header, 0f, 0f, 0f, 32f); RetainedUiKit.AddImage(header, RetainedUiKit.Header);
            TextMeshProUGUI title = RetainedUiKit.AddLabel("Title", header, "CRAFTING EXPANDED", 15f, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
            RetainedUiKit.Stretch(title.rectTransform, 10f, 0f, 72f, 0f);
            AddHeaderButton(header, "Reset", "R", -38f, ResetPosition);
            AddHeaderButton(header, "Close", "X", -6f, delegate { CraftingUiStateMachine.Close(); });
            RetainedUiKit.AddDragSurface("DragSurface", header, _panel, 72f,
                delegate { if (_position != null) _position.DragCompleted(_panel); });

            RectTransform content = RetainedUiKit.CreateRect("Content", _panel);
            content.anchorMin = Vector2.zero; content.anchorMax = Vector2.one; content.offsetMin = new Vector2(10f, 10f); content.offsetMax = new Vector2(-10f, -42f);
            VerticalLayoutGroup contentLayout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            contentLayout.padding = new RectOffset(8, 8, 8, 8); contentLayout.spacing = 6f; contentLayout.childAlignment = TextAnchor.UpperLeft;
            contentLayout.childControlWidth = true; contentLayout.childControlHeight = true; contentLayout.childForceExpandWidth = true; contentLayout.childForceExpandHeight = false;
            _level = AddLine(content, "Smithing", true);
            _xp = AddLine(content, "", false);
            _forge = AddLine(content, "", false);
            _hotkey = AddLine(content, "", false);

            RectTransform settingsRow = RetainedUiKit.AddHorizontalRow("Settings", content, 30f, 6f);
            _enabledButton = RetainedUiKit.AddButton("Enabled", settingsRow, "", delegate { CraftingController.SetEnabled(!(CraftingConfig.EnableMod != null && CraftingConfig.EnableMod.Value)); }, 126f, 26f, false);
            _enabledLabel = _enabledButton.GetComponentInChildren<TextMeshProUGUI>();
            _foragingButton = RetainedUiKit.AddButton("Foraging", settingsRow, "", delegate { CraftingController.SetForagingEnabled(!(ForagingConfig.EnableForaging != null && ForagingConfig.EnableForaging.Value)); }, 126f, 26f, false);
            _foragingLabel = _foragingButton.GetComponentInChildren<TextMeshProUGUI>();

            _commissionRoot = RetainedUiKit.CreateRect("Commission", content);
            VerticalLayoutGroup commissionLayout = _commissionRoot.gameObject.AddComponent<VerticalLayoutGroup>();
            commissionLayout.padding = new RectOffset(6, 6, 6, 6); commissionLayout.spacing = 4f; commissionLayout.childAlignment = TextAnchor.UpperLeft;
            commissionLayout.childControlWidth = true; commissionLayout.childControlHeight = true; commissionLayout.childForceExpandWidth = true; commissionLayout.childForceExpandHeight = false;
            LayoutElement cle = _commissionRoot.gameObject.AddComponent<LayoutElement>(); cle.minHeight = 105f; cle.preferredHeight = 105f; cle.flexibleWidth = 1f;
            _commissionText = AddLine(_commissionRoot, "No active request.", false);
            RectTransform buttons = RetainedUiKit.AddHorizontalRow("CommissionActions", _commissionRoot, 28f, 6f);
            _accept = RetainedUiKit.AddButton("Accept", buttons, "Accept", delegate { CommissionController.Accept(); }, 88f, 26f, false);
            _decline = RetainedUiKit.AddButton("Decline", buttons, "Decline", delegate { CommissionController.Decline(); }, 88f, 26f, false);

            RectTransform bottom = RetainedUiKit.AddHorizontalRow("Bottom", content, 30f, 6f);
            _pin = RetainedUiKit.AddButton("Pin", bottom, "Pin", TogglePin, 78f, 26f, false);
            _pinLabel = _pin.GetComponentInChildren<TextMeshProUGUI>();
            RetainedUiKit.AddButton("Close", bottom, "Close", delegate { CraftingUiStateMachine.Close(); }, 78f, 26f, false);

            TextMeshProUGUI hint = AddLine(content, "Native crafting remains authoritative. Debug diagnostics stay in /craftdiag.", false);
            hint.color = RetainedUiKit.Muted;

            _position = new RetainedPosition(x, y, 0.18f, 0.58f, persist);
            _position.Resolve(_panel);
            _root.SetActive(false);
        }

        internal static void Tick(bool visible)
        {
            if (_root == null) return;
            if (_root.activeSelf != visible) _root.SetActive(visible);
            if (!visible) return;
            if (_position != null) _position.Resolve(_panel);

            CraftingProgress progress = CraftingController.Progress ?? new CraftingProgress();
            int need = SmithingXpCurve.XpToNextLevel(progress.Level);
            _level.text = progress.Profession + "  Lv " + progress.Level.ToString();
            _xp.text = need > 0 ? progress.Xp.ToString() + " / " + need.ToString() + " XP" : "Max level";
            _forge.text = "Forge — Can Craft: " + CraftingController.LastCraftableCount.ToString();
            _hotkey.text = "Craft hotkey: " + (CraftingConfig.CraftHotkey != null && CraftingConfig.CraftHotkey.Value != KeyCode.None ? CraftingConfig.CraftHotkey.Value.ToString() : "(unbound)");
            if (_enabledLabel != null) _enabledLabel.text = (CraftingConfig.EnableMod != null && CraftingConfig.EnableMod.Value) ? "Crafting: ON" : "Crafting: OFF";
            if (_foragingLabel != null) _foragingLabel.text = (ForagingConfig.EnableForaging != null && ForagingConfig.EnableForaging.Value) ? "Foraging: ON" : "Foraging: OFF";

            CraftingCommission commission = CommissionController.Current;
            string signature = CommissionSignature(commission);
            if (!string.Equals(signature, _commissionSignature, StringComparison.Ordinal))
            {
                _commissionSignature = signature;
                BindCommission(commission);
            }
            bool pinned = CraftingUiStateMachine.Current == CraftingUiState.PinnedOpen;
            if (_pinLabel != null) _pinLabel.text = pinned ? "Unpin" : "Pin";
        }

        private static void BindCommission(CraftingCommission commission)
        {
            bool active = commission != null && (commission.State == CommissionState.Offered || commission.State == CommissionState.Accepted);
            if (!active)
            {
                _commissionText.text = "Current Request\nNo active request.";
                _accept.gameObject.SetActive(false); _decline.gameObject.SetActive(false); return;
            }
            _commissionText.text = "Current Request\n" + (commission.SimName ?? "Sim") + " needs:\n" + (commission.RequestedItemName ?? "Unknown item") +
                (commission.State == CommissionState.Accepted ? "\n(accepted)" : string.Empty);
            bool offered = commission.State == CommissionState.Offered;
            _accept.gameObject.SetActive(offered); _decline.gameObject.SetActive(offered);
        }

        private static string CommissionSignature(CraftingCommission c)
        {
            if (c == null) return "none";
            return (c.SimName ?? "") + "|" + (c.RequestedItemName ?? "") + "|" + c.State.ToString();
        }

        private static void TogglePin()
        {
            CraftingUiStateMachine.SetPinned(CraftingUiStateMachine.Current != CraftingUiState.PinnedOpen);
        }

        internal static void ResetTransientState()
        {
            SuiteDragHandler.ForceReleaseIfOwned(); _commissionSignature = string.Empty;
        }
        internal static void ResetPosition() { if (_position != null) _position.Reset(_panel); }
        internal static void Dispose()
        {
            SuiteDragHandler.ForceReleaseIfOwned(); RetainedUiKit.DestroyRoot(ref _root);
            _panel = null; _commissionRoot = null; _level = null; _xp = null; _forge = null; _hotkey = null; _commissionText = null;
            _accept = null; _decline = null; _pin = null; _pinLabel = null; _enabledButton = null; _enabledLabel = null; _foragingButton = null; _foragingLabel = null; _position = null; _commissionSignature = string.Empty;
        }

        private static TextMeshProUGUI AddLine(RectTransform parent, string text, bool bold)
        {
            TextMeshProUGUI label = RetainedUiKit.AddLabel("Line", parent, text, 11f, bold ? FontStyles.Bold : FontStyles.Normal, TextAlignmentOptions.TopLeft);
            LayoutElement le = label.gameObject.AddComponent<LayoutElement>(); le.minHeight = 24f; le.flexibleWidth = 1f;
            return label;
        }
        private static void AddHeaderButton(RectTransform header, string name, string label, float right, Action action)
        {
            Button b = RetainedUiKit.AddButton(name, header, label, action, 28f, 24f, false);
            RectTransform r = b.GetComponent<RectTransform>(); LayoutElement le = r.GetComponent<LayoutElement>(); if (le != null) UnityEngine.Object.DestroyImmediate(le);
            r.anchorMin = r.anchorMax = new Vector2(1f, 0.5f); r.pivot = new Vector2(1f, 0.5f); r.anchoredPosition = new Vector2(right, 0f); r.sizeDelta = new Vector2(28f, 24f);
        }
    }
}
