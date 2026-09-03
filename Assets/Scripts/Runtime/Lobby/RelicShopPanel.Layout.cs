using Abyss.Runtime.Localization;
using Abyss.Runtime.Meta;
using UnityEngine;
using UnityEngine.UI;

namespace Abyss.Runtime.Lobby
{
    /// <summary>
    /// <see cref="RelicShopPanel"/>의 <b>화면 조립</b> 부분. 로직(감정·장착·갱신)은 본체에 있다.
    /// (같은 partial 클래스라 필드를 공유한다. 500줄 규약으로 분리.)
    ///
    /// 🔴 <b>박스 높이를 내용에서 파생시킨다.</b> 고정 높이로 두면 유물이 늘어날 때
    /// 그리드가 버튼 위로 넘쳐 <b>에러도 로그도 없이 잘린다</b> — 상점 5번째 품목이
    /// [떠난다] 버튼과 겹쳤던 것과 같은 함정이다. 여기서는 카탈로그 수로 행 수를 먼저 구하고
    /// 그 아래에 메시지·버튼을 놓으므로, 유물을 몇 종으로 늘려도 겹칠 방법이 없다.
    /// </summary>
    public sealed partial class RelicShopPanel
    {
        // ── 색 ──
        private static readonly Color BoxBg = new(0.10f, 0.09f, 0.13f, 0.98f);
        private static readonly Color HeaderText = new(1f, 0.9f, 0.7f);
        private static readonly Color MutedText = new(0.60f, 0.60f, 0.68f);
        private static readonly Color SlotEmpty = new(0.16f, 0.16f, 0.20f);
        private static readonly Color TileDim = new(0.13f, 0.13f, 0.17f);
        private static readonly Color DrawBase = new(0.45f, 0.30f, 0.60f);
        private static readonly Color DrawDisabled = new(0.25f, 0.24f, 0.30f);
        private static readonly Color CloseBase = new(0.32f, 0.24f, 0.26f);

        // ── 치수 ──
        private const float BOX_W = 900f;
        private const int GRID_COLS = 5;
        private const float TILE_W = 158f;
        private const float TILE_H = 96f;
        private const float TILE_STEP_X = 166f;
        private const float TILE_STEP_Y = 104f;
        private const float SLOT_W = 200f;
        private const float SLOT_H = 110f;
        private const float SLOT_STEP_X = 216f;

        // 위에서부터의 고정 오프셋(음수 = 아래). 그리드 아래는 행 수에 따라 밀린다.
        private const float TITLE_Y = -40f;
        private const float EQUIP_HEADER_Y = -96f;
        private const float SLOT_ROW_Y = -160f;
        private const float OWNED_HEADER_Y = -248f;
        private const float GRID_FIRST_ROW_Y = -308f;

        /// <summary>최초 Open 시 1회 조립. 카탈로그가 비어 있어도 박스는 만든다(안내 문구가 보여야 한다).</summary>
        private void EnsureBuilt()
        {
            if (built) return;
            built = true;

            var parent = boxParent != null ? boxParent : root.GetComponent<RectTransform>();
            if (parent == null) return;

            var catalog = RelicCatalog.All;
            int count = catalog != null ? catalog.Length : 0;
            int rows = Mathf.Max(1, Mathf.CeilToInt(count / (float)GRID_COLS));

            // 그리드 마지막 행의 아래끝 → 그 아래로 메시지·버튼을 쌓고, 박스 높이를 거기서 구한다.
            float gridBottom = GRID_FIRST_ROW_Y - (rows - 1) * TILE_STEP_Y - TILE_H * 0.5f;
            float messageY = gridBottom - 38f;
            float buttonY = messageY - 60f;
            float boxHeight = -buttonY + 28f + 40f;   // 버튼 아래끝 + 여백

            var box = CreateRect(parent, "Box", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(BOX_W, boxHeight));
            box.gameObject.AddComponent<Image>().color = BoxBg;

            BuildHeader(box);
            BuildSlots(box);
            BuildGrid(box, catalog, rows);
            BuildFooter(box, messageY, buttonY);
        }

