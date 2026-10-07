using UnityEngine;
using UnityEngine.UI;

namespace Abyss.Runtime.UI
{
    /// <summary>
    /// 드래프트 창 배치(기준 캔버스 1920×1080, 중심 원점). DraftPanelBuilder가 만든 기존 계층 이름을 따른다.
    /// 시안 hud-draft-concept-v1(「FRAGMENT DRAFT」)의 위→아래 순서: 장식 제목 → 카드 3장 → 공통 상세 석판 → 리롤/스킵.
    ///
    /// MainPanel 1460×960, 중심 x=+110(빌더 값 유지 — 왼쪽 BuildContextPanel 자리). 화면 x 340..1800, y 60..1020.
    /// BuildContextPanel 왼쪽 40 + 폭 260 → 화면 x 40..300, MainPanel과 40 떨어진다.
    /// MainPanel 안(위 가장자리 기준): 제목 24..80 / 카드 100..540 / 상세 석판 568..844 / 버튼 864..924 / 아래 여백 36.
    /// 카드 3장 432×440 간격 32 → 폭 1360. 상세 석판도 같은 폭이라 카드 열과 좌우가 맞는다.
    ///
    /// 카드에는 아이콘·이름·희귀도 줄·설명만 둔다(시안). 공식·수치·폼·시너지는 아래 공통 상세에서 본다 —
    /// 상세는 기존 SkillCardDetailsPanel(호버·포커스 카드 하나)을 석판 자리에 붙인 것이다.
    /// </summary>
    public static partial class ModalArtSkin
    {
        private const float DRAFT_PANEL_CORNER = 24f;
        private const float DRAFT_CARD_CORNER = 24f;
        private const float DRAFT_BUTTON_CORNER = 16f;
        private const float DRAFT_DETAIL_CORNER = 16f;

        private static readonly Vector2 DraftPanelPosition = new(110f, 0f);
        private static readonly Vector2 DraftPanelSize = new(1460f, 960f);
        private const float DRAFT_PANEL_HALF_HEIGHT = 480f;

        private const float DRAFT_TITLE_TOP = 24f;
        private static readonly Vector2 DraftTitleSize = new(880f, 56f);

        // 제목 양옆 장식선: 제목 칸 바깥(±460)에서 ±660까지, 안쪽 끝에 ◆.
        private const float TITLE_RULE_INNER = 460f;
        private const float TITLE_RULE_OUTER = 660f;
        private const float TITLE_RULE_THICKNESS = 2f;
        private const float TITLE_DIAMOND_SIZE = 10f;

        private static readonly Vector2 DraftCardSize = new(432f, 440f);
        private const float DRAFT_CARD_GAP = 32f;
        private const float DRAFT_CARD_TOP = 100f;

        private const float DRAFT_DETAIL_TOP = 568f;
        private static readonly Vector2 DraftDetailSize = new(1360f, 276f);
        private const string DETAIL_SLOT_NAME = "ArtDetailSlot";

        private static readonly Vector2 DraftButtonSize = new(320f, 60f);
        private const float DRAFT_BUTTON_X = 180f;
        private const float DRAFT_BUTTON_TOP = 864f;

        private const float CARD_CONTENT_WIDTH = 384f;   // 432 - 좌우 24
        private const float CARD_ICON_SIZE = 112f;
        private const float CARD_ICON_FRAME = 8f;        // 아이콘 칸이 아이콘보다 사방 4씩 크다
        private const int DRAFT_CARD_NAME_FONT = 32;
        private const int DRAFT_HEADLINE_FONT = 24;

        private static readonly Vector2 BuildContextPosition = new(40f, 0f);
        private static readonly Vector2 BuildContextSize = new(260f, 900f);
        private const float BUILD_CONTEXT_CONTENT_WIDTH = 228f;
        private const float BUILD_CONTEXT_LIST_TOP = 176f;

        /// <summary>
        /// 드래프트 창 스킨. root 비활성 상태(Awake)에서 1회 적용한다. 카드 바인딩·버튼 콜백·문구는 Presenter 그대로다.
        /// details가 있으면 카드 아래 상세 석판 자리에 붙인다(없으면 기존처럼 카드 옆에 뜬다).
        /// </summary>
        public static void ApplyDraft(Transform root, Text title, SkillCardView[] cards,
            Button reroll, Text rerollLabel, Button skip, Text skipLabel, BuildContextPanel buildContext,
            SkillCardDetailsPanel details)
        {
            if (root == null) return;
            float ppu = ResolveReferencePpu(root);

            var mainPanel = FindRequired(root, "MainPanel");
            RectTransform slot = null;
            if (mainPanel != null)
            {
                SetRect(mainPanel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), DraftPanelPosition, DraftPanelSize);
                ApplyPanel(mainPanel, DRAFT_PANEL_CORNER, ppu);

                slot = EnsureDetailSlot(mainPanel, ppu);
                if (details != null && slot != null) details.DockTo(slot);
            }

