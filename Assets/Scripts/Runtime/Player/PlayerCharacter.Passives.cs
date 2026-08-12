using System;
using System.Collections.Generic;
using Abyss.Runtime.Combat;
using Abyss.Runtime.Draft;
using Abyss.Runtime.Enemy;
using Abyss.Runtime.Events;
using Abyss.Runtime.Feedback;
using Abyss.Runtime.Form;
using UnityEngine;

namespace Abyss.Runtime.Player
{
    /// <summary>
    /// 전용 발동 코드가 필요한 Passive 스킬 파트. 현재 5종 —
    /// 불꽃 축의 <b>연소 강화</b>(모든 연소 피해 +50%)·<b>불꽃 갑옷</b>(받는 피해 10%를 주변 적의 연소로 변환),
    /// 심연 축의 <b>잔상</b>(대시 자리에 남아 0.5초 후 폭발)·<b>심연 충전</b>(폼 교체 시 다음 기본 공격 2배)·
    /// <b>영혼 회수</b>(적 처치 시 HP 회복).
    ///
    /// <see cref="PlayerCharacter.Synergy"/>와 같은 구조(보유 스킬 조회 → 플래그 재계산)이지만
    /// 파일을 나눈 이유는 <b>카테고리가 다르면 함께 바뀌지 않기 때문</b>이다. 시너지는 축 임계 판정이,
    /// Passive는 보유 여부만이 조건이라 규칙이 얽히지 않는다.
    /// </summary>
    public sealed partial class PlayerCharacter
    {
        [Header("Passive — 불꽃 갑옷")]
        [Tooltip("받는 피해 중 연소로 변환되는 비율")]
        [SerializeField, Range(0f, 0.5f)] private float flameArmorConversionRatio = 0.1f;
        [Tooltip("변환된 연소를 부여할 반경(월드 유닛)")]
        [SerializeField, Min(0f)] private float flameArmorRadius = 3f;
        [SerializeField, Min(0.5f)] private float flameArmorBurnDuration = 4f;

        [Header("Passive — 영혼 회수")]
        [Tooltip("적 하나를 처치할 때 회복하는 HP")]
        [SerializeField, Min(0)] private int soulReclaimHealPerKill = 2;

        [Header("Passive — 잔상")]
        [Tooltip("대시 자리에 남은 잔상이 터지는 반경(월드 유닛)")]
        [SerializeField, Min(0f)] private float afterimageRadius = 1.6f;
        [Tooltip("잔상이 생성되고 폭발하기까지의 시간(초)")]
        [SerializeField, Min(0.05f)] private float afterimageDelay = 0.5f;
        [Tooltip("잔상 폭발 피해 = 약공격 피해 × 이 비율")]
        [SerializeField, Range(0f, 2f)] private float afterimageDamageRatio = 0.5f;
        [Tooltip("남는 잔상 스프라이트의 초기 알파")]
        [SerializeField, Range(0f, 1f)] private float afterimageGhostAlpha = 0.45f;
        [SerializeField, Min(0.05f)] private float afterimageRingDuration = 0.24f;

        /// <summary>연소 강화 배율. 03-skill-draft-system.md §5 #2 "모든 연소 데미지 +50%".</summary>
        private const float BURN_ENHANCEMENT_MULTIPLIER = 1.5f;

        /// <summary>심연 충전 피해 배율. 03-skill-draft-system.md §6-1 "폼 교체 시 다음 공격 2배".</summary>
        private const float ABYSS_CHARGE_MULTIPLIER = 2f;

        private bool isBurnEnhancementActive;
        private bool isFlameArmorActive;
        private bool isAfterimageActive;
        private bool isAbyssChargeActive;
        private bool isSoulReclaimActive;

        /// <summary>심연 충전이 장전된 상태(폼 교체 후 아직 적중시키지 않음).</summary>
        private bool isAbyssChargeReady;

        // 잔상 전용 버퍼. 폭발이 지연 실행이라 어느 순회 도중에 끼어들지 알 수 없다
        // (Synergy·Passive의 다른 버퍼와 공유하지 않는 것과 같은 이유).
        private static readonly Collider2D[] afterimageOverlapBuffer = new Collider2D[32];
        private static readonly List<EnemyBase> afterimageHitList = new();
        private static ContactFilter2D afterimageFilter = new ContactFilter2D().NoFilter();

        // 불꽃 갑옷 전용 버퍼. Combat·Synergy 버퍼와 공유하지 않는다 —
        // 피격은 적의 공격 처리 도중에 발생하므로 어느 순회 안에 끼어들지 알 수 없다.
        private static readonly Collider2D[] flameArmorOverlapBuffer = new Collider2D[32];
        private static readonly List<EnemyBase> flameArmorHitList = new();
        private static ContactFilter2D flameArmorFilter = new ContactFilter2D().NoFilter();

