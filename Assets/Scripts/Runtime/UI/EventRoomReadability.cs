using System.Collections.Generic;
using Abyss.Runtime.Localization;
using UnityEngine;
using UnityEngine.UI;
using static Abyss.Runtime.UI.UiFactory;

namespace Abyss.Runtime.UI
{
    /// <summary>
    /// <see cref="EventRoomPanel"/>의 메뉴 글자 확대·읽기 배치. 제목은 고정 머리, [계속]은 고정 바닥,
    /// 본문과 선택지(같은 버튼 참조)는 하나의 ScrollRect 안에서 흐른다. 글자는 자르거나 줄이지 않는다.
    ///
    /// 🔴 효과·클릭·포커스 규약은 건드리지 않는다. 버튼 복제·교체·onClick 추가 없음. 제목의 부모(실제 Panel)는 그대로다 —
    ///    그림(EventRoomPanel.Art)이 title.transform.parent를 쓴다. 본문·선택지는 body 아래에 남아 Navigation의 IsChildOf 판정이 유지된다.
    /// 이 파일은 구성·배치·상태 비교를 맡는다. 선택 추적·자동 노출·수동 스크롤은 EventRoomReadability.Input.cs가 맡는다.
    /// </summary>
    public sealed partial class EventRoomReadability : MonoBehaviour
    {
        private const float SAFE_MARGIN = 24f;
        private const float TOP_PADDING = 31f;
        private const float HEADER_GAP = 17f;
        private const float FOOTER_GAP = 10f;
        private const float BOTTOM_PADDING = 24f;
        private const float SIDE_INSET = 20f;
        private const float CONTENT_PADDING = 8f;
        private const float DESCRIPTION_CHOICE_GAP = 16f;
        private const float CHOICE_GAP = 12f;
        private const float LABEL_INSET = 12f;
        private const float LABEL_PADDING = 12f;
        private const float INITIAL_VIEWPORT_HEIGHT = 80f;
        private const float MIN_VIEWPORT_HEIGHT = 1f;
        private const float ART_GAP = 22f;
        private const float ART_TOP_INSET = 20f;
        private const float DESCRIPTION_LINE_SPACING = 1.4f;
        private const int HINT_FONT = 15;
        private const int CONTINUE_MASK_BIT = 30;
        private const string ART_NAME = "EventArtwork";

        private static readonly Vector2 ArtSize = new(180f, 200f);
        private static readonly Color HintColor = new(0.72f, 0.72f, 0.84f);

        private RectTransform panel;
        private RectTransform canvasRect;
        private Canvas rootCanvas;
        private Text title;
        private Text description;
        private Text continueLabel;
        private Text hint;
        private Button continueButton;
        private readonly List<Button> choices = new();
        private readonly List<Text> choiceLabels = new();
        private readonly List<Text> trackedTexts = new();
        private ScrollRect readingScroll;
        private RectTransform viewport;
        private RectTransform content;
        private Image artwork;

        // 기준값 — Attach에서 한 번만 잡는다. 이후 배치는 늘 여기서 다시 계산한다(누적 변경 없음).
        private Vector2 basePanel;
        private Vector2 baseTitle;
        private Vector2 baseDescription;
        private Vector2 baseChoice;
        private Vector2 baseContinue;
        private int titleFont;
        private int descriptionFont;
        private int choiceFont;
        private int continueFont;

        // 마지막 배치 시점의 상태. 달라졌을 때만 다시 배치한다.
        private float appliedScale = -1f;
        private float appliedCanvasScale = -1f;
        private Vector2 appliedCanvasSize;
        private LocalizationLanguage appliedLanguage;
        private int appliedActiveMask = -1;
        private int appliedInteractableMask = -1;
        private readonly List<string> appliedTexts = new();
        private readonly List<float> appliedHeights = new();

        private bool isResetPending;
        private bool isNormalizedPreserved;
        private bool isArtSourceVisible;
        private bool hasWarnedChromeOverflow;

