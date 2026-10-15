using Abyss.Runtime.Form;
using UnityEngine;

namespace Abyss.Runtime.Player
{
    /// <summary>
    /// 플레이어 캐릭터 루트. 책임 분리를 위해 partial로 분할:
    /// Movement / Combat / Health / FormProxy / Context(실행 맥락·입력 잠금) 등.
    /// Run 과 로비가 같은 컴포넌트를 쓴다 — 맥락 차이는 <see cref="PlayerContext"/> 하나로 가른다.
    /// Unity 6: Rigidbody2D.linearVelocity 사용, FindAnyObjectByType 권장.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed partial class PlayerCharacter : MonoBehaviour
    {
        [Header("컴포넌트 참조")]
        [SerializeField] private Rigidbody2D body;
        [SerializeField] private FormController formController;
        [SerializeField] private PlayerStateMachine stateMachine;

        public Rigidbody2D Body => body;
        public PlayerStateMachine StateMachine => stateMachine;

        private void Awake()
        {
            if (body == null) body = GetComponent<Rigidbody2D>();
            if (formController == null) formController = GetComponent<FormController>();
            if (stateMachine == null) stateMachine = GetComponent<PlayerStateMachine>();
            ApplyContext();     // 로비면 폼 플레이타임 등 런 기록을 끈다(PlayerCharacter.Context)
            InitializeHealth();
        }

        private void Update()
        {
            UpdateGrounded();
            UpdateDashTimers();
            UpdateGuard();      // 방향 전환보다 먼저 — 가드 중에는 방향이 고정된다
            UpdateFacing();
            UpdateAttackFlash();
            UpdateBuff();
            UpdateFormCombo();  // P04 A/B/C — 가드 · 접지 판정이 끝난 뒤
        }

        private void FixedUpdate()
        {
            FixedUpdateMovement();
        }

        private void OnDrawGizmosSelected()
        {
            DrawMovementGizmos();
            OnDrawGizmosSelectedCombat();
        }
    }
}
