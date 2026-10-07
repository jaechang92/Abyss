using System;
using Abyss.Runtime.Audio;
using Abyss.Runtime.Events;
using Abyss.Runtime.Flow;
using Abyss.Runtime.Form;
using Abyss.Runtime.Run;
using Abyss.Runtime.Stage;
using UnityEngine;

namespace Abyss.Runtime.Feedback
{
    /// <summary>획득 확정과 실제 전투 교체만 표현한다. 새 정지·쿨다운·보상 신호를 만들지 않는다.</summary>
    [DefaultExecutionOrder(50)]
    public sealed class FormPresentationFeedback : MonoBehaviour
    {
        private FormController controller;
        private SpriteRenderer body;
        private FormTransitionFx activeFx;
        private FormTransitionFx.BodySnapshot outgoing;
        private FormData pendingForm;
        private Vector3? pendingOrigin;
        private bool isAcquisition;
        private bool hasAcquiredThisRun;
        private bool isStopped;

        public event Action OnAcquisitionPresented;
        public int RoomVersion { get; private set; }

        public static FormPresentationFeedback Ensure(FormController source, SpriteRenderer renderer)
        {
            var feedback = source.GetComponent<FormPresentationFeedback>();
            if (feedback == null) feedback = source.gameObject.AddComponent<FormPresentationFeedback>();
            if (feedback.controller == null)
            {
                feedback.controller = source;
                if (feedback.isActiveAndEnabled) source.OnSwapStarted += feedback.HandleSwap;
            }
            feedback.body = renderer;
            return feedback;
        }

        public static void PlayAcquisition(FormController source, FormData form, Vector3? origin, int roomVersion)
        {
            if (source == null || form == null || source.CurrentForm != form) return;
            var feedback = source.GetComponent<FormPresentationFeedback>();
            if (feedback == null || !feedback.CanPresent || feedback.RoomVersion != roomVersion) return;
            feedback.ClearPending();
            feedback.pendingForm = form;
            feedback.pendingOrigin = origin;
            feedback.isAcquisition = true;
        }

        private bool CanPresent => isActiveAndEnabled && !isStopped && controller != null && body != null &&
            (!RunManager.HasInstance || RunManager.Instance.IsRunActive) &&
            (!SceneFlowController.HasInstance || !SceneFlowController.Instance.IsLoading);

        private void OnEnable()
        {
            if (controller != null) controller.OnSwapStarted += HandleSwap;
            GameEvents.OnRoomEntered += HandleRoom;
            GameEvents.OnRunStarted += HandleRunStarted;
            GameEvents.OnPlayerDead += Stop;
            GameEvents.OnRunEnded += Stop;
            GameEvents.OnRunAbandoned += Stop;
        }

        private void OnDisable()
        {
            if (controller != null) controller.OnSwapStarted -= HandleSwap;
            GameEvents.OnRoomEntered -= HandleRoom;
            GameEvents.OnRunStarted -= HandleRunStarted;
            GameEvents.OnPlayerDead -= Stop;
            GameEvents.OnRunEnded -= Stop;
            GameEvents.OnRunAbandoned -= Stop;
            ClearPending();
        }

        private void HandleSwap(FormData previous, FormData next)
        {
            if (!CanPresent || next == null) return;
            ClearPending();
            outgoing = FormTransitionFx.BodySnapshot.Capture(body);
            pendingForm = next;
            isAcquisition = false;
        }

        private void LateUpdate()
        {
            if (pendingForm == null) return;
            if (!CanPresent || controller.CurrentForm != pendingForm)
            {
                ClearPending();
                return;
            }
            if (Time.timeScale <= 0f) return;
            // FormVisualPresenter(30)·WeaponSocket(40) 뒤: 실제 새 몸이 적용된 프레임을 복제한다.
            var incoming = FormTransitionFx.BodySnapshot.Capture(body);
            if (isAcquisition)
            {
                float duration = hasAcquiredThisRun ? 0.4f : 1f;
                activeFx = FormTransitionFx.PlayAcquisition(incoming, body.transform, pendingOrigin,
                    pendingForm.castColor, duration);
                hasAcquiredThisRun = true;
                if (pendingForm.swapInSfx != null && AudioManager.HasInstance)
                    AudioManager.Instance.PlaySfx(pendingForm.swapInSfx);
                OnAcquisitionPresented?.Invoke();
            }
            else
            {
                activeFx = FormTransitionFx.PlaySwap(outgoing, incoming, pendingForm.castColor);
            }
            pendingForm = null;
            pendingOrigin = null;
        }

        private void HandleRoom(RoomData room)
        {
            RoomVersion++;
            ClearPending();
        }
        private void HandleRunStarted()
        {
            ClearPending();
            RoomVersion++;
            hasAcquiredThisRun = false;
            isStopped = false;
        }
        private void Stop()
        {
            isStopped = true;
            ClearPending();
        }
        private void ClearPending()
        {
            pendingForm = null;
            pendingOrigin = null;
            if (activeFx != null) activeFx.Clear();
            activeFx = null;
        }
    }
}
