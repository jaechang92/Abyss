using Abyss.Runtime.Events;
using Abyss.Runtime.Form;
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
    /// </summary>
    public sealed class FormReplacementModal : MonoBehaviour
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

        // 2단계 취소 확인: 첫 클릭은 '무장'(경고 라벨)만, 재클릭에서 실제 포기. 오조작으로 보상을 날리는 걸 막는다.
        private bool cancelArmed;
        private Text cancelLabel;
        private string cancelLabelDefault = "취소";
        private const string CancelArmedText = "포기 확정? (다시 클릭)";

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
                    currentSlotButtons[i].onClick.AddListener(() => OnSlotSelected(idx));
                }
            }
            if (cancelButton != null)
            {
                cancelButton.onClick.AddListener(Cancel);
                // 취소 버튼 자식 Text를 잡아 무장 시 라벨을 바꾼다(HudBuilder가 버튼 라벨을 자식 Text로 생성).
                cancelLabel = cancelButton.GetComponentInChildren<Text>();
                if (cancelLabel != null && !string.IsNullOrEmpty(cancelLabel.text)) cancelLabelDefault = cancelLabel.text;
            }
        }

        public void Open(FormData incoming, FormController formController)
        {
            if (incoming == null || formController == null) return;

            incomingForm = incoming;
            controller = formController;

            if (incomingText != null)
            {
                incomingText.text = $"폼 획득: {incoming.displayName}";
            }

            for (int i = 0; i < currentSlotButtons.Length; i++)
            {
                var slotForm = i < controller.SlotCount ? controller.GetSlot(i) : null;
                if (currentSlotLabels[i] != null)
                {
                    currentSlotLabels[i].text = slotForm != null ? slotForm.displayName : "—";
                }
                if (currentSlotButtons[i] != null)
                {
                    currentSlotButtons[i].interactable = i < controller.SlotCount;
                }
            }

            // 새로 열 때는 취소 무장을 해제해 항상 1클릭=경고 상태에서 시작한다.
            ResetCancelArm();

            if (root != null) root.SetActive(true);

            // 이미 열려 있는 상태(중복 호출)가 아니면 정지 진입.
            if (!isOpen)
            {
                isOpen = true;
                GameEvents.RaiseDraftOpened();
            }
        }

        private void Cancel()
        {
            // 2단계 확인: 첫 클릭은 무장(경고 라벨)만 하고 닫지 않는다. 재클릭에서 실제 포기.
            if (!cancelArmed)
            {
                cancelArmed = true;
                if (cancelLabel != null) cancelLabel.text = CancelArmedText;
                return;
            }
            // 무장 상태에서 재클릭 — 폼을 받지 않고 닫는다(획득 포기).
            Close();
        }

        // 취소 버튼을 기본 상태로 되돌린다(무장 해제 + 라벨 복원).
        private void ResetCancelArm()
        {
            cancelArmed = false;
            if (cancelLabel != null) cancelLabel.text = cancelLabelDefault;
        }

        private void Close()
        {
            ResetCancelArm();
            if (root != null) root.SetActive(false);
            incomingForm = null;
            controller = null;

            if (isOpen)
            {
                isOpen = false;
                GameEvents.RaiseDraftClosed();
                // 보상 흐름 종료 신호(획득/거절 공통). 보상 룸 게이트(StageDirector)가 이걸로 진행을 재개한다.
                GameEvents.RaiseFormRewardResolved();
            }
        }

        private void OnSlotSelected(int index)
        {
            if (controller == null || incomingForm == null) return;
            if (index < 0 || index >= controller.SlotCount) return;

            // 선택 슬롯에 주입 + 즉시 전환(activate). 전환을 알려 HUD·스킬 로드아웃·이동배율을 갱신.
            var previous = controller.CurrentForm;
            controller.EquipForm(incomingForm, index, activate: true);
            GameEvents.RaiseFormSwapped(previous, controller.CurrentForm);

            Close();
        }
    }
}
