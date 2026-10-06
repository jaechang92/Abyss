using System;
using Abyss.Runtime.Flow;
using Abyss.Runtime.Form;
using Abyss.Runtime.Localization;
using UnityEngine;
using UnityEngine.UI;

namespace Abyss.Runtime.Lobby
{
    /// <summary>
    /// 던전 포털 상호작용 시 열리는 시작 폼 선택 패널.
    /// 확정 시 RunStartContext.StartingForm을 기록하고 onConfirm 콜백을 호출한다.
    /// 폼 선택 로직은 이전 메뉴식 LobbyController에서 이전(추출)했다.
    /// 키보드·패드 조작(포커스·탐색 경로·취소)은 FormSelectPanel.Navigation.cs가 맡는다.
    /// </summary>
    public sealed partial class FormSelectPanel : MonoBehaviour
    {
        [Header("루트")]
        [SerializeField] private GameObject root;

        [Header("버튼")]
        [SerializeField] private Button confirmButton;
        [SerializeField] private Button cancelButton;

        [Header("표시")]
        [SerializeField] private Text titleLabel;

        [Header("폼 선택")]
        [Tooltip("선택 가능한 시작 폼. formButtons와 같은 순서로 1:1 대응.")]
        [SerializeField] private FormData[] selectableForms;
        [Tooltip("폼별 선택 버튼. selectableForms와 같은 순서.")]
        [SerializeField] private Button[] formButtons;

        [Tooltip("아직 아무것도 안 고른 상태에서 강조할 폼. Run 이 실제로 시작하는 폼과 같아야 한다.")]
        [SerializeField] private FormData defaultForm;

        // 폼 버튼 강조 색(선택/비선택)
        private static readonly Color formNormal = new Color(0.18f, 0.18f, 0.22f);
        private static readonly Color formSelected = new Color(0.3f, 0.5f, 0.7f);

        private int selectedFormIndex;
        private Action onConfirm;
        private Action onCancel;

        public bool IsOpen => root != null && root.activeSelf;

        private void Awake()
        {
            WireFormButtons();
            if (confirmButton != null) confirmButton.onClick.AddListener(OnConfirmClicked);
            if (cancelButton != null) cancelButton.onClick.AddListener(OnCancelClicked);
            AttachLabels();
            if (root != null) root.SetActive(false);
        }

        /// <summary>씬 빌더가 한글로 채워 둔 고정 문구를 언어를 따라가는 라벨로 바꾼다.</summary>
        private void AttachLabels()
        {
            LocalizedText.Attach(titleLabel, StringKey.FormSelect_Title);
            if (confirmButton != null) LocalizedText.Attach(confirmButton.GetComponentInChildren<Text>(true), StringKey.FormSelect_Confirm);
            if (cancelButton != null) LocalizedText.Attach(cancelButton.GetComponentInChildren<Text>(true), StringKey.FormSelect_Cancel);

            // 폼 버튼의 이름 라벨은 씬 빌더가 displayName(한글)으로 채워 둔다. 키가 있는 폼만 언어를 따라가게 바꾼다.
            if (formButtons == null || selectableForms == null) return;
            int count = Mathf.Min(formButtons.Length, selectableForms.Length);
            for (int i = 0; i < count; i++)
            {
                var form = selectableForms[i];
                if (formButtons[i] == null || form == null || string.IsNullOrEmpty(form.nameKey)) continue;
                LocalizedText.Attach(formButtons[i].GetComponentInChildren<Text>(true), form.nameKey);
            }
        }

        /// <summary>패널을 연다. confirm/cancel은 각각 확정·취소 시 콜백.</summary>
        public void Open(Action confirm, Action cancel)
        {
            bool wasOpen = IsOpen;
            onConfirm = confirm;
            onCancel = cancel;
            RestoreSelection();
            RefreshHighlight();
            if (root != null) root.SetActive(true);
            BeginFocus(isFirstOpen: !wasOpen);
        }

        /// <summary>표시만 닫는다 — 콜백은 부르지 않는다(외부 호출 의미 그대로). 패널이 쥔 포커스는 이전 선택으로 돌려준다.</summary>
        public void Close()
        {
            if (IsOpen) ReleaseFocus();
            if (root != null) root.SetActive(false);
        }

        private void WireFormButtons()
        {
            if (formButtons == null) return;
            for (int i = 0; i < formButtons.Length; i++)
            {
                int index = i; // 클로저 캡처 주의 — 지역 복사
                if (formButtons[i] != null)
                {
                    formButtons[i].onClick.AddListener(() => OnFormButtonClicked(index));
                }
            }
        }

        /// <summary>
        /// 마지막 선택 폼(RunStartContext) 복원. 아직 아무것도 안 골랐으면 <see cref="defaultForm"/>.
        ///
        /// 🔴 예전에는 무조건 <b>첫 폼</b>(목록 정렬 순서)이었다. 그런데 폼을 안 고르고 포털로 들어가면
        /// 런은 Player 프리팹의 slot 0 으로 시작한다 — 둘이 같다는 보장이 없었고 실제로 달랐다.
        /// 그래도 아무도 못 본 이유는 <b>로비 캐릭터가 흰 사각형이라 정체가 없었기</b> 때문이다.
        /// 실제 폼 그림을 세우는 순간 "패널은 A 를 강조하는데 서 있는 건 B"가 눈에 보인다.
        /// </summary>
        private void RestoreSelection()
        {
            selectedFormIndex = 0;
            if (selectableForms == null || selectableForms.Length == 0) return;

            // RunStartContext 가 우선. 없으면 기본 폼, 그것도 없으면 예전대로 첫 폼.
            string targetId = RunStartContext.HasStartingForm
                ? RunStartContext.StartingFormId
                : (defaultForm != null ? defaultForm.formId : null);
            if (string.IsNullOrEmpty(targetId)) return;

            for (int i = 0; i < selectableForms.Length; i++)
            {
                if (selectableForms[i] != null && selectableForms[i].formId == targetId)
                {
                    selectedFormIndex = i;
                    return;
                }
            }
        }

        private void SelectForm(int index)
        {
            if (selectableForms == null || index < 0 || index >= selectableForms.Length) return;
            selectedFormIndex = index;
            RefreshHighlight();
            // [선택]·[취소]에서 위로 올라갈 곳은 강조된 폼이다 — 강조가 바뀌면 경로도 다시 잇는다.
            RefreshNavigation();
        }

        private void RefreshHighlight()
        {
            if (formButtons == null) return;
            for (int i = 0; i < formButtons.Length; i++)
            {
                if (formButtons[i] == null) continue;
                if (formButtons[i].targetGraphic is Image img)
                {
                    img.color = (i == selectedFormIndex) ? formSelected : formNormal;
                }
            }
        }

        /// <summary>콜백은 지역에 잡고 먼저 비운다 — 같은 열림에서 확정·취소가 두 번 불리지 않는다.</summary>
        private void Confirm()
        {
            if (selectableForms != null && selectedFormIndex >= 0 && selectedFormIndex < selectableForms.Length
                && selectableForms[selectedFormIndex] != null)
            {
                RunStartContext.StartingForm = selectableForms[selectedFormIndex];
            }
            var confirm = onConfirm;
            onConfirm = null;
            onCancel = null;
            Close();
            confirm?.Invoke();
        }

        private void Cancel()
        {
            var cancel = onCancel;
            onConfirm = null;
            onCancel = null;
            Close();
            cancel?.Invoke();
        }
    }
}
