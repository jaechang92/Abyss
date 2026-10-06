using System.Collections.Generic;
using System.Globalization;
using Abyss.Runtime.Draft;
using Abyss.Runtime.Form;
using Abyss.Runtime.Localization;
using UnityEngine;
using UnityEngine.UI;

namespace Abyss.Runtime.UI
{
    /// <summary>
    /// 상세 패널의 본체 조립과 문구. 표시 값은 <see cref="SkillData"/>의 기존 필드 그대로다 —
    /// 강화 단계·강화 전후 비교·예상 피해는 데이터에 없으므로 만들어 내지 않는다.
    /// 스킬 이름·설명·공식 문자열은 에셋에 적힌 원문을 그대로 쓰고, 라벨만 현재 언어를 따른다.
    /// </summary>
    public sealed partial class SkillCardDetailsPanel
    {
        private const int HEADER_FONT_SIZE = 19;
        private const int BODY_FONT_SIZE = 17;
        private const int BODY_MIN_FONT_SIZE = 11;

        private static readonly Color PanelColor = new Color(0.06f, 0.06f, 0.09f, 0.94f);
        private static readonly Color HeaderColor = new Color(1f, 0.86f, 0.55f);
        private static readonly Color BodyColor = new Color(0.92f, 0.92f, 0.95f);

        // 도킹 판(드래프트 공통 상세 석판). 글자는 레이아웃 스펙의 보조 24 / 제목급 30.
        private const int DOCK_HEADER_FONT_SIZE = 30;
        private const int DOCK_BODY_FONT_SIZE = 24;
        private const float DOCK_PADDING = 20f;
        private const float DOCK_ICON_SIZE = 120f;
        private const float DOCK_ICON_GAP = 28f;
        private const float DOCK_ICON_INSET = 4f;
        private const string STAT_SEPARATOR = "   ·   ";

        private Image dockIconFrame;
        private Image dockIcon;