        /// <summary>
        /// 연소 피해 배율(1 = 무보정). 연소를 <b>부여하는 시점</b>에 곱해 확정한다 —
        /// 적이 tick마다 플레이어를 조회하지 않게 하기 위함(<see cref="BurnPayload"/> 참조).
        /// </summary>
        public float BurnDamageMultiplier => isBurnEnhancementActive ? BURN_ENHANCEMENT_MULTIPLIER : 1f;

        /// <summary>불꽃 갑옷 보유 여부(디버그·후속 UI 조회용).</summary>
        public bool IsFlameArmorActive => isFlameArmorActive;

        /// <summary>연소 강화 보유 여부(디버그·후속 UI 조회용).</summary>
        public bool IsBurnEnhancementActive => isBurnEnhancementActive;

        /// <summary>잔상 보유 여부(디버그·후속 UI 조회용).</summary>
        public bool IsAfterimageActive => isAfterimageActive;

        /// <summary>심연 충전 보유 여부(디버그·후속 UI 조회용).</summary>
        public bool IsAbyssChargeActive => isAbyssChargeActive;

        /// <summary>심연 충전이 장전돼 다음 적중이 2배가 되는 상태인지(디버그·후속 HUD 조회용).</summary>
        public bool IsAbyssChargeReady => isAbyssChargeReady;

        /// <summary>영혼 회수 보유 여부(디버그·후속 UI 조회용).</summary>
        public bool IsSoulReclaimActive => isSoulReclaimActive;

        // PlayerCharacter.Movement의 OnEnable/OnDisable에서 호출(partial 중복 정의 회피).
        private void SubscribePassiveEvents()
        {
            GameEvents.OnSkillDrafted += HandleSkillDraftedForPassives;
            GameEvents.OnFormSwapped += HandleFormSwappedForPassives;
            GameEvents.OnEnemyKilled += HandleEnemyKilledForPassives;
        }

        private void UnsubscribePassiveEvents()
        {
            GameEvents.OnSkillDrafted -= HandleSkillDraftedForPassives;
            GameEvents.OnFormSwapped -= HandleFormSwappedForPassives;
            GameEvents.OnEnemyKilled -= HandleEnemyKilledForPassives;
        }

        private void HandleSkillDraftedForPassives(SkillData skill, DraftTriggerReason reason)
        {
            RecomputePassiveState();
        }

        /// <summary>
        /// 보유 스킬로부터 Passive 활성 상태를 다시 계산한다(획득·교체 시점과 Start).
        /// 이벤트 인자의 스킬 하나만 보고 켜면 슬롯 교체로 스킬이 <b>빠지는</b> 경우를 놓친다.
        /// </summary>
        private void RecomputePassiveState()
        {
            var draft = ResolveDraftSession();
            var owned = draft != null ? draft.Owned : null;

            isBurnEnhancementActive = SkillIds.IsOwned(owned, SkillIds.BURN_ENHANCEMENT);
            isFlameArmorActive = SkillIds.IsOwned(owned, SkillIds.FLAME_ARMOR);
            isAfterimageActive = SkillIds.IsOwned(owned, SkillIds.AFTERIMAGE);
            isAbyssChargeActive = SkillIds.IsOwned(owned, SkillIds.ABYSS_CHARGE);
            isSoulReclaimActive = SkillIds.IsOwned(owned, SkillIds.SOUL_RECLAIM);

            // 스킬을 잃으면 장전분도 함께 사라진다 — 미보유 스킬의 효과가 한 대 더 나가면
            // "지금 무엇을 가졌는가"와 화면에서 벌어지는 일이 어긋난다.
            if (!isAbyssChargeActive) isAbyssChargeReady = false;
        }

        // ====== 영혼 회수 — 적 처치 시 HP 회복 ======

        /// <summary>
        /// 적이 죽을 때마다 소량 회복한다. 런 중 회복 수단이 휴식 방·이벤트뿐이라, <b>전투를 잘하는 것</b>이
        /// 회복이 되는 경로를 하나 연다.
        ///
        /// 거리·가해자를 따지지 않는다 — 스킬 설명이 "적 처치 시"이므로 조건을 더 붙이면 설명이 거짓이 된다.
        /// (잔상·폭발 신학이 죽인 적도 포함된다. 그것도 플레이어가 만든 처치다.)
        /// <see cref="Heal"/>이 최대 HP에서 잘리므로 만피에서는 아무 일도 일어나지 않는다.
        /// </summary>
        private void HandleEnemyKilledForPassives(EnemyData _, Vector3 __)
        {
            if (!isSoulReclaimActive || isDead || soulReclaimHealPerKill <= 0) return;
            Heal(soulReclaimHealPerKill);
        }

