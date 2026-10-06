using System.Collections.Generic;
using Abyss.Runtime.Draft;
using Abyss.Runtime.Enemy;
using Abyss.Runtime.Feedback;
using UnityEngine;

namespace Abyss.Runtime.Player
{
    /// <summary>
    /// 피의 서약 축(<see cref="SynergyAxis.AXIS_BLOOD_PACT"/>) 전용 발동 파트 — 흡혈·저HP 버서커·고위험 고보상.
    ///
    /// Passive 5종(피의 분노·흡혈 인장·핏빛 광채·죽음의 약속·혈영)과 Synergy 1종(최후의 일격).
    /// Active 혈맹의 칼날은 데이터(<c>GenericAbilityData.hpCostRatio</c>)만으로 동작해 여기 없다.
    ///
    /// 축을 대표하는 폼이 아직 없어 7종 모두 전역(formBound 비움)이다 — 어느 폼 런에서도 축 2개를 모을 수 있어야
    /// 최후의 일격이 '무용 카드'가 되지 않는다(반격 태세가 방패병 전용인 것과 반대 이유).
    ///
    /// 저HP 조건은 모두 <b>현재 HP / 실효 최대 HP</b> 비율로 판정한다. 핏빛 광채로 최대 HP가 오르면 같은 HP라도
    /// 비율이 내려간다 — 두 스킬을 함께 들면 버서커 구간에 들어가기 쉬워지는 것이 의도한 상호작용이다.
    /// </summary>
    public sealed partial class PlayerCharacter
    {
        [Header("피의 서약 — 피의 분노 · 최후의 일격 · 혈영")]
        [Tooltip("피의 분노가 켜지는 HP 비율(이하)")]
        [SerializeField, Range(0f, 1f)] private float bloodRageHpThreshold = 0.5f;
        [Tooltip("피의 분노 중 기본 공격 쿨다운 배율(0.7 = 30% 단축)")]
        [SerializeField, Range(0.25f, 1f)] private float bloodRageCooldownMultiplier = 0.7f;
        [Tooltip("최후의 일격이 켜지는 HP 비율(이하)")]
        [SerializeField, Range(0f, 1f)] private float finalStandHpThreshold = 0.25f;
        [Tooltip("최후의 일격 중 기본 공격 피해 배율")]
        [SerializeField, Min(1f)] private float finalStandDamageMultiplier = 2f;
        [Tooltip("혈영이 켜지는 HP 비율(이하)")]
        [SerializeField, Range(0f, 1f)] private float bloodShadeHpThreshold = 0.3f;
        [Tooltip("혈영 회피 확률")]
        [SerializeField, Range(0f, 1f)] private float bloodShadeDodgeChance = 0.25f;

        [Header("피의 서약 — 흡혈 인장 · 핏빛 광채")]
        [Tooltip("기본 공격 적중 피해 중 회복으로 돌리는 비율")]
        [SerializeField, Range(0f, 1f)] private float vampiricSealRatio = 0.05f;
        [Tooltip("적 하나를 처치할 때 오르는 최대 HP")]
        [SerializeField, Min(0)] private int crimsonRadianceMaxHpPerKill = 1;
        [Tooltip("핏빛 광채로 오를 수 있는 최대 HP 총량(런당)")]
        [SerializeField, Min(0)] private int crimsonRadianceMaxHpCap = 30;

        [Header("피의 서약 — 죽음의 약속")]
        [SerializeField, Min(0f)] private float deathsPromiseInvulnDuration = 2f;
        [Tooltip("버틴 순간 주변 적에게 퍼지는 충격 반경(월드 유닛)")]
        [SerializeField, Min(0f)] private float deathsPromiseRadius = 3f;
        [Tooltip("충격 피해 — 경직을 거는 일반 피해 경로다")]
        [SerializeField, Min(0)] private int deathsPromiseDamage = 20;
        [SerializeField, Min(0.05f)] private float deathsPromiseRingDuration = 0.35f;

        [SerializeField, Min(0.01f)] private float bloodShadeFlashDuration = 0.12f;

