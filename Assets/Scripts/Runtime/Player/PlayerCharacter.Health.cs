using System;
using Abyss.Runtime.Events;
using Abyss.Runtime.Meta;
using Abyss.Runtime.Run;
using UnityEngine;

namespace Abyss.Runtime.Player
{
    public sealed partial class PlayerCharacter
    {
        [Header("체력 (P-14: RunConfig.baseHp가 SoT, 아래 값은 폴백)")]
        [SerializeField, Min(1)] private int baseHp = 100;

        private int maxHp;          // 메타 업그레이드 보너스가 반영된 실효 최대 HP (런 시작 시 확정)
        private int currentHp;
        private bool isDead;
        private float metaAttackMult = 1f;  // 메타 공격 배율 캐시 (Combat.cs에서 사용)

        /// <summary>실효 최대 HP(메타 보너스 반영). 초기화 전에는 직렬화 baseHp로 폴백. HUD/CheatMenu 최대치.</summary>
        public int BaseHp => maxHp > 0 ? maxHp : baseHp;
        public int CurrentHp => currentHp;
        public bool IsDead => isDead;

        /// <summary>메타 업그레이드 공격 배율(1 = 무보정). Combat 데미지 계산에 곱한다.</summary>
        public float MetaAttackMult => metaAttackMult;

        /// <summary>치트/디버그용 무적. true면 TakeDamage 무시.</summary>
        public bool DebugInvincible { get; set; }

        /// <summary>HP 변화 로컬 이벤트. (previousHp, currentHp).</summary>
        public event Action<int, int> OnHpChanged;

        private void InitializeHealth()
        {
            // 최대 HP = RunConfig.baseHp(SoT) × 시작 폼 hpMultiplier + 메타 업그레이드 보너스.
            // 폼별 HP 차별화는 '시작 폼 고정' 정책: 런 시작 폼 기준으로 여기서 1회 확정하고,
            // 이후 폼 스왑(FormController.RequestSwap)에도 maxHp는 불변이다(악용·회복 없음).
            maxHp = Mathf.RoundToInt(ResolveConfigBaseHp() * ResolveStartingFormHpMultiplier()) + MetaUpgrades.MaxHpBonus();
            metaAttackMult = MetaUpgrades.AttackMultiplier();
            currentHp = maxHp;
            isDead = false;
        }

        /// <summary>
        /// 시작 폼의 hpMultiplier(1 = 무보정). formController 미참조 시 1배로 폴백한다.
        /// Awake 실행 순서와 무관하도록 FormController.ResolveStartingForm으로 해석한다.
        /// </summary>
        private float ResolveStartingFormHpMultiplier()
        {
            var startForm = formController != null ? formController.ResolveStartingForm() : null;
            return startForm != null ? startForm.hpMultiplier : 1f;
        }

        /// <summary>
        /// 기본 최대 HP를 RunConfig(SoT)에서 읽는다. RunManager가 준비돼 있으면 그 config(에디터 오버라이드
        /// 반영본)를 우선하고, 미준비(Bootstrap 미경유 등) 시 공유 RunConfigProvider.Current로 폴백해
        /// 초기화 순서와 무관하게 같은 값을 쓴다. 최후엔 직렬화 로컬 baseHp.
        /// </summary>
        private int ResolveConfigBaseHp()
        {
            var cfg = (RunManager.HasInstance && RunManager.Instance.Config != null)
                ? RunManager.Instance.Config
                : RunConfigProvider.Current;
            return cfg != null ? cfg.baseHp : baseHp;
        }

        /// <summary>
        /// 막을 수 없는 피해. 출처를 모르므로 가드가 끼지 않는다(치트 · 디버그 · 출처가 없는 피해).
        /// 적 공격은 출처를 넘기는 <see cref="TakeDamage(int, Vector2)"/> 를 쓴다.
        /// </summary>
        public void TakeDamage(int amount)
        {
            if (isDead || amount <= 0 || DebugInvincible) return;
            ApplyDamage(amount);
        }