        // ====== 불꽃 갑옷 — 받는 피해 10% → 주변 적 연소로 변환 ======

        /// <summary>
        /// 받는 피해에서 변환분을 덜어내고, 덜어낸 만큼을 주변 적의 연소로 옮긴다. 실제로 받을 피해를 돌려준다.
        ///
        /// "감소"가 아니라 <b>"변환"</b>인 것이 이 스킬의 정체성이다 — 사라지는 게 아니라 적이 대신 탄다.
        /// 그래서 경감분을 그냥 버리지 않고 연소 총피해로 환산해 넘긴다.
        ///
        /// 방어 버프(철벽 방어)와는 별개 단계다. 버프가 먼저 줄이고, 남은 피해에서 변환이 일어난다 —
        /// 순서를 바꾸면 방어 버프 중에 변환량이 커져 "방어할수록 더 태운다"는 이상한 규칙이 된다.
        /// </summary>
        private int ApplyFlameArmor(int incoming)
        {
            if (!isFlameArmorActive || incoming <= 0) return incoming;

            int converted = Mathf.FloorToInt(incoming * flameArmorConversionRatio);
            // 변환분이 0이면(약공) 아무 일도 일어나지 않는다 — 소수점을 올려 받아 피해를 없애지 않는다.
            if (converted <= 0) return incoming;

            // 최소 1 피해는 남긴다(TakeDamage의 기존 규약과 동일 — 완전 무효화 금지).
            int remaining = Mathf.Max(1, incoming - converted);
            SpreadFlameArmorBurn(converted);
            return remaining;
        }

        /// <summary>
        /// 변환된 피해량을 주변 적에게 연소로 분배한다. 스택 1 고정, 지속시간에 나눠 초당 피해를 산출 —
        /// 총량이 곧 변환분이라 "10%가 옮겨갔다"는 설명과 수치가 일치한다.
        /// </summary>
        private void SpreadFlameArmorBurn(int convertedDamage)
        {
            if (flameArmorRadius <= 0f || flameArmorBurnDuration <= 0f) return;

            flameArmorFilter.useTriggers = Physics2D.queriesHitTriggers;
            int count = Physics2D.OverlapCircle(
                (Vector2)transform.position, flameArmorRadius, flameArmorFilter, flameArmorOverlapBuffer);

            flameArmorHitList.Clear();
            for (int i = 0; i < count; i++)
            {
                var col = flameArmorOverlapBuffer[i];
                if (col == null) continue;
                var enemy = col.GetComponentInParent<EnemyBase>();
                if (enemy == null || enemy.IsDead) continue;
                if (flameArmorHitList.Contains(enemy)) continue;
                flameArmorHitList.Add(enemy);
            }
            if (flameArmorHitList.Count == 0) return;

            // 연소 강화는 여기에도 적용된다 — "모든 연소 데미지 +50%"이므로 부여 경로를 가리지 않는다.
            float perSecond = convertedDamage / flameArmorBurnDuration;
            var payload = new BurnPayload(1, flameArmorBurnDuration, perSecond).Scaled(BurnDamageMultiplier);

            for (int i = 0; i < flameArmorHitList.Count; i++)
            {
                flameArmorHitList[i].ApplyBurn(payload);
            }
        }

        // ====== 잔상 — 대시 자리에 남아 0.5초 후 폭발 ======

        /// <summary>
        /// 대시가 확정된 시점에 Movement 파트가 호출한다. 잔상은 <b>대시가 시작된 자리</b>에 남는다 —
        /// 따라오는 것이 아니라 뒤에 두고 오는 것이라, 위치를 여기서 캡처해 넘긴다.
        /// (호출 시점이 이동 전이므로 <c>transform.position</c>이 곧 출발 지점이다.)
        /// </summary>
        private void TryLeaveAfterimage()
        {
            if (!isAfterimageActive || isDead) return;
            LeaveAfterimageAsync(transform.position);
        }