        private bool isBloodRageActive;
        private bool isFinalStandActive;
        private bool isVampiricSealActive;
        private bool isCrimsonRadianceActive;
        private bool isDeathsPromiseActive;
        private bool isBloodShadeActive;

        // 런 스코프 상태 — InitializeHealth(런 시작)에서 되돌린다.
        private bool isDeathsPromiseUsed;
        private bool isDeathsPromisePending;
        private int crimsonRadianceGained;
        private float invulnerableUntil = -1f;

        // 죽음의 약속 전용 버퍼. 피격 처리 도중(적의 공격 순회 안)에 끼어들므로 다른 버퍼와 공유하지 않는다.
        private static readonly Collider2D[] deathsPromiseOverlapBuffer = new Collider2D[32];
        private static readonly List<EnemyBase> deathsPromiseHitList = new();
        private static ContactFilter2D deathsPromiseFilter = ContactFilter2D.noFilter;

        /// <summary>죽음의 약속 무적 중인가. 피해·가드 판정 자체를 건너뛴다(Health 파트).</summary>
        public bool IsInvulnerable => Time.time < invulnerableUntil;

        /// <summary>죽음의 약속을 이번 런에 이미 썼는가(디버그·후속 HUD 조회용).</summary>
        public bool IsDeathsPromiseUsed => isDeathsPromiseUsed;

        /// <summary>핏빛 광채로 이번 런에 오른 최대 HP(디버그·후속 HUD 조회용).</summary>
        public int CrimsonRadianceGained => crimsonRadianceGained;

        private float HpRatio => maxHp > 0 ? (float)currentHp / maxHp : 1f;

        /// <summary>피의 분노 기본 공격 쿨다운 배율. 조건(보유·HP 비율)을 매번 읽는다 — 회복하면 바로 꺼진다.</summary>
        public float BloodRageCooldownMultiplier =>
            isBloodRageActive && !isDead && HpRatio <= bloodRageHpThreshold ? bloodRageCooldownMultiplier : 1f;

        /// <summary>최후의 일격 피해 배율. <see cref="TotalAttackMult"/>의 한 층이다.</summary>
        public float FinalStandAttackMult =>
            isFinalStandActive && !isDead && HpRatio <= finalStandHpThreshold ? finalStandDamageMultiplier : 1f;

        /// <summary>RecomputePassiveState에서 함께 부른다(보유 목록 조회 한 번을 공유).</summary>
        private void RecomputeBloodPactState(IReadOnlyList<SkillData> owned)
        {
            isBloodRageActive = SkillIds.IsOwned(owned, SkillIds.BLOOD_RAGE);
            isFinalStandActive = SynergyAxis.IsSynergyActive(owned, SkillIds.FINAL_STAND);
            isVampiricSealActive = SkillIds.IsOwned(owned, SkillIds.VAMPIRIC_SEAL);
            isCrimsonRadianceActive = SkillIds.IsOwned(owned, SkillIds.CRIMSON_RADIANCE);
            isDeathsPromiseActive = SkillIds.IsOwned(owned, SkillIds.DEATHS_PROMISE);
            isBloodShadeActive = SkillIds.IsOwned(owned, SkillIds.BLOOD_SHADE);
        }

        /// <summary>
        /// 런 시작 시 런 스코프 상태를 되돌린다. 이미 오른 최대 HP는 InitializeHealth가 새로 계산하므로 카운터만 비운다.
        /// </summary>
        private void ResetBloodPactRunState()
        {
            isDeathsPromiseUsed = false;
            isDeathsPromisePending = false;
            crimsonRadianceGained = 0;
            invulnerableUntil = -1f;
        }

        // ====== 흡혈 인장 — 기본 공격 적중 피해의 일부 회복 ======

        /// <summary>
        /// 기본 공격이 실제로 준 피해 합계에서 회복한다(근접은 한 번에 맞힌 전원 합, 원거리는 화살 하나씩).
        /// 폼의 근접 흡수(암흑 검사)와는 별개 층이라 둘 다 있으면 둘 다 찬다.
        /// </summary>
        private void ApplyVampiricSeal(int totalDamage)
        {
            if (!isVampiricSealActive || isDead || totalDamage <= 0) return;

            int amount = Mathf.RoundToInt(totalDamage * vampiricSealRatio);
            if (amount > 0) Heal(amount);
        }

