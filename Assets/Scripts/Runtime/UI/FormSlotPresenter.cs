using Abyss.Runtime.Form;
using UnityEngine;
using UnityEngine.UI;

namespace Abyss.Runtime.UI
{
    /// <summary>
    /// 현재 폼·대기 폼 아이콘 + 교체 쿨다운 게이지.
    /// FormController.CooldownProgress를 매 프레임 참조.
    /// </summary>
    public sealed class FormSlotPresenter : MonoBehaviour
    {
        [SerializeField] private Image currentFormIcon;
        [SerializeField] private Image otherFormIcon;
        [SerializeField] private Image cooldownFill;

        private FormController formController;

        public void Bind(FormController controller)
        {
            if (formController != null) formController.OnSwapCompleted -= HandleSwapCompleted;
            formController = controller;
            if (formController != null)
            {
                formController.OnSwapCompleted += HandleSwapCompleted;
                RefreshIcons();
            }
        }

        private void OnDisable()
        {
            if (formController != null) formController.OnSwapCompleted -= HandleSwapCompleted;
        }

        private void Update()
        {
            if (formController == null) return;
            if (cooldownFill != null) cooldownFill.fillAmount = formController.CooldownProgress;
        }

        private void HandleSwapCompleted(FormData previous, FormData next)
        {
            RefreshIcons();
        }

        private void RefreshIcons()
        {
            if (formController == null) return;
            ApplyIcon(currentFormIcon, formController.CurrentForm);
            ApplyIcon(otherFormIcon, formController.OtherForm);
        }

        private static void ApplyIcon(Image target, FormData form)
        {
            if (target == null) return;
            target.sprite = form != null ? form.icon : null;
            target.enabled = form != null && form.icon != null;
        }
    }
}