            if (title != null)
            {
                SetTopRect(title.transform, DRAFT_TITLE_TOP, DraftTitleSize);
                StyleText(title, 40, BodyTextColor);
                title.alignment = TextAnchor.MiddleCenter;
                if (mainPanel != null) EnsureTitleRules(mainPanel);
            }

            if (cards != null)
            {
                float step = DraftCardSize.x + DRAFT_CARD_GAP;
                float start = -step * (cards.Length - 1) * 0.5f;
                float y = TopToCenterY(DRAFT_CARD_TOP, DraftCardSize.y);
                for (int i = 0; i < cards.Length; i++)
                {
                    if (cards[i] == null) continue;
                    ApplyDraftCard(cards[i].transform, new Vector2(start + i * step, y), ppu);
                }
            }

            float buttonY = TopToCenterY(DRAFT_BUTTON_TOP, DraftButtonSize.y);
            ApplyDraftButton(reroll, rerollLabel, new Vector2(-DRAFT_BUTTON_X, buttonY), ppu);
            ApplyDraftButton(skip, skipLabel, new Vector2(DRAFT_BUTTON_X, buttonY), ppu);

            if (buildContext != null) ApplyBuildContext(buildContext.transform, ppu);

            // 시안의 얇은 금속 테두리 — 위에서 만든 석판 프레임을 끄고 드래프트 대상에만 덧씌운다(공유 석판 규칙은 그대로).
            DraftArtSkin.Apply(mainPanel, slot, cards, reroll, skip,
                buildContext != null ? buildContext.transform : null, ppu);
        }

        /// <summary>MainPanel 위 가장자리에서 잰 top·높이 → 중심 원점 y.</summary>
        private static float TopToCenterY(float top, float height) => DRAFT_PANEL_HALF_HEIGHT - top - height * 0.5f;

        private static void ApplyDraftCard(Transform card, Vector2 position, float ppu)
        {
            SetRect(card, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), position, DraftCardSize);

            // 세로 영역: 희귀도 띠 2..6(DraftArtSkin) / 아이콘 28..140 / 이름 152..240(2줄) / 희귀도 244..308(2줄) / 설명 320..416(3줄)
            // 실제 문구는 시안보다 길다 — 각 칸은 Truncate라 넘친 줄은 잘리고 전체 문구는 아래 공통 상세에서 본다.
            // 아이콘은 자리·크기만. 스프라이트·enabled(아이콘 없으면 숨김)는 SkillCardView.Bind가 정한다.
            var icon = FindRequired(card, "Icon");
            if (icon != null)
            {
                SetTopRect(icon, 28f, new Vector2(CARD_ICON_SIZE, CARD_ICON_SIZE));
                EnsureIconFrame(card, icon);
            }

            var name = FindText(card, "Name");
            if (name != null)
            {
                SetTopRect(name.transform, 152f, new Vector2(CARD_CONTENT_WIDTH, 88f));
                StyleText(name, DRAFT_CARD_NAME_FONT, BodyTextColor); // 2줄 이름이 88 안에 들도록 32
                name.alignment = TextAnchor.MiddleCenter;
            }

            var headline = FindText(card, "RarityCategory");
            if (headline != null)
            {
                // 자동 글자 축소 없이 24 고정 — 별·분류·[축]이 384를 넘으면 둘째 줄로 내려가도록 높이 64(2줄).
                SetTopRect(headline.transform, 244f, new Vector2(CARD_CONTENT_WIDTH, 64f));
                StyleText(headline, DRAFT_HEADLINE_FONT); // 색은 빌더 값 유지. BestFit 끔·Wrap·Truncate
                headline.alignment = TextAnchor.MiddleCenter;
            }

            var description = FindText(card, "Description");
            if (description != null)
            {
                SetTopRect(description.transform, 320f, new Vector2(CARD_CONTENT_WIDTH, 96f));
                StyleText(description, 26, BodyTextColor);
                description.alignment = TextAnchor.UpperCenter;
            }

            // 공식은 카드에서 내리고 공통 상세에서 본다. 문구는 SkillCardView가 계속 채운다(꺼진 Text라 안 보일 뿐).
            var formula = FindRequired(card, "Formula");
            if (formula != null) formula.gameObject.SetActive(false);

