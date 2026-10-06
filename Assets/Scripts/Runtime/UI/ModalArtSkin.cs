using UnityEngine;
using UnityEngine.UI;

namespace Abyss.Runtime.UI
{
    /// <summary>
    /// 채택 UI(2026-10-06 「파편과 기록판」) 2단계 — 드래프트·갈림길 모달 공통 스킨.
    ///
    /// <see cref="HudArtSkin"/>과 같은 방식이다: 기존 오브젝트의 위치·크기·색·폰트만 바꾸고 석판 프레임 장식을 덧붙인다.
    /// 빌더·씬을 다시 만들지 않으므로 이미 저장된 Run 씬과 동적 생성 모달에 그대로 적용된다.
    /// 직렬화 참조·버튼 콜백·Navigation·선택 판정은 건드리지 않는다.
    ///
    /// 장식은 모두 raycastTarget=false이고 고정 이름이라 두 번 적용돼도 같은 오브젝트를 재사용한다.
    /// 화면별 좌표는 ModalArtSkin.Layout.cs.
    /// </summary>
    public static partial class ModalArtSkin
    {
        private const string FRAME_RESOURCE_PATH = "UI/AbyssSkin/slate-frame-v1";

        // HudArtSkin과 같은 원본 수치(1254px 원본의 9-slice 테두리·투명 여백·띠 끝).
        private const float FRAME_BORDER_PX = 256f;
        private const float FRAME_OUTER_EDGE_PX = 82f;
        private const float FRAME_INNER_EDGE_PX = 160f;
        private const float FRAME_SPRITE_PPU = 100f;

        private const string FRAME_NAME = "ArtFrame";
        private const string HOVER_NAME = "ArtHover";
        private const string FOCUS_NAME = "ArtFocus";

        // 선택 표식: 대상 바깥으로 이중 외곽선 + 좌우 홈. 카드·노드 간격(32)의 절반 안에 머문다.
        private const float FOCUS_OUTSET = 10f;
        private const float FOCUS_OUTER_THICKNESS = 3f;
        private const float FOCUS_INNER_OFFSET = 5f;
        private const float FOCUS_INNER_THICKNESS = 2f;
        private const float NOTCH_CENTER = 2f;
        private static readonly Vector2 NotchSize = new(10f, 56f);

        internal static readonly Color PanelColor = new(0.090f, 0.129f, 0.169f, 0.96f); // #17212B
        internal static readonly Color BodyTextColor = new(0.906f, 0.878f, 0.808f); // #E7E0CE
        internal static readonly Color SubTextColor = new(0.671f, 0.722f, 0.753f); // #ABB8C0
        internal static readonly Color FocusColor = new(0.906f, 0.878f, 0.808f); // #E7E0CE
        internal static readonly Color HoverFillColor = new(0.906f, 0.878f, 0.808f, 0.08f);

        // Sprite.Create로 만든 프레임 스프라이트 1개를 소유한다(HudArtSkin 캐시는 그쪽 private이라 따로 둔다).
        // 도메인 리로드를 끈 재진입에서도 살아 있으면 재사용하고, 파괴됐으면 Unity null 비교로 다시 만든다.
        private static Sprite frameSprite;
        private static bool hasWarnedMissingFrame;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            hasWarnedMissingFrame = false;
        }

        // ==================== 프레임 ====================

