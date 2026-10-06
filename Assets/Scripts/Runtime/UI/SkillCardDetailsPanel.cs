using Abyss.Runtime.Draft;
using Abyss.Runtime.Events;
using Abyss.Runtime.Form;
using Abyss.Runtime.Localization;
using UnityEngine;
using UnityEngine.UI;

namespace Abyss.Runtime.UI
{
    /// <summary>
    /// 드래프트 카드 상세 패널(03-skill-draft-system.md §8-2 Layer 2). 마우스 호버 또는 EventSystem 포커스가
    /// 머문 카드 <b>하나</b>의 스킬 데이터를 카드 옆에 보여준다.
    ///
    /// <see cref="DraftPanelPresenter"/>가 드래프트 root 밑에 런타임으로 만든다 — 씬·빌더 재생성이 필요 없고,
    /// root가 꺼지면(드래프트 닫힘·교체 모달·씬 정리) 이 컴포넌트의 OnDisable이 상태를 함께 비운다.
    /// root와 같은 씬 캔버스 층에 있으므로 모달(<see cref="UiSortingOrder.Modal"/>) 이상을 덮지 않는다.
    ///
    /// 표시 전용이다. 그래픽은 모두 raycastTarget=false이고 CanvasGroup도 레이캐스트를 막지 않아
    /// 카드·리롤·스킵 버튼 클릭을 가로채지 않는다.
    ///
    /// 우선순위: 호버 카드 → 포커스 카드. 호버가 빠지면 포커스 카드로 돌아가고 둘 다 없으면 숨긴다.
    /// </summary>
    public sealed partial class SkillCardDetailsPanel : MonoBehaviour
    {
        private const float PANEL_WIDTH = 420f;
        private const float PANEL_WIDE_WIDTH = 560f;
        private const float PADDING = 14f;
        private const float HEADER_GAP = 6f;
        private const float CARD_GAP = 12f;
        private const float SCREEN_MARGIN = 16f;
        private const float MIN_PANEL_HEIGHT = 80f;

        private static readonly Vector3[] cornerBuffer = new Vector3[4];

        private GameObject body;
        private Text headerText;
        private Text bodyText;

        private SkillCardView hoveredCard;
        private SkillCardView focusedCard;
        private SkillCardView shownCard;
        private SkillData shownSkill;

        private bool isLayoutDirty;
        private Vector2 lastBoundsSize;

