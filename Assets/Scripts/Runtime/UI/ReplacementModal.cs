using System.Collections.Generic;
using Abyss.Runtime.Draft;
using UnityEngine;
using UnityEngine.UI;

namespace Abyss.Runtime.UI
{
    /// <summary>
    /// Active 슬롯 상한 초과 시 교체 대상 선택 모달 (Analyst MF-3).
    /// GameEvents.OnDraftSlotReplaceRequested를 HUDPresenter가 받아서 Open() 호출.
    /// 슬롯 버튼 클릭 → DraftSessionController.ConfirmReplacement.
    /// 키보드·패드 조작(포커스·탐색 경로·ESC/패드 B 취소)은 ReplacementModal.Navigation.cs가 맡는다.
    /// </summary>
    public sealed partial class ReplacementModal : MonoBehaviour
    {
        [Header("루트")]
        [SerializeField] private GameObject root;

        [Header("정보")]
        [SerializeField] private Text incomingText;
        [SerializeField] private Button[] currentSlotButtons = new Button[2];
        [SerializeField] private Text[] currentSlotLabels = new Text[2];
        [SerializeField] private Button cancelButton;

        private DraftSessionController session;
        private SkillData incomingSkill;
        private IReadOnlyList<SkillData> currentActives;
        private bool isOpen;

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
            if (cancelButton != null) cancelButton.onClick.AddListener(OnCancelClicked);
        }

        public void Open(SkillData incoming, IReadOnlyList<SkillData> actives, DraftSessionController controller)
        {
            incomingSkill = incoming;
            currentActives = actives;
            session = controller;

            if (incomingText != null)
            {
                incomingText.text = incoming != null ? $"획득: {incoming.displayName}" : string.Empty;
            }

            for (int i = 0; i < currentSlotLabels.Length; i++)
            {
                bool hasSkill = actives != null && i < actives.Count && actives[i] != null;

                if (currentSlotLabels[i] != null)
                {
                    currentSlotLabels[i].text = hasSkill ? actives[i].displayName : "—";
                }
                if (currentSlotButtons[i] != null)
                {
                    currentSlotButtons[i].interactable = hasSkill;
                }
            }

            // root가 이 컴포넌트의 GameObject라 첫 SetActive(true)에서 Awake가 돈다 — 포커스는 그 뒤에 잡는다.
            if (root != null) root.SetActive(true);

            bool isFirstOpen = !isOpen;
            isOpen = true;
            BeginFocus(isFirstOpen);
        }

        /// <summary>교체 포기 — 모달을 닫고 드래프트 카드 선택으로 되돌아간다.</summary>
        private void Cancel()
        {
            // Close()가 session을 비우므로 먼저 잡아둔다.
            var controller = session;
            Close();
            controller?.CancelReplacement();
        }

        /// <summary>
        /// 상태를 비우고 모달이 쥔 포커스를 돌려준 뒤 root를 끈다. 세션에는 아무것도 알리지 않는다(외부 호출 의미 유지).
        /// root가 이 컴포넌트라 SetActive(false)가 OnDisable을 부르는데, 그때는 이미 닫힌 상태라 선택 정리만 한다.
        /// </summary>
        public void Close()
        {
            bool wasOpen = isOpen;
            isOpen = false;
            incomingSkill = null;
            currentActives = null;
            session = null;
            if (wasOpen) ReleaseFocus();
            if (root != null) root.SetActive(false);
        }

        /// <summary>
        /// 슬롯 확정. 🔴 세션 호출보다 <b>닫기가 먼저</b>다 — ConfirmReplacement는 안에서 DraftClosed를 내고,
        /// 대기 중인 레벨업이 있으면 같은 호출 안에서 다음 드래프트(OnDraftOptionsReady)까지 연다.
        /// 모달이 슬롯 선택을 쥔 채 그 드래프트가 열리면 DraftPanelPresenter가 「다른 UI의 포커스」로 보고 첫 카드를 안 잡는다.
        /// 그래서 세션·신규·버릴 스킬을 지역으로 잡고 닫은 뒤 한 번만 확정한다(서비스·이벤트 순서는 그대로).
        /// </summary>
        private void OnSlotSelected(int index)
        {
            if (session == null || incomingSkill == null || currentActives == null) return;
            if (index < 0 || index >= currentActives.Count) return;

            var controller = session;
            var incoming = incomingSkill;
            var dropped = currentActives[index];
            string droppedId = dropped != null ? dropped.skillId : string.Empty;

            Close();
            controller.ConfirmReplacement(droppedId, incoming);
        }
    }
}
