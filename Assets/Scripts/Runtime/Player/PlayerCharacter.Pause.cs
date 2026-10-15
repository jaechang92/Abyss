using Abyss.Runtime.Events;
using Abyss.Runtime.UI;
using UnityEngine.InputSystem;

namespace Abyss.Runtime.Player
{
    /// <summary>
    /// 일시정지 입력을 이벤트로 옮기는 얇은 어댑터(FormProxy와 동형).
    ///
    /// 입력이 두 갈래인 이유: 정지에 들어가면 InputRouter가 UI 모드로 전환해 Player 맵을 끄므로,
    /// 정지를 여는 ESC(Player 맵 Pause)와 닫는 ESC(UI 맵 Cancel)가 서로 다른 액션으로 도착한다.
    /// 어느 쪽이든 요청만 발행하고, 수락 여부는 GameFlowController가 현재 상태를 보고 판정한다.
    /// </summary>
    public sealed partial class PlayerCharacter
    {
        private void OnPause(InputValue value)
        {
            if (!value.isPressed) return;
            // 로비 ESC 는 로비 어댑터(LobbyPlayerController.OnPause)가 메뉴·설정·도감 개폐로 소유한다.
            // 같은 PlayerInput 이 SendMessages 로 양쪽에 보내므로, 여기서 정지 요청까지 내면 한 키가 두 일을 한다.
            if (!IsRunContext) return;
            if (SaveStatusOverlay.IsCapturingInput) return;
            if (KeyboardBindingsPanel.IsOpen || KeyboardBindingsPanel.WasInputConsumedThisFrame) return;
            GameEvents.RaisePauseRequested();
        }

        /// <summary>
        /// 정지 중 ESC. 가장 안쪽에 열린 것부터 닫는다 — 설정 패널이 떠 있는데 정지만 풀면
        /// 패널이 게임 위에 남는다.
        /// </summary>
        private void OnCancel(InputValue value)
        {
            if (!value.isPressed) return;
            if (!IsRunContext) return;  // 로비는 정지 상태가 없다 — OnPause 주석 참조
            // 저장 모달이 떠 있으면 그 키는 모달 몫이다(SaveStatusOverlay.IsCapturingInput).
            if (SaveStatusOverlay.IsCapturingInput) return;
            if (KeyboardBindingsPanel.IsOpen || KeyboardBindingsPanel.WasInputConsumedThisFrame) return;

            if (SettingsPanel.IsOpen)
            {
                SettingsPanel.Close();
                return;
            }
            // 설정 패널이 자기 Update에서 먼저 닫았다면 이번 ESC는 이미 소비된 것이다(순서 역전 가드).
            // 이게 없으면 한 번의 ESC로 패널이 닫히면서 정지까지 풀린다.
            if (SettingsPanel.WasClosedThisFrame) return;

            GameEvents.RaiseResumeRequested();
        }
    }
}
