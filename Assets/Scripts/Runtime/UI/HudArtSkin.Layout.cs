using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Abyss.Runtime.UI
{
    /// <summary>
    /// HUD 요소별 배치. 기준 캔버스 1920×1080(CanvasScaler 기준 해상도), 외곽 안전 여백 48.
    /// 근거: Art_Source/ui_concept_20261006/layout-spec.md 「화면 배치」·「공통 좌표·가독성」.
    /// 레이아웃 스펙의 좌상단 원점 좌표를 각 코너 앵커 기준 anchoredPosition으로 옮겼다.
    /// </summary>
    public static partial class HudArtSkin
    {
        private static readonly Vector2 TopLeft = new(0f, 1f);
        private static readonly Vector2 TopRight = new(1f, 1f);
        private static readonly Vector2 BottomLeft = new(0f, 0f);
        private static readonly Vector2 TopCenter = new(0.5f, 1f);
        private static readonly Vector2 BottomCenter = new(0.5f, 0f);

        // HP: 좌상 48/40, 360×44.
        private static readonly Vector2 HealthBarPosition = new(48f, -40f);
        private static readonly Vector2 HealthBarSize = new(360f, 44f);
        private const float HEALTH_FRAME_CORNER = 20f;

        // 폼: 좌상 48/96, 224×88. 현재 88, 대기 64(세로 중앙, 간격 16).
        private static readonly Vector2 FormSlotPosition = new(48f, -96f);
        private static readonly Vector2 FormSlotSize = new(224f, 88f);
        private static readonly Vector2 CurrentFormPosition = Vector2.zero;
        private static readonly Vector2 CurrentFormSize = new(88f, 88f);
        private static readonly Vector2 OtherFormPosition = new(104f, -12f);
        private static readonly Vector2 OtherFormSize = new(64f, 64f);
        private const float FORM_FRAME_CORNER = 16f;

        // 스킬 2칸: 좌하 48/48 · 160/48, 96×100. 아이콘 80은 위에 붙이고 이름 24는 아래 띠에 둔다.
        // 이름 폭 112 = 슬롯 96 + 간격 16 — 두 칸 이름이 서로 닿기 전까지 쓸 수 있는 최대 폭.
        // 이름 높이 56 = 24pt 두 줄. 슬롯 바닥(화면 48)에서 -44 → 이름 아래 끝이 화면 바닥 위 4에 멈춘다(안전 여백 4).
        // 긴 이름은 두 줄에서 잘린다(VerticalTruncate) — 원문은 상세 설명에서 본다.
        private static readonly Vector2 SkillSlot0Position = new(48f, 48f);
        private static readonly Vector2 SkillSlot1Position = new(160f, 48f);
        private static readonly Vector2 SkillSlotSize = new(96f, 100f);
        private static readonly Vector2 SkillIconSize = new(80f, 80f);
        private static readonly Vector2 SkillLabelPosition = new(0f, -44f);
        private static readonly Vector2 SkillLabelSize = new(112f, 56f);
        private const float SKILL_FRAME_CORNER = 16f;

        // 폼 아래 좌상 세로줄: 편향 경고(폼 끝 184 + 16) → 시너지(+56). 중앙 x=480 앞에서 끝난다.
        private static readonly Vector2 FormBiasPosition = new(48f, -200f);
        private static readonly Vector2 FormBiasSize = new(360f, 48f);
        // 시너지: 2열 격자(축 최대 6개 → 3줄). 한 줄 가로 배치는 6×148 ≈ 900으로 중앙 전투 공간을 침범한다.
        // 폭 432 = 212×2 + 8(A2: 칩 왼쪽 축 아이콘 칸 38을 더해 172 → 212), 높이 136 = 40×3 + 8×2.
        // 48 + 432 = 480 — 중앙 x=480 앞에서 끝난다.
        private static readonly Vector2 SynergyPosition = new(48f, -256f);
        private static readonly Vector2 SynergySize = new(432f, 136f);
        private const float SYNERGY_CHIP_WIDTH = 212f;
        private const float SYNERGY_CHIP_HEIGHT = 40f;
        private const float SYNERGY_CHIP_SPACING = 8f;
        private const int SYNERGY_COLUMNS = 2;

        // 연소 스택: 스킬 두 칸 오른쪽(160 + 96 + 16), 슬롯 높이 100의 세로 중앙.
        private static readonly Vector2 BurnStackPosition = new(272f, 80f);
        private static readonly Vector2 BurnStackSize = new(220f, 36f);

        // 통화: 우상 -48/-48, 232×48.
        private static readonly Vector2 GoldCounterPosition = new(-48f, -48f);
        private static readonly Vector2 GoldCounterSize = new(232f, 48f);
        private const float GOLD_FRAME_CORNER = 16f;

        private const int MIN_FONT_SIZE = 24;
        private const int HP_FONT_SIZE = 28;
        private const int GOLD_FONT_SIZE = 28;

        // ==================== HP ====================
        private static void ApplyHealthBar(Transform hudRoot, float referencePpu)
        {
            var root = FindRequired(hudRoot, "HealthBar");
            if (root == null) return;
            SetRect(root, TopLeft, HealthBarPosition, HealthBarSize);

            var background = FindRequired(root, "Background");
            if (background != null)
            {
                Stretch(background, OuterInset(HEALTH_FRAME_CORNER));
                if (background.TryGetComponent(out Image backgroundImage)) backgroundImage.color = PanelColor;
            }

            // Fill은 Filled 설정(Horizontal·fillAmount)을 그대로 두고 범위·색만 바꾼다. 프레임 스프라이트를 넣지 않는다.
            var fill = FindRequired(root, "Fill");
            if (fill != null)
            {
                Stretch(fill, InnerInset(HEALTH_FRAME_CORNER));
                if (fill.TryGetComponent(out Image fillImage)) fillImage.color = HpColor;
            }

            var hpText = FindText(root, "HPText");
            ApplyTextStyle(hpText, HP_FONT_SIZE);
            if (hpText != null) hpText.color = BodyTextColor;

            // A2 전용 체력 프레임이 있으면 그것을 쓰고, 없으면 아래 석판 프레임(기존).
            if (TryApplyHealthArt(root, background, fill, hpText)) return;
            var frame = EnsureFrame(root, FRAME_NAME, HEALTH_FRAME_CORNER, referencePpu);
            if (frame != null)
            {
                Stretch(frame.transform, 0f);
                PlaceBefore(frame.transform, hpText != null ? hpText.transform : null);
            }
        }

        // ==================== 폼 ====================
        private static void ApplyFormSlot(Transform hudRoot, float referencePpu)
        {
            var root = FindRequired(hudRoot, "FormSlot");
            if (root == null) return;
            SetRect(root, TopLeft, FormSlotPosition, FormSlotSize);

            var current = FindRequired(root, "CurrentFormIcon");
            var other = FindRequired(root, "OtherFormIcon");
            if (current != null) ApplyFormIcon(current, CurrentFormPosition, CurrentFormSize);
            if (other != null) ApplyFormIcon(other, OtherFormPosition, OtherFormSize);

            // 교체 쿨다운(Radial360·Top·시계방향)은 현재 폼 위에 겹친다 — 설정은 두고 크기만 맞춘다.
            var cooldown = FindRequired(root, "CooldownFill");
            if (cooldown != null) SetRect(cooldown, TopLeft, CurrentFormPosition, CurrentFormSize);

            // 백플레이트는 맨 뒤(아이콘이 비활성인 빈 칸에도 자리가 보이게), 프레임은 게이지 위 맨 앞. 이 슬롯엔 텍스트가 없다.
            if (current != null) ApplyFormDecoration(root, "Current", current, referencePpu);
            if (other != null) ApplyFormDecoration(root, "Other", other, referencePpu);
        }

        private static void ApplyFormIcon(Transform icon, Vector2 position, Vector2 size)
        {
            SetRect(icon, TopLeft, position, size);
            // 회색 생성 tint를 걷어 폼 아이콘을 원색으로. 스프라이트가 없으면 FormSlotPresenter가 Image를 끈다.
            if (icon.TryGetComponent(out Image image))
            {
                image.color = Color.white;
                image.raycastTarget = false;
            }
        }

        private static void ApplyFormDecoration(Transform root, string prefix, Transform icon, float referencePpu)
        {
            var backplate = GetOrCreateImage(root, prefix + BACKPLATE_NAME);
            CopyRect(backplate.transform, icon);
            InsetRect(backplate.rectTransform, OuterInset(FORM_FRAME_CORNER));
            backplate.color = PanelColor;
            backplate.transform.SetAsFirstSibling();

            if (TryApplyFormArt(root, prefix, icon)) return;
            var frame = EnsureFrame(root, prefix + FRAME_NAME, FORM_FRAME_CORNER, referencePpu);
            if (frame == null) return;
            CopyRect(frame.transform, icon);
            frame.transform.SetAsLastSibling();
        }

        // ==================== 스킬 ====================
        private static void ApplySkillSlot(Transform hudRoot, string slotName, Vector2 position, float referencePpu)
        {
            var root = FindRequired(hudRoot, slotName);
            if (root == null) return;
            SetRect(root, BottomLeft, position, SkillSlotSize);

            var backplate = GetOrCreateImage(root, BACKPLATE_NAME);
            Stretch(backplate.transform, OuterInset(SKILL_FRAME_CORNER));
            backplate.color = PanelColor;
            backplate.transform.SetAsFirstSibling();

            var icon = FindRequired(root, "Icon");
            if (icon != null)
            {
                SetRect(icon, TopCenter, Vector2.zero, SkillIconSize);
                if (icon.TryGetComponent(out Image iconImage))
                {
                    iconImage.raycastTarget = false;
                    SyncSkillIconTint(iconImage);
                }
            }

            // 쿨다운(Vertical) 오버레이는 아이콘 위에만 — 이름 띠까지 덮지 않게 아이콘과 같은 칸으로.
            var cooldown = FindRequired(root, "CooldownFill");
            if (cooldown != null) SetRect(cooldown, TopCenter, Vector2.zero, SkillIconSize);

            var label = FindText(root, "Label");
            if (label != null)
            {
                SetRect(label.transform, BottomCenter, SkillLabelPosition, SkillLabelSize);
                ApplyTextStyle(label, MIN_FONT_SIZE);
                label.alignment = TextAnchor.MiddleCenter;
                label.horizontalOverflow = HorizontalWrapMode.Wrap;
                label.verticalOverflow = VerticalWrapMode.Truncate;
                label.color = BodyTextColor;
            }

            if (TryApplySkillArt(root, icon, label)) return;
            var frame = EnsureFrame(root, FRAME_NAME, SKILL_FRAME_CORNER, referencePpu);
            if (frame != null)
            {
                Stretch(frame.transform, 0f);
                PlaceBefore(frame.transform, label != null ? label.transform : null);
            }
        }

        // ==================== 좌상 세로줄 · 연소 ====================
        // 세 Presenter 모두 루트는 항상 활성, Body만 토글하는 규약이다 — 활성 상태는 건드리지 않는다.
        private static void ApplyFormBiasWarning(Transform hudRoot)
        {
            var root = FindRequired(hudRoot, "FormBiasWarning");
            if (root == null) return;
            SetRect(root, TopLeft, FormBiasPosition, FormBiasSize);
            ApplyTextStyle(FindText(root, "Body/Label"), MIN_FONT_SIZE);
        }

        private static void ApplySynergyCounter(Transform hudRoot)
        {
            var root = FindRequired(hudRoot, "SynergyCounter");
            if (root == null) return;
            SetRect(root, TopLeft, SynergyPosition, SynergySize);

            // Body는 루트에 늘어붙어 있다(HudBuilder Stretch) — 루트 크기가 곧 격자 영역.
            var body = FindRequired(root, "Body");
            if (body == null) return;
            ApplySynergyGrid(body);

            // 템플릿은 이후 복제될 칩, 나머지 자식은 OnEnable Refresh에서 이미 복제된 칩 — 둘 다 같은 스타일로.
            if (FindRequired(body, "ChipTemplate") == null) return;
            for (int i = 0; i < body.childCount; i++)
            {
                var chip = body.GetChild(i);
                if (chip.TryGetComponent(out LayoutElement element))
                {
                    element.preferredWidth = SYNERGY_CHIP_WIDTH;
                    element.preferredHeight = SYNERGY_CHIP_HEIGHT;
                }
                var label = chip.GetComponentInChildren<Text>(true);
                if (label != null) ApplyTextStyle(label, MIN_FONT_SIZE);
            }
        }

        /// <summary>
        /// 빌더의 HorizontalLayoutGroup을 2열 GridLayoutGroup으로 교체한다. 두 번째 적용이면 기존 격자를 재사용.
        /// LayoutGroup은 한 오브젝트에 하나만 허용(DisallowMultipleComponent)되고 Destroy는 프레임 끝까지 지연되므로,
        /// 기존 그룹을 끄고 Destroy한 뒤 한 프레임 기다려 Grid를 붙인다 — 같은 프레임에 두 그룹이 공존하지 않는다.
        /// </summary>
        private static void ApplySynergyGrid(Transform body)
        {
            if (body.TryGetComponent(out GridLayoutGroup grid))
            {
                ConfigureSynergyGrid(grid);
                return;
            }

            if (body.TryGetComponent(out HorizontalLayoutGroup horizontal))
            {
                horizontal.enabled = false;
                Object.Destroy(horizontal);
            }
            _ = AttachSynergyGridNextFrameAsync(body);
        }

        // 대기 중인 Body — 같은 Body에 대한 재호출이 Grid 추가를 두 번 예약하지 않게 막는다.
        private static readonly HashSet<EntityId> PendingSynergyGridBodies = new();

        private static async Awaitable AttachSynergyGridNextFrameAsync(Transform body)
        {
            var bodyId = body.GetEntityId();
            if (!PendingSynergyGridBodies.Add(bodyId)) return;
            try
            {
                await Awaitable.NextFrameAsync();

                // 대기 중 HUD가 파괴(씬 전환 등)됐으면 할 일이 없다. 비활성 Body에도 컴포넌트 추가는 안전하다.
                if (body == null) return;
                if (!body.TryGetComponent(out GridLayoutGroup grid))
                {
                    if (body.TryGetComponent(out HorizontalLayoutGroup _))
                    {
                        Debug.LogWarning($"[HudArtSkin] '{body.name}' 의 HorizontalLayoutGroup이 아직 남아 있어 시너지 격자 스킨을 건너뜁니다.");
                        return;
                    }
                    grid = body.gameObject.AddComponent<GridLayoutGroup>();
                    if (grid == null)
                    {
                        Debug.LogWarning($"[HudArtSkin] '{body.name}' 에 GridLayoutGroup을 붙이지 못해 시너지 격자 스킨을 건너뜁니다 — 다른 LayoutGroup이 남아 있습니다.");
                        return;
                    }
                }
                ConfigureSynergyGrid(grid);
            }
            catch (System.Exception exception)
            {
                // 버려진(discard) Awaitable의 예외는 관찰되지 않으므로 여기서 남긴다.
                Debug.LogException(exception);
            }
            finally
            {
                PendingSynergyGridBodies.Remove(bodyId);
            }
        }

        private static void ConfigureSynergyGrid(GridLayoutGroup grid)
        {
            grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
            grid.startAxis = GridLayoutGroup.Axis.Horizontal;
            grid.childAlignment = TextAnchor.UpperLeft;
            grid.cellSize = new Vector2(SYNERGY_CHIP_WIDTH, SYNERGY_CHIP_HEIGHT);
            grid.spacing = new Vector2(SYNERGY_CHIP_SPACING, SYNERGY_CHIP_SPACING);
            grid.padding = new RectOffset(0, 0, 0, 0);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = SYNERGY_COLUMNS;
        }

        private static void ApplyBurnStack(Transform hudRoot)
        {
            var root = FindRequired(hudRoot, "BurnStack");
            if (root == null) return;
            SetRect(root, BottomLeft, BurnStackPosition, BurnStackSize);
            ApplyTextStyle(FindText(root, "Body/Label"), MIN_FONT_SIZE);
        }

        // ==================== 통화 ====================
        private static void ApplyGoldCounter(Transform hudRoot, float referencePpu)
        {
            var root = FindRequired(hudRoot, "GoldCounter");
            if (root == null) return;
            SetRect(root, TopRight, GoldCounterPosition, GoldCounterSize);

            var background = FindRequired(root, "Background");
            if (background != null)
            {
                Stretch(background, OuterInset(GOLD_FRAME_CORNER));
                if (background.TryGetComponent(out Image backgroundImage)) backgroundImage.color = PanelColor;
            }

            // 글자 색은 GoldCounterPresenter가 증감 강조로 관리하므로 크기·여백만 바꾼다.
            var label = FindText(root, "Label");
            ApplyTextStyle(label, GOLD_FONT_SIZE);
            if (label != null)
            {
                var labelRect = label.rectTransform;
                float padding = InnerInset(GOLD_FRAME_CORNER) + 4f;
                labelRect.offsetMin = new Vector2(padding, 0f);
                labelRect.offsetMax = new Vector2(-padding, 0f);
            }

            if (TryApplyCurrencyArt(root, background, label)) return;
            var frame = EnsureFrame(root, FRAME_NAME, GOLD_FRAME_CORNER, referencePpu);
            if (frame != null)
            {
                Stretch(frame.transform, 0f);
                PlaceBefore(frame.transform, label != null ? label.transform : null);
            }
        }

        private static void InsetRect(RectTransform rect, float inset)
        {
            rect.sizeDelta -= new Vector2(inset * 2f, inset * 2f);
            // 앵커·피벗이 좌상이므로 줄인 만큼 안쪽으로 옮겨 중심을 유지한다.
            rect.anchoredPosition += new Vector2(inset, -inset);
        }
    }
}
