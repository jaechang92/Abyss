using Abyss.Runtime.Feedback;
using UnityEngine;

namespace Abyss.Runtime.Camera
{
    /// <summary>
    /// 플레이어를 추적하는 경량 카메라. Cinemachine 도입 전 프로토 단계용.
    /// `CameraShake`가 같은 GameObject에 공존해도 서로 덮어쓰지 않도록
    /// smoothed 기준점을 내부에서 유지하고 shake offset만 최종 position에 합성한다.
    /// </summary>
    [DefaultExecutionOrder(10)]
    public sealed class PlayerCameraFollow : MonoBehaviour
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

        private void Awake()
        {
            shake = GetComponent<CameraShake>();
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
                smoothedPosition = ConstrainPosition(target.position + offset);
                smoothedInitialized = true;
                transform.position = smoothedPosition;
            }
        }

        private void LateUpdate()
        {
            if (target == null) return;

            if (!smoothedInitialized)
            {
                smoothedPosition = ConstrainPosition(target.position + offset);
                smoothedInitialized = true;
            }

            Vector3 desired = ConstrainPosition(target.position + offset);
            smoothedPosition = Vector3.SmoothDamp(smoothedPosition, desired, ref velocity, smoothTime);
            smoothedPosition = ConstrainPosition(smoothedPosition);

            Vector3 shakeOffset = shake != null ? shake.CurrentShakeOffset : Vector3.zero;
            transform.position = ConstrainPosition(smoothedPosition + shakeOffset);
        }

        // Opt-in for finite illustrated rooms. Existing run cameras remain unconstrained.
        public void SetEnvironmentBounds(Vector2 min, Vector2 max)
        {
            environmentMin = min;
            environmentMax = max;
            constrainToEnvironment = true;
            smoothedInitialized = false;
        }

        /// <summary>경계를 풀어 다시 제약 없이 따라간다(맵 방 → 옛 아레나 방). 다음 프레임에 대상 위치로 맞춘다.</summary>
        public void ClearEnvironmentBounds()
        {
            constrainToEnvironment = false;
            smoothedInitialized = false;
        }

        private Vector3 ConstrainPosition(Vector3 position)
        {
            if (!constrainToEnvironment || !TryGetComponent<UnityEngine.Camera>(out var camera)) return position;
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
        }
    }
}
