using Abyss.Runtime.Draft;
using Abyss.Runtime.Form;
using Abyss.Runtime.Localization;
using Abyss.Runtime.Meta;
using UnityEngine;
using UnityEngine.UI;

namespace Abyss.Runtime.Lobby
{
    /// <summary>
    /// 제단 패널의 행 문구(해금 항목)와 <b>행 수에서 파생한 패널 크기</b>(2026-10-06 메타 해금 1단계).
    ///
    /// 빌더(LobbySceneBuilder.Altar)는 Box 860×600 · 행 컨테이너 높이 360으로 고정해 두었는데, 행 4개(4×88=352)로 이미 꽉 찼다.
    /// 해금 3행이 더해지면 7×88=616이 되어 <b>오류 없이 화면 밖으로 잘린다</b>(상점·도감에서 세 번 겪은 종류).
    /// 그래서 크기를 「몇 행이 될 수 있는가」에서 계산한다. 4행일 때는 지금 화면과 같다(Box 600 · 닫기 y −250).
    /// </summary>
    public sealed partial class MetaUpgradePanel
    {
        private const float CLOSE_GAP = 16f;
        private const float BOTTOM_MARGIN = 18f;
        private const float SCREEN_MARGIN = 48f;
        private const float FALLBACK_SCREEN_HEIGHT = 1080f;
        /// <summary>행 내용(이름 위 · 설명 아래)이 들어가는 최소 행 간격. 이보다 줄면 글자가 겹친다.</summary>
        private const float MIN_ROW_HEIGHT = 84f;

        /// <summary>
        /// 행 컨테이너·Box·닫기 버튼을 행 수에 맞춘다. 화면에 다 안 들어가면 행 간격을 최소치까지 줄인다
        /// (그래도 넘치면 아래가 잘린다 — 그때는 스크롤이나 탭이 필요하다는 신호로 경고를 남긴다).
        /// </summary>
        private void FitPanelToRows(int count)
        {
            if (rowContainer == null || count <= 0) return;

            var box = rowContainer.parent as RectTransform;
            float closeHeight = closeButton != null ? ((RectTransform)closeButton.transform).sizeDelta.y : 64f;
            float fixedHeight = -rowContainer.anchoredPosition.y + CLOSE_GAP + closeHeight + BOTTOM_MARGIN;

            float availableRows = ResolveScreenHeight(box) - SCREEN_MARGIN * 2f - fixedHeight;
            if (count * rowHeight > availableRows)
            {
                rowHeight = Mathf.Max(MIN_ROW_HEIGHT, availableRows / count);
                if (count * rowHeight > availableRows)
                {
                    Debug.LogWarning($"[MetaUpgradePanel] 제단 항목 {count}개가 화면 높이를 넘는다 — 아래 행이 잘린다. 스크롤·탭이 필요하다.");
                }
            }

            float rowsHeight = count * rowHeight;
            rowContainer.sizeDelta = new Vector2(rowContainer.sizeDelta.x, rowsHeight);
            if (box == null) return;

            float boxHeight = Mathf.Max(box.sizeDelta.y, fixedHeight + rowsHeight);
            box.sizeDelta = new Vector2(box.sizeDelta.x, boxHeight);

            // 닫기 버튼은 Box 중앙 앵커(빌더 CreateButton) — 바닥에서 여백만큼 위로 둔다.
            if (closeButton != null)
            {
                var closeRect = (RectTransform)closeButton.transform;
                closeRect.anchoredPosition = new Vector2(closeRect.anchoredPosition.x,
                    -boxHeight * 0.5f + BOTTOM_MARGIN + closeHeight * 0.5f);
            }
        }

        private static float ResolveScreenHeight(RectTransform box)
        {
            var canvas = box != null ? box.GetComponentInParent<Canvas>(true) : null;
            if (canvas == null) return FALLBACK_SCREEN_HEIGHT;
            float height = ((RectTransform)canvas.rootCanvas.transform).rect.height;
            return height > 0f ? height : FALLBACK_SCREEN_HEIGHT;
        }

        /// <summary>칸을 넘는 문구는 자르지 않고 줄인다. 칸에 맞으면 원래 크기 그대로다.</summary>
        private static void FitText(Text text, int minSize)
        {
            if (text == null) return;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.resizeTextForBestFit = true;
            text.resizeTextMaxSize = text.fontSize;
            text.resizeTextMinSize = Mathf.Min(minSize, text.fontSize);
        }

        private static string ResolveRowName(MetaUpgradeData data)
        {
            switch (data.type)
            {
                case MetaUpgradeType.UnlockSkill:
                {
                    var skill = FindSkill(data.unlockTargetId);
                    return Loc.GetFormat(StringKey.Altar_UnlockSkillNameFormat,
                        skill != null ? skill.displayName : data.unlockTargetId);
                }
                case MetaUpgradeType.UnlockForm:
                {
                    var form = FormCatalog.GetById(data.unlockTargetId);
                    return Loc.GetFormat(StringKey.Altar_UnlockFormNameFormat,
                        form != null ? form.LocalizedName : data.unlockTargetId);
                }
                default:
                    return Loc.Get(data.nameKey);
            }
        }

        private static string ResolveRowDescription(MetaUpgradeData data)
        {
            switch (data.type)
            {
                case MetaUpgradeType.UnlockSkill:
                {
                    var skill = FindSkill(data.unlockTargetId);
                    return skill != null ? skill.description : string.Empty;
                }
                case MetaUpgradeType.UnlockForm:
                {
                    var form = FormCatalog.GetById(data.unlockTargetId);
                    return form != null ? form.description : string.Empty;
                }
                default:
                    return Loc.Get(data.descKey);
            }
        }

        private static SkillData FindSkill(string skillId)
        {
            if (string.IsNullOrEmpty(skillId)) return null;
            var all = SkillCatalog.All;
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i].skillId == skillId) return all[i];
            }
            return null;
        }
    }
}
