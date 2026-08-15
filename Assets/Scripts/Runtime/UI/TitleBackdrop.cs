using UnityEngine;
using UnityEngine.UI;

namespace Abyss.Runtime.UI
{
    /// <summary>
    /// 타이틀 화면 협곡 배경 연출. <b>양쪽에 절벽을 끼고 가운데 길을 걸어 들어가는</b>
    /// 회랑이다.
    ///
    /// 좌우 절벽을 <b>깊이가 다른 조각(slab)</b> 여러 장으로 나눠(<c>title_cliff_*</c>),
    /// 각자 소실점에서 생겨나 커지며 바깥으로 흘러 카메라를 지나가게 한다.
    ///
    /// 📌 <b>협곡 단면을 통째로 그린 '관문' 판은 폐기했다.</b> 판은 한 깊이에 놓인 평면이라
    /// 확대돼도 표면이 균일하게 커질 뿐 <b>표면 자체가 물러나지 않는다</b> — 결과가
    /// "아치 터널"로 읽히고 도프 줌과 구별되지 않았다. 절벽은 면이 z축으로 물러나야 하고,
    /// 그건 깊이에 흩어진 조각으로만 만들어진다.
    ///
    /// <b>배율은 등비로 키운다</b>(로그 선형 보간). 깊이를 등간격으로 두고 등속으로 다가오게
    /// 하면 물리적으로는 맞지만, 인접 조각의 간격 비가 계속 변해 어떤 순간에는 조각 사이가
    /// 벌어져 벽에 틈이 생긴다. 등비로 두면 인접 배율비가 항상 일정해 겹침 조건을 한 번만
    /// 만족시키면 된다.
    ///
    /// 조각 하나의 자리(배율 s):
    /// <code>
    /// 밑동 안쪽 모서리 = 소실점 + ( ±WALL_SIDE·s , -WALL_DROP·s )
    /// </code>
    /// 즉 <b>피벗이 길 가장자리 광선 위를 미끄러진다</b>. 배율에 비례해 바깥·아래로
    /// 흐르므로 벽면이 스쳐 지나가고, 그게 "내가 걷는다"의 직접적인 근거다.
    ///
    /// ⚠️ <see cref="CLIFF_PAIRS"/>·<see cref="SCALE_MIN"/>·<see cref="SCALE_MAX"/>는
    /// 조각 폭·굽이 진폭과 묶여 있다. 이웃 조각의 밑동이 이어지려면
    /// <c>CLIFF_REACH ≥ WALL_SIDE·(r-1) + 굽이 어긋남</c> 이어야 한다
    /// (r = (MAX/MIN)^(1/PAIRS)). 값을 바꾸려면 <c>Tools/PixelArt/title_bg_config.py</c>와
    /// <c>TitleBackdrop.Path.cs</c>의 진폭을 함께 봐야 한다.
    ///
    /// 걷는 감각을 만드는 나머지 절반(굽이·시선·걸음 흔들림)은 <c>TitleBackdrop.Path.cs</c>,
    /// 소실점에서 흘러나오는 재는 <c>TitleBackdrop.Motes.cs</c>에 있다.
    ///
    /// <b>모든 시간 계산은 <see cref="Time.unscaledDeltaTime"/></b> — 타이틀은 timeScale이 1이지만
    /// 정지 위에서 도는 다른 연출 패널들과 규약을 맞춘다.
    /// </summary>
    public sealed partial class TitleBackdrop : MonoBehaviour
    {
        [Header("깊이 순환 레이어")]
        /// <summary>좌우 번갈아 담긴다 — 짝수 인덱스가 왼쪽, 홀수가 오른쪽.</summary>
        [SerializeField] private RawImage[] cliffs;
        [SerializeField] private RawImage[] roadMarks;

        [Header("고정 레이어")]
        [SerializeField] private RawImage fog;
        [SerializeField] private RectTransform driftRoot;
        [SerializeField] private RectTransform cliffRoot;
        [SerializeField] private RectTransform horizon;

