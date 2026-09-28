using Abyss.Runtime.Form;
using UnityEngine.InputSystem;

namespace Abyss.Runtime.Player
{
    /// <summary>
    /// 폼 교체 입력을 FormController로 전달하는 얇은 어댑터 + 폼별 스탯 보정 조회.
    /// hpMultiplier는 시작 폼 고정(Health.InitializeHealth), moveSpeedMultiplier는 실시간(아래 프로퍼티)으로 반영한다.
    /// </summary>
    public sealed partial class PlayerCharacter
    {
        public FormController Form => formController;

        /// <summary>
        /// 현재 폼의 이동속도 배율(1 = 무보정). Movement가 매 FixedUpdate 참조한다.
        /// 폼 스왑에 즉시 반응(실시간). 폼 미지정 시 1배 폴백.
        /// </summary>
        public float FormMoveSpeedMultiplier =>
            (formController != null && formController.CurrentForm != null)
                ? formController.CurrentForm.moveSpeedMultiplier
                : 1f;

        private void OnFormSwap(InputValue value)
        {
            if (!value.isPressed) return;
            if (formController == null) return;
            // P04 A — 저스트 가드 창 안이면 즉시 교체하거나(성공 확인 포함) 1회 예약한다. 창이 없거나 지났으면 기존 경로.
            if (TryReserveGuardSwap()) return;
            formController.RequestSwap();
        }
    }
}
