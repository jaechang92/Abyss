using Abyss.Runtime.ArtIntegration;
using Abyss.Runtime.Localization;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Abyss.Runtime.UI
{
    /// <summary>
    /// 저장 상태 오버레이(<see cref="SaveStatusOverlay"/>)의 글자 확대·전체 줄바꿈.
    /// 보류 모달은 제목·하단 버튼을 고정하고 본문·경로·재시도 결과만 한 스크롤 영역에 둔다.
    /// 짧은 문구는 원래 좌표·간격 그대로이고, 화면(루트 캔버스 로컬 크기)에 다 안 들어갈 때만 스크롤한다.
    /// 「저장 안 됨」 표시는 오른쪽 경계(-16)를 지킨 채 왼쪽·아래로 자라고, 토스트는 하단 기준으로 위로 자란다.
    /// 기준값(크기·위치·글자 크기)은 부착할 때 한 번만 잡는다 — 다시 적용해도 누적되지 않는다.
    /// Text·Button 참조, 콜백·Navigation·포커스·색·활성 상태·토스트 타이머는 건드리지 않는다.
    /// 스크롤 입력은 저장 모달이 떠 있을 때만 받는다(모달 자신이 입력 주인이라 IsCapturingInput 으로 막지 않는다).
    /// </summary>
    public sealed class SaveStatusReadability : MonoBehaviour
    {
        private const float ROOT_MARGIN = 16f;
        private const float PANEL_EXTRA_WIDTH = 200f;
        private const float TITLE_ICON_SIZE = 36f;
        private const float SMALL_ICON_SIZE = 28f;
        private const float ICON_GAP = 10f;
        private const float BADGE_RIGHT_PADDING = 12f;
        private const float BAR_WIDTH = 6f;
        private const float BAR_OFFSET = 12f;
        private const float SCROLL_SPEED = 420f;
        private const float WHEEL_FACTOR = 0.5f;

        // ── 대상(고정 경로에서 한 번만 찾는다) ──
        private RectTransform root;
        private Canvas canvas;
        private GameObject modalBody;
        private RectTransform panel;
        private Text title;
        private Text body;
        private Text path;
        private Text retry;
        private RectTransform retryButton;
        private RectTransform continueButton;
        private Text retryButtonLabel;
        private Text continueButtonLabel;
        private RectTransform badgeRect;
        private Text badgeLabel;
        private RectTransform toastRect;
        private Text toastLabel;
        // ── 스크롤 ──
        private ScrollRect readingScroll;
        private Scrollbar readingBar;
        private bool isOverflowing;
        // ── 기준값 ──
        private Vector2 panelSize;
        private Vector2 titlePosition;
        private Vector2 titleSize;
        private Vector2 bodySize;
        private Vector2 pathSize;
        private Vector2 retrySize;
        private Vector2 buttonSize;
        private Vector2 retryButtonPosition;
        private Vector2 badgeSize;
        private Vector2 badgePosition;
        private Vector2 badgeLabelOffsetMin;
        private Vector2 badgeLabelOffsetMax;
        private Vector2 toastSize;
        private Vector2 toastPosition;
        private Vector2 toastLabelSize;
        private Vector2 toastLabelPosition;
        private int titleFont;
        private int bodyFont;
        private int pathFont;
        private int retryFont;
        private int retryButtonFont;
        private int continueButtonFont;
        private int badgeFont;
        private int toastFont;
        // 원래 배치에서 읽은 간격(패널 위→제목, 제목→본문, 본문→경로, 경로→결과, 결과→버튼, 버튼→패널 아래, 버튼 사이).
        private float headerTop;
        private float titleGap;
        private float bodyPathGap;
        private float pathRetryGap;
        private float readingFooterGap;
        private float footerBottom;
        private float buttonGapX;
        private float textInset;
        // ── 변화 감지 ──
        private Text[] watched;
        private string[] watchedContents;
        private float[] watchedWidths;
        private float lastScale = -1f;
        private float lastCanvasScale = -1f;
        private Vector2 lastRootSize;
        private LocalizationLanguage lastLanguage;
        private int lastReadingSignature;
        private bool hasApplied;
        private bool initialized;

        /// <summary>
        /// BuildUI 가 끝난 뒤 한 번 부른다. 다시 불러도 같은 헬퍼를 돌려준다.
        /// 모달 경로가 하나라도 없으면 아무것도 붙이지 않고 null.
        /// </summary>
        public static SaveStatusReadability Attach(Transform overlayRoot)
        {
            if (overlayRoot == null) return null;
            var existing = overlayRoot.GetComponent<SaveStatusReadability>();
            if (existing != null) return existing;

            var panel = overlayRoot.Find("Body/Panel") as RectTransform;
            if (panel == null) return null;
            var title = FindText(panel, "TitleText");
            var body = FindText(panel, "BodyText");
            var path = FindText(panel, "PathText");
            var retry = FindText(panel, "RetryResultText");
            var retryButton = panel.Find("RetryButton") as RectTransform;
            var continueButton = panel.Find("ContinueButton") as RectTransform;
            if (title == null || body == null || path == null || retry == null || retryButton == null || continueButton == null) return null;
            var retryLabel = FindText(retryButton, "Text");
            var continueLabel = FindText(continueButton, "Text");
            if (retryLabel == null || continueLabel == null) return null;

            var helper = overlayRoot.gameObject.AddComponent<SaveStatusReadability>();
            helper.root = overlayRoot as RectTransform;
            helper.canvas = overlayRoot.GetComponent<Canvas>();
            helper.modalBody = panel.parent.gameObject;
            helper.panel = panel;
            helper.title = title;
            helper.body = body;
            helper.path = path;
            helper.retry = retry;
            helper.retryButton = retryButton;
            helper.continueButton = continueButton;
            helper.retryButtonLabel = retryLabel;
            helper.continueButtonLabel = continueLabel;
            helper.badgeRect = overlayRoot.Find("Badge") as RectTransform;
            helper.badgeLabel = helper.badgeRect != null ? FindText(helper.badgeRect, "Text") : null;
            helper.toastRect = overlayRoot.Find("Toast") as RectTransform;
            helper.toastLabel = helper.toastRect != null ? FindText(helper.toastRect, "ToastText") : null;

            helper.CaptureBaseline();
            helper.BuildScroll();
            helper.initialized = true;
            helper.Apply();
            return helper;
        }

        private static Text FindText(Transform parent, string name)
        {
            var child = parent.Find(name);
            return child != null ? child.GetComponent<Text>() : null;
        }

        private void CaptureBaseline()
        {
            panelSize = panel.sizeDelta;
            titlePosition = title.rectTransform.anchoredPosition;
            titleSize = title.rectTransform.sizeDelta;
            bodySize = body.rectTransform.sizeDelta;
            pathSize = path.rectTransform.sizeDelta;
            retrySize = retry.rectTransform.sizeDelta;
            buttonSize = retryButton.sizeDelta;
            retryButtonPosition = retryButton.anchoredPosition;
            titleFont = title.fontSize;
            bodyFont = body.fontSize;
            pathFont = path.fontSize;
            retryFont = retry.fontSize;
            retryButtonFont = retryButtonLabel.fontSize;
            continueButtonFont = continueButtonLabel.fontSize;

            float panelTop = panelSize.y * 0.5f;
            var bodyPosition = body.rectTransform.anchoredPosition;
            var pathPosition = path.rectTransform.anchoredPosition;
            var retryPosition = retry.rectTransform.anchoredPosition;
            headerTop = panelTop - (titlePosition.y + titleSize.y * 0.5f);
            titleGap = (titlePosition.y - titleSize.y * 0.5f) - (bodyPosition.y + bodySize.y * 0.5f);
            bodyPathGap = (bodyPosition.y - bodySize.y * 0.5f) - (pathPosition.y + pathSize.y * 0.5f);
            pathRetryGap = (pathPosition.y - pathSize.y * 0.5f) - (retryPosition.y + retrySize.y * 0.5f);
            readingFooterGap = (retryPosition.y - retrySize.y * 0.5f) - (retryButtonPosition.y + buttonSize.y * 0.5f);
            footerBottom = (retryButtonPosition.y - buttonSize.y * 0.5f) + panelTop;
            buttonGapX = Mathf.Abs(continueButton.anchoredPosition.x - retryButtonPosition.x) - buttonSize.x;
            textInset = panelSize.x - bodySize.x;

            if (badgeRect != null && badgeLabel != null)
            {
                badgeSize = badgeRect.sizeDelta;
                badgePosition = badgeRect.anchoredPosition;
                badgeLabelOffsetMin = badgeLabel.rectTransform.offsetMin;
                badgeLabelOffsetMax = badgeLabel.rectTransform.offsetMax;
                badgeFont = badgeLabel.fontSize;
            }
            if (toastRect != null && toastLabel != null)
            {
                toastSize = toastRect.sizeDelta;
                toastPosition = toastRect.anchoredPosition;
                toastLabelSize = toastLabel.rectTransform.sizeDelta;
                toastLabelPosition = toastLabel.rectTransform.anchoredPosition;
                toastFont = toastLabel.fontSize;
            }

            watched = new[] { title, body, path, retry, retryButtonLabel, continueButtonLabel, badgeLabel, toastLabel };
            watchedContents = new string[watched.Length];
            watchedWidths = new float[watched.Length];
        }

        /// <summary>본문·경로·재시도 결과를 같은 Text 참조 그대로 스크롤 내용으로 옮긴다. 부착 때 한 번뿐이다.</summary>
        private void BuildScroll()
        {
            var center = new Vector2(0.5f, 0.5f);
            var top = new Vector2(0.5f, 1f);
            var viewport = UiFactory.CreateRect(panel, "SaveReadingViewport", center, center, top, Vector2.zero, Vector2.one);
            viewport.transform.SetSiblingIndex(body.transform.GetSiblingIndex());
            // 투명 면 — 끌기(드래그)를 받는 자리. 안의 글자들은 raycastTarget=false 다.
            var surface = viewport.AddComponent<Image>();
            surface.color = new Color(1f, 1f, 1f, 0f);
            surface.raycastTarget = true;
            viewport.AddComponent<RectMask2D>();

            readingScroll = viewport.AddComponent<ScrollRect>();
            readingScroll.viewport = (RectTransform)viewport.transform;
            readingScroll.content = (RectTransform)UiFactory.CreateRect(viewport.transform, "Content", top, top, top, Vector2.zero, Vector2.one).transform;
            readingScroll.horizontal = false;
            readingScroll.vertical = true;
            readingScroll.movementType = ScrollRect.MovementType.Clamped;
            // 휠은 아래 LateUpdate 가 포인터 위치를 보고 직접 처리한다 — 0 이 아니면 두 번 굴러간다.
            readingScroll.scrollSensitivity = 0f;

            foreach (var text in new[] { body, path, retry })
            {
                var rect = text.rectTransform;
                rect.SetParent(readingScroll.content, false);
                rect.anchorMin = rect.anchorMax = rect.pivot = top;
            }

            // 포인터로 끌고 누를 수 있는 막대. Navigation=None 이라 눌러도 선택되지 않는다(키보드·패드 포커스 유지).
            var bar = UiFactory.CreateRect(panel, "SaveReadingScrollbar", center, center, top, Vector2.zero, new Vector2(BAR_WIDTH, 1f));
            var track = bar.AddComponent<Image>();
            track.color = new Color(1f, 1f, 1f, 0.15f);
            track.raycastTarget = true;
            readingBar = bar.AddComponent<Scrollbar>();
            readingBar.direction = Scrollbar.Direction.BottomToTop;
            readingBar.navigation = new Navigation { mode = Navigation.Mode.None };
            readingBar.transition = Selectable.Transition.None;
            var handle = UiFactory.CreateRect(bar.transform, "Handle", Vector2.zero, Vector2.one, center, Vector2.zero, Vector2.zero);
            var handleImage = handle.AddComponent<Image>();
            handleImage.color = new Color(0.9f, 0.88f, 0.8f, 0.8f);
            handleImage.raycastTarget = true;
            readingBar.handleRect = (RectTransform)handle.transform;
            readingBar.targetGraphic = handleImage;
            readingScroll.verticalScrollbar = readingBar;
            readingScroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
            bar.SetActive(false);
        }

        /// <summary>현재 배율·루트 크기·글자로 전부 다시 배치한다. 기준값에서 계산하므로 몇 번 불러도 같다.</summary>
        public void Apply()
        {
            if (!initialized) return;
            float scale = MenuTextReadability.Scale;
            var area = RootSize();
            float availableWidth = Mathf.Max(1f, area.x - ROOT_MARGIN * 2f);
            float availableHeight = Mathf.Max(1f, area.y - ROOT_MARGIN * 2f);
            int readingSignature = ReadingSignature();
            bool resetScroll = !hasApplied || readingSignature != lastReadingSignature;
            float previous = readingScroll.verticalNormalizedPosition;

            ApplyModal(scale, availableWidth, availableHeight);
            ApplyBadge(scale, availableWidth);
            ApplyToast(scale, availableWidth, area.y);

            readingScroll.StopMovement();
            readingScroll.verticalNormalizedPosition = resetScroll ? 1f : previous;
            Remember(scale, area, readingSignature);
        }

        /// <summary>변한 것(배율·언어·루트 크기·글자·선호 크기)이 있을 때만 <see cref="Apply"/>. 매 프레임 재배치하지 않는다.</summary>
        public void RefreshIfNeeded()
        {
            if (!initialized) return;
            if (NeedsApply()) Apply();
        }

        private void ApplyModal(float scale, float availableWidth, float availableHeight)
        {
            float width = Mathf.Min(panelSize.x + PANEL_EXTRA_WIDTH * (scale - 1f), Mathf.Max(textInset + 1f, availableWidth));
            float textWidth = Mathf.Max(1f, width - textInset);

            // 제목 — 앞 아이콘 자리를 양쪽에 남겨 가운데 정렬이 유지되게 한다. 짧으면 원래 폭.
            Style(title, titleFont, scale);
            float titleReserve = (TITLE_ICON_SIZE + ICON_GAP) * 2f;
            float titleWidth = title.preferredWidth + titleReserve <= textWidth ? textWidth : Mathf.Max(1f, textWidth - titleReserve);
            float titleHeight = Mathf.Max(titleSize.y, MeasureHeight(title, titleWidth) + 4f);

            Style(retryButtonLabel, retryButtonFont, scale);
            Style(continueButtonLabel, continueButtonFont, scale);
            float buttonWidth = Mathf.Min(buttonSize.x, Mathf.Max(1f, (textWidth - buttonGapX) * 0.5f));
            float buttonHeight = Mathf.Max(buttonSize.y, MeasureHeight(retryButtonLabel, buttonWidth - 16f) + 12f);
            buttonHeight = Mathf.Max(buttonHeight, MeasureHeight(continueButtonLabel, buttonWidth - 16f) + 12f);

            Style(body, bodyFont, scale);
            Style(path, pathFont, scale);
            Style(retry, retryFont, scale);
            float bodyHeight = Mathf.Max(bodySize.y, MeasureHeight(body, textWidth) + 4f);
            float pathHeight = Mathf.Max(pathSize.y, MeasureHeight(path, textWidth) + 4f);
            float retryHeight = Mathf.Max(retrySize.y, MeasureHeight(retry, textWidth) + 4f);
            float stack = bodyHeight + bodyPathGap + pathHeight + pathRetryGap + retryHeight;

            float fixedHeight = headerTop + titleHeight + titleGap + readingFooterGap + buttonHeight + footerBottom;
            float maxReading = Mathf.Max(1f, availableHeight - fixedHeight);
            isOverflowing = stack > maxReading + 0.5f;
            float reading = isOverflowing ? maxReading : stack;
            float height = fixedHeight + reading;
            panel.sizeDelta = new Vector2(width, height);
            float top = height * 0.5f;

            title.rectTransform.sizeDelta = new Vector2(titleWidth, titleHeight);
            title.rectTransform.anchoredPosition = new Vector2(titlePosition.x, top - headerTop - titleHeight * 0.5f);

            float readingTop = top - headerTop - titleHeight - titleGap;
            readingScroll.viewport.anchoredPosition = new Vector2(0f, readingTop);
            readingScroll.viewport.sizeDelta = new Vector2(textWidth, reading);
            readingScroll.content.sizeDelta = new Vector2(textWidth, stack);
            PlaceInContent(body, 0f, textWidth, bodyHeight);
            PlaceInContent(path, bodyHeight + bodyPathGap, textWidth, pathHeight);
            PlaceInContent(retry, bodyHeight + bodyPathGap + pathHeight + pathRetryGap, textWidth, retryHeight);

            var barRect = (RectTransform)readingBar.transform;
            barRect.anchoredPosition = new Vector2(textWidth * 0.5f + BAR_OFFSET, readingTop);
            barRect.sizeDelta = new Vector2(BAR_WIDTH, reading);
            if (readingBar.gameObject.activeSelf != isOverflowing) readingBar.gameObject.SetActive(isOverflowing);

            float buttonY = -top + footerBottom + buttonHeight * 0.5f;
            float buttonX = (buttonWidth + buttonGapX) * 0.5f;
            float retrySide = retryButtonPosition.x <= 0f ? -1f : 1f;
            retryButton.sizeDelta = continueButton.sizeDelta = new Vector2(buttonWidth, buttonHeight);
            retryButton.anchoredPosition = new Vector2(buttonX * retrySide, buttonY);
            continueButton.anchoredPosition = new Vector2(-buttonX * retrySide, buttonY);

            // 기존 PlaceModalTitleIcon 이 먼저 돌았어도 최종 위치는 여기서 정한다.
            UiArtDecor.PlaceLeadingIcon(title, UiArtKeys.COMMON_SAVE_RECORD, TITLE_ICON_SIZE, ICON_GAP);
        }

        private static void PlaceInContent(Text text, float offset, float width, float height)
        {
            var rect = text.rectTransform;
            rect.anchoredPosition = new Vector2(0f, -offset);
            rect.sizeDelta = new Vector2(width, height);
        }

        /// <summary>
        /// 우상단 표시 — 오른쪽 경계는 고정, 글자가 길면 왼쪽·아래로 자란다(루트 가용 폭을 넘지 않는다).
        /// 글자 칸은 항상 왼쪽 아이콘 자리(10+28+10)와 오른쪽 여백(12)을 비운다.
        /// </summary>
        private void ApplyBadge(float scale, float availableWidth)
        {
            if (badgeRect == null || badgeLabel == null) return;
            Style(badgeLabel, badgeFont, scale);
            float reserve = ICON_GAP + SMALL_ICON_SIZE + ICON_GAP;
            float horizontalPadding = reserve + BADGE_RIGHT_PADDING;
            float desired = Mathf.Max(badgeSize.x, badgeLabel.preferredWidth + 2f + horizontalPadding);
            float width = Mathf.Min(desired, availableWidth);
            float labelWidth = Mathf.Max(1f, width - horizontalPadding);
            float height = Mathf.Max(badgeSize.y, MeasureHeight(badgeLabel, labelWidth) + 8f);

            badgeRect.sizeDelta = new Vector2(width, height);
            badgeRect.anchoredPosition = badgePosition;
            var labelRect = badgeLabel.rectTransform;
            labelRect.offsetMin = new Vector2(reserve, badgeLabelOffsetMin.y);
            labelRect.offsetMax = new Vector2(-BADGE_RIGHT_PADDING, badgeLabelOffsetMax.y);

            // 기존 아이콘을 그대로 옮긴다 — 그림·크기(종횡비)·색은 건드리지 않는다.
            if (badgeRect.Find("ArtSaveIcon") is RectTransform icon)
                icon.anchoredPosition = new Vector2(-width * 0.5f + ICON_GAP + SMALL_ICON_SIZE * 0.5f, 0f);
        }

        /// <summary>
        /// 하단 토스트 — 폭은 루트 가용 폭 안에서, 높이는 글자 전체 높이만큼 자란다(줄이지 않는다).
        /// 원래 위치(아래 96)에서 위가 모자랄 때만 아래로(최소 16) 내린다. 입력·타이머·활성 상태는 그대로다.
        /// </summary>
        private void ApplyToast(float scale, float availableWidth, float rootHeight)
        {
            if (toastRect == null || toastLabel == null) return;
            Style(toastLabel, toastFont, scale);
            float padding = toastSize.x - toastLabelSize.x;
            float reserve = (SMALL_ICON_SIZE + ICON_GAP) * 2f;
            float textWidth = toastLabel.preferredWidth;
            float width = toastSize.x;
            float labelWidth = toastLabelSize.x;
            if (textWidth + reserve > toastLabelSize.x || toastSize.x > availableWidth)
            {
                width = Mathf.Min(Mathf.Max(toastSize.x, textWidth + 2f + reserve + padding), availableWidth);
                labelWidth = Mathf.Max(1f, width - reserve - padding);
            }
            float verticalPadding = toastSize.y - toastLabelSize.y;
            float labelHeight = Mathf.Max(toastLabelSize.y, MeasureHeight(toastLabel, labelWidth) + 4f);
            float height = labelHeight + verticalPadding;
            // 루트(위아래 여백 16)보다 키가 크면 맨 아래(16)에 두고 위로 넘친다 — 글자 칸은 자르지 않는다(화면 한계).
            float bottom = Mathf.Max(ROOT_MARGIN, Mathf.Min(toastPosition.y, rootHeight - ROOT_MARGIN - height));

            toastLabel.rectTransform.sizeDelta = new Vector2(labelWidth, labelHeight);
            toastLabel.rectTransform.anchoredPosition = toastLabelPosition;
            toastRect.sizeDelta = new Vector2(width, height);
            toastRect.anchoredPosition = new Vector2(toastPosition.x, bottom);
            UiArtDecor.PlaceLeadingIcon(toastLabel, UiArtKeys.COMMON_SAVE_RECORD, SMALL_ICON_SIZE, ICON_GAP);
        }

        private static void Style(Text text, int font, float scale)
        {
            text.fontSize = Mathf.RoundToInt(font * scale);
            text.resizeTextForBestFit = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
        }

        private static float MeasureHeight(Text text, float width)
        {
            var settings = text.GetGenerationSettings(new Vector2(Mathf.Max(1f, width), 0f));
            return text.cachedTextGeneratorForLayout.GetPreferredHeight(text.text ?? string.Empty, settings) / text.pixelsPerUnit;
        }

        /// <summary>실제 루트 캔버스의 로컬 크기(화면 픽셀이 아니다). 첫 레이아웃 전이면 기준 해상도.</summary>
        private Vector2 RootSize()
        {
            var size = root != null ? root.rect.size : Vector2.zero;
            return size.x > 0f && size.y > 0f ? size : UiFactory.ReferenceResolution;
        }
        private float CanvasScale() => canvas != null ? canvas.scaleFactor : 1f;

        private int ReadingSignature()
        {
            int hash = (body.text ?? string.Empty).GetHashCode();
            hash = unchecked(hash * 31 + (path.text ?? string.Empty).GetHashCode());
            hash = unchecked(hash * 31 + (retry.text ?? string.Empty).GetHashCode());
            return unchecked(hash * 31 + (int)Loc.CurrentLanguage);
        }

        private void Remember(float scale, Vector2 area, int readingSignature)
        {
            lastScale = scale;
            lastCanvasScale = CanvasScale();
            lastRootSize = area;
            lastLanguage = Loc.CurrentLanguage;
            lastReadingSignature = readingSignature;
            for (int i = 0; i < watched.Length; i++)
            {
                var text = watched[i];
                watchedContents[i] = text != null ? text.text : null;
                watchedWidths[i] = text != null ? text.preferredWidth : 0f;
            }
            hasApplied = true;
        }

        private bool NeedsApply()
        {
            if (!hasApplied || lastScale != MenuTextReadability.Scale || lastLanguage != Loc.CurrentLanguage) return true;
            if (lastRootSize != RootSize() || !Mathf.Approximately(lastCanvasScale, CanvasScale())) return true;
            for (int i = 0; i < watched.Length; i++)
            {
                var text = watched[i];
                if (text == null) continue;
                if (watchedContents[i] != text.text || Mathf.Abs(watchedWidths[i] - text.preferredWidth) > 0.01f) return true;
            }
            return false;
        }

        private void OnEnable() => hasApplied = false;
        private void LateUpdate()
        {
            if (!initialized) return;
            RefreshIfNeeded();
            UpdateScrollInput();
        }

        /// <summary>휠(포인터가 영역 위일 때)·Page Up/Down·Home/End·패드 오른쪽 스틱. 정지 위에서도 돌도록 unscaled.</summary>
        private void UpdateScrollInput()
        {
            if (!isOverflowing || modalBody == null || !modalBody.activeInHierarchy) return;
            float range = readingScroll.content.rect.height - readingScroll.viewport.rect.height;
            if (range <= 0.5f) return;
            float pixels = 0f;
            var mouse = Mouse.current;
            if (mouse != null)
            {
                UnityEngine.Camera eventCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
                if (RectTransformUtility.RectangleContainsScreenPoint(readingScroll.viewport, mouse.position.ReadValue(), eventCamera))
                    pixels += mouse.scroll.ReadValue().y * WHEEL_FACTOR;
            }
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.pageUpKey.isPressed) pixels += SCROLL_SPEED * Time.unscaledDeltaTime;
                if (keyboard.pageDownKey.isPressed) pixels -= SCROLL_SPEED * Time.unscaledDeltaTime;
                if (keyboard.homeKey.wasPressedThisFrame) readingScroll.verticalNormalizedPosition = 1f;
                if (keyboard.endKey.wasPressedThisFrame) readingScroll.verticalNormalizedPosition = 0f;
            }
            var gamepad = Gamepad.current;
            if (gamepad != null) pixels += gamepad.rightStick.ReadValue().y * SCROLL_SPEED * Time.unscaledDeltaTime;
            if (pixels != 0f) readingScroll.verticalNormalizedPosition = Mathf.Clamp01(readingScroll.verticalNormalizedPosition + pixels / range);
        }
    }
}
