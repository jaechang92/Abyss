using System.Collections.Generic;
using UnityEngine;

namespace Abyss.Runtime.Draft
{
    /// <summary>
    /// 시너지 축 표시 규약의 단일 기준점(SoT).
    /// 축 태그 문자열(SkillData.synergyTag)을 표시명·색상으로 해석한다.
    ///
    /// 이 클래스가 생기기 전에는 SkillCardView·BuildContextPanel이 각자 raw 태그를 그대로 찍어
    /// 플레이어에게 "fire"·"abyss" 같은 내부 ID가 노출됐다. 표시 규약이 여러 곳에 파편화되면
    /// 축을 추가할 때마다 누락이 생기므로 여기로 모은다.
    ///
    /// 미등록 태그도 안전하게 다룬다(원문 표시 + 중립 색) — 데이터가 앞서가고 코드가 따라오는
    /// 콘텐츠 파이프라인 특성상, 축을 등록하지 않았다는 이유로 UI가 비어서는 안 된다.
    /// </summary>
    public static class SynergyAxis
    {
        /// <summary>
        /// 시너지 카테고리 스킬이 발동하는 동일 축 보유 개수(03-skill-draft-system.md §4).
        /// 예: "[불꽃] 2개+ 시 적 사망 폭발".
        /// </summary>
        public const int ACTIVATION_THRESHOLD = 2;

        /// <summary>축 태그가 없는 스킬(무축 보편)의 표시명.</summary>
        public const string NEUTRAL_LABEL = "일반";

        /// <summary>미등록·무축 태그의 표시 색.</summary>
        private static readonly Color NEUTRAL_COLOR = new(0.72f, 0.74f, 0.78f);

        /// <summary>축 하나의 표시 정보.</summary>
        public readonly struct AxisInfo
        {
            public readonly string Id;
            public readonly string DisplayName;
            public readonly Color Color;

            public AxisInfo(string id, string displayName, Color color)
            {
                Id = id;
                DisplayName = displayName;
                Color = color;
            }
        }

        /// <summary>
        /// 등록된 축 목록. 키는 SkillData.synergyTag에 실제로 들어가는 값과 일치해야 한다.
        /// (fire/abyss/guard는 현재 스킬 에셋 15종이 쓰는 값. blood_pact/frost/soul은 로드맵 예약분.)
        /// </summary>
        private static readonly Dictionary<string, AxisInfo> registry = new()
        {
            ["fire"] = new AxisInfo("fire", "불꽃", new Color(1f, 0.46f, 0.18f)),
            ["abyss"] = new AxisInfo("abyss", "심연", new Color(0.64f, 0.42f, 0.96f)),
            ["guard"] = new AxisInfo("guard", "수호", new Color(1f, 0.80f, 0.32f)),
            ["blood_pact"] = new AxisInfo("blood_pact", "피의 서약", new Color(0.90f, 0.22f, 0.30f)),
            ["frost"] = new AxisInfo("frost", "빙결", new Color(0.45f, 0.80f, 1f)),
            ["soul"] = new AxisInfo("soul", "영혼", new Color(0.55f, 0.95f, 0.70f)),
        };

        /// <summary>등록된 축인지. 미등록이어도 표시는 가능하지만 색·한글명은 폴백을 쓴다.</summary>
        public static bool IsRegistered(string tag)
        {
            return !string.IsNullOrEmpty(tag) && registry.ContainsKey(tag);
        }

        /// <summary>축 표시명. 빈 태그는 "일반", 미등록 태그는 원문을 그대로 돌려준다.</summary>
        public static string GetDisplayName(string tag)
        {
            if (string.IsNullOrEmpty(tag)) return NEUTRAL_LABEL;
            return registry.TryGetValue(tag, out var info) ? info.DisplayName : tag;
        }

        /// <summary>축 표시 색. 빈·미등록 태그는 중립 회색.</summary>
        public static Color GetColor(string tag)
        {
            if (string.IsNullOrEmpty(tag)) return NEUTRAL_COLOR;
            return registry.TryGetValue(tag, out var info) ? info.Color : NEUTRAL_COLOR;
        }

        /// <summary>해당 보유 개수가 시너지 발동 임계에 도달했는지.</summary>
        public static bool IsActivated(int count) => count >= ACTIVATION_THRESHOLD;

        /// <summary>
        /// 보유 스킬 목록을 축별 개수로 집계해 counts에 채운다(무축 스킬은 제외).
        /// HUD 카운터와 드래프트 빌드 패널이 같은 집계를 쓰도록 공유한다.
        /// </summary>
        public static void Tally(IReadOnlyList<SkillData> owned, Dictionary<string, int> counts)
        {
            if (counts == null) return;
            counts.Clear();
            if (owned == null) return;

            for (int i = 0; i < owned.Count; i++)
            {
                var skill = owned[i];
                if (skill == null || string.IsNullOrEmpty(skill.synergyTag)) continue;
                counts.TryGetValue(skill.synergyTag, out int current);
                counts[skill.synergyTag] = current + 1;
            }
        }
    }
}
