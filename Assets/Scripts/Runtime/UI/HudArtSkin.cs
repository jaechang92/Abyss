using UnityEngine;
using UnityEngine.UI;

namespace Abyss.Runtime.UI
{
    /// <summary>
    /// 채택 UI(2026-10-06 「파편과 기록판」) 1단계 — 전투 HUD 스킨.
    ///
    /// HudBuilder가 만든 기존 자식의 위치·크기·색·폰트만 바꾸고 석판 프레임 장식을 덧붙인다.
    /// 빌더·씬을 다시 만들지 않고 <see cref="HUDPresenter"/>.Start에서 적용하므로 이미 저장된 Run 씬에도 바로 반영된다.
    /// Presenter 직렬화 참조·이벤트 구독·Filled 게이지 설정(방향·원점)은 건드리지 않는다.
    ///
    /// 조회는 HUD 루트의 자기 자식만(Transform.Find — 비활성 포함). 이름이 빌더와 다르면 그 부분만 건너뛰고 경고한다.
    /// 추가 장식은 고정 이름이라 두 번 적용돼도 같은 오브젝트를 재사용한다. 좌표·크기는 HudArtSkin.Layout.cs.
    /// </summary>
    public static partial class HudArtSkin
    {
        private const string FRAME_RESOURCE_PATH = "UI/AbyssSkin/slate-frame-v1";

        // 1254px 원본의 9-slice 테두리. 모서리 장식(~250px)이 늘어나지 않도록 그 바깥에서 자른다.
        private const float FRAME_BORDER_PX = 256f;
        // 원본 가장자리의 투명 여백 끝(석판 띠 시작) — 백플레이트가 프레임 밖으로 비어져 나오지 않게 하는 여백.
        private const float FRAME_OUTER_EDGE_PX = 82f;
        // 석판 띠 + 금속선이 끝나는 지점 — 게이지·라벨이 프레임 띠 안쪽에서 시작하게 하는 여백.
        private const float FRAME_INNER_EDGE_PX = 160f;
        private const float FRAME_SPRITE_PPU = 100f;

        private const string FRAME_NAME = "ArtFrame";
        private const string BACKPLATE_NAME = "ArtBackplate";

        private static readonly Color PanelColor = new(0.090f, 0.129f, 0.169f, 0.92f); // #17212B
        private static readonly Color BodyTextColor = new(0.906f, 0.878f, 0.808f); // #E7E0CE
        private static readonly Color HpColor = new(0.725f, 0.294f, 0.325f); // #B94B53
        // HudBuilder가 슬롯 아이콘 생성 시 넣은 회색. 아이콘 없는 보유 스킬의 자리표시자 규약(SkillSlotPresenter.SetSkill).
        private static readonly Color SkillPlaceholderColor = new(0.35f, 0.35f, 0.4f);

        // Sprite.Create로 만든 프레임 스프라이트 1개를 소유한다. 도메인 리로드를 끈 플레이 재진입에서도
        // 살아 있으면 재사용하므로 누적 생성되지 않는다(파괴됐으면 Unity null 비교로 다시 만든다).
        private static Sprite frameSprite;
        private static bool hasWarnedMissingFrame;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            hasWarnedMissingFrame = false;
        }

        /// <summary>HUD 루트(HUDPresenter가 붙은 오브젝트)의 기존 자식에 스킨을 적용한다.</summary>
        public static void Apply(Transform hudRoot)
        {
            if (hudRoot == null) return;

            float referencePpu = ResolveReferencePpu(hudRoot);
            ApplyHealthBar(hudRoot, referencePpu);
            ApplyFormSlot(hudRoot, referencePpu);
            ApplySkillSlot(hudRoot, "SkillSlot0", SkillSlot0Position, referencePpu);
            ApplySkillSlot(hudRoot, "SkillSlot1", SkillSlot1Position, referencePpu);
            ApplyFormBiasWarning(hudRoot);
            ApplySynergyCounter(hudRoot);
            ApplyBurnStack(hudRoot);
            ApplyGoldCounter(hudRoot, referencePpu);
        }

        /// <summary>
        /// 스킬 교체 뒤 아이콘 색 동기화. 아이콘 스프라이트가 있으면 원색(white),
        /// 없으면 기존 회색 자리표시자 — 흰 tint로 두면 아이콘 없는 스킬이 흰 사각형으로 보인다.
        /// </summary>
        public static void SyncSkillIconTint(SkillSlotPresenter slot)
        {
            if (slot == null) return;
            var icon = slot.transform.Find("Icon");
            if (icon != null && icon.TryGetComponent(out Image image)) SyncSkillIconTint(image);
        }

        private static void SyncSkillIconTint(Image icon)
        {
            icon.color = icon.sprite != null ? Color.white : SkillPlaceholderColor;
        }

        // ==================== 프레임 ====================

