using UnityEngine;
using UnityEngine.UI;

namespace Abyss.Runtime.UI
{
    /// <summary>
    /// 엔딩 크레딧의 글자 확대·마스크 스크롤 창과 자막·크레딧 동안의 확인 안내 배치.
    ///
    /// 크레딧 본문(같은 <see cref="Text"/> 참조)을 <see cref="RectMask2D"/> 창 안으로 옮겨, 아래쪽 확인 안내
    /// 위에서만 흐르게 한다 — 글자가 커져도 굴러가는 크레딧이 안내를 덮지 않는다. 표시 전용이라 입력을 받지 않는다.
    ///
    /// 스크롤 속도는 캔버스 로컬 기준 초당 <see cref="SCROLL_SPEED"/>로 고정한다. 글자 배율을 곱하지 않는다.
    /// 진행은 비율(0 = 본문 전체가 창 아래, 1 = 본문 전체가 창 위)로 들고 있어 배율·크기가 바뀌어도
    /// 같은 비율 지점에서 이어 간다.
    ///
    /// 통계가 보이는 동안 확인 안내는 <see cref="EndingStatsReadability"/>가 단독으로 소유한다 —
    /// 여기서는 그동안 안내의 배치·글자를 쓰지 않는다. 문구·색·활성 상태는 패널이 소유한다.
    /// </summary>
    public sealed class EndingCreditsReadability : MonoBehaviour
    {
        private const float SCROLL_SPEED = 110f;          // 캔버스 로컬 px/s
        private const float FALLBACK_HEIGHT = 600f;
        private const float EDGE_MARGIN = 16f;
        private const float SIDE_MARGIN = 32f;
        private const float HINT_BOTTOM_MARGIN = 8f;
        private const float VIEW_HINT_GAP = 16f;
        private const float WIDTH_GROWTH = 200f;

        private Text credits;
        private RectTransform creditsRect;
        private Text hint;
        private RectTransform area;
        private CanvasGroup stats;
        private Canvas canvas;
        private RectTransform readingViewport;

        // 기준값 — 부착 시점에 한 번 잡는다.
        private int creditsFont;
        private float creditsWidth;
        private int hintFont;
        private Vector2 hintPosition;
        private Vector2 hintSize;

        private bool initialized;
        private bool isRunning;
        private bool isDirty = true;
        private float startY;
        private float endY;

        // 마지막 배치 조건. 하나라도 달라졌을 때만 다시 배치한다.
        private float lastScale = -1f;
        private Vector2 lastArea;
        private float lastCanvasScale = -1f;
        private string lastCreditsText;
        private string lastHintText;
        private bool lastStatsActive;
        private bool lastCreditsActive;

        /// <summary>현재 배율·폭에서 크레딧 본문 전체의 높이(캔버스 로컬).</summary>
        public float CreditsHeight { get; private set; }

        /// <summary>크레딧이 흐르는 마스크 창.</summary>
        public RectTransform Viewport => readingViewport;

        /// <summary>0 = 본문 전체가 창 아래, 1 = 본문 전체가 창 위.</summary>
        public float Progress { get; private set; }

        /// <summary>
        /// 패널 본체에 붙인다. 크레딧 Text를 마스크 창 안으로 한 번 옮기고 기준값을 잡는다.
        /// 같은 본체에 이미 붙어 있으면 그것을 돌려준다.
        /// </summary>
        public static EndingCreditsReadability Attach(Text credits, Text skipHint, Transform canvasBody, CanvasGroup statsGroup)
        {
            if (credits == null || skipHint == null || canvasBody == null) return null;
            var existing = canvasBody.GetComponent<EndingCreditsReadability>();
            if (existing != null) return existing;

            var helper = canvasBody.gameObject.AddComponent<EndingCreditsReadability>();
            helper.credits = credits;
            helper.creditsRect = credits.rectTransform;
            helper.hint = skipHint;
            helper.area = canvasBody as RectTransform;
            helper.stats = statsGroup;
            helper.canvas = canvasBody.GetComponentInParent<Canvas>();

            helper.creditsFont = credits.fontSize;
            helper.creditsWidth = helper.creditsRect.sizeDelta.x;
            var hintRect = skipHint.rectTransform;
            helper.hintFont = skipHint.fontSize;
            helper.hintPosition = hintRect.anchoredPosition;
            helper.hintSize = hintRect.sizeDelta;

            helper.BuildViewport();
            helper.initialized = true;
            return helper;
        }

