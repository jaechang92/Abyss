using System.Collections.Generic;
using Abyss.Runtime.Localization;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using static Abyss.Runtime.UI.UiFactory;

namespace Abyss.Runtime.UI
{
    public sealed partial class CodexPanel
    {
        private sealed class ReadingEntry
        {
            public int FontSize;
            public string Content;
            public float Height;
            public float Width;
        }
        private readonly Dictionary<Text, ReadingEntry> readingFonts = new();
        private Text[] detailReadingTexts;
        private Text[] recordReadingTexts;
        private Text[] headerReadingTexts;
        private Canvas readingCanvas;
        private float readingCanvasScale;
        private ScrollRect gridScroll;
        private ScrollRect detailScroll;
        private ScrollRect recordsScroll;
        private GameObject readingPanel;
        private float readingScale = -1f;
        private GameObject readingSelection;

        private void OnEnable() => Loc.AddLanguageChangedListener(RefreshReadingLanguage);

        private void RefreshReadingLanguage(LocalizationLanguage language)
        {
            if (body != null) Refresh();
        }

        private void BuildReadability(Transform panel)
        {
            readingPanel = panel.gameObject;
            readingCanvas = GetComponent<Canvas>();
            detailReadingTexts = new[] { detailName, detailBadge, detailDescription, detailStats };
            recordReadingTexts = new[] { recordsText, recordsSecondaryText };
            headerReadingTexts = new[] { panel.Find("TitleText").GetComponent<Text>(), panel.Find("HintText").GetComponent<Text>(), countLabel };
            foreach (var text in panel.GetComponentsInChildren<Text>(true))
            {
                // Decorative unknown glyphs and pager arrows retain their existing dimensions.
                if (text.name == "Glyph" || text.transform.parent.name == "PrevButton" || text.transform.parent.name == "NextButton") continue;
                readingFonts[text] = new ReadingEntry { FontSize = text.fontSize };
            }
            gridScroll = CreateReadingScroll(panel, "GridViewport", new Vector2(-297f, 0f), new Vector2(822f, 480f), 822f);
            var oldGrid = gridRoot;
            foreach (var tile in tiles) tile.Root.transform.SetParent(gridScroll.content, false);
            gridRoot = gridScroll.content.gameObject;
            Destroy(oldGrid);
            detailScroll = CreateReadingScroll(detailRoot.transform, "TextViewport", new Vector2(0f, -90f), new Vector2(540f, 336f), 520f);
            foreach (var text in detailReadingTexts)
                text.transform.SetParent(detailScroll.content, false);
            recordsScroll = CreateReadingScroll(panel, "RecordsViewport", new Vector2(0f, -25f), new Vector2(1380f, 530f), 1360f);
            recordsText.transform.SetParent(recordsScroll.content, false);
            recordsSecondaryText.transform.SetParent(recordsScroll.content, false);
        }

        private static ScrollRect CreateReadingScroll(Transform parent, string name, Vector2 position, Vector2 size, float width)
        {
            var viewport = CreateRect(parent, name, Vector2.one * 0.5f, Vector2.one * 0.5f, Vector2.one * 0.5f, position, size);
            viewport.AddComponent<Image>().color = Color.clear;
            viewport.AddComponent<RectMask2D>();
            var contentObject = CreateRect(viewport.transform, "Content", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f), Vector2.zero, new Vector2(width, size.y));
            var scroll = viewport.AddComponent<ScrollRect>();
            scroll.viewport = (RectTransform)viewport.transform;
            scroll.content = (RectTransform)contentObject.transform;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.inertia = false;
            scroll.scrollSensitivity = 45f;
            var barObject = CreateRect(viewport.transform, "Scrollbar", new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                Vector2.one * 0.5f, new Vector2(-5f, 0f), new Vector2(8f, size.y));
            barObject.AddComponent<Image>().color = new Color(0.2f, 0.2f, 0.3f, 0.8f);
            var handle = CreateRect(barObject.transform, "Handle", Vector2.zero, Vector2.one, Vector2.one * 0.5f, Vector2.zero, Vector2.zero);
            Stretch((RectTransform)handle.transform);
            var handleImage = handle.AddComponent<Image>();
            handleImage.color = new Color(0.7f, 0.66f, 0.85f);
            var bar = barObject.AddComponent<Scrollbar>();
            bar.handleRect = (RectTransform)handle.transform;
            bar.targetGraphic = handleImage;
            bar.direction = Scrollbar.Direction.BottomToTop;
            bar.navigation = new Navigation { mode = Navigation.Mode.None };
            scroll.verticalScrollbar = bar;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            return scroll;
        }

