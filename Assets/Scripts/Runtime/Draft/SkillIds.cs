namespace Abyss.Runtime.Draft
{
    /// <summary>
    /// 전용 발동 코드를 가진 스킬의 식별자 모음(SoT).
    ///
    /// 대부분의 스킬은 데이터만으로 동작한다(Active = relatedAbility, 수치 = flatBonus/multiplier).
    /// 그러나 Synergy·Passive 중 일부는 데이터로 표현할 수 없는 규칙을 갖는다 —
    /// "적이 죽으면 폭발한다", "받는 피해의 10%를 연소로 바꾼다" 같은 것들. 이들은 코드가
    /// 특정 스킬을 <b>이름으로</b> 알아야 하므로, 그 이름을 여기 한 곳에 모은다.
    ///
    /// 흩어 두면 오타가 조용한 미발동으로 이어진다(스킬 ID가 안 맞으면 "미보유"로 판정될 뿐
    /// 에러가 나지 않는다). 값은 스킬 에셋의 <c>SkillData.skillId</c>와 정확히 일치해야 한다.
    /// </summary>
    public static class SkillIds
    {
        // ── Synergy ──
        /// <summary>폭발 신학 — [불꽃] 2+ 시 적 사망 폭발.</summary>
        public const string EXPLOSIVE_THEOLOGY = "skill_explosive_theology";

        /// <summary>심연 동료 — [심연] 2+ 시 대시 CD 감소.</summary>
        public const string ABYSS_ALLY = "skill_abyss_ally";

        /// <summary>반격 태세 — [수호] 2+ 시 피격 피해의 일부를 주변 적에게 되돌린다.</summary>
        public const string COUNTER_STANCE = "skill_counter_stance";

        // ── Passive ──
        /// <summary>연소 강화 — 모든 연소 피해 +50%.</summary>
        public const string BURN_ENHANCEMENT = "skill_burn_enhancement";

        /// <summary>불꽃 갑옷 — 받는 피해 10%를 주변 적의 연소로 변환.</summary>
        public const string FLAME_ARMOR = "skill_flame_armor";

        /// <summary>잔상 — 대시 시 잔상 생성, 0.5초 후 폭발(기본 공격 × 0.5).</summary>
        public const string AFTERIMAGE = "skill_afterimage";

        /// <summary>심연 충전 — 폼 교체 시 다음 기본 공격 피해 2배.</summary>
        public const string ABYSS_CHARGE = "skill_abyss_charge";

        /// <summary>영혼 회수 — 적 처치 시 HP 회복.</summary>
        public const string SOUL_RECLAIM = "skill_soul_reclaim";

        /// <summary>보유 목록에 해당 스킬이 있는지. 상시 효과(Passive) 판정의 공통 진입점.</summary>
        public static bool IsOwned(System.Collections.Generic.IReadOnlyList<SkillData> owned, string skillId)
        {
            if (owned == null || string.IsNullOrEmpty(skillId)) return false;

            for (int i = 0; i < owned.Count; i++)
            {
                var skill = owned[i];
                if (skill != null && skill.skillId == skillId) return true;
            }
            return false;
        }
    }
}
