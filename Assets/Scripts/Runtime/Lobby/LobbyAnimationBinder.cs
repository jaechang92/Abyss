using Abyss.Runtime.Player;
using Anim.Core;
using UnityEngine;

namespace Abyss.Runtime.Lobby
{
    /// <summary>
    /// 로비 캐릭터의 움직임을 폼 애니메이션(대기·달리기·점프·낙하)으로 비춘다.
    ///
    /// 런의 <see cref="PlayerAnimationBinder"/>는 <b>FSM 상태 전이</b>를 듣는다. 로비에는 그 FSM이 없다 —
    /// 전투·피격·대시가 없어 <see cref="LobbyPlayerController"/>가 이동만 직접 한다(그 주석 참조).
    /// 그래서 여기서는 <b>물리 상태에서 자세를 파생</b>한다: 공중이면 상승/하강, 땅이면 이동/정지.
    /// 상태 이름·폴백 사슬은 런과 같은 <see cref="PlayerAnimationIds"/>를 쓴다 — 같은 폼이 두 씬에서
    /// 같은 규칙으로 그려져야 한다(Jump 그림이 없는 폼은 두 곳 모두 Fall로 물러난다).
    ///
    /// 폴링인 이유는 <see cref="LobbyFormVisual"/>과 같다 — 구독할 발행자가 없고, 같은 이름이면
    /// <see cref="AnimatorDriver.Play"/>가 바로 돌아가므로 프레임당 비용은 비교 몇 번이다.
    /// </summary>
    public sealed class LobbyAnimationBinder : MonoBehaviour
    {
        // 발판 끝에서 한두 프레임 떨리는 속도로 달리기/대기가 깜빡이지 않게 둔 문턱.
        private const float MOVE_THRESHOLD = 0.1f;
        private const float RISE_THRESHOLD = 0.01f;

        [SerializeField] private LobbyPlayerController controller;
        [SerializeField] private Rigidbody2D body;
        [Tooltip("비우면 자식(Visual)에서 찾는다.")]
        [SerializeField] private AnimatorDriver animatorDriver;

        private void Awake()
        {
            if (controller == null) controller = GetComponent<LobbyPlayerController>();
            if (body == null) body = GetComponent<Rigidbody2D>();
            if (animatorDriver == null) animatorDriver = GetComponentInChildren<AnimatorDriver>(true);
        }

        private void OnEnable()
        {
            if (animatorDriver != null) animatorDriver.OnClipsChanged += HandleClipsChanged;
        }

        private void OnDisable()
        {
            if (animatorDriver != null) animatorDriver.OnClipsChanged -= HandleClipsChanged;
        }

        private void Update()
        {
            Apply(restart: false);
        }

        /// <summary>
        /// 폼이 바뀌어 클립 한 벌이 갈렸다. 자세 이름이 그대로여도(대기 → 대기) 새 폼의 클립으로 다시 튼다 —
        /// 안 그러면 이름 비교에 걸려 이전 폼의 마지막 그림에 머문다.
        /// </summary>
        private void HandleClipsChanged()
        {
            Apply(restart: true);
        }

        private void Apply(bool restart)
        {
            if (animatorDriver == null || body == null) return;

            string animationId = AnimationChainResolver.Resolve(PlayerAnimationIds.FallbackChain(ResolveStateId()), animatorDriver);
            // 사슬이 전부 비었다(그림이 아직 없는 폼) — 정지 그림을 그대로 둔다.
            if (string.IsNullOrEmpty(animationId)) return;

            animatorDriver.Play(animationId, restart, immediate: false);
        }

        private string ResolveStateId()
        {
            Vector2 velocity = body.linearVelocity;
            bool isGrounded = controller == null || controller.IsGrounded;

            if (!isGrounded) return velocity.y > RISE_THRESHOLD ? PlayerStateIds.Jump : PlayerStateIds.Fall;
            return Mathf.Abs(velocity.x) > MOVE_THRESHOLD ? PlayerStateIds.Run : PlayerStateIds.Idle;
        }
    }
}
