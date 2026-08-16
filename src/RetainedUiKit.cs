using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ErenshorCraftingExpanded
{
    internal static class RetainedUiKit
    {
        internal static readonly Color Panel = new Color(0.015f, 0.09f, 0.125f, 0.96f);
        internal static readonly Color Header = new Color(0.025f, 0.14f, 0.18f, 0.98f);
        internal static readonly Color Button = new Color(0.035f, 0.19f, 0.24f, 0.98f);
        internal static readonly Color ButtonHover = new Color(0.08f, 0.32f, 0.39f, 1f);
        internal static readonly Color Selected = new Color(0.06f, 0.28f, 0.34f, 1f);
        internal static readonly Color Danger = new Color(0.30f, 0.17f, 0.08f, 0.98f);
        internal static readonly Color TextBack = new Color(0.012f, 0.05f, 0.065f, 0.98f);
        internal static readonly Color Edge = new Color(0.03f, 0.67f, 0.86f, 0.96f);
        internal static readonly Color Text = new Color(0.90f, 0.96f, 0.98f, 1f);
        internal static readonly Color Muted = new Color(0.62f, 0.76f, 0.80f, 1f);

        internal static GameObject CreateCanvas(string name, int sortingOrder)
        {
            GameObject root = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            CanvasScaler scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = 1f;
            scaler.referencePixelsPerUnit = 100f;
            UnityEngine.Object.DontDestroyOnLoad(root);
            return root;
        }

        internal static RectTransform CreateRect(string name, Transform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.GetComponent<RectTransform>();
        }

        internal static Image AddImage(RectTransform rect, Color color)
        {
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = true;
            return image;
        }

        internal static void Stretch(RectTransform rect, float left, float bottom, float right, float top)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }

        internal static void AnchorBottomLeft(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = Vector2.zero;
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(width, height);
        }

        internal static void AnchorTopStretch(RectTransform rect, float left, float top, float right, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(left, -top - height);
            rect.offsetMax = new Vector2(-right, -top);
        }

        internal static TextMeshProUGUI AddLabel(string name, Transform parent, string value, float fontSize, FontStyles style, TextAlignmentOptions alignment)
        {
            RectTransform rect = CreateRect(name, parent);
            TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.text = value ?? string.Empty;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.color = Text;
            text.alignment = alignment;
            text.enableWordWrapping = true;
            text.raycastTarget = false;
            return text;
        }

        internal static Button AddButton(string name, Transform parent, string label, Action onClick, float width, float height, bool danger)
        {
            RectTransform rect = CreateRect(name, parent);
            Image image = AddImage(rect, danger ? Danger : Button);
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.normalColor = danger ? Danger : Button;
            colors.highlightedColor = ButtonHover;
            colors.pressedColor = Selected;
            colors.selectedColor = Selected;
            button.colors = colors;
            LayoutElement le = rect.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = width;
            le.preferredHeight = height;
            le.minHeight = height;
            if (onClick != null) button.onClick.AddListener(delegate { onClick(); });

            TextMeshProUGUI text = AddLabel("Label", rect, label, 11f, FontStyles.Normal, TextAlignmentOptions.Center);
            Stretch(text.rectTransform, 4f, 1f, 4f, 1f);
            return button;
        }

        internal static RectTransform AddHorizontalRow(string name, Transform parent, float height, float spacing)
        {
            RectTransform rect = CreateRect(name, parent);
            HorizontalLayoutGroup layout = rect.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(0, 0, 0, 0);
            layout.spacing = spacing;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = false;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;
            LayoutElement le = rect.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = height;
            le.minHeight = height;
            return rect;
        }

        internal static RectTransform AddVerticalContent(string name, Transform parent, float spacing, int padding)
        {
            RectTransform rect = CreateRect(name, parent);
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
            VerticalLayoutGroup layout = rect.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(padding, padding, padding, padding);
            layout.spacing = spacing;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            ContentSizeFitter fitter = rect.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return rect;
        }

        internal static ScrollRect AddScrollRect(string name, Transform parent, bool horizontal, bool vertical, out RectTransform viewport, out RectTransform content)
        {
            RectTransform scrollRect = CreateRect(name, parent);
            ScrollRect scroll = scrollRect.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = horizontal;
            scroll.vertical = vertical;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 24f;

            viewport = CreateRect("Viewport", scrollRect);
            AddImage(viewport, new Color(0f, 0f, 0f, 0.02f));
            viewport.gameObject.AddComponent<RectMask2D>();
            Stretch(viewport, 0f, 0f, 0f, 0f);

            content = CreateRect("Content", viewport);
            if (vertical)
            {
                content.anchorMin = new Vector2(0f, 1f);
                content.anchorMax = new Vector2(1f, 1f);
                content.pivot = new Vector2(0.5f, 1f);
            }
            else
            {
                content.anchorMin = new Vector2(0f, 0f);
                content.anchorMax = new Vector2(0f, 1f);
                content.pivot = new Vector2(0f, 0.5f);
            }
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;
            scroll.viewport = viewport;
            scroll.content = content;
            return scroll;
        }

        internal static TMP_InputField AddInputField(string name, Transform parent, string value, bool multiline, int charLimit)
        {
            RectTransform root = CreateRect(name, parent);
            Image back = AddImage(root, TextBack);

            RectTransform viewport = CreateRect("Text Area", root);
            viewport.gameObject.AddComponent<RectMask2D>();
            Stretch(viewport, 7f, 5f, 7f, 5f);

            TextMeshProUGUI text = AddLabel("Text", viewport, value, 12f, FontStyles.Normal,
                multiline ? TextAlignmentOptions.TopLeft : TextAlignmentOptions.Left);
            Stretch(text.rectTransform, 0f, 0f, 0f, 0f);
            text.raycastTarget = false;

            TextMeshProUGUI placeholder = AddLabel("Placeholder", viewport, string.Empty, 12f, FontStyles.Italic,
                multiline ? TextAlignmentOptions.TopLeft : TextAlignmentOptions.Left);
            placeholder.color = Muted;
            Stretch(placeholder.rectTransform, 0f, 0f, 0f, 0f);

            TMP_InputField input = root.gameObject.AddComponent<TMP_InputField>();
            input.targetGraphic = back;
            input.textViewport = viewport;
            input.textComponent = text;
            input.placeholder = placeholder;
            input.text = value ?? string.Empty;
            input.characterLimit = charLimit;
            input.lineType = multiline ? TMP_InputField.LineType.MultiLineNewline : TMP_InputField.LineType.SingleLine;
            input.richText = false;
            return input;
        }

        internal static SuiteDragHandler AddDragSurface(string name, Transform parent, RectTransform target, float rightExclusion, Action onCompleted)
        {
            RectTransform rect = CreateRect(name, parent);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = new Vector2(-Mathf.Max(0f, rightExclusion), 0f);
            Image hit = AddImage(rect, Color.clear);
            hit.raycastTarget = true;
            SuiteDragHandler drag = rect.gameObject.AddComponent<SuiteDragHandler>();
            drag.Target = target;
            drag.OnDragCompleted = onCompleted;
            return drag;
        }

        internal static SuiteResizeHandler AddResizeGrip(string name, Transform parent, RectTransform target,
            float size, Vector2 minimumSize, Action<float, float> onCompleted)
        {
            RectTransform rect = CreateRect(name, parent);
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(1f, 0f);
            rect.anchoredPosition = new Vector2(-2f, 2f);
            rect.sizeDelta = new Vector2(size, size);
            Image hit = AddImage(rect, new Color(Edge.r, Edge.g, Edge.b, 0.65f));
            hit.raycastTarget = true;
            TextMeshProUGUI mark = AddLabel("Mark", rect, "↗", 10f, FontStyles.Bold, TextAlignmentOptions.Center);
            Stretch(mark.rectTransform, 0f, 0f, 0f, 0f);

            SuiteResizeHandler resize = rect.gameObject.AddComponent<SuiteResizeHandler>();
            resize.Target = target;
            resize.MinimumSize = minimumSize;
            resize.OnResizeCompleted = onCompleted;
            return resize;
        }

        internal static void ClearChildren(RectTransform parent)
        {
            if (parent == null) return;
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                Transform child = parent.GetChild(i);
                if (child != null) UnityEngine.Object.DestroyImmediate(child.gameObject);
            }
        }

        internal static void DestroyRoot(ref GameObject root)
        {
            if (root == null) return;
            UnityEngine.Object.DestroyImmediate(root);
            root = null;
        }
    }

    // Shared ownership for Crafting-owned retained drag/resize gestures. The native flag value
    // present before the first Crafting owner is restored when the last owner releases; this avoids
    // clearing ownership established by vanilla or another mod. While Crafting owns the gesture the
    // flag is monotonically reasserted because camera input is evaluated every frame.
    internal static class CraftingUiPointerOwnership
    {
        private const string GlobalOwnersKey = "forgetwhtuno.erenshor.ui.drag.owners.v1";
        private const string GlobalBaselineKey = "forgetwhtuno.erenshor.ui.drag.nativeBaseline.v1";
        private const string GlobalBaselineCapturedKey = "forgetwhtuno.erenshor.ui.drag.nativeBaselineCaptured.v1";
        private const string PluginOwnerKey = "forgetwhtuno.erenshor.crafting";

        private static readonly HashSet<object> Owners = new HashSet<object>();
        private static bool _localFallbackBaseline;
        private static bool _localFallbackCaptured;
        private static bool _usingGlobalCoordination;

        internal static bool HasOwners { get { return Owners.Count > 0; } }

        internal static void Acquire(object owner)
        {
            if (owner == null || Owners.Contains(owner)) return;
            bool firstLocalOwner = Owners.Count == 0;
            Owners.Add(owner);
            if (firstLocalOwner) AcquireProcessOwner();
            Reassert();
        }

        internal static void Reassert()
        {
            if (Owners.Count == 0) return;
            try { if (!GameData.DraggingUIElement) GameData.DraggingUIElement = true; } catch { }
        }

        internal static void Release(object owner)
        {
            if (owner == null || !Owners.Remove(owner)) return;
            if (Owners.Count == 0) ReleaseProcessOwner();
            else Reassert();
        }

        internal static void ForceRestoreIfEmpty()
        {
            if (Owners.Count == 0) ReleaseProcessOwner();
        }

        private static void AcquireProcessOwner()
        {
            HashSet<string> processOwners;
            if (TryGetProcessOwners(out processOwners))
            {
                lock (processOwners)
                {
                    bool firstProcessOwner = processOwners.Count == 0;
                    if (firstProcessOwner)
                    {
                        bool baseline = false;
                        try { baseline = GameData.DraggingUIElement; } catch { }
                        AppDomain.CurrentDomain.SetData(GlobalBaselineKey, baseline);
                        AppDomain.CurrentDomain.SetData(GlobalBaselineCapturedKey, true);
                    }
                    processOwners.Add(PluginOwnerKey);
                    _usingGlobalCoordination = true;
                    try { GameData.DraggingUIElement = true; } catch { }
                }
                return;
            }

            // A malformed process-local coordination slot is safer to leave untouched than to
            // replace behind another plugin. Fall back to restoring only the native value that
            // Crafting observed before its own first gesture.
            try { _localFallbackBaseline = GameData.DraggingUIElement; }
            catch { _localFallbackBaseline = false; }
            _localFallbackCaptured = true;
            _usingGlobalCoordination = false;
            try { GameData.DraggingUIElement = true; } catch { }
        }

        private static void ReleaseProcessOwner()
        {
            if (_usingGlobalCoordination)
            {
                HashSet<string> processOwners;
                if (TryGetExistingProcessOwners(out processOwners))
                {
                    lock (processOwners)
                    {
                        processOwners.Remove(PluginOwnerKey);
                        if (processOwners.Count > 0)
                        {
                            try { GameData.DraggingUIElement = true; } catch { }
                        }
                        else
                        {
                            object captured = AppDomain.CurrentDomain.GetData(GlobalBaselineCapturedKey);
                            bool shouldRestore = captured is bool && (bool)captured;
                            object baseline = AppDomain.CurrentDomain.GetData(GlobalBaselineKey);
                            bool value = baseline is bool && (bool)baseline;
                            if (shouldRestore)
                            {
                                try { GameData.DraggingUIElement = value; } catch { }
                            }
                            AppDomain.CurrentDomain.SetData(GlobalBaselineCapturedKey, false);
                            AppDomain.CurrentDomain.SetData(GlobalBaselineKey, false);
                        }
                    }
                }
                _usingGlobalCoordination = false;
            }

            if (_localFallbackCaptured)
            {
                try { GameData.DraggingUIElement = _localFallbackBaseline; } catch { }
                _localFallbackBaseline = false;
                _localFallbackCaptured = false;
            }
        }

        private static bool TryGetProcessOwners(out HashSet<string> owners)
        {
            owners = null;
            try
            {
                object existing = AppDomain.CurrentDomain.GetData(GlobalOwnersKey);
                if (existing == null)
                {
                    owners = new HashSet<string>(StringComparer.Ordinal);
                    AppDomain.CurrentDomain.SetData(GlobalOwnersKey, owners);
                    return true;
                }
                owners = existing as HashSet<string>;
                return owners != null;
            }
            catch { owners = null; return false; }
        }

        private static bool TryGetExistingProcessOwners(out HashSet<string> owners)
        {
            owners = null;
            try
            {
                owners = AppDomain.CurrentDomain.GetData(GlobalOwnersKey) as HashSet<string>;
                return owners != null;
            }
            catch { return false; }
        }
    }

    internal sealed class SuiteDragHandler : MonoBehaviour,
        IPointerDownHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerUpHandler
    {
        private static readonly HashSet<SuiteDragHandler> _owners = new HashSet<SuiteDragHandler>();
        internal static bool HasOwners { get { return _owners.Count > 0; } }
        internal RectTransform Target;
        internal Action OnDragCompleted;

        private RectTransform _parentRect;
        private Vector2 _startPointer;
        private Vector2 _startPosition;
        private readonly CraftingPointerOwnershipState _gesture = new CraftingPointerOwnershipState();

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData == null || eventData.button != PointerEventData.InputButton.Left) return;
            Acquire();
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (eventData == null || eventData.button != PointerEventData.InputButton.Left) return;
            try
            {
                if (Target == null) Target = GetComponent<RectTransform>();
                if (Target == null) { EndDrag(false); return; }
                Acquire();
                _gesture.BeginDrag();
                _parentRect = Target.parent as RectTransform;
                if (_parentRect == null) { EndDrag(false); return; }
                Vector2 local;
                if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_parentRect, eventData.position, eventData.pressEventCamera, out local))
                { EndDrag(false); return; }
                _startPointer = local;
                _startPosition = Target.anchoredPosition;
                CraftingUiPointerOwnership.Reassert();
            }
            catch { EndDrag(false); }
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!_gesture.IsDragging || eventData == null || Target == null || _parentRect == null) return;
            try
            {
                CraftingUiPointerOwnership.Reassert();
                Vector2 local;
                if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_parentRect, eventData.position, eventData.pressEventCamera, out local)) return;
                Vector2 next = _startPosition + (local - _startPointer);
                Rect pr = _parentRect.rect;
                Rect tr = Target.rect;
                next.x = Mathf.Clamp(next.x, 0f, Mathf.Max(0f, pr.width - tr.width));
                next.y = Mathf.Clamp(next.y, 0f, Mathf.Max(0f, pr.height - tr.height));
                Target.anchoredPosition = next;
            }
            catch { EndDrag(false); }
        }

        private void Update()
        {
            if (!_gesture.OwnsPointer) return;
            CraftingUiPointerOwnership.Reassert();
            try { if (!Input.GetMouseButton(0)) EndDrag(false); } catch { }
        }

        private void OnApplicationFocus(bool focused) { if (!focused) EndDrag(false); }
        private void OnApplicationPause(bool paused) { if (paused) EndDrag(false); }
        public void OnEndDrag(PointerEventData eventData) { if (eventData == null || eventData.button == PointerEventData.InputButton.Left) EndDrag(true); }
        public void OnPointerUp(PointerEventData eventData) { if (eventData == null || eventData.button == PointerEventData.InputButton.Left) EndDrag(false); }
        private void OnDisable() { EndDrag(false); }
        private void OnDestroy() { EndDrag(false); }

        private void Acquire()
        {
            if (!_gesture.PointerDown(true)) return;
            _owners.Add(this);
            CraftingUiPointerOwnership.Acquire(this);
        }

        private void EndDrag(bool notify)
        {
            bool completed = _gesture.IsDragging;
            if (_gesture.Release())
            {
                _owners.Remove(this);
                CraftingUiPointerOwnership.Release(this);
            }
            _parentRect = null;
            if (notify && completed && OnDragCompleted != null)
            {
                try { OnDragCompleted(); } catch { }
            }
        }

        private void ForceReleaseLocal()
        {
            _gesture.Release();
            _owners.Remove(this);
            CraftingUiPointerOwnership.Release(this);
            _parentRect = null;
        }

        internal static void ForceReleaseIfOwned()
        {
            if (_owners.Count > 0)
            {
                SuiteDragHandler[] owners = new SuiteDragHandler[_owners.Count];
                _owners.CopyTo(owners);
                for (int i = 0; i < owners.Length; i++)
                {
                    SuiteDragHandler owner = owners[i];
                    if (owner != null) owner.ForceReleaseLocal();
                    else CraftingUiPointerOwnership.Release((object)owner);
                }
                _owners.Clear();
            }
            SuiteResizeHandler.ForceReleaseIfOwned();
            CraftingUiPointerOwnership.ForceRestoreIfEmpty();
        }
    }

    internal sealed class SuiteResizeHandler : MonoBehaviour,
        IPointerDownHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerUpHandler
    {
        private static readonly HashSet<SuiteResizeHandler> _owners = new HashSet<SuiteResizeHandler>();
        internal static bool HasOwners { get { return _owners.Count > 0; } }
        internal RectTransform Target;
        internal Vector2 MinimumSize = new Vector2(320f, 240f);
        internal Action<float, float> OnResizeCompleted;

        private RectTransform _parentRect;
        private Vector2 _startPointer;
        private Vector2 _startSize;
        private readonly CraftingPointerOwnershipState _gesture = new CraftingPointerOwnershipState();

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData == null || eventData.button != PointerEventData.InputButton.Left) return;
            Acquire();
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (eventData == null || eventData.button != PointerEventData.InputButton.Left) return;
            try
            {
                if (Target == null) { EndResize(false); return; }
                Acquire();
                _gesture.BeginDrag();
                _parentRect = Target.parent as RectTransform;
                if (_parentRect == null) { EndResize(false); return; }
                Vector2 local;
                if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_parentRect, eventData.position, eventData.pressEventCamera, out local))
                { EndResize(false); return; }
                _startPointer = local;
                _startSize = Target.rect.size;
                CraftingUiPointerOwnership.Reassert();
            }
            catch { EndResize(false); }
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!_gesture.IsDragging || eventData == null || Target == null || _parentRect == null) return;
            try
            {
                CraftingUiPointerOwnership.Reassert();
                Vector2 local;
                if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_parentRect, eventData.position, eventData.pressEventCamera, out local)) return;
                Vector2 delta = local - _startPointer;
                Rect parentRect = _parentRect.rect;
                float maxWidth = Mathf.Max(120f, parentRect.width - Target.anchoredPosition.x);
                float maxHeight = Mathf.Max(120f, parentRect.height - Target.anchoredPosition.y);
                float minWidth = Mathf.Min(Mathf.Max(120f, MinimumSize.x), maxWidth);
                float minHeight = Mathf.Min(Mathf.Max(120f, MinimumSize.y), maxHeight);
                float width = Mathf.Clamp(_startSize.x + delta.x, minWidth, maxWidth);
                float height = Mathf.Clamp(_startSize.y + delta.y, minHeight, maxHeight);
                Target.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
                Target.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
            }
            catch { EndResize(false); }
        }

        private void Update()
        {
            if (!_gesture.OwnsPointer) return;
            CraftingUiPointerOwnership.Reassert();
            try { if (!Input.GetMouseButton(0)) EndResize(false); } catch { }
        }

        private void OnApplicationFocus(bool focused) { if (!focused) EndResize(false); }
        private void OnApplicationPause(bool paused) { if (paused) EndResize(false); }
        public void OnEndDrag(PointerEventData eventData) { if (eventData == null || eventData.button == PointerEventData.InputButton.Left) EndResize(true); }
        public void OnPointerUp(PointerEventData eventData) { if (eventData == null || eventData.button == PointerEventData.InputButton.Left) EndResize(false); }
        private void OnDisable() { EndResize(false); }
        private void OnDestroy() { EndResize(false); }

        private void Acquire()
        {
            if (!_gesture.PointerDown(true)) return;
            _owners.Add(this);
            CraftingUiPointerOwnership.Acquire(this);
        }

        private void EndResize(bool notify)
        {
            bool completed = _gesture.IsDragging;
            if (_gesture.Release())
            {
                _owners.Remove(this);
                CraftingUiPointerOwnership.Release(this);
            }
            _parentRect = null;
            if (notify && completed && Target != null)
            {
                try { LayoutRebuilder.ForceRebuildLayoutImmediate(Target); } catch { }
                if (OnResizeCompleted != null) { try { OnResizeCompleted(Target.rect.width, Target.rect.height); } catch { } }
            }
        }

        private void ForceReleaseLocal()
        {
            _gesture.Release();
            _owners.Remove(this);
            CraftingUiPointerOwnership.Release(this);
            _parentRect = null;
        }

        internal static void ForceReleaseIfOwned()
        {
            if (_owners.Count == 0) return;
            SuiteResizeHandler[] owners = new SuiteResizeHandler[_owners.Count];
            _owners.CopyTo(owners);
            for (int i = 0; i < owners.Length; i++)
            {
                SuiteResizeHandler owner = owners[i];
                if (owner != null) owner.ForceReleaseLocal();
                else CraftingUiPointerOwnership.Release((object)owner);
            }
            _owners.Clear();
        }
    }

    internal sealed class RetainedPosition
    {
        private readonly float _defaultX;
        private readonly float _defaultY;
        private readonly Action<float, float> _persist;
        private float _storedX;
        private float _storedY;
        private int _lastWidth = -1;
        private int _lastHeight = -1;

        internal RetainedPosition(float storedX, float storedY, float defaultX, float defaultY, Action<float, float> persist)
        {
            _storedX = SuiteUiPositionPolicy.InterpretStoredAxis(storedX);
            _storedY = SuiteUiPositionPolicy.InterpretStoredAxis(storedY);
            _defaultX = defaultX;
            _defaultY = defaultY;
            _persist = persist;
        }

        internal void Resolve(RectTransform target)
        {
            if (target == null) return;
            if (_lastWidth == Screen.width && _lastHeight == Screen.height) return;
            _lastWidth = Screen.width;
            _lastHeight = Screen.height;
            target.anchoredPosition = new Vector2(
                SuiteUiPositionPolicy.ResolveAxis(_storedX, _defaultX, Screen.width, target.rect.width),
                SuiteUiPositionPolicy.ResolveAxis(_storedY, _defaultY, Screen.height, target.rect.height));
        }

        internal void DragCompleted(RectTransform target)
        {
            if (target == null) return;
            Vector2 current = target.anchoredPosition;
            if (!SuiteUiPositionPolicy.IsFinite(current.x) || !SuiteUiPositionPolicy.IsFinite(current.y))
            {
                _lastWidth = -1;
                _lastHeight = -1;
                Resolve(target);
                return;
            }
            Clamp(target);
            _storedX = SuiteUiPositionPolicy.NormalizeAxis(target.anchoredPosition.x, Screen.width);
            _storedY = SuiteUiPositionPolicy.NormalizeAxis(target.anchoredPosition.y, Screen.height);
            _lastWidth = Screen.width;
            _lastHeight = Screen.height;
            if (_persist != null) _persist(_storedX, _storedY);
        }

        internal void Reset(RectTransform target)
        {
            _storedX = SuiteUiPositionPolicy.Unset;
            _storedY = SuiteUiPositionPolicy.Unset;
            _lastWidth = -1;
            _lastHeight = -1;
            Resolve(target);
            if (_persist != null) _persist(_storedX, _storedY);
        }

        internal void Clamp(RectTransform target)
        {
            if (target == null) return;
            Vector2 p = target.anchoredPosition;
            if (!SuiteUiPositionPolicy.IsFinite(p.x) || !SuiteUiPositionPolicy.IsFinite(p.y))
            {
                _lastWidth = -1;
                _lastHeight = -1;
                Resolve(target);
                return;
            }
            p.x = Mathf.Clamp(p.x, 0f, Mathf.Max(0f, Screen.width - target.rect.width));
            p.y = Mathf.Clamp(p.y, 0f, Mathf.Max(0f, Screen.height - target.rect.height));
            target.anchoredPosition = p;
        }
    }
}
