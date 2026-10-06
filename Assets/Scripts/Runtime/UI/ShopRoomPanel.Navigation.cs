using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Abyss.Runtime.UI
{
    /// <summary>
    /// <see cref="ShopRoomPanel"/>의 키보드·패드 조작 — 첫 포커스, 구매 버튼 ↕ [떠난다] 경로, ESC·패드 B 떠나기.
    ///
    /// 버튼을 코드로 만들어 기본 탐색(Automatic)이 그대로면 방향 입력이 화면 위치만 보고 엉뚱한 곳으로 샌다.
    /// 그래서 실제로 보이고 살 수 있는 구매 버튼과 [떠난다]만 위아래(Explicit)로 잇는다 — 숨은 자리·품절·잔액 부족은 빠진다.
    /// 구매는 선택된 버튼의 Submit(EventSystem)으로만 일어난다. 여기서는 선택만 옮기고 클릭을 직접 부르지 않는다.
    /// ESC·패드 B는 [떠난다] 버튼과 같은 <see cref="OnLeaveClicked"/>로 흘린다.
    /// </summary>
    public sealed partial class ShopRoomPanel
    {
        // 패널 내용의 뿌리(BuildContent의 body). 베이스의 body가 private라 여기 따로 둔다.
        private GameObject navigationRoot;
        private Button leaveButton;

        // 연 프레임. 여는 입력과 같은 프레임의 취소 입력으로 열자마자 떠나지 않게 한다.
        private int openedFrame = -1;

        // 선택을 옮기는 중인지. 선택 처리 안에서 다시 선택을 옮기는 재귀를 끊는다.
        private bool isApplyingSelection;

        private readonly List<Button> navigableButtons = new();

        private bool IsBodyOpen => navigationRoot != null && navigationRoot.activeSelf;

        /// <summary>저장 모달·설정이 입력과 선택을 쥐고 있는지(설정을 닫은 그 프레임 포함). 이때 상점은 떠나기·포커스를 건드리지 않는다.</summary>
        private static bool IsUpperModalOwningInput =>
            SaveStatusOverlay.IsCapturingInput || SettingsPanel.IsOpen || SettingsPanel.WasClosedThisFrame;

        private void Update()
        {
            if (!IsBodyOpen || current == null) return;
            if (IsUpperModalOwningInput) return;

            if (Time.frameCount != openedFrame && WasCancelPressedThisFrame())
            {
                OnLeaveClicked();
                return;
            }

            KeepFocusInside();
        }

        // 바깥에서 패널이 꺼지거나 씬이 내려갈 때 상점이 쥔 선택만 비운다 — 꺼진 버튼에 선택이 남지 않게.
        private void OnDisable() => ClearOwnedSelection();

        private void OnDestroy() => ClearOwnedSelection();

        private static bool WasCancelPressedThisFrame()
        {
            var keyboard = Keyboard.current;
            var gamepad = Gamepad.current;
            return (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) ||
                   (gamepad != null && gamepad.buttonEast.wasPressedThisFrame);
        }

        /// <summary>내용을 채우고 본체를 보인 뒤 1회. 첫 유효 구매 버튼(전부 못 사면 [떠난다])에 포커스를 준다.</summary>
        private void BeginFocus()
        {
            openedFrame = Time.frameCount;
            RefreshNavigation();

            if (IsUpperModalOwningInput) return;   // 상위 모달이 닫힌 뒤 Update가 상점 안으로 들인다.
            var eventSystem = EventSystem.current;
            if (eventSystem == null) return;
            SelectSafely(eventSystem, EntryButton());
        }

        /// <summary>
        /// 떠날 때. 상점이 쥔 선택만 비운다 — 이어서 열리는 드래프트가 잡을 선택은 건드리지 않는다.
        /// </summary>
        private void ClearOwnedSelection()
        {
            var eventSystem = EventSystem.current;
            if (eventSystem == null) return;
            var selected = eventSystem.currentSelectedGameObject;
            if (selected != null && IsInsideBody(selected)) SelectSafely(eventSystem, null);
        }

        /// <summary>
        /// 포커스가 비었거나 상점 밖으로 새면 상점 안으로 되돌린다. 마우스로 빈 곳을 누르면 선택이 풀려
        /// 그 뒤 패드 입력이 갈 곳이 없어진다.
        /// </summary>
        private void KeepFocusInside()
        {
            var eventSystem = EventSystem.current;
            if (eventSystem == null) return;

            var selected = eventSystem.currentSelectedGameObject;
            if (selected != null && IsInsideBody(selected))
            {
                RepairSelection(eventSystem, selected);
                return;
            }
            SelectSafely(eventSystem, EntryButton());
        }

        // ───────────────────────── 탐색 경로 ─────────────────────────

        /// <summary>
        /// 경로를 다시 잇고, 선택된 버튼이 숨었거나 못 누르게 됐으면 보정한다.
        /// 진열이 바뀌는 모든 경로(열기·구매)가 지나는 <see cref="RefreshItems"/> 끝에서 부른다.
        /// </summary>
        private void RefreshNavigation()
        {
            ApplyNavigation();

            if (isApplyingSelection || !IsBodyOpen || IsUpperModalOwningInput) return;
            var eventSystem = EventSystem.current;
            if (eventSystem == null) return;
            var selected = eventSystem.currentSelectedGameObject;
            if (selected != null && IsInsideBody(selected)) RepairSelection(eventSystem, selected);
        }

        private void ApplyNavigation()
        {
            navigableButtons.Clear();
            for (int i = 0; i < buyButtons.Count; i++)
            {
                if (IsBuyNavigable(i)) navigableButtons.Add(buyButtons[i]);
                // 숨은·품절·잔액 부족 버튼 자신의 경로도 비운다 — Automatic이 남으면 엉뚱한 곳으로 샌다.
                else SetVertical(buyButtons[i], null, null);
            }
            if (IsNavigable(leaveButton)) navigableButtons.Add(leaveButton);

            for (int i = 0; i < navigableButtons.Count; i++)
            {
                SetVertical(navigableButtons[i],
                    i > 0 ? navigableButtons[i - 1] : null,
                    i < navigableButtons.Count - 1 ? navigableButtons[i + 1] : null);
            }
            navigableButtons.Clear();
        }

        /// <summary>
        /// 선택된 버튼이 이제 못 고르는 상태(품절·잔액 부족)가 됐으면 그 아래의 다음 유효 구매 버튼으로,
        /// 없으면 [떠난다]로 옮긴다. 살아 있는 선택은 그대로 둔다.
        /// </summary>
        private void RepairSelection(EventSystem eventSystem, GameObject selected)
        {
            if (IsSame(selected, leaveButton))
            {
                if (!IsNavigable(leaveButton)) SelectSafely(eventSystem, EntryButton());
                return;
            }

            int slot = BuySlotOf(selected);
            if (slot >= 0 && IsBuyNavigable(slot)) return;

            SelectSafely(eventSystem, slot >= 0 ? NextBuyOrLeave(slot + 1) : EntryButton());
        }

        /// <summary>첫 유효 구매 버튼, 하나도 없으면 [떠난다].</summary>
        private Button EntryButton() => NextBuyOrLeave(0);

        private Button NextBuyOrLeave(int start)
        {
            for (int i = start; i < buyButtons.Count; i++)
            {
                if (IsBuyNavigable(i)) return buyButtons[i];
            }
            return IsNavigable(leaveButton) ? leaveButton : null;
        }

        /// <summary>
        /// 선택을 옮긴다. EventSystem이 선택 처리 중이면(OnSelect·OnDeselect 안) 재선택을 거부하므로 건너뛰고,
        /// 다음 프레임 <see cref="KeepFocusInside"/>가 마저 보정한다.
        /// </summary>
        private void SelectSafely(EventSystem eventSystem, Selectable target)
        {
            if (isApplyingSelection || eventSystem.alreadySelecting) return;
            var targetObject = target != null ? target.gameObject : null;
            if (eventSystem.currentSelectedGameObject == targetObject) return;

            isApplyingSelection = true;
            try
            {
                eventSystem.SetSelectedGameObject(targetObject);
            }
            finally
            {
                isApplyingSelection = false;
            }
        }

        // 본체가 꺼진 갱신 시점(열기 직전 RefreshItems)에도 판정해야 하므로 activeSelf를 본다(SettingsPanel 선례).
        private bool IsBuyNavigable(int slot) =>
            slot >= 0 && slot < buyButtons.Count && slot < itemRows.Count &&
            itemRows[slot] != null && itemRows[slot].activeSelf && IsNavigable(buyButtons[slot]);

        private static bool IsNavigable(Selectable selectable) =>
            selectable != null && selectable.gameObject.activeSelf && selectable.IsInteractable();

        private int BuySlotOf(GameObject target)
        {
            for (int i = 0; i < buyButtons.Count; i++)
            {
                if (IsSame(target, buyButtons[i])) return i;
            }
            return -1;
        }

        private static void SetVertical(Selectable selectable, Selectable up, Selectable down)
        {
            if (selectable == null) return;
            selectable.navigation = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnUp = up,
                selectOnDown = down,
            };
        }

        private static bool IsSame(GameObject target, Selectable selectable) =>
            selectable != null && target == selectable.gameObject;

        private bool IsInsideBody(GameObject target) =>
            navigationRoot != null && target.transform.IsChildOf(navigationRoot.transform);
    }
}