        /// <summary>패널 본체(딤 배경)에 붙인다. 본문·선택지를 스크롤 내용으로 한 번 옮긴다. 이미 붙어 있으면 그것을 돌려준다.</summary>
        public static EventRoomReadability Attach(Transform modalBody, Text title, Text description,
                                                  IReadOnlyList<Button> choices, Button continueButton)
        {
            if (modalBody == null || title == null || description == null || choices == null || continueButton == null) return null;
            if (title.transform.parent is not RectTransform panel) return null;
            var existing = modalBody.GetComponent<EventRoomReadability>();
            if (existing != null) return existing;

            var helper = modalBody.gameObject.AddComponent<EventRoomReadability>();
            helper.Build(panel, title, description, choices, continueButton);
            return helper;
        }

        private void Build(RectTransform targetPanel, Text titleText, Text descriptionText,
                           IReadOnlyList<Button> choiceButtons, Button continueTarget)
        {
            panel = targetPanel;
            title = titleText;
            description = descriptionText;
            continueButton = continueTarget;
            continueLabel = continueButton.GetComponentInChildren<Text>(true);
            rootCanvas = panel.GetComponentInParent<Canvas>();
            if (rootCanvas != null) rootCanvas = rootCanvas.rootCanvas;
            canvasRect = rootCanvas != null ? (RectTransform)rootCanvas.transform : null;

            basePanel = panel.sizeDelta;
            baseTitle = title.rectTransform.sizeDelta;
            baseDescription = description.rectTransform.sizeDelta;
            baseContinue = ((RectTransform)continueButton.transform).sizeDelta;
            titleFont = title.fontSize;
            descriptionFont = description.fontSize;
            continueFont = continueLabel != null ? continueLabel.fontSize : 20;

            for (int i = 0; i < choiceButtons.Count; i++)
            {
                if (choiceButtons[i] == null) continue;
                choices.Add(choiceButtons[i]);
                choiceLabels.Add(choiceButtons[i].GetComponentInChildren<Text>(true));
            }
            baseChoice = choices.Count > 0 ? ((RectTransform)choices[0].transform).sizeDelta : new Vector2(600f, 56f);
            choiceFont = choiceLabels.Count > 0 && choiceLabels[0] != null ? choiceLabels[0].fontSize : 19;

            BuildScroll();
            hint = CreateLabel(panel, "ReadingHint", Vector2.zero, new Vector2(baseTitle.x, 24f),
                string.Empty, HINT_FONT, HintColor, TextAnchor.MiddleCenter);
            hint.gameObject.SetActive(false);

            PrepareWrap(title);
            PrepareWrap(description);
            PrepareWrap(hint);
            PrepareButtonLabel(continueLabel);
            foreach (var label in choiceLabels) PrepareButtonLabel(label);

            // 같은 참조를 한 번만 옮긴다. 여전히 body 아래라 Navigation의 IsChildOf(body)가 그대로 성립한다.
            description.transform.SetParent(content, false);
            foreach (var choice in choices) choice.transform.SetParent(content, false);

            trackedTexts.Add(title);
            trackedTexts.Add(description);
            foreach (var label in choiceLabels) if (label != null) trackedTexts.Add(label);
            if (continueLabel != null) trackedTexts.Add(continueLabel);
        }

        private void BuildScroll()
        {
            var viewportGo = CreateRect(panel, "ReadingViewport", Vector2.one * 0.5f, Vector2.one * 0.5f,
                Vector2.one * 0.5f, Vector2.zero, new Vector2(basePanel.x - SIDE_INSET * 2f, INITIAL_VIEWPORT_HEIGHT));
            viewportGo.AddComponent<Image>().color = Color.clear;   // 빈 자리 드래그를 받는다
            viewportGo.AddComponent<RectMask2D>();
            viewport = (RectTransform)viewportGo.transform;

            var contentGo = CreateRect(viewport, "ReadingContent", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f), Vector2.zero, viewport.sizeDelta);
            content = (RectTransform)contentGo.transform;

