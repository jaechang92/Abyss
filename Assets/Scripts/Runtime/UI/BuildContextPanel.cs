using System.Collections.Generic;
using System.Text;
using Abyss.Runtime.Draft;
using UnityEngine;
using UnityEngine.UI;

namespace Abyss.Runtime.UI
{
    /// <summary>
    /// 드래프트 UX F3 대응 — 현재 보유 빌드를 항상 노출.
    /// 보유 스킬을 시너지 태그별로 집계해 리스트 표시.
    /// </summary>
    public sealed class BuildContextPanel : MonoBehaviour
    {
        [SerializeField] private Text titleText;
        [SerializeField] private Text synergyCountsText;
        [SerializeField] private Text skillListText;

        private readonly StringBuilder stringBuilder = new();
        private readonly Dictionary<string, int> synergyCounts = new();

        public void Refresh(IReadOnlyList<SkillData> owned)
        {
            if (titleText != null) titleText.text = "현재 빌드";

            if (owned == null || owned.Count == 0)
            {
                if (synergyCountsText != null) synergyCountsText.text = "시너지: —";
                if (skillListText != null) skillListText.text = "보유 스킬 없음";
                return;
            }

            // 집계·표시명은 SynergyAxis(SoT)를 따른다 — HUD 시너지 카운터와 같은 규약.
            SynergyAxis.Tally(owned, synergyCounts);

            stringBuilder.Clear();
            stringBuilder.Append("시너지: ");
            bool first = true;
            foreach (var kv in synergyCounts)
            {
                if (!first) stringBuilder.Append(" | ");
                stringBuilder.Append($"[{SynergyAxis.GetDisplayName(kv.Key)}] {kv.Value}");
                if (SynergyAxis.IsActivated(kv.Value)) stringBuilder.Append(" ✦");
                first = false;
            }
            if (first) stringBuilder.Append("—");
            if (synergyCountsText != null) synergyCountsText.text = stringBuilder.ToString();

            stringBuilder.Clear();
            int activeCount = 0;
            for (int i = 0; i < owned.Count; i++)
            {
                var skill = owned[i];
                if (skill == null) continue;
                if (skill.category == SkillCategory.Active) activeCount += 1;
                stringBuilder.Append($"· {skill.displayName} ({skill.category})\n");
            }
            stringBuilder.Append($"\n총 {owned.Count}개 (Active {activeCount}/2)");
            if (skillListText != null) skillListText.text = stringBuilder.ToString();
        }
    }
}
