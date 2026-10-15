using System.Collections.Generic;
using Abyss.Runtime.Localization;
using UnityEngine;
using UnityEngine.UI;
using static Abyss.Runtime.UI.UiFactory;

namespace Abyss.Runtime.UI
{
    /// <summary>
    /// <see cref="NodeMapPanel"/>의 메뉴 글자 확대·전체 예고 읽기 배치. 제목은 고정 머리, 노드 카드(같은 버튼 참조)는
    /// 하나의 2축 ScrollRect 안에서 한 줄로 흐른다. 카드는 원래 폭 아래로 줄이지 않고, 글자는 자르거나 줄이지 않는다.
    ///
    /// 🔴 클릭·포커스·탐색 규약은 건드리지 않는다. 버튼 복제·교체·onClick 추가 없음, 좌우 Explicit 경로는 Navigation 파일 그대로다.
    ///    제목의 부모(실제 Panel)는 그대로다. 카드는 body 아래에 남아 Navigation의 IsChildOf 판정이 유지된다.
    ///    프레임·호버 면·선택 표식은 카드에 늘어붙은 장식이라 카드 크기만 바꾸고 다시 만들지 않는다.
    /// 이 파일은 구성·배치·상태 비교를 맡는다. 선택 추적·자동 노출·수동 스크롤은 NodeMapReadability.Input.cs가 맡는다.
    /// </summary>
    public sealed partial class NodeMapReadability : MonoBehaviour
    {
        private const float SAFE_MARGIN = 24f;
        private const float TOP_PADDING = 20f;
        private const float HEADER_GAP = 8f;
        private const float FOOTER_GAP = 10f;
        private const float BOTTOM_PADDING = 24f;
        private const float SIDE_INSET = 20f;
        // 경로 그림(64칸)이 카드 위로 32 튀어나오고 선택 표식이 10 바깥으로 나간다 — 맨 앞·끝에서도 잘리지 않는 여백.
        private const float CONTENT_TOP_PADDING = 44f;
        private const float CONTENT_SIDE_PADDING = 26f;
        private const float CONTENT_BOTTOM_PADDING = 26f;   // 끝 스크롤에서 선택 표식 아래(10)와 가로 스크롤바(바닥 2~10) 사이 6을 남긴다
        private const float CARD_GAP = 32f;
        private const float CARD_TOP_PADDING = 36f;      // 카드 위에 걸친 그림 아래 절반(최대 32)을 피한다
        private const float TYPE_TITLE_GAP = 6f;
        private const float TITLE_HINT_GAP = 12f;
        private const float CARD_BOTTOM_PADDING = 24f;
        private const float INITIAL_VIEWPORT_HEIGHT = 80f;
        private const float MIN_VIEWPORT_HEIGHT = 1f;
        private const float SCROLLBAR_THICKNESS = 8f;
        private const int HEADING_FONT = 40;
        private const int TYPE_FONT = 28;
        private const int TITLE_FONT = 28;
        private const int HINT_FONT = 24;
        private const int READING_HINT_FONT = 15;
        private const string ROUTE_ICON_NAME = "ArtRouteIcon";

        private static readonly Color ReadingHintColor = new(0.72f, 0.72f, 0.84f);
        private static readonly Color ScrollTrackColor = new(0.2f, 0.2f, 0.3f, 0.8f);
        private static readonly Color ScrollHandleColor = new(0.7f, 0.66f, 0.85f);

        private RectTransform panel;
        private RectTransform canvasRect;
        private Canvas rootCanvas;
        private Text heading;
        private Text readingHint;
        private readonly List<Button> nodes = new();
        private readonly List<Text> types = new();
        private readonly List<Text> titles = new();
        private readonly List<Text> hints = new();
        private readonly List<Text> trackedTexts = new();
        private ScrollRect readingScroll;
        private RectTransform viewport;
        private RectTransform content;

        // 경로 그림 — ResetReading(Bind 직후)마다 실제 맞춘 크기·표시 여부를 다시 읽는다. 그림 자체는 건드리지 않는다.
        private readonly List<Image> routeIcons = new();
        private readonly List<Vector2> routeIconSizes = new();
        private readonly List<bool> routeIconVisible = new();