        /// <summary>
        /// 출처가 있는 피해 — 가드 판정이 먼저 돈다(<c>16-shield-guard.md</c>).
        ///
        /// 🔴 <b>가드가 꺼진 폼에서는 <see cref="TakeDamage(int)"/> 과 결과가 같다.</b> 모든 적 공격이 이 경로로 들어오므로
        /// 방패병이 아니면 출처는 무시된다.
        /// </summary>
        /// <param name="sourcePosition">공격이 날아온 위치(정면 판정용).</param>
        public void TakeDamage(int amount, Vector2 sourcePosition)
        {
            if (isDead || amount <= 0 || DebugInvincible) return;

            GuardOutcome outcome = ResolveIncomingGuard(sourcePosition);
            if (outcome == GuardOutcome.None)
            {
                ApplyDamage(amount);
                return;
            }

            int guarded = GuardResolver.ApplyGuard(amount, outcome, CurrentGuardSpec.holdDamageScale);
            if (guarded > 0)
            {
                // 막은 피해는 경직을 안 건다 — 상태 머신이 HP 감소를 보고 Hit 으로 가기 때문에 그 순간만 알린다.
                isSuppressingHitStun = true;
                try { ApplyDamage(guarded); }
                finally { isSuppressingHitStun = false; }
            }

            // 피해를 먼저 처리한다 — 막고도 1 이 남아 죽었으면 반격하지 않는다.
            OnGuardSucceeded(outcome);
        }

        /// <summary>
        /// 지금 들어온 HP 감소가 <b>막은 피해</b>라 경직을 걸면 안 되는가. <see cref="OnHpChanged"/> 가
        /// 동기로 호출되는 동안만 true 다(<c>PlayerStateMachine.HandleHpChanged</c> 가 읽는다).
        /// </summary>
        public bool IsSuppressingHitStun => isSuppressingHitStun;
        private bool isSuppressingHitStun;

        private void ApplyDamage(int amount)
        {
            // 방어 버프(철벽 방어 등) 적용 — 받는 피해 배율. 배율 적용 후에도 최소 1 피해 보장(약공 무효화 방지).
            int mitigated = DefenseMultiplier < 1f
                ? Mathf.Max(1, Mathf.RoundToInt(amount * DefenseMultiplier))
                : amount;

            // 불꽃 갑옷(Passive) — 남은 피해의 일부를 주변 적의 연소로 옮긴다. 버프 경감 '다음' 단계다.
            mitigated = ApplyFlameArmor(mitigated);

            int previous = currentHp;
            currentHp = Mathf.Max(0, currentHp - mitigated);
            OnHpChanged?.Invoke(previous, currentHp);

            // 반격 태세(Synergy) — 실제로 깎인 피해를 기준으로 되돌린다. 불꽃 갑옷과 달리
            // 받는 피해를 줄이지 않으므로 HP 차감 '뒤'가 맞는 자리다.
            // 죽는 피격에서도 한 번은 나간다 — 마지막 일격을 되갚는 그림이 이 축의 성격이고,
            // Die() 전에 두면 사망 처리와 순서가 뒤엉키지 않는다.
            ReflectCounterStance(mitigated);

            if (currentHp <= 0) Die();
        }

        public void Heal(int amount)
        {
            if (isDead || amount <= 0) return;

            int previous = currentHp;
            currentHp = Mathf.Min(maxHp, currentHp + amount);
            OnHpChanged?.Invoke(previous, currentHp);
        }

        /// <summary>
        /// 전투 외 HP 대가(이벤트 선택지 등). <see cref="TakeDamage"/>와 분리한 이유가 둘 있다.
        ///
        /// ① <b>방어 버프가 끼면 안 된다</b> — 제단에 바치는 피가 철벽 방어로 줄어드는 건 말이 안 된다.
        /// ② <b>이걸로 죽으면 안 된다</b> — 선택 화면은 정지 상태(DraftOpen)라, 여기서 사망하면
        ///    런 종료가 드래프트 정지 위에 겹친다. 최소 1은 남긴다.
        /// </summary>
        public void PayHpCost(int amount)
        {
            if (isDead || amount <= 0) return;

            int previous = currentHp;
            currentHp = Mathf.Max(1, currentHp - amount);
            OnHpChanged?.Invoke(previous, currentHp);
        }

        private void Die()
        {
            isDead = true;
            Debug.Log("[Player] 사망 — OnPlayerDead 발행");
            GameEvents.RaisePlayerDead();
        }

        [ContextMenu("Debug: Take 25 Damage")]
        private void DebugTakeDamage()
        {
            TakeDamage(25);
        }

        [ContextMenu("Debug: Heal 25")]
        private void DebugHeal()
        {
            Heal(25);
        }

        [ContextMenu("Debug: Kill")]
        private void DebugKill()
        {
            TakeDamage(currentHp);
        }
    }
}
