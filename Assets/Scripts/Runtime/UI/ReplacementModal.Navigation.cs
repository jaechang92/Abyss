using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Abyss.Runtime.UI
{
    /// <summary>
    /// <see cref="ReplacementModal"/>의 키보드·패드 조작 — 첫 포커스, 슬롯 → [취소] 탐색 경로,
    /// ESC·패드 B 취소, 닫을 때 이전 선택 복구. <see cref="FormReplacementModal"/>.Navigation과 같은 규약이다.
    ///
    /// 배치(HudBuilder.CreateReplacementModal): [슬롯 1](x=-120)·[슬롯 2](x=120)가 한 줄(y=20), 그 아래 [취소](0,-110).
    /// 그래서 실제로 누를 수 있는 슬롯(스킬이 든 칸)만 좌우로 잇고 아래는 [취소], [취소]의 위는 마지막으로 머문 슬롯(없으면 첫 슬롯)이다.
    ///
    /// 🔴 취소는 폼 보상과 달리 <b>2단계가 없다</b> — 스킬 교체 취소는 보상을 버리는 게 아니라 같은 드래프트 카드 선택으로
    /// 돌아가는 것뿐이다(<see cref="Draft.DraftSessionController.CancelReplacement"/>). ESC·패드 B는 [취소]와 같은 길로 한 번만 간다.
    ///
    /// 슬롯 교체·취소는 선택된 버튼의 Submit(EventSystem)과 ESC·패드 B로만 일어난다. 여기서는 선택만 옮긴다.
    /// 드래프트 카드를 고른 입력(Submit)과 같은 프레임에 모달이 열리므로 연 프레임의 클릭·취소는 무시하고,
    /// 같은 프레임에 [취소] 클릭과 ESC·패드 B가 겹쳐도 취소는 한 번만 센다.
    /// </summary>
    public sealed partial class ReplacementModal
    {
        // 모달을 열기 전 뒤 화면의 선택. 닫을 때 모달이 선택을 쥐고 있을 때만 돌려준다.
        private GameObject previousSelection;

        // 이번 열림에서 모달 안으로 포커스를 들였는지. 들인 뒤에 밖으로 샌 선택은 복구 대상으로 삼지 않는다.
        private bool hasEnteredFocus;

        // 연 프레임. 카드를 고른 입력과 같은 프레임의 Submit·취소로 교체·취소가 일어나지 않게 한다.
        private int openedFrame = -1;

        // 취소를 처리한 프레임. 같은 프레임의 두 번째 취소 입력(클릭+ESC/B)을 버린다.
        private int cancelHandledFrame = -1;

        // 선택을 옮기는 중인지. 선택 처리 안에서 다시 선택을 옮기는 재귀를 끊는다.
        private bool isApplyingSelection;

        // [취소]에서 위로 돌아갈 슬롯. 슬롯에 포커스가 머물 때마다 갱신한다.
        private Selectable lastFocusedSlot;

        // 왼쪽에서 오른쪽 순서의 누를 수 있는 슬롯 버튼. 갱신마다 다시 채운다.
        private readonly List<Selectable> slotNavigationItems = new();

        private bool IsOpenedThisFrame => Time.frameCount == openedFrame;
        private bool IsCancelHandledThisFrame => Time.frameCount == cancelHandledFrame;

        /// <summary>저장 모달·설정·도감이 입력과 선택을 쥐고 있는지(닫은 그 프레임 포함). 이때 모달은 교체·취소·포커스를 건드리지 않는다.</summary>
        private static bool IsUpperModalOwningInput =>
            SaveStatusOverlay.IsCapturingInput ||
            SettingsPanel.IsOpen || SettingsPanel.WasClosedThisFrame ||
            CodexPanel.IsOpen || CodexPanel.WasClosedThisFrame;

        private static bool IsUpperModalOpen =>
            SaveStatusOverlay.IsModalOpen || SettingsPanel.IsOpen || CodexPanel.IsOpen;

        // root가 이 컴포넌트의 GameObject라 열려 있을 때만 돈다.
        private void Update()
        {
            if (!isOpen) return;
            if (IsUpperModalOwningInput) return;

            if (!IsOpenedThisFrame && WasCancelPressedThisFrame())
            {
                HandleCancelInput();
                return;
            }

            KeepFocusInside();
        }

        /// <summary>
        /// 바깥에서 꺼지거나(HUD·Canvas 비활성) <see cref="Close"/>가 root를 끌 때. 모달이 쥔 선택만 비운다.
        /// 🔴 교체 취소(CancelReplacement)·세션 종료를 여기서 대신 내지 않는다 — 열린 채 바깥에서 꺼지면
        /// 드래프트 세션과 정지는 그대로 남는다(기존 한계, 결과 보고에 남김). Close를 거친 경우엔 이미 닫힌 상태다.
        /// </summary>
        private void OnDisable()
        {
            ClearOwnedSelection();
        }

        private void OnDestroy()
        {
            ClearOwnedSelection();
        }

        private static bool WasCancelPressedThisFrame()
        {
            var keyboard = Keyboard.current;
            var gamepad = Gamepad.current;
            return (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) ||
                   (gamepad != null && gamepad.buttonEast.wasPressedThisFrame);
        }

        /// <summary>버튼 클릭(마우스·Submit) 공통 관문. 닫힌 뒤·연 프레임·상위 모달이 쥔 동안·이번 프레임에 취소를 센 뒤는 무시한다.</summary>
        private bool CanAcceptClick =>
            isOpen && !IsOpenedThisFrame && !IsUpperModalOwningInput && !IsCancelHandledThisFrame;

        private void OnSlotClicked(int index)
        {
            if (!CanAcceptClick) return;
            OnSlotSelected(index);
        }

        private void OnCancelClicked()
        {
            if (!CanAcceptClick) return;
            HandleCancelInput();
        }

        /// <summary>[취소] 클릭과 ESC·패드 B가 지나는 한 길. 프레임당 한 번만 기존 <see cref="Cancel"/>(닫기 → CancelReplacement 1회)로 넘긴다.</summary>
        private void HandleCancelInput()
        {
            if (IsCancelHandledThisFrame) return;
            cancelHandledFrame = Time.frameCount;
            Cancel();
        }

        // ───────────────────────── 포커스 ─────────────────────────

        /// <summary>
        /// 열 때마다 1회. 새로 열릴 때만 이전 선택 기억을 초기화한다 — 열린 채 다시 Open이 오면
        /// 처음 기억한 뒤 화면 선택은 덮어쓰지 않는다.
        /// </summary>
        private void BeginFocus(bool isFirstOpen)
        {
            openedFrame = Time.frameCount;
            if (isFirstOpen)
            {
                previousSelection = null;
                hasEnteredFocus = false;
                lastFocusedSlot = null;
            }
            RefreshNavigation();

            // 상위 모달이 떠 있으면 그쪽 포커스를 유지한다. 모달이 닫힌 뒤 Update가 안으로 들인다.
            if (IsUpperModalOwningInput) return;
            KeepFocusInside();
        }

        /// <summary>
        /// 닫을 때. 모달이 선택을 쥐고 있을 때만(모달 안이거나 비어 있음) 이전 선택을 돌려준다.
        /// 이전 선택은 보통 숨겨진 드래프트 카드라 복구 대상이 못 되고 비워진다 — 이어서 열리는 드래프트가 첫 카드를 잡는다.
        /// 다른 화면이 쥔 선택은 그대로 두고, 상위 모달이 떠 있으면 모달이 쥔 선택만 비운다.
        /// </summary>
        private void ReleaseFocus()
        {
            var restore = previousSelection;
            previousSelection = null;
            hasEnteredFocus = false;
            lastFocusedSlot = null;

            var eventSystem = EventSystem.current;
            if (eventSystem == null) return;

            var selected = eventSystem.currentSelectedGameObject;
            if (selected != null && !IsInsideModal(selected)) return;

            if (IsUpperModalOpen)
            {
                if (selected != null) SelectSafely(eventSystem, null);
                return;
            }
            SelectSafely(eventSystem, CanRestore(restore) ? restore : null);
        }

        /// <summary>바깥에서 꺼지거나 파괴될 때. 모달이 쥔 선택만 비운다.</summary>
        private void ClearOwnedSelection()
        {
            var eventSystem = EventSystem.current;
            if (eventSystem == null) return;
            var selected = eventSystem.currentSelectedGameObject;
            if (selected != null && IsInsideModal(selected)) SelectSafely(eventSystem, null);
        }

        /// <summary>포커스가 비었거나 모달 밖(뒤 HUD·패널)으로 새면 모달 안으로 되돌린다.</summary>
        private void KeepFocusInside()
        {
            var eventSystem = EventSystem.current;
            if (eventSystem == null) return;

            var selected = eventSystem.currentSelectedGameObject;
            if (selected != null && IsInsideModal(selected))
            {
                hasEnteredFocus = true;
                RepairSelection(eventSystem, selected);
                RememberFocusedSlot(eventSystem.currentSelectedGameObject);
                return;
            }

            if (!hasEnteredFocus && selected != null) previousSelection = selected;

            var entry = EntryButton();
            if (entry == null) return;
            SelectSafely(eventSystem, entry.gameObject);
            if (eventSystem.currentSelectedGameObject == entry.gameObject) hasEnteredFocus = true;
        }

        /// <summary>선택된 버튼이 못 고르는 상태가 됐으면 첫 포커스 자리로 옮긴다. 살아 있는 선택은 그대로 둔다.</summary>
        private void RepairSelection(EventSystem eventSystem, GameObject selected)
        {
            if (selected.TryGetComponent<Selectable>(out var current) && IsNavigable(current)) return;
            var replacement = EntryButton();
            SelectSafely(eventSystem, replacement != null ? replacement.gameObject : null);
        }

        /// <summary>슬롯에 머문 포커스를 기억하고, 바뀌었으면 [취소]의 위쪽 경로를 그 슬롯으로 다시 잇는다.</summary>
        private void RememberFocusedSlot(GameObject selected)
        {
            if (selected == null) return;
            foreach (var slot in slotNavigationItems)
            {
                if (slot.gameObject != selected) continue;
                if (lastFocusedSlot == slot) return;
                lastFocusedSlot = slot;
                ApplyNavigation();
                return;
            }
        }

        /// <summary>첫 포커스: 스킬이 든 첫 슬롯, 없으면 [취소]. 포커스만 둘 뿐 교체하지 않는다.</summary>
        private Selectable EntryButton()
        {
            if (slotNavigationItems.Count > 0) return slotNavigationItems[0];
            return NavigableOrNull(cancelButton);
        }

        /// <summary>
        /// 선택을 옮긴다. EventSystem이 선택 처리 중이면(OnSelect·OnDeselect 안) 재선택을 거부하므로 건너뛰고,
        /// 다음 프레임 <see cref="KeepFocusInside"/>가 마저 보정한다.
        /// </summary>
        private void SelectSafely(EventSystem eventSystem, GameObject target)
        {
            if (isApplyingSelection || eventSystem.alreadySelecting) return;
            if (eventSystem.currentSelectedGameObject == target) return;

            isApplyingSelection = true;
            try
            {
                eventSystem.SetSelectedGameObject(target);
            }
            finally
            {
                isApplyingSelection = false;
            }
        }

        private static bool CanRestore(GameObject target)
        {
            // 파괴된 오브젝트는 Unity 비교에서 null이다.
            if (target == null || !target.activeInHierarchy) return false;
            return target.TryGetComponent<Selectable>(out var selectable) && selectable.IsInteractable();
        }

        // ───────────────────────── 탐색 경로 ─────────────────────────

        /// <summary>경로를 다시 잇는다. 열 때(슬롯 interactable이 정해진 뒤) 부른다.</summary>
        private void RefreshNavigation()
        {
            slotNavigationItems.Clear();
            for (int i = 0; i < currentSlotButtons.Length; i++)
            {
                if (IsNavigable(currentSlotButtons[i])) slotNavigationItems.Add(currentSlotButtons[i]);
            }
            if (lastFocusedSlot != null && !slotNavigationItems.Contains(lastFocusedSlot)) lastFocusedSlot = null;
            ApplyNavigation();
        }

        private void ApplyNavigation()
        {
            // 경로에서 빠진 슬롯(빈 칸)도 자기 경로를 비운다 — Automatic이 남으면 뒤 화면으로 샌다.
            foreach (var slot in currentSlotButtons) ClearNavigation(slot);
            ClearNavigation(cancelButton);

            var cancel = NavigableOrNull(cancelButton);
            for (int i = 0; i < slotNavigationItems.Count; i++)
            {
                slotNavigationItems[i].navigation = new Navigation
                {
                    mode = Navigation.Mode.Explicit,
                    selectOnLeft = i > 0 ? slotNavigationItems[i - 1] : null,
                    selectOnRight = i < slotNavigationItems.Count - 1 ? slotNavigationItems[i + 1] : null,
                    selectOnDown = cancel,
                };
            }

            if (cancel != null)
            {
                Selectable above = lastFocusedSlot != null
                    ? lastFocusedSlot
                    : (slotNavigationItems.Count > 0 ? slotNavigationItems[0] : null);
                cancel.navigation = new Navigation
                {
                    mode = Navigation.Mode.Explicit,
                    selectOnUp = above,
                };
            }
        }

        private static void ClearNavigation(Selectable selectable)
        {
            if (selectable == null) return;
            selectable.navigation = new Navigation { mode = Navigation.Mode.Explicit };
        }

        private static bool IsNavigable(Selectable selectable) =>
            selectable != null && selectable.gameObject.activeSelf && selectable.IsInteractable();

        private static Selectable NavigableOrNull(Selectable selectable) => IsNavigable(selectable) ? selectable : null;

        /// <summary>선택이 모달 안인지. root 아래만 본다. root 미배선이면 슬롯·[취소]만 본다.</summary>
        private bool IsInsideModal(GameObject target)
        {
            if (root != null) return target.transform.IsChildOf(root.transform);
            if (cancelButton != null && target == cancelButton.gameObject) return true;
            foreach (var slot in currentSlotButtons)
            {
                if (slot != null && target == slot.gameObject) return true;
            }
            return false;
        }
    }
}
