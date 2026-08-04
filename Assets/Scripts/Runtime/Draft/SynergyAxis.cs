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

        // ── 축 태그 ID ──
        // 문자열을 코드 곳곳에 흩어 쓰면 오타가 조용한 미발동으로 이어진다(축이 안 맞으면 개수가 0이 될 뿐
        // 에러가 나지 않는다). 등록표 키와 소비처가 같은 상수를 보게 모아 둔다.
        // ⚠ 값은 스킬 에셋(SkillData.synergyTag)에 실제로 들어 있는 문자열이며, 기획 문서의 영문 표기
        //    (blaze 등)가 아니다 — 데이터가 SoT다(Docs/technical/synergy-counter-hud.md 축 ID 정합성 항목).
        public const string AXIS_FIRE = "fire";
        public const string AXIS_ABYSS = "abyss";
        public const string AXIS_GUARD = "guard";
        public const string AXIS_BLOOD_PACT = "blood_pact";
        public const string AXIS_FROST = "frost";
        public const string AXIS_SOUL = "soul";

        // ── 발동 로직이 코드에 있는 시너지 스킬 ID ──
        // 시너지 효과는 데이터로 표현할 수 있는 형태가 아니라(사망 폭발·쿨다운 감소) 스킬별 전용 코드가 필요하다.
        // 그 코드가 어떤 에셋을 가리키는지 한 곳에서 보이도록 여기에 모은다.
        public const string SKILL_EXPLOSIVE_THEOLOGY = "skill_explosive_theology";
        public const string SKILL_ABYSS_ALLY = "skill_abyss_ally";

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
            [AXIS_FIRE] = new AxisInfo(AXIS_FIRE, "불꽃", new Color(1f, 0.46f, 0.18f)),
            [AXIS_ABYSS] = new AxisInfo(AXIS_ABYSS, "심연", new Color(0.64f, 0.42f, 0.96f)),
            [AXIS_GUARD] = new AxisInfo(AXIS_GUARD, "수호", new Color(1f, 0.80f, 0.32f)),
            [AXIS_BLOOD_PACT] = new AxisInfo(AXIS_BLOOD_PACT, "피의 서약", new Color(0.90f, 0.22f, 0.30f)),
            [AXIS_FROST] = new AxisInfo(AXIS_FROST, "빙결", new Color(0.45f, 0.80f, 1f)),
            [AXIS_SOUL] = new AxisInfo(AXIS_SOUL, "영혼", new Color(0.55f, 0.95f, 0.70f)),
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
        /// 지정 시너지 스킬이 발동 조건을 만족하는지. 조건은 둘 다다 —
        /// ① 그 스킬을 보유하고 있을 것 ② 그 스킬과 같은 축의 보유 수가 임계(2) 이상일 것.
        ///
        /// 개수에는 <b>스킬 자신도 포함</b>한다. HUD 시너지 칩이 쓰는 <see cref="Tally"/>가 보유 전부를 세므로,
        /// 여기서만 자신을 빼면 칩에 "[불꽃] 2 ✦"가 떠 있는데 효과는 안 나는 상태가 된다.
        /// 표시가 곧 발동 조건이어야 플레이어가 화면을 믿고 축을 노릴 수 있다.
        /// </summary>
        public static bool IsSynergyActive(IReadOnlyList<SkillData> owned, string skillId)
        {
            if (owned == null || string.IsNullOrEmpty(skillId)) return false;

            // ① 보유 여부 + 그 스킬의 축 확인. 축 태그가 없는 스킬은 시너지 판정 대상이 아니다.
            string axis = null;
            for (int i = 0; i < owned.Count; i++)
            {
                var skill = owned[i];
                if (skill == null || skill.skillId != skillId) continue;
                axis = skill.synergyTag;
                break;
            }
            if (string.IsNullOrEmpty(axis)) return false;

            // ② 같은 축 보유 수 집계.
            int count = 0;
            for (int i = 0; i < owned.Count; i++)
            {
                var skill = owned[i];
                if (skill != null && skill.synergyTag == axis) count += 1;
            }
            return IsActivated(count);
        }

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
