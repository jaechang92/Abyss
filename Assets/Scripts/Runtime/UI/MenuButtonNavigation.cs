using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Abyss.Runtime.UI
{
    /// <summary>
    /// 세로 버튼 메뉴(타이틀·로비 메뉴·일시정지)의 키보드·패드 출발점 — 첫 버튼 선택, 버튼끼리만 잇는 위아래 경로,
    /// 닫을 때 자기 선택 정리.
    ///
    /// 선택만 옮기고 클릭은 하지 않는다 — 이동·결정은 EventSystem(InputSystemUIInputModule)이 선택된 버튼으로
    /// 처리한다(SettingsPanel·SaveStatusOverlay와 같은 분담). 상위 화면(저장 모달·설정·도감)이 열려 있으면
    /// 그쪽 선택을 빼앗지 않는다.
    /// </summary>
    public static class MenuButtonNavigation
    {
        private static readonly List<Button> navigableButtons = new();

        /// <summary>메뉴 위에 덮이는 화면이 입력·선택을 쥐고 있는지. 이때는 메뉴가 선택을 건드리지 않는다.</summary>
        public static bool IsUpperOverlayOpen =>
            SaveStatusOverlay.IsCapturingInput || SettingsPanel.IsOpen || CodexPanel.IsOpen;

        /// <summary>
        /// 누를 수 있는 버튼끼리만 위아래로 잇는다. 끝에서는 길을 비워 메뉴 밖(뒤 화면)으로 새지 않게 한다.
        /// 꺼졌거나 못 누르는 버튼은 경로에서 빠지고, 그 버튼 자신의 경로도 비운다.
        /// 메뉴 본체가 꺼진 채 부를 수 있으므로 그때는 activeSelf를 본다(SettingsPanel 선례).
        /// 하나라도 보이는 버튼이 있으면 메뉴가 열린 것이므로 activeInHierarchy로 좁힌다 — 꺼진 중간 묶음 아래 버튼이 경로에 끼지 않게.
        /// </summary>
        public static void ApplyVertical(IReadOnlyList<Button> buttons)
        {
            if (buttons == null) return;

            bool isMenuVisible = false;
            foreach (var button in buttons)
            {
                if (button != null && button.gameObject.activeInHierarchy)
                {
                    isMenuVisible = true;
                    break;
                }
            }

            navigableButtons.Clear();
            foreach (var button in buttons)
            {
                if (button == null) continue;
                bool isActive = isMenuVisible ? button.gameObject.activeInHierarchy : button.gameObject.activeSelf;
                if (isActive && button.IsInteractable())
                {
                    navigableButtons.Add(button);
                }
                else
                {
                    button.navigation = new Navigation { mode = Navigation.Mode.Explicit };
                }
            }

            for (int i = 0; i < navigableButtons.Count; i++)
            {
                navigableButtons[i].navigation = new Navigation
                {
                    mode = Navigation.Mode.Explicit,
                    selectOnUp = i > 0 ? navigableButtons[i - 1] : null,
                    selectOnDown = i < navigableButtons.Count - 1 ? navigableButtons[i + 1] : null,
                };
            }
            navigableButtons.Clear();
        }

        /// <summary>메뉴를 처음 열 때 1회. 실제로 보이고 누를 수 있는 첫 버튼을 선택한다. 상위 화면이 열려 있으면 하지 않는다.</summary>
        public static bool SelectFirst(IReadOnlyList<Button> buttons) => Select(null, buttons);

        /// <summary>
        /// 선택이 비었거나 꺼진 오브젝트에 남았을 때만 되돌린다(상위 화면이 닫힌 직후 등).
        /// 살아 있는 다른 선택(서사 포커스 자리·설정이 돌려준 선택)은 그대로 둔다.
        /// </summary>
        public static bool SelectIfLost(Button preferred, IReadOnlyList<Button> buttons)
        {
            var eventSystem = EventSystem.current;
            if (eventSystem == null) return false;
            var selected = eventSystem.currentSelectedGameObject;
            if (selected != null && selected.activeInHierarchy) return false;
            return Select(preferred, buttons);
        }

        /// <summary>메뉴가 닫히거나 꺼질 때. 이 메뉴의 버튼이 쥔 선택만 비운다 — 다른 화면의 선택은 건드리지 않는다.</summary>
        public static void ClearOwnedSelection(IReadOnlyList<Button> buttons)
        {
            var eventSystem = EventSystem.current;
            if (eventSystem == null || buttons == null) return;
            var selected = eventSystem.currentSelectedGameObject;
            if (selected == null) return;

            foreach (var button in buttons)
            {
                if (button != null && button.gameObject == selected)
                {
                    eventSystem.SetSelectedGameObject(null);
                    return;
                }
            }
        }

        private static bool Select(Button preferred, IReadOnlyList<Button> buttons)
        {
            if (IsUpperOverlayOpen) return false;
            var eventSystem = EventSystem.current;
            if (eventSystem == null) return false;

            var target = IsSelectable(preferred) ? preferred : FirstSelectable(buttons);
            if (target == null) return false;
            if (eventSystem.currentSelectedGameObject != target.gameObject) eventSystem.SetSelectedGameObject(target.gameObject);
            return true;
        }

        private static Button FirstSelectable(IReadOnlyList<Button> buttons)
        {
            if (buttons == null) return null;
            foreach (var button in buttons)
            {
                if (IsSelectable(button)) return button;
            }
            return null;
        }

        // 파괴된 오브젝트는 Unity 비교에서 null이다.
        private static bool IsSelectable(Button button) =>
            button != null && button.gameObject.activeInHierarchy && button.IsInteractable();
    }
}