        /// <summary>
        /// 카드 옆에 뜨는 대신 slot 위에 겹쳐 고정 표시한다(드래프트 시안의 공통 상세). 면·테두리는 slot이 그리므로
        /// 본체 바탕을 끄고, 왼쪽에 아이콘 칸을 만든다. 표시 대상 규칙(호버 → 포커스 → 숨김)은 그대로다.
        /// </summary>
        public void DockTo(RectTransform slot)
        {
            dockSlot = slot;
            if (body == null || slot == null) return;

            if (body.TryGetComponent(out Image background)) background.enabled = false;

            headerText.fontSize = DOCK_HEADER_FONT_SIZE;
            headerText.color = ModalArtSkin.BodyTextColor;
            bodyText.fontSize = DOCK_BODY_FONT_SIZE;
            bodyText.color = ModalArtSkin.SubTextColor;
            bodyText.resizeTextMaxSize = DOCK_BODY_FONT_SIZE;

            if (dockIconFrame == null)
            {
                var topLeft = new Vector2(0f, 1f);
                var frameGo = UiFactory.CreateRect(body.transform, "IconFrame", topLeft, topLeft, topLeft, Vector2.zero, Vector2.zero);
                dockIconFrame = frameGo.AddComponent<Image>();
                dockIconFrame.color = ModalArtSkin.PanelColor;
                dockIconFrame.raycastTarget = false;

                var iconGo = UiFactory.CreateRect(frameGo.transform, "Icon", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
                var iconRect = (RectTransform)iconGo.transform;
                iconRect.offsetMin = new Vector2(DOCK_ICON_INSET, DOCK_ICON_INSET);
                iconRect.offsetMax = new Vector2(-DOCK_ICON_INSET, -DOCK_ICON_INSET);
                dockIcon = iconGo.AddComponent<Image>();
                dockIcon.preserveAspect = true;
                dockIcon.raycastTarget = false;
                dockIcon.enabled = false;
            }

            shownSkill = null;
            isLayoutDirty = true;
        }

        private void BuildView()
        {
            var center = new Vector2(0.5f, 0.5f);
            body = UiFactory.CreateRect(transform, "Body", center, center, new Vector2(0f, 1f), Vector2.zero, new Vector2(PANEL_WIDTH, MIN_PANEL_HEIGHT));

            var background = body.AddComponent<Image>();
            background.color = PanelColor;
            background.raycastTarget = false;

            // 그래픽마다 raycastTarget을 끈 것에 더해, 자식이 늘어도 입력을 막지 않게 묶어 둔다.
            var group = body.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;

            headerText = CreateText(body.transform, "Header", HEADER_FONT_SIZE, HeaderColor);
            bodyText = CreateText(body.transform, "Text", BODY_FONT_SIZE, BodyColor);
            bodyText.resizeTextMinSize = BODY_MIN_FONT_SIZE;
            bodyText.resizeTextMaxSize = BODY_FONT_SIZE;

            body.SetActive(false);
        }

        private static Text CreateText(Transform parent, string name, int fontSize, Color color)
        {
            var topLeft = new Vector2(0f, 1f);
            var go = UiFactory.CreateRect(parent, name, topLeft, topLeft, topLeft, Vector2.zero, Vector2.zero);

            var text = go.AddComponent<Text>();
            UiFactory.ApplyFont(text);
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = TextAnchor.UpperLeft;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            // 설명 원문에 '<'가 섞여도 태그로 먹히지 않게 한다.
            text.supportRichText = false;
            text.raycastTarget = false;
            return text;
        }

        private void RebuildContent(SkillData skill)
        {
            if (skill == null) return;
            bool isDocked = dockSlot != null;

            // 도킹 판은 시안처럼 이름만 제목으로 둔다(왼쪽 아이콘과 같은 줄에서 이미 상세 자리라 「상세」 꼬리가 필요 없다).
            headerText.text = isDocked ? skill.displayName : $"{skill.displayName} — {Loc.Get(StringKey.DraftDetail_Title)}";
            bodyText.text = BuildBody(skill, isDocked);

            if (dockIcon != null)
            {
                dockIcon.sprite = skill.icon;
                dockIcon.enabled = skill.icon != null;
            }
        }

        /// <summary>isCompact: 수치·폼·시너지 네 줄을 한 줄로 잇는다(도킹 석판 높이가 고정이라). 값·문구는 같다.</summary>
        private static string BuildBody(SkillData skill, bool isCompact)
        {
            var lines = new List<string>();

            if (!string.IsNullOrEmpty(skill.description))
                lines.Add(Loc.GetFormat(StringKey.DraftDetail_DescriptionFormat, skill.description));
            if (!string.IsNullOrEmpty(skill.formulaDescription))
                lines.Add(Loc.GetFormat(StringKey.Codex_Skill_FormulaFormat, skill.formulaDescription));

            var stats = new[]
            {
                Loc.GetFormat(StringKey.DraftDetail_FlatBonusFormat, FormatSigned(skill.flatBonus)),
                Loc.GetFormat(StringKey.DraftDetail_MultiplierFormat, FormatNumber(skill.multiplier)),
                FormLine(skill),
                Loc.GetFormat(StringKey.DraftDetail_SynergyFormat, SynergyAxis.GetDisplayName(skill.synergyTag)),
            };
            if (isCompact) lines.Add(string.Join(STAT_SEPARATOR, stats));
            else lines.AddRange(stats);

            return string.Join("\n", lines);
        }

        // 도감 스킬 상세(CodexPanel.SkillStats)와 같은 규약: 카탈로그의 표시 이름, 못 찾으면 id 그대로.
        private static string FormLine(SkillData skill)
        {
            if (string.IsNullOrEmpty(skill.formBound)) return Loc.Get(StringKey.Codex_Skill_FormShared);

            var form = FormCatalog.GetById(skill.formBound);
            string formName = form != null ? form.LocalizedName : skill.formBound;
            return Loc.GetFormat(StringKey.Codex_Skill_FormBoundFormat, formName);
        }

        private static string FormatNumber(float value) => value.ToString("0.##", CultureInfo.InvariantCulture);

        private static string FormatSigned(float value) => value.ToString("+0.##;-0.##;0", CultureInfo.InvariantCulture);
    }
}
