using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Abyss.Runtime.UI
{
    /// <summary>
    /// <see cref="NodeMapPanel"/>의 키보드·패드 조작 — 노드 좌우 경로·첫 포커스, 열린 동안 포커스 유지, 닫을 때 자기 선택 비우기.
    /// <see cref="EventRoomPanel"/>.Navigation과 같은 규약이다.
    ///
    /// 버튼을 코드로 만들어 기본 탐색(Automatic)이 그대로면 방향 입력이 화면 위치만 보고 엉뚱한 곳으로 샌다.
    /// 그래서 실제로 보이고 누를 수 있는 노드만 좌우(Explicit)로 잇는다. 상하·양끝은 비워 패널 밖으로 나가지 않는다.
    /// 선택은 선택된 노드의 Submit(EventSystem)으로만 일어난다. 여기서는 선택만 옮기고 클릭을 직접 부르지 않는다.
    ///
    /// 🔴 ESC·패드 B는 무응답이다 — 갈림길은 선택 필수라 고르지 않고 넘어가는 길(취소·자동 선택)을 새로 만들지 않는다.
    ///
    /// 📌 닫을 때 이전 HUD 선택을 돌려주지 않는다. 갈림길 뒤에는 곧바로 방(이벤트·상점 모달)이 이어질 수 있고,
    /// 뒤 화면 선택이 살아 있으면 그 모달이 「다른 UI의 포커스」로 보고 첫 버튼을 안 잡는다. 자기 선택만 비운다.
    /// </summary>
    public sealed partial class NodeMapPanel
    {
        // 패널 내용의 뿌리(BuildContent의 body). 베이스의 body가 private라 여기 따로 둔다.
        private GameObject navigationRoot;

        // 연 프레임. 갈림길을 연 입력과 같은 프레임의 Submit으로 노드가 곧바로 확정되지 않게 한다.
        private int openedFrame = -1;

        // 선택을 옮기는 중인지. 선택 처리 안에서 다시 선택을 옮기는 재귀를 끊는다.
        private bool isApplyingSelection;

        private bool IsBodyOpen => navigationRoot != null && navigationRoot.activeSelf;

        /// <summary>저장 모달·설정·도감이 입력과 선택을 쥐고 있는지(닫은 그 프레임 포함). 이때 패널은 선택·포커스를 건드리지 않는다.</summary>
        private static bool IsUpperModalOwningInput =>
            SaveStatusOverlay.IsCapturingInput ||
            SettingsPanel.IsOpen || SettingsPanel.WasClosedThisFrame ||
            CodexPanel.IsOpen || CodexPanel.WasClosedThisFrame;

        /// <summary>노드 클릭 관문. 한 번만 — 연 프레임·이미 고른 뒤(options 비움)·상위 모달이 쥔 동안은 무시한다.</summary>
        private bool CanAcceptPick =>
            IsBodyOpen && options != null &&
            Time.frameCount != openedFrame && !IsUpperModalOwningInput;

        private void Update()
        {
            if (!IsBodyOpen || options == null) return;
            if (IsUpperModalOwningInput) return;

            KeepFocusInside();
        }

        // 바깥에서 패널이 꺼지거나 씬이 내려갈 때 패널이 쥔 선택만 비운다.
        // 🔴 onPicked 호출·정지 해제를 여기서 대신 내지 않는다 — 기존 갈림길 게이트 한계 그대로.
        private void OnDisable() => ClearOwnedSelection();

        private void OnDestroy() => ClearOwnedSelection();

        // ───────────────────────── 포커스 ─────────────────────────

        /// <summary>내용을 채우고 본체를 보인 뒤 1회. 첫 유효 노드에 포커스를 준다.</summary>
        private void BeginFocus()
        {
            openedFrame = Time.frameCount;
            RefreshNavigation();

            if (IsUpperModalOwningInput) return;   // 상위 모달이 닫힌 뒤 Update가 패널 안으로 들인다.
            KeepFocusInside();
        }

        /// <summary>패널이 쥔 선택만 비운다. 닫기 직전·바깥에서 꺼질 때. 다른 UI의 선택은 건드리지 않는다.</summary>
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
                RepairSelection(eventSystem, selected);
                return;
            }

            var entry = EntryButton();
            if (entry != null) SelectSafely(eventSystem, entry.gameObject);
        }

        /// <summary>선택된 버튼이 숨었거나 못 고르게 됐으면 첫 노드로 옮긴다. 살아 있는 선택은 그대로 둔다.</summary>
        private void RepairSelection(EventSystem eventSystem, GameObject selected)
        {
            if (selected.TryGetComponent<Selectable>(out var selectable) && IsNavigable(selectable)) return;
            var replacement = EntryButton();
            SelectSafely(eventSystem, replacement != null ? replacement.gameObject : null);
        }

        /// <summary>첫 유효 노드. 없으면 null — 포커스만 둘 뿐 누르지 않는다.</summary>
        private Selectable EntryButton()
        {
            for (int i = 0; i < nodeButtons.Count; i++)
            {
                if (IsNavigable(nodeButtons[i])) return nodeButtons[i];
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

        // ───────────────────────── 탐색 경로 ─────────────────────────

        /// <summary>경로를 다시 잇는다. 노드를 묶은 뒤(열기) 부른다.</summary>
        private void RefreshNavigation()
        {
            Button previous = null;
            foreach (var button in nodeButtons)
            {
                // 숨은 노드 자신의 경로도 비운다 — Automatic이 남으면 엉뚱한 곳으로 샌다.
                SetHorizontal(button, null, null);
                if (!IsNavigable(button)) continue;

                if (previous != null)
                {
                    SetHorizontal(previous, previous.navigation.selectOnLeft, button);
                    SetHorizontal(button, previous, null);
                }
                previous = button;
            }
        }

        // 상하는 비워 둔다 — 노드는 한 줄뿐이고 위아래로 패널 밖에 갈 곳이 없다.
        private static void SetHorizontal(Selectable selectable, Selectable left, Selectable right)
        {
            if (selectable == null) return;
            selectable.navigation = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnLeft = left,
                selectOnRight = right,
            };
        }

        // 본체가 꺼진 갱신 시점에도 판정해야 하므로 activeSelf를 본다(EventRoomPanel 선례).
        private static bool IsNavigable(Selectable selectable) =>
            selectable != null && selectable.gameObject.activeSelf && selectable.IsInteractable();

        private bool IsInsideBody(GameObject target) =>
            navigationRoot != null && target.transform.IsChildOf(navigationRoot.transform);
    }
}
