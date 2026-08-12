using UnityEngine;
using UnityEngine.UI;
using static Abyss.Runtime.UI.UiFactory;

namespace Abyss.Runtime.UI
{
    /// <summary>
    /// <see cref="CodexPanel"/>의 uGUI 조립 파트. 좌표·크기 상수는 전부 여기에만 둔다.
    /// 상태 갱신은 CodexPanel.cs, 항목 수집은 CodexPanel.Items.cs가 담당한다.
    ///
    /// 프로젝트의 다른 동적 패널과 같이 <see cref="UiFactory"/> 수동 좌표로 짠다(기준 해상도 1920×1080).
    /// </summary>
    public sealed partial class CodexPanel
    {
        // 패널 본체
        private const float PANEL_W = 1480f;
        private const float PANEL_H = 860f;

        // 내부 여백 기준선(패널 중심 기준 좌표)
        private const float CONTENT_LEFT = -700f;
        private const float CONTENT_RIGHT = 700f;

        // 타일 그리드
        private const float TILE_SIZE = 150f;
        private const float TILE_STEP = 164f;      // 타일 + 간격 14
        private const float GRID_TOP_Y = 165f;     // 첫 행 타일 중심 y

        /// <summary>타일 하나의 표시 요소 묶음. 페이지를 넘길 때 오브젝트를 다시 만들지 않고 내용만 바꾼다.</summary>
        private struct TileView
        {
            public GameObject Root;
            public Image Selection;
            public Image Background;
            public Image Icon;
            public Text Glyph;
            public Text NameLabel;
        }

        private void BuildUI(Transform root)
        {
            body = CreateDimBody(root, 0.88f);

            var panel = CreateRect(body.transform, "Panel",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(PANEL_W, PANEL_H));
            var panelImg = panel.AddComponent<Image>();
            panelImg.color = new Color(0.08f, 0.08f, 0.12f, 0.99f);

            BuildHeader(panel.transform);
            BuildTabs(panel.transform);
            BuildGrid(panel.transform);
            BuildPager(panel.transform);
            BuildDetail(panel.transform);
            BuildRecords(panel.transform);
        }

        // ───────────────────────── 헤더 ─────────────────────────

        private void BuildHeader(Transform panel)
        {
            CreateLabel(panel, "TitleText", new Vector2(CONTENT_LEFT + 150f, 372f), new Vector2(300, 48),
                "도감", 32, AccentColor, TextAnchor.MiddleLeft);

            CreateLabel(panel, "HintText", new Vector2(400f, 372f), new Vector2(300, 32),
                "ESC로 닫기", 15, MutedColor, TextAnchor.MiddleRight);

            var close = CreateButton(panel, "CloseButton", new Vector2(CONTENT_RIGHT - 70f, 372f), new Vector2(140, 44), "닫기", 18);
            close.onClick.AddListener(Close);

            // 구분선. 헤더·탭과 본문을 시각적으로 끊는다.
            var divider = CreateRect(panel, "Divider",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 272f), new Vector2(1400f, 2f));
            var dividerImg = divider.AddComponent<Image>();
            dividerImg.color = new Color(0.28f, 0.28f, 0.36f);
            dividerImg.raycastTarget = false;
        }

        private void BuildTabs(Transform panel)
        {
            string[] labels = { "폼", "스킬", "적", "보스", "기록" };

            for (int i = 0; i < labels.Length; i++)
            {
                var tab = (CodexTab)i;
                float x = CONTENT_LEFT + 84f + i * 176f;

                var button = CreateTabButton(panel, $"Tab_{tab}", new Vector2(x, 306f), new Vector2(168, 46), labels[i]);
                button.onClick.AddListener(() => SelectTab(tab));
                tabButtons[i] = button;
            }

            countLabel = CreateLabel(panel, "CountText", new Vector2(520f, 306f), new Vector2(360, 32),
                string.Empty, 17, MutedColor, TextAnchor.MiddleRight);
        }

