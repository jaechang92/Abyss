using UnityEngine;

namespace Abyss.Runtime.Player
{
    /// <summary>
    /// 공격 시각 이펙트. 짧은 시간 SpriteRenderer를 켰다 끔.
    /// Light/Heavy 에 따라 색상·스케일·지속시간을 다르게 부여.
    /// 프로토 범위: 풀링 없이 Player 자식으로 단일 인스턴스.
    /// </summary>
    public sealed class AttackEffect : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer sr;

        [Header("Light")]
        [SerializeField, Min(0.01f)] private float lightDuration = 0.18f;
        [SerializeField] private Color lightColor = new(1f, 0.95f, 0.3f, 0.95f);
        [SerializeField] private Vector3 lightScale = new(1.6f, 0.8f, 1f);

        [Header("Heavy")]
        [SerializeField, Min(0.01f)] private float heavyDuration = 0.28f;
        [SerializeField] private Color heavyColor = new(1f, 0.5f, 0.1f, 1f);
        [SerializeField] private Vector3 heavyScale = new(2.1f, 1.1f, 1f);

        private float timer;

        private void Awake()
        {
            if (sr == null) sr = GetComponent<SpriteRenderer>();
            if (sr != null) sr.enabled = false;
        }

        public void PlayLight()
        {
            Play(lightColor, lightScale, lightDuration);
        }

        public void PlayHeavy()
        {
            Play(heavyColor, heavyScale, heavyDuration);
        }

        private void Play(Color color, Vector3 scale, float duration)
        {
            if (sr == null) return;
            sr.enabled = true;
            sr.color = color;
            transform.localScale = scale;
            timer = Mathf.Max(timer, duration);
        }

        private void Update()
        {
            if (timer <= 0f) return;

            timer -= Time.unscaledDeltaTime;
            if (timer <= 0f && sr != null)
            {
                sr.enabled = false;
            }
        }
    }
}