        // 기준값 — Attach에서 한 번만 잡는다. 이후 배치는 늘 여기서 다시 계산한다(누적 변경 없음).
        private Vector2 basePanel;
        private Vector2 baseHeading;
        private float cardWidth;
        private float cardMinHeight;
        private float labelWidth;

        // 마지막 배치 시점의 상태. 달라졌을 때만 다시 배치한다.
        private float appliedScale = -1f;
        private float appliedCanvasScale = -1f;
        private Vector2 appliedCanvasSize;
        private LocalizationLanguage appliedLanguage;
        private int appliedActiveMask = -1;
        private readonly List<string> appliedTexts = new();
        private readonly List<float> appliedHeights = new();

        private bool isResetPending;
        private bool hasWarnedChromeOverflow;

        /// <summary>패널 본체(딤 배경)에 붙인다. 노드 카드를 스크롤 내용으로 한 번 옮긴다. 이미 붙어 있으면 그것을 돌려준다.</summary>
        public static NodeMapReadability Attach(Transform modalBody, Text heading, IReadOnlyList<Button> nodes,
                                                IReadOnlyList<Text> types, IReadOnlyList<Text> titles, IReadOnlyList<Text> hints)
        {
            if (modalBody == null || heading == null || nodes == null || types == null || titles == null || hints == null) return null;
            if (heading.transform.parent is not RectTransform panel) return null;
            var existing = modalBody.GetComponent<NodeMapReadability>();
            if (existing != null) return existing;

            var helper = modalBody.gameObject.AddComponent<NodeMapReadability>();
            helper.Build(panel, heading, nodes, types, titles, hints);
            return helper;
        }

        private void Build(RectTransform targetPanel, Text headingText, IReadOnlyList<Button> nodeButtons,
                           IReadOnlyList<Text> nodeTypes, IReadOnlyList<Text> nodeTitles, IReadOnlyList<Text> nodeHints)
        {
            panel = targetPanel;
            heading = headingText;
            rootCanvas = panel.GetComponentInParent<Canvas>();
            if (rootCanvas != null) rootCanvas = rootCanvas.rootCanvas;
            canvasRect = rootCanvas != null ? (RectTransform)rootCanvas.transform : null;

            basePanel = panel.sizeDelta;
            baseHeading = heading.rectTransform.sizeDelta;

            int count = Mathf.Min(nodeButtons.Count, Mathf.Min(nodeTypes.Count, Mathf.Min(nodeTitles.Count, nodeHints.Count)));
            for (int i = 0; i < count; i++)
            {
                if (nodeButtons[i] == null || nodeTypes[i] == null || nodeTitles[i] == null || nodeHints[i] == null) continue;
                nodes.Add(nodeButtons[i]);
                types.Add(nodeTypes[i]);
                titles.Add(nodeTitles[i]);
                hints.Add(nodeHints[i]);
                routeIcons.Add(null);
                routeIconSizes.Add(Vector2.zero);
                routeIconVisible.Add(false);
            }

            var baseCard = nodes.Count > 0 ? ((RectTransform)nodes[0].transform).sizeDelta : new Vector2(384f, 280f);
            cardWidth = baseCard.x;
            cardMinHeight = baseCard.y;
            labelWidth = types.Count > 0 ? types[0].rectTransform.sizeDelta.x : cardWidth - 64f;

            BuildScroll();
            readingHint = CreateLabel(panel, "ReadingHint", Vector2.zero, new Vector2(baseHeading.x, 24f),
                string.Empty, READING_HINT_FONT, ReadingHintColor, TextAnchor.MiddleCenter);
            readingHint.gameObject.SetActive(false);

            PrepareWrap(heading);
            PrepareWrap(readingHint);
            for (int i = 0; i < nodes.Count; i++)
            {
                PrepareWrap(types[i]);
                PrepareWrap(titles[i]);
                PrepareWrap(hints[i]);
            }

            // 같은 참조를 한 번만 옮긴다. 여전히 body 아래라 Navigation의 IsChildOf(body)가 그대로 성립한다.
            foreach (var node in nodes) node.transform.SetParent(content, false);

            trackedTexts.Add(heading);
            for (int i = 0; i < nodes.Count; i++)
            {
                trackedTexts.Add(types[i]);
                trackedTexts.Add(titles[i]);
                trackedTexts.Add(hints[i]);
            }
        }

