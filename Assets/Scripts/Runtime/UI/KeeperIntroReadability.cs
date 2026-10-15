using UnityEngine;
using UnityEngine.UI;

namespace Abyss.Runtime.UI
{
    /// <summary>수호자 하단 띠의 메뉴 확대와 측정 배치. 대사/heading은 서사 도우미가 소유한다.</summary>
    [DefaultExecutionOrder(100)]
    public sealed class KeeperIntroReadability : MonoBehaviour
    {
        private const float MARGIN = 24f;
        private const float GAP = 8f;
        private const float PORTRAIT_SIZE = 96f;
        private const float PORTRAIT_RESERVE = 112f;
        private RectTransform body;
        private RectTransform band;
        private RectTransform titleLayout;
        private RectTransform lineRoot;
        private Text title;
        private Text epithet;
        private Text line;
        private Text pager;
        private Text hint;
        private NarrativeReadability narrative;
        private Canvas canvas;
        private Image portrait;
        private bool hasPortrait;
        private Vector2 portraitSize;
        private readonly Vector3[] canvasCorners = new Vector3[4];
        private bool isDirty = true;
        private Vector2 lastSize;
        private float lastMenu = -1f;
        private float lastNarrative = -1f;
        private float lastCanvasScale;
        private bool lastContrast;
        private string lastTitleText;
        private string lastEpithetText;
        private string lastLineText;
        private string lastPagerText;
        private string lastHintText;
        private Font lastTitleFont;
        private Font lastEpithetFont;
        private Font lastLineFont;
        private Font lastPagerFont;
        private Font lastHintFont;
        private bool lastHintActive;

        /// <summary>전체 행을 축소 없이 배치하면 안전 높이를 넘는 상태. 지원 해상도에서 사용자 확인이 필요하다.</summary>
        public bool IsConstrained { get; private set; }

        public static KeeperIntroReadability Attach(RectTransform body, RectTransform band,
            RectTransform titleLayout, RectTransform lineRoot, Text title, Text epithet,
            Text line, Text pager, Text hint)
        {
            var component = body.gameObject.AddComponent<KeeperIntroReadability>();
            component.body = body;
            component.band = band;
            component.titleLayout = titleLayout;
            component.lineRoot = lineRoot;
            component.title = title;
            component.epithet = epithet;
            component.line = line;
            component.pager = pager;
            component.hint = hint;
            component.narrative = line.GetComponent<NarrativeReadability>();
            component.canvas = body.GetComponentInParent<Canvas>();
            return component;
        }

        // PlaceLeadingIcon이 현재 소개의 그림/활성 상태를 정한 직후에만 호출한다.
        public void CapturePortraitSource()
        {
            var found = title.transform.Find("ArtLeadingIcon");
            portrait = found != null ? found.GetComponent<Image>() : null;
            if (portrait != null) portraitSize = portrait.rectTransform.sizeDelta;
            hasPortrait = portrait != null && portrait.enabled && portrait.gameObject.activeSelf && portrait.sprite != null;
            isDirty = true;
            RefreshIfNeeded();
        }

        private Vector2 ReadSize()
        {
            // 이 패널 body는 캔버스를 채운다. 캔버스 사각형을 body 로컬 좌표로 변환한다.
            var rect = canvas != null ? canvas.transform as RectTransform : body;
            if (rect == null) return body.rect.size;

            rect.GetWorldCorners(canvasCorners);
            Vector3 min = body.InverseTransformPoint(canvasCorners[0]);
            Vector3 max = body.InverseTransformPoint(canvasCorners[2]);
            return new Vector2(Mathf.Abs(max.x - min.x), Mathf.Abs(max.y - min.y));
        }

        private void OnEnable() => isDirty = true;
        private void LateUpdate() => RefreshIfNeeded();

        public void RefreshIfNeeded()
        {
            if (body == null || title == null) return;
            Vector2 size = ReadSize();
            float canvasScale = canvas != null ? canvas.scaleFactor : 1f;
            if (!isDirty && lastSize == size && lastMenu == MenuTextReadability.Scale &&
                lastNarrative == NarrativeReadability.Scale && lastContrast == NarrativeReadability.HighContrast &&
                lastCanvasScale == canvasScale && lastTitleText == title.text && lastEpithetText == epithet.text &&
                lastLineText == line.text && lastPagerText == pager.text && lastHintText == hint.text && lastTitleFont == title.font &&
                lastEpithetFont == epithet.font && lastLineFont == line.font && lastPagerFont == pager.font &&
                lastHintFont == hint.font && lastHintActive == hint.gameObject.activeSelf) return;
            Apply(size);
            lastSize = size;
            lastMenu = MenuTextReadability.Scale;
            lastNarrative = NarrativeReadability.Scale;
            lastContrast = NarrativeReadability.HighContrast;
            lastCanvasScale = canvasScale;
            lastTitleText = title.text;
            lastEpithetText = epithet.text;
            lastLineText = line.text;
            lastPagerText = pager.text;
            lastHintText = hint.text;
            lastTitleFont = title.font;
            lastEpithetFont = epithet.font;
            lastLineFont = line.font;
            lastPagerFont = pager.font;
            lastHintFont = hint.font;
            lastHintActive = hint.gameObject.activeSelf;
            isDirty = false;
        }

