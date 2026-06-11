using UnityEngine;

namespace Abyss.Runtime.Feedback
{
    /// <summary>
    /// 보스 근접 광역 패턴(회전베기·꼬리치기)의 타격 범위를 보여주는 일회성 원형 링 이펙트.
    /// 프리팹/리소스 의존 없이 런타임에 링 스프라이트를 1회 생성·캐시하고,
    /// 작은 원에서 지정 반경까지 확장하며 알파가 페이드아웃된 뒤 자동 소멸한다.
    ///
    /// 보스 패턴은 빈도가 낮아(초당 1회 미만) 풀링 없이 Instantiate/Destroy로 충분하다(완성 우선).
    /// 정적 팩토리 <see cref="Spawn"/>로 호출한다.
    /// </summary>
    public sealed class BossAreaEffect : MonoBehaviour
    {
        private const int RingTextureSize = 64;
        private static Sprite ringSprite;

        private SpriteRenderer sr;
        private float maxRadius;
        private float duration;
        private float timer;
        private Color baseColor;

        /// <summary>
        /// 지정 위치에 반경 radius(월드 유닛)의 원형 링 이펙트를 띄운다.
        /// 링은 radius*0.4에서 radius까지 확장하며 알파가 사라진다.
        /// </summary>
        public static void Spawn(Vector3 position, float radius, Color color, float duration = 0.35f)
        {
            var go = new GameObject("BossAreaEffect");
            go.transform.position = position;
            var fx = go.AddComponent<BossAreaEffect>();
            fx.Init(radius, color, duration);
        }

        private void Init(float radius, Color color, float dur)
        {
            sr = gameObject.AddComponent<SpriteRenderer>();
            sr.sprite = GetRingSprite();
            sr.sortingOrder = 5; // 적/플레이어보다 위에 보이도록
            maxRadius = Mathf.Max(0.1f, radius);
            baseColor = color;
            duration = Mathf.Max(0.05f, dur);
            timer = 0f;
            Apply(0f);
        }

        private void Update()
        {
            timer += Time.deltaTime;
            float t = Mathf.Clamp01(timer / duration);
            Apply(t);
            if (t >= 1f) Destroy(gameObject);
        }

        private void Apply(float t)
        {
            // 링 스프라이트는 지름 1유닛(PPU=size) → localScale = 지름. 반경 radius면 scale = radius*2.
            float diameter = Mathf.Lerp(maxRadius * 0.8f, maxRadius * 2f, t);
            transform.localScale = new Vector3(diameter, diameter, 1f);

            Color c = baseColor;
            c.a = Mathf.Lerp(0.9f, 0f, t);
            sr.color = c;
        }

        /// <summary>
        /// 흰색 원형 링(테두리만) 스프라이트를 1회 생성해 캐시. 색은 SpriteRenderer.color로 입힌다.
        /// 안쪽 70%는 투명, 70~100% 구간만 채워 링 형태를 만든다.
        /// </summary>
        private static Sprite GetRingSprite()
        {
            if (ringSprite != null) return ringSprite;

            int size = RingTextureSize;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            float center = (size - 1) * 0.5f;
            float outer = center;
            float inner = center * 0.7f;
            var clear = new Color(1f, 1f, 1f, 0f);
            var pixels = new Color[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Mathf.Sqrt((x - center) * (x - center) + (y - center) * (y - center));
                    pixels[y * size + x] = (dist <= outer && dist >= inner) ? Color.white : clear;
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();

            // PPU = size → 텍스처 전체가 1유닛(지름 1). pivot 중앙.
            ringSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            return ringSprite;
        }
    }
}
