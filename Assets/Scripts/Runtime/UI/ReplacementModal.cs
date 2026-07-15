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
    /// </summary>
    public sealed class ReplacementModal : MonoBehaviour
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
            if (cancelButton != null) cancelButton.onClick.AddListener(Cancel);
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

            if (root != null) root.SetActive(true);
        }

        /// <summary>교체 포기 — 모달을 닫고 드래프트 카드 선택으로 되돌아간다.</summary>
        private void Cancel()
        {
            // Close()가 session을 비우므로 먼저 잡아둔다.
            var controller = session;
            Close();
            controller?.CancelReplacement();
        }

        public void Close()
        {
            if (root != null) root.SetActive(false);
            incomingSkill = null;
            currentActives = null;
            session = null;
        }

        private void OnSlotSelected(int index)
        {
            if (session == null || incomingSkill == null || currentActives == null) return;
            if (index < 0 || index >= currentActives.Count) return;

            var dropped = currentActives[index];
            string droppedId = dropped != null ? dropped.skillId : string.Empty;
            session.ConfirmReplacement(droppedId, incomingSkill);
            Close();
        }
    }
}