        private static Sprite GetFrameSprite()
        {
            if (frameSprite != null) return frameSprite;

            // UI 텍스처는 non-readable이어도 Sprite.Create가 가능하다(픽셀을 읽지 않고 참조만 한다).
            var texture = Resources.Load<Texture2D>(FRAME_RESOURCE_PATH);
            if (texture == null)
            {
                if (!hasWarnedMissingFrame)
                {
                    Debug.LogWarning($"[HudArtSkin] 프레임 텍스처 Resources/{FRAME_RESOURCE_PATH} 를 찾지 못해 프레임 장식을 건너뜁니다.");
                    hasWarnedMissingFrame = true;
                }
                return null;
            }

            var border = new Vector4(FRAME_BORDER_PX, FRAME_BORDER_PX, FRAME_BORDER_PX, FRAME_BORDER_PX);
            frameSprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f), FRAME_SPRITE_PPU, 0, SpriteMeshType.FullRect, border);
            frameSprite.name = "slate-frame-v1 (HudArtSkin)";
            frameSprite.hideFlags = HideFlags.DontSave;
            return frameSprite;
        }

        /// <summary>
        /// 이름 고정 프레임 Image를 만들거나 재사용한다. cornerUnits = 화면(캔버스 기준 해상도)에서 모서리 한 칸의 크기.
        /// Sliced 모서리 크기 = border / (spritePPU / referencePPU × multiplier) 이므로 multiplier를 역산한다.
        /// 예: 기준 PPU 100에서 cornerUnits 16 → multiplier 16 (256px 모서리가 16단위로 그려짐).
        /// </summary>
        private static Image EnsureFrame(Transform parent, string name, float cornerUnits, float referencePpu)
        {
            var sprite = GetFrameSprite();
            if (sprite == null) return null;

            var frame = GetOrCreateImage(parent, name);
            frame.sprite = sprite;
            frame.type = Image.Type.Sliced;
            frame.fillCenter = false; // 중앙은 투명 — 아이콘·게이지·텍스트를 가리지 않는다
            frame.pixelsPerUnitMultiplier = FRAME_BORDER_PX * referencePpu / (FRAME_SPRITE_PPU * cornerUnits);
            frame.color = Color.white;
            return frame;
        }

        /// <summary>프레임 원본의 투명 여백만큼 안쪽 — 백플레이트 여백.</summary>
        private static float OuterInset(float cornerUnits) => cornerUnits * FRAME_OUTER_EDGE_PX / FRAME_BORDER_PX;

        /// <summary>프레임 띠가 끝나는 안쪽 — 게이지·라벨 여백.</summary>
        private static float InnerInset(float cornerUnits) => cornerUnits * FRAME_INNER_EDGE_PX / FRAME_BORDER_PX;

        private static float ResolveReferencePpu(Transform hudRoot)
        {
            var canvas = hudRoot.GetComponentInParent<Canvas>(true);
            return canvas != null ? canvas.referencePixelsPerUnit : 100f;
        }

        // ==================== 조회·생성 헬퍼 ====================

        private static Transform FindRequired(Transform parent, string path)
        {
            var child = parent.Find(path);
            if (child == null)
            {
                Debug.LogWarning($"[HudArtSkin] '{parent.name}/{path}' 를 찾지 못해 해당 스킨을 건너뜁니다 — HudBuilder 계층 이름과 다릅니다.");
            }
            return child;
        }

        private static Text FindText(Transform parent, string path)
        {
            var child = FindRequired(parent, path);
            if (child == null) return null;
            if (!child.TryGetComponent(out Text text))
            {
                Debug.LogWarning($"[HudArtSkin] '{parent.name}/{path}' 에 Text가 없어 글자 스킨을 건너뜁니다.");
            }
            return text;
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

        private static void SetRect(Transform target, Vector2 anchor, Vector2 position, Vector2 size)
        {
            if (target is not RectTransform rect) return;
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
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

        private static void CopyRect(Transform target, Transform source)
        {
            if (target is not RectTransform rect || source is not RectTransform from) return;
            rect.anchorMin = from.anchorMin;
            rect.anchorMax = from.anchorMax;
            rect.pivot = from.pivot;
            rect.anchoredPosition = from.anchoredPosition;
            rect.sizeDelta = from.sizeDelta;
        }

        /// <summary>장식을 텍스트 바로 앞 형제로 — 아이콘·게이지 위, 글자 아래.</summary>
        private static void PlaceBefore(Transform decoration, Transform text)
        {
            if (text == null)
            {
                decoration.SetAsLastSibling();
                return;
            }

            int index = text.GetSiblingIndex();
            if (decoration.GetSiblingIndex() < index) index--; // 자신이 빠지면 텍스트가 한 칸 당겨진다
            decoration.SetSiblingIndex(index);
        }

        private static void ApplyTextStyle(Text text, int fontSize)
        {
            if (text == null) return;
            text.fontSize = fontSize;
            text.resizeTextForBestFit = false; // 본문 자동 축소 금지(레이아웃 스펙)
            text.raycastTarget = false;
        }
    }
}
