using System.Collections.Generic;
using Abyss.Runtime.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Abyss.Runtime.Lobby
{
    /// <summary>
    /// <see cref="MetaUpgradePanel"/>의 키보드·패드 조작 — 첫 포커스, 제단 안 탐색 경로, ESC·패드 B 닫기,
    /// 닫을 때 이전 선택 복구. <see cref="RelicShopPanel"/>.Navigation과 같은 규약이다.
    ///
    /// 행 버튼을 코드로 만들어 기본 탐색(Automatic)이 그대로면 방향 입력이 화면 위치만 보고 못 사는 버튼이나
    /// 제단 밖(뒤 로비 메뉴)으로 샌다. 그래서 실제로 누를 수 있는 [강화] 버튼만 행 순서대로, 마지막에 [닫기]를
    /// 위아래 명시적(Explicit)으로 잇는다. 최대 레벨·잔액 부족 행은 빠지므로 살 게 없어도 [닫기]까지 닿는다.
    ///
    /// 구매·닫기는 선택된 버튼의 Submit(EventSystem)으로만 일어난다. 여기서는 선택만 옮긴다.
    /// NPC 상호작용과 같은 입력이 연 프레임에 Submit으로도 흘러 들어오지 않게 연 프레임의 클릭·취소는 무시한다.
    /// </summary>
    public sealed partial class MetaUpgradePanel
    {
        // 제단을 열기 전 뒤 화면의 선택. 닫을 때 제단이 선택을 쥐고 있을 때만 돌려준다.
        private GameObject previousSelection;

        // 이번 열림에서 제단 안으로 포커스를 들였는지. 들인 뒤에 밖으로 샌 선택은 복구 대상으로 삼지 않는다.
        private bool hasEnteredFocus;

        // 연 프레임. 여는 입력과 같은 프레임의 Submit·취소로 구매·닫기가 일어나지 않게 한다.
        private int openedFrame = -1;

        // 선택을 옮기는 중인지. 선택 처리 안에서 다시 선택을 옮기는 재귀를 끊는다.
        private bool isApplyingSelection;

        // 바깥 비활성으로 취소됐지만 그 자리에서 root를 끄지 못했다. 다시 켜진 뒤 첫 Update에서 끈다.
        private bool isRootHidePending;

        // 위에서 아래 순서의 탐색 대상. 갱신마다 다시 채운다.
        private readonly List<Selectable> navigationItems = new();

        private bool IsOpenedThisFrame => Time.frameCount == openedFrame;

        /// <summary>저장 모달·설정·도감이 입력과 선택을 쥐고 있는지(닫은 그 프레임 포함). 이때 제단은 닫기·포커스를 건드리지 않는다.</summary>
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
                // 바깥 비활성으로 취소된 뒤 다시 켜졌다 — 남은 제단 화면을 닫힌 상태에 맞춘다.
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
        /// 바깥에서 꺼질 때(씬 전환·Canvas 비활성·컴포넌트 끔). 열린 채였다면 열림 표시를 정리하고 콜백을 한 번 부른다 —
        /// <see cref="AltarNpc"/>의 콜백은 busy·이동 잠금 해제만 하므로 여기서 불러도 안전하고,
        /// 부르지 않으면 제단이 다시 열리지 않고 플레이어가 묶인 채 남는다.
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
        /// 바깥에서 꺼졌는데 root가 켜진 채면(로비 Canvas 비활성·컴포넌트 끔) 다시 켜졌을 때 IsOpen=false인 제단 화면이
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

        /// <summary>[닫기] 버튼. 연 프레임의 Submit·상위 모달이 쥔 동안의 클릭은 무시한다 — 외부 정리용 <see cref="Close"/>는 막지 않는다.</summary>
        private void OnCloseClicked()
        {
            if (!IsOpen || IsOpenedThisFrame || IsUpperModalOwningInput) return;
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

            // 상위 모달이 떠 있으면 그쪽 포커스를 유지한다. 모달이 닫힌 뒤 Update가 제단 안으로 들인다.
            if (IsUpperModalOwningInput) return;
            KeepFocusInside();
        }

        /// <summary>
        /// 닫을 때. 제단이 선택을 쥐고 있을 때만(제단 안이거나 비어 있음) 이전 선택을 돌려준다.
        /// 다른 화면이 쥔 선택은 그대로 두고, 상위 모달이 떠 있으면 제단이 쥔 선택만 비운다.
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

        /// <summary>바깥에서 꺼지거나 파괴될 때. 제단이 쥔 선택만 비운다.</summary>
        private void ClearOwnedSelection()
        {
            var eventSystem = EventSystem.current;
            if (eventSystem == null) return;
            var selected = eventSystem.currentSelectedGameObject;
            if (selected != null && IsInsidePanel(selected)) SelectSafely(eventSystem, null);
        }

        /// <summary>
        /// 포커스가 비었거나 제단 밖(뒤 메뉴 등)으로 새면 제단 안으로 되돌린다. 처음 들이기 전에 본 바깥 선택만
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
        /// 선택된 버튼이 못 고르는 상태가 됐으면 옮긴다 — 구매로 최대 레벨·잔액 부족이 된 [강화]는
        /// 아래쪽의 다음 살 수 있는 행으로, 없으면 [닫기]로. 살아 있는 선택은 그대로 둔다.
        /// </summary>
        private void RepairSelection(EventSystem eventSystem, GameObject selected)
        {
            if (selected.TryGetComponent<Selectable>(out var current) && IsNavigable(current)) return;

            Selectable replacement = null;
            int rowIndex = RowIndexOf(selected);
            if (rowIndex >= 0) replacement = NextNavigablePurchase(rowIndex + 1);
            if (replacement == null) replacement = NavigableOrNull(closeButton);
            if (replacement == null) replacement = EntryButton();

            SelectSafely(eventSystem, replacement != null ? replacement.gameObject : null);
        }

        /// <summary>첫 포커스: 처음으로 살 수 있는 [강화], 없으면 [닫기].</summary>
        private Selectable EntryButton()
        {
            var purchase = NextNavigablePurchase(0);
            return purchase != null ? purchase : NavigableOrNull(closeButton);
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
        /// 경로를 다시 잇고, 선택된 버튼이 못 고르게 됐으면 보정한다. 열기·구매가 모두 지나는
        /// <see cref="Refresh"/> 끝에서 부른다 — 최대 레벨 도달·조각 소진이 여기서 반영된다.
        /// </summary>
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

        private void BuildNavigationItems()
        {
            navigationItems.Clear();
            foreach (var row in rows)
            {
                if (row != null && IsNavigable(row.Button)) navigationItems.Add(row.Button);
            }
            if (IsNavigable(closeButton)) navigationItems.Add(closeButton);
        }

        private void ApplyNavigation()
        {
            // 경로에서 빠진 버튼(최대 레벨·잔액 부족)도 자기 경로를 비운다 — Automatic이 남으면 엉뚱한 곳으로 샌다.
            foreach (var row in rows) ClearNavigation(row?.Button);
            ClearNavigation(closeButton);

            for (int i = 0; i < navigationItems.Count; i++)
            {
                // 맨 위·맨 아래와 좌우는 비워 둔다 — 제단 밖(뒤 화면)으로 나가지 않는다.
                navigationItems[i].navigation = new Navigation
                {
                    mode = Navigation.Mode.Explicit,
                    selectOnUp = i > 0 ? navigationItems[i - 1] : null,
                    selectOnDown = i < navigationItems.Count - 1 ? navigationItems[i + 1] : null,
                };
            }
        }

        /// <summary>startIndex 행부터 아래로 처음 살 수 있는 [강화] 버튼.</summary>
        private Selectable NextNavigablePurchase(int startIndex)
        {
            for (int i = Mathf.Max(0, startIndex); i < rows.Count; i++)
            {
                var row = rows[i];
                if (row != null && IsNavigable(row.Button)) return row.Button;
            }
            return null;
        }

        private int RowIndexOf(GameObject target)
        {
            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                if (row != null && IsSame(target, row.Button)) return i;
            }
            return -1;
        }

        private static void ClearNavigation(Selectable selectable)
        {
            if (selectable == null) return;
            selectable.navigation = new Navigation { mode = Navigation.Mode.Explicit };
        }

        // 열기 직전 Refresh는 root가 꺼진 채 돈다 — activeInHierarchy가 아니라 activeSelf를 본다(SettingsPanel 선례).
        private static bool IsNavigable(Selectable selectable) =>
            selectable != null && selectable.gameObject.activeSelf && selectable.IsInteractable();

        private static Selectable NavigableOrNull(Selectable selectable) => IsNavigable(selectable) ? selectable : null;

        private static bool IsSame(GameObject target, Selectable selectable) =>
            selectable != null && target == selectable.gameObject;

        /// <summary>
        /// 선택이 제단 안인지. 이 컴포넌트는 로비 공용 Canvas에 붙어 있어(LobbySceneBuilder.Altar) gameObject 기준으로는
        /// 뒤 메뉴까지 안으로 보인다 — root(AltarRoot) 아래만 본다. root 미배선이면 행 컨테이너와 [닫기]만 본다.
        /// </summary>
        private bool IsInsidePanel(GameObject target)
        {
            if (root != null) return target.transform.IsChildOf(root.transform);
            if (rowContainer != null && target.transform.IsChildOf(rowContainer)) return true;
            return IsSame(target, closeButton);
        }
    }
}
