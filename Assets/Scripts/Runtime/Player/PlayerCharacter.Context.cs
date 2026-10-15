using Abyss.Runtime.Input;
using Abyss.Runtime.Interaction;
using Abyss.Runtime.UI;
using UnityEngine;

namespace Abyss.Runtime.Player
{
    /// <summary>
    /// 실행 맥락(<see cref="PlayerContext"/>)과 외부 입력 잠금 파트.
    ///
    /// 🔑 <b>로비도 이 캐릭터를 그대로 쓴다.</b> 예전 로비 전용 컨트롤러(LobbyPlayerController)가 이동·점프를
    /// 따로 들고 있어 두 씬의 조작감·대시·애니메이션이 갈렸다. 이제 그 컴포넌트는 NPC·메뉴가 들고 있는
    /// 직렬화 참조를 받는 <b>잠금 어댑터</b>일 뿐이고, 잠금은 여기 <see cref="SetInputLocked"/> 하나로 들어온다.
    ///
    /// 🔴 <b>잠금은 "입력을 무시"만으로 끝나지 않는다.</b> 대시 도중 창이 열리면 대시 속도가 남아 미끄러지고,
    /// 잠금 전에 누른 방향키는 moveInput 에 남아 창을 닫는 순간 캐릭터가 저절로 걷는다. 그래서 잠글 때
    /// 이동·대시·접근·가드를 즉시 거두고, 풀 때는 <b>새 입력이 올 때까지</b> 움직이지 않는다.
    /// </summary>
    public sealed partial class PlayerCharacter : IInteractionBlocker
    {
        [Header("실행 맥락")]
        [Tooltip("Run = 던전(기본). Lobby = 허브 — 이동·FSM·애니메이션은 같고 런 기록·보상·튜토리얼·일시정지 요청은 건드리지 않는다.")]
        [SerializeField] private PlayerContext context = PlayerContext.Run;

        private bool isInputLocked;

        // 잠금이 풀린 뒤 「누른 채로 남은」 가드 키를 새 입력으로 보지 않기 위한 표시. 한 번 떼면 풀린다.
        private bool isAwaitingHeldInputRelease;

        public PlayerContext Context => context;

        /// <summary>런 기록·보상·튜토리얼·일시정지 요청을 내도 되는 맥락인가. 로비면 false.</summary>
        public bool IsRunContext => context == PlayerContext.Run;

        /// <summary>외부(로비 NPC·메뉴 등)가 조작을 잠갔는가. FSM 의 공격·폼 교체 게이트도 이것을 본다.</summary>
        public bool IsInputLocked => isInputLocked;

        /// <summary>직접 잠금과 런 모달의 입력 소유권을 상호작용에도 적용한다.</summary>
        public bool BlocksInteraction
        {
            get
            {
                if (isInputLocked) return true;
                if (!IsRunContext) return false;
                var router = InputRouter.GetInstanceSafe();
                return (router != null && router.CurrentMode != InputRouter.InputMode.Gameplay) ||
                    SaveStatusOverlay.IsCapturingInput ||
                    SettingsPanel.IsOpen || SettingsPanel.WasClosedThisFrame ||
                    CodexPanel.IsOpen || CodexPanel.WasClosedThisFrame;
            }
        }

        /// <summary>
        /// 조작 잠금. 잠글 때 진행 중인 이동·대시·접근·가드를 즉시 멈추고, 풀 때는 잠금 전·중에 눌러 둔 입력을 버린다.
        /// 같은 값으로 다시 불러도 안전하다(멱등).
        /// </summary>
        public void SetInputLocked(bool locked)
        {
            if (isInputLocked == locked) return;
            isInputLocked = locked;

            // 잠금·해제 모두 방향 입력을 비운다 — 해제 후에는 새 OnMove 가 와야만 움직인다.
            moveInput = Vector2.zero;
            if (!locked)
            {
                isAwaitingHeldInputRelease = true;
                return;
            }

            bool wasDashing = IsDashing;
            dashTimer = 0f;
            CancelLunge("입력 잠금");
            EndGuard();
            if (body != null)
            {
                // 대시는 대각선으로도 나가므로 세로 속도까지 거둔다. 일반 이동이면 중력 낙하는 그대로 둔다.
                body.linearVelocity = new Vector2(0f, wasDashing ? 0f : body.linearVelocity.y);
            }
        }

        /// <summary>
        /// 로비 전용 폼 반영 진입점 — 각인사 선택을 활성 슬롯에 꽂고 점프 횟수를 새 폼에 맞춘다.
        ///
        /// 🔴 <b>EquipForm 만으로는 점프 횟수가 안 바뀐다.</b> EquipForm 은 OnSwapStarted 를 내지 않고,
        /// <see cref="UpdateGrounded"/> 는 착지하는 순간에만 횟수를 채운다. 서 있는 채로 1단→2단 폼을 고르면
        /// 다음 착지 전까지 옛 횟수로 뛴다. 그래서 접지 중이면 새 폼 횟수로 바로 채우고,
        /// 공중이면 새 폼 최대치를 넘지 않게 <b>줄이기만</b> 한다(폼을 바꿔 공중 점프를 다시 얻지 못하게).
        /// Run 은 이 경로를 쓰지 않는다 — 런의 장착·교체는 기존 FormController 흐름 그대로다.
        /// </summary>
        public void EquipLobbyForm(Abyss.Runtime.Form.FormData form)
        {
            if (form == null || formController == null) return;
            if (IsRunContext)
            {
                Debug.LogWarning("[PlayerCharacter] EquipLobbyForm 은 로비 맥락 전용이다 — Run 에서는 무시한다.");
                return;
            }

            formController.EquipForm(form, formController.ActiveSlot, activate: true);

            int maxJumps = Mathf.Max(1, form.jumpCount);
            jumpsRemaining = isGrounded ? maxJumps : Mathf.Min(jumpsRemaining, maxJumps);
        }

        /// <summary>
        /// 맥락을 폼 계층에 알린다(Awake). 폼은 Player 를 모르므로 값만 넘긴다 — 의존 방향 유지.
        /// </summary>
        private void ApplyContext()
        {
            if (formController != null) formController.SetRunContext(IsRunContext);
        }

        /// <summary>
        /// 눌린 상태를 직접 읽는 입력(가드)의 잠금 필터. 잠긴 동안은 false, 풀린 뒤에는 <b>한 번 뗄 때까지</b> false.
        /// </summary>
        private bool FilterHeldInput(bool isPressed)
        {
            if (isInputLocked) return false;
            if (!isAwaitingHeldInputRelease) return isPressed;
            if (!isPressed) isAwaitingHeldInputRelease = false;
            return false;
        }
    }
}
