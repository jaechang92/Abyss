using UnityEngine;

namespace Abyss.Runtime.Camera
{
    /// <summary>
    /// 플레이어를 추적하는 경량 카메라. Cinemachine 도입 전 프로토 단계용.
    /// Week 1 에디터 체크리스트 Top 10 #10(카메라 누락) 해결.
    /// </summary>
    public sealed class PlayerCameraFollow : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 offset = new(0f, 2f, -10f);
        [SerializeField, Min(0f)] private float smoothTime = 0.18f;
        [SerializeField] private bool findPlayerAtStart = true;

        private Vector3 velocity;

        private void Start()
        {
            if (target == null && findPlayerAtStart)
            {
                var player = FindAnyObjectByType<Abyss.Runtime.Player.PlayerCharacter>();
                if (player != null) target = player.transform;
            }
        }

        private void LateUpdate()
        {
            if (target == null) return;

            Vector3 desired = target.position + offset;
            transform.position = Vector3.SmoothDamp(transform.position, desired, ref velocity, smoothTime);
        }

        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
        }
    }
}