        // ====== 핏빛 광채 — 적 처치 시 최대 HP 증가 ======

        /// <summary>처치마다 최대 HP를 올리고 같은 양을 채운다. 런당 상한을 넘지 않는다.</summary>
        private void ApplyCrimsonRadiance()
        {
            if (!isCrimsonRadianceActive || isDead) return;

            int gain = Mathf.Min(crimsonRadianceMaxHpPerKill, crimsonRadianceMaxHpCap - crimsonRadianceGained);
            if (gain <= 0) return;

            crimsonRadianceGained += gain;
            IncreaseMaxHp(gain);
        }

        // ====== 혈영 — 저HP 회피 ======

        /// <summary>
        /// HP가 임계 이하일 때 확률로 피격을 통째로 무효화한다. 회피하면 경감·변환·반격 단계가 모두 돌지 않는다 —
        /// 맞지 않은 것이므로 반격 태세가 나가면 "피격당하면"이라는 설명과 어긋난다.
        /// </summary>
        private bool TryDodgeWithBloodShade()
        {
            if (!isBloodShadeActive || isDead || HpRatio > bloodShadeHpThreshold) return false;
            if (Random.value >= bloodShadeDodgeChance) return false;

            TriggerAttackFlash(SynergyAxis.GetColor(SynergyAxis.AXIS_BLOOD_PACT), bloodShadeFlashDuration);
            return true;
        }

        // ====== 죽음의 약속 — 런당 1회 치명상을 HP 1로 버틴다 ======

        /// <summary>
        /// 모든 경감이 끝난 피해가 치명상이면 HP 1을 남기는 양으로 줄이고, 버틴 뒤의 연출·무적은
        /// <see cref="ResolveDeathsPromise"/>가 HP 차감 뒤에 건다(HP 이벤트 순서를 기존과 같게 둔다).
        /// </summary>
        private int ApplyDeathsPromise(int damage)
        {
            if (!isDeathsPromiseActive || isDeathsPromiseUsed || damage < currentHp) return damage;

            isDeathsPromiseUsed = true;
            isDeathsPromisePending = true;
            return Mathf.Max(0, currentHp - 1);
        }

        /// <summary>버틴 직후: 무적 시작 + 주변 적에게 충격(경직을 거는 일반 피해).</summary>
        private void ResolveDeathsPromise()
        {
            if (!isDeathsPromisePending) return;
            isDeathsPromisePending = false;

            invulnerableUntil = Time.time + deathsPromiseInvulnDuration;
            Debug.Log($"[PlayerCharacter] 죽음의 약속 발동 — HP 1로 버팀, {deathsPromiseInvulnDuration:F1}초 무적");

            if (deathsPromiseRadius <= 0f) return;

            BossAreaEffect.Spawn(transform.position, deathsPromiseRadius,
                                 SynergyAxis.GetColor(SynergyAxis.AXIS_BLOOD_PACT), deathsPromiseRingDuration);
            if (deathsPromiseDamage <= 0) return;

            deathsPromiseFilter.useTriggers = Physics2D.queriesHitTriggers;
            int count = Physics2D.OverlapCircle(
                (Vector2)transform.position, deathsPromiseRadius, deathsPromiseFilter, deathsPromiseOverlapBuffer);

            deathsPromiseHitList.Clear();
            for (int i = 0; i < count; i++)
            {
                var col = deathsPromiseOverlapBuffer[i];
                if (col == null) continue;
                var enemy = col.GetComponentInParent<EnemyBase>();
                if (enemy == null || enemy.IsDead) continue;
                if (deathsPromiseHitList.Contains(enemy)) continue;
                deathsPromiseHitList.Add(enemy);
            }

            // 수집을 끝낸 뒤 적용 — 충격으로 적이 죽으면 폭발 신학이 같은 프레임에 끼어들 수 있다.
            for (int i = 0; i < deathsPromiseHitList.Count; i++)
            {
                deathsPromiseHitList[i].TakeDamage(deathsPromiseDamage);
            }
        }
    }
}
