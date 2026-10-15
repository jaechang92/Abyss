using Abyss.Runtime.Feedback;
using UnityEngine;

namespace Abyss.Runtime.Camera
{
    /// <summary>
    /// 플레이어를 추적하는 경량 카메라. Cinemachine 도입 전 프로토 단계용.
    /// `CameraShake`가 같은 GameObject에 공존해도 서로 덮어쓰지 않도록
    /// smoothed 기준점을 내부에서 유지하고 shake offset만 최종 position에 합성한다.
    ///
    /// 추적 방식은 둘이다. 같은 오브젝트에 켜진 <see cref="RunCameraProfile"/>이 있으면 Run 축별 추적
    /// (PlayerCameraFollow.RunMotion), 없으면 기존 <c>Vector3.SmoothDamp</c>(로비 등). 어느 쪽이든
    /// transform 을 쓰는 곳은 이 컴포넌트의 최종 합성 한 곳뿐이다.
    /// </summary>
    [DefaultExecutionOrder(10)]
    public sealed partial class PlayerCameraFollow : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 offset = new(0f, 2f, -10f);
        [SerializeField, Min(0f)] private float smoothTime = 0.18f;
        [SerializeField] private bool findPlayerAtStart = true;
        [SerializeField] private bool constrainToEnvironment;
        [SerializeField] private Vector2 environmentMin;
        [SerializeField] private Vector2 environmentMax;

        private Vector3 velocity;
        private Vector3 smoothedPosition;
        private bool smoothedInitialized;
        private CameraShake shake;
        private UnityEngine.Camera viewCamera;
        private bool viewCameraResolved;

        // 같은 오브젝트의 Camera. 경계·화면 크기 계산마다 검색하지 않도록 한 번만 찾는다.
        private UnityEngine.Camera ViewCamera
        {
            get
            {
                if (!viewCameraResolved)
                {
                    viewCamera = GetComponent<UnityEngine.Camera>();
                    viewCameraResolved = true;
                }
                return viewCamera;
            }
        }

        private void Awake()
        {
            shake = GetComponent<CameraShake>();
            runProfile = GetComponent<RunCameraProfile>();
        }

        private void Start()
        {
            if (target == null && findPlayerAtStart)
            {
                var player = FindAnyObjectByType<Abyss.Runtime.Player.PlayerCharacter>();
                if (player != null) target = player.transform;
            }

            if (target != null)
            {
                RefreshTargetCache();
                if (IsRunMotionActive)
                {
                    SnapRunMotion(target.position);
                }
                else
                {
                    smoothedPosition = ConstrainPosition(target.position + offset);
                    smoothedInitialized = true;
                }
                transform.position = smoothedPosition;
            }
        }

        private void LateUpdate()
        {
            if (target == null) return;

            RefreshTargetCache();
            // 파괴된 연출 소유자는 추적 정지 판정(IsPresentationHeld) 전에 푼다.
            ReleaseIfOwnerDestroyed();
            bool runMotion = IsRunMotionActive;
            if (ConsumeRunViewChange(runMotion)) ResetForViewChange();

            if (runMotion)
            {
                // Run 축별 추적. 정지·모달·연출 중에는 기준점이 멈추고, 아래 최종 합성(연출·흔들림)은 그대로 돈다.
                UpdateRunMotion();
            }
            else
            {
                if (!smoothedInitialized)
                {
                    smoothedPosition = ConstrainPosition(target.position + offset);
                    smoothedInitialized = true;
                }

                if (!IsPresentationHeld)
                {
                    Vector3 desired = ConstrainPosition(target.position + offset);
                    smoothedPosition = Vector3.SmoothDamp(smoothedPosition, desired, ref velocity, smoothTime);
                    smoothedPosition = ConstrainPosition(smoothedPosition);
                }
            }

            // 연출 임시 중심(PlayerCameraFollow.Presentation)은 추적 기준점을 바꾸지 않고 최종 위치에만 합성한다.
            // 흔들림은 연출·모달 동안 버린다(PlayerCameraFollow.RoomFraming).
            Vector3 framed = ApplyPresentationOffset(smoothedPosition);
            transform.position = ConstrainPosition(framed + ResolveShakeOffset());
        }

        // Opt-in for finite illustrated rooms. Existing run cameras remain unconstrained.
        // 경계 변경 = 새 방. 방 구도(보스)는 호출자가 다시 선언하고, 이전 방의 연출 임대·흔들림은 무효로 한다.
        public void SetEnvironmentBounds(Vector2 min, Vector2 max)
        {
            environmentMin = min;
            environmentMax = max;
            constrainToEnvironment = true;
            smoothedInitialized = false;
            ResetRoomTransition();
        }

        /// <summary>경계를 풀어 다시 제약 없이 따라간다(맵 방 → 옛 아레나 방). 다음 프레임에 대상 위치로 맞춘다. 보스 구도도 해제한다.</summary>
        public void ClearEnvironmentBounds()
        {
            constrainToEnvironment = false;
            smoothedInitialized = false;
            ResetRoomTransition();
        }

        private Vector3 ConstrainPosition(Vector3 position)
        {
            if (!constrainToEnvironment) return position;
            var camera = ViewCamera;
            if (camera == null) return position;
            float halfHeight = camera.orthographicSize;
            float halfWidth = halfHeight * camera.aspect;
            position.x = ClampAxis(position.x, environmentMin.x, environmentMax.x, halfWidth);
            position.y = ClampAxis(position.y, environmentMin.y, environmentMax.y, halfHeight);
            return position;
        }

        private static float ClampAxis(float value, float min, float max, float extent)
        {
            return max - min <= extent * 2f ? (min + max) * 0.5f : Mathf.Clamp(value, min + extent, max - extent);
        }

        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
            smoothedInitialized = false;
            ResetRunMotionState();
        }
    }
}
