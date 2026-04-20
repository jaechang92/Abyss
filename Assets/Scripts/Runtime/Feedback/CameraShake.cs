using UnityEngine;

namespace Abyss.Runtime.Feedback
{
    /// <summary>
    /// transform.localPosition 직접 쉐이크. Cinemachine Impulse 미사용 절충.
    /// Shake(mag, dur) 다중 호출 시 더 큰 강도/긴 시간이 이김.
    /// </summary>
    public sealed class CameraShake : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float dampPerSecond = 6f;

        private Vector3 baseLocalPosition;
        private float currentMagnitude;
        private float timer;
        private bool initialized;

        private void OnEnable()
        {
            if (!initialized)
            {
                baseLocalPosition = transform.localPosition;
                initialized = true;
            }
        }

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
                transform.localPosition = baseLocalPosition + new Vector3(offset.x, offset.y, 0f);

                currentMagnitude = Mathf.MoveTowards(currentMagnitude, 0f, dampPerSecond * Time.unscaledDeltaTime);
                return;
            }

            if (transform.localPosition != baseLocalPosition)
            {
                transform.localPosition = baseLocalPosition;
                currentMagnitude = 0f;
            }
        }
    }
}
