using System.Collections.Generic;
using Abyss.Runtime.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Abyss.Runtime.Lobby
{
    /// <summary>
    /// <see cref="FormSelectPanel"/>의 키보드·패드 조작 — 첫 포커스, 폼 버튼 → [선택] → [취소] 탐색 경로,
    /// ESC·패드 B 취소, 닫을 때 이전 선택 복구. <see cref="MetaUpgradePanel"/>.Navigation과 같은 규약이다.
    ///
    /// 🔴 예전에는 Update가 Enter를 직접 읽어 확정했다. Enter는 UI Submit이기도 해서, 폼 버튼에 포커스를 두고
    /// Enter를 누르면 같은 프레임에 폼 선택(Submit)과 런 확정(Update)이 함께 일어났다. 이제 확정·폼 선택은
    /// 선택된 버튼의 Submit(EventSystem) 하나로만 일어나고, 여기서는 선택만 옮긴다. 포커스를 옮기는 것만으로는
    /// 아무것도 확정되지 않는다 — 강조(선택 폼)도 버튼을 눌러야 바뀐다.
    ///
    /// 배치(LobbySceneBuilder.CreateFormSelectPanel): 폼 버튼은 배열 순서대로 왼쪽→오른쪽 한 줄(y=30),
    /// 그 아래 [선택](x=-130)·[취소](x=130). 그래서 폼 버튼은 좌우로 잇고 아래는 [선택], 두 버튼은 좌우로 잇고
    /// 위는 강조된 폼 버튼으로 돌아간다. 바깥(뒤 로비 화면)으로 나가는 방향은 비워 둔다.
    ///
    /// 포털·NPC 상호작용과 같은 입력이 연 프레임에 Submit으로 흘러 들어오지 않게 연 프레임의 클릭·취소는 무시한다.
    /// </summary>
    public sealed partial class FormSelectPanel
    {
        // 패널을 열기 전 뒤 화면의 선택. 닫을 때 패널이 선택을 쥐고 있을 때만 돌려준다.
        private GameObject previousSelection;

        // 이번 열림에서 패널 안으로 포커스를 들였는지. 들인 뒤에 밖으로 샌 선택은 복구 대상으로 삼지 않는다.
        private bool hasEnteredFocus;

        // 연 프레임. 여는 입력과 같은 프레임의 Submit·취소로 확정·취소가 일어나지 않게 한다.
        private int openedFrame = -1;

        // 선택을 옮기는 중인지. 선택 처리 안에서 다시 선택을 옮기는 재귀를 끊는다.
        private bool isApplyingSelection;

        // 왼쪽에서 오른쪽 순서의 고를 수 있는 폼 버튼. 갱신마다 다시 채운다.
        private readonly List<Selectable> formNavigationItems = new();

        private bool IsOpenedThisFrame => Time.frameCount == openedFrame;

        /// <summary>저장 모달·설정·도감이 입력과 선택을 쥐고 있는지(닫은 그 프레임 포함). 이때 패널은 확정·취소·포커스를 건드리지 않는다.</summary>
        private static bool IsUpperModalOwningInput =>
            SaveStatusOverlay.IsCapturingInput ||
            SettingsPanel.IsOpen || SettingsPanel.WasClosedThisFrame ||
            CodexPanel.IsOpen || CodexPanel.WasClosedThisFrame;

        private static bool IsUpperModalOpen =>
            SaveStatusOverlay.IsModalOpen || SettingsPanel.IsOpen || CodexPanel.IsOpen;

        private void Update()
        {
            if (!IsOpen) return;
            if (IsUpperModalOwningInput) return;

            if (!IsOpenedThisFrame && WasCancelPressedThisFrame())
            {
                Cancel();
                return;
            }

            KeepFocusInside();
        }

        /// <summary>
        /// 바깥에서 꺼질 때(로비 Canvas 비활성·씬 전환). 패널이 쥔 선택만 비운다 — 콜백은 부르지 않는다.
        /// 이 컴포넌트는 로비 공용 Canvas에 붙어 있고 root는 그 자식이라, Canvas가 다시 켜지면 열린 상태와 콜백이
        /// 그대로 이어진다(ServiceNpc의 콜백은 확정·취소 모두 busy·이동 잠금 해제뿐이며 한 번만 불린다).
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

        /// <summary>
        /// 버튼 클릭(마우스·Submit) 공통 관문. 닫힌 뒤·연 프레임·상위 모달이 쥔 동안은 무시한다 —
        /// 외부 정리용 <see cref="Close"/>는 막지 않는다.
        /// </summary>
        private bool CanAcceptClick => IsOpen && !IsOpenedThisFrame && !IsUpperModalOwningInput;

        private void OnConfirmClicked()
        {
            if (!CanAcceptClick) return;
            Confirm();
        }

        private void OnCancelClicked()
        {
            if (!CanAcceptClick) return;
            Cancel();
        }

        /// <summary>폼 버튼은 강조(선택 폼)만 바꾼다. 확정은 [선택] 버튼의 Submit으로만 일어난다.</summary>
        private void OnFormButtonClicked(int index)
        {
            if (!CanAcceptClick) return;
            SelectForm(index);
        }

        // ───────────────────────── 포커스 ─────────────────────────

        /// <summary>
        /// 열 때마다 1회. 새로 열릴 때만 이전 선택 기억을 초기화한다 — 열린 채 다시 Open이 오면
        /// 콜백은 바꾸되 처음 기억한 뒤 화면 선택은 덮어쓰지 않는다.
        /// </summary>
        private void BeginFocus(bool isFirstOpen)
        {
            openedFrame = Time.frameCount;
            if (isFirstOpen)
            {
                previousSelection = null;
                hasEnteredFocus = false;
            }
            RefreshNavigation();

            // 상위 모달이 떠 있으면 그쪽 포커스를 유지한다. 모달이 닫힌 뒤 Update가 패널 안으로 들인다.
            if (IsUpperModalOwningInput) return;
            KeepFocusInside();
        }

        /// <summary>
        /// 닫을 때. 패널이 선택을 쥐고 있을 때만(패널 안이거나 비어 있음) 이전 선택을 돌려준다.
        /// 다른 화면이 쥔 선택은 그대로 두고, 상위 모달이 떠 있으면 패널이 쥔 선택만 비운다.
        /// </summary>
        private void ReleaseFocus()
        {
            var restore = previousSelection;
            previousSelection = null;
            hasEnteredFocus = false;

            var eventSystem = EventSystem.current;
            if (eventSystem == null) return;

            var selected = eventSystem.currentSelectedGameObject;
            if (selected != null && !IsInsidePanel(selected)) return;

            if (IsUpperModalOpen)
            {
                if (selected != null) SelectSafely(eventSystem, null);
                return;
            }
            SelectSafely(eventSystem, CanRestore(restore) ? restore : null);
        }

        /// <summary>바깥에서 꺼지거나 파괴될 때. 패널이 쥔 선택만 비운다.</summary>
        private void ClearOwnedSelection()
        {
            var eventSystem = EventSystem.current;
            if (eventSystem == null) return;
            var selected = eventSystem.currentSelectedGameObject;
            if (selected != null && IsInsidePanel(selected)) SelectSafely(eventSystem, null);
        }

        /// <summary>
        /// 포커스가 비었거나 패널 밖(뒤 로비 화면)으로 새면 패널 안으로 되돌린다. 처음 들이기 전에 본 바깥 선택만
        /// 복구 대상으로 기억한다.
        /// </summary>
        private void KeepFocusInside()
        {
            var eventSystem = EventSystem.current;
            if (eventSystem == null) return;

            var selected = eventSystem.currentSelectedGameObject;
            if (selected != null && IsInsidePanel(selected))
            {
                hasEnteredFocus = true;
                RepairSelection(eventSystem, selected);
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

        /// <summary>
        /// 첫 포커스: 강조된(복원된) 폼 버튼, 그게 못 고르면 첫 번째 고를 수 있는 폼 버튼, 없으면 [선택]·[취소].
        /// 포커스만 둘 뿐 클릭하지 않는다 — 열자마자 확정되지 않는다.
        /// </summary>
        private Selectable EntryButton()
        {
            var highlighted = HighlightedFormButton();
            if (highlighted != null) return highlighted;
            if (formNavigationItems.Count > 0) return formNavigationItems[0];
            var confirm = NavigableOrNull(confirmButton);
            return confirm != null ? confirm : NavigableOrNull(cancelButton);
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

        /// <summary>경로를 다시 잇고, 선택된 버튼이 못 고르게 됐으면 보정한다. 열기·폼 선택에서 부른다.</summary>
        private void RefreshNavigation()
        {
            BuildNavigationItems();
            ApplyNavigation();

            if (isApplyingSelection || !IsOpen || IsUpperModalOwningInput) return;
            var eventSystem = EventSystem.current;
            if (eventSystem == null) return;
            var selected = eventSystem.currentSelectedGameObject;
            if (selected != null && IsInsidePanel(selected)) RepairSelection(eventSystem, selected);
        }

        /// <summary>폼이 연결된, 실제로 누를 수 있는 폼 버튼만 배열(=화면 왼쪽→오른쪽) 순서로 모은다.</summary>
        private void BuildNavigationItems()
        {
            formNavigationItems.Clear();
            if (formButtons == null) return;
            for (int i = 0; i < formButtons.Length; i++)
            {
                if (HasForm(i) && IsNavigable(formButtons[i])) formNavigationItems.Add(formButtons[i]);
            }
        }

        private void ApplyNavigation()
        {
            // 경로에서 빠진 버튼도 자기 경로를 비운다 — Automatic이 남으면 뒤 로비 화면으로 샌다.
            if (formButtons != null)
            {
                foreach (var button in formButtons) ClearNavigation(button);
            }
            ClearNavigation(confirmButton);
            ClearNavigation(cancelButton);

            var confirm = NavigableOrNull(confirmButton);
            var cancel = NavigableOrNull(cancelButton);
            Selectable below = confirm != null ? confirm : cancel;
            Selectable above = EntryFormButton();

            for (int i = 0; i < formNavigationItems.Count; i++)
            {
                formNavigationItems[i].navigation = new Navigation
                {
                    mode = Navigation.Mode.Explicit,
                    selectOnLeft = i > 0 ? formNavigationItems[i - 1] : null,
                    selectOnRight = i < formNavigationItems.Count - 1 ? formNavigationItems[i + 1] : null,
                    selectOnDown = below,
                };
            }

            if (confirm != null)
            {
                confirm.navigation = new Navigation
                {
                    mode = Navigation.Mode.Explicit,
                    selectOnUp = above,
                    selectOnRight = cancel,
                };
            }
            if (cancel != null)
            {
                cancel.navigation = new Navigation
                {
                    mode = Navigation.Mode.Explicit,
                    selectOnUp = above,
                    selectOnLeft = confirm,
                };
            }
        }

        /// <summary>[선택]·[취소]에서 위로 갈 곳: 강조된 폼 버튼, 없으면 첫 번째 고를 수 있는 폼 버튼.</summary>
        private Selectable EntryFormButton()
        {
            var highlighted = HighlightedFormButton();
            if (highlighted != null) return highlighted;
            return formNavigationItems.Count > 0 ? formNavigationItems[0] : null;
        }

        private Selectable HighlightedFormButton()
        {
            if (formButtons == null || selectedFormIndex < 0 || selectedFormIndex >= formButtons.Length) return null;
            var button = formButtons[selectedFormIndex];
            return HasForm(selectedFormIndex) && IsNavigable(button) ? button : null;
        }

        private bool HasForm(int index) =>
            selectableForms != null && index >= 0 && index < selectableForms.Length && selectableForms[index] != null;

        private static void ClearNavigation(Selectable selectable)
        {
            if (selectable == null) return;
            selectable.navigation = new Navigation { mode = Navigation.Mode.Explicit };
        }

        // 열기 직전·폼 선택 중 경로 갱신은 root 활성 여부와 무관하게 돈다 — activeSelf를 본다(MetaUpgradePanel 선례).
        private static bool IsNavigable(Selectable selectable) =>
            selectable != null && selectable.gameObject.activeSelf && selectable.IsInteractable();

        private static Selectable NavigableOrNull(Selectable selectable) => IsNavigable(selectable) ? selectable : null;

        private static bool IsSame(GameObject target, Selectable selectable) =>
            selectable != null && target == selectable.gameObject;

        /// <summary>
        /// 선택이 패널 안인지. 이 컴포넌트는 로비 공용 Canvas에 붙어 있어 gameObject 기준으로는 뒤 화면까지
        /// 안으로 보인다 — root(FormSelectRoot) 아래만 본다. root 미배선이면 폼 버튼·[선택]·[취소]만 본다.
        /// </summary>
        private bool IsInsidePanel(GameObject target)
        {
            if (root != null) return target.transform.IsChildOf(root.transform);
            if (IsSame(target, confirmButton) || IsSame(target, cancelButton)) return true;
            if (formButtons == null) return false;
            foreach (var button in formButtons)
            {
                if (IsSame(target, button)) return true;
            }
            return false;
        }
    }
}
