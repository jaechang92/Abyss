using UnityEngine;
using UnityEngine.UI;

namespace Abyss.Runtime.UI
{
    /// <summary>
    /// 드래프트 카드 3장·상세 슬롯의 테두리를 Codex가 만든 이미지 3종으로 바꾼다. <see cref="DraftArtSkin.Apply"/>가 relink 뒤 마지막에 부른다.
    ///
    /// 이 클래스는 이미지 로드·잘라 쓸 rect·캐시만 담당한다. 선택·호버·비활성 표시는 기존 <see cref="SelectableArtFeedback"/>을
    /// 그대로 다시 Initialize해서 쓴다(frame=카드 프레임 이미지, marks=선택 이미지 1개). 선택·Submit·Navigation은 건드리지 않는다.
    ///
    /// Resources 텍스처가 하나라도 없으면 경고만 남기고 아무것도 바꾸지 않는다 — 얇은 테두리·ArtFocus 표식이 그대로 남는다(대체 경로).
    /// 성공했을 때만 카드의 DraftThinFrame·ArtFocus 표식과 상세 슬롯의 DraftThinFrame을 끈다. 상세 구분선·아이콘 액자·희귀도 띠는 그대로다.
    ///
    /// 좌표는 원본 PNG 왼쪽 위 기준으로 적고, Unity Sprite rect는 y = 텍스처 높이 - (top + height)로 뒤집는다. 픽셀은 읽지 않는다.
    /// </summary>
    public static class DraftImageSkin
    {
        private const string RESOURCE_FOLDER = "UI/DraftImages/";
        private const string CARD_FRAME_PATH = RESOURCE_FOLDER + "card-frame-v1";
        private const string DETAIL_FRAME_PATH = RESOURCE_FOLDER + "detail-frame-v1";
        private const string CARD_SELECTED_PATH = RESOURCE_FOLDER + "card-selected-v1";
        private const string BUTTON_FRAME_PATH = RESOURCE_FOLDER + "button-frame-v1";
        private const string TITLE_RULE_PATH = RESOURCE_FOLDER + "title-rule-v1";
        private const string PANEL_FRAME_PATH = RESOURCE_FOLDER + "panel-frame-v1";

        private const string THIN_FRAME_NAME = "DraftThinFrame";
        private const string HOVER_NAME = "ArtHover";
        private const string FOCUS_NAME = "ArtFocus";
        private const string IMAGE_FRAME_NAME = "DraftImageFrame";
        private const string IMAGE_SELECTED_NAME = "DraftImageSelected";

        private const float SPRITE_PPU = 100f;

        // card-frame-v1(1244×1265): 불투명 프레임 bbox. 카드 432×440에 Simple로 맞춘다.
        private static readonly RectInt CardFrameSource = new(90, 56, 1065, 1153);

        // detail-frame-v1(2172×724): 상세 슬롯 1360×276. 원본 영역과 종횡비 차이는 약 0.23%다.
        private static readonly RectInt DetailFrameSource = new(27, 146, 2119, 429);
        private static readonly RectInt ButtonFrameSource = new(36, 244, 1978, 279);
        private static readonly RectInt TitleRuleSource = new(61, 395, 1669, 98);
        private static readonly RectInt PanelFrameSource = new(30, 30, 1196, 1194);
        private const float PANEL_BORDER_PX = 160f;

        // card-selected-v1(1244×1265): 몸통 left139 right1105 top88 bottom1115, 아래 표식 끝 1169.
        // 몸통 바깥으로 좌·우·위 10px, 아래 표식 54px + 여백 10px을 함께 잘라 낸다.
        private static readonly RectInt SelectedSource = new(129, 78, 986, 1101);
        private const float SELECTED_BODY_WIDTH = 966f;   // 1105 - 139
        private const float SELECTED_BODY_HEIGHT = 1027f; // 1115 - 88
        private const float SELECTED_SIDE_MARGIN = 10f;
        private const float SELECTED_TOP_MARGIN = 10f;
        private const float SELECTED_BOTTOM_EXTENT = 64f; // 표식 54 + 여백 10

