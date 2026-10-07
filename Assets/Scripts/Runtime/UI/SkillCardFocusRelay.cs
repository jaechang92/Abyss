using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Abyss.Runtime.UI
{
    /// <summary>
    /// 드래프트 카드의 <b>선택 버튼 GameObject</b>에 붙어 select/deselect/pointer enter/exit를 카드로 넘긴다.
    ///
    /// EventSystem은 select/deselect를 <b>선택된 GameObject 하나에만</b> 보낸다 — 부모로 전파하지 않는다.
    /// 그래서 카드 루트(<see cref="SkillCardView"/>)에 ISelectHandler를 달아도 자식 버튼이 포커스를 받을 때
    /// 아무것도 오지 않는다. 이벤트가 실제로 도착하는 버튼 쪽에 받는 쪽을 둔다.
    ///
    /// 포커스·호버를 알릴 뿐 클릭(스킬 선택)은 하지 않는다 — 선택은 기존 Button.onClick 경로 그대로다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SkillCardFocusRelay : MonoBehaviour,
        ISelectHandler, IDeselectHandler, IPointerEnterHandler, IPointerExitHandler
    {
        public event Action OnFocusSelected;
        public event Action OnFocusDeselected;
        public event Action OnPointerEntered;
        public event Action OnPointerExited;

        public void OnSelect(BaseEventData eventData) => OnFocusSelected?.Invoke();

        public void OnDeselect(BaseEventData eventData) => OnFocusDeselected?.Invoke();

        public void OnPointerEnter(PointerEventData eventData) => OnPointerEntered?.Invoke();

        public void OnPointerExit(PointerEventData eventData) => OnPointerExited?.Invoke();
    }
}
