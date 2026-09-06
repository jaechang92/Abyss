using System.Collections.Generic;
using Abyss.Runtime.Interaction;
using Abyss.Runtime.Physics;
using Abyss.Runtime.UI;
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
    public sealed class LobbyPlayerController : MonoBehaviour, IInteractionBlocker
    {
        // 명시적 구현 — 공개 API는 InputLocked 하나로 둔다.
        // 같은 뜻의 프로퍼티가 둘로 보이면 어느 쪽을 세팅해야 하는지가 헷갈린다.
        bool IInteractionBlocker.BlocksInteraction => InputLocked;

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

        // 재사용 버퍼 — GroundProbe 가 채운다(런의 PlayerCharacter 와 같은 규약).
        private static readonly List<Collider2D> groundProbe = new(8);

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

        /// <summary>
        /// ESC(Player 맵 Pause). 로비에는 GameFlowController가 없어 Run의 일시정지 구조를 쓸 수 없으므로
        /// 이 컨트롤러가 메뉴 개폐를 직접 소유한다. 메뉴가 떠 있어도 입력 맵은 Player 그대로 두기 때문에
        /// 닫는 ESC도 같은 액션으로 도착한다(Run처럼 Cancel로 갈라지지 않는다).
        ///
        /// 가장 안쪽에 열린 것부터 닫는다: 설정 → 도감 → 메뉴.
        /// </summary>
        private void OnPause(InputValue value)
        {
            if (!value.isPressed) return;

            if (SettingsPanel.IsOpen)
            {
                SettingsPanel.Close();
                return;
            }
            // 설정 패널이 자기 Update에서 먼저 닫았다면 이번 ESC는 이미 소비된 것이다(순서 역전 가드).
            if (SettingsPanel.WasClosedThisFrame) return;

            // 도감도 ESC를 자체 소유하므로 설정과 같은 양방향 가드를 둔다.
            if (CodexPanel.IsOpen)
            {
                CodexPanel.Close();
                return;
            }
            if (CodexPanel.WasClosedThisFrame) return;

            if (LobbyMenuPanel.IsOpen)
            {
                LobbyMenuPanel.Close();
                return;
            }

            // 대화·폼 선택 등 다른 패널이 화면을 점유한 동안에는 메뉴를 열지 않는다.
            // 그 패널들이 InputLocked의 소유자라, 메뉴를 닫으며 잠금을 풀면 소유권이 어긋난다.
            if (inputLocked) return;

            InputLocked = true;
            LobbyMenuPanel.Open(() => InputLocked = false);
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

            int n = GroundProbe.Overlap(groundCheck.position, groundCheckRadius, groundLayer, groundProbe);
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
