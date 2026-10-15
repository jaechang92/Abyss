using UnityEngine;
using UnityEngine.UI;

namespace Abyss.Runtime.UI
{
    /// <summary>
    /// 스테이지 낙하 연출의 「몇 층 · 이름」 글자를 메뉴 글자 배율에 맞춘다.
    /// 원본 배치(묶음 1400x140, 중심 (0,-290))를 한 번만 기억하고, 배율·화면 크기·문자열이 바뀔 때마다
    /// 그 원본에서 다시 계산한다 — 반복 적용해도 누적되지 않는다.
    ///
    /// 글자는 줄이거나 자르지 않는다. 번호 → 간격 → 이름을 위에서 아래로 쌓고, 떨어지는 플레이어의
    /// 흔들림 범위 아래에 두는 것을 우선한다. 화면 아래 안전 여백 안에 다 들어가지 않으면 플레이어와 겹치더라도
    /// 글자를 화면 안에 둔다(<see cref="IsConstrained"/>). 화면 높이 자체를 넘으면 아래 정렬로 두고 한 번 경고한다.
    /// 위치·크기·글자 크기만 바꾼다 — 투명도·활성·레이캐스트·색은 건드리지 않는다.
    /// </summary>
    public sealed class StageFallReadability : MonoBehaviour
    {
        private const float SAFE_MARGIN = 24f;
        private const float PLAYER_GAP = 12f;
        private const float PLAYER_BOB_Y = 8f;
        private const float PLAYER_TILT_DEGREES = 9f;
        private const float WIDTH_PADDING = 8f;

        private RectTransform body;
        private Canvas canvas;
        private RectTransform group;
        private Text stageLabel;
        private Text titleLabel;

        // 원본(배율 1) 배치 — Attach에서 한 번만 잡는다.
        private Vector2 groupPosition;
        private Vector2 groupSize;
        private Vector2 stagePosition;
        private Vector2 stageSize;
        private Vector2 titlePosition;
        private Vector2 titleSize;
        private int stageFont;
        private int titleFont;
        private float topPadding;
        private float bottomPadding;
        private float labelGap;
        private float originalStackTop;
        private float playerEnvelopeBottom;

        // 마지막 적용 조건 — 바뀐 것이 있을 때만 다시 잰다.
        private bool hasApplied;
        private float lastScale;
        private Vector2 lastCanvasSize;
        private float lastCanvasScale;
        private string lastStageText;
        private string lastTitleText;
        private Font lastStageFontAsset;
        private Font lastTitleFontAsset;
        private bool isOverflowing;

        /// <summary>글자 묶음이 플레이어 흔들림 범위 아래에 다 들어가지 못해 겹치거나 화면을 넘는 상태.</summary>
        public bool IsConstrained { get; private set; }

        /// <summary>
        /// <paramref name="modalBody"/>에 붙인다. 이미 붙어 있으면 그것을 돌려준다(원본은 처음 붙을 때만 잡는다).
        /// 플레이어는 움직이기 전의 크기·중심만 읽는다 — 매 프레임 흔들리는 실제 경계는 쓰지 않는다.
        /// </summary>
        public static StageFallReadability Attach(Transform modalBody, CanvasGroup titleGroup, Text stageLabel, Text titleLabel, RectTransform playerRect)
        {
            if (modalBody == null || titleGroup == null || stageLabel == null || titleLabel == null || playerRect == null) return null;
            var existing = modalBody.GetComponent<StageFallReadability>();
            if (existing != null) return existing;

            var component = modalBody.gameObject.AddComponent<StageFallReadability>();
            component.body = modalBody as RectTransform;
            component.canvas = modalBody.GetComponentInParent<Canvas>(true);
            component.group = (RectTransform)titleGroup.transform;
            component.stageLabel = stageLabel;
            component.titleLabel = titleLabel;
            component.CaptureBaseline(playerRect);
            return component;
        }