        private static Sprite cardFrameSprite;
        private static Sprite detailFrameSprite;
        private static Sprite selectedSprite;
        private static bool hasWarnedMissing;
        private static Sprite buttonFrameSprite;
        private static Sprite titleRuleSprite;
        private static bool hasWarnedMissingButton;
        private static bool hasWarnedMissingTitle;
        private static Sprite panelFrameSprite;
        private static bool hasWarnedMissingPanel;

        /// <summary>카드·상세 슬롯 테두리를 이미지로 바꾼다. 텍스처가 빠지면 기존 절차 테두리를 그대로 둔다.</summary>
        public static void Apply(Transform detailSlot, SkillCardView[] cards)
        {
            if (!TryLoadSprites()) return;

            ApplyDetail(detailSlot);
            if (cards == null) return;
            for (int i = 0; i < cards.Length; i++)
            {
                if (cards[i] == null) continue;
                ApplyCard(cards[i].transform);
            }
        }

        // ==================== 대상 ====================

        /// <summary>큰 바탕과 아이콘 액자는 같은 이미지의 모서리를 고정해 크기를 맞춘다.</summary>
        public static void ApplyChrome(Transform mainPanel, Transform buildContext, SkillCardView[] cards)
        {
            if (panelFrameSprite == null)
                panelFrameSprite = CreateSprite(PANEL_FRAME_PATH, PanelFrameSource,
                    new Vector4(PANEL_BORDER_PX, PANEL_BORDER_PX, PANEL_BORDER_PX, PANEL_BORDER_PX));
            if (panelFrameSprite == null)
            {
                if (!hasWarnedMissingPanel)
                {
                    hasWarnedMissingPanel = true;
                    Debug.LogWarning("[DraftImageSkin] 바탕 패널 이미지가 없어 기존 프레임을 유지합니다.");
                }
                return;
            }

            ApplySlicedFrame(mainPanel, 20f);
            ApplySlicedFrame(buildContext, 16f);
            if (cards != null)
            {
                foreach (var card in cards)
                {
                    if (card != null) ApplySlicedFrame(card.transform.Find("ArtIconFrame"), 8f);
                }
            }
            if (mainPanel != null && mainPanel.parent != null)
                ApplySlicedFrame(mainPanel.parent.Find("SkillCardDetails/Body/IconFrame"), 8f);
        }

        private static void ApplySlicedFrame(Transform target, float cornerUnits)
        {
            if (target == null) return;
            var image = GetOrCreateImage(target, "DraftImagePanelFrame");
            image.sprite = panelFrameSprite;
            image.type = Image.Type.Sliced;
            image.fillCenter = false;
            image.color = Color.white;
            image.enabled = true;
            float referencePpu = ModalArtSkin.ResolveReferencePpu(target);
            image.pixelsPerUnitMultiplier = PANEL_BORDER_PX * referencePpu / (SPRITE_PPU * cornerUnits);
            Stretch(image.rectTransform, Vector2.zero, Vector2.zero);
            image.transform.SetAsFirstSibling();
            SetGraphicEnabled(target.Find(THIN_FRAME_NAME), false);
        }

        /// <summary>제목과 버튼 장식만 교체한다. 개별 이미지 누락 시 해당 기존 장식을 유지한다.</summary>
        public static void ApplyDecorations(Transform mainPanel, Button reroll, Button skip)
        {
            if (LoadDecoration(ref buttonFrameSprite, BUTTON_FRAME_PATH, ButtonFrameSource,
                ref hasWarnedMissingButton))
            {
                ApplyButton(reroll);
                ApplyButton(skip);
            }

            if (mainPanel == null) return;
            if (!LoadDecoration(ref titleRuleSprite, TITLE_RULE_PATH, TitleRuleSource,
                ref hasWarnedMissingTitle)) return;

            ApplyTitleRule(mainPanel, "DraftImageTitleLeft", -560f, false);
            ApplyTitleRule(mainPanel, "DraftImageTitleRight", 560f, true);
            SetGraphicEnabled(mainPanel.Find("ArtTitleRuleLeft"), false);
            SetGraphicEnabled(mainPanel.Find("ArtTitleRuleRight"), false);
            SetGraphicEnabled(mainPanel.Find("ArtTitleDiamondLeft"), false);
            SetGraphicEnabled(mainPanel.Find("ArtTitleDiamondRight"), false);
        }

