using Abyss.Runtime.Form;
using Abyss.Runtime.Feedback;
using Abyss.Runtime.Events;
using UnityEngine;
using UnityEngine.UI;

namespace Abyss.Runtime.UI
{
    /// <summary>
    /// 현재 폼·대기 폼 아이콘 + 교체 쿨다운 게이지.
    /// FormController.CooldownProgress를 매 프레임 참조.
    /// </summary>
    public sealed partial class FormSlotPresenter : MonoBehaviour
    {
        [SerializeField] private Image currentFormIcon;
        [SerializeField] private Image otherFormIcon;
        [SerializeField] private Image cooldownFill;

        private FormController formController;
        private FormPresentationFeedback feedback;
        private FormData shownCurrent;
        private FormData shownOther;
        private Image acquisitionGlow;
        private float glowRemaining;

        public void Bind(FormController controller)
        {
            Unsubscribe();
            ClearAcquisitionGlow();
            ResetWeaponBadges();
            formController = controller;
            if (formController != null)
            {
                feedback = controller.GetComponent<FormPresentationFeedback>();
                if (isActiveAndEnabled) Subscribe();
                RefreshIcons();
            }
        }

        private void OnEnable()
        {
            Subscribe();
            GameEvents.OnRoomEntered += HandleRoomEntered;
            GameEvents.OnRunEnded += ClearAcquisitionGlow;
            GameEvents.OnRunAbandoned += ClearAcquisitionGlow;
            GameEvents.OnPlayerDead += ClearAcquisitionGlow;
            GameEvents.OnRunStarted += ResetWeaponBadges;
        }

        private void Subscribe()
        {
            if (formController != null)
            {
                formController.OnSwapCompleted -= HandleSwapCompleted;
                formController.OnSwapCompleted += HandleSwapCompleted;
            }
            if (feedback != null)
            {
                feedback.OnAcquisitionPresented -= HandleAcquisition;
                feedback.OnAcquisitionPresented += HandleAcquisition;
            }
        }

        private void Unsubscribe()
        {
            if (formController != null) formController.OnSwapCompleted -= HandleSwapCompleted;
            if (feedback != null) feedback.OnAcquisitionPresented -= HandleAcquisition;
            feedback = null;
        }

        private void OnDisable()
        {
            Unsubscribe();
            GameEvents.OnRoomEntered -= HandleRoomEntered;
            GameEvents.OnRunEnded -= ClearAcquisitionGlow;
            GameEvents.OnRunAbandoned -= ClearAcquisitionGlow;
            GameEvents.OnPlayerDead -= ClearAcquisitionGlow;
            GameEvents.OnRunStarted -= ResetWeaponBadges;
            ClearAcquisitionGlow();
        }

        private void HandleRoomEntered(Stage.RoomData room) => ClearAcquisitionGlow();

        private void ClearAcquisitionGlow()
        {
            ClearWeaponBadgeFlashes();
            glowRemaining = 0f;
            if (acquisitionGlow != null) acquisitionGlow.gameObject.SetActive(false);
        }

        private void Update()
        {
            if (formController == null) return;
            if (feedback == null)
            {
                feedback = formController.GetComponent<FormPresentationFeedback>();
                if (feedback != null) Subscribe();
            }
            if (shownCurrent != formController.CurrentForm || shownOther != formController.OtherForm) RefreshIcons();
            TickWeaponBadges();
            if (cooldownFill != null) cooldownFill.fillAmount = formController.CooldownProgress;
            if (glowRemaining > 0f && acquisitionGlow != null)
            {
                glowRemaining = Mathf.Max(0f, glowRemaining - Time.deltaTime);
                var color = acquisitionGlow.color;
                color.a = 0.3f * Mathf.Sin((glowRemaining / 0.35f) * Mathf.PI);
                acquisitionGlow.color = color;
                if (glowRemaining <= 0f) acquisitionGlow.gameObject.SetActive(false);
            }
        }

        private void HandleAcquisition()
        {
            RefreshIcons();
            if (currentFormIcon == null || !currentFormIcon.enabled) return;
            if (acquisitionGlow == null)
            {
                var glow = new GameObject("FormAcquisitionGlow", typeof(RectTransform), typeof(Image));
                glow.transform.SetParent(currentFormIcon.transform, false);
                var rect = (RectTransform)glow.transform;
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = rect.offsetMax = Vector2.zero;
                acquisitionGlow = glow.GetComponent<Image>();
                acquisitionGlow.raycastTarget = false;
            }
            acquisitionGlow.color = new Color(0.65f, 0.85f, 1f, 0f);
            acquisitionGlow.gameObject.SetActive(true);
            glowRemaining = 0.35f;
        }

        private void HandleSwapCompleted(FormData previous, FormData next)
        {
            RefreshIcons();
        }

        private void RefreshIcons()
        {
            if (formController == null) return;
            shownCurrent = formController.CurrentForm;
            shownOther = formController.OtherForm;
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