            readingScroll = viewportGo.AddComponent<ScrollRect>();
            readingScroll.viewport = viewport;
            readingScroll.content = content;
            readingScroll.horizontal = false;
            readingScroll.vertical = true;
            readingScroll.movementType = ScrollRect.MovementType.Clamped;
            readingScroll.inertia = false;
            // 휠은 Input.cs의 LateUpdate가 직접 처리한다 — 0이 아니면 한 번 굴려 두 번 움직인다.
            readingScroll.scrollSensitivity = 0f;

            var barRoot = CreateRect(viewport, "ReadingScrollbar", new Vector2(1f, 0f), new Vector2(1f, 1f),
                new Vector2(1f, 0.5f), new Vector2(-2f, 0f), new Vector2(8f, 0f));
            barRoot.AddComponent<Image>().color = new Color(0.2f, 0.2f, 0.3f, 0.8f);
            var handle = CreateRect(barRoot.transform, "Handle", Vector2.zero, Vector2.one, Vector2.one * 0.5f,
                Vector2.zero, Vector2.zero);
            Stretch((RectTransform)handle.transform);
            var handleImage = handle.AddComponent<Image>();
            handleImage.color = new Color(0.7f, 0.66f, 0.85f);
            var bar = barRoot.AddComponent<Scrollbar>();
            bar.targetGraphic = handleImage;
            bar.handleRect = (RectTransform)handle.transform;
            bar.direction = Scrollbar.Direction.BottomToTop;
            bar.navigation = new Navigation { mode = Navigation.Mode.None };   // 포커스 경로에 끼지 않는다
            readingScroll.verticalScrollbar = bar;
            readingScroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
        }

        private static void PrepareWrap(Text text)
        {
            if (text == null) return;
            text.resizeTextForBestFit = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
        }

        private static void PrepareButtonLabel(Text label)
        {
            if (label == null) return;
            PrepareWrap(label);
            // 버튼에 늘어붙은 라벨 — 좌우만 들여 글자가 테두리에 닿지 않게 한다.
            label.rectTransform.offsetMin = new Vector2(LABEL_INSET, 0f);
            label.rectTransform.offsetMax = new Vector2(-LABEL_INSET, 0f);
        }

        /// <summary>새 이벤트·새 결과. 맨 위부터 읽게 하고, 방금 적용된 그림이 원래 보이는 그림인지 기록한다.</summary>
        public void ResetReading()
        {
            isResetPending = true;
            ResetSelectionTracking();
            // ApplyEventArtwork 직후다 — enabled는 그쪽이 방금 정했다(그림 없음 = false). 낡은 sprite로 추정하지 않는다.
            var image = FindArtwork();
            isArtSourceVisible = image != null && image.enabled;
            if (content != null) SetScroll(0f);
        }

