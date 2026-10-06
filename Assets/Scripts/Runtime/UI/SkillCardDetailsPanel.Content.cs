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
            headerText.text = $"{skill.displayName} — {Loc.Get(StringKey.DraftDetail_Title)}";
            bodyText.text = BuildBody(skill);
        }

        private static string BuildBody(SkillData skill)
        {
            var lines = new List<string>();

            if (!string.IsNullOrEmpty(skill.description))
                lines.Add(Loc.GetFormat(StringKey.DraftDetail_DescriptionFormat, skill.description));
            if (!string.IsNullOrEmpty(skill.formulaDescription))
                lines.Add(Loc.GetFormat(StringKey.Codex_Skill_FormulaFormat, skill.formulaDescription));

            lines.Add(Loc.GetFormat(StringKey.DraftDetail_FlatBonusFormat, FormatSigned(skill.flatBonus)));
            lines.Add(Loc.GetFormat(StringKey.DraftDetail_MultiplierFormat, FormatNumber(skill.multiplier)));
            lines.Add(FormLine(skill));
            lines.Add(Loc.GetFormat(StringKey.DraftDetail_SynergyFormat, SynergyAxis.GetDisplayName(skill.synergyTag)));

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
