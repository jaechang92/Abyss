using System.Collections.Generic;
using Abyss.Runtime.Draft;
using Abyss.Runtime.Feedback;
using Abyss.Runtime.Run;
using UnityEngine;

namespace Abyss.Runtime.Player
{
    /// <summary>
    /// 무축 보편 Passive(축 태그 없음) 전용 발동 파트 — 신중한 시선·신성 보호·황금 손길.
    /// 운명의 가호는 드래프트 추첨에만 영향을 주므로 <see cref="DraftSessionController"/>가 직접 본다.
    /// Active 시간 왜곡은 데이터(버프 공격 쿨다운 배율)만으로 동작한다.
    ///
    /// 축이 없어 시너지 개수에 잡히지 않는다 — 어느 빌드에도 끼울 수 있는 대신 축을 키워 주지 않는다.
    /// </summary>
    public sealed partial class PlayerCharacter
    {
        [Header("무축 — 신중한 시선")]
        [Tooltip("기본 공격 치명타 확률")]
        [SerializeField, Range(0f, 1f)] private float keenEyeCritChance = 0.1f;
        [Tooltip("치명타 피해 배율")]
        [SerializeField, Min(1f)] private float keenEyeCritMultiplier = 1.5f;

        [Header("무축 — 신성 보호")]
        [Tooltip("피해를 1로 줄인 뒤 다시 준비되기까지의 시간(초)")]
        [SerializeField, Min(0.5f)] private float holyWardInterval = 5f;
        [SerializeField, Min(0f)] private float holyWardRingRadius = 0.9f;
        [SerializeField, Min(0.05f)] private float holyWardRingDuration = 0.2f;

        [Header("무축 — 황금 손길")]
        [Tooltip("적 처치·방 클리어 골드 배율")]
        [SerializeField, Min(1f)] private float goldenTouchMultiplier = 1.25f;

        private static readonly Color HolyWardColor = new(1f, 0.93f, 0.6f, 1f);

        private bool isKeenEyeActive;
        private bool isHolyWardActive;
        private bool isGoldenTouchActive;

        // 신성 보호가 다시 준비되는 시각. 처음 얻으면 바로 준비 상태다(0 이하 = 준비).
        private float holyWardReadyAt;

        /// <summary>신성 보호가 지금 준비돼 있는가(디버그·후속 HUD 조회용).</summary>
        public bool IsHolyWardReady => isHolyWardActive && Time.time >= holyWardReadyAt;

        /// <summary>RecomputePassiveState에서 함께 부른다.</summary>
        private void RecomputeNeutralState(IReadOnlyList<SkillData> owned)
        {
            isKeenEyeActive = SkillIds.IsOwned(owned, SkillIds.KEEN_EYE);
            isHolyWardActive = SkillIds.IsOwned(owned, SkillIds.HOLY_WARD);
            isGoldenTouchActive = SkillIds.IsOwned(owned, SkillIds.GOLDEN_TOUCH);

            // 골드 배율은 RunManager가 지급할 때 곱한다 — 보유가 바뀔 때마다 값을 밀어 둔다(교체로 빠지는 경우 포함).
            if (RunManager.HasInstance)
            {
                RunManager.Instance.CombatGoldMultiplier = isGoldenTouchActive ? goldenTouchMultiplier : 1f;
            }
        }

        // ====== 신중한 시선 — 기본 공격 치명타 ======

        /// <summary>
        /// 적중이 확정된 기본 공격의 피해에 치명타를 굴린다. 근접은 휘두름 한 번에 한 번(맞은 적 모두 같은 결과),
        /// 원거리는 화살이 적에게 닿을 때마다 굴린다.
        /// </summary>
        private int RollKeenEye(int damage)
        {
            if (!isKeenEyeActive || damage <= 0) return damage;
            if (Random.value >= keenEyeCritChance) return damage;

            return Mathf.Max(damage + 1, Mathf.RoundToInt(damage * keenEyeCritMultiplier));
        }

        // ====== 신성 보호 — 주기마다 다음 피격을 1로 ======

        /// <summary>
        /// 방어 버프 다음, 불꽃 갑옷 앞 단계. 1로 줄인 피격에는 변환할 몫이 없다.
        /// 이미 1인 피해에는 쓰지 않는다 — 약공 한 대에 보호막이 소모되면 정작 큰 공격을 못 막는다.
        /// </summary>
        private int ApplyHolyWard(int damage)
        {
            if (!isHolyWardActive || damage <= 1 || Time.time < holyWardReadyAt) return damage;

            holyWardReadyAt = Time.time + holyWardInterval;
            if (holyWardRingRadius > 0f)
            {
                BossAreaEffect.Spawn(transform.position, holyWardRingRadius, HolyWardColor, holyWardRingDuration);
            }
            return 1;
        }
    }
}
