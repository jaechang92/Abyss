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
        /// <summary>Strike=발동 시 확장+페이드, Telegraph=발동 전 고정 범위 깜빡임(회피 안내).</summary>
        public enum Mode { Strike, Telegraph }

        private const int RingTextureSize = 64;
        // 예고 경계선(반경 비율) — 짙은 안쪽선 0.90~0.95, 밝은 바깥선 0.95~1.0. 첫 시안, 사용자 판독 후 조정.
        private const float BOUNDARY_INNER = 0.90f;
        private const float BOUNDARY_SPLIT = 0.95f;
        private static readonly Color BoundaryDark = new Color(0.08f, 0.08f, 0.08f, 1f);
        private static Sprite ringSprite;
        private static Sprite boundarySprite;

        private SpriteRenderer sr;
        private float maxRadius;
        private float duration;
        private float timer;
        private Color baseColor;
        private Mode mode;

        /// <summary>
        /// 지정 위치에 반경 radius(월드 유닛)의 원형 링 이펙트를 띄운다.
        /// Strike: radius*0.4 → radius까지 확장하며 알파 페이드.
        /// Telegraph: 타격 범위(반경 radius)를 고정 표시하며 깜빡임 → 회피 안내.
        /// 만든 이펙트를 돌려준다 — 호출자가 부모를 옮기거나 일찍 치울 때 쓴다(추가 소환 예고). 무시해도 된다.
        /// </summary>
        public static BossAreaEffect Spawn(Vector3 position, float radius, Color color, float duration = 0.35f, Mode mode = Mode.Strike)
        {
            var go = new GameObject("BossAreaEffect");
            go.transform.position = position;
            var fx = go.AddComponent<BossAreaEffect>();
            fx.Init(radius, color, duration, mode);
            return fx;
        }

        private void Init(float radius, Color color, float dur, Mode effectMode)
        {
            sr = gameObject.AddComponent<SpriteRenderer>();
            sr.sprite = GetRingSprite();
            sr.sortingOrder = 5; // 적/플레이어보다 위에 보이도록
            maxRadius = Mathf.Max(0.1f, radius);
            baseColor = color;
            duration = Mathf.Max(0.05f, dur);
            mode = effectMode;
            timer = 0f;
            if (mode == Mode.Telegraph) CreateBoundary();
            Apply(0f);
        }

        /// <summary>
        /// Telegraph 전용 무채색 이중 경계선. 색상과 무관하게 발동 전 범위를 읽게 한다.
        /// 자식 로컬 변환은 항등 → 부모 localScale(지름)을 그대로 따라 반경이 일치한다.
        /// 색은 텍스처에 굽고 tint는 흰색 alpha=1 고정 — 부모 점멸(Apply)과 무관하게 유지된다.
        /// 자식이라 DiscardSilently/Destroy 때 함께 사라진다.
        /// </summary>
        private void CreateBoundary()
        {
            var child = new GameObject("Boundary");
            child.transform.SetParent(transform, false);
            var boundary = child.AddComponent<SpriteRenderer>();
            boundary.sprite = GetBoundarySprite();
            boundary.color = Color.white;
            boundary.sortingLayerID = sr.sortingLayerID;
            boundary.sortingOrder = sr.sortingOrder + 1; // 기존 링 바로 위
        }

        /// <summary>
        /// 런 경계 정리(<see cref="Abyss.Runtime.Run.RunTransientCleanup"/>) — 예고·타격 링을 남은 시간과 무관하게 즉시 치운다.
        /// timeScale 0 에서도 바로 꺼진다(파괴는 프레임 끝). 이미 치운 링이면 false.
        /// </summary>
        public bool DiscardSilently()
        {
            if (!gameObject.activeSelf) return false;
            gameObject.SetActive(false);
            Destroy(gameObject);
            return true;
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
            if (mode == Mode.Telegraph)
            {
                // 타격 범위를 고정 표시하고 깜빡임 + 후반으로 갈수록 진해져 임박을 알린다.
                transform.localScale = new Vector3(maxRadius * 2f, maxRadius * 2f, 1f);
                Color tc = baseColor;
                float blink = 0.4f + 0.4f * Mathf.Abs(Mathf.Sin(t * Mathf.PI * 4f));
                tc.a = blink * Mathf.Lerp(0.5f, 1f, t);
                sr.color = tc;
                return;
            }

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

        /// <summary>
        /// 예고 경계선 스프라이트를 1회 생성해 캐시. 링 스프라이트와 같은 크기·PPU·pivot이라 같은 지름에 겹친다.
        /// 0.90~0.95는 짙은 회색, 0.95~1.0은 흰색, 나머지는 투명 — 기존 링 채움(0.7~1.0) 안쪽에만 그린다.
        /// </summary>
        private static Sprite GetBoundarySprite()
        {
            if (boundarySprite != null) return boundarySprite;

            int size = RingTextureSize;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            float center = (size - 1) * 0.5f;
            float outer = center;
            float split = center * BOUNDARY_SPLIT;
            float inner = center * BOUNDARY_INNER;
            var clear = new Color(1f, 1f, 1f, 0f);
            var pixels = new Color[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Mathf.Sqrt((x - center) * (x - center) + (y - center) * (y - center));
                    Color pixel = clear;
                    if (dist <= outer && dist >= split) pixel = Color.white;
                    else if (dist < split && dist >= inner) pixel = BoundaryDark;
                    pixels[y * size + x] = pixel;
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();

            boundarySprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            return boundarySprite;
        }
    }
}
