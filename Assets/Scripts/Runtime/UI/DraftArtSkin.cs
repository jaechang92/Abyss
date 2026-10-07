using UnityEngine;
using UnityEngine.UI;

namespace Abyss.Runtime.UI
{
    /// <summary>
    /// 드래프트 창 전용 얇은 금속 테두리(시안 hud-draft-concept-v1 우측). <see cref="ModalArtSkin.ApplyDraft"/>가 끝에서 부른다.
    ///
    /// ApplyDraft가 이미 만든 석판 ArtFrame은 지우지 않고 Image.enabled만 끈다 — 공유 석판 텍스처·코너 규칙은 그대로이고
    /// 갈림길 등 다른 모달은 영향이 없다. 대신 2px 선 + 모서리 각인(14px)을 그린 9-slice 스프라이트를 이 클래스가 하나 만든다.
    ///
    /// 선택 가능한 대상(카드·리롤·스킵)은 <see cref="SelectableArtFeedback"/>의 frame을 얇은 테두리로 다시 연결한다.
    /// Refresh는 frame.color만 바꾸고 enabled는 건드리지 않으므로 꺼 둔 석판은 계속 꺼져 있고,
    /// 얇은 테두리가 기본(어둡게)·호버/선택(밝게)·비활성(흐리게) 밝기를 이어받는다. 호버 면·이중 외곽선·화살은 기존 그대로다.
    /// 장식은 모두 raycastTarget=false, 고정 이름이라 다시 적용해도 같은 오브젝트를 재사용한다.
    /// </summary>
    public static class DraftArtSkin
    {
        private const string STONE_FRAME_NAME = "ArtFrame";
        private const string THIN_FRAME_NAME = "DraftThinFrame";
        private const string HOVER_NAME = "ArtHover";
        private const string FOCUS_NAME = "ArtFocus";
        private const string RARITY_ACCENT_NAME = "DraftRarityAccent";
        private const string ICON_FRAME_NAME = "ArtIconFrame";
        private const string DETAIL_DIVIDER_NAME = "DraftDetailDivider";

        // 상세 슬롯 구분선: 아이콘 칸(DockPadding 20 + Icon 120)과 본문 사이 Gap 28의 가운데, 왼쪽 기준 x=154.
        // 세로는 상하 20씩 들이고 앵커를 위아래로 늘려 슬롯 높이가 바뀌어도 높이 = 슬롯 높이 - 40을 유지한다.
        private const float DIVIDER_X = 154f;
        private const float DIVIDER_WIDTH = 2f;
        private const float DIVIDER_VERTICAL_INSET = 20f;

        // 텍스처 1px = 기준 캔버스 1단위. 모서리 16px 안에 선 2px + ㄱ자 각인(4..13px) + 작은 마름모.
        private const int TEXTURE_SIZE = 48;
        private const int CORNER_PX = 16;
        private const int LINE_PX = 2;
        private const int ENGRAVE_START = 4;
        private const int ENGRAVE_END = 14;
        private const int ENGRAVE_THICKNESS = 2;
        private const int DIAMOND_CENTER = 9;
        private const float SPRITE_PPU = 100f;

        // 희귀도 띠: 카드 위 가장자리 선 바로 아래, 모서리 각인을 피해 좌우 20씩 들인다.
        private const float ACCENT_TOP = 2f;
        private const float ACCENT_HEIGHT = 4f;
        private const float ACCENT_SIDE_INSET = 20f;

        private static readonly Color32 LineColor = new(138, 131, 120, 255);      // #8A8378 저채도 금속
        private static readonly Color32 EngraveColor = new(179, 170, 154, 255);   // #B3AA9A 각인
        private static readonly Color32 Clear = new(0, 0, 0, 0);

        private static Sprite thinFrameSprite;