        private void CaptureBaseline(RectTransform playerRect)
        {
            groupPosition = group.anchoredPosition;
            groupSize = group.sizeDelta;
            stagePosition = stageLabel.rectTransform.anchoredPosition;
            stageSize = stageLabel.rectTransform.sizeDelta;
            titlePosition = titleLabel.rectTransform.anchoredPosition;
            titleSize = titleLabel.rectTransform.sizeDelta;
            stageFont = stageLabel.fontSize;
            titleFont = titleLabel.fontSize;

            // 원본: 번호 아래 17 / 이름 위 15 → 간격 2. 묶음 위 여백 19, 아래 여백 15.
            float stageTop = stagePosition.y + stageSize.y * 0.5f;
            float stageBottom = stagePosition.y - stageSize.y * 0.5f;
            float titleTop = titlePosition.y + titleSize.y * 0.5f;
            float titleBottom = titlePosition.y - titleSize.y * 0.5f;
            labelGap = Mathf.Max(0f, stageBottom - titleTop);
            topPadding = groupSize.y * 0.5f - stageTop;
            bottomPadding = titleBottom + groupSize.y * 0.5f;
            originalStackTop = groupPosition.y + stageTop;

            // 흔들림 전체 범위의 아래 끝(보수적): 중심 Y가 ±8, 기울기 ±9도일 때 사각형이 내려오는 가장 낮은 점.
            Vector2 half = playerRect.sizeDelta * 0.5f;
            float radians = PLAYER_TILT_DEGREES * Mathf.Deg2Rad;
            float verticalExtent = half.y * Mathf.Cos(radians) + half.x * Mathf.Sin(radians);
            playerEnvelopeBottom = playerRect.anchoredPosition.y - PLAYER_BOB_Y - verticalExtent;
        }

        /// <summary>현재 배율·화면 크기·문자열로 원본에서 다시 배치한다.</summary>
        public void Apply()
        {
            if (group == null || stageLabel == null || titleLabel == null) return;

            float scale = MenuTextReadability.Scale;
            SetFont(stageLabel, stageFont, scale);
            SetFont(titleLabel, titleFont, scale);

            Vector2 canvasSize = ReadCanvasSize();
            Vector2 layoutSize = canvasSize.x > 0f && canvasSize.y > 0f ? canvasSize : new Vector2(1920f, 1080f);

            float width = ResolveWidth(scale, layoutSize.x);
            float stageHeight = MeasureHeight(stageLabel, width, stageSize.y);
            float titleHeight = MeasureHeight(titleLabel, width, titleSize.y);
            float stackHeight = stageHeight + labelGap + titleHeight;

            float safeBottom = -layoutSize.y * 0.5f + SAFE_MARGIN;
            float safeHeight = layoutSize.y - SAFE_MARGIN * 2f;
            float playerLimit = playerEnvelopeBottom - PLAYER_GAP;

            // 원본 위치를 우선하고, 아래가 넘치면 위로 올린다.
            float stackTop = originalStackTop;
            if (stackTop - stackHeight < safeBottom) stackTop = safeBottom + stackHeight;
            bool fitsBelowPlayer = stackTop <= playerLimit;
            bool overflows = stackHeight > safeHeight;
            // 플레이어 아래에 못 들어가면 화면 아래 안전 여백에 붙인다 — 겹침을 줄이고 글자는 화면 안에.
            // 화면 높이 자체를 넘으면 아래 정렬 그대로 둔다(위쪽이 화면 밖으로 나간다).
            if (!fitsBelowPlayer || overflows) stackTop = safeBottom + stackHeight;

            float groupHeight = topPadding + stackHeight + bottomPadding;
            float groupWidth = width + (groupSize.x - Mathf.Max(stageSize.x, titleSize.x));
            float groupCenter = stackTop + topPadding - groupHeight * 0.5f;
            group.sizeDelta = new Vector2(Mathf.Max(width, groupWidth), groupHeight);
            group.anchoredPosition = new Vector2(groupPosition.x, groupCenter);

            float localStageTop = groupHeight * 0.5f - topPadding;
            float localTitleTop = localStageTop - stageHeight - labelGap;
            stageLabel.rectTransform.sizeDelta = new Vector2(width, stageHeight);
            stageLabel.rectTransform.anchoredPosition = new Vector2(stagePosition.x, localStageTop - stageHeight * 0.5f);
            titleLabel.rectTransform.sizeDelta = new Vector2(width, titleHeight);
            titleLabel.rectTransform.anchoredPosition = new Vector2(titlePosition.x, localTitleTop - titleHeight * 0.5f);

            IsConstrained = !fitsBelowPlayer || overflows;
            if (overflows && !isOverflowing)
                Debug.LogWarning($"[StageFallReadability] 스테이지 이름 묶음({stackHeight:0}px)이 화면 안전 높이({safeHeight:0}px)보다 커 아래 정렬로 둔다.");
            isOverflowing = overflows;

            hasApplied = true;
            lastScale = scale;
            lastCanvasSize = canvasSize;
            lastCanvasScale = canvas != null ? canvas.scaleFactor : 1f;
            lastStageText = stageLabel.text;
            lastTitleText = titleLabel.text;
            lastStageFontAsset = stageLabel.font;
            lastTitleFontAsset = titleLabel.font;
        }