        /// <summary>parent(드래프트 root) 밑에 화면 전체를 덮는 빈 홀더와 숨긴 패널 본체를 만든다.</summary>
        public static SkillCardDetailsPanel Create(Transform parent)
        {
            var go = UiFactory.CreateRect(parent, "SkillCardDetails", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            UiFactory.Stretch((RectTransform)go.transform);
            go.transform.SetAsLastSibling();

            // root에 레이아웃 그룹이 있어도 카드 배치를 밀어내지 않게 한다.
            go.AddComponent<LayoutElement>().ignoreLayout = true;

            var panel = go.AddComponent<SkillCardDetailsPanel>();
            panel.BuildView();
            return panel;
        }

        private void OnEnable()
        {
            Loc.AddLanguageChangedListener(HandleLanguageChanged);
            GameEvents.OnFormSwapped += HandleFormSwapped;
        }

        private void OnDisable()
        {
            Loc.RemoveLanguageChangedListener(HandleLanguageChanged);
            GameEvents.OnFormSwapped -= HandleFormSwapped;
            Clear();
        }

        private void LateUpdate()
        {
            if (shownCard == null) return;
            if (!CanShow(shownCard))
            {
                Refresh();
                return;
            }
            UpdateLayout();
        }

        /// <summary>카드 선택 버튼에 포인터가 들어오거나 나갔다.</summary>
        public void NotifyHover(SkillCardView card, bool isEntered)
        {
            if (isEntered) hoveredCard = card;
            else if (hoveredCard == card) hoveredCard = null;
            Refresh();
        }

        /// <summary>카드 선택 버튼이 EventSystem 포커스를 얻거나 잃었다. 클릭(스킬 선택)과는 무관하다.</summary>
        public void NotifyFocus(SkillCardView card, bool isSelected)
        {
            if (isSelected) focusedCard = card;
            else if (focusedCard == card) focusedCard = null;
            Refresh();
        }

        /// <summary>카드가 다시 Bind됐다(리롤·교체 취소 재표시·null Bind). 보이던 카드면 내용을 다시 만든다.</summary>
        public void NotifyCardRebound(SkillCardView card)
        {
            if (card == shownCard) shownSkill = null;
            Refresh();
        }

        /// <summary>카드가 꺼지거나 사라진다 — 그 카드를 가리키던 호버·포커스를 잊는다.</summary>
        public void ForgetCard(SkillCardView card)
        {
            if (hoveredCard == card) hoveredCard = null;
            if (focusedCard == card) focusedCard = null;
            Refresh();
        }

        /// <summary>호버·포커스 기록을 모두 지우고 숨긴다(드래프트 닫힘·교체 모달·런 종료).</summary>
        public void Clear()
        {
            hoveredCard = null;
            focusedCard = null;
            Hide();
        }

        /// <summary>현재 호버/포커스 상태로 표시 대상을 다시 고른다.</summary>
        public void Refresh()
        {
            SkillCardView target = CanShow(hoveredCard) ? hoveredCard
                : CanShow(focusedCard) ? focusedCard
                : null;

            if (target == null || body == null)
            {
                Hide();
                return;
            }

            if (target != shownCard || target.CurrentSkill != shownSkill)
            {
                shownCard = target;
                shownSkill = target.CurrentSkill;
                RebuildContent(shownSkill);
                isLayoutDirty = true;
            }

            if (!body.activeSelf) body.SetActive(true);
            UpdateLayout();
        }

        private bool CanShow(SkillCardView card) =>
            card != null && card.isActiveAndEnabled && card.CurrentSkill != null && isActiveAndEnabled;

        private void Hide()
        {
            shownCard = null;
            shownSkill = null;
            if (body != null && body.activeSelf) body.SetActive(false);
        }

        private void HandleLanguageChanged(LocalizationLanguage _) => ForceRebuild();

        // 폼 이름은 현재 폼과 무관하지만 폼 교체 직후의 표시가 옛 상태로 남지 않게 다시 만든다.
        private void HandleFormSwapped(FormData _, FormData __) => ForceRebuild();

        private void ForceRebuild()
        {
            if (shownCard == null) return;
            shownSkill = null;
            Refresh();
        }

        // ── 배치 ──

        /// <summary>
        /// 크기는 내용·화면 크기가 바뀔 때만 다시 재고, 위치는 매 프레임 카드에 맞춘다
        /// (카드가 레이아웃 그룹 안에 있으면 드래프트를 연 첫 프레임에는 자리가 아직 정해지지 않았다).
        /// </summary>
        private void UpdateLayout()
        {
            if (shownCard == null || body == null) return;

            var holder = (RectTransform)transform;
            Rect bounds = ScreenBoundsIn(holder);
            Rect card = RectIn((RectTransform)shownCard.transform, holder);

            if (isLayoutDirty || bounds.size != lastBoundsSize)
            {
                ResizeToContent(bounds);
                lastBoundsSize = bounds.size;
                isLayoutDirty = false;
            }

            var bodyRect = (RectTransform)body.transform;
            Vector2 size = bodyRect.sizeDelta;
            float minX = bounds.xMin + SCREEN_MARGIN;
            float maxX = Mathf.Max(minX, bounds.xMax - SCREEN_MARGIN - size.x);

            // 카드 오른쪽 → 왼쪽 → 둘 다 안 되면 카드 중앙에 겹쳐 화면 안으로 민다.
            float x = card.xMax + CARD_GAP;
            if (x > maxX) x = card.xMin - CARD_GAP - size.x;
            if (x < minX) x = Mathf.Clamp(card.center.x - size.x * 0.5f, minX, maxX);

            float minTop = bounds.yMin + SCREEN_MARGIN + size.y;
            float maxTop = Mathf.Max(minTop, bounds.yMax - SCREEN_MARGIN);
            float top = Mathf.Clamp(card.yMax, minTop, maxTop);

            var position = new Vector3(x, top, 0f);
            if (bodyRect.localPosition != position) bodyRect.localPosition = position;
        }

        /// <summary>
        /// 기본 폭에서 높이를 재고, 화면 높이를 넘으면 넓은 폭으로 한 번 더 잰다. 그래도 넘으면
        /// 글자 크기를 줄여(best fit) 화면 안에 담는다 — 스크롤 같은 새 조작을 늘리지 않고 내용을 다 보인다.
        /// </summary>
        private void ResizeToContent(Rect bounds)
        {
            float maxWidth = Mathf.Max(PADDING * 4f, bounds.width - SCREEN_MARGIN * 2f);
            float maxHeight = Mathf.Max(MIN_PANEL_HEIGHT, bounds.height - SCREEN_MARGIN * 2f);

            float width = Mathf.Min(PANEL_WIDTH, maxWidth);
            float height = MeasureHeight(width, out float headerHeight, out float textHeight);
            if (height > maxHeight && width < maxWidth)
            {
                width = Mathf.Min(PANEL_WIDE_WIDTH, maxWidth);
                height = MeasureHeight(width, out headerHeight, out textHeight);
            }

            bool isOverflowing = height > maxHeight;
            if (isOverflowing)
            {
                height = maxHeight;
                textHeight = Mathf.Max(BODY_MIN_FONT_SIZE, maxHeight - PADDING * 2f - headerHeight - HEADER_GAP);
            }
            bodyText.resizeTextForBestFit = isOverflowing;
            bodyText.verticalOverflow = isOverflowing ? VerticalWrapMode.Truncate : VerticalWrapMode.Overflow;

            float innerWidth = width - PADDING * 2f;
            var headerRect = headerText.rectTransform;
            headerRect.anchoredPosition = new Vector2(PADDING, -PADDING);
            headerRect.sizeDelta = new Vector2(innerWidth, headerHeight);

            var textRect = bodyText.rectTransform;
            textRect.anchoredPosition = new Vector2(PADDING, -(PADDING + headerHeight + HEADER_GAP));
            textRect.sizeDelta = new Vector2(innerWidth, textHeight);

            ((RectTransform)body.transform).sizeDelta = new Vector2(width, height);
        }

        private float MeasureHeight(float width, out float headerHeight, out float textHeight)
        {
            float innerWidth = width - PADDING * 2f;

            // preferredHeight는 현재 rect 폭으로 줄바꿈해 잰다 — 폭부터 맞춘다. best fit은 끈 상태로 잰다.
            bodyText.resizeTextForBestFit = false;
            headerText.rectTransform.sizeDelta = new Vector2(innerWidth, 0f);
            bodyText.rectTransform.sizeDelta = new Vector2(innerWidth, 0f);

            headerHeight = Mathf.Ceil(headerText.preferredHeight);
            textHeight = Mathf.Ceil(bodyText.preferredHeight);
            return PADDING * 2f + headerHeight + HEADER_GAP + textHeight;
        }

        /// <summary>최상위 캔버스가 덮는 화면 영역을 space 좌표로. 패널이 화면 밖으로 나가지 않게 하는 경계다.</summary>
        private Rect ScreenBoundsIn(RectTransform space)
        {
            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null) return space.rect;
            return RectIn((RectTransform)canvas.rootCanvas.transform, space);
        }

        private static Rect RectIn(RectTransform target, RectTransform space)
        {
            target.GetWorldCorners(cornerBuffer);
            Vector3 a = space.InverseTransformPoint(cornerBuffer[0]);
            Vector3 b = space.InverseTransformPoint(cornerBuffer[2]);
            return Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
        }
    }
}