        private static bool LoadDecoration(ref Sprite cached, string path, RectInt source, ref bool warned)
        {
            if (cached == null) cached = CreateSprite(path, source);
            if (cached != null) return true;
            if (!warned)
            {
                warned = true;
                Debug.LogWarning($"[DraftImageSkin] Resources/{path} 장식을 읽지 못해 기존 표시를 유지합니다.");
            }
            return false;
        }

        private static void ApplyButton(Button button)
        {
            if (button == null) return;
            var target = button.transform;
            var frame = GetOrCreateImage(target, "DraftImageButtonFrame");
            frame.sprite = buttonFrameSprite;
            frame.type = Image.Type.Simple;
            frame.preserveAspect = true;
            frame.color = Color.white;
            frame.enabled = true;
            Stretch(frame.rectTransform, Vector2.zero, Vector2.zero);
            frame.transform.SetAsFirstSibling();

            var hoverTarget = target.Find(HOVER_NAME);
            Image hover = null;
            if (hoverTarget != null) hoverTarget.TryGetComponent(out hover);
            var focus = target.Find(FOCUS_NAME);
            var marks = focus != null ? focus.GetComponentsInChildren<Image>(true) : null;
            if (button.TryGetComponent(out SelectableArtFeedback feedback))
                feedback.Initialize(button, frame, hover, marks);

            SetGraphicEnabled(target.Find(THIN_FRAME_NAME), false);
        }

        private static void ApplyTitleRule(Transform parent, string name, float x, bool mirrored)
        {
            var image = GetOrCreateImage(parent, name);
            image.sprite = titleRuleSprite;
            image.type = Image.Type.Simple;
            image.preserveAspect = true;
            image.color = Color.white;
            image.enabled = true;
            var rect = image.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(x, -52f);
            rect.sizeDelta = new Vector2(200f, 12f);
            rect.localRotation = Quaternion.identity;
            rect.localScale = mirrored ? new Vector3(-1f, 1f, 1f) : Vector3.one;
        }

        private static void ApplyCard(Transform card)
        {
            var frame = GetOrCreateImage(card, IMAGE_FRAME_NAME);
            frame.sprite = cardFrameSprite;
            frame.type = Image.Type.Simple;
            frame.preserveAspect = false;
            Stretch((RectTransform)frame.transform, Vector2.zero, Vector2.zero);
            frame.transform.SetAsFirstSibling();

            // 선택 이미지: 몸통을 카드 크기에 맞추고 바깥 여백·아래 표식만큼 늘린다. 프레임 바로 위, 본문보다 아래.
            var selected = GetOrCreateImage(card, IMAGE_SELECTED_NAME);
            selected.sprite = selectedSprite;
            selected.type = Image.Type.Simple;
            selected.preserveAspect = false;
            selected.color = Color.white;
            selected.enabled = false;
            var cardSize = ((RectTransform)card).rect.size;
            if (cardSize.x <= 0f || cardSize.y <= 0f) cardSize = ((RectTransform)card).sizeDelta;
            float horizontalScale = cardSize.x / SELECTED_BODY_WIDTH;
            float verticalScale = cardSize.y / SELECTED_BODY_HEIGHT;
            Stretch((RectTransform)selected.transform,
                new Vector2(-SELECTED_SIDE_MARGIN * horizontalScale, -SELECTED_BOTTOM_EXTENT * verticalScale),
                new Vector2(SELECTED_SIDE_MARGIN * horizontalScale, SELECTED_TOP_MARGIN * verticalScale));
            selected.transform.SetSiblingIndex(1);

            SetGraphicEnabled(card.Find(THIN_FRAME_NAME), false);
            DisableFocusMarks(card);
            RelinkFeedback(card, frame, selected);
        }