        /// <summary>배율·화면·문자열·글꼴 중 바뀐 것이 있을 때만 <see cref="Apply"/>한다.</summary>
        public void RefreshIfNeeded()
        {
            if (group == null || stageLabel == null || titleLabel == null) return;
            if (!hasApplied || lastScale != MenuTextReadability.Scale || lastCanvasSize != ReadCanvasSize() ||
                lastCanvasScale != (canvas != null ? canvas.scaleFactor : 1f) ||
                lastStageText != stageLabel.text || lastTitleText != titleLabel.text ||
                lastStageFontAsset != stageLabel.font || lastTitleFontAsset != titleLabel.font)
                Apply();
        }

        private void LateUpdate() => RefreshIfNeeded();

        /// <summary>실제 부모 캔버스의 로컬 크기(화면 픽셀이 아니다).</summary>
        private Vector2 ReadCanvasSize()
        {
            var rect = canvas != null ? canvas.transform as RectTransform : null;
            if (rect == null) rect = body;
            return rect != null ? rect.rect.size : Vector2.zero;
        }

        /// <summary>
        /// 원본 폭(1400)을 화면 안전 폭으로 제한한다. 확대 배율에서 한 줄이 원본 폭을 넘을 때만 배율만큼 넓힌다.
        /// 배율 1이면 언제나 원본 폭으로 돌아간다.
        /// </summary>
        private float ResolveWidth(float scale, float canvasWidth)
        {
            float labelWidth = Mathf.Max(stageSize.x, titleSize.x);
            float available = Mathf.Max(1f, canvasWidth - SAFE_MARGIN * 2f);
            float baseWidth = Mathf.Min(labelWidth, available);
            if (scale <= 1f) return baseWidth;

            float cap = Mathf.Max(baseWidth, Mathf.Min(labelWidth * scale, available));
            float needed = Mathf.Max(stageLabel.preferredWidth, titleLabel.preferredWidth) + WIDTH_PADDING;
            return Mathf.Clamp(needed, baseWidth, cap);
        }

        /// <summary>폭을 먼저 정한 뒤 줄바꿈된 전체 높이를 잰다. 원본 높이보다 작아지지 않는다.</summary>
        private static float MeasureHeight(Text text, float width, float baseHeight)
        {
            text.rectTransform.sizeDelta = new Vector2(width, baseHeight);
            return Mathf.Max(baseHeight, Mathf.Ceil(text.preferredHeight));
        }

        private static void SetFont(Text text, int font, float scale)
        {
            text.fontSize = Mathf.RoundToInt(font * scale);
            text.resizeTextForBestFit = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
        }
    }
}
