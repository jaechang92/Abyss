using System.Collections.Generic;
using Abyss.Runtime.Draft;
using Abyss.Runtime.Enemy;
using Abyss.Runtime.Events;
using Abyss.Runtime.Feedback;
using UnityEngine;

namespace Abyss.Runtime.Player
{
    /// <summary>
    /// 시너지 카테고리 스킬(<see cref="SkillCategory.Synergy"/>)의 실제 발동 파트.
    ///
    /// 시너지 효과는 SkillData의 수치 필드(flatBonus/multiplier)로 표현할 수 있는 형태가 아니라
    /// (적 사망 시 폭발·대시 쿨다운 감소) 스킬마다 전용 코드가 필요하다. 그래서 이 파트가
    /// <see cref="SynergyAxis.IsSynergyActive"/>로 활성 여부만 판정하고 효과 자체는 직접 구현한다.
    ///
    /// <b>전용 씬 오브젝트를 만들지 않은 이유</b>: 두 효과가 필요로 하는 것이 플레이어의 대시 쿨다운과
    /// 플레이어 기준 적 탐색이라 소유자가 명백히 플레이어다. 별도 컨트롤러를 두면 씬·빌더를 손대야 하고
    /// (HUD·제단 빌더 재실행 사고 전례), 얻는 것은 파일 분리뿐이다.
    /// </summary>
    public sealed partial class PlayerCharacter
    {
        [Header("시너지 — 폭발 신학 (불꽃 2+)")]
        [Tooltip("적이 죽은 자리에서 터지는 폭발 반경(월드 유닛)")]
        [SerializeField, Min(0f)] private float deathExplosionRadius = 2.2f;
        [Tooltip("폭발 피해 = 죽은 적의 baseHp × 이 비율")]
        [SerializeField, Range(0f, 1f)] private float deathExplosionHpRatio = 0.3f;
        [Tooltip("피해 상한. 보스는 baseHp가 수백이라 비율만 쓰면 폭발 한 번이 방을 지운다.")]
        [SerializeField, Min(1)] private int deathExplosionDamageCap = 60;
        [SerializeField, Min(0.05f)] private float deathExplosionRingDuration = 0.28f;

        /// <summary>심연 동료 대시 쿨다운 감소량(초). 03-skill-draft-system.md §5 #9.</summary>
        private const float ABYSS_ALLY_DASH_CD_REDUCTION = 0.5f;

        /// <summary>
        /// 대시 쿨다운 하한. 기본 대시 CD가 0.5초라 -0.5초를 그대로 적용하면 0이 되는데,
        /// 대시 중에는 FixedUpdateMovement가 속도를 통째로 대입해 <b>중력까지 덮어쓴다</b>.
        /// 쿨다운이 대시 지속(0.15초)에 근접하면 대시가 끊이지 않아 사실상 비행이 되어 지형이 무의미해진다.
        /// 하한을 둬 대시 사이에 중력이 작용하는 구간을 반드시 남긴다.
        /// (기획 문서의 -0.5초는 대시 CD가 정해지기 전에 쓰인 값이다 — 문서보다 실제 이동 수치가 우선.)
        /// </summary>
        private const float DASH_COOLDOWN_FLOOR = 0.3f;

        private bool isExplosiveTheologyActive;
        private bool isAbyssAllyActive;

        // 폭발이 죽인 적이 다시 폭발하는 연쇄를 1단계에서 끊는다. 없으면 밀집 구간에서 무한 재귀로 스택이 넘친다.
        private bool isResolvingDeathExplosion;

        // Combat 파트의 overlapBuffer/reusableHitList를 재사용하지 않는다 —
        // 폭발은 PerformAttack이 히트 목록을 순회하는 도중(적이 죽는 그 시점)에 끼어들므로,
        // 같은 버퍼를 쓰면 진행 중인 순회 대상이 폭발 결과로 덮여 원래 공격의 남은 히트가 사라진다.
        private static readonly Collider2D[] explosionOverlapBuffer = new Collider2D[32];
        private static readonly List<EnemyBase> explosionHitList = new();
        private static ContactFilter2D explosionFilter = new ContactFilter2D().NoFilter();

        /// <summary>대시 쿨다운 감소량(초). 심연 동료 활성 시에만 0보다 크다.</summary>
        public float DashCooldownReduction => isAbyssAllyActive ? ABYSS_ALLY_DASH_CD_REDUCTION : 0f;

        /// <summary>시너지 반영 후 실제 대시 쿨다운. Movement 파트의 OnDash가 이 값을 본다.</summary>
        public float EffectiveDashCooldown =>
            Mathf.Max(DASH_COOLDOWN_FLOOR, dashCooldown - DashCooldownReduction);