        private void BuildHeader(RectTransform box)
        {
            CreateTop(box, "Title", Loc.Get(StringKey.Relic_Title), 28,
                new Vector2(-BOX_W * 0.5f + 240f, TITLE_Y), new Vector2(440f, 40f),
                TextAnchor.MiddleLeft, HeaderText);

            shardsLabel = CreateTop(box, "Shards", string.Empty, 20,
                new Vector2(BOX_W * 0.5f - 190f, TITLE_Y), new Vector2(340f, 40f),
                TextAnchor.MiddleRight, new Color(0.85f, 0.9f, 1f));

            CreateTop(box, "EquipHeader", Loc.Get(StringKey.Relic_Equipped), 18,
                new Vector2(-BOX_W * 0.5f + 200f, EQUIP_HEADER_Y), new Vector2(360f, 28f),
                TextAnchor.MiddleLeft, MutedText);
        }

        private void BuildSlots(RectTransform box)
        {
            float startX = -(MetaSave.EquippedRelicSlots - 1) * SLOT_STEP_X * 0.5f;

            for (int i = 0; i < MetaSave.EquippedRelicSlots; i++)
            {
                var rect = CreateRect(box, $"Slot_{i}", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                    new Vector2(0.5f, 0.5f), new Vector2(startX + i * SLOT_STEP_X, SLOT_ROW_Y),
                    new Vector2(SLOT_W, SLOT_H));

                var image = rect.gameObject.AddComponent<Image>();
                image.color = SlotEmpty;
                var button = rect.gameObject.AddComponent<Button>();
                button.targetGraphic = image;

                var name = CreateCentered(rect, "Name", string.Empty, 17, new Vector2(0f, 14f),
                    new Vector2(SLOT_W - 16f, 30f), TextAnchor.MiddleCenter, Color.white);
                var level = CreateCentered(rect, "Level", string.Empty, 14, new Vector2(0f, -18f),
                    new Vector2(SLOT_W - 16f, 24f), TextAnchor.MiddleCenter, new Color(0.85f, 0.9f, 1f));

                var view = new SlotView { Index = i, Background = image, NameLabel = name, LevelLabel = level, Button = button };
                button.onClick.AddListener(() => OnSlotClicked(view));
                slots.Add(view);
            }
        }

        private void BuildGrid(RectTransform box, RelicData[] catalog, int rows)
        {
            ownedHeaderLabel = CreateTop(box, "OwnedHeader", Loc.Get(StringKey.Relic_NoneOwned), 18,
                new Vector2(-BOX_W * 0.5f + 200f, OWNED_HEADER_Y), new Vector2(360f, 28f),
                TextAnchor.MiddleLeft, MutedText);

            if (catalog == null) return;

            float startX = -(GRID_COLS - 1) * TILE_STEP_X * 0.5f;
            for (int i = 0; i < catalog.Length; i++)
            {
                var data = catalog[i];
                if (data == null) continue;

                int col = i % GRID_COLS;
                int row = i / GRID_COLS;
                var rect = CreateRect(box, $"Tile_{data.relicId}", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                    new Vector2(0.5f, 0.5f),
                    new Vector2(startX + col * TILE_STEP_X, GRID_FIRST_ROW_Y - row * TILE_STEP_Y),
                    new Vector2(TILE_W, TILE_H));

                var image = rect.gameObject.AddComponent<Image>();
                image.color = TileDim;
                var button = rect.gameObject.AddComponent<Button>();
                button.targetGraphic = image;

                var name = CreateCentered(rect, "Name", string.Empty, 15, new Vector2(0f, 12f),
                    new Vector2(TILE_W - 12f, 30f), TextAnchor.MiddleCenter, Color.white);
                var level = CreateCentered(rect, "Level", string.Empty, 13, new Vector2(0f, -16f),
                    new Vector2(TILE_W - 12f, 22f), TextAnchor.MiddleCenter, new Color(0.85f, 0.9f, 1f));

                var view = new TileView { Data = data, Root = rect.gameObject, Background = image, NameLabel = name, LevelLabel = level };
                button.onClick.AddListener(() => OnTileClicked(view));
                rect.gameObject.SetActive(false);   // 보유 판정은 Refresh 가 한다
                tiles.Add(view);
            }

            // rows 는 박스 높이 계산에만 쓰였다 — 여기서 다시 세면 두 값이 갈릴 수 있어 인자로 받는다.
            if (rows < 1) Debug.LogWarning("[RelicShopPanel] 그리드 행 수가 0 이하 — 박스 높이 계산을 확인할 것.");
        }