        private void BuildViewport()
        {
            var center = new Vector2(0.5f, 0.5f);
            var go = UiFactory.CreateRect(area, "CreditsReadingViewport", center, center, center,
                creditsRect.anchoredPosition, creditsRect.sizeDelta);
            // 원래 크레딧 자리(확인 안내보다 앞)에 둔다 — 그리는 순서가 바뀌지 않는다.
            if (creditsRect.parent == area) go.transform.SetSiblingIndex(creditsRect.GetSiblingIndex());
            // 그래픽이 없는 마스크 — 클릭을 가로채지 않는다.
            go.AddComponent<RectMask2D>();
            readingViewport = (RectTransform)go.transform;

            creditsRect.SetParent(readingViewport, false);
            creditsRect.anchorMin = center;
            creditsRect.anchorMax = center;
            creditsRect.pivot = center;
            credits.raycastTarget = false;

            go.SetActive(credits.gameObject.activeSelf);
        }

        /// <summary>크레딧 시작(재생 포함). 본문 전체가 창 바로 아래에 오도록 다시 배치한다.</summary>
        public void BeginCredits()
        {
            if (!initialized) return;
            Progress = 0f;
            isRunning = true;
            isDirty = true;
            RefreshIfNeeded();
        }

        /// <summary>
        /// 위로 흘린다. 본문 전체가 창 위로 빠져나간 순간 한 번만 true를 돌려준다.
        /// 창이 보이지 않으면 움직이지 않는다.
        /// </summary>
        public bool Advance(float unscaledDelta)
        {
            if (!initialized || !isRunning) return false;
            if (!credits.gameObject.activeSelf || !readingViewport.gameObject.activeSelf) return false;

            float distance = endY - startY;
            Progress = distance > 0f
                ? Mathf.Clamp01(Progress + SCROLL_SPEED * Mathf.Max(0f, unscaledDelta) / distance)
                : 1f;
            PlaceCredits();

            if (Progress < 1f) return false;
            isRunning = false;
            return true;
        }

        /// <summary>
        /// 배율·캔버스 크기·캔버스 배율·크레딧 문구·안내 문구·통계/크레딧 표시 상태 중 무엇이라도 달라졌을 때만 다시 배치한다.
        /// 위치는 진행 비율에서 다시 계산하므로 절대 위치를 리셋하지 않는다.
        /// </summary>
        public void RefreshIfNeeded()
        {
            if (!initialized || credits == null || hint == null) return;

            bool isStatsActive = stats != null && stats.gameObject.activeInHierarchy;
            bool isCreditsActive = credits.gameObject.activeSelf;
            // 크레딧이 꺼지면 창만 끈다(크레딧 Text의 활성은 패널이 소유). 통계가 보이면 창을 감춰 잔상을 남기지 않는다.
            bool isViewportActive = isCreditsActive && !isStatsActive;
            if (readingViewport.gameObject.activeSelf != isViewportActive) readingViewport.gameObject.SetActive(isViewportActive);

            float scale = MenuTextReadability.Scale;
            Vector2 size = AreaSize();
            float canvasScale = CanvasScale();
            bool changed = isDirty || scale != lastScale || size != lastArea || canvasScale != lastCanvasScale ||
                credits.text != lastCreditsText || hint.text != lastHintText ||
                isStatsActive != lastStatsActive || isCreditsActive != lastCreditsActive;
            if (!changed) return;

            Layout(scale, size, isStatsActive, isCreditsActive);

            isDirty = false;
            lastScale = scale;
            lastArea = size;
            lastCanvasScale = canvasScale;
            lastCreditsText = credits.text;
            lastHintText = hint.text;
            lastStatsActive = isStatsActive;
            lastCreditsActive = isCreditsActive;
        }

