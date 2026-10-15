using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Abyss.Runtime.UI
{
    /// <summary>
    /// <see cref="EventRoomReadability"/>의 입력 쪽 — 선택 추적·최소 노출·수동 스크롤(휠·Page·Home·End·오른쪽 스틱).
    ///
    /// 자동 노출은 세 경우뿐이다. 매 프레임 끌어당기지 않아 수동 스크롤 위치가 지켜진다.
    /// <list type="bullet">
    /// <item>새 이벤트·결과를 연 뒤 첫 선택 — 첫 선택지가 이미 보이면 맨 위를 유지, 안 보이면 최소한만 내린다(첫 Submit이 안 보이는 선택지를 누르지 않게).</item>
    /// <item>키보드·패드로 선택이 바뀔 때 — 포인터 누름(그 프레임과 다음 프레임)·빈 선택 뒤 복구는 제외한다(Navigation의 첫 선택지 복구가 위치를 잡아당기지 않게).</item>
    /// <item>재배치(배율·화면 크기·잔액 라벨) 직전에 <b>보이던</b> 같은 선택 버튼이 밀려났을 때 — 한 번만. 사용자가 굴려 내보낸 선택은 끌어오지 않는다.</item>
    /// </list>
    /// 바닥의 [계속]은 스크롤 내용이 아니므로 노출 대상이 아니다. 상위 모달(저장·설정·도감)이 입력을 쥔 동안은 아무것도 하지 않는다.
    /// </summary>
    public sealed partial class EventRoomReadability
    {
        private const float REVEAL_MARGIN = 6f;
        private const float VISIBLE_TOLERANCE = 1f;
        private const float WHEEL_STEP = 72f;
        private const float PAGE_RATIO = 0.8f;
        private const float STICK_SPEED = 420f;
        private const float STICK_DEADZONE = 0.2f;

        private bool isInitialRevealPending;
        private bool hasSeenNullSelection;
        private GameObject lastSelection;
        private GameObject reflowRevealTarget;
        private int pointerFrame = -10;

        /// <summary>저장 모달·설정·도감이 입력을 쥔 동안(닫은 그 프레임 포함)은 스크롤·자동 노출을 하지 않는다. EventRoomPanel.Navigation과 같은 조건이다.</summary>
        private static bool IsUpperModalOwningInput =>
            SaveStatusOverlay.IsCapturingInput ||
            SettingsPanel.IsOpen || SettingsPanel.WasClosedThisFrame ||
            CodexPanel.IsOpen || CodexPanel.WasClosedThisFrame;

        /// <summary>선택된 것이 스크롤 내용(본문·선택지) 안이면 최소한만 움직여 보이게 한다. 바닥의 [계속]은 대상이 아니다. 클릭은 부르지 않는다.</summary>
        public void EnsureSelectedVisible(GameObject selected)
        {
            var rect = ContentItemOf(selected);
            if (rect == null) return;

            float height = rect.sizeDelta.y;
            float itemTop = -rect.anchoredPosition.y - height * 0.5f;
            float itemBottom = itemTop + height;
            float viewportHeight = viewport.sizeDelta.y;
            float y = content.anchoredPosition.y;

            if (height > viewportHeight) y = itemTop;   // 보기보다 큰 버튼은 윗부분부터
            else if (itemTop < y) y = itemTop - REVEAL_MARGIN;
            else if (itemBottom > y + viewportHeight) y = itemBottom - viewportHeight + REVEAL_MARGIN;
            SetScroll(y);
        }

        /// <summary>새 이벤트·결과. 첫 선택을 다시 확인하고, 이전 열림의 추적·예약을 비운다.</summary>
        private void ResetSelectionTracking()
        {
            isInitialRevealPending = true;
            hasSeenNullSelection = false;
            lastSelection = null;
            reflowRevealTarget = null;
        }

        /// <summary>재배치 직전에 호출된다. 현재 선택이 스크롤 내용의 활성 버튼이고 보기 영역 안에 보이면 그것을, 아니면 null.</summary>
        private GameObject CaptureVisibleSelection()
        {
            var eventSystem = EventSystem.current;
            var selected = eventSystem != null ? eventSystem.currentSelectedGameObject : null;
            var rect = ContentItemOf(selected);
            if (rect == null || !rect.gameObject.activeSelf) return null;

            float height = rect.sizeDelta.y;
            float itemTop = -rect.anchoredPosition.y - height * 0.5f;
            float itemBottom = itemTop + height;
            float viewportHeight = viewport.sizeDelta.y;
            float y = content.anchoredPosition.y;
            bool isVisible = height > viewportHeight
                ? itemTop < y + viewportHeight && itemBottom > y
                : itemTop >= y - VISIBLE_TOLERANCE && itemBottom <= y + viewportHeight + VISIBLE_TOLERANCE;
            return isVisible ? selected : null;
        }

        /// <summary>재배치 뒤 한 번 노출을 예약한다. 처리는 LateUpdate의 상위 모달 관문 뒤에서 한다.</summary>
        private void ScheduleReflowReveal(GameObject selected) => reflowRevealTarget = selected;

        /// <summary>스크롤 내용의 직접 자식(선택 버튼)을 찾는다. 내용 밖이면 null.</summary>
        private RectTransform ContentItemOf(GameObject target)
        {
            if (target == null || content == null || !target.transform.IsChildOf(content)) return null;
            var item = target.transform;
            while (item.parent != null && item.parent != content) item = item.parent;
            return item as RectTransform;
        }

        // ───────────────────────── 프레임 처리 ─────────────────────────

        private void LateUpdate()
        {
            if (readingScroll == null) return;
            RefreshIfNeeded();
            if (IsUpperModalOwningInput) return;

            TrackPointer();
            ProcessReflowReveal();
            TrackSelection();
            HandleManualScroll();
        }

        private void TrackPointer()
        {
            var mouse = Mouse.current;
            bool isPressed = mouse != null &&
                (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame ||
                 mouse.middleButton.wasPressedThisFrame);
            var touch = Touchscreen.current;
            if (touch != null && touch.primaryTouch.press.wasPressedThisFrame) isPressed = true;
            if (isPressed) pointerFrame = Time.frameCount;
        }

        /// <summary>재배치 전에 보이던 같은 선택이 아직 선택돼 있으면 한 번만 최소 노출한다. 선택이 바뀌었으면 버린다(그쪽은 TrackSelection이 판단).</summary>
        private void ProcessReflowReveal()
        {
            if (reflowRevealTarget == null) return;
            var target = reflowRevealTarget;
            reflowRevealTarget = null;
            var eventSystem = EventSystem.current;
            if (eventSystem != null && eventSystem.currentSelectedGameObject == target) EnsureSelectedVisible(target);
        }

        /// <summary>
        /// 선택이 바뀐 프레임에만 노출한다. 포인터 누름(그 프레임과 다음 프레임)·빈 선택(또는 패널 밖) 뒤 복구는 노출하지 않는다 —
        /// Navigation이 첫 선택지로 되돌려도 사용자가 굴려 둔 위치를 잡아당기지 않게. 열린 직후 첫 선택은 예외로 늘 확인한다.
        /// </summary>
        private void TrackSelection()
        {
            var eventSystem = EventSystem.current;
            var selected = eventSystem != null ? eventSystem.currentSelectedGameObject : null;
            if (selected == null || !selected.transform.IsChildOf(panel))
            {
                hasSeenNullSelection = true;
                return;
            }
            if (selected == lastSelection)
            {
                // 빈 선택 뒤 같은 버튼으로 복구됐다 — 움직이지 않고 표시만 소비한다(다음 실제 이동을 복구로 오판하지 않게).
                hasSeenNullSelection = false;
                return;
            }

            bool isRestore = hasSeenNullSelection || Time.frameCount - pointerFrame <= 1;
            bool shouldReveal = isInitialRevealPending || !isRestore;
            isInitialRevealPending = false;
            hasSeenNullSelection = false;
            lastSelection = selected;
            if (shouldReveal) EnsureSelectedVisible(selected);
        }

        private void HandleManualScroll()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.homeKey.wasPressedThisFrame)
                {
                    SetScroll(0f);
                    return;
                }
                if (keyboard.endKey.wasPressedThisFrame)
                {
                    SetScroll(ScrollRange());
                    return;
                }
            }

            float delta = 0f;
            float page = viewport.sizeDelta.y * PAGE_RATIO;
            if (keyboard != null)
            {
                if (keyboard.pageDownKey.wasPressedThisFrame) delta += page;
                if (keyboard.pageUpKey.wasPressedThisFrame) delta -= page;
            }

            var mouse = Mouse.current;
            if (mouse != null)
            {
                float wheel = mouse.scroll.ReadValue().y;
                if (wheel > 0.01f) delta -= WHEEL_STEP;
                else if (wheel < -0.01f) delta += WHEEL_STEP;
            }

            var gamepad = Gamepad.current;
            if (gamepad != null)
            {
                float stick = gamepad.rightStick.ReadValue().y;
                // 정지 중(timeScale 0)에도 움직이도록 unscaled.
                if (Mathf.Abs(stick) > STICK_DEADZONE) delta -= stick * STICK_SPEED * Time.unscaledDeltaTime;
            }

            if (delta != 0f) SetScroll(content.anchoredPosition.y + delta);
        }
    }
}