            // 포커스는 카드 루트의 선택 버튼에 머문다(SkillCardView.FocusTarget). 배경색(희귀도)은 SkillCardView 소유.
            if (card.TryGetComponent(out Button button)) EnsureSelectableSkin(button, DRAFT_CARD_CORNER, ppu, hasArrows: true);
        }

        /// <summary>아이콘 뒤 어두운 사각 칸(시안의 아이콘 액자). 아이콘 바로 앞 형제라 아이콘 아래에 그려진다.</summary>
        private static void EnsureIconFrame(Transform card, Transform icon)
        {
            var frame = GetOrCreateImage(card, "ArtIconFrame");
            frame.sprite = null;
            frame.color = InsetColor;
            float size = CARD_ICON_SIZE + CARD_ICON_FRAME;
            SetTopRect(frame.transform, 28f - CARD_ICON_FRAME * 0.5f, new Vector2(size, size));
            // 처음 만들 때만 아이콘 앞으로 옮긴다 — 이미 앞에 있으면 그대로(다시 옮기면 아이콘 뒤로 밀린다).
            if (frame.transform.GetSiblingIndex() > icon.GetSiblingIndex())
            {
                frame.transform.SetSiblingIndex(icon.GetSiblingIndex());
            }
        }

        /// <summary>카드 아래 공통 상세 석판. 면과 프레임만 그리고 내용은 SkillCardDetailsPanel이 위에 얹는다.</summary>
        private static RectTransform EnsureDetailSlot(Transform mainPanel, float ppu)
        {
            var face = GetOrCreateImage(mainPanel, DETAIL_SLOT_NAME);
            face.sprite = null;
            face.color = InsetColor;
            SetTopRect(face.transform, DRAFT_DETAIL_TOP, DraftDetailSize);
            EnsureFrame(face.transform, DRAFT_DETAIL_CORNER, ppu);
            return (RectTransform)face.transform;
        }

        /// <summary>제목 양옆 금속색 장식선 + 안쪽 끝 ◆. 제목 문구 폭과 무관한 고정 자리다.</summary>
        private static void EnsureTitleRules(Transform mainPanel)
        {
            float y = -(DRAFT_TITLE_TOP + DraftTitleSize.y * 0.5f);
            float length = TITLE_RULE_OUTER - TITLE_RULE_INNER;
            float center = (TITLE_RULE_OUTER + TITLE_RULE_INNER) * 0.5f;
            var top = new Vector2(0.5f, 1f);
            var middle = new Vector2(0.5f, 0.5f);

            for (int side = -1; side <= 1; side += 2)
            {
                string suffix = side < 0 ? "Left" : "Right";

                var rule = GetOrCreateImage(mainPanel, "ArtTitleRule" + suffix);
                rule.sprite = null;
                rule.color = MetalColor;
                SetRect(rule.transform, top, middle, new Vector2(side * center, y), new Vector2(length, TITLE_RULE_THICKNESS));

                var diamond = GetOrCreateImage(mainPanel, "ArtTitleDiamond" + suffix);
                diamond.sprite = null;
                diamond.color = MetalColor;
                SetRect(diamond.transform, top, middle, new Vector2(side * TITLE_RULE_INNER, y),
                    new Vector2(TITLE_DIAMOND_SIZE, TITLE_DIAMOND_SIZE));
                diamond.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            }
        }

        private static void ApplyDraftButton(Button button, Text label, Vector2 position, float ppu)
        {
            if (button == null) return;
            SetRect(button.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), position, DraftButtonSize);
            if (label != null)
            {
                StyleText(label, 26, BodyTextColor);
                label.alignment = TextAnchor.MiddleCenter;
            }
            EnsureSelectableSkin(button, DRAFT_BUTTON_CORNER, ppu);
        }

        /// <summary>
        /// 현재 빌드 패널. 글자 24로 키우되 내용(Refresh 문구)은 그대로다. 보유 목록이 영역을 넘으면 기존처럼 아랫줄이 잘린다.
        /// </summary>
        private static void ApplyBuildContext(Transform panel, float ppu)
        {
            SetRect(panel, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), BuildContextPosition, BuildContextSize);
            ApplyPanel(panel, DRAFT_PANEL_CORNER, ppu);

            var title = FindText(panel, "Title");
            if (title != null)
            {
                SetTopRect(title.transform, 20f, new Vector2(BUILD_CONTEXT_CONTENT_WIDTH, 40f));
                StyleText(title, 24, BodyTextColor);
                title.alignment = TextAnchor.MiddleCenter;
            }

            var synergy = FindText(panel, "SynergyCounts");
            if (synergy != null)
            {
                SetTopRect(synergy.transform, 64f, new Vector2(BUILD_CONTEXT_CONTENT_WIDTH, 100f));
                StyleText(synergy, 24); // 색은 빌더 값 유지
                synergy.alignment = TextAnchor.UpperCenter;
            }

            var list = FindText(panel, "SkillList");
            if (list != null && list.transform is RectTransform listRect)
            {
                listRect.anchorMin = Vector2.zero;
                listRect.anchorMax = Vector2.one;
                listRect.offsetMin = new Vector2(16f, 16f);
                listRect.offsetMax = new Vector2(-16f, -BUILD_CONTEXT_LIST_TOP);
                StyleText(list, 24, BodyTextColor);
                list.alignment = TextAnchor.UpperLeft;
            }
        }

        private static Text FindText(Transform parent, string path)
        {
            var child = FindRequired(parent, path);
            if (child == null) return null;
            if (!child.TryGetComponent(out Text text))
            {
                Debug.LogWarning($"[ModalArtSkin] '{parent.name}/{path}' 에 Text가 없어 글자 스킨을 건너뜁니다.");
            }
            return text;
        }
    }
}