        private void BuildScroll()
        {
            var viewportGo = CreateRect(panel, "ReadingViewport", Vector2.one * 0.5f, Vector2.one * 0.5f,
                Vector2.one * 0.5f, Vector2.zero, new Vector2(basePanel.x - SIDE_INSET * 2f, INITIAL_VIEWPORT_HEIGHT));
            viewportGo.AddComponent<Image>().color = Color.clear;   // 빈 자리 드래그를 받는다
            viewportGo.AddComponent<RectMask2D>();                   // 스크롤 내용만 자른다(제목은 Panel 자식이라 밖)
            viewport = (RectTransform)viewportGo.transform;

            // 왼쪽 위 기준 — anchoredPosition = (-가로 위치, 세로 위치).
            var contentGo = CreateRect(viewport, "ReadingContent", new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(0f, 1f), Vector2.zero, viewport.sizeDelta);
            content = (RectTransform)contentGo.transform;

            readingScroll = viewportGo.AddComponent<ScrollRect>();
            readingScroll.viewport = viewport;
            readingScroll.content = content;
            readingScroll.horizontal = true;
            readingScroll.vertical = true;
            readingScroll.movementType = ScrollRect.MovementType.Clamped;
            readingScroll.inertia = false;
            // 휠은 Input.cs의 LateUpdate가 직접 처리한다 — 0이 아니면 한 번 굴려 두 번 움직인다.
            readingScroll.scrollSensitivity = 0f;

            readingScroll.verticalScrollbar = BuildScrollbar("ReadingScrollbarV", new Vector2(1f, 0f), Vector2.one,
                new Vector2(1f, 0.5f), new Vector2(-2f, 0f), new Vector2(SCROLLBAR_THICKNESS, 0f),
                Scrollbar.Direction.BottomToTop);
            readingScroll.horizontalScrollbar = BuildScrollbar("ReadingScrollbarH", Vector2.zero, new Vector2(1f, 0f),
                new Vector2(0.5f, 0f), new Vector2(0f, 2f), new Vector2(0f, SCROLLBAR_THICKNESS),
                Scrollbar.Direction.LeftToRight);
            readingScroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            readingScroll.horizontalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
        }

        private Scrollbar BuildScrollbar(string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
                                         Vector2 position, Vector2 size, Scrollbar.Direction direction)
        {
            var barRoot = CreateRect(viewport, name, anchorMin, anchorMax, pivot, position, size);
            barRoot.AddComponent<Image>().color = ScrollTrackColor;
            var handle = CreateRect(barRoot.transform, "Handle", Vector2.zero, Vector2.one, Vector2.one * 0.5f,
                Vector2.zero, Vector2.zero);
            Stretch((RectTransform)handle.transform);
            var handleImage = handle.AddComponent<Image>();
            handleImage.color = ScrollHandleColor;
            var bar = barRoot.AddComponent<Scrollbar>();
            bar.targetGraphic = handleImage;
            bar.handleRect = (RectTransform)handle.transform;
            bar.direction = direction;
            // None — 포커스 경로에 끼지 않고, 눌러도 선택(포커스)을 가져가지 않는다(Selectable.OnPointerDown 규약).
            bar.navigation = new Navigation { mode = Navigation.Mode.None };
            return bar;
        }

        private static void PrepareWrap(Text text)
        {
            if (text == null) return;
            text.resizeTextForBestFit = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
        }

        /// <summary>
        /// 새로 연 갈림길. 처음(가로·세로 맨 앞)부터 읽게 하고, Bind가 방금 맞춘 경로 그림의 실제 크기·표시 여부를 다시 읽는다.
        /// Bind 뒤, Apply 앞에 부른다.
        /// </summary>
        public void ResetReading()
        {
            isResetPending = true;
            ResetSelectionTracking();
            CaptureRouteIcons();
            if (content != null) SetScroll(0f, 0f);
        }

