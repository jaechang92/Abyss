using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Abyss.Runtime.UI
{
    /// <summary>
    /// <see cref="EventRoomPanel"/>의 키보드·패드 조작 — 선택지 상하 경로·첫 포커스, 결과 단계 [계속] 포커스·ESC/패드 B,
    /// 닫을 때 이전 선택 복구. <see cref="ShopRoomPanel"/>.Navigation과 같은 규약이다.
    ///
    /// 버튼을 코드로 만들어 기본 탐색(Automatic)이 그대로면 방향 입력이 화면 위치만 보고 엉뚱한 곳으로 샌다.
    /// 그래서 실제로 보이고 누를 수 있는 선택지만 위아래(Explicit)로 잇는다 — 숨은 자리·잔액 부족은 빠진다.
    /// 선택·계속은 선택된 버튼의 Submit(EventSystem)으로만 일어난다. 여기서는 선택만 옮기고 클릭을 직접 부르지 않는다.
    ///
    /// 🔴 ESC·패드 B는 <b>결과 단계에서만</b> [계속]과 같은 <see cref="OnContinueClicked"/>로 흘린다.
    /// 선택 단계에선 무응답이다 — 고르지 않고 넘어가는 길(공짜 진행·자동 포기)을 새로 만들지 않는다.
    /// 선택지가 전부 못 고르는 데이터(전부 잔액 부족)면 포커스 자리가 없고 진행도 안 된다 — 기존 데이터 한계로 남긴다.
    /// </summary>
    public sealed partial class EventRoomPanel
    {
        // 패널 내용의 뿌리(BuildContent의 body). 베이스의 body가 private라 여기 따로 둔다.
        private GameObject navigationRoot;

        // 연 프레임. 방에 들어온 입력과 같은 프레임의 Submit으로 선택지가 눌리지 않게 한다.
        private int openedFrame = -1;

        // 결과를 띄운 프레임. 선택지를 누른 Submit·ESC가 같은 프레임에 [계속]까지 넘기지 않게 한다.
        private int resultShownFrame = -1;

        // 열기 전 뒤 화면의 선택. 닫을 때 패널이 선택을 쥐고 있을 때만 돌려준다.
        private GameObject previousSelection;

        // 이번 열림에서 패널 안으로 포커스를 들였는지. 들인 뒤에 밖으로 샌 선택은 복구 대상으로 삼지 않는다.
        private bool hasEnteredFocus;

        // 선택을 옮기는 중인지. 선택 처리 안에서 다시 선택을 옮기는 재귀를 끊는다.
        private bool isApplyingSelection;

        private readonly List<Button> navigableChoices = new();

        private bool IsBodyOpen => navigationRoot != null && navigationRoot.activeSelf;

        /// <summary>저장 모달·설정·도감이 입력과 선택을 쥐고 있는지(닫은 그 프레임 포함). 이때 패널은 선택·계속·포커스를 건드리지 않는다.</summary>
        private static bool IsUpperModalOwningInput =>
            SaveStatusOverlay.IsCapturingInput ||
            SettingsPanel.IsOpen || SettingsPanel.WasClosedThisFrame ||
            CodexPanel.IsOpen || CodexPanel.WasClosedThisFrame;

        private static bool IsUpperModalOpen =>
            SaveStatusOverlay.IsModalOpen || SettingsPanel.IsOpen || CodexPanel.IsOpen;

        /// <summary>선택지 클릭 관문. 선택 단계 한 번만 — 연 프레임·이미 고른 뒤·상위 모달이 쥔 동안은 무시한다.</summary>
        private bool CanAcceptChoice =>
            IsBodyOpen && current != null && chosen == null &&
            Time.frameCount != openedFrame && !IsUpperModalOwningInput;

        /// <summary>[계속] 관문. 결과 단계 한 번만 — 결과를 띄운 프레임·상위 모달이 쥔 동안은 무시한다.</summary>
        private bool CanAcceptContinue =>
            IsBodyOpen && current != null && chosen != null &&
            Time.frameCount != resultShownFrame && !IsUpperModalOwningInput;

        private void Update()
        {
            if (!IsBodyOpen || current == null) return;
            if (IsUpperModalOwningInput) return;

            // 결과 단계에서만 ESC·패드 B = [계속]. 관문(CanAcceptContinue)이 결과를 띄운 프레임을 거른다.
            if (chosen != null && CanAcceptContinue && WasCancelPressedThisFrame())
            {
                OnContinueClicked();
                return;
            }

            KeepFocusInside();
        }

        // 바깥에서 패널이 꺼지거나 씬이 내려갈 때 패널이 쥔 선택만 비운다.
        // 🔴 이벤트 해결(EventResolved)·정지 해제를 여기서 대신 내지 않는다 — 기존 게이트 한계 그대로.
        private void OnDisable() => ClearOwnedSelection();

        private void OnDestroy() => ClearOwnedSelection();

        private static bool WasCancelPressedThisFrame()
        {
            var keyboard = Keyboard.current;
            var gamepad = Gamepad.current;
            return (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) ||
                   (gamepad != null && gamepad.buttonEast.wasPressedThisFrame);
        }

        // ───────────────────────── 포커스 ─────────────────────────

        /// <summary>내용을 채우고 본체를 보인 뒤 1회. 첫 유효 선택지에 포커스를 준다.</summary>
        private void BeginFocus()
        {
            openedFrame = Time.frameCount;
            resultShownFrame = -1;
            previousSelection = null;
            hasEnteredFocus = false;
            RefreshNavigation();

            if (IsUpperModalOwningInput) return;   // 상위 모달이 닫힌 뒤 Update가 패널 안으로 들인다.
            KeepFocusInside();
        }

        /// <summary>선택지를 고른 직후 1회. 선택지가 숨고 [계속]만 남으니 경로를 다시 잇고 [계속]에 포커스를 준다.</summary>
        private void BeginResultFocus()
        {
            resultShownFrame = Time.frameCount;
            RefreshNavigation();

            if (IsUpperModalOwningInput) return;
            var eventSystem = EventSystem.current;
            if (eventSystem == null) return;
            var entry = EntryButton();
            if (entry != null) SelectSafely(eventSystem, entry.gameObject);
        }

        /// <summary>
        /// 닫을 때. 패널이 선택을 쥐고 있을 때만(패널 안이거나 비어 있음) 이전 선택을 돌려준다.
        /// 이어서 드래프트가 열리면(<paramref name="hasFollowUpModal"/>) 돌려주지 않고 비우기만 한다 —
        /// 뒤 화면 선택이 살아 있으면 드래프트가 「다른 UI의 포커스」로 보고 첫 카드를 안 잡는다.
        /// </summary>
        private void ReleaseFocus(bool hasFollowUpModal)
        {
            var restore = previousSelection;
            previousSelection = null;
            hasEnteredFocus = false;

            var eventSystem = EventSystem.current;
            if (eventSystem == null) return;

            var selected = eventSystem.currentSelectedGameObject;
            if (selected != null && !IsInsideBody(selected)) return;

            if (hasFollowUpModal || IsUpperModalOpen)
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
            if (selected != null && IsInsideBody(selected)) SelectSafely(eventSystem, null);
        }

        /// <summary>포커스가 비었거나 패널 밖(뒤 HUD)으로 새면 패널 안으로 되돌린다.</summary>
        private void KeepFocusInside()
        {
            var eventSystem = EventSystem.current;
            if (eventSystem == null) return;

            var selected = eventSystem.currentSelectedGameObject;
            if (selected != null && IsInsideBody(selected))
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

        /// <summary>선택된 버튼이 숨었거나 못 고르게 됐으면 첫 포커스 자리로 옮긴다. 살아 있는 선택은 그대로 둔다.</summary>
        private void RepairSelection(EventSystem eventSystem, GameObject selected)
        {
            if (selected.TryGetComponent<Selectable>(out var selectable) && IsNavigable(selectable)) return;
            var replacement = EntryButton();
            SelectSafely(eventSystem, replacement != null ? replacement.gameObject : null);
        }

        /// <summary>결과 단계면 [계속], 선택 단계면 첫 유효 선택지. 없으면 null — 포커스만 둘 뿐 누르지 않는다.</summary>
        private Selectable EntryButton()
        {
            if (chosen != null) return IsNavigable(continueButton) ? continueButton : null;
            for (int i = 0; i < choiceButtons.Count; i++)
            {
                if (IsNavigable(choiceButtons[i])) return choiceButtons[i];
            }
            return null;
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

        /// <summary>경로를 다시 잇는다. 선택지를 묶은 뒤(열기)와 결과로 넘어갈 때 부른다.</summary>
        private void RefreshNavigation()
        {
            navigableChoices.Clear();
            foreach (var button in choiceButtons)
            {
                // 숨은·잔액 부족 선택지 자신의 경로도 비운다 — Automatic이 남으면 엉뚱한 곳으로 샌다.
                if (IsNavigable(button)) navigableChoices.Add(button);
                else SetVertical(button, null, null);
            }

            for (int i = 0; i < navigableChoices.Count; i++)
            {
                SetVertical(navigableChoices[i],
                    i > 0 ? navigableChoices[i - 1] : null,
                    i < navigableChoices.Count - 1 ? navigableChoices[i + 1] : null);
            }
            navigableChoices.Clear();

            // [계속]은 혼자 뜬다(선택지와 동시 표시 없음) — 어디로도 새지 않게 경로를 비운다.
            SetVertical(continueButton, null, null);
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

        // 본체가 꺼진 갱신 시점에도 판정해야 하므로 activeSelf를 본다(ShopRoomPanel 선례).
        private static bool IsNavigable(Selectable selectable) =>
            selectable != null && selectable.gameObject.activeSelf && selectable.IsInteractable();

        private bool IsInsideBody(GameObject target) =>
            navigationRoot != null && target.transform.IsChildOf(navigationRoot.transform);
    }
}
