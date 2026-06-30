using UnityEngine;
using UnityEngine.InputSystem;

namespace Abyss.Runtime.Lobby
{
    /// <summary>
    /// 로비(허브) 전용 경량 플레이어 컨트롤러. 이동 + 점프만 담당한다.
    /// Run의 PlayerCharacter를 재사용하지 않는 이유: FormController가 매 프레임
    /// RunManager에 폼 플레이타임을 기록해 런 통계를 오염시키고, 전투/스킬/체력 등
    /// 로비에 불필요한 시스템이 딸려온다(architect 자문).
    /// 입력은 PlayerInput SendMessages(OnMove/OnJump). 상호작용은 PlayerInteractor가 담당.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class LobbyPlayerController : MonoBehaviour
    {
        [Header("이동")]
        [SerializeField] private float moveSpeed = 6f;
        [SerializeField] private float jumpForce = 12f;

        [Header("지면 체크")]
        [SerializeField] private Transform groundCheck;
        [SerializeField, Min(0f)] private float groundCheckRadius = 0.12f;
        [SerializeField] private LayerMask groundLayer;

        [SerializeField] private Rigidbody2D body;

        private Vector2 moveInput;
        private bool isGrounded;
        private bool inputLocked;

        private static readonly Collider2D[] groundProbe = new Collider2D[8];

        /// <summary>패널 표시 등으로 이동을 잠글 때 사용.</summary>
        public bool InputLocked
        {
            get => inputLocked;
            set
            {
                inputLocked = value;
                if (value) moveInput = Vector2.zero;
            }
        }

        private void Awake()
        {
            if (body == null) body = GetComponent<Rigidbody2D>();
        }

        // PlayerInput SendMessages
        private void OnMove(InputValue value)
        {
            moveInput = inputLocked ? Vector2.zero : value.Get<Vector2>();
        }

        private void OnJump(InputValue value)
        {
            if (inputLocked || !value.isPressed || !isGrounded || body == null) return;
            body.linearVelocity = new Vector2(body.linearVelocity.x, jumpForce);
        }

        private void Update()
        {
            UpdateGrounded();
            UpdateFacing();
        }

        private void FixedUpdate()
        {
            if (body == null) return;
            float vx = inputLocked ? 0f : moveInput.x * moveSpeed;
            body.linearVelocity = new Vector2(vx, body.linearVelocity.y);
        }

        private void UpdateGrounded()
        {
            isGrounded = false;
            if (groundCheck == null) return;

            int n = Physics2D.OverlapCircleNonAlloc(groundCheck.position, groundCheckRadius, groundProbe, groundLayer);
            for (int i = 0; i < n; i++)
            {
                var c = groundProbe[i];
                if (c == null) continue;
                if (c.transform == transform || c.transform.IsChildOf(transform)) continue;
                isGrounded = true;
                break;
            }
        }

        private void UpdateFacing()
        {
            if (Mathf.Abs(moveInput.x) <= 0.01f) return;
            int sign = moveInput.x > 0f ? 1 : -1;
            var s = transform.localScale;
            if (Mathf.Sign(s.x) != sign)
            {
                transform.localScale = new Vector3(Mathf.Abs(s.x) * sign, s.y, s.z);
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (groundCheck == null) return;
            Gizmos.color = isGrounded ? Color.green : Color.red;
            Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
        }
    }
}