        private void SetReadabilityTab(bool records)
        {
            if (recordsScroll == null) return;
            recordsScroll.gameObject.SetActive(records);
            gridScroll.gameObject.SetActive(!records);
        }

        private void ApplyReadability(bool resetScroll = false)
        {
            if (readingPanel == null) return;
            readingScale = MenuTextReadability.Scale;
            foreach (var entry in readingFonts)
            {
                entry.Key.fontSize = Mathf.RoundToInt(entry.Value.FontSize * readingScale);
                entry.Key.resizeTextForBestFit = false;
                entry.Key.horizontalOverflow = HorizontalWrapMode.Wrap;
            }
            foreach (var tab in tabButtons)
            {
                var rect = (RectTransform)tab.transform;
                rect.sizeDelta = new Vector2(TAB_W, Mathf.Max(46f, tab.GetComponentInChildren<Text>().preferredHeight + 6f));
            }
            float tabBottom = TAB_ROW_Y - 23f;
            foreach (var tab in tabButtons) tabBottom = Mathf.Min(tabBottom, TAB_ROW_Y - ((RectTransform)tab.transform).rect.height * 0.5f);
            readingPanel.transform.Find("Divider").GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, Mathf.Min(272f, tabBottom - 8f));
            var closeRect = (RectTransform)closeButton.transform;
            closeRect.sizeDelta = new Vector2(140f, Mathf.Max(44f, closeButton.GetComponentInChildren<Text>().preferredHeight + 8f));
            foreach (var text in headerReadingTexts)
            {
                text.rectTransform.sizeDelta = new Vector2(text.rectTransform.sizeDelta.x, Mathf.Max(text.name == "TitleText" ? 48f : 32f, text.preferredHeight));
            }
            pageLabel.rectTransform.sizeDelta = new Vector2(120f, Mathf.Max(44f, pageLabel.preferredHeight));
            float offset = 8f;
            for (int rowIndex = 0; rowIndex < GRID_ROWS; rowIndex++)
            {
                float rowHeight = TILE_SIZE;
                for (int col = 0; col < GRID_COLS; col++)
                {
                    var tile = tiles[rowIndex * GRID_COLS + col];
                    if (!tile.Root.activeSelf) continue;
                    rowHeight = Mathf.Max(rowHeight, 120f + tile.NameLabel.preferredHeight);
                }
                for (int col = 0; col < GRID_COLS; col++)
                {
                    var tile = tiles[rowIndex * GRID_COLS + col];
                    var rect = (RectTransform)tile.Root.transform;
                    // Tiles are built center-anchored; the y below is measured from the content top, so anchor there.
                    // Pivot stays centered — x is still relative to the content center and children keep their offsets.
                    rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
                    rect.sizeDelta = new Vector2(TILE_SIZE, rowHeight);
                    rect.anchoredPosition = new Vector2(-gridScroll.content.rect.width * 0.5f + 83f + col * TILE_STEP, -offset - rowHeight * 0.5f);
                    tile.Background.rectTransform.sizeDelta = rect.sizeDelta;
                    tile.Selection.rectTransform.sizeDelta = rect.sizeDelta + Vector2.one * 8f;
                    tile.Icon.rectTransform.anchoredPosition = tile.Glyph.rectTransform.anchoredPosition = new Vector2(0f, rowHeight * 0.5f - 55f);
                    float textHeight = Mathf.Max(38f, tile.NameLabel.preferredHeight);
                    tile.NameLabel.rectTransform.sizeDelta = new Vector2(138f, textHeight);
                    tile.NameLabel.rectTransform.anchoredPosition = new Vector2(0f, rowHeight * 0.5f - 106f - textHeight * 0.5f);
                    ApplyEntryFrame(tile);
                }
                offset += rowHeight + 14f;
            }
            ResizeReadingContent(gridScroll, offset - 6f, resetScroll);
            offset = 0f;
            foreach (var text in detailReadingTexts)
            {
                float height = string.IsNullOrEmpty(text.text) ? 0f : text.preferredHeight;
                text.rectTransform.anchorMin = text.rectTransform.anchorMax = new Vector2(0.5f, 1f);
                text.rectTransform.pivot = new Vector2(0.5f, 1f);
                text.rectTransform.sizeDelta = new Vector2(text == detailName || text == detailBadge ? 520f : 510f, height);
                text.rectTransform.anchoredPosition = new Vector2(0f, -offset);
                offset += height > 0f ? height + 12f : 0f;
            }
            ResizeReadingContent(detailScroll, offset, resetScroll);
            float recordsHeight = 0f;
            foreach (var text in recordReadingTexts)
            {
                text.rectTransform.anchorMin = text.rectTransform.anchorMax = new Vector2(0.5f, 1f);
                text.rectTransform.pivot = new Vector2(0.5f, 1f);
                text.rectTransform.sizeDelta = new Vector2(660f, text.preferredHeight);
                text.rectTransform.anchoredPosition = new Vector2(text == recordsText ? -340f : 340f, 0f);
                recordsHeight = Mathf.Max(recordsHeight, text.preferredHeight);
            }
            ResizeReadingContent(recordsScroll, recordsHeight, resetScroll);
            readingSelection = null;
            readingCanvasScale = readingCanvas.scaleFactor;
            foreach (var entry in readingFonts)
            {
                entry.Value.Content = entry.Key.text;
                entry.Value.Height = entry.Key.preferredHeight;
                entry.Value.Width = entry.Key.preferredWidth;
            }
        }

        private static void ResizeReadingContent(ScrollRect scroll, float height, bool reset)
        {
            scroll.content.sizeDelta = new Vector2(scroll.content.sizeDelta.x, Mathf.Max(scroll.viewport.rect.height, height));
            float position = reset ? 0f : scroll.content.anchoredPosition.y;
            scroll.content.anchoredPosition = new Vector2(0f, Mathf.Clamp(position, 0f, scroll.content.rect.height - scroll.viewport.rect.height));
        }

        private void LateUpdate()
        {
            if (!IsOpen || readingPanel == null) return;
            // Re-measure fonts after canvas/resolution or text changes, including dynamic record content.
            bool changed = readingScale != MenuTextReadability.Scale || readingCanvasScale != readingCanvas.scaleFactor;
            foreach (var entry in readingFonts)
                if (entry.Value.Content != entry.Key.text || Mathf.Abs(entry.Value.Height - entry.Key.preferredHeight) > 0.01f ||
                    Mathf.Abs(entry.Value.Width - entry.Key.preferredWidth) > 0.01f) { changed = true; break; }
            if (changed) ApplyReadability();
            if (IsUpperModalOwningFocus) return;
            RevealReadingTile();
            var scroll = currentTab == CodexTab.Records ? recordsScroll : detailScroll;
            float delta = 0f;
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.pageDownKey.wasPressedThisFrame) delta += scroll.viewport.rect.height * 0.8f;
                if (keyboard.pageUpKey.wasPressedThisFrame) delta -= scroll.viewport.rect.height * 0.8f;
            }
            var gamepad = Gamepad.current;
            if (gamepad != null) delta += -gamepad.rightStick.ReadValue().y * 420f * Time.unscaledDeltaTime;
            if (delta != 0f)
                scroll.content.anchoredPosition = new Vector2(0f, Mathf.Clamp(scroll.content.anchoredPosition.y + delta, 0f,
                    scroll.content.rect.height - scroll.viewport.rect.height));
        }

        private void RevealReadingTile()
        {
            var selection = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            if (selection == readingSelection) return;
            readingSelection = selection;
            int slot = SlotOf(selection);
            if (slot < 0 || !gridRoot.activeSelf) return;
            var tile = (RectTransform)tiles[slot].Root.transform;
            float top = -tile.anchoredPosition.y - tile.rect.height * 0.5f - 4f;
            float bottom = top + tile.rect.height + 8f;
            float y = gridScroll.content.anchoredPosition.y;
            if (top < y) y = top;
            else if (bottom > y + gridScroll.viewport.rect.height) y = bottom - gridScroll.viewport.rect.height;
            gridScroll.content.anchoredPosition = new Vector2(0f, Mathf.Clamp(y, 0f, gridScroll.content.rect.height - gridScroll.viewport.rect.height));
        }
    }
}