        [Header("연출 대상")]
        [SerializeField] private Graphic logo;
        [SerializeField] private RectTransform moteRoot;
        [SerializeField] private Texture2D moteTexture;

        /// <summary>조각 한 장이 가장 먼 곳에서 카메라를 지나칠 때까지 걸리는 시간(초).</summary>
        private const float CYCLE = 19f;
        private const float SCALE_MIN = 0.16f;
        private const float SCALE_MAX = 2.6f;
        /// <summary>한쪽 벽에 놓이는 조각 수. 실제 이미지 수는 이것의 2배다.</summary>
        private const int CLIFF_PAIRS = 8;

        // 절벽 기하 — Tools/PixelArt/title_bg_config.py의 같은 이름 상수와 짝이 맞아야 한다.
        /// <summary>배율 1에서 벽 밑동이 소실점 아래로 내려간 거리.</summary>
        private const float WALL_DROP = 240f;
        /// <summary>배율 1에서의 좌우 벽 위치. 생성기의 ROAD_SPREAD × WALL_DROP 이다.</summary>
        private const float WALL_SIDE = 318.83f;

        // 가장 먼 조각은 투명하게 나타난다 — 아니면 작은 조각이 소실점에 툭 생긴다.
        private const float FADE_IN_END = 0.28f;
        // ⚠️ 조각은 화면 밖으로 나가지 않는다(안쪽 모서리가 계속 화면 안에 있다). 그래서
        //    되돌아갈 때 반드시 페이드로 지워야 하고, 그 시점에는 <b>바로 앞 조각이 이미
        //    화면 가장자리를 덮고 있어야</b> 한다. 배율 1.95면 다음 조각이 s/r ≒ 1.38 로
        //    이미 가장자리 너머까지 뻗는다.
        private const float FADE_OUT_START = 1.95f;

        /// <summary>화면에서 소실점이 놓이는 세로 비율. 생성기의 VANISH_RATIO와 같아야 한다.</summary>
        private const float VANISH_RATIO = 0.545f;

        // 배율 ↔ 로그 깊이 변환. 경로(Path.cs)와 배율 보간이 같은 값을 봐야 한다.
        private static readonly float LogMin = Mathf.Log(SCALE_MIN);
        private static readonly float LogMax = Mathf.Log(SCALE_MAX);
        private static readonly float LogSpan = LogMax - LogMin;

        // 대기 원근 — 가까울수록 검은 실루엣, 멀수록 하늘빛에 가깝다.
        private static readonly Color TintNear = new Color(0.17f, 0.17f, 0.23f);
        private static readonly Color TintFar = new Color(0.92f, 0.90f, 1f);

        // 길바닥은 벽만큼 어두워지면 안 된다 — 가까운 균열이 검게 묻히면 지면 흐름이 사라지고,
        // 발밑이 정지한 것과 같아진다(그게 애초에 이 레이어를 넣은 이유다).
        private static readonly Color MarkNear = new Color(0.72f, 0.69f, 0.82f);
        private static readonly Color MarkFar = new Color(0.92f, 0.90f, 1f);

        // 길바닥은 절벽 조각 사이사이(위상 0.5)에 끼워 흐름이 끊기지 않게 하고, 절벽보다
        // 늦게 사라진다 — 가장 가까운 구간이 곧 발밑이라 여기서 지워지면 아무 의미가 없다.
        private static readonly RingStyle MarkStyle =
            new RingStyle(MarkNear, MarkFar, 0.24f, 2.15f, 0.9f, 0.5f);

        // 보행 흔들림 위에 겹치는 아주 느린 표류(Path.cs의 Walk가 쓴다).
        private const float DRIFT_X = 6f;
        private const float DRIFT_Y = 3.5f;
        private const float DRIFT_PERIOD_X = 17.3f;
        private const float DRIFT_PERIOD_Y = 11.7f;

