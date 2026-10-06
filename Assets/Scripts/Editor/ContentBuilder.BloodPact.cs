#if UNITY_EDITOR
using Abyss.Runtime.Draft;
using Abyss.Runtime.Skill;
using UnityEngine;

namespace Abyss.EditorTools
{
    /// <summary>
    /// 스킬 19~30 (2026-10-06) — 피의 서약 축 7종 + 무축 보편 5종. 08-content-roadmap §2-2 목록을 실제 시스템에 맞춰 조정했다.
    ///
    /// 조정한 것(로드맵 원안 → 구현):
    /// · 최후의 일격 임계 3개+ → 2개+ — 발동 임계는 <see cref="SynergyAxis.ACTIVATION_THRESHOLD"/> 하나다(HUD ✦ 표시와 발동 조건 일치).
    /// · 죽음의 약속 Active → Passive — 「사망 직전」에 반응하는 효과라 버튼으로 쓸 수 없다. 「적 전체 5초 마비」 → 주변 충격(경직).
    /// · 혈영 「회피율」 → 「저HP 피격 무효 확률」 — 회피 스탯이 없어 같은 의미를 피격 단계에서 굴린다.
    /// · 운명의 가호 「Legendary +5%(영구·메타)」 → 이번 런 Epic·Legendary 가중치 2배 — 메타 이전은 별도 결정.
    /// · 시간 왜곡 「시간 50% 감속」 → 자신 가속(이동·공격 쿨다운) — timeScale은 GameFlow FSM 소유라 스킬이 건드리지 않는다.
    ///
    /// 피의 서약을 대표하는 폼이 없어 7종 모두 전역이다. 효과 코드는 PlayerCharacter.BloodPact / .NeutralPassives.
    /// </summary>
    public static partial class ContentBuilder
    {
        private static void CreateSkillsBloodPactAndNeutral()
        {
            // ── 피의 서약 (blood_pact) 7 ──
            AddSkill(19, "skill_blood_pact_blade", "혈맹의 칼날", SkillCategory.Active, SkillRarity.Rare, SynergyAxis.AXIS_BLOOD_PACT, "",
                "현재 HP 10%를 바쳐 전방을 크게 벤다 (피해 40, CD 4초)");
            AddSkill(20, "skill_blood_rage", "피의 분노", SkillCategory.Passive, SkillRarity.Common, SynergyAxis.AXIS_BLOOD_PACT, "",
                "HP 50% 이하에서 기본 공격 쿨다운 30% 감소");
            AddSkill(21, "skill_final_stand", "최후의 일격", SkillCategory.Synergy, SkillRarity.Epic, SynergyAxis.AXIS_BLOOD_PACT, "",
                "[피의 서약] 2개+ 시 HP 25% 이하에서 기본 공격 피해 2배");
            AddSkill(22, "skill_vampiric_seal", "흡혈 인장", SkillCategory.Augment, SkillRarity.Common, SynergyAxis.AXIS_BLOOD_PACT, "",
                "기본 공격 적중 피해의 5%만큼 HP 회복");
            AddSkill(23, "skill_crimson_radiance", "핏빛 광채", SkillCategory.Passive, SkillRarity.Legendary, SynergyAxis.AXIS_BLOOD_PACT, "",
                "적 처치 시 최대 HP +1 (이번 런, 최대 +30)");
            AddSkill(24, "skill_deaths_promise", "죽음의 약속", SkillCategory.Passive, SkillRarity.Epic, SynergyAxis.AXIS_BLOOD_PACT, "",
                "런당 1회, 치명상을 HP 1로 버티고 2초간 무적. 주변 적에게 충격 20");
            AddSkill(25, "skill_blood_shade", "혈영", SkillCategory.Passive, SkillRarity.Rare, SynergyAxis.AXIS_BLOOD_PACT, "",
                "HP 30% 이하에서 피격 시 25% 확률로 피해 무효");

            // ── 무축 보편 5 ──
            AddSkill(26, "skill_keen_eye", "신중한 시선", SkillCategory.Passive, SkillRarity.Common, "", "",
                "기본 공격이 10% 확률로 치명타 (피해 1.5배)");
            AddSkill(27, "skill_holy_ward", "신성 보호", SkillCategory.Passive, SkillRarity.Rare, "", "",
                "5초마다 다음 피격 피해를 1로 줄인다");
            AddSkill(28, "skill_golden_touch", "황금 손길", SkillCategory.Passive, SkillRarity.Common, "", "",
                "적 처치·방 클리어 골드 +25%");
            AddSkill(29, "skill_fates_favor", "운명의 가호", SkillCategory.Passive, SkillRarity.Legendary, "", "",
                "이번 런 드래프트에서 에픽·전설 스킬 등장 확률 2배");
            AddSkill(30, "skill_time_warp", "시간 왜곡", SkillCategory.Active, SkillRarity.Epic, "", "",
                "4초간 자신의 시간을 가속한다: 이동속도 1.3배, 기본 공격 쿨다운 절반 (CD 14초)");
        }

        private static void CreateAbilitiesBloodPactAndNeutral()
        {
            CreateOrSkip<GenericAbilityData>($"{AbyssPaths.Abilities}/Ability_BloodPactBlade.asset", so =>
            {
                so.abilityName = "blood_pact_blade";
                so.description = "현재 HP 10%를 바쳐 전방을 크게 벤다.";
                so.cooldownDuration = 4f;
                so.effectType = AbilityEffectType.MeleeArea;
                so.damage = 40;                       // 같은 Rare 근접 광역(방패 강타 28)보다 크다 — 대가만큼
                so.meleeBoxSize = new Vector2(2.8f, 1.8f);
                so.meleeForwardOffset = 1.2f;
                so.hpCostRatio = 0.1f;
                so.showHitEffect = true;
                so.effectColor = new Color(0.9f, 0.22f, 0.3f, 1f);
            });

            CreateOrSkip<GenericAbilityData>($"{AbyssPaths.Abilities}/Ability_TimeWarp.asset", so =>
            {
                so.abilityName = "time_warp";
                so.description = "4초간 이동속도와 공격 속도를 끌어올린다.";
                so.cooldownDuration = 14f;
                so.effectType = AbilityEffectType.Buff;
                so.healAmount = 0;
                so.buffMoveSpeedMultiplier = 1.3f;
                so.buffAttackMultiplier = 1f;
                so.buffDefenseMultiplier = 1f;
                so.buffAttackCooldownMultiplier = 0.5f;
                so.buffDuration = 4f;
                so.showHitEffect = true;
                so.effectColor = new Color(0.55f, 0.85f, 1f, 1f);
            });
        }

        private static void WireActiveAbilitiesBloodPactAndNeutral()
        {
            WireOne("skill_blood_pact_blade", $"{AbyssPaths.Abilities}/Ability_BloodPactBlade.asset");
            WireOne("skill_time_warp", $"{AbyssPaths.Abilities}/Ability_TimeWarp.asset");
        }
    }
}
#endif
