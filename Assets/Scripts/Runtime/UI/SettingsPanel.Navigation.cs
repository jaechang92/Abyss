using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Abyss.Runtime.UI
{
    /// <summary>
    /// 설정 패널의 키보드·패드 조작 — 첫 포커스, 닫을 때 뒤 화면 포커스 복구, ESC·패드 B 닫기, 설정 안 탐색 경로.
    ///
    /// 위젯을 코드로 만들어 기본 탐색(Automatic)이 그대로면 방향 입력이 화면 위치만 보고 뒤 메뉴(정지·타이틀·로비)
    /// 버튼으로 빠져나간다. 그래서 실제 설정 항목끼리만 명시적(Explicit)으로 잇고, 끝에서는 길을 비워 둔다.
    ///
    /// 이동·결정·값 조절은 EventSystem(InputSystemUIInputModule)이 선택된 위젯으로 처리한다 — 여기서는 선택만 옮기고
    /// 클릭·값 변경을 직접 호출하지 않는다(SaveStatusOverlay와 같은 분담).
    /// </summary>
    public sealed partial class SettingsPanel
    {
        // 설정을 열기 전 뒤 화면의 선택. 닫을 때 설정이 선택을 쥐고 있을 때만 돌려준다.
        private GameObject previousSelection;
        private Button closeButton;

        // 위에서 아래 순서의 탐색 행. 한 행 안은 좌우로, 행끼리는 위아래로 잇는다. 갱신마다 다시 채운다.
        private readonly List<List<Selectable>> navigationRows = new();

        /// <summary>
        /// 열려 있는 동안 ESC·패드 B를 직접 받고, 포커스를 설정 안에 붙잡아 둔다.
        ///
        /// 씬마다 ESC가 도착하는 경로가 다르다 — Run은 UI 맵 <c>Cancel</c>, 로비는 Player 맵 <c>Pause</c>,
        /// 타이틀은 <b>수신자가 아예 없다</b>. 패널이 자기 닫기를 직접 소유하면 타이틀처럼 입력 배선이
        /// 없는 씬에서도 ESC·B가 통한다(씬마다 입력을 배선하는 것보다 싸다).
        ///
        /// 씬의 ESC 처리기가 먼저 닫는 경우도 있으므로(입력 처리는 Update보다 먼저 돈다) 여기서는 아직
        /// 열려 있을 때만 동작하고, 반대 순서는 <see cref="WasClosedThisFrame"/>가 막는다.
        /// 저장 모달이 입력을 쥔 동안에는 닫기도 포커스도 건드리지 않는다 — 그 키와 선택은 모달 몫이다.
        /// timeScale=0에서도 Update는 돌기 때문에 일시정지 중에도 유효하다.
        /// </summary>
        private void Update()
        {
            if (body == null || !body.activeSelf) return;
            if (SaveStatusOverlay.IsCapturingInput) return;

            if (WasCancelPressedThisFrame())
            {
                Close();
                return;
            }
            KeepFocusInside();
        }

        private static bool WasCancelPressedThisFrame()
        {
            var keyboard = Keyboard.current;
            var gamepad = Gamepad.current;
            return (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) ||
                   (gamepad != null && gamepad.buttonEast.wasPressedThisFrame);
        }

        /// <summary>새로 열릴 때 1회. 뒤 화면 선택을 기억하고 첫 항목(마스터 음량)에 포커스를 준다.</summary>
        private void BeginFocus()
        {
            previousSelection = null;
            // 직전 동기화는 body가 꺼진 채 돌았다 — 켜진 뒤 경로를 한 번 더 맞춘다.
            RefreshNavigation();

            var eventSystem = EventSystem.current;
            if (eventSystem == null) return;
            // 저장 모달이 떠 있으면 모달의 포커스를 유지한다. 모달이 닫힌 뒤 Update가 설정 안으로 들인다.
            if (SaveStatusOverlay.IsCapturingInput) return;

            var selected = eventSystem.currentSelectedGameObject;
            if (selected != null && !IsInsideBody(selected)) previousSelection = selected;
            SelectFirst(eventSystem);
        }

        /// <summary>
        /// 닫을 때. 설정이 선택을 쥐고 있을 때만(설정 안이거나 비어 있음) 이전 선택을 돌려준다.
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

            eventSystem.SetSelectedGameObject(CanRestore(restore) ? restore : null);
        }

        private static bool CanRestore(GameObject target)
        {
            // 파괴된 오브젝트는 Unity 비교에서 null이다.
            if (target == null || !target.activeInHierarchy) return false;
            return target.TryGetComponent<Selectable>(out var selectable) && selectable.IsInteractable();
        }

        /// <summary>
        /// 포커스가 비었거나 뒤 화면으로 새면 설정 안으로 되돌린다. 마우스로 빈 곳을 누르면 선택이 풀려
        /// 그 뒤 키보드·패드 입력이 갈 곳이 없어진다(SaveStatusOverlay와 같은 이유).
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
            // 연 순간 저장 모달이 떠 있었다면 모달이 닫히며 돌려준 뒤 화면 선택이 여기 온다 — 그것을 복구 대상으로 삼는다.
            if (previousSelection == null && selected != null) previousSelection = selected;
            SelectFirst(eventSystem);
        }

        /// <summary>
        /// 선택된 항목이 못 누르는 상태가 됐으면(해상도·언어 끝에 닿아 버튼이 꺼짐) 같은 행의 반대 버튼으로,
        /// 그것도 없으면 첫 항목으로 옮긴다. 꺼진 버튼을 쥔 채면 결정이 먹지 않는다.
        /// </summary>
        private void RepairSelection(EventSystem eventSystem, GameObject selected)
        {
            if (selected.TryGetComponent<Selectable>(out var current) && IsNavigable(current)) return;

            var replacement = SiblingOf(selected);
            if (replacement == null) replacement = FirstNavigable();
            eventSystem.SetSelectedGameObject(replacement != null ? replacement.gameObject : null);
        }

        private Selectable SiblingOf(GameObject selected)
        {
            if (IsSame(selected, resolutionPrev)) return NavigableOrNull(resolutionNext);
            if (IsSame(selected, resolutionNext)) return NavigableOrNull(resolutionPrev);
            if (IsSame(selected, languagePrev)) return NavigableOrNull(languageNext);
            if (IsSame(selected, languageNext)) return NavigableOrNull(languagePrev);
            return null;
        }

        private void SelectFirst(EventSystem eventSystem)
        {
            var first = FirstNavigable();
            eventSystem.SetSelectedGameObject(first != null ? first.gameObject : null);
        }

        /// <summary>마스터 음량이 첫 행이다 — 꺼져 있으면 그다음 유효한 항목.</summary>
        private Selectable FirstNavigable()
        {
            BuildNavigationRows();
            return navigationRows.Count > 0 ? navigationRows[0][0] : null;
        }

        // ───────────────────────── 탐색 경로 ─────────────────────────

        /// <summary>
        /// 설정 항목끼리만 잇는 명시적 경로를 다시 만든다. 해상도·언어 끝 버튼의 활성이 바뀔 때마다 부른다.
        /// 꺼진 버튼은 행에서 빠지므로 위아래·좌우 이동이 그 자리를 건너뛴다.
        /// </summary>
        private void RefreshNavigation()
        {
            BuildNavigationRows();
            ApplyNavigation();

            if (body == null || !body.activeSelf || SaveStatusOverlay.IsCapturingInput) return;
            var eventSystem = EventSystem.current;
            if (eventSystem == null) return;
            var selected = eventSystem.currentSelectedGameObject;
            if (selected != null && IsInsideBody(selected)) RepairSelection(eventSystem, selected);
        }

        private void BuildNavigationRows()
        {
            navigationRows.Clear();
            AddRow(masterSlider);
            AddRow(bgmSlider);
            AddRow(sfxSlider);
            AddRow(fullscreenToggle);
            AddRow(resolutionPrev, resolutionNext);
            AddRow(languagePrev, languageNext);
            AddRow(closeButton);
        }

        private void AddRow(params Selectable[] items)
        {
            var row = new List<Selectable>(items.Length);
            foreach (var item in items)
            {
                if (IsNavigable(item)) row.Add(item);
            }
            if (row.Count > 0) navigationRows.Add(row);
        }

        private void ApplyNavigation()
        {
            for (int r = 0; r < navigationRows.Count; r++)
            {
                var row = navigationRows[r];
                for (int c = 0; c < row.Count; c++)
                {
                    var item = row[c];
                    var navigation = new Navigation
                    {
                        mode = Navigation.Mode.Explicit,
                        // 맨 위·맨 아래는 비워 둔다 — 설정 밖(뒤 메뉴)으로 나가지 않는다.
                        selectOnUp = PickInRow(r - 1, c),
                        selectOnDown = PickInRow(r + 1, c),
                    };
                    // 슬라이더는 좌우를 비워 둔다 — 비어 있어야 Slider.OnMove가 좌우 입력을 값 조절로 쓴다.
                    if (item is not Slider)
                    {
                        navigation.selectOnLeft = c > 0 ? row[c - 1] : null;
                        navigation.selectOnRight = c < row.Count - 1 ? row[c + 1] : null;
                    }
                    item.navigation = navigation;
                }
            }
        }

        /// <summary>인접 행에서 같은 열(없으면 그 행의 마지막 항목)을 고른다.</summary>
        private Selectable PickInRow(int rowIndex, int column)
        {
            if (rowIndex < 0 || rowIndex >= navigationRows.Count) return null;
            var row = navigationRows[rowIndex];
            return row[Mathf.Min(column, row.Count - 1)];
        }

        // body가 꺼진 동기화 시점에도 판정해야 하므로 activeInHierarchy(IsActive)가 아니라 activeSelf를 본다.
        private static bool IsNavigable(Selectable selectable) =>
            selectable != null && selectable.gameObject.activeSelf && selectable.IsInteractable();

        private static Selectable NavigableOrNull(Selectable selectable) => IsNavigable(selectable) ? selectable : null;

        private static bool IsSame(GameObject target, Selectable selectable) =>
            selectable != null && target == selectable.gameObject;

        private bool IsInsideBody(GameObject target) => body != null && target.transform.IsChildOf(body.transform);
    }
}
