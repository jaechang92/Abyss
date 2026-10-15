using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Abyss.Runtime.UI
{
    /// <summary>
    /// <see cref="NodeMapReadability"/>의 입력 쪽 — 선택 추적·최소 노출·수동 스크롤(휠·Page·Home·End·오른쪽 스틱, 가로·세로).
    ///
    /// 자동 노출은 세 경우뿐이다. 매 프레임 끌어당기지 않아 수동 스크롤 위치가 두 축 모두 지켜진다.
    /// <list type="bullet">
    /// <item>새로 연 뒤 첫 선택 — 처음(가로·세로 맨 앞)에서 이미 보이면 그대로 둔다.</item>
    /// <item>키보드·패드로 노드가 바뀔 때 — 가로는 최소한만, 세로는 새 카드의 맨 위(처음부터 읽기)로.
    ///       포인터 누름(그 프레임과 다음 프레임)·빈 선택 뒤 복구는 제외한다(Navigation의 첫 노드 복구가 위치를 잡아당기지 않게).</item>
    /// <item>재배치(배율·화면·글자·골드·언어·표시 카드) 직전에 <b>가로로 보이던</b> 같은 선택 카드가 밀려났을 때 — 한 번, 가로만.
    ///       세로 읽기 위치는 비율로 유지하고 건드리지 않는다.</item>
    /// </list>
    /// 상위 모달(저장·설정·도감)이 입력을 쥔 동안은 수동 스크롤·자동 노출을 하지 않는다. 선택·클릭은 부르지 않는다.
    /// </summary>
    public sealed partial class NodeMapReadability
    {
        private const float REVEAL_MARGIN = 6f;
        private const float VISIBLE_TOLERANCE = 1f;
        private const float WHEEL_STEP = 72f;
        private const float PAGE_RATIO = 0.8f;
        private const float STICK_SPEED = 420f;
        private const float STICK_DEADZONE = 0.2f;

        private bool isInitialRevealPending;
        private bool hasSeenNullSelection;
        private GameObject lastSelection;      // 마지막으로 확인한 유효 노드
        private GameObject reflowRevealTarget;
        private int pointerFrame = -10;

        /// <summary>저장 모달·설정·도감이 입력을 쥔 동안(닫은 그 프레임 포함). NodeMapPanel.Navigation과 같은 조건이다.</summary>
        private static bool IsUpperModalOwningInput =>
            SaveStatusOverlay.IsCapturingInput ||
            SettingsPanel.IsOpen || SettingsPanel.WasClosedThisFrame ||
            CodexPanel.IsOpen || CodexPanel.WasClosedThisFrame;

        /// <summary>
        /// 선택된 노드 카드를 가로로 최소한만 움직여 보이게 하고, 세로는 카드 맨 위(처음부터 읽기)로 둔다.
        /// 노드가 아니면 아무것도 하지 않는다. 클릭은 부르지 않는다.
        /// </summary>
        public void EnsureSelectedVisible(GameObject selected)
        {
            var rect = CardOf(selected);
            if (rect == null) return;
            SetScroll(RevealX(rect), 0f);
        }

        /// <summary>새로 연 갈림길. 첫 선택을 다시 확인하고, 이전 열림의 추적·예약을 비운다.</summary>
        private void ResetSelectionTracking()
        {
            isInitialRevealPending = true;
            hasSeenNullSelection = false;
            lastSelection = null;
            reflowRevealTarget = null;
        }

        /// <summary>재배치 직전에 호출된다. 현재 선택이 보이는 노드 카드이고 가로로 보기 영역 안에 있으면 그것을, 아니면 null.</summary>
        private GameObject CaptureHorizontallyVisibleSelection()
        {
            var eventSystem = EventSystem.current;
            var selected = eventSystem != null ? eventSystem.currentSelectedGameObject : null;
            var rect = CardOf(selected);
            if (rect == null) return null;

            float width = rect.sizeDelta.x;
            float left = rect.anchoredPosition.x - width * 0.5f;
            float right = left + width;
            float viewportWidth = viewport.sizeDelta.x;
            float x = ScrollX();
            bool isVisible = width > viewportWidth
                ? left < x + viewportWidth && right > x
                : left >= x - VISIBLE_TOLERANCE && right <= x + viewportWidth + VISIBLE_TOLERANCE;
            return isVisible ? selected : null;
        }

        /// <summary>재배치 뒤 한 번 가로 노출을 예약한다. 처리는 LateUpdate의 상위 모달 관문 뒤에서 한다.</summary>
        private void ScheduleReflowReveal(GameObject selected) => reflowRevealTarget = selected;

        /// <summary>가로 최소 이동 위치. 보기보다 넓은 카드는 왼쪽부터.</summary>
        private float RevealX(RectTransform rect)
        {
            float width = rect.sizeDelta.x;
            float left = rect.anchoredPosition.x - width * 0.5f;
            float right = left + width;
            float viewportWidth = viewport.sizeDelta.x;
            float x = ScrollX();

            if (width > viewportWidth) return left;
            if (left < x) return left - REVEAL_MARGIN;
            if (right > x + viewportWidth) return right - viewportWidth + REVEAL_MARGIN;
            return x;
        }

        /// <summary>선택이 보이는 노드(또는 그 자식)이면 그 카드. 아니면 null.</summary>
        private RectTransform CardOf(GameObject target)
        {
            if (target == null || content == null) return null;
            foreach (var node in nodes)
            {
                if (!node.gameObject.activeSelf) continue;
                if (target.transform == node.transform || target.transform.IsChildOf(node.transform))
                    return (RectTransform)node.transform;
            }
            return null;
        }

        // ───────────────────────── 스크롤 위치 ─────────────────────────
        // 내용은 왼쪽 위 기준이다 — anchoredPosition = (-가로 위치, 세로 위치). 0이 처음(왼쪽·위)이다.

        private float RangeX() => content == null ? 0f : Mathf.Max(0f, content.sizeDelta.x - viewport.sizeDelta.x);

        private float RangeY() => content == null ? 0f : Mathf.Max(0f, content.sizeDelta.y - viewport.sizeDelta.y);

        private float ScrollX() => content == null ? 0f : -content.anchoredPosition.x;

        private float ScrollY() => content == null ? 0f : content.anchoredPosition.y;

        private static float Normalized(float value, float range) => range > 0f ? Mathf.Clamp01(value / range) : 0f;

        private void SetScroll(float x, float y)
        {
            readingScroll.StopMovement();
            content.anchoredPosition = new Vector2(-Mathf.Clamp(x, 0f, RangeX()), Mathf.Clamp(y, 0f, RangeY()));
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

        /// <summary>재배치 전에 가로로 보이던 같은 선택이 아직 선택돼 있으면 한 번만 가로로 최소 노출한다. 세로는 그대로.</summary>
        private void ProcessReflowReveal()
        {
            if (reflowRevealTarget == null) return;
            var target = reflowRevealTarget;
            reflowRevealTarget = null;
            var eventSystem = EventSystem.current;
            if (eventSystem == null || eventSystem.currentSelectedGameObject != target) return;

            var rect = CardOf(target);
            if (rect != null) SetScroll(RevealX(rect), ScrollY());
        }

        /// <summary>
        /// 유효 노드가 바뀐 프레임에만 노출한다. 포인터 누름(그 프레임과 다음 프레임)·빈 선택(또는 노드 밖) 뒤 복구는 노출하지 않는다 —
        /// Navigation이 첫 노드로 되돌려도 사용자가 굴려 둔 위치를 잡아당기지 않게. 열린 직후 첫 선택은 예외로 늘 확인한다.
        /// </summary>
        private void TrackSelection()
        {
            var eventSystem = EventSystem.current;
            var selected = eventSystem != null ? eventSystem.currentSelectedGameObject : null;
            if (CardOf(selected) == null)
            {
                hasSeenNullSelection = true;
                return;
            }
            if (selected == lastSelection)
            {
                // 빈 선택 뒤 같은 노드로 복구됐다 — 움직이지 않고 표시만 소비한다(다음 실제 이동을 복구로 오판하지 않게).
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
                    SetScroll(0f, 0f);
                    return;
                }
                if (keyboard.endKey.wasPressedThisFrame)
                {
                    SetScroll(RangeX(), RangeY());
                    return;
                }
            }

            bool isShift = keyboard != null && keyboard.shiftKey.isPressed;
            float dx = 0f;
            float dy = 0f;
            if (keyboard != null)
            {
                float page = isShift ? viewport.sizeDelta.x * PAGE_RATIO : viewport.sizeDelta.y * PAGE_RATIO;
                float step = 0f;
                if (keyboard.pageDownKey.wasPressedThisFrame) step += page;
                if (keyboard.pageUpKey.wasPressedThisFrame) step -= page;
                if (isShift) dx += step;
                else dy += step;
            }

            var mouse = Mouse.current;
            if (mouse != null)
            {
                Vector2 wheel = mouse.scroll.ReadValue();
                // 세로 휠: 세로로 넘칠 때는 세로, 아니면 가로. Shift를 누르면 가로.
                float wheelStep = wheel.y > 0.01f ? -WHEEL_STEP : wheel.y < -0.01f ? WHEEL_STEP : 0f;
                if (isShift || RangeY() <= 0f) dx += wheelStep;
                else dy += wheelStep;
                // 가로 휠(틸트)은 늘 가로.
                if (wheel.x > 0.01f) dx += WHEEL_STEP;
                else if (wheel.x < -0.01f) dx -= WHEEL_STEP;
            }

            var gamepad = Gamepad.current;
            if (gamepad != null)
            {
                Vector2 stick = gamepad.rightStick.ReadValue();
                // 정지 중(timeScale 0)에도 움직이도록 unscaled.
                if (Mathf.Abs(stick.x) > STICK_DEADZONE) dx += stick.x * STICK_SPEED * Time.unscaledDeltaTime;
                if (Mathf.Abs(stick.y) > STICK_DEADZONE) dy -= stick.y * STICK_SPEED * Time.unscaledDeltaTime;
            }

            if (dx != 0f || dy != 0f) SetScroll(ScrollX() + dx, ScrollY() + dy);
        }
    }
}
