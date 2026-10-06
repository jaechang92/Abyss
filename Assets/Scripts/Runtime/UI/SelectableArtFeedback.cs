using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Abyss.Runtime.UI
{
    /// <summary>
    /// 버튼의 선택·호버·비활성 상태를 <see cref="ModalArtSkin"/> 장식(프레임 밝기·호버 면·이중 외곽선+좌우 홈)으로만 보여준다.
    ///
    /// EventSystem은 select/deselect를 선택된 GameObject 하나에만 보낸다 — 그래서 포커스가 실제로 머무는
    /// 버튼 GameObject에 붙는다(<see cref="SkillCardFocusRelay"/>와 같은 자리, 둘 다 이벤트를 받는다).
    ///
    /// 🔴 시각 전용이다. 클릭·Submit 처리를 만들지 않고, 선택을 옮기거나 Navigation을 바꾸지 않으며,
    /// 버튼 배경색(희귀도 등)도 건드리지 않는다. 상위 모달로 선택이 넘어가면 deselect를 받아 표식만 끈다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SelectableArtFeedback : MonoBehaviour,
        ISelectHandler, IDeselectHandler, IPointerEnterHandler, IPointerExitHandler
    {
        private static readonly Color FrameNormalColor = new(0.78f, 0.80f, 0.82f, 1f);
        private static readonly Color FrameDisabledColor = new(0.55f, 0.57f, 0.60f, 0.55f);

        private Selectable selectable;
        private Image frame;
        private Image hoverFill;
        private Image[] focusMarks;

        private bool isSelected;
        private bool isHovered;
        private bool wasInteractable = true;

        /// <summary><see cref="ModalArtSkin.EnsureSelectableSkin"/>이 장식을 만든 뒤 부른다. 다시 불러도 같은 결과다.</summary>
        public void Initialize(Selectable target, Image frameImage, Image hoverImage, Image[] marks)
        {
            selectable = target;
            frame = frameImage;
            hoverFill = hoverImage;
            focusMarks = marks;
            wasInteractable = IsInteractable();
            Refresh();
        }

        private void OnEnable()
        {
            // 다시 켜질 때 이미 선택돼 있으면 표식을 되살린다(선택 자체는 바꾸지 않는다).
            var eventSystem = EventSystem.current;
            isSelected = eventSystem != null && eventSystem.currentSelectedGameObject == gameObject;
            isHovered = false;
            wasInteractable = IsInteractable();
            Refresh();
        }

        private void OnDisable()
        {
            isSelected = false;
            isHovered = false;
            Refresh();
        }

        // interactable 변경에는 이벤트가 없다 — 바뀐 프레임에만 다시 그린다.
        private void LateUpdate()
        {
            bool isInteractable = IsInteractable();
            if (isInteractable == wasInteractable) return;
            wasInteractable = isInteractable;
            Refresh();
        }

        public void OnSelect(BaseEventData eventData)
        {
            isSelected = true;
            Refresh();
        }

        public void OnDeselect(BaseEventData eventData)
        {
            isSelected = false;
            Refresh();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            isHovered = true;
            Refresh();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            isHovered = false;
            Refresh();
        }

        private bool IsInteractable() => selectable == null || selectable.IsInteractable();

        /// <summary>
        /// 비활성: 프레임만 어둡게, 호버·선택 표식 없음. 선택: 이중 외곽선+좌우 홈+밝은 프레임.
        /// 호버: 옅은 면+밝은 프레임. 기본: 약간 어두운 프레임. 색만이 아니라 표식 유무로도 구별된다.
        /// GameObject 활성 상태를 바꾸지 않고 Graphic.enabled만 바꾼다 — 부모가 꺼지는 OnDisable 중에도 안전하다.
        /// </summary>
        private void Refresh()
        {
            bool isInteractable = IsInteractable();
            bool showFocus = isInteractable && isSelected;
            bool showHover = isInteractable && isHovered;

            if (frame != null)
            {
                frame.color = !isInteractable ? FrameDisabledColor
                    : showFocus || showHover ? Color.white
                    : FrameNormalColor;
            }
            if (hoverFill != null) hoverFill.enabled = showHover;

            if (focusMarks == null) return;
            for (int i = 0; i < focusMarks.Length; i++)
            {
                if (focusMarks[i] != null) focusMarks[i].enabled = showFocus;
            }
        }
    }
}