        private void Layout(float scale, Vector2 size, bool isStatsActive, bool isCreditsActive)
        {
            float halfHeight = size.y * 0.5f;
            float hintTop;

            if (!isStatsActive)
            {
                // 자막·크레딧 동안의 확인 안내 — 아래쪽 고정, 캔버스 안에 들어오도록만 끌어올린다.
                Style(hint, hintFont, scale);
                float hintWidth = Mathf.Min(hintSize.x + WIDTH_GROWTH * (scale - 1f), Mathf.Max(1f, size.x - EDGE_MARGIN * 2f));
                float hintHeight = Mathf.Max(hintSize.y, Height(hint, hintWidth) + 4f);
                float hintY = Mathf.Max(hintPosition.y, -halfHeight + HINT_BOTTOM_MARGIN + hintHeight * 0.5f);
                var hintRect = hint.rectTransform;
                hintRect.sizeDelta = new Vector2(hintWidth, hintHeight);
                hintRect.anchoredPosition = new Vector2(hintPosition.x, hintY);
                hint.raycastTarget = false;
                hintTop = hintY + hintHeight * 0.5f;
            }
            else
            {
                // 통계 쪽이 소유한 배치를 읽기만 한다.
                var hintRect = hint.rectTransform;
                hintTop = hintRect.anchoredPosition.y + hintRect.rect.height * (1f - hintRect.pivot.y);
            }

            if (!isCreditsActive) return;

            float top = halfHeight - EDGE_MARGIN;
            float bottom = hintTop + VIEW_HINT_GAP;
            float viewHeight = Mathf.Max(1f, top - bottom);
            float viewWidth = Mathf.Min(creditsWidth + WIDTH_GROWTH * (scale - 1f), Mathf.Max(1f, size.x - SIDE_MARGIN * 2f));
            readingViewport.sizeDelta = new Vector2(viewWidth, viewHeight);
            readingViewport.anchoredPosition = new Vector2(0f, (top + bottom) * 0.5f);

            // 글자만 배율로 키운다. 줄 간격·문구·색은 그대로.
            Style(credits, creditsFont, scale);
            float height = Height(credits, viewWidth);
            if (height <= 0f) height = FALLBACK_HEIGHT;
            CreditsHeight = height;
            creditsRect.sizeDelta = new Vector2(viewWidth, height);

            // 창 중심 기준: 시작 = 본문 위끝이 창 아래끝, 끝 = 본문 아래끝이 창 위끝.
            startY = -(viewHeight + height) * 0.5f;
            endY = -startY;
            PlaceCredits();
        }

        private void PlaceCredits()
        {
            creditsRect.anchoredPosition = new Vector2(0f, Mathf.Lerp(startY, endY, Progress));
        }

        /// <summary>글자 크기만 배율로 키운다. 자동 맞춤(축소)은 끄고 줄바꿈으로 전부 보이게 한다.</summary>
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
            return text.cachedTextGeneratorForLayout.GetPreferredHeight(text.text ?? string.Empty, settings) / text.pixelsPerUnit;
        }

        /// <summary>실제 캔버스 본체의 로컬 크기(화면 픽셀이 아니다). 아직 계산 전이면 기준 해상도.</summary>
        private Vector2 AreaSize()
        {
            if (area != null && area.rect.width > 0f && area.rect.height > 0f) return area.rect.size;
            return UiFactory.ReferenceResolution;
        }

        private float CanvasScale() => canvas != null ? canvas.scaleFactor : 1f;

        /// <summary>본체가 다시 켜졌다 — 꺼진 동안 다른 쪽이 안내를 되돌렸을 수 있으므로 다음 배치를 강제한다.</summary>
        private void OnEnable() => isDirty = true;

        /// <summary>캐시된 배치만 맞춘다. 이동·리셋은 패널의 진행(<see cref="Advance"/>·<see cref="BeginCredits"/>)만 한다.</summary>
        private void LateUpdate() => RefreshIfNeeded();
    }
}