        // ⚠️ 안개는 화면 전체를 덮는 한 장이라 <b>근경 절벽까지 뿌옇게 만든다.</b> 관문판에서는
        //    화면 가운데가 먼 곳이라 짙어도 됐지만, 회랑에서는 좌우가 코앞이라 옅어야 한다.
        private const float FOG_ALPHA_MIN = 0.32f;
        private const float FOG_ALPHA_MAX = 0.58f;
        private const float FOG_PERIOD = 14.9f;

        private const float LOGO_FADE_IN = 1.4f;
        private const float LOGO_BREATH_MIN = 0.88f;
        private const float LOGO_BREATH_PERIOD = 5.2f;

        /// <summary>깊이 순환 레이어 한 벌의 색·페이드 설정.</summary>
        private readonly struct RingStyle
        {
            public readonly Color Near;
            public readonly Color Far;
            public readonly float FadeInEnd;
            public readonly float FadeOutStart;
            public readonly float Alpha;
            /// <summary>순환 내 위상 어긋남(0~1). 서로 다른 벌을 사이사이에 끼울 때 쓴다.</summary>
            public readonly float Phase;

            public RingStyle(Color near, Color far, float fadeInEnd, float fadeOutStart,
                float alpha, float phase)
            {
                Near = near;
                Far = far;
                FadeInEnd = fadeInEnd;
                FadeOutStart = fadeOutStart;
                Alpha = alpha;
                Phase = phase;
            }
        }

        private float elapsed;
        private float[] cliffProgress;
        private float[] markProgress;
        private Vector2 driftOrigin;
        private Vector2 horizonOrigin;
        private Vector2 vanishOffset;
        private Color logoBaseColor;

        private void Awake()
        {
            cliffProgress = new float[cliffs != null ? cliffs.Length : 0];
            markProgress = new float[roadMarks != null ? roadMarks.Length : 0];
            if (driftRoot != null) driftOrigin = driftRoot.anchoredPosition;
            if (horizon != null) horizonOrigin = horizon.anchoredPosition;
            if (logo != null)
            {
                logoBaseColor = logo.color;
                var c = logoBaseColor;
                c.a = 0f;
                logo.color = c;
            }
            SpawnMotes();
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            elapsed += dt;

            // 걸음이 먼저다 — 절벽·재가 이번 프레임의 소실점을 보고 자리를 잡는다.
            UpdateVanishPoint();
            Walk();
            AdvanceCliffs();
            AdvanceRing(roadMarks, markProgress, MarkStyle);
            PulseFog();
            BreatheLogo();
            UpdateMotes();
        }

        // ───────────────────────── 절벽 ─────────────────────────

        /// <summary>소실점 위치. 해상도가 바뀌어도 따라가도록 매 프레임 다시 잡는다.</summary>
        private void UpdateVanishPoint()
        {
            var reference = cliffRoot != null ? cliffRoot : driftRoot;
            if (reference == null) return;
            vanishOffset = new Vector2(0f, (0.5f - VANISH_RATIO) * reference.rect.height);
        }

