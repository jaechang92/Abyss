using UnityEngine;

namespace Abyss.Runtime.Player
{
    /// <summary>
    /// 공격 시각 이펙트. 짧은 시간 SpriteRenderer를 켰다 끔.
    /// Light/Heavy 에 따라 색상·크기·지속시간을 다르게 부여.
    /// 프로토 범위: 풀링 없이 Player 자식으로 단일 인스턴스.
    ///
    /// 🔴 <b>크기를 절대값으로 안 들고 판정 박스에서 파생한다</b> (2026-09-15).
    /// 예전 값(light <c>1.6x0.8</c> · heavy <c>2.1x1.1</c>)은 <b>몸 그림에 참격이 구워져 있던</b>
    /// 시절 그 위에 얹던 보조광이었다. 참격을 그림에서 뺀 뒤로 이 이펙트가 참격을 혼자 맡는데,
    /// heavy 의 <c>2.1</c> 은 실제 판정 폭 <c>1.6</c> 보다 넓다 — <b>닿을 것 같은데 안 닿는다.</b>
    /// 라이트와 헤비는 <b>같은 판정 박스 하나</b>를 쓰므로(<c>PlayerCharacter.attackBoxSize</c>)
    /// 헤비가 더 넓게 보일 근거가 애초에 없었다. 둘을 가르는 것은 <b>색과 지속시간</b>이다.
    ///
    /// 🔑 <b>그래서 호출자가 판정 크기를 준다.</b> 무기 장비가 붙으면 사거리가 무기마다 달라지는데
    /// (<c>14-weapon-equipment-system.md</c>), 값이 여기 박혀 있으면 그때 또 어긋난다.
    /// 화면이 판정을 그대로 그리면 플레이어가 <b>사거리를 눈으로 배운다.</b>
    /// </summary>
    public sealed class AttackEffect : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer sr;

        [Header("Light")]
        [SerializeField, Min(0.01f)] private float lightDuration = 0.18f;
        [SerializeField] private Color lightColor = new(1f, 0.97f, 0.62f, 0.9f);

        [Tooltip("판정 박스 대비 채움 비율. 1 = 판정 크기 그대로. 1을 넘기면 화면이 사거리를 부풀린다.")]
        [SerializeField] private Vector2 lightFill = new(0.9f, 0.75f);

        [Header("Heavy")]
        [SerializeField, Min(0.01f)] private float heavyDuration = 0.28f;
        [SerializeField] private Color heavyColor = new(1f, 0.6f, 0.22f, 0.92f);

        [Tooltip("판정 박스 대비 채움 비율. 헤비는 판정을 꽉 채운다 - 라이트와 판정이 같으므로 더 키우지 않는다.")]
        [SerializeField] private Vector2 heavyFill = Vector2.one;

        private float timer;

        private void Awake()
        {
            if (sr == null) sr = GetComponent<SpriteRenderer>();
            if (sr != null) sr.enabled = false;
        }

        /// <param name="attackBoxSize">이번 공격의 판정 박스 크기(유닛). 이펙트가 그 안을 채운다.</param>
        public void PlayLight(Vector2 attackBoxSize)
        {
            Play(lightColor, ScaleFor(attackBoxSize, lightFill), lightDuration);
        }

        /// <param name="attackBoxSize">이번 공격의 판정 박스 크기(유닛). 이펙트가 그 안을 채운다.</param>
        public void PlayHeavy(Vector2 attackBoxSize)
        {
            Play(heavyColor, ScaleFor(attackBoxSize, heavyFill), heavyDuration);
        }

        /// <summary>
        /// 목표 크기(유닛) → <c>localScale</c>.
        ///
        /// 🔑 <b>스프라이트 자체 크기로 나눈다.</b> 지금 물린 <c>WhiteSquare</c> 는 1x1 유닛이라
        /// 나눗셈이 항등이지만, 나중에 <b>진짜 참격 그림</b>으로 갈아끼우면 크기가 달라진다.
        /// 그때 스케일이 조용히 틀어지지 않게 여기서 흡수한다.
        /// </summary>
        private Vector3 ScaleFor(Vector2 attackBoxSize, Vector2 fill)
        {
            Vector2 target = new(attackBoxSize.x * fill.x, attackBoxSize.y * fill.y);
            Vector2 spriteSize = sr != null && sr.sprite != null
                ? (Vector2)sr.sprite.bounds.size
                : Vector2.one;

            float x = spriteSize.x > 0.0001f ? target.x / spriteSize.x : target.x;
            float y = spriteSize.y > 0.0001f ? target.y / spriteSize.y : target.y;
            return new Vector3(x, y, 1f);
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
