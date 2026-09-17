using Anim.Core;
using FSM.Core;
using UnityEngine;

namespace Abyss.Runtime.Enemy
{
    /// <summary>
    /// <b>적 FSM 의 결론을 재생기로 옮기는 얇은 이음매</b> — <c>PlayerAnimationBinder</c> 와 같은 역할이다.
    /// 어느 상태인지는 <see cref="EnemyBase"/> 가, 무엇으로 물러날지는 <see cref="EnemyAnimationIds"/> 가 안다.
    ///
    /// 📌 애니메이션이 있는 적에만 붙는다(<c>EnemyAnimationBuilder</c> 가 배선). 없는 적은 정지 그림 그대로다.
    /// </summary>
    public sealed class EnemyAnimationBinder : MonoBehaviour
    {
        [SerializeField] private StateMachine stateMachine;
        [SerializeField] private AnimatorDriver animatorDriver;

        private IAnimationDriver driver;

        private void Awake()
        {
            if (stateMachine == null) stateMachine = GetComponent<StateMachine>();
            // 그림은 발밑에 맞춘 자식(Visual)에 있으므로 재생기도 그쪽에 붙는다.
            if (animatorDriver == null) animatorDriver = GetComponentInChildren<AnimatorDriver>(true);
            driver = animatorDriver;
        }

        private void OnEnable()
        {
            if (stateMachine != null) stateMachine.OnStateChanged += HandleStateChanged;
            if (driver != null) driver.OnClipsChanged += HandleClipsChanged;
            ApplyCurrent();
        }

        private void OnDisable()
        {
            if (stateMachine != null) stateMachine.OnStateChanged -= HandleStateChanged;
            if (driver != null) driver.OnClipsChanged -= HandleClipsChanged;
        }

        /// <summary>🔴 old == new 가 온다 — 경직 중 다시 맞으면 같은 이름으로 강제 전이된다. 거르지 않는다.</summary>
        private void HandleStateChanged(string oldStateId, string newStateId)
        {
            Apply(newStateId);
        }

        private void HandleClipsChanged()
        {
            ApplyCurrent();
        }

        private void ApplyCurrent()
        {
            if (stateMachine == null) return;
            Apply(stateMachine.CurrentStateId);
        }

        private void Apply(string enemyStateId)
        {
            if (driver == null || string.IsNullOrEmpty(enemyStateId)) return;

            string animationId = AnimationChainResolver.Resolve(EnemyAnimationIds.FallbackChain(enemyStateId), driver);
            if (string.IsNullOrEmpty(animationId)) return;

            driver.Play(
                animationId,
                EnemyAnimationIds.RestartsOnReenter(animationId),
                EnemyAnimationIds.IsOneShot(animationId));
        }
    }
}