        private static float Fit(Text text, int baseFont, float width, float minimumHeight)
        {
            text.fontSize = Mathf.RoundToInt(baseFont * MenuTextReadability.Scale);
            text.resizeTextForBestFit = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.rectTransform.sizeDelta = new Vector2(width, minimumHeight);
            float height = Mathf.Max(minimumHeight, Mathf.Ceil(text.preferredHeight) + 8f);
            text.rectTransform.sizeDelta = new Vector2(width, height);
            return height;
        }

        private float FitLine(float width)
        {
            // 글자 크기·색·고대비는 NarrativeReadability만 쓴다. 확대 결과를 원본 폰트로 재캡처하지 않는다.
            narrative.Apply();
            var rect = line.rectTransform;
            rect.sizeDelta = new Vector2(Mathf.Min(1400f, Mathf.Max(1f, width - 16f)), 44f);
            float height = Mathf.Max(44f, Mathf.Ceil(line.preferredHeight) + 8f);
            rect.sizeDelta = new Vector2(rect.sizeDelta.x, height);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            narrative.CaptureLayout();
            narrative.Apply();
            return rect.rect.height + (NarrativeReadability.HighContrast && !string.IsNullOrEmpty(line.text) ? 12f : 0f);
        }

        private void Apply(Vector2 size)
        {
            float width = Mathf.Max(1f, size.x - MARGIN * 2f);
            float safeHeight = Mathf.Max(1f, size.y - MARGIN * 2f);
            float epithetHeight = Fit(epithet, 22, Mathf.Min(1600f, width), 32f);
            float pagerHeight = Fit(pager, 17, Mathf.Min(600f, width), 26f);
            float hintHeight = Fit(hint, 17, Mathf.Min(600f, width), 26f);
            float lineHeight = FitLine(width);
            bool showHint = hint.gameObject.activeSelf;
            float lowerHeight = epithetHeight + lineHeight + pagerHeight + GAP * 3f;
            if (showHint) lowerHeight += hintHeight + GAP;
            bool showPortrait = hasPortrait && width > PORTRAIT_RESERVE + 100f;
            float titleHeight = Fit(title, 46, Mathf.Min(1600f, Mathf.Max(1f, width - (showPortrait ? PORTRAIT_RESERVE : 0f))), 60f);
            float titleRow = showPortrait ? Mathf.Max(titleHeight, PORTRAIT_SIZE) : titleHeight;
            if (showPortrait && lowerHeight + titleRow > safeHeight)
            {
                showPortrait = false;
                titleHeight = Fit(title, 46, Mathf.Min(1600f, width), 60f);
                titleRow = titleHeight;
            }
            float total = titleRow + lowerHeight;
            IsConstrained = total > safeHeight;
            // 하단에서 위로 확장한다. 물리적으로 들어가지 않으면 축소 없이 상단부터 배치한다.
            float bottom = IsConstrained ? size.y * 0.5f - MARGIN - total : -size.y * 0.5f + MARGIN;
            float cursor = bottom;
            if (showHint)
            {
                Place(hint.rectTransform, 0f, cursor + hintHeight * 0.5f);
                cursor += hintHeight + GAP;
            }
            Place(pager.rectTransform, 0f, cursor + pagerHeight * 0.5f);
            cursor += pagerHeight + GAP;
            Place(lineRoot, 0f, cursor + lineHeight * 0.5f);
            cursor += lineHeight + GAP;
            float epithetY = cursor + epithetHeight * 0.5f;
            cursor += epithetHeight + GAP;
            float titleY = cursor + titleRow * 0.5f;
            // heading 자체는 NarrativeReadability 소유다. 전용 부모만 옮긴다.
            Place(titleLayout, 0f, titleY);
            Place(title.rectTransform, showPortrait ? PORTRAIT_RESERVE * 0.5f : 0f, 0f);
            Place(epithet.rectTransform, 0f, epithetY - titleY);
            if (portrait != null)
            {
                portrait.enabled = showPortrait;
                if (showPortrait)
                {
                    var rect = portrait.rectTransform;
                    float textWidth = Mathf.Min(title.preferredWidth, title.rectTransform.rect.width);
                    rect.anchoredPosition = new Vector2(-textWidth * 0.5f - 16f - PORTRAIT_SIZE * 0.5f, 0f);
                    rect.sizeDelta = portraitSize;
                }
            }
            band.sizeDelta = new Vector2(0f, Mathf.Min(size.y, Mathf.Max(230f, total + MARGIN * 2f)));
        }

        private static void Place(RectTransform rect, float x, float y) => rect.anchoredPosition = new Vector2(x, y);
    }
}