        /// <summary>폭발 신학이 발동 조건을 만족한 상태인지(디버그·후속 UI 조회용).</summary>
        public bool IsExplosiveTheologyActive => isExplosiveTheologyActive;

        /// <summary>심연 동료가 발동 조건을 만족한 상태인지(디버그·후속 UI 조회용).</summary>
        public bool IsAbyssAllyActive => isAbyssAllyActive;

        // PlayerCharacter.Movement의 OnEnable/OnDisable에서 호출(partial 중복 정의 회피).
        private void SubscribeSynergyEvents()
        {
            GameEvents.OnSkillDrafted += HandleSkillDraftedForSynergy;
            GameEvents.OnEnemyKilled += HandleEnemyKilledForSynergy;
        }

        private void UnsubscribeSynergyEvents()
        {
            GameEvents.OnSkillDrafted -= HandleSkillDraftedForSynergy;
            GameEvents.OnEnemyKilled -= HandleEnemyKilledForSynergy;
        }

        private void HandleSkillDraftedForSynergy(SkillData skill, DraftTriggerReason reason)
        {
            RecomputeSynergyState();
        }

        /// <summary>
        /// 보유 스킬로부터 시너지 활성 상태를 다시 계산한다. 스킬 획득·교체 시점과 Start에서 호출.
        /// "이벤트는 신호, 값은 조회" 규약 — 드래프트 이벤트는 갱신 시점만 알려주고 실제 보유 목록은
        /// DraftSessionController에서 직접 읽는다(교체로 축이 <b>줄어드는</b> 경우까지 한 경로로 덮는다).
        /// </summary>
        private void RecomputeSynergyState()
        {
            var draft = ResolveDraftSession();
            var owned = draft != null ? draft.Owned : null;

            isExplosiveTheologyActive = SynergyAxis.IsSynergyActive(owned, SynergyAxis.SKILL_EXPLOSIVE_THEOLOGY);
            isAbyssAllyActive = SynergyAxis.IsSynergyActive(owned, SynergyAxis.SKILL_ABYSS_ALLY);
        }

        // ====== 폭발 신학 — [불꽃] 2개+ 시 적 사망 폭발 ======

        private void HandleEnemyKilledForSynergy(EnemyData data, Vector3 position)
        {
            if (isDead || data == null || !isExplosiveTheologyActive) return;
            if (isResolvingDeathExplosion) return;

            isResolvingDeathExplosion = true;
            try
            {
                TriggerDeathExplosion(data, position);
            }
            finally
            {
                isResolvingDeathExplosion = false;
            }
        }

        /// <summary>
        /// 죽은 적의 자리에 광역 피해 + 불꽃색 링. 피해는 죽은 적의 baseHp 기반이라
        /// 강한 적일수록 폭발도 커진다(잡몹 청소가 아니라 "덩치를 터뜨려 주변을 정리"하는 그림).
        /// </summary>
        private void TriggerDeathExplosion(EnemyData source, Vector3 position)
        {
            if (deathExplosionRadius <= 0f) return;

            int damage = Mathf.Clamp(
                Mathf.RoundToInt(source.baseHp * deathExplosionHpRatio), 1, deathExplosionDamageCap);

            BossAreaEffect.Spawn(position, deathExplosionRadius,
                                 SynergyAxis.GetColor(SynergyAxis.AXIS_FIRE), deathExplosionRingDuration);

            // OverlapCircleAll의 매 호출 배열 할당을 피하기 위한 무할당 버퍼. 전역 트리거 설정을 매번 동기화.
            explosionFilter.useTriggers = Physics2D.queriesHitTriggers;
            int count = Physics2D.OverlapCircle((Vector2)position, deathExplosionRadius, explosionFilter, explosionOverlapBuffer);

            explosionHitList.Clear();
            for (int i = 0; i < count; i++)
            {
                var col = explosionOverlapBuffer[i];
                if (col == null) continue;
                var enemy = col.GetComponentInParent<EnemyBase>();
                // 방금 죽은 본인은 IsDead로 자연히 걸러진다(Die가 플래그를 먼저 세운 뒤 이벤트를 발행).
                if (enemy == null || enemy.IsDead) continue;
                if (explosionHitList.Contains(enemy)) continue;
                explosionHitList.Add(enemy);
            }

            // 피해 적용은 수집을 끝낸 뒤에 — 적용 중 사망이 발생해도 위의 재귀 가드가 이 버퍼를
            // 다시 쓰는 것을 막지만, 수집·적용을 섞으면 그 가드에 의존하는 코드가 된다.
            for (int i = 0; i < explosionHitList.Count; i++)
            {
                explosionHitList[i].TakeDamage(damage);
            }
        }
    }
}