        /// <summary>
        /// 현재 글자·배율·화면 크기로 다시 배치한다. 새 이벤트/결과는 맨 위, 배율·크기 변화는 비율 위치, 그 밖은 절대 위치를 유지한다.
        /// 재배치 직전에 보이던 선택 버튼만 재배치 뒤 한 번 다시 보이게 예약한다(Input.cs). 사용자가 화면 밖으로 굴려 둔 선택은 끌어오지 않는다.
        /// </summary>
        public void Apply()
        {
            if (panel == null || readingScroll == null) return;
            float scale = MenuTextReadability.Scale;
            float oldY = content.anchoredPosition.y;
            float oldRange = ScrollRange();
            float oldNormalized = oldRange > 0f ? oldY / oldRange : 0f;
            var visibleSelection = isResetPending ? null : CaptureVisibleSelection();

            ApplyFonts(scale);
            hint.text = HintText(CurrentLanguage());

            Vector2 canvasSize = CanvasSize();
            float panelWidth = Mathf.Min(basePanel.x, Mathf.Max(1f, canvasSize.x - SAFE_MARGIN * 2f));
            float maxHeight = Mathf.Max(1f, canvasSize.y - SAFE_MARGIN * 2f);
            float viewportWidth = Mathf.Max(1f, panelWidth - SIDE_INSET * 2f);
            float titleWidth = Mathf.Max(1f, panelWidth - (basePanel.x - baseTitle.x));
            float descriptionWidth = Mathf.Max(1f, panelWidth - (basePanel.x - baseDescription.x));
            float choiceWidth = Mathf.Max(1f, panelWidth - (basePanel.x - baseChoice.x));
            float continueWidth = Mathf.Min(baseContinue.x, titleWidth);

            float titleHeight = MeasureText(title, titleWidth, baseTitle.y);
            bool hasContinue = continueButton.gameObject.activeSelf;
            var continueRect = (RectTransform)continueButton.transform;
            float continueHeight = hasContinue
                ? MeasureButton(continueRect, continueLabel, continueWidth, baseContinue.y)
                : 0f;
            float contentHeight = LayoutContent(descriptionWidth, choiceWidth);

            float header = TOP_PADDING + titleHeight + HEADER_GAP;
            float footer = BOTTOM_PADDING + (hasContinue ? continueHeight + FOOTER_GAP : 0f);
            float panelHeight = Mathf.Min(Mathf.Max(basePanel.y, header + contentHeight + footer), maxHeight);

            // 넘칠 때만 스크롤 안내를 바닥에 더한다. 보기 영역은 실제로 남는 높이만 쓴다(인위적 최소 높이로 캔버스를 넘지 않는다).
            bool isOverflowing = contentHeight > panelHeight - header - footer + 0.5f;
            float hintBlock = isOverflowing ? MeasureText(hint, titleWidth, 20f) + FOOTER_GAP : 0f;
            float chrome = header + footer + hintBlock;
            float viewportHeight = Mathf.Max(MIN_VIEWPORT_HEIGHT, panelHeight - chrome);
            panelHeight = chrome + viewportHeight;
            WarnIfChromeOverflows(panelHeight, maxHeight);

            panel.sizeDelta = new Vector2(panelWidth, panelHeight);
            float top = panelHeight * 0.5f;
            PlaceCentered(title.rectTransform, titleWidth, titleHeight, top - TOP_PADDING - titleHeight * 0.5f);

            float viewportTop = top - header;
            PlaceCentered(viewport, viewportWidth, viewportHeight, viewportTop - viewportHeight * 0.5f);
            content.sizeDelta = new Vector2(viewportWidth, Mathf.Max(viewportHeight, contentHeight));

            float cursor = viewportTop - viewportHeight;
            hint.gameObject.SetActive(isOverflowing);
            if (isOverflowing)
            {
                float hintHeight = hintBlock - FOOTER_GAP;
                cursor -= FOOTER_GAP;
                PlaceCentered(hint.rectTransform, titleWidth, hintHeight, cursor - hintHeight * 0.5f);
                cursor -= hintHeight;
            }
            if (hasContinue)
            {
                cursor -= FOOTER_GAP;
                PlaceCentered(continueRect, continueWidth, continueHeight, cursor - continueHeight * 0.5f);
            }

            float range = ScrollRange();
            float y = isResetPending ? 0f : isNormalizedPreserved ? oldNormalized * range : oldY;
            isResetPending = false;
            isNormalizedPreserved = false;
            SetScroll(y);
            if (visibleSelection != null) ScheduleReflowReveal(visibleSelection);

            ApplyArtwork(canvasSize, panelWidth, panelHeight);
            Snapshot(scale, canvasSize);
        }

        /// <summary>배율·화면·언어·글자·표시 상태가 마지막 배치와 다를 때만 다시 배치한다.</summary>
        public void RefreshIfNeeded()
        {
            if (panel == null || readingScroll == null) return;
            Vector2 canvasSize = CanvasSize();
            bool isRescaled = appliedScale != MenuTextReadability.Scale ||
                              appliedCanvasSize != canvasSize ||
                              appliedCanvasScale != CanvasScaleFactor();
            bool hasChanged = isRescaled ||
                              appliedActiveMask != ActiveMask() ||
                              appliedInteractableMask != InteractableMask() ||
                              appliedLanguage != CurrentLanguage() ||
                              HasTextChanged();
            if (!hasChanged) return;
            if (isRescaled) isNormalizedPreserved = true;
            Apply();
        }

