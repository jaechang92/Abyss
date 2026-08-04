using System.Collections.Generic;
using Abyss.Runtime.Combat;
using Abyss.Runtime.Draft;
using Abyss.Runtime.Enemy;
using Abyss.Runtime.Events;
using UnityEngine;

namespace Abyss.Runtime.Player
{
    /// <summary>
    /// 전용 발동 코드가 필요한 Passive 스킬 파트. 현재 2종 —
    /// <b>연소 강화</b>(모든 연소 피해 +50%)와 <b>불꽃 갑옷</b>(받는 피해 10%를 주변 적의 연소로 변환).
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

        /// <summary>연소 강화 배율. 03-skill-draft-system.md §5 #2 "모든 연소 데미지 +50%".</summary>
        private const float BURN_ENHANCEMENT_MULTIPLIER = 1.5f;

        private bool isBurnEnhancementActive;
        private bool isFlameArmorActive;

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

        // PlayerCharacter.Movement의 OnEnable/OnDisable에서 호출(partial 중복 정의 회피).
        private void SubscribePassiveEvents()
        {
            GameEvents.OnSkillDrafted += HandleSkillDraftedForPassives;
        }

        private void UnsubscribePassiveEvents()
        {
            GameEvents.OnSkillDrafted -= HandleSkillDraftedForPassives;
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
    }
}
