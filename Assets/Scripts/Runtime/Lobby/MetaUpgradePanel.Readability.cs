using System.Collections.Generic;
using Abyss.Runtime.ArtIntegration;
using Abyss.Runtime.Localization;
using Abyss.Runtime.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Abyss.Runtime.Lobby
{
    /// <summary>실제 캔버스 크기 내에서 고정 글자 크기와 전체 설명·수동 스크롤을 제공한다.</summary>
    public sealed partial class MetaUpgradePanel
    {
        private RectTransform readingBox;
        private ScrollRect readingScroll;
        private Canvas readingCanvas;
        private Vector2 readingCanvasSize;
        private float readingCanvasScale;
        private float readingScale = -1f;
        private LocalizationLanguage readingLanguage;
        private GameObject readingSelection;
        private readonly Dictionary<Text, Vector3> readingMetrics = new();

        private void RefreshReadingLabels()
        {
            if (titleLabel != null) titleLabel.text = Loc.Get(StringKey.Altar_Title);
            if (closeButton != null)
            {
                var label = closeButton.GetComponentInChildren<Text>(true);
                if (label != null) label.text = Loc.Get(StringKey.Common_Close);
            }
            foreach (var row in rows)
            {
                row.NameLabel.text = ResolveRowName(row.Data);
                row.DescriptionLabel.text = ResolveRowDescription(row.Data);
                var label = row.Button.GetComponentInChildren<Text>(true);
                if (label != null) label.text = Loc.Get(row.Data.IsUnlock ? StringKey.Altar_Unlock : StringKey.Altar_Purchase);
            }
        }

        private void BuildReadingScroll()
        {
            if (readingScroll != null || rowContainer == null) return;
            readingBox = rowContainer.parent as RectTransform;
            if (readingBox == null) return;
            readingCanvas = readingBox.GetComponentInParent<Canvas>(true)?.rootCanvas;
            var viewport = CreateRect(readingBox, "AltarReadingViewport", new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, Vector2.one);
            viewport.gameObject.AddComponent<Image>().color = Color.clear;
            viewport.gameObject.AddComponent<RectMask2D>();
            rowContainer.SetParent(viewport, false);
            rowContainer.anchorMin = rowContainer.anchorMax = rowContainer.pivot = new Vector2(0.5f, 1f);
            rowContainer.anchoredPosition = Vector2.zero;
            readingScroll = viewport.gameObject.AddComponent<ScrollRect>();
            readingScroll.viewport = viewport;
            readingScroll.content = rowContainer;
            readingScroll.horizontal = false;
            readingScroll.inertia = false;
            readingScroll.movementType = ScrollRect.MovementType.Clamped;
            readingScroll.scrollSensitivity = 48f;

            var barRect = CreateRect(viewport, "Scrollbar", new Vector2(1f, 0f), Vector2.one,
                new Vector2(1f, 0.5f), Vector2.zero, new Vector2(12f, 0f));
            barRect.gameObject.AddComponent<Image>().color = RowBg;
            var handle = CreateRect(barRect, "Handle", Vector2.zero, Vector2.one,
                Vector2.one * 0.5f, Vector2.zero, Vector2.zero);
            var image = handle.gameObject.AddComponent<Image>();
            image.color = new Color(0.7f, 0.6f, 0.85f);
            var bar = barRect.gameObject.AddComponent<Scrollbar>();
            bar.handleRect = handle;
            bar.targetGraphic = image;
            bar.direction = Scrollbar.Direction.BottomToTop;
            bar.navigation = new Navigation { mode = Navigation.Mode.None };
            readingScroll.verticalScrollbar = bar;
            readingScroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
        }

        private Vector2 ReadCanvasSize() => readingCanvas != null
            ? ((RectTransform)readingCanvas.transform).rect.size : new Vector2(1920f, 1080f);

        private void ApplyReadability()
        {
            BuildReadingScroll();
            if (readingScroll == null) return;
            float readingY = rowContainer.anchoredPosition.y;
            readingScale = MenuTextReadability.Scale;
            readingLanguage = Loc.CurrentLanguage;
            readingCanvasSize = ReadCanvasSize();
            readingCanvasScale = readingCanvas != null ? readingCanvas.scaleFactor : 1f;
            float width = Mathf.Max(100f, Mathf.Min(960f, readingCanvasSize.x - 48f));
            float height = Mathf.Max(100f, readingCanvasSize.y - 48f);
            readingBox.anchorMin = readingBox.anchorMax = readingBox.pivot = Vector2.one * 0.5f;
            readingBox.anchoredPosition = Vector2.zero;
            readingBox.sizeDelta = new Vector2(width, height);

            float header = 16f;
            header += PlaceReadingText(titleLabel, 30, readingScale, width - 48f, header) + 8f;
            header += PlaceReadingText(shardsLabel, 22, readingScale, width - 80f, header) + 16f;
            if (shardsLabel != null)
                UiArtDecor.PlaceLeadingIcon(shardsLabel, UiArtKeys.CURRENCY_ABYSS_SHARDS, SHARDS_ICON_SIZE * readingScale);

            float closeHeight = 0f;
            if (closeButton != null)
            {
                var label = closeButton.GetComponentInChildren<Text>(true);
                SetReadingFont(label, 24, readingScale);
                var rect = (RectTransform)closeButton.transform;
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
                rect.pivot = new Vector2(0.5f, 0f);
                rect.sizeDelta = new Vector2(Mathf.Min(280f * readingScale, width - 32f), 64f * readingScale);
                if (label != null) rect.sizeDelta = new Vector2(rect.sizeDelta.x, Mathf.Max(rect.sizeDelta.y, label.preferredHeight + 16f));
                rect.anchoredPosition = new Vector2(0f, 16f);
                closeHeight = rect.sizeDelta.y + 32f;
            }
            readingScroll.viewport.sizeDelta = new Vector2(width - 32f, Mathf.Max(1f, height - header - closeHeight));
            readingScroll.viewport.anchoredPosition = new Vector2(0f, -header);

            float rowWidth = width - 64f;
            float offset = 0f;
            foreach (var row in rows)
            {
                row.Rect.sizeDelta = new Vector2(rowWidth, 1f);
                row.Rect.anchoredPosition = new Vector2(0f, -offset);
                float y = 12f;
                y += PlaceReadingText(row.NameLabel, 22, readingScale, rowWidth - 24f, y) + 8f;
                y += PlaceReadingText(row.DescriptionLabel, 14, readingScale, rowWidth - 24f, y) + 12f;
                // 정보/구매를 세로로 배치해 좁은 캔버스와 번역 문구에서도 열끼리 겹치지 않는다.
                y += PlaceReadingText(row.LevelLabel, 20, readingScale, rowWidth - 24f, y) + 4f;
                y += PlaceReadingText(row.CostLabel, 18, readingScale, rowWidth - 24f, y) + 8f;
                var buttonRect = (RectTransform)row.Button.transform;
                var buttonLabel = row.Button.GetComponentInChildren<Text>(true);
                SetReadingFont(buttonLabel, 18, readingScale);
                buttonRect.anchorMin = buttonRect.anchorMax = buttonRect.pivot = new Vector2(0.5f, 1f);
                buttonRect.sizeDelta = new Vector2(rowWidth - 24f, 56f * readingScale);
                if (buttonLabel != null) buttonRect.sizeDelta = new Vector2(buttonRect.sizeDelta.x, Mathf.Max(buttonRect.sizeDelta.y, buttonLabel.preferredHeight + 16f));
                buttonRect.anchoredPosition = new Vector2(0f, -y);
                y += buttonRect.sizeDelta.y + 12f;
                row.Rect.sizeDelta = new Vector2(rowWidth, y);
                offset += y + 8f;
            }
            rowContainer.sizeDelta = new Vector2(rowWidth, Mathf.Max(readingScroll.viewport.rect.height, offset));
            // 단순 재배치는 같은 선택을 새 선택으로 취급하지 않는다. 수동 읽기 위치만 새 범위에 맞춘다.
            ClampReadingScroll(readingY);
            readingMetrics.Clear();
            foreach (var text in readingBox.GetComponentsInChildren<Text>(true)) readingMetrics[text] = ReadingMetric(text);
        }

        private static void SetReadingFont(Text text, int size, float scale)
        {
            if (text == null) return;
            UiFactory.ApplyFont(text);
            text.fontSize = Mathf.RoundToInt(size * scale);
            text.resizeTextForBestFit = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
        }

        private static float PlaceReadingText(Text text, int size, float scale, float width, float y)
        {
            if (text == null) return 0f;
            SetReadingFont(text, size, scale);
            var rect = text.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(width, 1f);
            float height = Mathf.Max(text.fontSize * 1.5f, text.preferredHeight + 2f);
            rect.sizeDelta = new Vector2(width, height);
            rect.anchoredPosition = new Vector2(0f, -y);
            return height;
        }

        private static Vector3 ReadingMetric(Text text) => new Vector3(text.preferredWidth, text.preferredHeight,
            text.font != null ? text.font.GetEntityId().GetHashCode() : 0);

        private void ClampReadingScroll(float y) => rowContainer.anchoredPosition = new Vector2(0f,
            Mathf.Clamp(y, 0f, Mathf.Max(0f, rowContainer.rect.height - readingScroll.viewport.rect.height)));

        private void LateUpdate()
        {
            if (!IsOpen) return;
            bool languageChanged = readingLanguage != Loc.CurrentLanguage;
            if (languageChanged) Refresh();
            bool changed = readingScale != MenuTextReadability.Scale || readingCanvasSize != ReadCanvasSize() ||
                readingCanvasScale != (readingCanvas != null ? readingCanvas.scaleFactor : 1f);
            foreach (var entry in readingMetrics)
                if (entry.Key != null && entry.Value != ReadingMetric(entry.Key)) { changed = true; break; }
            if (changed) ApplyReadability();
            if (readingScroll == null) return;
            // 상위 모달 동안 휠·드래그도 넘긴다. 닫힌 프레임까지 기존 입력 가드를 따른다.
            readingScroll.enabled = !IsUpperModalOwningInput;
            readingScroll.verticalScrollbar.interactable = !IsUpperModalOwningInput;
            if (IsUpperModalOwningInput) return;
            var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            if (selected != readingSelection)
            {
                readingSelection = selected;
                foreach (var row in rows)
                {
                    if (selected != row.Button.gameObject) continue;
                    float top = -row.Rect.anchoredPosition.y;
                    float bottom = top + row.Rect.rect.height;
                    float viewHeight = readingScroll.viewport.rect.height;
                    float y = rowContainer.anchoredPosition.y;
                    // 큰 행도 구매 버튼은 화면에 들인다. 설명 앞부분은 수동 스크롤로 읽는다.
                    if (top < y && bottom - top <= viewHeight) y = top;
                    else if (bottom < y) y = bottom - viewHeight;
                    else if (bottom > y + viewHeight) y = bottom - viewHeight;
                    ClampReadingScroll(y);
                    break;
                }
            }
            float delta = 0f;
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.pageDownKey.wasPressedThisFrame) delta += readingScroll.viewport.rect.height * 0.8f;
                if (keyboard.pageUpKey.wasPressedThisFrame) delta -= readingScroll.viewport.rect.height * 0.8f;
                if (keyboard.homeKey.wasPressedThisFrame) ClampReadingScroll(0f);
                if (keyboard.endKey.wasPressedThisFrame) ClampReadingScroll(rowContainer.rect.height);
            }
            if (delta != 0f) ClampReadingScroll(rowContainer.anchoredPosition.y + delta);
        }
    }
}
