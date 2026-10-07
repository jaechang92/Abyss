using System.Collections.Generic;
using Abyss.Runtime.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Abyss.Runtime.Lobby
{
    /// <summary>
    /// <see cref="RelicShopPanel"/>의 키보드·패드 조작 — 첫 포커스, 상점 안 탐색 경로, ESC·패드 B 닫기,
    /// 닫을 때 이전 선택 복구.
    ///
    /// 버튼을 코드로 만들어 기본 탐색(Automatic)이 그대로면 방향 입력이 화면 위치만 보고 숨은 타일·빈 슬롯이나
    /// 상점 밖으로 샌다. 그래서 실제로 보이고 누를 수 있는 버튼만 화면의 줄 순서대로 명시적(Explicit)으로 잇는다 —
    /// 장착 슬롯 줄 → 보유 타일 그리드의 실제 행들 → [감정]·[떠난다] 줄. 한 줄 안은 좌우, 줄끼리는 위아래로
    /// 가장 가까운 x의 버튼으로 간다. 빈 줄은 빠지므로 보유 0개·잔액 0이어도 [떠난다]까지 닿는다.
    ///
    /// 감정·장착·해제·닫기는 선택된 버튼의 Submit(EventSystem)으로만 일어난다. 여기서는 선택만 옮긴다.
    /// NPC 상호작용과 같은 입력이 연 프레임에 Submit으로도 흘러 들어오지 않게 연 프레임의 클릭·취소는 무시한다.
    /// </summary>
    public sealed partial class RelicShopPanel
    {
        // 상점 박스. 선택이 상점 안인지 판정하는 기준이다.
        private RectTransform navigationBox;
        private Button closeButton;

        // 상점을 열기 전 뒤 화면의 선택. 닫을 때 상점이 선택을 쥐고 있을 때만 돌려준다.
        private GameObject previousSelection;

        // 이번 열림에서 상점 안으로 포커스를 들였는지. 들인 뒤에 밖으로 샌 선택은 복구 대상으로 삼지 않는다.
        private bool hasEnteredFocus;

        // 연 프레임. 여는 입력과 같은 프레임의 Submit·취소로 감정·장착·닫기가 일어나지 않게 한다.
        private int openedFrame = -1;

        // 선택을 옮기는 중인지. 선택 처리 안에서 다시 선택을 옮기는 재귀를 끊는다.
        private bool isApplyingSelection;

        // 바깥 비활성으로 취소됐지만 그 자리에서 root를 끄지 못했다. 다시 켜진 뒤 첫 Update에서 끈다.
        private bool isRootHidePending;

        // 위에서 아래 순서의 탐색 줄. 갱신마다 다시 채운다.
        private readonly List<List<Selectable>> navigationRows = new();

        private bool IsOpenedThisFrame => Time.frameCount == openedFrame;

        /// <summary>저장 모달·설정·도감이 입력과 선택을 쥐고 있는지(닫은 그 프레임 포함). 이때 상점은 닫기·포커스를 건드리지 않는다.</summary>
        private static bool IsUpperModalOwningInput =>
            SaveStatusOverlay.IsCapturingInput ||
            SettingsPanel.IsOpen || SettingsPanel.WasClosedThisFrame ||
            CodexPanel.IsOpen || CodexPanel.WasClosedThisFrame;

        private static bool IsUpperModalOpen =>
            SaveStatusOverlay.IsModalOpen || SettingsPanel.IsOpen || CodexPanel.IsOpen;

        private void Update()
        {
            if (isRootHidePending)
            {
                // 바깥 비활성으로 취소된 뒤 다시 켜졌다 — 남은 상점 화면을 닫힌 상태에 맞춘다.
                isRootHidePending = false;
                if (!IsOpen && root != null) root.SetActive(false);
            }
            if (!IsOpen) return;
            if (IsUpperModalOwningInput) return;

            if (!IsOpenedThisFrame && WasCancelPressedThisFrame())
            {
                Close();
                return;
            }

            KeepFocusInside();
        }

        /// <summary>
        /// 바깥에서 꺼질 때(씬 전환·상위 오브젝트 비활성·컴포넌트 끔). 열린 채였다면 열림 표시를 정리하고 콜백을 한 번 부른다 —
        /// <see cref="RelicShopNpc"/>의 콜백은 busy·이동 잠금 해제만 하므로 여기서 불러도 안전하고,
        /// 부르지 않으면 NPC가 다시 열리지 않고 플레이어가 묶인 채 남는다.
        /// <see cref="Close"/>가 root를 끄며 여기 들어올 때는 이미 IsOpen·콜백이 비어 있어 선택만 정리된다.
        /// </summary>
        private void OnDisable()
        {
            if (IsOpen) CancelByExternalDisable(hideRoot: true);
            else ClearOwnedSelection();
        }

        /// <summary>파괴 중에는 root를 건드리지 않는다 — 콜백·선택만 정리한다.</summary>
        private void OnDestroy()
        {
            if (IsOpen) CancelByExternalDisable(hideRoot: false);
            else ClearOwnedSelection();
            onClosed = null;
            isRootHidePending = false;
        }

        /// <summary>상태와 콜백을 먼저 비운 뒤 표시를 정리하고, 지역에 잡아 둔 콜백을 한 번만 부른다.</summary>
        private void CancelByExternalDisable(bool hideRoot)
        {
            IsOpen = false;
            var closed = onClosed;
            onClosed = null;
            ReleaseFocus();
            if (hideRoot) HideRootAfterExternalDisable();
            closed?.Invoke();
        }

        /// <summary>
        /// 바깥에서 꺼졌는데 root가 켜진 채면(상위 비활성·컴포넌트 끔) 다시 켜졌을 때 IsOpen=false인 상점 화면이
        /// 조작도 닫기도 안 되는 채로 보인다. 그래서 root 표시도 끈다.
        ///
        /// 🔴 OnDisable 안에서 바로 끄는 것은 컴포넌트만 꺼진 경우(enabled=false, 오브젝트는 계층상 활성)뿐이다 —
        /// 오브젝트 활성 전환 중이거나 파괴 중일 수 있는 나머지 경우에 SetActive를 부르면 위험하므로 미뤄 두고,
        /// 다시 켜진 뒤 첫 Update에서 끈다(파괴되면 다시 켜지지 않으니 미룬 채 사라진다).
        /// </summary>
        private void HideRootAfterExternalDisable()
        {
            if (root == null || !root.activeSelf) return;
            if (!enabled && gameObject.activeInHierarchy)
            {
                isRootHidePending = false;
                root.SetActive(false);
                return;
            }
            isRootHidePending = true;
        }

        private static bool WasCancelPressedThisFrame()
        {
            var keyboard = Keyboard.current;
            var gamepad = Gamepad.current;
            return (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) ||
                   (gamepad != null && gamepad.buttonEast.wasPressedThisFrame);
        }

        /// <summary>[떠난다] 버튼. 연 프레임의 Submit은 무시한다 — 외부 정리용 <see cref="Close"/>는 막지 않는다.</summary>
        private void OnCloseClicked()
        {
            if (IsOpenedThisFrame) return;
            Close();
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

            // 상위 모달이 떠 있으면 그쪽 포커스를 유지한다. 모달이 닫힌 뒤 Update가 상점 안으로 들인다.
            if (IsUpperModalOwningInput) return;
            KeepFocusInside();
        }

        /// <summary>
        /// 닫을 때. 상점이 선택을 쥐고 있을 때만(상점 안이거나 비어 있음) 이전 선택을 돌려준다.
        /// 다른 화면이 쥔 선택은 그대로 두고, 상위 모달이 떠 있으면 상점이 쥔 선택만 비운다.
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

        /// <summary>바깥에서 꺼지거나 파괴될 때. 상점이 쥔 선택만 비운다.</summary>
        private void ClearOwnedSelection()
        {
            var eventSystem = EventSystem.current;
            if (eventSystem == null) return;
            var selected = eventSystem.currentSelectedGameObject;
            if (selected != null && IsInsidePanel(selected)) SelectSafely(eventSystem, null);
        }

        /// <summary>
        /// 포커스가 비었거나 상점 밖(뒤 메뉴 등)으로 새면 상점 안으로 되돌린다. 처음 들이기 전에 본 바깥 선택만
        /// 복구 대상으로 기억한다 — 연 순간 저장 모달이 떠 있었다면 모달이 돌려준 뒤 화면 선택이 여기 온다.
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

        /// <summary>
        /// 선택된 버튼이 못 고르는 상태가 됐으면 옮긴다 — 해제로 빈 슬롯은 가장 가까운 채워진 슬롯으로,
        /// 조각이 떨어진 [감정]은 [떠난다]로, 그 밖에는 첫 진입 버튼으로. 살아 있는 선택은 그대로 둔다.
        /// </summary>
        private void RepairSelection(EventSystem eventSystem, GameObject selected)
        {
            if (selected.TryGetComponent<Selectable>(out var current) && IsNavigable(current)) return;

            Selectable replacement = null;
            if (IsSlotButton(selected)) replacement = NearestNavigableSlot(PositionX(selected.transform));
            else if (IsSame(selected, drawButton)) replacement = NavigableOrNull(closeButton);
            if (replacement == null) replacement = EntryButton();

            SelectSafely(eventSystem, replacement != null ? replacement.gameObject : null);
        }

        /// <summary>첫 포커스: 감정할 수 있으면 [감정], 아니면 보유 타일 → 채운 슬롯, 그것도 없으면 [떠난다].</summary>
        private Selectable EntryButton()
        {
            if (IsNavigable(drawButton)) return drawButton;
            foreach (var tile in tiles)
            {
                if (tile != null && IsNavigable(tile.Button)) return tile.Button;
            }
            foreach (var slot in slots)
            {
                if (slot != null && IsNavigable(slot.Button)) return slot.Button;
            }
            return NavigableOrNull(closeButton);
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

        /// <summary>
        /// 경로를 다시 잇고, 선택된 버튼이 못 고르게 됐으면 보정한다. 감정·장착·해제·열기가 모두 지나는
        /// <see cref="Refresh"/> 끝에서 부른다 — 새로 나타난 타일, 채워지거나 빈 슬롯, 조각 소진이 여기서 반영된다.
        /// </summary>
        private void RefreshNavigation()
        {
            BuildNavigationRows();
            ApplyNavigation();

            if (isApplyingSelection || !IsOpen || IsUpperModalOwningInput) return;
            var eventSystem = EventSystem.current;
            if (eventSystem == null) return;
            var selected = eventSystem.currentSelectedGameObject;
            if (selected != null && IsInsidePanel(selected)) RepairSelection(eventSystem, selected);
        }

        private void BuildNavigationRows()
        {
            navigationRows.Clear();

            var slotRow = new List<Selectable>(slots.Count);
            foreach (var slot in slots)
            {
                if (slot != null && IsNavigable(slot.Button)) slotRow.Add(slot.Button);
            }
            AddRow(slotRow);

            // 타일은 카탈로그 순서(행 우선)로 들어 있다 — 같은 그리드 행끼리 묶으면 열 순서도 그대로다.
            List<Selectable> tileRow = null;
            int currentRow = -1;
            foreach (var tile in tiles)
            {
                if (tile == null || !IsNavigable(tile.Button)) continue;
                if (tileRow == null || tile.Row != currentRow)
                {
                    AddRow(tileRow);
                    tileRow = new List<Selectable>(GRID_COLS);
                    currentRow = tile.Row;
                }
                tileRow.Add(tile.Button);
            }
            AddRow(tileRow);

            var footerRow = new List<Selectable>(2);
            if (IsNavigable(drawButton)) footerRow.Add(drawButton);
            if (IsNavigable(closeButton)) footerRow.Add(closeButton);
            AddRow(footerRow);
        }

        private void AddRow(List<Selectable> row)
        {
            if (row != null && row.Count > 0) navigationRows.Add(row);
        }

        private void ApplyNavigation()
        {
            // 경로에서 빠진 버튼(숨은 타일·빈 슬롯·잔액 부족 감정)도 자기 경로를 비운다 — Automatic이 남으면 엉뚱한 곳으로 샌다.
            foreach (var slot in slots) ClearNavigation(slot?.Button);
            foreach (var tile in tiles) ClearNavigation(tile?.Button);
            ClearNavigation(drawButton);
            ClearNavigation(closeButton);

            for (int r = 0; r < navigationRows.Count; r++)
            {
                var row = navigationRows[r];
                for (int c = 0; c < row.Count; c++)
                {
                    var item = row[c];
                    float x = PositionX(item.transform);
                    // 맨 위·맨 아래·줄 끝은 비워 둔다 — 상점 밖(뒤 화면)으로 나가지 않는다.
                    item.navigation = new Navigation
                    {
                        mode = Navigation.Mode.Explicit,
                        selectOnLeft = c > 0 ? row[c - 1] : null,
                        selectOnRight = c < row.Count - 1 ? row[c + 1] : null,
                        selectOnUp = NearestInRow(r - 1, x),
                        selectOnDown = NearestInRow(r + 1, x),
                    };
                }
            }
        }

        /// <summary>인접 줄에서 x가 가장 가까운 버튼. 슬롯·타일·하단 버튼이 모두 박스 상단 중앙 기준이라 x를 그대로 비교한다.</summary>
        private Selectable NearestInRow(int rowIndex, float x)
        {
            if (rowIndex < 0 || rowIndex >= navigationRows.Count) return null;
            return Nearest(navigationRows[rowIndex], x);
        }

        private Selectable NearestNavigableSlot(float x)
        {
            Selectable best = null;
            float bestDistance = float.MaxValue;
            foreach (var slot in slots)
            {
                if (slot == null || !IsNavigable(slot.Button)) continue;
                float distance = Mathf.Abs(PositionX(slot.Button.transform) - x);
                if (distance >= bestDistance) continue;
                best = slot.Button;
                bestDistance = distance;
            }
            return best;
        }

        private static Selectable Nearest(List<Selectable> row, float x)
        {
            Selectable best = null;
            float bestDistance = float.MaxValue;
            foreach (var item in row)
            {
                float distance = Mathf.Abs(PositionX(item.transform) - x);
                if (distance >= bestDistance) continue;
                best = item;
                bestDistance = distance;
            }
            return best;
        }

        private static float PositionX(Transform target) =>
            target is RectTransform rect ? rect.anchoredPosition.x : target.localPosition.x;

        private static void ClearNavigation(Selectable selectable)
        {
            if (selectable == null) return;
            selectable.navigation = new Navigation { mode = Navigation.Mode.Explicit };
        }

        private bool IsSlotButton(GameObject target)
        {
            foreach (var slot in slots)
            {
                if (slot != null && IsSame(target, slot.Button)) return true;
            }
            return false;
        }

        // 열기 직전 Refresh는 root가 꺼진 채 돈다 — activeInHierarchy가 아니라 activeSelf를 본다(SettingsPanel 선례).
        // 타일은 버튼이 타일 Root 자신이라 미보유로 숨으면 여기서 빠진다.
        private static bool IsNavigable(Selectable selectable) =>
            selectable != null && selectable.gameObject.activeSelf && selectable.IsInteractable();

        private static Selectable NavigableOrNull(Selectable selectable) => IsNavigable(selectable) ? selectable : null;

        private static bool IsSame(GameObject target, Selectable selectable) =>
            selectable != null && target == selectable.gameObject;

        private bool IsInsidePanel(GameObject target) =>
            navigationBox != null && target.transform.IsChildOf(navigationBox);
    }
}