        /// <summary>드래프트 창의 지정 대상에만 얇은 테두리를 씌우고 카드에 희귀도 띠를 연결한다.</summary>
        public static void Apply(Transform mainPanel, Transform detailSlot, SkillCardView[] cards,
            Button reroll, Button skip, Transform buildContext, float referencePpu)
        {
            ApplyThinFrame(mainPanel, referencePpu);
            ApplyThinFrame(detailSlot, referencePpu);
            EnsureDetailDivider(detailSlot);
            ApplyThinFrame(buildContext, referencePpu);
            if (reroll != null) ApplyThinFrame(reroll.transform, referencePpu);
            if (skip != null) ApplyThinFrame(skip.transform, referencePpu);

            if (cards != null)
            {
                for (int i = 0; i < cards.Length; i++)
                {
                    if (cards[i] == null) continue;
                    ApplyThinFrame(cards[i].transform, referencePpu);
                    // 아이콘 배경칸에도 같은 금속 액자. 배경칸 Image(스프라이트 없음)는 그대로 두고 자식 장식만 붙는다.
                    ApplyThinFrame(cards[i].transform.Find(ICON_FRAME_NAME), referencePpu);
                    cards[i].AttachRarityAccent(EnsureRarityAccent(cards[i].transform));
                }
            }

            // 카드·상세 슬롯 테두리는 이미지로 덮어쓴다. 위 relink 뒤여야 feedback이 이미지 프레임·선택 이미지를 가리킨다.
            DraftImageSkin.Apply(detailSlot, cards);
            DraftImageSkin.ApplyDecorations(mainPanel, reroll, skip);
            DraftImageSkin.ApplyChrome(mainPanel, buildContext, cards);
        }

        private static void ApplyThinFrame(Transform target, float referencePpu)
        {
            if (target == null) return;
            var sprite = GetThinFrameSprite();
            if (sprite == null) return;

            var stone = target.Find(STONE_FRAME_NAME);
            if (stone != null && stone.TryGetComponent(out Image stoneImage)) stoneImage.enabled = false;

            var frame = GetOrCreateImage(target, THIN_FRAME_NAME);
            frame.sprite = sprite;
            frame.type = Image.Type.Sliced;
            frame.fillCenter = false;
            frame.pixelsPerUnitMultiplier = referencePpu / SPRITE_PPU; // 모서리 = CORNER_PX 단위
            frame.color = Color.white;
            Stretch(frame.transform);
            frame.transform.SetAsFirstSibling();

            RelinkFeedback(target, frame);
        }

        /// <summary>선택 표식 소유자는 그대로 두고 밝기 대상 frame만 얇은 테두리로 바꾼다. 호버 면·표식은 기존 오브젝트를 찾아 넘긴다.</summary>
        private static void RelinkFeedback(Transform target, Image frame)
        {
            if (!target.TryGetComponent(out SelectableArtFeedback feedback)) return;
            if (!target.TryGetComponent(out Selectable selectable)) return;

            Image hover = null;
            var hoverTransform = target.Find(HOVER_NAME);
            if (hoverTransform != null) hoverTransform.TryGetComponent(out hover);

            var focus = target.Find(FOCUS_NAME);
            var marks = focus != null ? focus.GetComponentsInChildren<Image>(true) : null;

            feedback.Initialize(selectable, frame, hover, marks);
        }

        /// <summary>카드 위 가장자리 희귀도 띠. 색·표시 여부는 <see cref="SkillCardView"/>가 Bind 때 정한다.</summary>
        private static Image EnsureRarityAccent(Transform card)
        {
            var accent = GetOrCreateImage(card, RARITY_ACCENT_NAME);
            accent.sprite = null;
            var rect = (RectTransform)accent.transform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(ACCENT_SIDE_INSET, -(ACCENT_TOP + ACCENT_HEIGHT));
            rect.offsetMax = new Vector2(-ACCENT_SIDE_INSET, -ACCENT_TOP);
            return accent;
        }

