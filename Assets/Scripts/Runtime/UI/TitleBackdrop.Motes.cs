using UnityEngine;
using UnityEngine.UI;

namespace Abyss.Runtime.UI
{
    /// <summary>
    /// 타이틀 배경의 재 입자. <b>소실점에서 생겨나 바깥으로 흘러나온다</b> — 카메라가 앞으로
    /// 나아갈 때 지나치는 먼지의 광학 흐름이고, z축 전진감을 가장 직접적으로 만드는 요소다.
    ///
    /// 반경은 배율과 같은 이유로 <b>등비</b>로 키운다. 등속으로 밀면 소실점 근처에서만
    /// 오래 머물다가 바깥에서 순식간에 사라져 흐름이 끊겨 보인다.
    ///
    /// 파티클 시스템이 아니라 UI 이미지를 직접 도는 이유: 배경이 ScreenSpaceOverlay 캔버스라
    /// ParticleSystem을 얹으려면 전용 카메라나 렌더 순서 조정이 따라온다. 입자 수가 수십 개
    /// 규모라 갱신 비용도 문제가 되지 않는다.
    /// </summary>
    public sealed partial class TitleBackdrop
    {
        private const int MOTE_COUNT = 30;

        private const float MOTE_RADIUS_MIN = 18f;    // 소실점 부근
        private const float MOTE_RADIUS_MAX = 1500f;  // 화면 밖
        private const float MOTE_CYCLE_MIN = 7f;      // 한 입자가 끝까지 흐르는 시간(초)
        private const float MOTE_CYCLE_MAX = 15f;
        private const float MOTE_SIZE_MIN = 2f;
        private const float MOTE_SIZE_MAX = 9f;
        private const float MOTE_ALPHA_MAX = 0.42f;
        private const float MOTE_FADE_IN = 0.16f;     // 진행도 0~이 구간에서 나타난다
        private const float MOTE_FADE_OUT = 0.72f;    // 이 구간부터 사라진다

        // 위로 살짝 몰리게 한다 — 재는 떠오르므로 아래쪽으로만 흐르면 눈에 거슬린다.
        private const float MOTE_RISE = 0.22f;

        private static readonly Color MoteTint = new Color(0.84f, 0.78f, 1f);

        private struct Mote
        {
            public RectTransform rect;
            public Graphic graphic;
            public float angle;
            public float phase;
            public float cycle;
            public float sizeMax;
            public float alphaMax;
        }

        private Mote[] motes;

        private void SpawnMotes()
        {
            if (moteRoot == null || moteTexture == null) return;

            // 고정 시드 — 이상한 배치가 나왔을 때 재현이 되고 스크린샷 비교도 가능하다.
            var rng = new System.Random(4771);
            motes = new Mote[MOTE_COUNT];

            for (int i = 0; i < MOTE_COUNT; i++)
            {
                var go = new GameObject($"Mote{i}", typeof(RectTransform));
                go.transform.SetParent(moteRoot, false);

                var rect = (RectTransform)go.transform;
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);

                var image = go.AddComponent<RawImage>();
                image.texture = moteTexture;
                image.raycastTarget = false;

                float depth = Range(rng, 0f, 1f);
                motes[i] = new Mote
                {
                    rect = rect,
                    graphic = image,
                    angle = Range(rng, 0f, Mathf.PI * 2f),
                    phase = Range(rng, 0f, 1f),
                    cycle = Mathf.Lerp(MOTE_CYCLE_MIN, MOTE_CYCLE_MAX, depth),
                    // 느린(=먼) 입자일수록 작고 흐리다. 크기만 다르면 평면으로 보인다.
                    sizeMax = Mathf.Lerp(MOTE_SIZE_MAX, MOTE_SIZE_MIN, depth),
                    alphaMax = MOTE_ALPHA_MAX * Mathf.Lerp(1f, 0.45f, depth),
                };
            }
        }

        private void UpdateMotes()
        {
            if (motes == null) return;

            float ratio = MOTE_RADIUS_MAX / MOTE_RADIUS_MIN;

            for (int i = 0; i < motes.Length; i++)
            {
                ref var m = ref motes[i];
                if (m.rect == null) continue;

                float p = Mathf.Repeat(elapsed / m.cycle + m.phase, 1f);
                float radius = MOTE_RADIUS_MIN * Mathf.Pow(ratio, p);

                var dir = new Vector2(Mathf.Cos(m.angle), Mathf.Sin(m.angle) + MOTE_RISE * p);
                m.rect.anchoredPosition = vanishOffset + dir * radius;

                float size = Mathf.Lerp(1f, m.sizeMax, p);
                m.rect.sizeDelta = new Vector2(size, size);

                float fade = Mathf.InverseLerp(0f, MOTE_FADE_IN, p)
                             * (1f - Mathf.InverseLerp(MOTE_FADE_OUT, 1f, p));
                var c = MoteTint;
                c.a = m.alphaMax * fade;
                m.graphic.color = c;
            }
        }

        private static float Range(System.Random rng, float min, float max)
        {
            return min + (float)rng.NextDouble() * (max - min);
        }
    }
}
