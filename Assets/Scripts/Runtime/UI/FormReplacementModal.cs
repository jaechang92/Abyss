using Abyss.Runtime.Events;
using Abyss.Runtime.Form;
using Abyss.Runtime.Localization;
using UnityEngine;
using UnityEngine.UI;

namespace Abyss.Runtime.UI
{
    /// <summary>
    /// 미드런 폼 보상 획득 시 어느 슬롯에 넣을지 선택하는 모달(Skul식).
    /// GameEvents.OnFormRewardOffered를 HUDPresenter가 받아 Open() 호출.
    /// 슬롯 버튼 클릭 → FormController.EquipForm(선택 폼, 슬롯, activate:true)로 즉시 전환.
    /// 스킬 교체 모달(ReplacementModal)과 동형. 열림/닫힘에 OnDraftOpened/Closed를 발행해
    /// 기존 DraftOpen FSM 상태로 게임을 전역 정지/재개시킨다(전용 정지 로직 불필요).
    /// 키보드·패드 조작(포커스·탐색 경로·ESC/패드 B 취소)은 FormReplacementModal.Navigation.cs가 맡는다.
    /// </summary>
    public sealed partial class FormReplacementModal : MonoBehaviour
    {
        [Header("루트")]
        [SerializeField] private GameObject root;

        [Header("정보")]
        [SerializeField] private Text incomingText;
        [SerializeField] private Button[] currentSlotButtons = new Button[2];
        [SerializeField] private Text[] currentSlotLabels = new Text[2];
        [SerializeField] private Button cancelButton;

        private FormController controller;
        private FormData incomingForm;
        private bool isOpen;
        private Vector3? acquisitionOrigin;
        private FormAltar rewardAltar;
        private int rewardGeneration;
        private bool hasRewardAltar;
        private int modalGeneration;
        private bool ownsModalPause;
        private bool isPublishingPause;
        private bool hasDeferredPauseRelease;

        // 2단계 취소 확인: 첫 클릭은 '무장'(경고 라벨)만, 재클릭에서 실제 포기. 오조작으로 보상을 날리는 걸 막는다.
        private bool cancelArmed;
        private Text cancelLabel;

        private void Awake()
        {
            // 주의: root.SetActive(false)를 여기서 하지 않는다. 컴포넌트가 root와 같은 GameObject에 있어
            // Open()의 첫 root.SetActive(true)가 이 Awake를 유발하는데, 여기서 다시 끄면 모달이 안 열린다.
            // 초기 숨김은 빌더(HudBuilder)가 root를 비활성으로 직렬화해 보장한다.
            for (int i = 0; i < currentSlotButtons.Length; i++)
            {
                int idx = i;
                if (currentSlotButtons[i] != null)
                {
                    currentSlotButtons[i].onClick.AddListener(() => OnSlotClicked(idx));
                }
            }
            if (cancelButton != null)
            {
                cancelButton.onClick.AddListener(OnCancelClicked);
                // 취소 버튼 자식 Text를 잡아 무장 시 라벨을 바꾼다(HudBuilder가 버튼 라벨을 자식 Text로 생성).
                cancelLabel = cancelButton.GetComponentInChildren<Text>();
            }
        }

