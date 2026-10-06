using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Abyss.Runtime.UI
{
    /// <summary>
    /// <see cref="CodexPanel"/>의 키보드·패드 조작 — 첫 포커스, 닫을 때 뒤 화면 포커스 복구, ESC·패드 B 닫기, 도감 안 탐색 경로.
    ///
    /// 버튼을 코드로 만들어 기본 탐색(Automatic)이 그대로면 방향 입력이 화면 위치만 보고 뒤 메뉴(타이틀·로비) 버튼으로
    /// 빠져나간다. 그래서 실제로 보이고 누를 수 있는 버튼끼리만 명시적(Explicit)으로 잇는다(SettingsPanel 선례).
    ///
    ///   닫기 ↕ 탭 줄(좌우) ↕ 타일 5열(상하좌우, 빈 칸 건너뜀) ↕ 페이지 ◀ ▶
    ///
    /// 기록 탭·항목 0개·한 페이지일 때는 숨은 그리드·페이지 버튼이 경로에서 빠져 탭과 닫기만 오간다.
    /// 이동·결정은 EventSystem(InputSystemUIInputModule)이 처리한다 — 여기서는 선택만 옮기고 클릭을 직접 부르지 않는다.
    /// </summary>
    public sealed partial class CodexPanel
    {
        // 도감을 열기 전 뒤 화면의 선택. 닫을 때 도감이 선택을 쥐고 있을 때만 돌려준다.
        private GameObject previousSelection;
        private Button closeButton;

        // 연 프레임. 여는 입력과 같은 프레임의 취소 입력으로 열자마자 닫히지 않게 한다.
        private int openedFrame = -1;

        // 타일 선택 이벤트(OnSelect) 안에서 갱신 중인지. EventSystem은 선택 처리 중 재선택을 거부하므로
        // 이때는 선택을 옮기지 않는다(SelectItem → Refresh → 선택 보정 → OnSelect 재귀도 여기서 끊긴다).
        private bool isHandlingTileSelect;

        /// <summary>저장 모달·설정이 입력과 선택을 쥐고 있는지. 이때 도감은 닫기·포커스를 건드리지 않는다.</summary>
        private static bool IsUpperModalOwningFocus => SaveStatusOverlay.IsCapturingInput || SettingsPanel.IsOpen;

        private void OnDisable() => ReleaseFocus();

        private void OnDestroy() => ReleaseFocus();

        private static bool WasCancelPressedThisFrame()
        {
            var keyboard = Keyboard.current;
            var gamepad = Gamepad.current;
            return (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) ||
                   (gamepad != null && gamepad.buttonEast.wasPressedThisFrame);
        }

        /// <summary>새로 열릴 때 1회. 뒤 화면 선택을 기억하고 현재 탭 버튼에 포커스를 준다.</summary>
        private void BeginFocus()
        {
            openedFrame = Time.frameCount;
            previousSelection = null;
            RefreshNavigation();

            var eventSystem = EventSystem.current;
            if (eventSystem == null) return;
            // 상위 모달이 떠 있으면 그 포커스를 유지한다. 모달이 닫힌 뒤 Update가 도감 안으로 들인다.
            if (IsUpperModalOwningFocus) return;

            var selected = eventSystem.currentSelectedGameObject;
            if (selected != null && !IsInsideBody(selected)) previousSelection = selected;
            SelectEntry(eventSystem);
        }

        /// <summary>
        /// 닫히거나 파괴될 때. 도감이 선택을 쥐고 있을 때만(도감 안이거나 비어 있음) 이전 선택을 돌려준다.
        /// 다른 상위 모달이 쥔 선택은 그대로 둔다. 이전 선택이 씬과 함께 사라졌거나 못 누르는 상태면 비운다.
        /// </summary>
        private void ReleaseFocus()
        {
            var restore = previousSelection;
            previousSelection = null;

            var eventSystem = EventSystem.current;
            if (eventSystem == null || SaveStatusOverlay.IsCapturingInput) return;

            var selected = eventSystem.currentSelectedGameObject;
            if (selected != null && !IsInsideBody(selected)) return;
            // 닫힌 도감이 쥔 것이 없으면(애초에 선택이 비어 있음) 남의 선택을 만들지 않는다.
            if (selected == null && restore == null) return;

            eventSystem.SetSelectedGameObject(CanRestore(restore) ? restore : null);
        }

        private static bool CanRestore(GameObject target)
        {
            // 파괴된 오브젝트는 Unity 비교에서 null이다.
            if (target == null || !target.activeInHierarchy) return false;
            return target.TryGetComponent<Selectable>(out var selectable) && selectable.IsInteractable();
        }

        /// <summary>
        /// 포커스가 비었거나 뒤 화면으로 새면 도감 안으로 되돌린다. 마우스로 빈 곳을 누르면 선택이 풀려
        /// 그 뒤 패드 입력이 갈 곳이 없어지고, 뒤 메뉴에 남은 선택은 닫힌 줄 알았던 버튼을 결정해 버린다.
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
            // 연 순간 상위 모달이 떠 있었다면 모달이 닫히며 돌려준 뒤 화면 선택이 여기 온다 — 그것을 복구 대상으로 삼는다.
            if (previousSelection == null && selected != null) previousSelection = selected;
            SelectEntry(eventSystem);
        }

        /// <summary>
        /// 선택된 버튼이 숨었거나 못 누르게 됐으면 대신 고를 버튼으로 옮긴다.
        /// 살아 있는 선택(탭·페이지 버튼을 눌러 갱신한 경우 등)은 그대로 둔다.
        /// </summary>
        private void RepairSelection(EventSystem eventSystem, GameObject selected)
        {
            if (selected.TryGetComponent<Selectable>(out var current) && IsNavigableInPanel(current)) return;

            var replacement = ReplacementFor(selected);
            eventSystem.SetSelectedGameObject(replacement != null ? replacement.gameObject : null);
        }

        private Selectable ReplacementFor(GameObject selected)
        {
            // 페이지 끝에 닿아 꺼진 쪽 버튼 → 반대쪽 버튼, 그것도 없으면 상세가 가리키는 타일.
            if (IsSame(selected, prevButton)) return PagerOrNull(nextButton) ?? FocusedTileOrFirst() ?? EntrySelectable();
            if (IsSame(selected, nextButton)) return PagerOrNull(prevButton) ?? FocusedTileOrFirst() ?? EntrySelectable();
            if (SlotOf(selected) >= 0) return FocusedTileOrFirst() ?? EntrySelectable();
            return EntrySelectable();
        }

        private void SelectEntry(EventSystem eventSystem)
        {
            var entry = EntrySelectable();
            eventSystem.SetSelectedGameObject(entry != null ? entry.gameObject : null);
        }

        /// <summary>현재 탭 버튼, 못 누르면 첫 유효 탭, 그것도 없으면 닫기.</summary>
        private Selectable EntrySelectable()
        {
            var tab = EntryTab();
            if (tab != null) return tab;
            return IsNavigable(closeButton) ? closeButton : null;
        }

        private Selectable EntryTab()
        {
            int current = (int)currentTab;
            if (current >= 0 && current < tabButtons.Length && IsNavigable(tabButtons[current])) return tabButtons[current];
            foreach (var tab in tabButtons)
            {
                if (IsNavigable(tab)) return tab;
            }
            return null;
        }

        /// <summary>상세가 가리키는 타일(이 페이지에 있으면), 아니면 첫 타일.</summary>
        private Selectable FocusedTileOrFirst()
        {
            int count = NavigableTileCount();
            if (count == 0) return null;
            int slot = selectedIndex - currentPage * PAGE_SIZE;
            return tiles[slot >= 0 && slot < count ? slot : 0].Button;
        }

        /// <summary>타일 버튼이 선택됐다(<see cref="CodexTileFocusRelay"/>). 상세를 그 항목으로 바로 바꾼다.</summary>
        internal void OnTileSelected(int slot)
        {
            // 저장 모달·설정이 포커스를 쥔 동안의 선택 이벤트로 상세를 바꾸지 않는다.
            if (isHandlingTileSelect || body == null || !body.activeSelf || IsUpperModalOwningFocus) return;
            int index = currentPage * PAGE_SIZE + slot;
            if (index == selectedIndex) return;

            isHandlingTileSelect = true;
            try
            {
                SelectItem(index);
            }
            finally
            {
                isHandlingTileSelect = false;
            }
        }

        // ───────────────────────── 탐색 경로 ─────────────────────────

        /// <summary>
        /// 경로를 다시 잇고, 도감 안의 선택이 숨었거나 못 누르게 됐으면 보정한다.
        /// 탭·페이지·항목이 바뀌는 모든 경로가 지나는 <see cref="Refresh"/> 끝에서 부른다.
        /// </summary>
        private void RefreshNavigation()
        {
            ApplyNavigation();

            if (isHandlingTileSelect || body == null || !body.activeSelf || IsUpperModalOwningFocus) return;
            var eventSystem = EventSystem.current;
            if (eventSystem == null) return;
            var selected = eventSystem.currentSelectedGameObject;
            if (selected != null && IsInsideBody(selected)) RepairSelection(eventSystem, selected);
        }

        private void ApplyNavigation()
        {
            int tileCount = NavigableTileCount();
            var prev = PagerOrNull(prevButton);
            var next = PagerOrNull(nextButton);
            var entryTab = EntryTab();

            SetExplicit(closeButton, null, entryTab, null, null);

            for (int i = 0; i < tabButtons.Length; i++)
            {
                var tab = tabButtons[i];
                if (!IsNavigable(tab))
                {
                    SetExplicit(tab, null, null, null, null);
                    continue;
                }
                // 탭 6개 · 열 5개 — 마지막 탭은 맨 오른쪽 열로 내려간다.
                var down = TileInRow(0, Mathf.Min(i, GRID_COLS - 1), tileCount) ?? NearestPager(i, prev, next);
                SetExplicit(tab, NavigableOrNull(closeButton), down, NavigableTabBefore(i), NavigableTabAfter(i));
            }

            for (int slot = 0; slot < tiles.Length; slot++)
            {
                var button = tiles[slot].Button;
                if (slot >= tileCount)
                {
                    // 숨은 타일 자신의 경로도 비운다 — Automatic이 남으면 뒤 메뉴로 샌다.
                    SetExplicit(button, null, null, null, null);
                    continue;
                }

                int row = slot / GRID_COLS;
                int col = slot % GRID_COLS;
                var up = row > 0 ? tiles[slot - GRID_COLS].Button : TabAbove(col) ?? entryTab;
                Selectable down;
                if (slot + GRID_COLS < tileCount) down = tiles[slot + GRID_COLS].Button;
                // 아래 행이 덜 찼으면(빈 마지막 행) 그 행의 마지막 타일로 내려간다.
                else if ((row + 1) * GRID_COLS < tileCount) down = tiles[tileCount - 1].Button;
                else down = NearestPager(col, prev, next);

                var left = col > 0 ? tiles[slot - 1].Button : null;
                var right = col < GRID_COLS - 1 && slot + 1 < tileCount ? tiles[slot + 1].Button : null;
                SetExplicit(button, up, down, left, right);
            }

            int lastRow = tileCount > 0 ? (tileCount - 1) / GRID_COLS : 0;
            SetExplicit(prevButton, prev != null ? TileInRow(lastRow, 1, tileCount) ?? entryTab : null, null, null, prev != null ? next : null);
            SetExplicit(nextButton, next != null ? TileInRow(lastRow, GRID_COLS - 2, tileCount) ?? entryTab : null, null, next != null ? prev : null, null);
        }

        /// <summary>보이고 누를 수 있는 타일 수. 타일은 0번부터 연속으로 켜진다(<see cref="RefreshGrid"/>).</summary>
        private int NavigableTileCount()
        {
            if (gridRoot == null || !gridRoot.activeSelf) return 0;
            int count = 0;
            while (count < tiles.Length && tiles[count].Root != null && tiles[count].Root.activeSelf && IsNavigable(tiles[count].Button))
            {
                count++;
            }
            return count;
        }

        /// <summary>해당 행에서 같은 열(없으면 그 행의 마지막 타일)을 고른다.</summary>
        private Selectable TileInRow(int row, int col, int tileCount)
        {
            int rowStart = row * GRID_COLS;
            if (tileCount <= rowStart) return null;
            return tiles[Mathf.Min(rowStart + col, tileCount - 1)].Button;
        }

        private Selectable TabAbove(int col) => col < tabButtons.Length ? NavigableOrNull(tabButtons[col]) : null;

        private Selectable NavigableTabBefore(int index)
        {
            for (int i = index - 1; i >= 0; i--)
            {
                if (IsNavigable(tabButtons[i])) return tabButtons[i];
            }
            return null;
        }

        private Selectable NavigableTabAfter(int index)
        {
            for (int i = index + 1; i < tabButtons.Length; i++)
            {
                if (IsNavigable(tabButtons[i])) return tabButtons[i];
            }
            return null;
        }

        /// <summary>왼쪽 열은 ◀, 나머지는 ▶ 쪽 — 그쪽이 끝에 닿아 꺼졌으면 반대쪽.</summary>
        private static Selectable NearestPager(int col, Selectable prev, Selectable next) =>
            col < GRID_COLS / 2 ? prev ?? next : next ?? prev;

        private Selectable PagerOrNull(Button button) =>
            pagerRoot != null && pagerRoot.activeSelf && IsNavigable(button) ? button : null;

        /// <summary>도감이 탐색 대상으로 쓰는 버튼인지 — 실제로 보이고 누를 수 있어야 한다.</summary>
        private bool IsNavigableInPanel(Selectable selectable)
        {
            var target = selectable.gameObject;
            if (IsSame(target, closeButton)) return IsNavigable(closeButton);
            foreach (var tab in tabButtons)
            {
                if (IsSame(target, tab)) return IsNavigable(tab);
            }
            if (IsSame(target, prevButton) || IsSame(target, nextButton)) return PagerOrNull((Button)selectable) != null;

            int slot = SlotOf(target);
            return slot >= 0 && slot < NavigableTileCount();
        }

        private int SlotOf(GameObject target)
        {
            for (int slot = 0; slot < tiles.Length; slot++)
            {
                if (IsSame(target, tiles[slot].Button)) return slot;
            }
            return -1;
        }

        private static void SetExplicit(Selectable selectable, Selectable up, Selectable down, Selectable left, Selectable right)
        {
            if (selectable == null) return;
            selectable.navigation = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnUp = up,
                selectOnDown = down,
                selectOnLeft = left,
                selectOnRight = right,
            };
        }

        // body가 꺼진 갱신 시점에도 판정해야 하므로 activeInHierarchy가 아니라 activeSelf를 본다(SettingsPanel 선례).
        // 부모 묶음(그리드·페이지)의 활성은 호출하는 쪽이 따로 본다.
        private static bool IsNavigable(Selectable selectable) =>
            selectable != null && selectable.gameObject.activeSelf && selectable.IsInteractable();

        private static Selectable NavigableOrNull(Selectable selectable) => IsNavigable(selectable) ? selectable : null;

        private static bool IsSame(GameObject target, Selectable selectable) =>
            selectable != null && target == selectable.gameObject;

        private bool IsInsideBody(GameObject target) => body != null && target.transform.IsChildOf(body.transform);
    }
}