        /// <summary>
        /// 탭 버튼. <see cref="UiFactory.CreateButton"/>을 쓰지 않는 이유는 배경 Image의 색을
        /// <b>흰색으로 두어야</b> 하기 때문이다 — uGUI Button은 상태 색을 CanvasRenderer에 곱하므로,
        /// Image에도 색을 넣으면 두 색이 곱해져 선택/비선택 대비가 사라진다.
        /// 실제 색은 <see cref="ApplyTabColors"/>가 ColorBlock으로 넣는다.
        /// </summary>
        private static Button CreateTabButton(Transform parent, string name, Vector2 pos, Vector2 size, string label)
        {
            var go = CreateRect(parent, name,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, size);

            var img = go.AddComponent<Image>();
            img.color = Color.white;

            var button = go.AddComponent<Button>();
            button.targetGraphic = img;

            var textGo = CreateRect(go.transform, "Text", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            Stretch((RectTransform)textGo.transform);
            var text = textGo.AddComponent<Text>();
            ApplyFont(text);
            text.text = label;
            text.fontSize = 19;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.raycastTarget = false;

            return button;
        }

        // ───────────────────────── 그리드 ─────────────────────────

        private void BuildGrid(Transform panel)
        {
            gridRoot = CreateRect(panel, "Grid",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(PANEL_W, PANEL_H));

            for (int row = 0; row < GRID_ROWS; row++)
            {
                for (int col = 0; col < GRID_COLS; col++)
                {
                    int slot = row * GRID_COLS + col;
                    float x = CONTENT_LEFT + TILE_SIZE * 0.5f + col * TILE_STEP;
                    float y = GRID_TOP_Y - row * TILE_STEP;
                    tiles[slot] = CreateTile(gridRoot.transform, slot, new Vector2(x, y));
                }
            }
        }

        private TileView CreateTile(Transform parent, int slot, Vector2 pos)
        {
            var root = CreateRect(parent, $"Tile_{slot}",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, new Vector2(TILE_SIZE, TILE_SIZE));

            // 선택 표시는 배경보다 조금 큰 사각형을 뒤에 깔아 테두리처럼 보이게 한다
            // (uGUI 기본 Image에는 외곽선이 없어 Outline 컴포넌트 대신 두 겹으로 만든다).
            var selectionGo = CreateRect(root.transform, "Selection",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(TILE_SIZE + 8f, TILE_SIZE + 8f));
            var selection = selectionGo.AddComponent<Image>();
            selection.color = Color.clear;
            selection.raycastTarget = false;

            var bgGo = CreateRect(root.transform, "Background",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(TILE_SIZE, TILE_SIZE));
            var background = bgGo.AddComponent<Image>();
            background.color = LockedTileColor;

            var iconGo = CreateRect(root.transform, "Icon",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 20f), new Vector2(84, 84));
            var icon = iconGo.AddComponent<Image>();
            icon.preserveAspect = true;   // 적 스프라이트는 정사각형이 아니다
            icon.raycastTarget = false;

            var glyph = CreateLabel(root.transform, "Glyph", new Vector2(0f, 20f), new Vector2(84, 84),
                UNKNOWN_GLYPH, 44, Color.white, TextAnchor.MiddleCenter);

            var nameLabel = CreateLabel(root.transform, "Name", new Vector2(0f, -50f), new Vector2(138, 38),
                string.Empty, 13, Color.white, TextAnchor.UpperCenter);
            nameLabel.horizontalOverflow = HorizontalWrapMode.Wrap;   // 이름이 길면 두 줄로

            // 버튼의 대상은 배경 Image다 — 항목별 색은 Image.color가 들고, 호버/클릭 밝기는
            // ColorBlock이 그 위에 곱해진다(흰색 계열을 써야 항목 색이 살아남는다).
            var button = bgGo.AddComponent<Button>();
            button.targetGraphic = background;
            var colors = button.colors;
            colors.normalColor = new Color(0.85f, 0.85f, 0.85f);
            colors.highlightedColor = Color.white;
            colors.pressedColor = new Color(0.7f, 0.7f, 0.7f);
            colors.selectedColor = new Color(0.85f, 0.85f, 0.85f);
            button.colors = colors;

            int captured = slot;
            button.onClick.AddListener(() => SelectItem(currentPage * PAGE_SIZE + captured));

            return new TileView
            {
                Root = root,
                Selection = selection,
                Background = background,
                Icon = icon,
                Glyph = glyph,
                NameLabel = nameLabel
            };
        }

