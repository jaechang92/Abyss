using Abyss.Runtime.Combat;
using Abyss.Runtime.Feedback;
using UnityEngine;

namespace Abyss.Runtime.Enemy
{
    /// <summary>
    /// 적 상태이상 파트. 현재는 연소(Burn) 하나 — 불꽃 축의 기반 메커닉
    /// (03-skill-draft-system.md §6-1 "적에게 초당 피해, 스택 가능").
    ///
    /// 스택·지속·스택당 피해를 적이 들고 있되 <b>피해량은 부여 시점에 확정된 값을 그대로 쓴다</b>
    /// (<see cref="BurnPayload"/>). 적이 플레이어의 스킬 구성을 조회하지 않게 하기 위함이다.
    /// </summary>
    public partial class EnemyBase
    {
        /// <summary>
        /// 연소 스택 상한. 상한이 없으면 다수 히트 스킬(공허 연사 등)이 반복되는 구간에서
        /// 스택이 선형으로 쌓여 DoT 하나가 모든 직접 피해를 압도한다.
        /// </summary>
        private const int MAX_BURN_STACKS = 10;

        /// <summary>연소 피해 주기(초). 1초 단위로 끊어야 "초당 N 피해"라는 설명과 화면이 일치한다.</summary>
        private const float BURN_TICK_INTERVAL = 1f;

        private const float BURN_RING_RADIUS = 0.45f;
        private const float BURN_RING_DURATION = 0.25f;
        private static readonly Color BURN_COLOR = new(1f, 0.55f, 0.2f, 1f);

        private int burnStacks;
        private float burnRemaining;
        private float burnDamagePerStackPerSecond;
        private float burnTickTimer;

        /// <summary>현재 연소 스택(HUD·후속 시너지 판정용).</summary>
        public int BurnStacks => burnStacks;

        /// <summary>연소 중인지.</summary>
        public bool IsBurning => burnStacks > 0 && burnRemaining > 0f;

        /// <summary>
        /// 연소 부여(중첩). 이미 타고 있으면 스택은 더하고 지속시간은 긴 쪽으로 갱신한다.
        ///
        /// 스택당 피해는 <b>강한 쪽을 유지</b>한다 — 연소 강화를 획득하기 전에 건 불이 남아 있는 상태에서
        /// 강화 후 부여가 약한 값으로 덮이면(또는 그 반대) 이미 붙은 불이 약해져 <i>강화가 손해처럼 보인다</i>.
        /// </summary>
        public void ApplyBurn(BurnPayload payload)
        {
            if (isDead || !payload.HasBurn) return;

            burnStacks = Mathf.Min(MAX_BURN_STACKS, burnStacks + payload.Stacks);
            burnRemaining = Mathf.Max(burnRemaining, payload.Duration);
            burnDamagePerStackPerSecond =
                Mathf.Max(burnDamagePerStackPerSecond, payload.DamagePerStackPerSecond);
        }

        /// <summary>연소 해제(사망·방 전환 등).</summary>
        public void ClearBurn()
        {
            burnStacks = 0;
            burnRemaining = 0f;
            burnDamagePerStackPerSecond = 0f;
            burnTickTimer = 0f;
        }

        /// <summary>
        /// EnemyBase.Update에서 매 프레임 호출. 지속시간을 깎고 1초마다 피해를 준다.
        ///
        /// Time.deltaTime을 쓰므로 정지(timeScale=0) 중에는 진행하지 않는다 — 연소는 전투 상태이지
        /// 정지 화면 위에서 흘러야 하는 UI 연출이 아니다.
        /// </summary>
        private void UpdateBurn()
        {
            if (isDead)
            {
                if (burnStacks > 0) ClearBurn();
                return;
            }
            if (!IsBurning) return;

            burnRemaining -= Time.deltaTime;
            burnTickTimer += Time.deltaTime;

            if (burnTickTimer >= BURN_TICK_INTERVAL)
            {
                burnTickTimer -= BURN_TICK_INTERVAL;
                PulseBurnVisual();

                int tickDamage = Mathf.Max(1, Mathf.RoundToInt(burnStacks * burnDamagePerStackPerSecond));
                TakeBurnDamage(tickDamage);
                if (isDead) return;  // 연소로 사망 — 아래 정리는 Die가 이미 처리
            }

            if (burnRemaining <= 0f) ClearBurn();
        }

        /// <summary>
        /// 연소 표시. <b>EnemyVisuals.Tint를 쓰지 않는다</b> — 틴트 채널은 보스 패턴 예고(telegraph)가
        /// 쓰고 있고, 연소가 매 tick 덮으면 예고 색이 지워져 회피 단서가 흔들린다.
        /// 예고를 흐리게 만드는 대가로 얻는 정보가 아니므로 채널을 분리한다(작은 링).
        /// </summary>
        private void PulseBurnVisual()
        {
            BossAreaEffect.Spawn(transform.position, BURN_RING_RADIUS, BURN_COLOR, BURN_RING_DURATION);
        }

        /// <summary>
        /// 연소 피해. <see cref="TakeDamage"/>와 분리한 이유는 <b>경직</b>이다 —
        /// TakeDamage는 staggerQueued를 세워 FSM을 Stagger로 강제 전이시키는데,
        /// 1초마다 그것이 반복되면 타는 동안 적이 영영 행동하지 못한다(DoT가 곧 하드 CC가 된다).
        /// </summary>
        public void TakeBurnDamage(int amount)
        {
            ApplyDamage(amount, causesStagger: false);
        }
    }
}