        public void Open(FormData incoming, FormController formController)
        {
            if (incoming == null || formController == null) return;

            modalGeneration++;
            incomingForm = incoming;
            controller = formController;
            acquisitionOrigin = FormAltar.CaptureOfferOrigin(incoming, formController);
            rewardAltar = FormAltar.CaptureOffer(incoming, formController);
            rewardGeneration = rewardAltar != null ? rewardAltar.OfferGeneration : -1;
            hasRewardAltar = rewardAltar != null;
            if (hasRewardAltar)
            {
                if (!rewardAltar.CanResolveOffer(rewardGeneration))
                {
                    Close(resolveReward: false, releasePause: false);
                    return;
                }
                int generation = modalGeneration;
                rewardAltar.BindModalCancellation(rewardGeneration, releasePause =>
                {
                    if (this != null && generation == modalGeneration)
                        Close(resolveReward: false, releasePause: releasePause);
                });
            }

            // 새 폼·슬롯 이름은 언어 변경 때도 같은 함수로 다시 그린다(FormReplacementModal.Localization.cs).
            RefreshFormTexts();

            for (int i = 0; i < currentSlotButtons.Length; i++)
            {
                if (currentSlotButtons[i] != null)
                {
                    currentSlotButtons[i].interactable = i < controller.SlotCount;
                }
            }

            // 새로 열 때는 취소 무장을 해제해 항상 1클릭=경고 상태에서 시작한다.
            // root가 이 컴포넌트의 GameObject라 첫 SetActive(true)에서 Awake가 돈다 — 포커스는 그 뒤에 잡는다.
            if (root != null) root.SetActive(true);
            ResetCancelArm();
            ReplacementTextReadability.Attach(root != null ? root.transform : null, incomingText, currentSlotButtons, cancelButton);

            // 이미 열려 있는 상태(중복 호출)가 아니면 정지 진입.
            bool isFirstOpen = !isOpen;
            if (!isOpen)
            {
                isOpen = true;
                ownsModalPause = true;
            }
            hasDeferredPauseRelease = false;
            _ = TrackModalPauseAsync(modalGeneration);
            if (isFirstOpen)
            {
                isPublishingPause = true;
                try { GameEvents.RaiseDraftOpened(); }
                finally { isPublishingPause = false; }
            }
            BeginFocus(isFirstOpen);
        }

        // UI를 닫은 뒤에도 자기 정지 token은 유지한다. 사망/씬 전환은 같은 호출 안에서 먼저 확정된다.
        // 다음 프레임의 활성 런에서만 회수하고, 다른 모달이 DraftOpened를 냈으면 그 정지에는 손대지 않는다.
        private async Awaitable TrackModalPauseAsync(int generation)
        {
            void MarkTakenOver()
            {
                if (this != null && generation == modalGeneration && !isPublishingPause) ownsModalPause = false;
            }
            void AbandonPause()
            {
                if (this == null || generation != modalGeneration) return;
                ownsModalPause = false;
                hasDeferredPauseRelease = false;
            }
            GameEvents.OnDraftOpened += MarkTakenOver;
            GameEvents.OnRunEnded += AbandonPause;
            GameEvents.OnRunAbandoned += AbandonPause;
            GameEvents.OnPlayerDead += AbandonPause;
            try
            {
                while (generation == modalGeneration && ownsModalPause && (isOpen || hasDeferredPauseRelease))
                {
                    await Awaitable.NextFrameAsync(destroyCancellationToken);
                    if (this == null || generation != modalGeneration) return;
                    if (hasDeferredPauseRelease && CanReleaseModalPause()) ReleaseModalPause();
                }
            }
            catch (System.OperationCanceledException) { }
            finally
            {
                GameEvents.OnDraftOpened -= MarkTakenOver;
                GameEvents.OnRunEnded -= AbandonPause;
                GameEvents.OnRunAbandoned -= AbandonPause;
                GameEvents.OnPlayerDead -= AbandonPause;
            }
        }

        private bool CanReleaseModalPause() => ownsModalPause && !IsUpperModalOpen &&
            !NodeMapPanel.IsOpen && !EventRoomPanel.IsOpen && !ShopRoomPanel.IsOpen &&
            (!Run.RunManager.HasInstance || Run.RunManager.Instance.IsRunActive) &&
            (!Flow.SceneFlowController.HasInstance || !Flow.SceneFlowController.Instance.IsLoading);

        private void ReleaseModalPause()
        {
            ownsModalPause = false;
            hasDeferredPauseRelease = false;
            GameEvents.RaiseDraftClosed();
        }

