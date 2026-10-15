using Abyss.Runtime.Localization;

namespace Abyss.Runtime.UI
{
    /// <summary>
    /// <see cref="FormReplacementModal"/>의 언어 즉시 갱신 — 열린 채 언어가 바뀌면 새 폼 이름·슬롯 이름·[취소] 라벨을
    /// 기존 키로 다시 그린다. 글자만 바꾸고 취소 무장·포커스·탐색 경로·장착/포기·이벤트는 건드리지 않는다.
    ///
    /// 구독은 켜져 있는 동안만 둔다. root가 이 컴포넌트의 GameObject라 첫 Open의 root.SetActive(true)에서
    /// Awake → OnEnable 순으로 돌아 구독하고, Close·바깥 비활성의 OnDisable(Navigation partial)에서 뗀다.
    /// 꺼진 동안 언어가 바뀌었으면 열린 상태로 다시 켜질 때 OnEnable이 한 번 다시 그린다.
    /// </summary>
    public sealed partial class FormReplacementModal
    {
        private void OnEnable()
        {
            Loc.AddLanguageChangedListener(HandleLanguageChanged);
            // 첫 Open의 활성화 시점엔 아직 isOpen=false — Open이 이어서 글자를 채운다.
            if (isOpen) RefreshLocalizedTexts();
        }

        private void RemoveLanguageListener() => Loc.RemoveLanguageChangedListener(HandleLanguageChanged);

        private void HandleLanguageChanged(LocalizationLanguage language)
        {
            if (isOpen) RefreshLocalizedTexts();
        }

        private void RefreshLocalizedTexts()
        {
            RefreshFormTexts();
            RefreshCancelLabel();
        }

        /// <summary>새 폼 제목과 현재 슬롯 이름. 슬롯 버튼의 interactable·탐색 경로는 Open만 정한다.</summary>
        private void RefreshFormTexts()
        {
            if (incomingText != null && incomingForm != null)
            {
                incomingText.text = Loc.GetFormat(StringKey.Form_AcquiredFormat, incomingForm.LocalizedName);
            }

            if (controller == null) return;
            for (int i = 0; i < currentSlotLabels.Length; i++)
            {
                if (currentSlotLabels[i] == null) continue;
                var slotForm = i < controller.SlotCount ? controller.GetSlot(i) : null;
                currentSlotLabels[i].text = slotForm != null ? slotForm.LocalizedName : "—";
            }
        }

        /// <summary>[취소] 라벨을 현재 무장 상태대로 그린다. cancelArmed는 읽기만 한다.</summary>
        private void RefreshCancelLabel()
        {
            if (cancelLabel == null) return;
            cancelLabel.text = Loc.Get(cancelArmed ? StringKey.Form_ReplaceCancelConfirm : StringKey.Common_Cancel);
        }
    }
}
