using Abyss.Runtime.Flow;
using Abyss.Runtime.Form;
using Abyss.Runtime.Interaction;
using Abyss.Runtime.Player;
using Abyss.Runtime.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Abyss.Runtime.Lobby
{
    /// <summary>
    /// 로비 <b>서비스 어댑터</b>. 이동·점프·대시·방향·접지·애니메이션은 하지 않는다 — 전부 같은 몸의
    /// <see cref="PlayerCharacter"/>(Run 과 같은 컴포넌트)가 한다(2026-10-12 사용자: 로비와 Run 은 같은 캐릭터).
    ///
    /// 이 컴포넌트가 남은 이유는 셋이다.
    /// <list type="number">
    /// <item>NPC·포털·패널이 이 타입을 직렬화 참조로 들고 <see cref="InputLocked"/>를 세운다 — 참조·fileID 를 보존한다.</item>
    /// <item>로비 ESC(메뉴·설정·도감 개폐)의 소유자다. 로비에는 GameFlowController 가 없어 Run 의 정지 요청을 쓸 수 없다.</item>
    /// <item>각인사에서 고른 시작 폼(<see cref="RunStartContext.StartingForm"/>)을 공유 <see cref="FormController"/>에 꽂는다 —
    ///       그림·이동 배율·점프 횟수가 모두 그 한 폼에서 나오게.</item>
    /// </list>
    ///
    /// 🔴 <b>OnMove/OnJump 를 두지 않는다.</b> 같은 PlayerInput 이 SendMessages 로 모든 컴포넌트에 보내므로,
    /// 여기에도 있으면 한 입력이 두 컨트롤러를 움직인다(예전 구조).
    /// </summary>
    public sealed class LobbyPlayerController : MonoBehaviour, IInteractionBlocker
    {
        // 명시적 구현 — 공개 API는 InputLocked 하나로 둔다.
        // 같은 뜻의 프로퍼티가 둘로 보이면 어느 쪽을 세팅해야 하는지가 헷갈린다.
        bool IInteractionBlocker.BlocksInteraction => InputLocked;

        [Tooltip("같은 몸의 공유 플레이어. 비우면 자기 자신에서 찾는다.")]
        [SerializeField] private PlayerCharacter player;

        private bool inputLocked;

        /// <summary>발밑이 지면에 닿아 있는가. 공유 플레이어의 판정을 그대로 돌려준다(카메라·귀환 연출이 읽는다).</summary>
        public bool IsGrounded => player != null && player.IsGrounded;

        /// <summary>
        /// 패널 표시 등으로 조작을 잠근다. 공유 플레이어의 이동·점프·대시·공격·폼 교체·가드가 함께 잠기고,
        /// 잠글 때 진행 중인 대시·이동은 즉시 멈춘다(<see cref="PlayerCharacter.SetInputLocked"/>).
        /// </summary>
        public bool InputLocked
        {
            get => inputLocked;
            set
            {
                inputLocked = value;
                if (player != null) player.SetInputLocked(value);
            }
        }

        private void Awake()
        {
            if (player == null) player = GetComponent<PlayerCharacter>();
            if (player == null)
            {
                Debug.LogError("[LobbyPlayerController] 같은 오브젝트에 PlayerCharacter 가 없다 — 로비 캐릭터가 움직이지 않는다. " +
                               "Abyss Tools 의 로비 씬 빌더로 다시 만들거나 PlayerCharacter(context=Lobby)를 붙일 것.");
            }
            else if (player.IsRunContext)
            {
                Debug.LogWarning("[LobbyPlayerController] 공유 PlayerCharacter 의 맥락이 Run 이다 — 로비에서 런 기록이 남을 수 있다. context=Lobby 로 둘 것.");
            }
        }

        private void Update()
        {
            SyncStartingForm();
        }

        /// <summary>
        /// 각인사에서 고른 시작 폼을 공유 폼 슬롯에 반영한다. 미선택이면 씬에 깔린 기본 폼(Run 이 실제로 시작하는 폼)을 둔다.
        /// 활성 슬롯 하나만 바꾼다 — 로비에 두 번째 폼을 지급하지 않는다(교체 키는 런의 「교체 대상 없음」과 같이 거절된다).
        /// RunStartContext 는 이벤트가 없는 정적 컨텍스트라 참조 비교로 미러링한다(<see cref="LobbyFormVisual"/>과 같은 이유).
        /// </summary>
        private void SyncStartingForm()
        {
            // 맥락이 Run 이면 Awake 경고로 끝낸다 — 로비 전용 진입점이 매 프레임 거절 로그를 내지 않게.
            if (player == null || player.Form == null || player.IsRunContext || !RunStartContext.HasStartingForm) return;

            var form = player.Form;
            var selected = RunStartContext.StartingForm;
            if (ReferenceEquals(form.CurrentForm, selected)) return;
            // 장착과 점프 횟수 갱신을 한 번에 — EquipForm 직접 호출은 서 있는 동안 옛 점프 횟수를 남긴다.
            player.EquipLobbyForm(selected);
        }

        /// <summary>
        /// ESC(Player 맵 Pause). 로비에는 GameFlowController가 없어 Run의 일시정지 구조를 쓸 수 없으므로
        /// 이 어댑터가 메뉴 개폐를 직접 소유한다. 공유 PlayerCharacter 는 로비 맥락에서 정지 요청을 내지 않는다.
        /// 메뉴가 떠 있어도 입력 맵은 Player 그대로 두기 때문에 닫는 ESC도 같은 액션으로 도착한다.
        ///
        /// 가장 안쪽에 열린 것부터 닫는다: 설정 → 도감 → 메뉴.
        /// </summary>
        private void OnPause(InputValue value)
        {
            if (!value.isPressed) return;
            // 저장 모달이 떠 있으면 그 키는 모달 몫이다(SaveStatusOverlay.IsCapturingInput).
            if (SaveStatusOverlay.IsCapturingInput) return;
            if (KeyboardBindingsPanel.IsOpen || KeyboardBindingsPanel.WasInputConsumedThisFrame) return;

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
    }
}
