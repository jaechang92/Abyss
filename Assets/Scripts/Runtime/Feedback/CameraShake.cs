using UnityEngine;

namespace Abyss.Runtime.Feedback
{
    /// <summary>
    /// 카메라 흔들림 오프셋 공급자. 직접 transform을 쓰지 않고 `CurrentShakeOffset` 만 제공.
    /// PlayerCameraFollow가 LateUpdate에서 읽어 최종 position에 합성한다.
    /// 이 분리가 필요한 이유: 같은 GameObject에 Follow + Shake가 공존하면
    /// localPosition / position을 서로 덮어써 추적이 무효화되기 때문.
    /// </summary>
    [DefaultExecutionOrder(-10)]
    public sealed class CameraShake : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float dampPerSecond = 6f;

        private float currentMagnitude;
        private float timer;

        /// <summary>현재 프레임 흔들림 오프셋(월드 XY). Follow가 읽어 더한다.</summary>
        public Vector3 CurrentShakeOffset { get; private set; }

        /// <summary>
        /// 쉐이크 요청. 다중 호출 시 더 큰 강도·긴 시간이 이김(덮어쓰지 않고 max).
        /// </summary>
        public void Shake(float magnitude, float duration)
        {
            if (magnitude <= 0f || duration <= 0f) return;

            currentMagnitude = Mathf.Max(currentMagnitude, magnitude);
            timer = Mathf.Max(timer, duration);
        }

        private void LateUpdate()
        {
            if (timer > 0f)
            {
                timer -= Time.unscaledDeltaTime;
                Vector2 offset = Random.insideUnitCircle * currentMagnitude;
                CurrentShakeOffset = new Vector3(offset.x, offset.y, 0f);
                currentMagnitude = Mathf.MoveTowards(currentMagnitude, 0f, dampPerSecond * Time.unscaledDeltaTime);
            }
            else
            {
                CurrentShakeOffset = Vector3.zero;
                currentMagnitude = 0f;
            }
        }

        private void OnDisable()
        {
            // 비활성 시 잔여 offset 정리. 다음 활성화 시 깨끗한 상태로 시작.
            CurrentShakeOffset = Vector3.zero;
            currentMagnitude = 0f;
            timer = 0f;
        }
    }
}