        /// <summary>
        /// 좌우 절벽 조각을 진행시킨다. 배열은 좌·우가 번갈아 담겨 있고, 두 장이 한 깊이를
        /// 이룬다 — 같은 깊이의 좌우가 함께 흘러야 회랑이 대칭으로 열린다.
        /// </summary>
        private void AdvanceCliffs()
        {
            if (cliffs == null || cliffs.Length < 2) return;
            int pairs = cliffs.Length / 2;

            for (int i = 0; i < cliffs.Length; i++)
            {
                var image = cliffs[i];
                if (image == null) continue;

                int depth = i / 2;
                // 왼쪽 벽은 오른쪽 텍스처를 뒤집어 쓴다 — 텍스처를 두 벌 굽지 않는다.
                float side = (i % 2 == 0) ? -1f : 1f;

                float progress = Mathf.Repeat(elapsed / CYCLE + (float)depth / pairs, 1f);

                // 한 바퀴 돌아 다시 가장 먼 곳으로 갔다 — 형제 순서에서도 맨 뒤로 보낸다.
                // uGUI는 계층 순서가 곧 렌더 순서라, 이걸 하지 않으면 먼 조각이 가까운 조각을 덮는다.
                // 순서 갱신을 리셋 순간에만 하므로 매 프레임 정렬할 필요가 없다.
                if (progress < cliffProgress[i]) image.rectTransform.SetSiblingIndex(0);
                cliffProgress[i] = progress;

                float scale = Mathf.Exp(Mathf.Lerp(LogMin, LogMax, progress));
                var rect = image.rectTransform;
                rect.localScale = new Vector3(scale * side, scale, 1f);

                // 밑동 안쪽 모서리가 길 가장자리 광선을 타고 바깥·아래로 흐른다.
                // 여기에 굽이(LateralOffset)를 더하면 회랑 전체가 경로를 따라 휜다.
                rect.anchoredPosition = vanishOffset + new Vector2(
                    LateralOffset(scale) + side * WALL_SIDE * scale,
                    -WALL_DROP * scale);

                var color = Color.Lerp(TintNear, TintFar, 1f - progress);
                color.a = Mathf.InverseLerp(SCALE_MIN, FADE_IN_END, scale)
                          * (1f - Mathf.InverseLerp(FADE_OUT_START, SCALE_MAX, scale));
                image.color = color;
            }
        }

        /// <summary>
        /// 깊이 순환 레이어 한 벌을 진행시킨다(현재는 길바닥 무늬). 절벽과 캔버스는 다르지만
        /// 피벗이 소실점에 정렬돼 있어 같은 배율 스케줄을 그대로 쓴다.
        /// </summary>
        private void AdvanceRing(RawImage[] ring, float[] last, RingStyle style)
        {
            if (ring == null || ring.Length == 0) return;

            for (int i = 0; i < ring.Length; i++)
            {
                var image = ring[i];
                if (image == null) continue;

                float progress = Mathf.Repeat(
                    elapsed / CYCLE + (i + style.Phase) / ring.Length, 1f);

                if (progress < last[i]) image.rectTransform.SetSiblingIndex(0);
                last[i] = progress;

                float scale = Mathf.Exp(Mathf.Lerp(LogMin, LogMax, progress));
                var rect = image.rectTransform;
                rect.localScale = new Vector3(scale, scale, 1f);
                // 굽이를 따라 좌우로 민다 — 이 한 줄이 도프 줌과 도보를 가른다.
                rect.anchoredPosition = vanishOffset + new Vector2(LateralOffset(scale), 0f);

                var color = Color.Lerp(style.Near, style.Far, 1f - progress);
                color.a = style.Alpha
                          * Mathf.InverseLerp(SCALE_MIN, style.FadeInEnd, scale)
                          * (1f - Mathf.InverseLerp(style.FadeOutStart, SCALE_MAX, scale));
                image.color = color;
            }
        }

        // ───────────────────────── 부수 연출 ─────────────────────────

        private void PulseFog()
        {
            if (fog == null) return;
            float t = (Mathf.Sin(elapsed * Mathf.PI * 2f / FOG_PERIOD) + 1f) * 0.5f;
            var c = fog.color;
            c.a = Mathf.Lerp(FOG_ALPHA_MIN, FOG_ALPHA_MAX, t);
            fog.color = c;
        }

        private void BreatheLogo()
        {
            if (logo == null) return;

            var c = logoBaseColor;
            if (elapsed < LOGO_FADE_IN)
            {
                // 페이드 인 구간에는 숨쉬기를 섞지 않는다 — 두 곡선이 겹치면 등장이 흔들려 보인다.
                float u = elapsed / LOGO_FADE_IN;
                c.a = logoBaseColor.a * (u * u * (3f - 2f * u));
            }
            else
            {
                float t = (Mathf.Sin((elapsed - LOGO_FADE_IN) * Mathf.PI * 2f / LOGO_BREATH_PERIOD) + 1f) * 0.5f;
                c.a = logoBaseColor.a * Mathf.Lerp(LOGO_BREATH_MIN, 1f, t);
            }
            logo.color = c;
        }
    }
}
