using UnityEngine;

namespace Abyss.Runtime.Enemy
{
    /// <summary>
    /// 적 시각 피드백 전담. 피격 플래시·baseColor 유지 담당.
    /// 프로토 단계: SpriteRenderer 색상 일시 변경으로 hit 피드백 제공.
    /// nametag 텍스트는 자식 GameObject(TextMesh)로 분리 관리 — 본 컴포넌트는 SpriteRenderer만.
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

        private void Awake()
        {
            sr = GetComponent<SpriteRenderer>();
            if (sr != null) baseColor = sr.color;
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
            if (sr != null && flashTimer <= 0f) sr.color = baseColor;
        }

        private void Update()
        {
            if (flashTimer <= 0f) return;

            flashTimer -= Time.deltaTime;
            if (flashTimer <= 0f && sr != null)
            {
                sr.color = baseColor;
            }
        }
    }
}