        /// <summary>
        /// 현재 글자·배율·화면 크기로 다시 배치한다. 새로 연 갈림길은 처음부터, 그 밖의 재배치는 가로·세로 비율 위치를 유지한다.
        /// 재배치 직전에 가로로 보이던 선택 카드만 재배치 뒤 한 번 가로로만 다시 보이게 예약한다(Input.cs). 세로는 끌어오지 않는다.
        /// </summary>
        public void Apply()
        {
            if (panel == null || readingScroll == null) return;
            float scale = MenuTextReadability.Scale;
            float oldX = Normalized(ScrollX(), RangeX());
            float oldY = Normalized(ScrollY(), RangeY());
            var visibleSelection = isResetPending ? null : CaptureHorizontallyVisibleSelection();

            ApplyFonts(scale);
            readingHint.text = HintText(CurrentLanguage());

            Vector2 canvasSize = CanvasSize();
            float panelWidth = Mathf.Min(basePanel.x, Mathf.Max(1f, canvasSize.x - SAFE_MARGIN * 2f));
            float maxHeight = Mathf.Max(1f, canvasSize.y - SAFE_MARGIN * 2f);
            float viewportWidth = Mathf.Max(1f, panelWidth - SIDE_INSET * 2f);
            float headingWidth = Mathf.Max(1f, panelWidth - (basePanel.x - baseHeading.x));
            float headingHeight = MeasureText(heading, headingWidth, baseHeading.y);

            float cardHeight = MeasureCards(out int activeCount);
            float rowWidth = activeCount * cardWidth + Mathf.Max(0, activeCount - 1) * CARD_GAP;
            float contentWidth = rowWidth + CONTENT_SIDE_PADDING * 2f;
            float contentHeight = CONTENT_TOP_PADDING + cardHeight + CONTENT_BOTTOM_PADDING;

            float header = TOP_PADDING + headingHeight + HEADER_GAP;
            float footer = BOTTOM_PADDING;
            float hintHeight = MeasureText(readingHint, headingWidth, 20f);
            bool isHorizontalOverflow = contentWidth > viewportWidth + 0.5f;
            float hintBlock = isHorizontalOverflow ? hintHeight + FOOTER_GAP : 0f;
            float panelHeight = Mathf.Min(Mathf.Max(basePanel.y, header + contentHeight + hintBlock + footer), maxHeight);

            // 넘칠 때만 스크롤 안내를 바닥에 더한다. 보기 영역은 실제로 남는 높이만 쓴다(인위적 최소 높이로 캔버스를 넘지 않는다).
            bool isVerticalOverflow = contentHeight > panelHeight - header - footer - hintBlock + 0.5f;
            if (isVerticalOverflow && hintBlock <= 0f) hintBlock = hintHeight + FOOTER_GAP;
            float chrome = header + footer + hintBlock;
            float viewportHeight = Mathf.Max(MIN_VIEWPORT_HEIGHT, panelHeight - chrome);
            panelHeight = chrome + viewportHeight;
            WarnIfChromeOverflows(panelHeight, maxHeight);

            panel.sizeDelta = new Vector2(panelWidth, panelHeight);
            float top = panelHeight * 0.5f;
            PlaceCentered(heading.rectTransform, headingWidth, headingHeight, top - TOP_PADDING - headingHeight * 0.5f);

            float viewportTop = top - header;
            PlaceCentered(viewport, viewportWidth, viewportHeight, viewportTop - viewportHeight * 0.5f);
            float finalWidth = Mathf.Max(viewportWidth, contentWidth);
            content.sizeDelta = new Vector2(finalWidth, Mathf.Max(viewportHeight, contentHeight));
            PlaceCards(finalWidth, rowWidth, cardHeight);

            bool hasHint = hintBlock > 0f;
            readingHint.gameObject.SetActive(hasHint);
            if (hasHint)
            {
                float hintCenter = viewportTop - viewportHeight - FOOTER_GAP - hintHeight * 0.5f;
                PlaceCentered(readingHint.rectTransform, headingWidth, hintHeight, hintCenter);
            }

            if (isResetPending) SetScroll(0f, 0f);
            else SetScroll(oldX * RangeX(), oldY * RangeY());
            isResetPending = false;
            if (visibleSelection != null) ScheduleReflowReveal(visibleSelection);

            Snapshot(scale, canvasSize);
        }