        private static Sprite GetFrameSprite()
        {
            if (frameSprite != null) return frameSprite;

            var texture = Resources.Load<Texture2D>(FRAME_RESOURCE_PATH);
            if (texture == null)
            {
                if (!hasWarnedMissingFrame)
                {
                    Debug.LogWarning($"[ModalArtSkin] 프레임 텍스처 Resources/{FRAME_RESOURCE_PATH} 를 찾지 못해 프레임 장식을 건너뜁니다.");
                    hasWarnedMissingFrame = true;
                }
                return null;
            }

            var border = new Vector4(FRAME_BORDER_PX, FRAME_BORDER_PX, FRAME_BORDER_PX, FRAME_BORDER_PX);
            frameSprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f), FRAME_SPRITE_PPU, 0, SpriteMeshType.FullRect, border);
            frameSprite.name = "slate-frame-v1 (ModalArtSkin)";
            frameSprite.hideFlags = HideFlags.DontSave;
            return frameSprite;
        }

        /// <summary>
        /// 대상 rect 둘레에 석판 프레임을 씌운다(맨 아래 자식 — 내용 글자·아이콘이 위에 그려진다).
        /// 원본의 투명 여백만큼 바깥으로 늘려 석판 띠가 대상 가장자리에서 시작한다.
        /// Sliced 모서리 크기 = border / (spritePPU / referencePPU × multiplier) 이므로 multiplier를 역산한다.
        /// </summary>
        public static Image EnsureFrame(Transform target, float cornerUnits, float referencePpu)
        {
            if (target == null) return null;
            var sprite = GetFrameSprite();
            if (sprite == null) return null;

            var frame = GetOrCreateImage(target, FRAME_NAME);
            frame.sprite = sprite;
            frame.type = Image.Type.Sliced;
            frame.fillCenter = false; // 중앙은 투명 — 카드 희귀도 배경·글자를 덮지 않는다
            frame.pixelsPerUnitMultiplier = FRAME_BORDER_PX * referencePpu / (FRAME_SPRITE_PPU * cornerUnits);
            frame.color = Color.white;
            Stretch(frame.transform, -OuterInset(cornerUnits));
            frame.transform.SetAsFirstSibling();
            return frame;
        }

        /// <summary>모달 바탕 면 색 + 프레임. 면 Image는 기존 것을 그대로 쓴다(없으면 프레임만).</summary>
        public static void ApplyPanel(Transform panel, float cornerUnits, float referencePpu)
        {
            if (panel == null) return;
            if (panel.TryGetComponent(out Image face)) face.color = PanelColor;
            EnsureFrame(panel, cornerUnits, referencePpu);
        }

        /// <summary>프레임 띠가 끝나는 안쪽 — 대상 가장자리 기준 글자 여백.</summary>
        public static float InnerInset(float cornerUnits) =>
            cornerUnits * (FRAME_INNER_EDGE_PX - FRAME_OUTER_EDGE_PX) / FRAME_BORDER_PX;

        private static float OuterInset(float cornerUnits) => cornerUnits * FRAME_OUTER_EDGE_PX / FRAME_BORDER_PX;

        public static float ResolveReferencePpu(Transform target)
        {
            var canvas = target != null ? target.GetComponentInParent<Canvas>(true) : null;
            return canvas != null ? canvas.referencePixelsPerUnit : 100f;
        }

        // ==================== 선택 가능한 대상 ====================

        /// <summary>
        /// 버튼(카드·노드·리롤·스킵)에 프레임·호버 면·선택 표식을 만들고 <see cref="SelectableArtFeedback"/>을
        /// 실제 포커스가 머무는 버튼 GameObject에 붙인다. 버튼의 색 전환·콜백·Navigation은 그대로다.
        /// </summary>
        public static SelectableArtFeedback EnsureSelectableSkin(Selectable target, float cornerUnits, float referencePpu)
        {
            if (target == null) return null;
            var t = target.transform;

            var frame = EnsureFrame(t, cornerUnits, referencePpu);

            // 호버 면: 기존 배경색(희귀도 등)을 바꾸지 않고 그 위에 옅은 면을 얹는다. 프레임보다 아래.
            var hover = GetOrCreateImage(t, HOVER_NAME);
            hover.sprite = null;
            hover.color = HoverFillColor;
            Stretch(hover.transform, 0f);
            hover.transform.SetAsFirstSibling();

            var marks = EnsureFocusMarks(t);

            if (!target.TryGetComponent(out SelectableArtFeedback feedback))
            {
                feedback = target.gameObject.AddComponent<SelectableArtFeedback>();
            }
            feedback.Initialize(target, frame, hover, marks);
            return feedback;
        }

        /// <summary>이중 외곽선 4+4개와 좌우 홈 2개. 맨 위 자식이지만 대상 바깥 테두리에만 그린다.</summary>
        private static Image[] EnsureFocusMarks(Transform target)
        {
            var group = target.Find(FOCUS_NAME);
            if (group == null)
            {
                var go = new GameObject(FOCUS_NAME, typeof(RectTransform));
                go.layer = target.gameObject.layer;
                go.transform.SetParent(target, false);
                group = go.transform;
            }
            Stretch(group, -FOCUS_OUTSET);
            group.SetAsLastSibling();

            float o = FOCUS_OUTER_THICKNESS;
            float a = FOCUS_INNER_OFFSET;
            float b = FOCUS_INNER_OFFSET + FOCUS_INNER_THICKNESS;
            return new[]
            {
                Bar(group, "OuterTop", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -o), Vector2.zero),
                Bar(group, "OuterBottom", Vector2.zero, new Vector2(1, 0), Vector2.zero, new Vector2(0, o)),
                Bar(group, "OuterLeft", Vector2.zero, new Vector2(0, 1), Vector2.zero, new Vector2(o, 0)),
                Bar(group, "OuterRight", new Vector2(1, 0), Vector2.one, new Vector2(-o, 0), Vector2.zero),
                Bar(group, "InnerTop", new Vector2(0, 1), Vector2.one, new Vector2(a, -b), new Vector2(-a, -a)),
                Bar(group, "InnerBottom", Vector2.zero, new Vector2(1, 0), new Vector2(a, a), new Vector2(-a, b)),
                Bar(group, "InnerLeft", Vector2.zero, new Vector2(0, 1), new Vector2(a, a), new Vector2(b, -a)),
                Bar(group, "InnerRight", new Vector2(1, 0), Vector2.one, new Vector2(-b, a), new Vector2(-a, -a)),
                Notch(group, "NotchLeft", new Vector2(0f, 0.5f), new Vector2(NOTCH_CENTER, 0f)),
                Notch(group, "NotchRight", new Vector2(1f, 0.5f), new Vector2(-NOTCH_CENTER, 0f)),
            };
        }

        private static Image Bar(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            var image = GetOrCreateImage(parent, name);
            image.sprite = null;
            image.color = FocusColor;
            var rect = (RectTransform)image.transform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            return image;
        }

        private static Image Notch(Transform parent, string name, Vector2 anchor, Vector2 position)
        {
            var image = GetOrCreateImage(parent, name);
            image.sprite = null;
            image.color = FocusColor;
            SetRect(image.transform, anchor, new Vector2(0.5f, 0.5f), position, NotchSize);
            return image;
        }

        // ==================== 조회·생성 헬퍼 ====================

        private static Transform FindRequired(Transform parent, string path)
        {
            if (parent == null) return null;
            var child = parent.Find(path);
            if (child == null)
            {
                Debug.LogWarning($"[ModalArtSkin] '{parent.name}/{path}' 를 찾지 못해 해당 스킨을 건너뜁니다 — 빌더 계층 이름과 다릅니다.");
            }
            return child;
        }

        /// <summary>같은 이름의 장식 자식이 있으면 재사용, 없으면 새로 만든다. 장식은 입력을 받지 않는다.</summary>
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

        /// <summary>앵커·피벗을 같은 점에 두고 위치·크기를 정한다.</summary>
        public static void SetRect(Transform target, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            if (target is not RectTransform rect) return;
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        /// <summary>부모 위쪽 가운데 기준. top = 부모 위 가장자리에서 아래로 떨어진 거리.</summary>
        public static void SetTopRect(Transform target, float top, Vector2 size)
        {
            SetRect(target, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -top), size);
        }

        private static void Stretch(Transform target, float inset)
        {
            if (target is not RectTransform rect) return;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
        }

        /// <summary>
        /// 글자 스타일. 본문 자동 축소는 끈다(레이아웃 스펙). 폭 안에서 줄바꿈하고 영역을 넘는 줄은 잘라
        /// 옆 요소와 겹치지 않게 한다 — 잘린 전체 문구는 기존 상세 패널에서 본다.
        /// </summary>
        public static void StyleText(Text text, int fontSize, Color color)
        {
            if (text == null) return;
            text.color = color;
            StyleText(text, fontSize);
        }

        /// <summary>색은 그대로 두는 판 — 데이터가 색을 정하는 글자(희귀도 줄·방 타입)용.</summary>
        public static void StyleText(Text text, int fontSize)
        {
            if (text == null) return;
            text.fontSize = fontSize;
            text.resizeTextForBestFit = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
        }
    }
}