        private static void ApplyDetail(Transform detailSlot)
        {
            if (detailSlot == null) return;

            var frame = GetOrCreateImage(detailSlot, IMAGE_FRAME_NAME);
            frame.sprite = detailFrameSprite;
            frame.type = Image.Type.Simple;
            frame.preserveAspect = false;
            frame.color = Color.white;
            Stretch((RectTransform)frame.transform, Vector2.zero, Vector2.zero);
            frame.transform.SetAsFirstSibling();

            SetGraphicEnabled(detailSlot.Find(THIN_FRAME_NAME), false);
        }

        /// <summary>밝기 대상 frame은 카드 프레임 이미지, 선택 표식은 선택 이미지 1개. 호버 면은 기존 오브젝트를 넘긴다.</summary>
        private static void RelinkFeedback(Transform card, Image frame, Image selected)
        {
            if (!card.TryGetComponent(out SelectableArtFeedback feedback)) return;
            if (!card.TryGetComponent(out Selectable selectable)) return;

            Image hover = null;
            var hoverTransform = card.Find(HOVER_NAME);
            if (hoverTransform != null) hoverTransform.TryGetComponent(out hover);

            feedback.Initialize(selectable, frame, hover, new[] { selected });
        }

        /// <summary>ArtFocus 그룹은 지우지 않고(이름 재사용) 그래픽만 끈다. feedback이 더는 이 표식을 켜지 않는다.</summary>
        private static void DisableFocusMarks(Transform card)
        {
            var focus = card.Find(FOCUS_NAME);
            if (focus == null) return;
            var graphics = focus.GetComponentsInChildren<Graphic>(true);
            for (int i = 0; i < graphics.Length; i++) graphics[i].enabled = false;
        }

        // ==================== 스프라이트 ====================

        /// <summary>
        /// 텍스처 3장을 모두 얻었을 때만 true. 스프라이트는 도메인 리로드를 끈 재진입에서도 살아 있으면 재사용하고,
        /// 파괴됐으면 Unity null 비교로 다시 만든다.
        /// </summary>
        private static bool TryLoadSprites()
        {
            if (cardFrameSprite == null) cardFrameSprite = CreateSprite(CARD_FRAME_PATH, CardFrameSource);
            if (detailFrameSprite == null) detailFrameSprite = CreateSprite(DETAIL_FRAME_PATH, DetailFrameSource);
            if (selectedSprite == null) selectedSprite = CreateSprite(CARD_SELECTED_PATH, SelectedSource);

            bool isReady = cardFrameSprite != null && detailFrameSprite != null && selectedSprite != null;
            if (!isReady && !hasWarnedMissing)
            {
                hasWarnedMissing = true;
                Debug.LogWarning($"[DraftImageSkin] Resources/{RESOURCE_FOLDER} 이미지 3종 중 일부를 찾지 못했습니다. " +
                    "드래프트 카드·상세 슬롯은 기존 얇은 테두리로 표시합니다.");
            }
            return isReady;
        }

        private static Sprite CreateSprite(string path, RectInt sourceTopLeft, Vector4 border = default)
        {
            var texture = Resources.Load<Texture2D>(path);
            if (texture == null) return null;

            // 원본 왼쪽 위 기준 → Unity 왼쪽 아래 기준
            float y = texture.height - (sourceTopLeft.y + sourceTopLeft.height);
            var rect = new Rect(sourceTopLeft.x, y, sourceTopLeft.width, sourceTopLeft.height);
            if (rect.xMin < 0f || rect.yMin < 0f || rect.xMax > texture.width || rect.yMax > texture.height)
            {
                Debug.LogWarning($"[DraftImageSkin] {path} 크기 {texture.width}×{texture.height}가 예상과 달라 잘라 쓸 수 없습니다.");
                return null;
            }

            var sprite = Sprite.Create(texture, rect, new Vector2(0.5f, 0.5f), SPRITE_PPU, 0, SpriteMeshType.FullRect, border);
            sprite.name = texture.name + " (DraftImageSkin)";
            sprite.hideFlags = HideFlags.DontSave;
            return sprite;
        }

        // ==================== 헬퍼 ====================

        private static void SetGraphicEnabled(Transform target, bool isEnabled)
        {
            if (target != null && target.TryGetComponent(out Graphic graphic)) graphic.enabled = isEnabled;
        }

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

        private static void Stretch(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }
    }
}
