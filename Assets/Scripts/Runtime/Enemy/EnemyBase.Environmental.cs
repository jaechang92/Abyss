using FSM.Core;
using UnityEngine;

namespace Abyss.Runtime.Enemy
{
    /// <summary>환경형 확장 이음매. 기존 적은 기본 무동작으로 기존 FSM·공격 순서를 유지한다.</summary>
    public partial class EnemyBase
    {
        protected StateMachine CombatStateMachine => fsm;

        /// <summary>Update의 경직 수락 전 FixedUpdate 타격도 차단한다. 경직 면역은 기존 판정을 따른다.</summary>
        protected bool IsCombatInterruptionPending => staggerQueued
            && !EnemyTimedState.IsStaggerIgnored(ResistsStaggerWhileAttacking,
                fsm != null ? fsm.CurrentStateId : string.Empty, Time.time, timedStateExitTime);

        protected virtual string InitialStateId => EnemyStateIds.Patrol;

        // Awake에서 호출된다. 상태 등록만 하고 Start 초기화 데이터에 의존하지 않는다.
        protected virtual void RegisterAdditionalStates(StateMachine machine) { }

        protected virtual bool TryEvaluateSpecialTransitions(string current) => false;

        protected virtual bool TryMoveSpecialState(string current, float dt) => false;

        // 실제 수락된 경직만 호출한다. 연소나 면역으로 버린 요청은 공격을 취소하지 않는다.
        protected virtual void OnCombatInterrupted() { }

        protected virtual void StopDeadMotion() => StopHorizontal();

        // Attack partial의 기존 OnDisable에서 연사 세대 무효화 다음에 호출한다.
        protected virtual void OnCombatDisabled() { }

        protected virtual void OnCombatDestroyed() { }

        private void OnDestroy() => OnCombatDestroyed();

        /// <summary>일반 공격과 같은 DT008 클립/보스 제외 정책. 유효 환경형 Strike 시작에만 1회 호출한다.</summary>
        protected void PlayPatternAttackSfx() => PlayAttackSfx();
    }
}
