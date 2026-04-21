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

        public bool IsGrounded => isGrounded;
        public Vector2 Velocity => body != null ? body.linearVelocity : Vector2.zero;
        public bool IsDashing => dashTimer > 0f;
        public int FacingSign => facingSign;

        private void OnMove(InputValue value)
        {
            moveInput = value.Get<Vector2>();
        }

        private void OnJump(InputValue value)
        {
            if (!value.isPressed) return;
            if (!isGrounded) return;
            body.linearVelocity = new Vector2(body.linearVelocity.x, jumpForce);
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
            isGrounded = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer) != null;
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