        private void BuildPager(Transform panel)
        {
            pagerRoot = CreateRect(panel, "Pager",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(PANEL_W, PANEL_H));

            // 그리드 가로 중앙 아래에 둔다.
            float gridCenterX = CONTENT_LEFT + TILE_SIZE * 0.5f + (GRID_COLS - 1) * TILE_STEP * 0.5f;

            prevButton = CreateButton(pagerRoot.transform, "PrevButton", new Vector2(gridCenterX - 90f, -290f), new Vector2(56, 44), "◀", 18);
            prevButton.onClick.AddListener(() => ChangePage(-1));

            pageLabel = CreateLabel(pagerRoot.transform, "PageText", new Vector2(gridCenterX, -290f), new Vector2(120, 44),
                "1 / 1", 18, MutedColor, TextAnchor.MiddleCenter);

            nextButton = CreateButton(pagerRoot.transform, "NextButton", new Vector2(gridCenterX + 90f, -290f), new Vector2(56, 44), "▶", 18);
            nextButton.onClick.AddListener(() => ChangePage(1));
        }

        // ───────────────────────── 상세 / 기록 ─────────────────────────

        private void BuildDetail(Transform panel)
        {
            detailRoot = CreateRect(panel, "Detail",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(415f, -25f), new Vector2(570f, 530f));
            var bg = detailRoot.AddComponent<Image>();
            bg.color = new Color(0.12f, 0.12f, 0.17f, 1f);

            var frameGo = CreateRect(detailRoot.transform, "IconFrame",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 165f), new Vector2(136, 136));
            var frame = frameGo.AddComponent<Image>();
            frame.color = new Color(0.17f, 0.17f, 0.23f);
            frame.raycastTarget = false;

            var iconGo = CreateRect(detailRoot.transform, "Icon",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 165f), new Vector2(120, 120));
            detailIcon = iconGo.AddComponent<Image>();
            detailIcon.preserveAspect = true;
            detailIcon.raycastTarget = false;

            detailGlyph = CreateLabel(detailRoot.transform, "Glyph", new Vector2(0f, 165f), new Vector2(120, 120),
                UNKNOWN_GLYPH, 64, Color.white, TextAnchor.MiddleCenter);

            detailName = CreateLabel(detailRoot.transform, "NameText", new Vector2(0f, 62f), new Vector2(520, 44),
                EMPTY_VALUE, 26, Color.white, TextAnchor.MiddleCenter);

            detailBadge = CreateLabel(detailRoot.transform, "BadgeText", new Vector2(0f, 22f), new Vector2(520, 30),
                string.Empty, 16, NeutralBadgeColor, TextAnchor.MiddleCenter);

            detailDescription = CreateWrappedLabel(detailRoot.transform, "DescriptionText", new Vector2(0f, -62f), new Vector2(510, 132),
                string.Empty, 17, Color.white, TextAnchor.UpperCenter);

            detailStats = CreateWrappedLabel(detailRoot.transform, "StatsText", new Vector2(0f, -192f), new Vector2(510, 128),
                string.Empty, 15, MutedColor, TextAnchor.UpperLeft);
        }

        private void BuildRecords(Transform panel)
        {
            recordsText = CreateWrappedLabel(panel, "RecordsText", new Vector2(20f, -25f), new Vector2(1360, 530),
                string.Empty, 19, new Color(0.86f, 0.88f, 0.94f), TextAnchor.UpperLeft);
            recordsText.gameObject.SetActive(false);
        }

        /// <summary>
        /// 줄바꿈되는 라벨. <see cref="UiFactory.CreateLabel"/>은 버튼 라벨 기준이라 Overflow로 두는데,
        /// 설명·수치·기록은 폭을 넘기면 잘려서는 안 되므로 Wrap으로 되돌린다.
        /// 세로도 Overflow로 둔다 — 잘린 마지막 줄보다 칸을 넘치는 편이 낫다.
        /// </summary>
        private static Text CreateWrappedLabel(Transform parent, string name, Vector2 pos, Vector2 size,
                                               string content, int fontSize, Color color, TextAnchor anchor)
        {
            var text = CreateLabel(parent, name, pos, size, content, fontSize, color, anchor);
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }
    }
}
