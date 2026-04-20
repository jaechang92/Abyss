using Abyss.Runtime.Form;
using UnityEngine.InputSystem;

namespace Abyss.Runtime.Player
{
    /// <summary>
    /// 폼 교체 입력을 FormController로 전달하는 얇은 어댑터.
    /// P-14 이후 폼별 스탯 보정(hpMultiplier 등)을 HP/이동에 반영하는 훅이 여기에 추가될 예정.
    /// </summary>
    public sealed partial class PlayerCharacter
    {
        public FormController Form => formController;

        private void OnFormSwap(InputValue value)
        {
            if (!value.isPressed) return;
            if (formController == null) return;
            formController.RequestSwap();
        }
    }
}