        private void ApplyFonts(float scale)
        {
            title.fontSize = Mathf.RoundToInt(titleFont * scale);
            description.fontSize = Mathf.RoundToInt(descriptionFont * scale);
            description.lineSpacing = DESCRIPTION_LINE_SPACING;
            hint.fontSize = Mathf.RoundToInt(HINT_FONT * scale);
            foreach (var label in choiceLabels) if (label != null) label.fontSize = Mathf.RoundToInt(choiceFont * scale);
            if (continueLabel != null) continueLabel.fontSize = Mathf.RoundToInt(continueFont * scale);
        }

        /// <summary>본문 아래로 보이는 선택지를 차례로 쌓는다. 필요한 내용 높이를 돌려준다.</summary>
        private float LayoutContent(float descriptionWidth, float choiceWidth)
        {
            float offset = CONTENT_PADDING;
            float descriptionHeight = string.IsNullOrEmpty(description.text)
                ? 0f
                : MeasureText(description, descriptionWidth, 0f);
            PlaceFromTop(description.rectTransform, descriptionWidth, descriptionHeight, offset);
            offset += descriptionHeight;

            bool isFirst = true;
            for (int i = 0; i < choices.Count; i++)
            {
                if (!choices[i].gameObject.activeSelf) continue;
                offset += isFirst ? DESCRIPTION_CHOICE_GAP : CHOICE_GAP;
                isFirst = false;
                var rect = (RectTransform)choices[i].transform;
                float height = MeasureButton(rect, choiceLabels[i], choiceWidth, baseChoice.y);
                PlaceFromTop(rect, choiceWidth, height, offset);
                offset += height;
            }
            return offset + CONTENT_PADDING;
        }

        /// <summary>제목·안내·[계속]만으로 캔버스를 넘는 병적 경우. 글자를 자르지 않으므로 맞출 수 없다 — 상태가 바뀔 때 한 번만 알린다.</summary>
        private void WarnIfChromeOverflows(float panelHeight, float maxHeight)
        {
            bool isOverflowing = panelHeight > maxHeight + 0.5f;
            if (isOverflowing && !hasWarnedChromeOverflow)
                Debug.LogWarning($"[EventRoomReadability] 고정 머리·바닥({panelHeight:0})이 캔버스 여유({maxHeight:0})보다 크다 — 패널이 화면을 넘는다.");
            hasWarnedChromeOverflow = isOverflowing;
        }

        private void ApplyArtwork(Vector2 canvasSize, float panelWidth, float panelHeight)
        {
            var image = FindArtwork();
            // 원래 보이지 않는 그림(그림 없음·낡은 초상)은 여기서 절대 켜지 않는다.
            if (image == null || !isArtSourceVisible) return;

            float halfWidth = canvasSize.x * 0.5f - SAFE_MARGIN;
            float halfHeight = canvasSize.y * 0.5f - SAFE_MARGIN;
            float right = -panelWidth * 0.5f - ART_GAP;
            float left = right - ArtSize.x;
            if (left < -halfWidth || ArtSize.y > halfHeight * 2f)
            {
                image.enabled = false;   // 자리가 없으면 이미지만 끈다(오브젝트·참조는 그대로)
                return;
            }

            float centerY = Mathf.Clamp(panelHeight * 0.5f - ART_TOP_INSET - ArtSize.y * 0.5f,
                -halfHeight + ArtSize.y * 0.5f, halfHeight - ArtSize.y * 0.5f);
            var rect = image.rectTransform;
            rect.anchorMin = rect.anchorMax = Vector2.one * 0.5f;
            rect.pivot = Vector2.one * 0.5f;
            rect.sizeDelta = ArtSize;
            rect.anchoredPosition = new Vector2(left + ArtSize.x * 0.5f, centerY);
            image.enabled = true;
        }

        private static float MeasureText(Text text, float width, float minHeight)
        {
            var rect = text.rectTransform;
            rect.sizeDelta = new Vector2(width, rect.sizeDelta.y);   // 줄바꿈 폭을 먼저 정해야 높이가 맞다
            return Mathf.Max(minHeight, text.preferredHeight);
        }

