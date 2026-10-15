using System.Collections.Generic;
using Abyss.Runtime.ArtIntegration;
using Abyss.Runtime.Localization;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Abyss.Runtime.UI
{
    /// <summary>결과 제목·버튼은 고정하고 긴 통계만 스크롤한다. 기존 참조와 입력은 유지한다.</summary>
    public sealed class ResultPanelReadability : MonoBehaviour
    {
        private sealed class Entry
        {
            public Text Text;
            public Transform Parent;
            public Vector2 AnchorMin, AnchorMax, Pivot, Position, Size;
            public int Font;
        }
        private readonly List<Entry> stats = new();
        private readonly List<Entry> buttonLabels = new();
        private Text title;
        private Text shards;
        private RectTransform panel;
        private RectTransform area;
        private Vector2 panelSize;
        private Vector2 titleSize;
        private Vector2 titlePosition;
        private Button[] buttons;
        private Vector2[] buttonPositions;
        private Vector2[] buttonPivots;
        private ScrollRect readingScroll;
        private Scrollbar readingBar;
        private Text readingHint;
        private float lastScale = -1f;
        private float lastCanvasScale = -1f;
        private Vector2 lastArea;
        private int lastContent;
        private bool initialized;

        public static ResultPanelReadability Attach(GameObject root, Text title, Text[] stats, Button[] buttons)
        {
            if (root == null || title == null) return null;
            var helper = root.GetComponent<ResultPanelReadability>();
            if (helper != null) return helper;
            helper = root.AddComponent<ResultPanelReadability>();
            helper.title = title;
            helper.shards = stats.Length > 7 ? stats[7] : null;
            helper.panel = title.transform.parent as RectTransform;
            helper.area = root.transform as RectTransform;
            helper.panelSize = helper.panel.rect.size;
            helper.titleSize = title.rectTransform.sizeDelta;
            helper.titlePosition = title.rectTransform.anchoredPosition;
            foreach (var text in stats) if (text != null) helper.stats.Add(Capture(text));
            helper.buttons = buttons;
            helper.buttonPositions = new Vector2[buttons.Length];
            helper.buttonPivots = new Vector2[buttons.Length];
            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i] == null) continue;
                var rect = (RectTransform)buttons[i].transform;
                helper.buttonPositions[i] = rect.anchoredPosition;
                helper.buttonPivots[i] = rect.pivot;
                foreach (var text in buttons[i].GetComponentsInChildren<Text>(true)) helper.buttonLabels.Add(Capture(text));
            }
            helper.BuildScroll();
            helper.initialized = true;
            helper.Apply();
            return helper;
        }

        private static Entry Capture(Text text)
        {
            var rect = text.rectTransform;
            return new Entry { Text = text, Parent = rect.parent, AnchorMin = rect.anchorMin, AnchorMax = rect.anchorMax,
                Pivot = rect.pivot, Position = rect.anchoredPosition, Size = rect.sizeDelta, Font = text.fontSize };
        }

        private void BuildScroll()
        {
            var viewport = UiFactory.CreateRect(panel, "ResultReadingViewport", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f), Vector2.zero, Vector2.one);
            var surface = viewport.AddComponent<Image>();
            surface.color = new Color(1f, 1f, 1f, 0.02f);
            viewport.AddComponent<RectMask2D>();
            readingScroll = viewport.AddComponent<ScrollRect>();
            readingScroll.viewport = (RectTransform)viewport.transform;
            readingScroll.content = (RectTransform)UiFactory.CreateRect(viewport.transform, "Content", new Vector2(0f, 1f),
                new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, Vector2.one).transform;
            readingScroll.horizontal = false;
            readingScroll.vertical = true;
            readingScroll.movementType = ScrollRect.MovementType.Clamped;
            readingScroll.scrollSensitivity = 0f;
            var bar = UiFactory.CreateRect(panel, "ResultReadingScrollbar", new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(1f, 1f), Vector2.zero, new Vector2(8f, 1f));
            var track = bar.AddComponent<Image>(); track.color = new Color(1f, 1f, 1f, 0.15f);
            readingBar = bar.AddComponent<Scrollbar>();
            readingBar.direction = Scrollbar.Direction.BottomToTop;
            readingBar.navigation = new Navigation { mode = Navigation.Mode.None };
            var handle = UiFactory.CreateRect(bar.transform, "Handle", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            var image = handle.AddComponent<Image>(); image.color = new Color(0.9f, 0.88f, 0.8f, 0.8f);
            readingBar.handleRect = (RectTransform)handle.transform;
            readingBar.targetGraphic = image;
            readingScroll.verticalScrollbar = readingBar;
            readingHint = UiFactory.CreateLabel(panel, "ResultReadingHint", Vector2.zero, new Vector2(600f, 24f), string.Empty,
                16, Color.white, TextAnchor.MiddleCenter);
            readingHint.raycastTarget = false;
        }

        public void Apply()
        {
            if (!initialized) return;
            float scale = MenuTextReadability.Scale;
            int content = ContentSignature();
            bool reset = content != lastContent;
            float previous = readingScroll.verticalNormalizedPosition;
            float availableWidth = area != null && area.rect.width > 0f ? area.rect.width : 1920f;
            float availableHeight = area != null && area.rect.height > 0f ? area.rect.height : 1080f;
            float width = Mathf.Min(panelSize.x + 200f * (scale - 1f), Mathf.Max(100f, availableWidth - 32f));
            float statWidth = Mathf.Max(1f, width - 80f);
            Style(title, 28, scale);
            bool longTitle = title.preferredWidth > width - 136f;
            float titleWidth = Mathf.Min(titleSize.x, Mathf.Max(1f, width - (longTitle ? 136f : 20f)));
            float titleHeight = Mathf.Max(titleSize.y, Height(title, titleWidth) + 4f);
            title.rectTransform.sizeDelta = new Vector2(titleWidth, titleHeight);
            title.rectTransform.anchoredPosition = titlePosition + Vector2.right * (longTitle ? 29f : 0f);
            float statTop = Mathf.Max(110f, -titlePosition.y + titleHeight + 22f);
            float buttonHeight = 72f;
            float buttonWidth = Mathf.Min(300f + 40f * (scale - 1f), Mathf.Max(1f, (width - 100f) * 0.5f));
            foreach (var entry in buttonLabels)
            {
                Style(entry.Text, entry.Font, scale);
                buttonHeight = Mathf.Max(buttonHeight, Height(entry.Text, buttonWidth - 16f) + 16f);
            }
            float fullHeight = 0f;
            foreach (var entry in stats)
            {
                Style(entry.Text, entry.Font, scale);
                fullHeight += Mathf.Max(entry.Size.y, Height(entry.Text, statWidth) + 4f) + 6f;
            }
            fullHeight = Mathf.Max(0f, fullHeight - 6f);
            float buttonTop = 0f;
            for (int i = 0; i < buttons.Length; i++)
                if (buttons[i] != null) buttonTop = Mathf.Max(buttonTop, buttonPositions[i].y + (1f - buttonPivots[i].y) * buttonHeight);
            float footer = buttonTop + 14f;
            float height = Mathf.Min(Mathf.Max(panelSize.y, statTop + fullHeight + footer), Mathf.Max(100f, availableHeight - 32f));
            panel.sizeDelta = new Vector2(width, height);
            float space = Mathf.Max(1f, height - statTop - footer);
            bool overflow = fullHeight > space;
            readingHint.text = Loc.CurrentLanguage switch
            {
                LocalizationLanguage.Korean => "휠 · Page Up/Down · 패드 오른쪽 스틱",
                LocalizationLanguage.Japanese => "ホイール · Page Up/Down · 右スティック",
                _ => "Wheel · Page Up/Down · Right stick"
            };
            Style(readingHint, 16, scale);
            float hintHeight = overflow ? Height(readingHint, width - 40f) + 4f : 0f;
            readingHint.rectTransform.sizeDelta = new Vector2(width - 40f, hintHeight);
            readingHint.rectTransform.anchoredPosition = new Vector2(0f, -height * 0.5f + footer + hintHeight * 0.5f);
            if (overflow)
            {
                space = Mathf.Max(1f, space - hintHeight - 6f);
                statWidth = Mathf.Max(1f, width - 96f);
                fullHeight = 0f;
                foreach (var entry in stats) fullHeight += Mathf.Max(entry.Size.y, Height(entry.Text, statWidth) + 4f) + 6f;
                fullHeight = Mathf.Max(0f, fullHeight - 6f);
            }
            readingScroll.gameObject.SetActive(overflow);
            readingBar.gameObject.SetActive(overflow);
            readingHint.gameObject.SetActive(overflow);
            readingScroll.viewport.anchoredPosition = new Vector2(0f, -statTop);
            readingScroll.viewport.sizeDelta = new Vector2(width - 40f, space);
            readingScroll.content.sizeDelta = new Vector2(width - 40f, Mathf.Max(space, fullHeight));
            readingBar.GetComponent<RectTransform>().anchoredPosition = new Vector2(-20f, -statTop);
            readingBar.GetComponent<RectTransform>().sizeDelta = new Vector2(8f, space);
            float top = 0f;
            foreach (var entry in stats)
            {
                var rect = entry.Text.rectTransform;
                float lineHeight = Mathf.Max(entry.Size.y, Height(entry.Text, statWidth) + 4f);
                rect.SetParent(overflow ? readingScroll.content : entry.Parent, false);
                rect.anchorMin = overflow ? new Vector2(0f, 1f) : entry.AnchorMin;
                rect.anchorMax = overflow ? new Vector2(0f, 1f) : entry.AnchorMax;
                rect.pivot = overflow ? new Vector2(0f, 1f) : entry.Pivot;
                rect.sizeDelta = new Vector2(statWidth, lineHeight);
                rect.anchoredPosition = overflow ? new Vector2(36f, -top) : new Vector2(entry.Position.x, -statTop - top);
                top += lineHeight + 6f;
            }
            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i] == null) continue;
                var rect = (RectTransform)buttons[i].transform;
                rect.sizeDelta = new Vector2(buttonWidth, buttonHeight);
                float x = (i == 0 ? -1f : 1f) * (buttonWidth + 20f) * 0.5f;
                rect.anchoredPosition = new Vector2(x, buttonPositions[i].y);
                var art = rect.Find("ReturnStairsArt") as RectTransform;
                if (art != null) art.anchoredPosition = new Vector2(Mathf.Min(buttonWidth * 0.5f + 30f, width * 0.5f - x - art.rect.width * 0.5f - 4f), art.anchoredPosition.y);
            }
            UiArtDecor.PlaceLeadingIcon(title, UiArtKeys.UI_RUN_RESULT_EMBLEM, 48f, 10f);
            if (shards != null) UiArtDecor.PlaceLeadingIcon(shards, UiArtKeys.CURRENCY_ABYSS_SHARDS, 26f, 10f);
            readingScroll.StopMovement();
            readingScroll.verticalNormalizedPosition = reset ? 1f : previous;
            lastScale = scale; lastContent = content;
            lastArea = area != null ? area.rect.size : Vector2.zero;
            var canvas = GetComponentInParent<Canvas>();
            lastCanvasScale = canvas != null ? canvas.scaleFactor : 1f;
        }

        private static void Style(Text text, int font, float scale)
        {
            text.fontSize = Mathf.RoundToInt(font * scale);
            text.resizeTextForBestFit = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
        }

        private static float Height(Text text, float width)
        {
            var settings = text.GetGenerationSettings(new Vector2(Mathf.Max(1f, width), 0f));
            return text.cachedTextGeneratorForLayout.GetPreferredHeight(text.text, settings) / text.pixelsPerUnit;
        }

        private int ContentSignature()
        {
            int hash = title.text.GetHashCode();
            foreach (var entry in stats) hash = unchecked(hash * 31 + (entry.Text.text ?? string.Empty).GetHashCode());
            foreach (var entry in buttonLabels) hash = unchecked(hash * 31 + (entry.Text.text ?? string.Empty).GetHashCode());
            return unchecked(hash * 31 + (int)Loc.CurrentLanguage);
        }

        private void OnEnable() => lastScale = -1f;

        private void LateUpdate()
        {
            if (!initialized) return;
            var canvas = GetComponentInParent<Canvas>();
            if (lastScale != MenuTextReadability.Scale || lastContent != ContentSignature() ||
                lastArea != (area != null ? area.rect.size : Vector2.zero) || lastCanvasScale != (canvas != null ? canvas.scaleFactor : 1f)) Apply();
            if (!readingScroll.gameObject.activeSelf || SaveStatusOverlay.IsCapturingInput || SettingsPanel.IsOpen || CodexPanel.IsOpen) return;
            float range = readingScroll.content.rect.height - readingScroll.viewport.rect.height;
            if (range <= 0f) return;
            float pixels = 0f;
            UnityEngine.Camera camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            if (Mouse.current != null && RectTransformUtility.RectangleContainsScreenPoint(readingScroll.viewport, Mouse.current.position.ReadValue(), camera))
                pixels += Mouse.current.scroll.ReadValue().y * 0.5f;
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
    }
}
