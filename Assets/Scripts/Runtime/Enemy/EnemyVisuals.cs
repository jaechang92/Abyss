using UnityEngine;

namespace Abyss.Runtime.Enemy
{
    /// <summary>
    /// 적 시각 피드백 전담. 피격 플래시·baseColor 유지 + 보스 연출(틴트·스케일 펀치) 담당.
    /// 프로토 단계: SpriteRenderer 색상/스케일 변경으로 피드백 제공(Animator 미사용).
    /// nametag 텍스트는 자식 GameObject(TextMesh)로 분리 관리 — 본 컴포넌트는 SpriteRenderer만.
    /// 색 우선순위: 피격 Flash > 지속 Tint(예고) > baseColor. 스케일 펀치는 색과 독립.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class EnemyVisuals : MonoBehaviour
    {
        [Header("Hit Flash")]
        [SerializeField, Min(0.01f)] private float flashDuration = 0.08f;
        [SerializeField] private Color flashColor = Color.white;

        private SpriteRenderer sr;
        private Color baseColor;
        private float flashTimer;

        // 지속 틴트(예고 등) — Flash보다 낮은 우선순위로 유지.
        private Color tintColor;
        private float tintTimer;

        // 스케일 펀치(강타 모션) — sin 곡선으로 커졌다 복원.
        private Vector3 baseScale = Vector3.one;
        private float punchMagnitude;
        private float punchDuration;
        private float punchTimer;

        private void Awake()
        {
            sr = GetComponent<SpriteRenderer>();
            if (sr != null) baseColor = sr.color;
            baseScale = transform.localScale;
        }

        /// <summary>
        /// 피격 플래시. 연속 호출 시 타이머 연장(max).
        /// </summary>
        public void Flash()
        {
            if (sr == null) return;
            flashTimer = Mathf.Max(flashTimer, flashDuration);
            sr.color = flashColor;
        }

        /// <summary>
        /// 런타임 중 baseColor 교체. EnemyData 변경 등 예외 경로.
        /// </summary>
        public void SetBaseColor(Color color)
        {
            baseColor = color;
            if (sr != null && flashTimer <= 0f && tintTimer <= 0f) sr.color = baseColor;
        }

        /// <summary>
        /// duration 동안 색조를 유지(예고 telegraph 등). Flash가 진행 중이면 Flash가 우선,
        /// 종료 후 남은 Tint 시간 동안 tintColor를 표시한다. 연속 호출 시 타이머 연장(max).
        /// </summary>
        public void Tint(Color color, float duration)
        {
            if (duration <= 0f) return;
            tintColor = color;
            tintTimer = Mathf.Max(tintTimer, duration);
            if (sr != null && flashTimer <= 0f) sr.color = tintColor;
        }

        /// <summary>
        /// 스케일 펀치(강타 모션). baseScale을 기준으로 sin 곡선만큼 순간 확대 후 복원.
        /// magnitude=0.2면 최대 +20% 확대. 색 연출과 독립적으로 동작.
        /// </summary>
        public void PunchScale(float magnitude, float duration)
        {
            if (duration <= 0f) return;
            punchMagnitude = magnitude;
            punchDuration = duration;
            punchTimer = duration;
        }

        private void Update()
        {
            UpdateColor();
            UpdatePunch();
        }

        private void UpdateColor()
        {
            if (flashTimer > 0f)
            {
                flashTimer -= Time.deltaTime;
                if (flashTimer <= 0f && sr != null)
                    sr.color = tintTimer > 0f ? tintColor : baseColor;
                return;
            }

            if (tintTimer > 0f)
            {
                tintTimer -= Time.deltaTime;
                if (sr != null) sr.color = tintTimer > 0f ? tintColor : baseColor;
            }
        }

        private void UpdatePunch()
        {
            if (punchTimer <= 0f) return;

            punchTimer -= Time.deltaTime;
            if (punchTimer <= 0f)
            {
                transform.localScale = baseScale;
                return;
            }

            float t = 1f - (punchTimer / punchDuration); // 0→1
            float scale = 1f + punchMagnitude * Mathf.Sin(t * Mathf.PI);
            transform.localScale = baseScale * scale;
        }
    }
}