        /// <summary>상세 슬롯의 아이콘·본문 사이 2px 세로 구분선. 장식이라 raycast를 받지 않아 본문 입력을 가로채지 않는다.</summary>
        private static void EnsureDetailDivider(Transform detailSlot)
        {
            if (detailSlot == null) return;

            var divider = GetOrCreateImage(detailSlot, DETAIL_DIVIDER_NAME);
            divider.sprite = null;
            divider.color = LineColor;
            var rect = (RectTransform)divider.transform;
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(DIVIDER_X, 0f);
            rect.sizeDelta = new Vector2(DIVIDER_WIDTH, -DIVIDER_VERTICAL_INSET * 2f);
        }

        // ==================== 스프라이트 ====================

        /// <summary>
        /// 9-slice 테두리 스프라이트 1개를 소유한다. 네 모서리는 같은 무늬를 대칭으로 그린다.
        /// 도메인 리로드를 끈 재진입에서 살아 있으면 재사용하고, 파괴됐으면 Unity null 비교로 다시 만든다.
        /// </summary>
        private static Sprite GetThinFrameSprite()
        {
            if (thinFrameSprite != null) return thinFrameSprite;

            var texture = new Texture2D(TEXTURE_SIZE, TEXTURE_SIZE, TextureFormat.RGBA32, false)
            {
                name = "draft-thin-frame (DraftArtSkin)",
                filterMode = FilterMode.Bilinear, // 720p(0.67배)에서 2px 선이 끊기지 않게
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave
            };

            var pixels = new Color32[TEXTURE_SIZE * TEXTURE_SIZE];
            for (int y = 0; y < TEXTURE_SIZE; y++)
            {
                for (int x = 0; x < TEXTURE_SIZE; x++)
                {
                    pixels[y * TEXTURE_SIZE + x] = PixelAt(x, y);
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);

            var border = new Vector4(CORNER_PX, CORNER_PX, CORNER_PX, CORNER_PX);
            thinFrameSprite = Sprite.Create(texture, new Rect(0f, 0f, TEXTURE_SIZE, TEXTURE_SIZE),
                new Vector2(0.5f, 0.5f), SPRITE_PPU, 0, SpriteMeshType.FullRect, border);
            thinFrameSprite.name = "draft-thin-frame (DraftArtSkin)";
            thinFrameSprite.hideFlags = HideFlags.DontSave;
            return thinFrameSprite;
        }

        private static Color32 PixelAt(int x, int y)
        {
            int dx = Mathf.Min(x, TEXTURE_SIZE - 1 - x);
            int dy = Mathf.Min(y, TEXTURE_SIZE - 1 - y);

            if (dx < LINE_PX || dy < LINE_PX) return LineColor;
            if (dx >= CORNER_PX || dy >= CORNER_PX) return Clear;

            // ㄱ자 각인: 모서리에서 4px 들어온 두 팔
            bool verticalArm = dx >= ENGRAVE_START && dx < ENGRAVE_START + ENGRAVE_THICKNESS
                && dy >= ENGRAVE_START && dy < ENGRAVE_END;
            bool horizontalArm = dy >= ENGRAVE_START && dy < ENGRAVE_START + ENGRAVE_THICKNESS
                && dx >= ENGRAVE_START && dx < ENGRAVE_END;
            bool diamond = Mathf.Abs(dx - DIAMOND_CENTER) + Mathf.Abs(dy - DIAMOND_CENTER) <= 1;
            return verticalArm || horizontalArm || diamond ? EngraveColor : Clear;
        }

        // ==================== 헬퍼 ====================

        private static Image GetOrCreateImage(Transform parent, string name)
        {
            var existing = parent.Find(name);
            GameObject go;
            if (existing != null)
            {
                go = existing.gameObject;
            }
            else
            {
                go = new GameObject(name, typeof(RectTransform));
                go.layer = parent.gameObject.layer;
                go.transform.SetParent(parent, false);
            }

            if (!go.TryGetComponent(out Image image)) image = go.AddComponent<Image>();
            image.raycastTarget = false;
            return image;
        }

        private static void Stretch(Transform target)
        {
            if (target is not RectTransform rect) return;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