        private void Cancel()
        {
            if (!HasCurrentRewardOwner())
            {
                Close(resolveReward: false, releasePause: false);
                return;
            }
            // 2단계 확인: 첫 클릭은 무장(경고 라벨)만 하고 닫지 않는다. 재클릭에서 실제 포기.
            if (!cancelArmed)
            {
                cancelArmed = true;
                RefreshCancelLabel();
                return;
            }
            // 무장 상태에서 재클릭 — 폼을 받지 않고 닫는다(획득 포기).
            Close();
        }

        // 취소 버튼을 기본 상태로 되돌린다(무장 해제 + 라벨 복원).
        private void ResetCancelArm()
        {
            cancelArmed = false;
            RefreshCancelLabel();
        }

        /// <summary>
        /// 상태를 먼저 비우고 포커스를 돌려준 뒤 root를 끈다 — root가 이 컴포넌트라 SetActive(false)가 OnDisable을
        /// 부르는데, 그때는 이미 닫힌 상태라 선택 정리만 한다(닫기 중복 없음). 닫힘 신호는 맨 마지막에 낸다 —
        /// 그 신호로 이어지는 다음 방·드래프트가 잡는 포커스를 이 모달이 뒤에서 뺏지 않는다.
        /// </summary>
        private bool HasCurrentRewardOwner() => !hasRewardAltar ||
            rewardAltar != null && rewardAltar.CanResolveOffer(rewardGeneration);

        private void Close(bool acquired = false, bool resolveReward = true, bool releasePause = true)
        {
            var sourceAltar = rewardAltar;
            int sourceGeneration = rewardGeneration;
            bool hadAltar = hasRewardAltar;
            bool canResolve = resolveReward && HasCurrentRewardOwner();
            ResetCancelArm();
            bool wasOpen = isOpen;
            hasDeferredPauseRelease = ownsModalPause && (wasOpen || hasDeferredPauseRelease);
            isOpen = false;
            incomingForm = null;
            controller = null;
            acquisitionOrigin = null;
            rewardAltar = null;
            hasRewardAltar = false;
            if (wasOpen) ReleaseFocus();
            if (root != null) root.SetActive(false);

            if (wasOpen)
            {
                // 낡은 모달은 결과/씬/다른 모달이 가져간 정지를 풀지 않는다.
                if (releasePause && CanReleaseModalPause()) ReleaseModalPause();
                bool completed = canResolve && (!hadAltar ||
                    sourceAltar != null && sourceAltar.CompletePresentation(acquired, sourceGeneration));
                if (completed) GameEvents.RaiseFormRewardResolved();
            }
        }

        private void OnSlotSelected(int index)
        {
            if (!isActiveAndEnabled || !HasCurrentRewardOwner())
            {
                Close(resolveReward: false, releasePause: false);
                return;
            }
            if (controller == null || incomingForm == null) return;
            if (index < 0 || index >= controller.SlotCount) return;

            // 선택 슬롯에 주입 + 즉시 전환(activate). 전환을 알려 HUD·스킬 로드아웃·이동배율을 갱신.
            var previous = controller.CurrentForm;
            var acquiredController = controller;
            var acquiredForm = incomingForm;
            int operationGeneration = modalGeneration;
            var origin = acquisitionOrigin;
            var presentation = acquiredController.GetComponent<Feedback.FormPresentationFeedback>();
            int roomVersion = presentation != null ? presentation.RoomVersion : -1;
            acquiredController.EquipForm(acquiredForm, index, activate: true);
            if (operationGeneration != modalGeneration || !isOpen || !HasCurrentRewardOwner()) return;
            GameEvents.RaiseFormSwapped(previous, acquiredController.CurrentForm);
            if (operationGeneration != modalGeneration || !isOpen || !HasCurrentRewardOwner()) return;

            Close(acquired: true);
            // 획득 성공 전용. 닫기/거절/시작 폼 적용은 이 경로를 지나지 않는다.
            Feedback.FormPresentationFeedback.PlayAcquisition(acquiredController, acquiredForm, origin, roomVersion);
        }
    }
}
