using Abyss.Runtime.Localization;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Abyss.Runtime.UI
{
    /// <summary>
    /// 엔딩 통계 화면의 글자 확대·긴 통계 스크롤. 제목과 기존 확인 안내는 제자리에 고정하고 본문만 스크롤한다.
    ///
    /// 통계 그룹(<see cref="CanvasGroup"/>)에 붙어 통계가 보일 때만 동작한다 — 자막·크레딧 동안에는
    /// 공용 안내 문구를 건드리지 않고, 통계가 꺼지면 안내의 원래 배치를 돌려준다.
    /// 페이드는 그룹 알파 하나가 소유하므로 여기서는 어떤 알파도 만지지 않는다.
    /// </summary>
    public sealed class EndingStatsReadability : MonoBehaviour
    {
        private const float EDGE_MARGIN = 16f;
        private const float HINT_BOTTOM_MARGIN = 8f;
        private const float TITLE_BODY_GAP = 15f;
        private const float BODY_HINT_GAP = 16f;
        private const float BODY_SIDE_MARGIN = 64f;
        private const float WIDTH_GROWTH = 200f;
        private const float SCROLL_HINT_FONT = 16f;
        private const float SCROLL_HINT_GAP = 6f;
        private const float SCROLLBAR_WIDTH = 8f;
        private const float SCROLLBAR_OFFSET = 12f;
        private const float WHEEL_PIXELS = 0.5f;
        private const float KEY_SCROLL_SPEED = 900f;
        private const float STICK_SCROLL_SPEED = 600f;
        private const float STICK_DEADZONE = 0.2f;

        private sealed class Baseline
        {
            public Text Text;
            public Transform Parent;
            public int SiblingIndex;
            public Vector2 AnchorMin, AnchorMax, Pivot, Position, Size;
            public int Font;
            public bool BestFit;
            public HorizontalWrapMode HorizontalWrap;
            public VerticalWrapMode VerticalWrap;
        }

        private CanvasGroup group;
        private RectTransform groupRect;
        private RectTransform area;
        private Baseline title;
        private Baseline body;
        private Baseline hint;

        private ScrollRect readingScroll;
        private Scrollbar readingBar;
        private Text readingHint;

        private bool initialized;
        private bool isPendingTop = true;
        private bool wasOverflow;
        private float lastScale = -1f;
        private float lastCanvasScale = -1f;
        private Vector2 lastArea;
        private int lastContent;
        private string lastHintText;
        private LocalizationLanguage lastLanguage;

        /// <summary>마지막 배치에서 통계 본문이 스크롤 영역으로 넘어갔는가. 넘어갔으면 자동 진행을 멈춘다.</summary>
        public bool IsOverflow { get; private set; }

        /// <summary>
        /// 통계 그룹에 붙인다. 기준값은 여기서 한 번만 잡는다 — 그룹이 꺼져 있어도 RectTransform 값은 읽힌다.
        /// 같은 그룹에 이미 붙어 있으면 그것을 돌려준다.
        /// </summary>
        public static EndingStatsReadability Attach(CanvasGroup group, Text title, Text body, Text skipHint, Transform canvasBody)
        {
            if (group == null || title == null || body == null || skipHint == null || canvasBody == null) return null;
            var existing = group.GetComponent<EndingStatsReadability>();
            if (existing != null) return existing;

            var helper = group.gameObject.AddComponent<EndingStatsReadability>();
            helper.group = group;
            helper.groupRect = group.transform as RectTransform;
            helper.area = canvasBody as RectTransform;
            helper.title = Capture(title);
            helper.body = Capture(body);
            helper.hint = Capture(skipHint);
            helper.BuildScroll();
            helper.initialized = true;
            return helper;
        }

        private static Baseline Capture(Text text)
        {
            var rect = text.rectTransform;
            return new Baseline
            {
                Text = text, Parent = rect.parent, SiblingIndex = rect.GetSiblingIndex(),
                AnchorMin = rect.anchorMin, AnchorMax = rect.anchorMax, Pivot = rect.pivot,
                Position = rect.anchoredPosition, Size = rect.sizeDelta,
                Font = text.fontSize, BestFit = text.resizeTextForBestFit,
                HorizontalWrap = text.horizontalOverflow, VerticalWrap = text.verticalOverflow,
            };
        }

        private void BuildScroll()
        {
            var center = new Vector2(0.5f, 0.5f);
            var viewport = UiFactory.CreateRect(groupRect, "StatsReadingViewport", center, center, center, Vector2.zero, Vector2.one);
            // 끌기 입력을 받을 표면. 검은 배경 위에서 거의 보이지 않는 값.
            var surface = viewport.AddComponent<Image>();
            surface.color = new Color(1f, 1f, 1f, 0.01f);
            viewport.AddComponent<RectMask2D>();

            readingScroll = viewport.AddComponent<ScrollRect>();
            readingScroll.viewport = (RectTransform)viewport.transform;
            var top = new Vector2(0.5f, 1f);
            readingScroll.content = (RectTransform)UiFactory.CreateRect(viewport.transform, "Content", top, top, top,
                Vector2.zero, Vector2.one).transform;
            readingScroll.horizontal = false;
            readingScroll.vertical = true;
            readingScroll.movementType = ScrollRect.MovementType.Clamped;
            // 휠은 아래 LateUpdate가 직접 읽는다. 여기서도 받으면 한 번의 휠이 두 번 굴러간다.
            readingScroll.scrollSensitivity = 0f;

            var bar = UiFactory.CreateRect(groupRect, "StatsReadingScrollbar", center, center, center, Vector2.zero,
                new Vector2(SCROLLBAR_WIDTH, 1f));
            var track = bar.AddComponent<Image>();
            track.color = new Color(1f, 1f, 1f, 0.15f);
            readingBar = bar.AddComponent<Scrollbar>();
            readingBar.direction = Scrollbar.Direction.BottomToTop;
            readingBar.navigation = new Navigation { mode = Navigation.Mode.None };
            var handle = UiFactory.CreateRect(bar.transform, "Handle", Vector2.zero, Vector2.one, center, Vector2.zero, Vector2.zero);
            var handleImage = handle.AddComponent<Image>();
            handleImage.color = new Color(0.9f, 0.88f, 0.8f, 0.8f);
            readingBar.handleRect = (RectTransform)handle.transform;
            readingBar.targetGraphic = handleImage;
            readingScroll.verticalScrollbar = readingBar;
            readingScroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;

            readingHint = UiFactory.CreateLabel(groupRect, "StatsReadingHint", Vector2.zero, new Vector2(600f, 24f), string.Empty,
                Mathf.RoundToInt(SCROLL_HINT_FONT), new Color(0.6f, 0.6f, 0.7f), TextAnchor.MiddleCenter);

            viewport.SetActive(false);
            bar.SetActive(false);
            readingHint.gameObject.SetActive(false);
        }

        /// <summary>
        /// 현재 배율·캔버스 크기·본문으로 다시 배치한다. 통계가 화면에 있을 때만 동작한다.
        /// 새 본문(또는 통계가 새로 켜짐)이면 맨 위에서, 배율·크기 변화면 읽던 위치 비율을 유지한다.
        /// </summary>
        public void Apply()
        {
            if (!initialized || group == null || !group.gameObject.activeInHierarchy) return;

            float scale = MenuTextReadability.Scale;
            int content = ContentSignature();
            bool reset = isPendingTop || content != lastContent || !wasOverflow;
            float previous = readingScroll.verticalNormalizedPosition;

            Vector2 size = AreaSize();
            float halfHeight = size.y * 0.5f;
            // 그룹 자식은 그룹 중심 기준이다. 캔버스 중심 기준 좌표에서 그룹 위치를 뺀다.
            float offsetY = groupRect != null ? groupRect.localPosition.y : 0f;
            float maxWidth = Mathf.Max(1f, size.x - EDGE_MARGIN * 2f);

            // 기존 확인 안내 — 아래쪽 고정. 캔버스 안에 들어오도록만 끌어올린다.
            var hintText = hint.Text;
            Style(hintText, hint.Font, scale);
            float hintWidth = Mathf.Min(hint.Size.x + WIDTH_GROWTH * (scale - 1f), maxWidth);
            float hintHeight = Mathf.Max(hint.Size.y, Height(hintText, hintWidth) + 4f);
            float hintY = Mathf.Max(hint.Position.y, -halfHeight + HINT_BOTTOM_MARGIN + hintHeight * 0.5f);
            PlaceAtBaseline(hint, hint.Position.x, hintY, hintWidth, hintHeight);
            float hintTop = hintY + hintHeight * 0.5f;

            // 제목 — 위쪽 고정.
            var titleText = title.Text;
            Style(titleText, title.Font, scale);
            float titleWidth = Mathf.Min(title.Size.x + WIDTH_GROWTH * (scale - 1f), maxWidth);
            float titleHeight = Mathf.Max(title.Size.y, Height(titleText, titleWidth) + 4f);
            float titleCenter = Mathf.Min(title.Position.y + offsetY, halfHeight - EDGE_MARGIN - titleHeight * 0.5f);
            titleCenter = Mathf.Max(titleCenter, hintTop + BODY_HINT_GAP + titleHeight * 0.5f);
            PlaceAtBaseline(title, title.Position.x, titleCenter - offsetY, titleWidth, titleHeight);
            float titleBottom = titleCenter - titleHeight * 0.5f;

            // 본문이 쓸 수 있는 띠(캔버스 중심 기준).
            float baseTop = body.Position.y + offsetY + body.Size.y * 0.5f;
            float baseBottom = body.Position.y + offsetY - body.Size.y * 0.5f;
            float bandTop = Mathf.Min(baseTop, titleBottom - TITLE_BODY_GAP);
            float bandBottom = hintTop + BODY_HINT_GAP;
            float space = Mathf.Max(1f, bandTop - bandBottom);

            var bodyText = body.Text;
            Style(bodyText, body.Font, scale);
            float bodyWidth = Mathf.Min(body.Size.x + WIDTH_GROWTH * (scale - 1f), Mathf.Max(1f, size.x - BODY_SIDE_MARGIN));
            float fullHeight = Height(bodyText, bodyWidth) + 4f;
            bool overflow = fullHeight > space;

            readingHint.text = Loc.CurrentLanguage switch
            {
                LocalizationLanguage.Korean => "휠 · 드래그 · Page Up/Down · 오른쪽 스틱",
                LocalizationLanguage.Japanese => "ホイール · ドラッグ · Page Up/Down · 右スティック",
                _ => "Wheel · Drag · Page Up/Down · Right stick",
            };

            if (!overflow)
            {
                ReturnBody();
                bool isBaseline = Mathf.Approximately(scale, 1f) && Mathf.Approximately(bodyWidth, body.Size.x) &&
                    fullHeight <= body.Size.y && baseTop <= bandTop + 0.01f && baseBottom >= bandBottom - 0.01f;
                var rect = bodyText.rectTransform;
                if (isBaseline)
                {
                    rect.sizeDelta = body.Size;
                    rect.anchoredPosition = body.Position;
                }
                else
                {
                    float height = Mathf.Clamp(Mathf.Max(fullHeight, body.Size.y), 1f, space);
                    rect.sizeDelta = new Vector2(bodyWidth, height);
                    rect.anchoredPosition = new Vector2(body.Position.x, bandTop - height * 0.5f - offsetY);
                }
                SetScrollActive(false);
            }
            else
            {
                Style(readingHint, Mathf.RoundToInt(SCROLL_HINT_FONT), scale);
                float readingHintHeight = Height(readingHint, bodyWidth) + 4f;
                var readingHintRect = readingHint.rectTransform;
                readingHintRect.sizeDelta = new Vector2(bodyWidth, readingHintHeight);
                readingHintRect.anchoredPosition = new Vector2(body.Position.x, bandBottom + readingHintHeight * 0.5f - offsetY);

                float viewBottom = bandBottom + readingHintHeight + SCROLL_HINT_GAP;
                float viewHeight = Mathf.Max(1f, bandTop - viewBottom);
                float viewCenter = (bandTop + viewBottom) * 0.5f - offsetY;
                var viewport = readingScroll.viewport;
                viewport.sizeDelta = new Vector2(bodyWidth, viewHeight);
                viewport.anchoredPosition = new Vector2(body.Position.x, viewCenter);

                var barRect = (RectTransform)readingBar.transform;
                barRect.sizeDelta = new Vector2(SCROLLBAR_WIDTH, viewHeight);
                barRect.anchoredPosition = new Vector2(body.Position.x + bodyWidth * 0.5f + SCROLLBAR_OFFSET, viewCenter);

                float contentHeight = Mathf.Max(viewHeight, fullHeight);
                readingScroll.content.sizeDelta = new Vector2(bodyWidth, contentHeight);

                // 같은 Text 참조를 옮기기만 한다. 앵커·피벗은 원래 값(중심) 그대로 두고 콘텐츠를 가득 채운다.
                var rect = bodyText.rectTransform;
                if (rect.parent != readingScroll.content) rect.SetParent(readingScroll.content, false);
                rect.anchorMin = body.AnchorMin;
                rect.anchorMax = body.AnchorMax;
                rect.pivot = body.Pivot;
                rect.sizeDelta = new Vector2(bodyWidth, contentHeight);
                rect.anchoredPosition = Vector2.zero;
                SetScrollActive(true);

                readingScroll.StopMovement();
                readingScroll.verticalNormalizedPosition = reset ? 1f : previous;
            }

            IsOverflow = overflow;
            wasOverflow = overflow;
            isPendingTop = false;
            lastScale = scale;
            lastContent = content;
            lastArea = size;
            lastCanvasScale = CanvasScale();
            lastHintText = hintText.text;
            lastLanguage = Loc.CurrentLanguage;
        }

        /// <summary>본문을 원래 부모·형제 순서·앵커·피벗으로 돌려놓는다.</summary>
        private void ReturnBody()
        {
            var rect = body.Text.rectTransform;
            if (rect.parent != body.Parent)
            {
                rect.SetParent(body.Parent, false);
                rect.SetSiblingIndex(body.SiblingIndex);
            }
            rect.anchorMin = body.AnchorMin;
            rect.anchorMax = body.AnchorMax;
            rect.pivot = body.Pivot;
        }

        private void SetScrollActive(bool isActive)
        {
            if (readingScroll.gameObject.activeSelf != isActive) readingScroll.gameObject.SetActive(isActive);
            if (readingBar.gameObject.activeSelf != isActive) readingBar.gameObject.SetActive(isActive);
            if (readingHint.gameObject.activeSelf != isActive) readingHint.gameObject.SetActive(isActive);
        }

        private static void PlaceAtBaseline(Baseline baseline, float x, float y, float width, float height)
        {
            var rect = baseline.Text.rectTransform;
            rect.anchorMin = baseline.AnchorMin;
            rect.anchorMax = baseline.AnchorMax;
            rect.pivot = baseline.Pivot;
            rect.sizeDelta = new Vector2(width, height);
            rect.anchoredPosition = new Vector2(x, y);
        }

        /// <summary>글자 크기만 배율로 키운다. 자동 맞춤(축소)은 끄고 줄바꿈으로 전부 보이게 한다. 줄 간격은 건드리지 않는다.</summary>
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

        private float CanvasScale()
        {
            var canvas = GetComponentInParent<Canvas>();
            return canvas != null ? canvas.scaleFactor : 1f;
        }

        private int ContentSignature()
        {
            int hash = (title.Text.text ?? string.Empty).GetHashCode();
            return unchecked(hash * 31 + (body.Text.text ?? string.Empty).GetHashCode());
        }

        /// <summary>통계가 새로 켜졌다 — 다음 배치는 맨 위에서 시작하고, 변화 비교 기준을 비워 다시 계산하게 한다.</summary>
        private void OnEnable()
        {
            isPendingTop = true;
            lastScale = -1f;
        }

        /// <summary>
        /// 통계가 꺼지면 공용 안내 문구의 원래 배치를 돌려준다 — 다음 재생의 자막·크레딧은 기준 모습을 쓴다.
        /// 문구·색·활성 상태는 패널이 소유하므로 건드리지 않는다.
        /// </summary>
        private void OnDisable()
        {
            if (!initialized || hint == null || hint.Text == null) return;
            var text = hint.Text;
            text.fontSize = hint.Font;
            text.resizeTextForBestFit = hint.BestFit;
            text.horizontalOverflow = hint.HorizontalWrap;
            text.verticalOverflow = hint.VerticalWrap;
            PlaceAtBaseline(hint, hint.Position.x, hint.Position.y, hint.Size.x, hint.Size.y);
        }

        /// <summary>
        /// 다음 배치를 맨 위에서 시작하게 한다. 같은 요약으로 다시 재생해 본문이 그대로여도 처음부터 읽게 하는 용도 —
        /// 배율·크기 변화만으로 다시 배치할 때는 부르지 않는다(읽던 위치 유지).
        /// </summary>
        public void ResetReading() => isPendingTop = true;

        /// <summary>
        /// 배율·캔버스 크기·캔버스 배율·본문·확인 안내·언어 중 무엇이라도 마지막 배치와 달라졌을 때만 다시 배치한다.
        /// 패널의 Update가 이 컴포넌트의 LateUpdate보다 먼저 돌므로, 자동 진행 판정 직전에 불러 넘침 여부를 최신으로 맞춘다.
        /// </summary>
        public void RefreshIfNeeded()
        {
            if (!initialized || group == null || !group.gameObject.activeInHierarchy) return;
            bool changed = lastScale != MenuTextReadability.Scale || lastContent != ContentSignature() ||
                lastArea != AreaSize() || lastCanvasScale != CanvasScale() ||
                lastHintText != hint.Text.text || lastLanguage != Loc.CurrentLanguage;
            if (changed) Apply();
        }

        private void LateUpdate()
        {
            if (!initialized) return;
            RefreshIfNeeded();

            if (!IsOverflow || !readingScroll.gameObject.activeInHierarchy || SaveStatusOverlay.IsCapturingInput) return;
            TickScrollInput();
        }

        /// <summary>휠(포인터가 본문 위일 때)·Page Up/Down·Home/End·패드 오른쪽 스틱. 정지 중이라 unscaled 시간을 쓴다.</summary>
        private void TickScrollInput()
        {
            float range = readingScroll.content.rect.height - readingScroll.viewport.rect.height;
            if (range <= 0f) return;

            float pixels = 0f;
            float delta = Time.unscaledDeltaTime;

            var mouse = Mouse.current;
            if (mouse != null)
            {
                var canvas = GetComponentInParent<Canvas>();
                UnityEngine.Camera camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
                if (RectTransformUtility.RectangleContainsScreenPoint(readingScroll.viewport, mouse.position.ReadValue(), camera))
                    pixels += mouse.scroll.ReadValue().y * WHEEL_PIXELS;
            }

            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.homeKey.wasPressedThisFrame)
                {
                    readingScroll.StopMovement();
                    readingScroll.verticalNormalizedPosition = 1f;
                    return;
                }
                if (keyboard.endKey.wasPressedThisFrame)
                {
                    readingScroll.StopMovement();
                    readingScroll.verticalNormalizedPosition = 0f;
                    return;
                }
                if (keyboard.pageUpKey.isPressed) pixels += KEY_SCROLL_SPEED * delta;
                if (keyboard.pageDownKey.isPressed) pixels -= KEY_SCROLL_SPEED * delta;
            }

            var gamepad = Gamepad.current;
            if (gamepad != null)
            {
                float stick = gamepad.rightStick.ReadValue().y;
                if (Mathf.Abs(stick) > STICK_DEADZONE) pixels += stick * STICK_SCROLL_SPEED * delta;
            }

            if (pixels == 0f) return;
            readingScroll.StopMovement();
            readingScroll.verticalNormalizedPosition = Mathf.Clamp01(readingScroll.verticalNormalizedPosition + pixels / range);
        }
    }
}
