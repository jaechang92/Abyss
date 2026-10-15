using System.Collections.Generic;
using Abyss.Runtime.ArtIntegration;
using Abyss.Runtime.Localization;
using Abyss.Runtime.Meta;
using Abyss.Runtime.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Abyss.Runtime.Lobby
{
    public sealed partial class RelicShopPanel
    {
        private sealed class ReadingEntry
        {
            public int FontSize;
            public string Content;
            public float Height;
        }
        private readonly Dictionary<Text, ReadingEntry> readingFonts = new();
        private ScrollRect readingScroll;
        private Text readingTitle;
        private Text readingEquipHeader;
        private Canvas readingCanvas;
        private float readingScale = -1f;
        private float readingCanvasScale;
        private GameObject readingSelection;
        private Selectable readingFocus;
        private string readingMessageKey;
        private object[] readingMessageArgs;
        private RelicData readingMessageRelic;

        private void OnEnable() => Loc.AddLanguageChangedListener(RefreshReadabilityLanguage);

        private void RefreshReadabilityLanguage(LocalizationLanguage language)
        {
            RefreshReadabilityLabels();
            readingSelection = null;
            if (built && readingScroll != null) Refresh();
        }

        private void RefreshReadabilityLabels()
        {
            if (!built || readingScroll == null) return;
            readingTitle.text = Loc.Get(StringKey.Relic_Title);
            readingEquipHeader.text = Loc.Get(StringKey.Relic_Equipped);
            drawButton.GetComponentInChildren<Text>().text = Loc.GetFormat(StringKey.Relic_DrawFormat, RelicGacha.DRAW_COST);
            closeButton.GetComponentInChildren<Text>().text = Loc.Get(StringKey.Relic_Close);
            if (readingMessageRelic != null && readingMessageArgs != null && readingMessageArgs.Length > 0)
                readingMessageArgs[0] = Loc.Get(readingMessageRelic.nameKey);
            if (readingMessageKey != null) messageLabel.text = Loc.GetFormat(readingMessageKey, readingMessageArgs);
        }

        private void SetLocalizedMessage(string key, params object[] args)
        {
            readingMessageKey = key;
            readingMessageRelic = null;
            readingMessageArgs = args;
            if (messageLabel != null) messageLabel.text = Loc.GetFormat(key, args);
        }

        private void BuildReadability()
        {
            readingCanvas = navigationBox.GetComponentInParent<Canvas>();
            navigationBox.sizeDelta = new Vector2(BOX_W, Mathf.Min(820f, navigationBox.sizeDelta.y));
            readingTitle = navigationBox.Find("Title").GetComponent<Text>();
            readingEquipHeader = navigationBox.Find("EquipHeader").GetComponent<Text>();
            foreach (var text in navigationBox.GetComponentsInChildren<Text>(true))
                readingFonts[text] = new ReadingEntry { FontSize = text.fontSize };
            var viewport = CreateRect(navigationBox, "ReadingViewport", Vector2.one * 0.5f, Vector2.one * 0.5f,
                Vector2.one * 0.5f, Vector2.zero, new Vector2(860f, 420f));
            viewport.gameObject.AddComponent<Image>().color = Color.clear;
            viewport.gameObject.AddComponent<RectMask2D>();
            var content = CreateRect(viewport, "Content", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f), Vector2.zero, new Vector2(840f, 420f));
            readingScroll = viewport.gameObject.AddComponent<ScrollRect>();
            readingScroll.viewport = viewport;
            readingScroll.content = content;
            readingScroll.horizontal = false;
            readingScroll.inertia = false;
            readingScroll.movementType = ScrollRect.MovementType.Clamped;
            readingScroll.scrollSensitivity = 45f;
            readingEquipHeader.transform.SetParent(content, false);
            ownedHeaderLabel.transform.SetParent(content, false);
            foreach (var slot in slots) slot.Button.transform.SetParent(content, false);
            foreach (var tile in tiles) tile.Root.transform.SetParent(content, false);
            var barRoot = CreateRect(viewport, "Scrollbar", new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                Vector2.one * 0.5f, new Vector2(-5f, 0f), new Vector2(8f, 420f));
            barRoot.gameObject.AddComponent<Image>().color = new Color(0.2f, 0.2f, 0.3f, 0.8f);
            var handle = CreateRect(barRoot, "Handle", Vector2.zero, Vector2.one, Vector2.one * 0.5f, Vector2.zero, Vector2.zero);
            UiFactory.Stretch(handle);
            var image = handle.gameObject.AddComponent<Image>();
            image.color = new Color(0.7f, 0.66f, 0.85f);
            var bar = barRoot.gameObject.AddComponent<Scrollbar>();
            bar.handleRect = handle;
            bar.targetGraphic = image;
            bar.direction = Scrollbar.Direction.BottomToTop;
            bar.navigation = new Navigation { mode = Navigation.Mode.None };
            readingScroll.verticalScrollbar = bar;
            readingScroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
        }

        private void ApplyReadability()
        {
            bool rescaled = readingScale != MenuTextReadability.Scale || readingCanvasScale != (readingCanvas != null ? readingCanvas.scaleFactor : 1f);
            readingScale = MenuTextReadability.Scale;
            readingCanvasScale = readingCanvas != null ? readingCanvas.scaleFactor : 1f;
            foreach (var entry in readingFonts)
            {
                entry.Key.fontSize = Mathf.RoundToInt(entry.Value.FontSize * readingScale);
                entry.Key.resizeTextForBestFit = false;
            }
            float titleHeight = Mathf.Max(40f, Mathf.Max(readingTitle.preferredHeight, shardsLabel.preferredHeight));
            PositionReadingText(readingTitle, -210f, 20f, 440f, titleHeight);
            PositionReadingText(shardsLabel, 260f, 20f, 340f, titleHeight);
            UiArtDecor.PlaceLeadingIcon(shardsLabel, UiArtKeys.CURRENCY_ABYSS_SHARDS, SHARDS_ICON_SIZE);
            float buttonHeight = Mathf.Max(56f, Mathf.Max(drawButton.GetComponentInChildren<Text>().preferredHeight,
                closeButton.GetComponentInChildren<Text>().preferredHeight) + 12f);
            float boxHeight = navigationBox.rect.height;
            var draw = (RectTransform)drawButton.transform;
            var close = (RectTransform)closeButton.transform;
            draw.sizeDelta = new Vector2(240f, buttonHeight);
            close.sizeDelta = new Vector2(200f, buttonHeight);
            draw.anchoredPosition = new Vector2(-140f, -boxHeight + 24f + buttonHeight * 0.5f);
            close.anchoredPosition = new Vector2(140f, draw.anchoredPosition.y);
            float messageHeight = Mathf.Max(32f, messageLabel.preferredHeight);
            PositionReadingText(messageLabel, 0f, boxHeight - 24f - buttonHeight - 14f - messageHeight, 820f, messageHeight);
            float top = -20f - titleHeight - 18f;
            float bottom = -boxHeight + 24f + buttonHeight + 14f + messageHeight + 16f;
            readingScroll.viewport.anchorMin = readingScroll.viewport.anchorMax = new Vector2(0.5f, 1f);
            readingScroll.viewport.sizeDelta = new Vector2(860f, Mathf.Max(80f, top - bottom));
            readingScroll.viewport.anchoredPosition = new Vector2(0f, top - readingScroll.viewport.rect.height * 0.5f);
            ((RectTransform)readingScroll.verticalScrollbar.transform).sizeDelta = new Vector2(8f, readingScroll.viewport.rect.height);
            float offset = 8f;
            float headerHeight = Mathf.Max(28f, readingEquipHeader.preferredHeight);
            PositionReadingText(readingEquipHeader, -250f, offset, 360f, headerHeight);
            offset += headerHeight + 12f;
            float slotHeight = SLOT_H;
            foreach (var slot in slots) slotHeight = Mathf.Max(slotHeight, slot.NameLabel.preferredHeight + slot.LevelLabel.preferredHeight + 28f);
            foreach (var slot in slots)
            {
                var rect = (RectTransform)slot.Button.transform;
                rect.sizeDelta = new Vector2(SLOT_W, slotHeight);
                rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, -offset - slotHeight * 0.5f);
                PlaceReadingPair(slot.NameLabel, slot.LevelLabel, SLOT_W, slotHeight);
            }
            offset += slotHeight + 18f;
            headerHeight = Mathf.Max(28f, ownedHeaderLabel.preferredHeight);
            PositionReadingText(ownedHeaderLabel, -250f, offset, 360f, headerHeight);
            offset += headerHeight + 12f;
            int maxRow = -1;
            foreach (var tile in tiles) if (tile.Root.activeSelf) maxRow = Mathf.Max(maxRow, tile.Row);
            for (int row = 0; row <= maxRow; row++)
            {
                float height = 0f;
                foreach (var tile in tiles)
                    if (tile.Row == row && tile.Root.activeSelf)
                        height = Mathf.Max(height, Mathf.Max(TILE_H, tile.NameLabel.preferredHeight + tile.LevelLabel.preferredHeight + 28f));
                if (height == 0f) continue;
                foreach (var tile in tiles)
                {
                    if (tile.Row != row || !tile.Root.activeSelf) continue;
                    var rect = (RectTransform)tile.Root.transform;
                    rect.sizeDelta = new Vector2(TILE_W, height);
                    rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, -offset - height * 0.5f);
                    PlaceReadingPair(tile.NameLabel, tile.LevelLabel, TILE_W, height);
                }
                offset += height + 8f;
            }
            readingScroll.content.sizeDelta = new Vector2(840f, Mathf.Max(readingScroll.viewport.rect.height, offset));
            ClampReadingScroll(readingScroll.content.anchoredPosition.y);
            if (rescaled) readingSelection = null;
            foreach (var entry in readingFonts)
            {
                entry.Value.Content = entry.Key.text;
                entry.Value.Height = entry.Key.preferredHeight;
            }
        }

        private static void PositionReadingText(Text text, float x, float offset, float width, float height)
        {
            var rect = text.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(width, height);
            rect.anchoredPosition = new Vector2(x, -offset);
        }

        private static void PlaceReadingPair(Text name, Text level, float width, float height)
        {
            float nameHeight = Mathf.Max(30f, name.preferredHeight);
            float levelHeight = Mathf.Max(24f, level.preferredHeight);
            name.rectTransform.sizeDelta = new Vector2(width - 16f, nameHeight);
            level.rectTransform.sizeDelta = new Vector2(width - 16f, levelHeight);
            name.rectTransform.anchoredPosition = new Vector2(0f, height * 0.5f - 10f - nameHeight * 0.5f);
            level.rectTransform.anchoredPosition = new Vector2(0f, height * 0.5f - 18f - nameHeight - levelHeight * 0.5f);
        }

        private void ClampReadingScroll(float y) => readingScroll.content.anchoredPosition = new Vector2(0f,
            Mathf.Clamp(y, 0f, Mathf.Max(0f, readingScroll.content.rect.height - readingScroll.viewport.rect.height)));

        private void LateUpdate()
        {
            if (!IsOpen || readingScroll == null) return;
            bool changed = readingScale != MenuTextReadability.Scale || readingCanvasScale != (readingCanvas != null ? readingCanvas.scaleFactor : 1f);
            foreach (var entry in readingFonts)
                if (entry.Value.Content != entry.Key.text || Mathf.Abs(entry.Value.Height - entry.Key.preferredHeight) > 0.01f) { changed = true; break; }
            if (changed) ApplyReadability();
            if (IsUpperModalOwningInput) return;
            var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            if (selected != null && selected != readingSelection)
            {
                readingSelection = selected;
                if (selected.TryGetComponent<Selectable>(out var selectable) && IsNavigable(selectable)) readingFocus = selectable;
                if (selected.transform.IsChildOf(readingScroll.content))
                {
                    var rect = (RectTransform)selected.transform;
                    float top = -rect.anchoredPosition.y - rect.rect.height * 0.5f;
                    float bottom = top + rect.rect.height;
                    float y = readingScroll.content.anchoredPosition.y;
                    if (top < y) y = top;
                    else if (bottom > y + readingScroll.viewport.rect.height) y = bottom - readingScroll.viewport.rect.height;
                    ClampReadingScroll(y);
                }
            }
            float delta = 0f;
            if (Keyboard.current != null)
            {
                if (Keyboard.current.pageDownKey.wasPressedThisFrame) delta += readingScroll.viewport.rect.height * 0.8f;
                if (Keyboard.current.pageUpKey.wasPressedThisFrame) delta -= readingScroll.viewport.rect.height * 0.8f;
            }
            if (Gamepad.current != null) delta += -Gamepad.current.rightStick.ReadValue().y * 420f * Time.unscaledDeltaTime;
            if (delta != 0f) ClampReadingScroll(readingScroll.content.anchoredPosition.y + delta);
        }
    }
}