        private static float MeasureButton(RectTransform button, Text label, float width, float minHeight)
        {
            button.sizeDelta = new Vector2(width, button.sizeDelta.y);
            if (label == null) return minHeight;
            return Mathf.Max(minHeight, label.preferredHeight + LABEL_PADDING);
        }

        private static void PlaceCentered(RectTransform rect, float width, float height, float centerY)
        {
            rect.anchorMin = rect.anchorMax = Vector2.one * 0.5f;
            rect.pivot = Vector2.one * 0.5f;
            rect.sizeDelta = new Vector2(width, height);
            rect.anchoredPosition = new Vector2(0f, centerY);
        }

        private static void PlaceFromTop(RectTransform rect, float width, float height, float offset)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = Vector2.one * 0.5f;
            rect.sizeDelta = new Vector2(width, height);
            rect.anchoredPosition = new Vector2(0f, -offset - height * 0.5f);
        }

        private float ScrollRange() =>
            content == null ? 0f : Mathf.Max(0f, content.sizeDelta.y - viewport.sizeDelta.y);

        private void SetScroll(float y)
        {
            readingScroll.StopMovement();
            content.anchoredPosition = new Vector2(0f, Mathf.Clamp(y, 0f, ScrollRange()));
        }

        private void Snapshot(float scale, Vector2 canvasSize)
        {
            appliedScale = scale;
            appliedCanvasSize = canvasSize;
            appliedCanvasScale = CanvasScaleFactor();
            appliedLanguage = CurrentLanguage();
            appliedActiveMask = ActiveMask();
            appliedInteractableMask = InteractableMask();
            appliedTexts.Clear();
            appliedHeights.Clear();
            foreach (var text in trackedTexts)
            {
                appliedTexts.Add(text.text);
                appliedHeights.Add(text.preferredHeight);
            }
        }

        private bool HasTextChanged()
        {
            if (appliedTexts.Count != trackedTexts.Count) return true;
            for (int i = 0; i < trackedTexts.Count; i++)
            {
                var text = trackedTexts[i];
                if (appliedTexts[i] != text.text) return true;
                if (Mathf.Abs(appliedHeights[i] - text.preferredHeight) > 0.01f) return true;
            }
            return false;
        }

        private int ActiveMask()
        {
            int mask = continueButton.gameObject.activeSelf ? 1 << CONTINUE_MASK_BIT : 0;
            for (int i = 0; i < choices.Count; i++) if (choices[i].gameObject.activeSelf) mask |= 1 << i;
            return mask;
        }

        private int InteractableMask()
        {
            int mask = 0;
            for (int i = 0; i < choices.Count; i++) if (choices[i].interactable) mask |= 1 << i;
            return mask;
        }

        private Vector2 CanvasSize()
        {
            Vector2 size = canvasRect != null ? canvasRect.rect.size : Vector2.zero;
            // 캔버스가 아직 크기를 못 받았으면 기준 패널만큼만 가정한다 — 그림은 숨고, 크기가 잡히면 다시 배치된다.
            if (size.x < 1f || size.y < 1f) size = basePanel + Vector2.one * SAFE_MARGIN * 2f;
            return size;
        }

        private float CanvasScaleFactor() => rootCanvas != null ? rootCanvas.scaleFactor : 1f;

        private Image FindArtwork()
        {
            if (artwork != null || panel == null) return artwork;
            var child = panel.Find(ART_NAME);
            if (child != null) child.TryGetComponent(out artwork);
            return artwork;
        }

        private static LocalizationLanguage CurrentLanguage()
        {
            var manager = LocalizationManager.GetInstanceSafe();
            return manager != null ? manager.CurrentLanguage : LocalizationLanguage.Korean;
        }

        private static string HintText(LocalizationLanguage language) => language switch
        {
            LocalizationLanguage.English => "Wheel / PgUp/Dn / R-stick · Home: top",
            LocalizationLanguage.Japanese => "ホイール・PgUp/Dn・右スティック / Home:先頭",
            _ => "휠·PgUp/Dn·우스틱 / Home: 처음",
        };
    }
}
