using UnityEngine;
using UnityEngine.InputSystem;

namespace Abyss.Runtime.Camera
{
    /// <summary>
    /// 아트 테스트 씬 전용 카메라 조작. <b>게임 시스템을 하나도 안 끌어온다.</b>
    ///
    /// 🔑 <b>왜 플레이어를 안 쓰나</b> — 배경·타일·프롭만 보려는데 플레이어를 띄우면
    /// 부트스트랩·매니저·세이브가 줄줄이 따라온다. 그중 하나가 어긋나면 <b>아트를 보러 왔다가
    /// 시스템을 디버깅하게 된다.</b> 대신 축척 기준은 씬에 놓인 2유닛 표지가 맡는다.
    ///
    /// 조작: 방향키/WASD 이동 · Q·E 또는 휠 확대축소 · R 원위치 · Shift 빠르게.
    ///
    /// ⚠️ <b>Input System(신) 전용.</b> 이 프로젝트는 <c>activeInputHandler: 1</c>이라
    /// 구 <c>Input.GetAxis</c>는 런타임에 예외를 던진다.
    ///
    /// ⚠️ <b>모든 시간은 <see cref="Time.unscaledDeltaTime"/></b> — 이 씬은 timeScale을 쓰지 않고,
    /// 정지 위에서 도는 다른 연출과 규약을 맞춘다.
    /// </summary>
    [RequireComponent(typeof(UnityEngine.Camera))]
    public sealed class ArtTestPanner : MonoBehaviour
    {
        [SerializeField] private float panSpeed = 8f;
        [SerializeField] private float zoomSpeed = 6f;
        [SerializeField] private float minSize = 1.5f;
        [SerializeField] private float maxSize = 20f;

        private UnityEngine.Camera cam;
        private Vector3 home;
        private float homeSize;

        private void Awake()
        {
            cam = GetComponent<UnityEngine.Camera>();
            home = transform.position;
            homeSize = cam.orthographicSize;
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            float dt = Time.unscaledDeltaTime;
            float speed = panSpeed * (keyboard.leftShiftKey.isPressed ? 3f : 1f);

            var move = Vector2.zero;
            if (keyboard.leftArrowKey.isPressed || keyboard.aKey.isPressed) move.x -= 1f;
            if (keyboard.rightArrowKey.isPressed || keyboard.dKey.isPressed) move.x += 1f;
            if (keyboard.downArrowKey.isPressed || keyboard.sKey.isPressed) move.y -= 1f;
            if (keyboard.upArrowKey.isPressed || keyboard.wKey.isPressed) move.y += 1f;

            // 확대할수록 같은 키 입력이 화면에서 더 멀리 가는 것을 막는다 — 배율에 비례해 옮긴다.
            float scale = cam.orthographicSize / homeSize;
            transform.position += (Vector3)(move * (speed * scale * dt));

            float zoom = 0f;
            if (keyboard.qKey.isPressed) zoom += 1f;
            if (keyboard.eKey.isPressed) zoom -= 1f;
            if (Mouse.current != null) zoom -= Mouse.current.scroll.ReadValue().y * 0.01f;

            if (!Mathf.Approximately(zoom, 0f))
                cam.orthographicSize = Mathf.Clamp(
                    cam.orthographicSize + zoom * zoomSpeed * scale * dt, minSize, maxSize);

            if (keyboard.rKey.wasPressedThisFrame)
            {
                transform.position = home;
                cam.orthographicSize = homeSize;
            }
        }
    }
}