        /// <summary>배율·화면·언어·글자·표시 카드가 마지막 배치와 다를 때만 다시 배치한다.</summary>
        public void RefreshIfNeeded()
        {
            if (panel == null || readingScroll == null) return;
            bool hasChanged = appliedScale != MenuTextReadability.Scale ||
                              appliedCanvasSize != CanvasSize() ||
                              appliedCanvasScale != CanvasScaleFactor() ||
                              appliedActiveMask != ActiveMask() ||
                              appliedLanguage != CurrentLanguage() ||
                              HasTextChanged();
            if (hasChanged) Apply();
        }

        private void ApplyFonts(float scale)
        {
            heading.fontSize = Mathf.RoundToInt(HEADING_FONT * scale);
            readingHint.fontSize = Mathf.RoundToInt(READING_HINT_FONT * scale);
            for (int i = 0; i < nodes.Count; i++)
            {
                types[i].fontSize = Mathf.RoundToInt(TYPE_FONT * scale);
                titles[i].fontSize = Mathf.RoundToInt(TITLE_FONT * scale);
                hints[i].fontSize = Mathf.RoundToInt(HINT_FONT * scale);
                // 예고 줄을 줄이던 bestFit이 다시 켜져 있어도 여기서 끈다 — 글자는 줄이지 않고 카드가 늘어난다.
                PrepareWrap(types[i]);
                PrepareWrap(titles[i]);
                PrepareWrap(hints[i]);
            }
            PrepareWrap(heading);
        }

        /// <summary>보이는 카드마다 타입·이름·힌트를 위에서부터 쌓는다. 가장 큰 카드 높이(최소 원래 높이)를 돌려준다.</summary>
        private float MeasureCards(out int activeCount)
        {
            activeCount = 0;
            float height = cardMinHeight;
            for (int i = 0; i < nodes.Count; i++)
            {
                if (!nodes[i].gameObject.activeSelf) continue;
                activeCount++;

                float offset = CARD_TOP_PADDING;
                offset = StackLabel(types[i], offset, 0f);
                offset = StackLabel(titles[i], offset, TYPE_TITLE_GAP);
                offset = StackLabel(hints[i], offset, TITLE_HINT_GAP);
                height = Mathf.Max(height, offset + CARD_BOTTOM_PADDING);
            }
            return height;
        }

        /// <summary>카드 위쪽 기준으로 글자 하나를 놓고 다음 위치를 돌려준다. 빈 글자는 앞 간격을 두지 않는다.</summary>
        private float StackLabel(Text label, float offset, float gap)
        {
            if (!string.IsNullOrEmpty(label.text)) offset += gap;
            float height = MeasureText(label, labelWidth, 0f);
            PlaceFromTop(label.rectTransform, labelWidth, height, offset);
            return offset + height;
        }

        /// <summary>한 줄로 놓는다. 내용이 보기보다 좁으면 가운데, 넓으면 왼쪽 여백부터(가로 스크롤).</summary>
        private void PlaceCards(float contentWidth, float rowWidth, float cardHeight)
        {
            float start = (contentWidth - rowWidth) * 0.5f;
            float centerY = -(CONTENT_TOP_PADDING + cardHeight * 0.5f);
            int column = 0;
            for (int i = 0; i < nodes.Count; i++)
            {
                if (!nodes[i].gameObject.activeSelf) continue;
                var rect = (RectTransform)nodes[i].transform;
                rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
                rect.pivot = Vector2.one * 0.5f;
                rect.sizeDelta = new Vector2(cardWidth, cardHeight);
                rect.anchoredPosition = new Vector2(start + column * (cardWidth + CARD_GAP) + cardWidth * 0.5f, centerY);
                column++;
                PlaceRouteIcon(i, cardHeight);
            }
        }

