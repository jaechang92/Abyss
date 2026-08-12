using UnityEngine;

namespace Abyss.Runtime.Feedback
{
    /// <summary>
    /// 캐릭터가 지나간 자리에 남는 반투명 잔상. 원본 SpriteRenderer의 스프라이트·크기·방향을 복제해
    /// 그 자리에 붙여 두고, 지정 시간에 걸쳐 알파가 빠지며 자동 소멸한다.
    ///
    /// <see cref="BossAreaEffect"/>와 같은 정책이다 — 프리팹·리소스 의존 없이 런타임 생성,
    /// 빈도가 낮아(대시 쿨다운 0.3초 이상) 풀링 없이 Instantiate/Destroy로 충분하다.
    ///
    /// <b>피해 판정은 하지 않는다.</b> 이 컴포넌트는 순수 표현이고, 잔상이 언제 터지고 얼마나
    /// 아픈지는 스킬을 소유한 쪽(<c>PlayerCharacter</c> Passive 파트)이 정한다 — 표현이 규칙을
    /// 소유하면 잔상을 쓰는 다른 스킬이 생겼을 때 수치가 두 곳으로 갈라진다.
    /// </summary>
    public sealed class AfterimageEffect : MonoBehaviour
    {
        private SpriteRenderer sr;
        private float duration;
        private float timer;
        private Color baseColor;

        /// <summary>
        /// 원본 렌더러의 겉모습을 복제한 잔상을 지정 위치에 남긴다.
        /// scale은 호출부가 넘긴다 — 좌우 반전이 <c>transform.localScale.x</c> 부호로 표현되므로
        /// (facing SoT) 렌더러가 아니라 트랜스폼에서 가져와야 방향이 맞는다.
        /// </summary>
        public static void Spawn(SpriteRenderer source, Vector3 position, Vector3 scale, Color tint, float duration)
        {
            if (source == null || source.sprite == null) return;

            var go = new GameObject("AfterimageEffect");
            go.transform.position = position;
            go.transform.localScale = scale;

            var fx = go.AddComponent<AfterimageEffect>();
            fx.Init(source, tint, duration);
        }

        private void Init(SpriteRenderer source, Color tint, float dur)
        {
            sr = gameObject.AddComponent<SpriteRenderer>();
            sr.sprite = source.sprite;
            sr.flipX = source.flipX;
            sr.flipY = source.flipY;
            sr.sortingLayerID = source.sortingLayerID;
            // 본체보다 뒤에 둔다 — 잔상이 캐릭터를 가리면 "내가 어디 있는지"가 흐려진다.
            sr.sortingOrder = source.sortingOrder - 1;

            baseColor = tint;
            duration = Mathf.Max(0.05f, dur);
            timer = 0f;
            Apply(0f);
        }

        private void Update()
        {
            // 스케일 시간을 쓴다 — 정지 중에는 잔상도 멈춰야 폭발 타이밍(같은 시간축)과 어긋나지 않는다.
            timer += Time.deltaTime;
            float t = Mathf.Clamp01(timer / duration);
            Apply(t);
            if (t >= 1f) Destroy(gameObject);
        }

        private void Apply(float t)
        {
            Color c = baseColor;
            // 후반으로 갈수록 옅어진다 — 터지기 직전에 가장 흐리므로 링이 시선을 대신 잡는다.
            c.a = Mathf.Lerp(baseColor.a, 0f, t);
            sr.color = c;
        }
    }
}