        /// <summary>
        /// 잔상 스프라이트를 남기고 지연 후 그 자리에서 폭발시킨다.
        ///
        /// 대시마다 독립된 잔상이 생기므로 <b>재진입 가드를 두지 않는다</b> — 연속 대시로 잔상이 여러 개
        /// 겹치는 것은 결함이 아니라 이 스킬의 그림이다. 각 호출이 자기 위치만 들고 끝까지 가면 된다.
        /// 대기는 Coroutine 금지 규약(ADR-002)에 따라 Awaitable.
        /// </summary>
        private async void LeaveAfterimageAsync(Vector3 position)
        {
            try
            {
                var ghostColor = SynergyAxis.GetColor(SynergyAxis.AXIS_ABYSS);
                ghostColor.a = afterimageGhostAlpha;
                AfterimageEffect.Spawn(ResolvePlayerSr(), position, transform.localScale, ghostColor, afterimageDelay);

                await Awaitable.WaitForSecondsAsync(afterimageDelay, destroyCancellationToken);

                // 대기 중 죽었으면 터뜨리지 않는다 — 사망 후에도 피해가 나가면 결과 화면 뒤에서
                // 킬 수가 늘어나는 식으로 통계가 어긋난다.
                if (isDead) return;

                DetonateAfterimage(position);
            }
            catch (OperationCanceledException)
            {
                // 파괴·씬 전환으로 취소됨. 잔상은 자기 수명으로 스스로 사라지므로 정리할 것이 없다.
            }
        }

        /// <summary>
        /// 잔상 자리에 광역 피해 + 심연색 링. 피해는 <b>약공격 기준</b>으로 산출한다 —
        /// 강공격 기준이면 대시 한 번이 강공격 반값을 무료로 주는 셈이라 공격 버튼을 누를 이유가 줄어든다.
        /// 공격력 버프·메타 강화는 그대로 태워, 성장이 잔상에도 반영되게 한다.
        /// </summary>
        private void DetonateAfterimage(Vector3 position)
        {
            if (afterimageRadius <= 0f) return;

            int damage = Mathf.Max(1, Mathf.RoundToInt(
                lightAttackDamage * AttackMultiplier * MetaAttackMult * afterimageDamageRatio));

            BossAreaEffect.Spawn(position, afterimageRadius,
                                 SynergyAxis.GetColor(SynergyAxis.AXIS_ABYSS), afterimageRingDuration);

            afterimageFilter.useTriggers = Physics2D.queriesHitTriggers;
            int count = Physics2D.OverlapCircle(
                (Vector2)position, afterimageRadius, afterimageFilter, afterimageOverlapBuffer);

            afterimageHitList.Clear();
            for (int i = 0; i < count; i++)
            {
                var col = afterimageOverlapBuffer[i];
                if (col == null) continue;
                var enemy = col.GetComponentInParent<EnemyBase>();
                if (enemy == null || enemy.IsDead) continue;
                if (afterimageHitList.Contains(enemy)) continue;
                afterimageHitList.Add(enemy);
            }

            // 수집을 끝낸 뒤 적용 — 피해로 적이 죽으면 폭발 신학이 끼어들 수 있어(같은 프레임)
            // 순회 중 버퍼가 흔들리는 상황을 애초에 만들지 않는다.
            for (int i = 0; i < afterimageHitList.Count; i++)
            {
                afterimageHitList[i].TakeDamage(damage);
            }
        }

        // ====== 심연 충전 — 폼 교체 시 다음 기본 공격 2배 ======

        /// <summary>
        /// 폼이 바뀌면 다음 한 방을 장전한다. 교체 방향·폼 종류를 가리지 않는다 —
        /// 스킬 설명이 "폼 교체 시"이므로 조건을 더 붙이면 설명이 거짓이 된다.
        /// </summary>
        private void HandleFormSwappedForPassives(FormData previous, FormData next)
        {
            if (!isAbyssChargeActive) return;
            isAbyssChargeReady = true;
        }

        /// <summary>
        /// 기본 공격의 피해에 장전분을 적용하고 소비한다. 적용 후의 피해를 돌려준다.
        ///
        /// <b>적중한 순간에만 소비한다</b>(Combat 파트가 히트 목록을 확정한 뒤 호출) — 허공에 휘두른
        /// 것으로 장전이 풀리면 "다음 공격 2배"라는 약속이 오조작 한 번에 깨진다. 폼을 바꾸고 적에게
        /// 달려가는 동안 버프가 살아 있어야 이 스킬을 쓸 이유가 생긴다.
        ///
        /// 폼 전용 액티브 스킬에는 적용하지 않는다 — 여기(근접 기본 공격)가 유일한 적용 지점이라
        /// 플레이어가 언제 터질지 예측할 수 있고, 발사체·광역 등 경로마다 적용점이 흩어지지 않는다.
        /// </summary>
        private int ConsumeAbyssCharge(int damage)
        {
            if (!isAbyssChargeActive || !isAbyssChargeReady) return damage;

            isAbyssChargeReady = false;
            return Mathf.Max(1, Mathf.RoundToInt(damage * ABYSS_CHARGE_MULTIPLIER));
        }
    }
}
