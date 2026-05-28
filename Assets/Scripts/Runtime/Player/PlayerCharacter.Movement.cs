using UnityEngine;
using UnityEngine.InputSystem;

namespace Abyss.Runtime.Player
{
    public sealed partial class PlayerCharacter
    {
        [Header("이동 (프로토 기본값)")]
        [SerializeField] private float moveSpeed = 6f;
        [SerializeField] private float jumpForce = 12f;
        [SerializeField] private float dashSpeed = 16f;
        [SerializeField, Min(0f)] private float dashDuration = 0.15f;
        [SerializeField, Min(0f)] private float dashCooldown = 0.5f;

        [Header("지면 체크")]
        [SerializeField] private Transform groundCheck;
        [SerializeField, Min(0f)] private float groundCheckRadius = 0.1f;
        [SerializeField] private LayerMask groundLayer;

        private Vector2 moveInput;
        private bool isGrounded;
        private float dashTimer;
        private float lastDashTime = -999f;
        private Vector2 dashDirection;
        private int facingSign = 1;
        private int jumpsRemaining;

        private static readonly Collider2D[] groundProbeBuffer = new Collider2D[8];

        public bool IsGrounded => isGrounded;
        public Vector2 Velocity => body != null ? body.linearVelocity : Vector2.zero;
        public bool IsDashing => dashTimer > 0f;
        public int FacingSign => facingSign;
        public int JumpsRemaining => jumpsRemaining;

        private void OnMove(InputValue value)
        {
            moveInput = value.Get<Vector2>();
        }

        private void OnJump(InputValue value)
        {
            if (!value.isPressed) return;
            if (jumpsRemaining <= 0) return;

            body.linearVelocity = new Vector2(body.linearVelocity.x, jumpForce);
            jumpsRemaining--;
        }

        private void OnDash(InputValue value)
        {
            if (!value.isPressed) return;
            if (Time.time < lastDashTime + dashCooldown) return;

            lastDashTime = Time.time;
            dashTimer = dashDuration;
            dashDirection = moveInput.sqrMagnitude > 0.01f
                ? moveInput.normalized
                : new Vector2(Mathf.Sign(transform.localScale.x), 0f);
        }

        private void UpdateGrounded()
        {
            if (groundCheck == null)
            {
                isGrounded = false;
                return;
            }

            bool wasGrounded = isGrounded;
            isGrounded = false;

            // GC 회피를 위해 NonAlloc 버전 사용.
            int hitCount = Physics2D.OverlapCircleNonAlloc(
                groundCheck.position, groundCheckRadius, groundProbeBuffer, groundLayer);

            for (int i = 0; i < hitCount; i++)
            {
                var col = groundProbeBuffer[i];
                if (col == null) continue;
                // Enemy 콜라이더는 ground로 인정하지 않음 — 적 위에서 무한 점프 방지.
                if (col.GetComponentInParent<Abyss.Runtime.Enemy.EnemyBase>() != null) continue;
                // 자기 자신 콜라이더 제외.
                if (col.transform == transform || col.transform.IsChildOf(transform)) continue;
                isGrounded = true;
                break;
            }

            // 접지 진입 시 점프 카운터 리셋 — 현재 폼의 jumpCount 기반 (기획 02-form-change-system.md).
            if (isGrounded && !wasGrounded)
            {
                int maxJumps = (formController != null && formController.CurrentForm != null)
                    ? Mathf.Max(1, formController.CurrentForm.jumpCount)
                    : 1;
                jumpsRemaining = maxJumps;
            }
        }

        private void UpdateDashTimers()
        {
            if (dashTimer > 0f) dashTimer -= Time.deltaTime;
        }

        /// <summary>
        /// 이동 입력 기반 facing 전환. 자식 attackPoint/AttackEffect가 함께 뒤집히도록 localScale.x 부호만 반전.
        /// </summary>
        private void UpdateFacing()
        {
            if (Mathf.Abs(moveInput.x) <= 0.01f) return;

            int desiredSign = moveInput.x > 0f ? 1 : -1;
            if (desiredSign == facingSign) return;

            facingSign = desiredSign;
            Vector3 s = transform.localScale;
            transform.localScale = new Vector3(Mathf.Abs(s.x) * desiredSign, s.y, s.z);
        }

        private void OnEnable()
        {
            if (formController != null)
            {
                formController.OnSwapStarted += HandleFormSwapStarted;
            }
        }

        private void OnDisable()
        {
            if (formController != null)
            {
                formController.OnSwapStarted -= HandleFormSwapStarted;
            }
        }

        /// <summary>
        /// 폼 교체 시작 시점에 호출. 새 폼의 jumpCount로 점프 카운터를 즉시 리셋한다.
        /// 공중에서 폼을 바꿔도 새 폼 기준 점프 횟수가 즉시 적용된다.
        /// </summary>
        private void HandleFormSwapStarted(Abyss.Runtime.Form.FormData previous, Abyss.Runtime.Form.FormData next)
        {
            if (next == null) return;
            jumpsRemaining = Mathf.Max(1, next.jumpCount);
        }

        private void FixedUpdateMovement()
        {
            if (body == null) return;

            if (dashTimer > 0f)
            {
                body.linearVelocity = dashDirection * dashSpeed;
                return;
            }

            body.linearVelocity = new Vector2(moveInput.x * moveSpeed, body.linearVelocity.y);
        }

        private void DrawMovementGizmos()
        {
            if (groundCheck == null) return;
            Gizmos.color = isGrounded ? Color.green : Color.red;
            Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
        }
    }
}
