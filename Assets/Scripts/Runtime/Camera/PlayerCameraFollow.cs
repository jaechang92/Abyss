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
                smoothedPosition = target.position + offset;
                smoothedInitialized = true;
                transform.position = smoothedPosition;
            }
        }

        private void LateUpdate()
        {
            if (target == null) return;

            if (!smoothedInitialized)
            {
                smoothedPosition = target.position + offset;
                smoothedInitialized = true;
            }

            Vector3 desired = target.position + offset;
            smoothedPosition = Vector3.SmoothDamp(smoothedPosition, desired, ref velocity, smoothTime);

            Vector3 shakeOffset = shake != null ? shake.CurrentShakeOffset : Vector3.zero;
            transform.position = smoothedPosition + shakeOffset;
        }

        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
            smoothedInitialized = false;
        }
    }
}
