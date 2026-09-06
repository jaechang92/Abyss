using System.Collections.Generic;
using Abyss.Runtime.Physics;
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

        // 재사용 버퍼 — GroundProbe 가 여기에 결과를 채운다. 한 번 커진 뒤로는 용량을
        // 유지하므로 프레임마다 할당이 없다(옛 NonAlloc 배열이 하던 역할).
        private static readonly List<Collider2D> groundProbeBuffer = new(8);

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
            // 대시 중에는 FixedUpdateMovement가 속도를 dashDirection으로 덮어써 점프가 무효화된다.
            // 점프 횟수만 낭비되지 않도록 대시 종료 후에만 점프를 허용한다(대시-점프 캔슬은 후속 검토).
            if (IsDashing) return;
            if (jumpsRemaining <= 0) return;

            body.linearVelocity = new Vector2(body.linearVelocity.x, jumpForce);
            jumpsRemaining--;
        }

        private void OnDash(InputValue value)
        {
            if (!value.isPressed) return;
            // 시너지 '심연 동료'가 활성이면 쿨다운이 줄어든다(Synergy 파트가 하한과 함께 계산).
            if (Time.time < lastDashTime + EffectiveDashCooldown) return;

            lastDashTime = Time.time;
            dashTimer = dashDuration;
            // 입력 방향 우선, 무입력 시 facing SoT(facingSign)로 대시. transform.localScale 직접 읽기 대신 SoT 사용.
            dashDirection = moveInput.sqrMagnitude > 0.01f
                ? moveInput.normalized
                : new Vector2(facingSign, 0f);

            // Passive '잔상'은 출발 지점에 남는다 — 이동이 시작되기 전인 여기서 호출해야 위치가 맞다.
            TryLeaveAfterimage();
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

            int hitCount = GroundProbe.Overlap(
                groundCheck.position, groundCheckRadius, groundLayer, groundProbeBuffer);

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
            SubscribeSkillEvents();
            SubscribeSynergyEvents();
            SubscribePassiveEvents();
        }

        private void OnDisable()
        {
            if (formController != null)
            {
                formController.OnSwapStarted -= HandleFormSwapStarted;
            }
            UnsubscribeSkillEvents();
            UnsubscribeSynergyEvents();
            UnsubscribePassiveEvents();
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

            body.linearVelocity = new Vector2(moveInput.x * moveSpeed * MoveSpeedMultiplier * FormMoveSpeedMultiplier, body.linearVelocity.y);
        }

        private void DrawMovementGizmos()
        {
            if (groundCheck == null) return;
            Gizmos.color = isGrounded ? Color.green : Color.red;
            Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
        }
    }
}
