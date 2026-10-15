using System.Collections.Generic;
using Abyss.Runtime.Localization;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Abyss.Runtime.UI
{
    public sealed partial class BuildContextPanel
    {
        private static readonly HashSet<BuildContextPanel> activeReadingPanels = new();
        private ScrollRect readingScroll;
        private Selectable readingFocus;
        private Scrollbar readingBar;
        private Text readingHint;
        private RectTransform readingPanel;
        private int titleFont;
        private int synergyFont;
        private int listFont;
        private int hintFont = 14;
        private Vector2 titleSize;
        private Vector2 titlePosition;
        private float titleTop;
        private float contentGap;
        private float bottomPadding;
        private float contentLeft;
        private float contentRight;
        private float lastReadingScale = -1f;
        private float lastCanvasScale = -1f;
        private Vector2 lastPanelSize;
        private LocalizationLanguage lastReadingLanguage;
        private string lastSynergy;
        private string lastList;
        private string lastTitle;
        private bool readingDirty;
        private bool resetReadingScroll;

        public Selectable ReadingTarget => readingFocus;

        /// <summary>상세 석판이 같은 Page/스틱 입력을 함께 소비하지 않도록 읽기 영역이 소유권을 알린다.</summary>
        public static bool IsReadingInputOwned
        {
            get
            {
                foreach (var panel in activeReadingPanels)
                    if (panel != null && panel.isActiveAndEnabled && panel.readingScroll != null &&
                        panel.readingScroll.gameObject.activeInHierarchy && (panel.HasReadingFocus() || panel.IsPointerOverReadingPanel())) return true;
                return false;
            }
        }

        /// <summary>스킨 적용 뒤 기준 폰트와 패널 여백을 한 번만 보존한다.</summary>
        public void AttachReadability()
        {
            if (readingScroll != null) return;
            readingPanel = transform as RectTransform;
            if (readingPanel == null || titleText == null || synergyCountsText == null || skillListText == null) return;
            titleFont = titleText.fontSize;
            synergyFont = synergyCountsText.fontSize;
            listFont = skillListText.fontSize;
            titleSize = titleText.rectTransform.sizeDelta;
            titlePosition = titleText.rectTransform.anchoredPosition;
            var corners = new Vector3[4];
            titleText.rectTransform.GetWorldCorners(corners);
            titleTop = readingPanel.rect.yMax - readingPanel.InverseTransformPoint(corners[1]).y;
            synergyCountsText.rectTransform.GetWorldCorners(corners);
            float synergyTop = readingPanel.rect.yMax - readingPanel.InverseTransformPoint(corners[1]).y;
            contentGap = Mathf.Max(4f, synergyTop - titleTop - titleText.rectTransform.rect.height);
            skillListText.rectTransform.GetWorldCorners(corners);
            bottomPadding = Mathf.Max(12f, readingPanel.InverseTransformPoint(corners[0]).y - readingPanel.rect.yMin);
            contentLeft = Mathf.Max(12f, readingPanel.InverseTransformPoint(corners[0]).x - readingPanel.rect.xMin);
            contentRight = Mathf.Max(12f, readingPanel.rect.xMax - readingPanel.InverseTransformPoint(corners[2]).x);

            var topLeft = new Vector2(0f, 1f);
            var viewport = UiFactory.CreateRect(transform, "BuildReadingViewport", topLeft, topLeft, topLeft, Vector2.zero, Vector2.one);
            var surface = viewport.AddComponent<Image>();
            surface.color = new Color(0.22f, 0.18f, 0.35f, 1f);
            surface.raycastTarget = true;
            viewport.AddComponent<RectMask2D>();
            readingScroll = viewport.AddComponent<ScrollRect>();
            readingScroll.horizontal = false;
            readingScroll.vertical = true;
            readingScroll.inertia = false;
            readingScroll.scrollSensitivity = 0f;
            readingScroll.movementType = ScrollRect.MovementType.Clamped;
            readingScroll.viewport = (RectTransform)viewport.transform;
            var content = UiFactory.CreateRect(viewport.transform, "Content", topLeft, topLeft, topLeft, Vector2.zero, Vector2.one);
            readingScroll.content = (RectTransform)content.transform;
            readingFocus = viewport.AddComponent<Selectable>();
            readingFocus.targetGraphic = surface;
            readingFocus.navigation = new Navigation { mode = Navigation.Mode.Automatic };
            var colors = ColorBlock.defaultColorBlock;
            colors.normalColor = new Color(1f, 1f, 1f, 0.02f);
            colors.highlightedColor = new Color(1f, 1f, 1f, 0.3f);
            colors.selectedColor = new Color(1f, 1f, 1f, 0.75f);
            colors.pressedColor = new Color(1f, 1f, 1f, 0.8f);
            colors.fadeDuration = 0.08f;
            readingFocus.colors = colors;
            var outline = viewport.AddComponent<Outline>();
            outline.effectColor = new Color(0.85f, 0.8f, 1f, 0.65f);
            outline.effectDistance = new Vector2(2f, -2f);
            synergyCountsText.transform.SetParent(content.transform, false);
            skillListText.transform.SetParent(content.transform, false);
            foreach (var text in new[] { synergyCountsText, skillListText })
            {
                text.rectTransform.anchorMin = text.rectTransform.anchorMax = topLeft;
                text.rectTransform.pivot = topLeft;
                text.raycastTarget = false;
            }
            var hintObject = UiFactory.CreateRect(transform, "BuildReadingHint", topLeft, topLeft, topLeft, Vector2.zero, Vector2.one);
            readingHint = hintObject.AddComponent<Text>();
            UiFactory.ApplyFont(readingHint);
            readingHint.fontSize = hintFont;
            readingHint.color = ModalArtSkin.SubTextColor;
            readingHint.alignment = TextAnchor.MiddleCenter;
            readingHint.horizontalOverflow = HorizontalWrapMode.Wrap;
            readingHint.raycastTarget = false;
            readingHint.supportRichText = false;

            var barObject = UiFactory.CreateRect(viewport.transform, "Scrollbar", new Vector2(1f, 0f), Vector2.one,
                new Vector2(1f, 0.5f), Vector2.zero, new Vector2(8f, 0f));
            var track = barObject.AddComponent<Image>();
            track.color = new Color(1f, 1f, 1f, 0.12f);
            var handleObject = UiFactory.CreateRect(barObject.transform, "Handle", Vector2.zero, Vector2.one,
                new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            var handle = handleObject.AddComponent<Image>();
            handle.color = new Color(0.85f, 0.82f, 1f, 0.7f);
            readingBar = barObject.AddComponent<Scrollbar>();
            readingBar.direction = Scrollbar.Direction.BottomToTop;
            readingBar.handleRect = (RectTransform)handleObject.transform;
            readingBar.targetGraphic = handle;
            readingBar.navigation = new Navigation { mode = Navigation.Mode.None };
            readingScroll.verticalScrollbar = readingBar;
            if (isActiveAndEnabled) activeReadingPanels.Add(this);
            RefreshReadingContent();
            ApplyReadingLayout();
        }

        private void RefreshReadingContent()
        {
            if (readingScroll == null) return;
            bool changed = lastSynergy != synergyCountsText.text || lastList != skillListText.text;
            if (changed) { resetReadingScroll = true; readingDirty = true; }
            if (lastTitle != titleText.text) readingDirty = true;
        }

        private void ApplyReadingLayout()
        {
            float previous = readingScroll.verticalNormalizedPosition;
            float scale = MenuTextReadability.Scale;
            titleText.fontSize = Mathf.RoundToInt(titleFont * scale);
            synergyCountsText.fontSize = Mathf.RoundToInt(synergyFont * scale);
            skillListText.fontSize = Mathf.RoundToInt(listFont * scale);
            readingHint.fontSize = Mathf.RoundToInt(hintFont * scale);
            foreach (var text in new[] { titleText, synergyCountsText, skillListText, readingHint })
            {
                text.resizeTextForBestFit = false;
                text.horizontalOverflow = HorizontalWrapMode.Wrap;
                text.verticalOverflow = VerticalWrapMode.Overflow;
            }
            titleText.rectTransform.sizeDelta = titleSize;
            titleText.rectTransform.sizeDelta = new Vector2(titleSize.x, Mathf.Max(titleSize.y, titleText.preferredHeight));
            titleText.rectTransform.anchoredPosition = titlePosition;
            float top = titleTop + titleText.rectTransform.sizeDelta.y + contentGap;
            float width = Mathf.Max(1f, readingPanel.rect.width - contentLeft - contentRight);
            float textWidth = Mathf.Max(1f, width - 16f);
            synergyCountsText.rectTransform.sizeDelta = new Vector2(textWidth, 0f);
            skillListText.rectTransform.sizeDelta = new Vector2(textWidth, 0f);
            float synergyHeight = Mathf.Ceil(synergyCountsText.preferredHeight);
            float listHeight = Mathf.Ceil(skillListText.preferredHeight);
            float fullHeight = synergyHeight + 12f + listHeight;
            float available = Mathf.Max(1f, readingPanel.rect.height - top - bottomPadding);
            bool overflowing = fullHeight > available;
            readingHint.text = Loc.CurrentLanguage switch
            {
                LocalizationLanguage.Korean => "휠 · Page Up/Down\n패드 오른쪽 스틱",
                LocalizationLanguage.Japanese => "ホイール · Page Up/Down\n右スティック",
                _ => "Wheel · Page Up/Down\nRight stick"
            };
            readingHint.rectTransform.sizeDelta = new Vector2(width, 0f);
            float hintHeight = overflowing ? Mathf.Ceil(readingHint.preferredHeight) : 0f;
            float viewportHeight = Mathf.Max(1f, available - (overflowing ? hintHeight + 6f : 0f));
            readingHint.gameObject.SetActive(overflowing);
            readingHint.rectTransform.sizeDelta = new Vector2(width, hintHeight);
            readingHint.rectTransform.anchoredPosition = new Vector2(contentLeft, -readingPanel.rect.height + bottomPadding + hintHeight);
            readingScroll.viewport.anchoredPosition = new Vector2(contentLeft, -top);
            readingScroll.viewport.sizeDelta = new Vector2(width, viewportHeight);
            readingScroll.content.sizeDelta = new Vector2(textWidth, Mathf.Max(viewportHeight, fullHeight));
            synergyCountsText.rectTransform.anchoredPosition = Vector2.zero;
            synergyCountsText.rectTransform.sizeDelta = new Vector2(textWidth, synergyHeight);
            skillListText.rectTransform.anchoredPosition = new Vector2(0f, -synergyHeight - 12f);
            skillListText.rectTransform.sizeDelta = new Vector2(textWidth, listHeight);
            readingBar.gameObject.SetActive(overflowing);
            readingScroll.StopMovement();
            readingScroll.verticalNormalizedPosition = resetReadingScroll ? 1f : previous;
            resetReadingScroll = false;
            readingDirty = false;
            lastReadingScale = scale;
            var canvas = GetComponentInParent<Canvas>();
            lastCanvasScale = canvas != null ? canvas.scaleFactor : 1f;
            lastPanelSize = readingPanel.rect.size;
            lastSynergy = synergyCountsText.text;
            lastList = skillListText.text;
            lastTitle = titleText.text;
            lastReadingLanguage = Loc.CurrentLanguage;
        }

        private bool HasReadingFocus()
        {
            var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            return selected != null && readingScroll != null &&
                (selected.transform == readingScroll.transform || selected.transform.IsChildOf(readingScroll.transform));
        }

        private bool IsPointerOverReadingPanel()
        {
            if (readingPanel == null || Mouse.current == null) return false;
            var canvas = GetComponentInParent<Canvas>();
            UnityEngine.Camera camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            return RectTransformUtility.RectangleContainsScreenPoint(readingPanel, Mouse.current.position.ReadValue(), camera);
        }

        private void LateUpdate()
        {
            if (readingScroll == null) return;
            RefreshReadingContent();
            var canvas = GetComponentInParent<Canvas>();
            if (readingDirty || lastReadingScale != MenuTextReadability.Scale || lastReadingLanguage != Loc.CurrentLanguage ||
                lastPanelSize != readingPanel.rect.size || lastCanvasScale != (canvas != null ? canvas.scaleFactor : 1f)) ApplyReadingLayout();
            if (SaveStatusOverlay.IsCapturingInput || SettingsPanel.IsOpen || SettingsPanel.WasClosedThisFrame ||
                CodexPanel.IsOpen || CodexPanel.WasClosedThisFrame || (!HasReadingFocus() && !IsPointerOverReadingPanel())) return;
            float range = readingScroll.content.rect.height - readingScroll.viewport.rect.height;
            if (range <= 0f) return;
            float pixels = IsPointerOverReadingPanel() && Mouse.current != null ? Mouse.current.scroll.ReadValue().y * 0.5f : 0f;
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.pageUpKey.isPressed) pixels += 420f * Time.unscaledDeltaTime;
                if (keyboard.pageDownKey.isPressed) pixels -= 420f * Time.unscaledDeltaTime;
                if (keyboard.homeKey.wasPressedThisFrame) readingScroll.verticalNormalizedPosition = 1f;
                if (keyboard.endKey.wasPressedThisFrame) readingScroll.verticalNormalizedPosition = 0f;
            }
            if (Gamepad.current != null) pixels += Gamepad.current.rightStick.ReadValue().y * 420f * Time.unscaledDeltaTime;
            if (pixels != 0f) readingScroll.verticalNormalizedPosition = Mathf.Clamp01(readingScroll.verticalNormalizedPosition + pixels / range);
        }

        private void OnEnable() { if (readingScroll != null) activeReadingPanels.Add(this); }
        private void OnDisable() => activeReadingPanels.Remove(this);
        private void OnDestroy() => activeReadingPanels.Remove(this);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetReadingStatics() => activeReadingPanels.Clear();
    }
}