        private void BuildFooter(RectTransform box, float messageY, float buttonY)
        {
            messageLabel = CreateTop(box, "Message", string.Empty, 18,
                new Vector2(0f, messageY), new Vector2(BOX_W - 80f, 32f),
                TextAnchor.MiddleCenter, new Color(1f, 0.95f, 0.75f));

            var (draw, drawImage) = CreateButton(box, "DrawButton", new Vector2(-140f, buttonY),
                new Vector2(240f, 56f), DrawBase,
                Loc.GetFormat(StringKey.Relic_DrawFormat, RelicGacha.DRAW_COST));
            draw.onClick.AddListener(OnDraw);
            drawButton = draw;
            drawButtonImage = drawImage;

            var (close, _) = CreateButton(box, "CloseButton", new Vector2(140f, buttonY),
                new Vector2(200f, 56f), CloseBase, Loc.Get(StringKey.Relic_Close));
            close.onClick.AddListener(Close);
        }

        // ───────────────────────── UI 헬퍼(런타임 생성) ─────────────────────────

        /// <summary>박스 상단 기준 라벨. y는 위에서부터의 음수 오프셋이다.</summary>
        private Text CreateTop(Transform parent, string name, string content, int size,
                               Vector2 pos, Vector2 rectSize, TextAnchor anchor, Color color)
        {
            var rect = CreateRect(parent, name, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0.5f, 0.5f), pos, rectSize);
            return AddText(rect, content, size, anchor, color);
        }

        private Text CreateCentered(Transform parent, string name, string content, int size,
                                    Vector2 pos, Vector2 rectSize, TextAnchor anchor, Color color)
        {
            var rect = CreateRect(parent, name, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), pos, rectSize);
            return AddText(rect, content, size, anchor, color);
        }

        private Text AddText(RectTransform rect, string content, int size, TextAnchor anchor, Color color)
        {
            var text = rect.gameObject.AddComponent<Text>();
            ApplyFont(text);
            text.text = content;
            text.fontSize = size;
            text.alignment = anchor;
            text.color = color;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            return text;
        }

        private RectTransform CreateRect(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax,
                                         Vector2 pivot, Vector2 anchoredPos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = size;
            return rect;
        }

        private (Button button, Image image) CreateButton(Transform parent, string name, Vector2 pos,
                                                          Vector2 size, Color baseColor, string labelText)
        {
            var rect = CreateRect(parent, name, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0.5f, 0.5f), pos, size);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = baseColor;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;

            var colors = button.colors;
            colors.normalColor = baseColor;
            colors.highlightedColor = Color.Lerp(baseColor, Color.white, 0.25f);
            colors.selectedColor = Color.Lerp(baseColor, Color.white, 0.35f);
            colors.pressedColor = Color.Lerp(baseColor, Color.black, 0.2f);
            colors.disabledColor = DrawDisabled;
            button.colors = colors;

            var labelRect = CreateRect(rect, "Label", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            AddText(labelRect, labelText, 18, TextAnchor.MiddleCenter, Color.white);

            return (button, image);
        }

        private void ApplyFont(Text text)
        {
            if (uiFont == null)
            {
                uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
            }
            if (uiFont != null) text.font = uiFont;
        }
    }
}
