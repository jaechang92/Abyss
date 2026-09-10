using Anim.Core;
using UnityEngine;

namespace Abyss.Runtime.Player
{
    /// <summary>
    /// <b>FSM의 결론을 재생기로 옮기는 얇은 이음매.</b> 판단은 하지 않는다 —
    /// 어느 상태인지는 <see cref="PlayerStateMachine"/>이, 무엇으로 물러날지는
    /// <see cref="PlayerAnimationIds"/>가, 어떻게 트는지는 <see cref="IAnimationDriver"/>가 안다.
    /// 이 클래스가 아는 것은 <b>그 셋을 잇는 순서</b>뿐이다.
    ///
    /// 🔑 <b>얇게 두는 것이 목적이다.</b> 여기에 조건을 하나 넣기 시작하면
    /// "지금 무슨 애니메이션인가"의 판단이 FSM과 여기 둘로 갈린다 — 이 구조가 피하려던 바로 그 모양이다.
    /// </summary>
    public sealed class PlayerAnimationBinder : MonoBehaviour
    {
        [SerializeField] private PlayerStateMachine stateMachine;
        [SerializeField] private AnimatorDriver animatorDriver;

        private IAnimationDriver driver;

        private void Awake()
        {
            if (stateMachine == null) stateMachine = GetComponent<PlayerStateMachine>();
            // 그림은 폼 몸을 담는 자식(Visual)에 있으므로 재생기도 그쪽에 붙는다.
            if (animatorDriver == null) animatorDriver = GetComponentInChildren<AnimatorDriver>(true);
            driver = animatorDriver;
        }

        private void OnEnable()
        {
            if (stateMachine != null && stateMachine.Machine != null)
            {
                stateMachine.Machine.OnStateChanged += HandleStateChanged;
            }
            if (driver != null)
            {
                driver.OnClipsChanged += HandleClipsChanged;
            }

            // 🔑 구독은 놓쳐도 되지만 켜질 때의 갱신은 놓치면 안 된다.
            //    꺼졌다 켜지는 동안 상태가 바뀌었으면 이벤트는 이미 지나갔고,
            //    화면만 옛 그림으로 남는다. 설정 패널에서 같은 자리를 이미 한 번 겪었다.
            ApplyCurrent();
        }

        private void OnDisable()
        {
            if (stateMachine != null && stateMachine.Machine != null)
            {
                stateMachine.Machine.OnStateChanged -= HandleStateChanged;
            }
            if (driver != null)
            {
                driver.OnClipsChanged -= HandleClipsChanged;
            }
        }

        /// <summary>
        /// 🔴 <paramref name="oldStateId"/>와 <paramref name="newStateId"/>가 <b>같을 수 있다.</b>
        /// 연속 공격이 그 경로다 — 이미 AttackLight일 때 다시 공격하면 FSM은 강제 전이를 걸고,
        /// 이름은 그대로인 채 이벤트만 온다. 이름 비교로 걸러내면 <b>두 번째 타격에 그림이 안 움직인다.</b>
        /// </summary>
        private void HandleStateChanged(string oldStateId, string newStateId)
        {
            Apply(newStateId);
        }

        /// <summary>
        /// 폼이 바뀌어 클립 한 벌이 갈렸다. <b>재생 중이던 것이 새 폼에는 없을 수 있으므로</b>
        /// 상태는 그대로여도 다시 고른다 — 안 하면 이전 폼의 마지막 그림에 얼어붙는다.
        /// </summary>
        private void HandleClipsChanged()
        {
            ApplyCurrent();
        }

        private void ApplyCurrent()
        {
            if (stateMachine == null) return;
            Apply(stateMachine.CurrentStateId);
        }

        private void Apply(string playerStateId)
        {
            if (driver == null || string.IsNullOrEmpty(playerStateId)) return;

            var chain = PlayerAnimationIds.FallbackChain(playerStateId);
            string animationId = AnimationChainResolver.Resolve(chain, driver);

            // 사슬이 전부 비었다 — 이 폼은 아직 아무것도 안 그려졌다.
            // 직전 그림을 그대로 두는 편이 낫다. 지우면 캐릭터가 화면에서 사라진다.
            if (string.IsNullOrEmpty(animationId)) return;

            driver.Play(
                animationId,
                PlayerAnimationIds.RestartsOnReenter(animationId),
                PlayerAnimationIds.IsOneShot(animationId));
        }
    }
}