        /// <summary>Bind가 둔 경로 그림의 실제 크기 그대로 카드 위 가장자리 가운데로 옮긴다. 숨은 그림은 숨긴 채 둔다.</summary>
        private void PlaceRouteIcon(int index, float cardHeight)
        {
            var icon = routeIcons[index];
            if (icon == null || !routeIconVisible[index]) return;
            var rect = icon.rectTransform;
            rect.anchorMin = rect.anchorMax = Vector2.one * 0.5f;
            rect.pivot = Vector2.one * 0.5f;
            rect.sizeDelta = routeIconSizes[index];
            rect.anchoredPosition = new Vector2(0f, cardHeight * 0.5f);
        }

        private void CaptureRouteIcons()
        {
            for (int i = 0; i < nodes.Count; i++)
            {
                var child = nodes[i].transform.Find(ROUTE_ICON_NAME);
                Image icon = null;
                if (child != null) child.TryGetComponent(out icon);
                routeIcons[i] = icon;
                routeIconVisible[i] = icon != null && icon.enabled && icon.gameObject.activeSelf;
                routeIconSizes[i] = icon != null ? icon.rectTransform.sizeDelta : Vector2.zero;
            }
        }

        /// <summary>제목·안내만으로 캔버스를 넘는 병적 경우. 글자를 자르지 않으므로 맞출 수 없다 — 상태가 바뀔 때 한 번만 알린다.</summary>
        private void WarnIfChromeOverflows(float panelHeight, float maxHeight)
        {
            bool isOverflowing = panelHeight > maxHeight + 0.5f;
            if (isOverflowing && !hasWarnedChromeOverflow)
                Debug.LogWarning($"[NodeMapReadability] 고정 머리·바닥({panelHeight:0})이 캔버스 여유({maxHeight:0})보다 크다 — 패널이 화면을 넘는다.");
            hasWarnedChromeOverflow = isOverflowing;
        }

        private static float MeasureText(Text text, float width, float minHeight)
        {
            var rect = text.rectTransform;
            rect.sizeDelta = new Vector2(width, rect.sizeDelta.y);   // 줄바꿈 폭을 먼저 정해야 높이가 맞다
            return Mathf.Max(minHeight, text.preferredHeight);
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

        // ───────────────────────── 상태 비교 ─────────────────────────

        private void Snapshot(float scale, Vector2 canvasSize)
        {
            appliedScale = scale;
            appliedCanvasSize = canvasSize;
            appliedCanvasScale = CanvasScaleFactor();
            appliedLanguage = CurrentLanguage();
            appliedActiveMask = ActiveMask();
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
            int mask = 0;
            for (int i = 0; i < nodes.Count; i++) if (nodes[i].gameObject.activeSelf) mask |= 1 << i;
            return mask;
        }

        private Vector2 CanvasSize()
        {
            Vector2 size = canvasRect != null ? canvasRect.rect.size : Vector2.zero;
            // 캔버스가 아직 크기를 못 받았으면 기준 패널만큼만 가정한다 — 크기가 잡히면 다시 배치된다.
            if (size.x < 1f || size.y < 1f) size = basePanel + Vector2.one * SAFE_MARGIN * 2f;
            return size;
        }

        private float CanvasScaleFactor() => rootCanvas != null ? rootCanvas.scaleFactor : 1f;

        private static LocalizationLanguage CurrentLanguage()
        {
            var manager = LocalizationManager.GetInstanceSafe();
            return manager != null ? manager.CurrentLanguage : LocalizationLanguage.Korean;
        }

        private static string HintText(LocalizationLanguage language) => language switch
        {
            LocalizationLanguage.English => "Wheel·PgUp/Dn: vertical · Shift: horizontal · R-stick · Home/End",
            LocalizationLanguage.Japanese => "ホイール・PgUp/Dn:縦 · Shift:横 · 右スティック · Home/End",
            _ => "휠·PgUp/Dn: 세로 · Shift: 가로 · 우스틱 · Home/End",
        };
    }
}
