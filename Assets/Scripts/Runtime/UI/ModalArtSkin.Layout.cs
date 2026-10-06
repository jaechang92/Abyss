using UnityEngine;
using UnityEngine.UI;

namespace Abyss.Runtime.UI
{
    /// <summary>
    /// 드래프트 창 배치(기준 캔버스 1920×1080, 중심 원점). DraftPanelBuilder가 만든 기존 계층 이름을 따른다.
    ///
    /// MainPanel 1400×820, 중심 x=+110(빌더 값 유지 — 왼쪽 BuildContextPanel 자리). 화면 x 370..1770.
    /// BuildContextPanel 왼쪽 40 + 폭 260 → 화면 x 40..300, MainPanel과 70 떨어진다.
    /// 카드 3장 400×500 간격 32 → 폭 1264. 제목 40 · 카드 이름 40 · 본문 26 · 보조 24.
    /// 카드 안 이름·희귀도·설명·공식은 겹치지 않는 고정 영역 안에서 줄바꿈하고 넘는 줄은 자른다 —
    /// 전체 설명은 기존 SkillCardDetailsPanel(호버·포커스 상세)에서 본다.
    /// </summary>
    public static partial class ModalArtSkin
    {
        private const float DRAFT_PANEL_CORNER = 24f;
        private const float DRAFT_CARD_CORNER = 24f;
        private const float DRAFT_BUTTON_CORNER = 16f;

        private static readonly Vector2 DraftPanelPosition = new(110f, 0f);
        private static readonly Vector2 DraftPanelSize = new(1400f, 820f);

        private static readonly Vector2 DraftCardSize = new(400f, 500f);
        private const float DRAFT_CARD_GAP = 32f;
        private const float DRAFT_CARD_Y = 50f;          // 카드 위 가장자리 = 410-110 = 300 (제목 아래)

        private static readonly Vector2 DraftButtonSize = new(320f, 64f);
        private const float DRAFT_BUTTON_X = 180f;
        private const float DRAFT_BUTTON_Y = -300f;      // 카드 아래 가장자리(-200)와 68 떨어진다

        private const float CARD_CONTENT_WIDTH = 352f;   // 400 - 좌우 24

        private static readonly Vector2 BuildContextPosition = new(40f, 0f);
        private static readonly Vector2 BuildContextSize = new(260f, 820f);
        private const float BUILD_CONTEXT_CONTENT_WIDTH = 228f;
        private const float BUILD_CONTEXT_LIST_TOP = 176f;

        /// <summary>
        /// 드래프트 창 스킨. root 비활성 상태(Awake)에서 1회 적용한다. 카드 바인딩·버튼 콜백·문구는 Presenter 그대로다.
        /// </summary>
        public static void ApplyDraft(Transform root, Text title, SkillCardView[] cards,
            Button reroll, Text rerollLabel, Button skip, Text skipLabel, BuildContextPanel buildContext)
        {
            if (root == null) return;
            float ppu = ResolveReferencePpu(root);

            var mainPanel = FindRequired(root, "MainPanel");
            if (mainPanel != null)
            {
                SetRect(mainPanel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), DraftPanelPosition, DraftPanelSize);
                ApplyPanel(mainPanel, DRAFT_PANEL_CORNER, ppu);
            }

            if (title != null)
            {
                SetTopRect(title.transform, 28f, new Vector2(1240f, 56f));
                StyleText(title, 40, BodyTextColor);
                title.alignment = TextAnchor.MiddleCenter;
            }

            if (cards != null)
            {
                float step = DraftCardSize.x + DRAFT_CARD_GAP;
                float start = -step * (cards.Length - 1) * 0.5f;
                for (int i = 0; i < cards.Length; i++)
                {
                    if (cards[i] == null) continue;
                    ApplyDraftCard(cards[i].transform, new Vector2(start + i * step, DRAFT_CARD_Y), ppu);
                }
            }

            ApplyDraftButton(reroll, rerollLabel, new Vector2(-DRAFT_BUTTON_X, DRAFT_BUTTON_Y), ppu);
            ApplyDraftButton(skip, skipLabel, new Vector2(DRAFT_BUTTON_X, DRAFT_BUTTON_Y), ppu);

            if (buildContext != null) ApplyBuildContext(buildContext.transform, ppu);
        }

        private static void ApplyDraftCard(Transform card, Vector2 position, float ppu)
        {
            SetRect(card, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), position, DraftCardSize);

            // 아이콘은 자리·크기만. 스프라이트·enabled(아이콘 없으면 숨김)는 SkillCardView.Bind가 정한다.
            var icon = FindRequired(card, "Icon");
            if (icon != null) SetTopRect(icon, 32f, new Vector2(112f, 112f));

            // 세로 영역: 아이콘 32..144 / 이름 152..248(2줄) / 희귀도 252..312(2줄) / 설명 320..412(3줄) / 공식 아래 16..76(2줄)
            var name = FindText(card, "Name");
            if (name != null)
            {
                SetTopRect(name.transform, 152f, new Vector2(CARD_CONTENT_WIDTH, 96f));
                StyleText(name, 40, BodyTextColor);
                name.alignment = TextAnchor.MiddleCenter;
            }

            var headline = FindText(card, "RarityCategory");
            if (headline != null)
            {
                SetTopRect(headline.transform, 252f, new Vector2(CARD_CONTENT_WIDTH, 60f));
                StyleText(headline, 24); // 색은 빌더 값 유지
                headline.alignment = TextAnchor.MiddleCenter;
            }

            var description = FindText(card, "Description");
            if (description != null)
            {
                SetTopRect(description.transform, 320f, new Vector2(CARD_CONTENT_WIDTH, 92f));
                StyleText(description, 26, BodyTextColor);
                description.alignment = TextAnchor.UpperCenter;
            }

            var formula = FindText(card, "Formula");
            if (formula != null)
            {
                SetRect(formula.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 16f),
                    new Vector2(CARD_CONTENT_WIDTH, 60f));
                StyleText(formula, 24, SubTextColor);
                formula.alignment = TextAnchor.LowerCenter;
            }

            // 포커스는 카드 루트의 선택 버튼에 머문다(SkillCardView.FocusTarget). 배경색(희귀도)은 SkillCardView 소유.
            if (card.TryGetComponent(out Button button)) EnsureSelectableSkin(button, DRAFT_CARD_CORNER, ppu);
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
