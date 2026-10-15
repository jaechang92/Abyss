using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Abyss.Runtime.UI
{
    /// <summary>
    /// 데이터 초기화 창의 키보드 조작 — ESC 뒤로, 포커스 가두기, 명시적 탐색 경로, 선택 항목 스크롤.
    ///
    /// 열려 있는 동안 설정 창은 자기 Update(닫기·포커스)를 멈춘다(SettingsPanel.Navigation의 가드). 그래서 ESC는
    /// 이 창만 소비하고, 같은 프레임에 설정까지 닫히지 않게 <see cref="WasInputConsumedThisFrame"/>를 남긴다.
    /// 저장 모달이 입력을 쥔 동안에는 아무것도 하지 않는다(SaveStatusOverlay 우선).
    /// 확인 단계의 첫 포커스는 [돌아가기]다 — 되돌릴 수 없는 버튼을 기본 선택으로 두지 않는다.
    /// </summary>
    public sealed partial class DataResetPanel
    {
        private GameObject lastScrollSelection;

        private void Update()
        {
            if (!IsOpen) return;
            if (!SettingsPanel.IsOpen)
            {
                Close();
                return;
            }
            if (SaveStatusOverlay.IsCapturingInput) return;
            if (Time.frameCount == openedFrame || WasInputConsumedThisFrame) return;
            if (WasCancelPressed())
            {
                RequestBack();
                return;
            }
            KeepFocusInside();
        }

        // 패드 전용 조작은 보류(사용자 지정) — 공용 EventSystem·키보드·포인터만 다룬다.
        private static bool WasCancelPressed() =>
            Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;

        private void SelectDefault()
        {
            var events = EventSystem.current;
            if (events == null || SaveStatusOverlay.IsCapturingInput) return;
            Selectable target = stage == Stage.Select
                ? (groupToggles.Count > 0 ? groupToggles[0] : cancelButton)
                : backButton;
            events.SetSelectedGameObject(target != null ? target.gameObject : null);
            lastScrollSelection = null;
        }

        /// <summary>취소로 닫을 때만. 이 창이 선택을 쥐고 있을 때만 설정 창의 이전 선택을 돌려준다.</summary>
        private void RestoreFocus()
        {
            var restore = previousSelection;
            previousSelection = null;
            var events = EventSystem.current;
            if (events == null || SaveStatusOverlay.IsCapturingInput) return;
            var selected = events.currentSelectedGameObject;
            if (selected != null && !IsInside(selected)) return;
            bool canRestore = restore != null && restore.activeInHierarchy &&
                              restore.TryGetComponent<Selectable>(out var selectable) && selectable.IsInteractable();
            events.SetSelectedGameObject(canRestore ? restore : null);
        }

        private void KeepFocusInside()
        {
            var events = EventSystem.current;
            if (events == null) return;
            var selected = events.currentSelectedGameObject;
            if (!IsInside(selected) || !selected.activeInHierarchy ||
                !selected.TryGetComponent<Selectable>(out var current) || !current.IsInteractable())
            {
                SelectDefault();
                selected = events.currentSelectedGameObject;
            }
            ScrollToSelection(selected);
        }

        private void ScrollToSelection(GameObject selected)
        {
            if (selected == lastScrollSelection || content == null) return;
            lastScrollSelection = selected;
            if (selected == null || !selected.transform.IsChildOf(content)) return;
            var target = (RectTransform)selected.transform;
            while (target.parent != content && target.parent is RectTransform parent) target = parent;
            float top = -target.anchoredPosition.y;
            float bottom = top + target.rect.height;
            float current = content.anchoredPosition.y;
            float height = scroll.viewport.rect.height;
            if (top < current) current = top;
            else if (bottom > current + height) current = bottom - height;
            content.anchoredPosition = new Vector2(0f, Mathf.Clamp(current, 0f, Mathf.Max(0f, content.rect.height - height)));
        }

        private bool IsInside(GameObject target) =>
            target != null && body != null && target.transform.IsChildOf(body.transform);

        /// <summary>창 안 항목끼리만 명시적으로 잇는다. 끝에서는 길을 비워 뒤 설정 창으로 새지 않는다.</summary>
        private void ConfigureNavigation()
        {
            if (stage == Stage.Select)
            {
                int last = groupToggles.Count - 1;
                for (int i = 0; i <= last; i++)
                {
                    SetNavigation(groupToggles[i],
                        up: i > 0 ? groupToggles[i - 1] : null,
                        down: i < last ? groupToggles[i + 1] : FirstNavigable(selectAllButton, fullButton));
                }
                Selectable lastToggle = last >= 0 ? groupToggles[last] : null;
                SetNavigation(selectAllButton, up: lastToggle, down: FirstNavigable(nextButton, cancelButton), right: fullButton);
                SetNavigation(fullButton, up: lastToggle, down: FirstNavigable(cancelButton, nextButton), left: selectAllButton);
                SetNavigation(nextButton, up: selectAllButton, right: cancelButton);
                SetNavigation(cancelButton, up: fullButton, left: Navigable(nextButton));
            }
            else
            {
                SetNavigation(confirmButton, right: backButton);
                SetNavigation(backButton, left: confirmButton);
            }
        }

        private static void SetNavigation(Selectable item, Selectable up = null, Selectable down = null,
            Selectable left = null, Selectable right = null)
        {
            if (item == null) return;
            item.navigation = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnUp = up,
                selectOnDown = down,
                selectOnLeft = left,
                selectOnRight = right
            };
        }

        private static Selectable Navigable(Selectable selectable) =>
            selectable != null && selectable.gameObject.activeSelf && selectable.IsInteractable() ? selectable : null;

        private static Selectable FirstNavigable(params Selectable[] items)
        {
            foreach (var item in items)
            {
                var navigable = Navigable(item);
                if (navigable != null) return navigable;
            }
            return null;
        }
    }
}
