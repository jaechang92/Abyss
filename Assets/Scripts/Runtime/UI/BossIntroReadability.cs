using UnityEngine;
using UnityEngine.UI;

namespace Abyss.Runtime.UI
{
    /// <summary>
    /// 보스 등장 카드의 이름(64)·별칭(26)·안내(17) 글자를 메뉴 글자 배율에 맞추고, 대사 줄 묶음까지
    /// 실제 부모 캔버스 안(가장자리 24 여백)에 겹치지 않게 다시 쌓는다.
    ///
    /// 원본 배치는 붙을 때 한 번만 잡고, 배율·캔버스 크기·문자열·글꼴이 바뀔 때마다 그 원본에서 다시 계산한다 —
    /// 반복 적용해도 누적되지 않는다. 배율 100%·짧은 문자열이면 원본 좌표 그대로다. 글자는 줄이거나 자르지 않는다.
    ///
    /// 대사 줄의 글자 크기·색·고대비·높이는 <see cref="NarrativeReadability"/> 소유다. 여기서는 줄을 감싼 묶음
    /// (NarrativeLineRoot)의 위치만 정하고, 쓸 수 있는 폭이 바뀔 때만 줄의 기준 사각형(폭 x 원본 높이)을 다시 잡는다.
    /// 서사 도우미의 LateUpdate(기본 순서) 뒤에 돌도록 실행 순서를 100으로 둔다.
    ///
    /// 초상은 장식이다. 공간이 모자라면 숨기고, 다시 넉넉해지면 맞춰 둔 사각형 그대로 되살린다(크기·색·그림은 건드리지 않는다).
    /// 글자 묶음 자체가 화면 높이를 넘으면 높이는 그대로 두고 위에 붙여 겹치지 않게만 둔다(<see cref="IsConstrained"/>).
    /// 위치·크기·글자 크기·줄바꿈만 바꾼다 — 투명도·색·타이머는 건드리지 않는다.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public sealed class BossIntroReadability : MonoBehaviour
    {
        private const string PORTRAIT_NAME = "ArtBossPortrait";
        private const float SAFE_MARGIN = 24f;
        private const float WIDTH_PADDING = 8f;
        private const float PORTRAIT_GAP = 16f;
        private const float HINT_GAP = 12f;
        private const float CONTRAST_PADDING = 12f;
        // NarrativeReadability 고대비 바탕은 줄 폭 + 16(좌우 8씩)이다 — 바탕까지 안전 여백 안에 두려고 폭에서 미리 뺀다.
        private const float CONTRAST_WIDTH_PADDING = 16f;
        // 대사 줄의 원본(배율 1) 기준 사각형. 서사 도우미가 Attach 시점에 저장된 배율로 이미 적용한 뒤라
        // 실제 사각형(피벗 1·위치 45 등)을 읽으면 확대 배치를 원본으로 착각한다 — 생성 값을 고정값으로 쓴다.
        private const float LINE_BASE_WIDTH = 1400f;
        private const float LINE_BASE_HEIGHT = 90f;
        private static readonly Vector2 LineBasePivot = new Vector2(0.5f, 0.5f);

        /// <summary>메뉴 배율을 따르는 라벨 하나의 원본(배율 1) 상태와 마지막 적용 조건.</summary>
        private sealed class LabelBaseline
        {
            public readonly Text Text;
            public readonly int FontSize;
            public readonly Vector2 Size;
            public readonly Vector2 Position;
            public readonly HorizontalWrapMode Wrap;
            public readonly VerticalWrapMode VerticalWrap;
            private string lastText;
            private Font lastFont;

            public LabelBaseline(Text text)
            {
                Text = text;
                FontSize = text.fontSize;
                Size = text.rectTransform.sizeDelta;
                Position = text.rectTransform.anchoredPosition;
                Wrap = text.horizontalOverflow;
                VerticalWrap = text.verticalOverflow;
            }

            public float Top => Position.y + Size.y * 0.5f;
            public float Bottom => Position.y - Size.y * 0.5f;
            public bool HasChanged => Text.text != lastText || Text.font != lastFont;

            /// <summary>
            /// 원본 폭을 안전 폭으로 제한하고, 한 줄이 넘칠 때만 안전 폭까지 넓힌 뒤 그래도 넘치면 줄바꿈한다.
            /// 높이는 줄바꿈된 전체 높이 — 원본보다 작아지지 않는다. 적용한 높이를 돌려준다.
            /// </summary>
            public float Fit(float scale, float available)
            {
                Text.fontSize = Mathf.RoundToInt(FontSize * scale);
                Text.resizeTextForBestFit = false;

                float baseWidth = Mathf.Min(Size.x, available);
                float preferred = Text.preferredWidth;
                float needed = preferred + WIDTH_PADDING;
                float width = needed <= baseWidth ? baseWidth : Mathf.Min(needed, available);
                Text.horizontalOverflow = preferred > width ? HorizontalWrapMode.Wrap : Wrap;

                var rect = Text.rectTransform;
                rect.sizeDelta = new Vector2(width, Size.y);
                float height = Mathf.Max(Size.y, Mathf.Ceil(Text.preferredHeight));
                Text.verticalOverflow = height > Size.y ? VerticalWrapMode.Overflow : VerticalWrap;
                rect.sizeDelta = new Vector2(width, height);
                return height;
            }

            public void Place(float y) => Text.rectTransform.anchoredPosition = new Vector2(Position.x, y);

            public void Remember()
            {
                lastText = Text.text;
                lastFont = Text.font;
            }
        }

        private RectTransform body;
        private Canvas canvas;
        private LabelBaseline title;
        private LabelBaseline epithet;
        private LabelBaseline hint;
        private Text line;
        private NarrativeReadability narrative;
        private RectTransform lineRoot;
        private RectTransform divider;

        // 원본(배율 1) 배치 — Attach에서 한 번만 잡는다.
        private Vector2 dividerSize;
        private Vector2 dividerPosition;
        private Vector2 lineRootPosition;
        private Vector2 lineSize;
        private Vector2 linePivot;
        private Vector2 linePosition;
        private VerticalWrapMode lineVerticalWrap;
        private float lineCenter;
        private float titleGap;
        private float epithetGap;
        private float lineGap;

        // 초상 — 재생마다 다시 맞춰지므로 CapturePortraitSource 호출 시점에만 잡는다.
        private Image portrait;
        private Sprite portraitSprite;
        private Vector2 portraitSize;
        private Vector2 portraitPosition;
        private bool isPortraitEnabled;
        private bool isPortraitActive;
        private bool hasPortraitSource;
        private bool isPortraitHiddenByHelper;
        private bool isPortraitDirty;

        // 대사 줄 기준 사각형을 마지막으로 잡은 폭.
        private bool hasLineBaseline;
        private float lineBaselineWidth;

        // 마지막 적용 조건 — 바뀐 것이 있을 때만 다시 쌓는다.
        private bool hasApplied;
        private float lastMenuScale;
        private float lastNarrativeScale;
        private bool isLastHighContrast;
        private Vector2 lastCanvasSize;
        private float lastCanvasScale;
        private string lastLineText;
        private Font lastLineFont;

        /// <summary>글자 묶음(이름~안내)이 캔버스 안전 높이보다 커서 전부 화면 안에 둘 수 없는 상태.</summary>
        public bool IsConstrained { get; private set; }

        /// <summary>
        /// <paramref name="body"/>에 붙인다. 이미 붙어 있으면 그것을 돌려준다(원본은 처음 붙을 때만 잡는다).
        /// 대사 줄은 <paramref name="narrativeLineRoot"/>의 자식이고 <see cref="NarrativeReadability"/>가 먼저 붙어 있어야 한다.
        /// </summary>
        public static BossIntroReadability Attach(Transform body, Text title, Text epithet, Text line, Text hint,
            RectTransform divider, RectTransform narrativeLineRoot)
        {
            if (body == null || title == null || epithet == null || line == null || hint == null ||
                divider == null || narrativeLineRoot == null) return null;
            var existing = body.GetComponent<BossIntroReadability>();
            if (existing != null) return existing;

            var component = body.gameObject.AddComponent<BossIntroReadability>();
            component.body = body as RectTransform;
            component.canvas = body.GetComponentInParent<Canvas>(true);
            component.title = new LabelBaseline(title);
            component.epithet = new LabelBaseline(epithet);
            component.hint = new LabelBaseline(hint);
            component.line = line;
            component.narrative = line.GetComponent<NarrativeReadability>();
            component.divider = divider;
            component.lineRoot = narrativeLineRoot;
            component.CaptureBaseline();
            component.CapturePortraitSource();
            return component;
        }

        private void CaptureBaseline()
        {
            dividerSize = divider.sizeDelta;
            dividerPosition = divider.anchoredPosition;
            lineRootPosition = lineRoot.anchoredPosition;
            lineSize = new Vector2(LINE_BASE_WIDTH, LINE_BASE_HEIGHT);
            linePivot = LineBasePivot;
            linePosition = Vector2.zero;
            lineVerticalWrap = line.verticalOverflow;
            lineCenter = lineRootPosition.y + linePosition.y + (0.5f - linePivot.y) * lineSize.y;

            // 원본: 이름 아래 65 / 별칭 위 64 → 1, 별칭 아래 24 / 구분선 위 13 → 11,
            // 구분선 아래 11 / 대사 위 -35 → 46(고대비 바탕 여백 6을 뺀 40을 최소 간격으로 쓴다).
            float dividerTop = dividerPosition.y + dividerSize.y * 0.5f;
            float dividerBottom = dividerPosition.y - dividerSize.y * 0.5f;
            float lineTop = lineCenter + lineSize.y * 0.5f;
            titleGap = Mathf.Max(0f, title.Bottom - epithet.Top);
            epithetGap = Mathf.Max(0f, epithet.Bottom - dividerTop);
            lineGap = Mathf.Max(0f, dividerBottom - lineTop - CONTRAST_PADDING * 0.5f);
        }

        /// <summary>
        /// 초상을 다시 맞춘(<see cref="UiArtDecor.ApplyIconInRect"/>) 직후 부른다. 그림이 없어 꺼진 경우도 그대로 기록한다.
        /// 기록한 사각형은 숨겼다 되살릴 때의 원본이다 — 그림·크기·색은 바꾸지 않는다.
        /// </summary>
        public void CapturePortraitSource()
        {
            portrait = null;
            var found = body != null ? body.Find(PORTRAIT_NAME) : null;
            if (found != null) portrait = found.GetComponent<Image>();

            hasPortraitSource = false;
            isPortraitHiddenByHelper = false;
            if (portrait != null)
            {
                var rect = portrait.rectTransform;
                isPortraitEnabled = portrait.enabled;
                isPortraitActive = portrait.gameObject.activeSelf;
                portraitSprite = portrait.sprite;
                portraitSize = rect.sizeDelta;
                portraitPosition = rect.anchoredPosition;
                hasPortraitSource = isPortraitEnabled && isPortraitActive && portraitSprite != null;
            }
            isPortraitDirty = true;
        }

        /// <summary>현재 배율·캔버스 크기·문자열로 원본에서 다시 쌓는다.</summary>
        public void Apply()
        {
            if (body == null || title == null || line == null || divider == null || lineRoot == null) return;

            float scale = MenuTextReadability.Scale;
            Vector2 canvasSize = ReadCanvasSize();
            Vector2 layoutSize = canvasSize.x > 0f && canvasSize.y > 0f ? canvasSize : new Vector2(1920f, 1080f);
            float available = Mathf.Max(1f, layoutSize.x - SAFE_MARGIN * 2f);

            float titleHeight = title.Fit(scale, available);
            float epithetHeight = epithet.Fit(scale, available);
            float hintHeight = hint.Fit(scale, available);
            divider.sizeDelta = new Vector2(Mathf.Min(dividerSize.x, available), dividerSize.y);

            float lineReserved = ApplyLine(available, out float lineLocalCenter);
            Arrange(layoutSize, titleHeight, epithetHeight, lineReserved, lineLocalCenter, hintHeight);
            Remember(scale, canvasSize);
        }

        /// <summary>배율·서사 설정·캔버스·문자열·글꼴·초상 중 바뀐 것이 있을 때만 <see cref="Apply"/>한다.</summary>
        public void RefreshIfNeeded()
        {
            if (body == null || title == null || line == null) return;
            if (!hasApplied || isPortraitDirty || lastMenuScale != MenuTextReadability.Scale ||
                lastNarrativeScale != NarrativeReadability.Scale || isLastHighContrast != NarrativeReadability.HighContrast ||
                lastCanvasSize != ReadCanvasSize() || lastCanvasScale != CanvasScale() ||
                title.HasChanged || epithet.HasChanged || hint.HasChanged ||
                lastLineText != line.text || lastLineFont != line.font)
                Apply();
        }

        private void LateUpdate() => RefreshIfNeeded();

        /// <summary>
        /// 쓸 수 있는 폭이 바뀔 때만 줄 기준 사각형(폭 x 원본 높이, 원본 피벗·위치)을 다시 잡고 서사 도우미가 그 값을 기본으로 삼게 한다.
        /// 그 밖에는 서사 도우미의 적용만 부른다 — 확대된 높이를 기준으로 다시 잡지 않는다.
        /// 돌려주는 값은 대사가 실제로 차지하는 높이(고대비 바탕 여백 포함), <paramref name="localCenter"/>는 묶음 안 중심.
        /// </summary>
        private float ApplyLine(float available, out float localCenter)
        {
            var rect = line.rectTransform;
            // 고대비 바탕(폭 + 16)까지 안전 여백 안 — 1920 기준이면 1400 그대로다.
            float width = Mathf.Min(lineSize.x, Mathf.Max(1f, available - CONTRAST_WIDTH_PADDING));
            if (!hasLineBaseline || !Mathf.Approximately(width, lineBaselineWidth))
            {
                rect.sizeDelta = new Vector2(width, lineSize.y);
                rect.pivot = linePivot;
                rect.anchoredPosition = linePosition;
                if (narrative != null) narrative.CaptureLayout();
                hasLineBaseline = true;
                lineBaselineWidth = width;
            }
            if (narrative != null) narrative.Apply();

            float rectHeight = rect.sizeDelta.y;
            float textHeight = Mathf.Ceil(line.preferredHeight);
            // 서사 배율 100%의 고정 높이를 넘는 긴 대사도 잘리지 않게 세로 넘침만 허용한다(글자·높이 정책은 서사 도우미 그대로).
            line.verticalOverflow = textHeight > rectHeight ? VerticalWrapMode.Overflow : lineVerticalWrap;
            bool hasContrast = NarrativeReadability.HighContrast && !string.IsNullOrEmpty(line.text);
            localCenter = rect.anchoredPosition.y + (0.5f - rect.pivot.y) * rectHeight;
            return Mathf.Max(rectHeight, textHeight) + (hasContrast ? CONTRAST_PADDING : 0f);
        }

        /// <summary>
        /// 구분선 원본 위치를 기준으로 이름·별칭은 위로, 대사·안내는 아래로 자란다. 원본 위치를 우선하고,
        /// 위가 넘치면 초상 여유 → 위 묶음 순으로 내리고, 아래가 넘치면 안내 → 대사 → 위 묶음 순으로 필요한 만큼만 올린다.
        /// </summary>
        private void Arrange(Vector2 layoutSize, float titleHeight, float epithetHeight, float lineReserved,
            float lineLocalCenter, float hintHeight)
        {
            float safeTop = layoutSize.y * 0.5f - SAFE_MARGIN;
            float safeBottom = -layoutSize.y * 0.5f + SAFE_MARGIN;
            float safeHeight = safeTop - safeBottom;
            float dividerHalf = dividerSize.y * 0.5f;
            float lineHalf = lineReserved * 0.5f;
            float hintHalf = hintHeight * 0.5f;
            float portraitHalf = portraitSize.y * 0.5f;

            // 구분선 중심 → 이름 위끝, 구분선 중심 → 안내 아래끝(최소 간격).
            float aboveDivider = dividerHalf + epithetGap + epithetHeight + titleGap + titleHeight;
            float belowDivider = dividerHalf + lineGap + lineReserved + HINT_GAP + hintHeight;
            float textStack = aboveDivider + belowDivider;

            IsConstrained = textStack > safeHeight;
            bool canShowPortrait = hasPortraitSource && portrait != null &&
                                   textStack + PORTRAIT_GAP + portraitSize.y <= safeHeight;

            float dividerY;
            float lineY;
            float hintY;
            float portraitY = portraitPosition.y;
            if (IsConstrained)
            {
                // 물리적으로 다 들어가지 않는다 — 높이는 그대로, 겹침 없이 위에 붙인다(안내가 먼저 화면 밖으로 나간다).
                dividerY = safeTop - aboveDivider;
                lineY = dividerY - dividerHalf - lineGap - lineHalf;
                hintY = lineY - lineHalf - HINT_GAP - hintHalf;
            }
            else
            {
                dividerY = dividerPosition.y;
                lineY = Mathf.Min(lineCenter, dividerY - dividerHalf - lineGap - lineHalf);
                hintY = Mathf.Min(hint.Position.y, lineY - lineHalf - HINT_GAP - hintHalf);
                if (canShowPortrait)
                    portraitY = Mathf.Max(portraitPosition.y, dividerY + aboveDivider + PORTRAIT_GAP + portraitHalf);

                float top = canShowPortrait ? portraitY + portraitHalf : dividerY + aboveDivider;
                if (top > safeTop && canShowPortrait)
                {
                    portraitY = Mathf.Max(dividerY + aboveDivider + PORTRAIT_GAP + portraitHalf, portraitY - (top - safeTop));
                    top = portraitY + portraitHalf;
                }
                if (top > safeTop)
                {
                    float down = top - safeTop;
                    dividerY -= down;
                    portraitY -= down;
                    lineY = Mathf.Min(lineY, dividerY - dividerHalf - lineGap - lineHalf);
                    hintY = Mathf.Min(hintY, lineY - lineHalf - HINT_GAP - hintHalf);
                }

                if (hintY - hintHalf < safeBottom)
                {
                    hintY = safeBottom + hintHalf;
                    lineY = Mathf.Max(lineY, hintY + hintHalf + HINT_GAP + lineHalf);
                    float up = lineY + lineHalf + lineGap + dividerHalf - dividerY;
                    if (up > 0f)
                    {
                        dividerY += up;
                        portraitY += up;
                    }
                    if (canShowPortrait && portraitY + portraitHalf > safeTop)
                        portraitY = Mathf.Max(dividerY + aboveDivider + PORTRAIT_GAP + portraitHalf, safeTop - portraitHalf);
                }
            }

            float epithetY = dividerY + dividerHalf + epithetGap + epithetHeight * 0.5f;
            float titleY = epithetY + epithetHeight * 0.5f + titleGap + titleHeight * 0.5f;
            title.Place(titleY);
            epithet.Place(epithetY);
            hint.Place(hintY);
            divider.anchoredPosition = new Vector2(dividerPosition.x, dividerY);
            lineRoot.anchoredPosition = new Vector2(lineRootPosition.x, lineY - lineLocalCenter);
            PlacePortrait(canShowPortrait, portraitY);
        }

        /// <summary>
        /// 원본 그림이 있을 때만 다룬다. 공간이 모자라면 끄고(이 도우미가 끈 것만 기억), 넉넉하면 기록한 사각형 크기로 되살려 위치만 맞춘다.
        /// 그림이 없던 초상은 꺼진 그대로 둔다.
        /// </summary>
        private void PlacePortrait(bool isVisible, float y)
        {
            if (portrait == null || !hasPortraitSource) return;
            if (!isVisible)
            {
                if (portrait.enabled)
                {
                    portrait.enabled = false;
                    isPortraitHiddenByHelper = true;
                }
                return;
            }

            if (isPortraitHiddenByHelper)
            {
                portrait.enabled = isPortraitEnabled;
                isPortraitHiddenByHelper = false;
            }
            var rect = portrait.rectTransform;
            rect.sizeDelta = portraitSize;
            rect.anchoredPosition = new Vector2(portraitPosition.x, y);
        }

        private void Remember(float scale, Vector2 canvasSize)
        {
            hasApplied = true;
            isPortraitDirty = false;
            lastMenuScale = scale;
            lastNarrativeScale = NarrativeReadability.Scale;
            isLastHighContrast = NarrativeReadability.HighContrast;
            lastCanvasSize = canvasSize;
            lastCanvasScale = CanvasScale();
            title.Remember();
            epithet.Remember();
            hint.Remember();
            lastLineText = line.text;
            lastLineFont = line.font;
        }

        private float CanvasScale() => canvas != null ? canvas.scaleFactor : 1f;

        /// <summary>실제 부모 캔버스의 로컬 크기(화면 픽셀이 아니다).</summary>
        private Vector2 ReadCanvasSize()
        {
            var rect = canvas != null ? canvas.transform as RectTransform : null;
            if (rect == null) rect = body;
            return rect != null ? rect.rect.size : Vector2.zero;
        }
    }
}
