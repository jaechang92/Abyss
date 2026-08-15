using UnityEngine;

namespace Abyss.Runtime.UI
{
    /// <summary>
    /// 협곡 경로(굽이)와 보행 카메라. <b>"저것이 다가온다"를 "내가 걸어간다"로 뒤집는 부분</b>이다.
    ///
    /// 관문을 소실점 한 점에서 등비로 키우기만 하면 광학적으로는 도프 줌과 구별되지 않는다.
    /// 자기운동으로 읽히려면 (a) 깊이마다 다른 좌우 어긋남(시차), (b) 진로를 따라 도는 시선,
    /// (c) 걸음 리듬의 흔들림 — 셋이 있어야 한다. 이 파셜이 그 셋을 만든다.
    ///
    /// <b>경로는 로그 깊이로 매개변수화한다.</b> 관문의 배율이 등비라 실제 깊이 z는 시간에
    /// 대해 지수적으로 줄어든다. 경로를 실거리 z로 매개변수화하면 한 관문의 '경로 위 위치'가
    /// 시간에 따라 흘러가 버려(월드가 굳지 않아) 굽이가 앞뒤로 미끄러진다. σ = -ln z 로 잡으면
    /// 관문마다 σ가 <b>상수</b>가 되고 카메라의 σ만 등속으로 증가한다 — 굽이가 공간에 박혀 있고
    /// 내가 그 위를 지나간다는 읽기가 이 성질에서 나온다. 실거리 기준으로는 등속이 아니지만
    /// 타이틀 배경에서 그 차이를 분간할 사람은 없다.
    ///
    /// 화면 좌우 어긋남은 원근 그대로 <c>(경로차 × 그 관문의 배율)</c>이다. 배율을 곱하는 것이
    /// 핵심 — 가까운 관문은 크게 휘둘리고 먼 관문은 거의 안 움직여, 시차가 공짜로 따라온다.
    ///
    /// ⚠️ <b>진폭은 절벽 조각의 폭에 묶여 있다.</b> 이웃한 두 조각의 어긋남 차가 커지면
    /// 조각 사이가 벌어져 벽 밑동에 틈이 생긴다. 상한은
    /// <c>CLIFF_REACH ≥ WALL_SIDE·(r-1) + 어긋남</c> 이고,
    /// 여기 진폭·주기를 키우려면 <c>Tools/PixelArt/title_bg_config.py</c>의
    /// <c>CLIFF_REACH</c>도 함께 봐야 한다.
    /// </summary>
    public sealed partial class TitleBackdrop
    {
        // ───────────────────────── 경로 ─────────────────────────

        // 긴 굽이 하나와 짧은 굽이 하나를 겹친다. 하나만 쓰면 좌우로 규칙적으로 흔들려
        // '길'이 아니라 '진자'로 읽힌다. 주기를 어긋나게 둬야 굽이 → 잠깐 곧은 길 →
        // 반대 굽이 하는 리듬이 생긴다.
        private const float PATH_AMP_LONG = 150f;
        private const float PATH_LEN_LONG = 6.2f;
        private const float PATH_AMP_SHORT = 42f;
        private const float PATH_LEN_SHORT = 2.55f;
        private const float PATH_PHASE_SHORT = 1.37f;

        /// <summary>진로를 따라 도는 시선의 세기. 화면 전체를 함께 미는 양(px/기울기).</summary>
        private const float YAW_GAIN = 0.09f;

        /// <summary>시선이 굽이를 따라가는 지연(σ). 즉시 따라가면 '돌고 있다'가 안 읽힌다.</summary>
        private const float YAW_LAG = 0.55f;

        // ───────────────────────── 보행 ─────────────────────────

        /// <summary>한 걸음의 주기(초). 느리고 무거운 걸음 — 초당 1.16보.</summary>
        private const float STEP_PERIOD = 0.86f;

        // 상하는 한 걸음마다, 좌우 흔들림과 기울기는 두 걸음(한 보폭)마다다.
        // 이 둘을 같은 주기로 묶으면 걷기가 아니라 부유로 읽힌다.
        private const float BOB_Y = 6f;
        private const float SWAY_X = 4.5f;
        private const float ROLL_DEG = 0.3f;

        /// <summary>|sin| 의 평균. 빼주지 않으면 화면이 통째로 아래로 눌린 채 고정된다.</summary>
        private const float ABS_SIN_MEAN = 0.6366f;

        /// <summary>먼 산이 시점 <b>평행이동</b>에 반응하는 비율. 회전(요)에는 그대로 따라간다.</summary>
        private const float HORIZON_PARALLAX = 0.12f;

        /// <summary>카메라가 지금 서 있는 경로 위 지점(로그 깊이).</summary>
        private float SigmaCam => elapsed / CYCLE * LogSpan;

        /// <summary>경로의 좌우 위치(px, 배율 1 기준).</summary>
        private static float PathX(float sigma)
        {
            return Mathf.Sin(sigma * Mathf.PI * 2f / PATH_LEN_LONG) * PATH_AMP_LONG
                   + Mathf.Sin(sigma * Mathf.PI * 2f / PATH_LEN_SHORT + PATH_PHASE_SHORT) * PATH_AMP_SHORT;
        }

        /// <summary>경로의 기울기 = 그 지점에서 몸이 향하는 방향.</summary>
        private static float PathSlope(float sigma)
        {
            return Mathf.Cos(sigma * Mathf.PI * 2f / PATH_LEN_LONG)
                       * PATH_AMP_LONG * Mathf.PI * 2f / PATH_LEN_LONG
                   + Mathf.Cos(sigma * Mathf.PI * 2f / PATH_LEN_SHORT + PATH_PHASE_SHORT)
                       * PATH_AMP_SHORT * Mathf.PI * 2f / PATH_LEN_SHORT;
        }

        /// <summary>
        /// 배율이 <paramref name="scale"/>인 깊이 레이어의 좌우 어긋남(px).
        /// 그 레이어는 카메라보다 <c>LogMax - ln(scale)</c> 만큼 앞선 경로 지점에 서 있다.
        /// </summary>
        private float LateralOffset(float scale)
        {
            float sigma = SigmaCam;
            float ahead = LogMax - Mathf.Log(scale);
            return (PathX(sigma + ahead) - PathX(sigma)) * scale;
        }

        /// <summary>
        /// 걸음 흔들림 + 진로 추종. 시점 자체의 움직임이므로 <see cref="driftRoot"/> 통째로 건다 —
        /// 하늘·길·수평선·관문·재가 함께 밀려야 '카메라가 움직였다'로 읽힌다.
        /// (안개와 비네트는 드리프트 밖이라 화면에 붙어 있다.)
        /// </summary>
        private void Walk()
        {
            if (driftRoot == null) return;

            float step = elapsed * Mathf.PI * 2f / STEP_PERIOD;
            // 착지마다 아래로 내려앉는다 — |sin|이라 한 걸음에 한 번. 평균값을 빼 중심을 맞춘다.
            float bobY = (ABS_SIN_MEAN - Mathf.Abs(Mathf.Sin(step))) * BOB_Y;
            float swayX = Mathf.Sin(step * 0.5f) * SWAY_X;
            float roll = Mathf.Sin(step * 0.5f + 0.6f) * ROLL_DEG;

            // 느린 표류를 겹쳐 보행 주기가 기계적으로 반복되는 것을 감춘다.
            swayX += Mathf.Sin(elapsed * Mathf.PI * 2f / DRIFT_PERIOD_X) * DRIFT_X;
            bobY += Mathf.Sin(elapsed * Mathf.PI * 2f / DRIFT_PERIOD_Y) * DRIFT_Y;

            // 몸이 향한 쪽으로 시선이 돌면 화면은 반대로 밀린다.
            float yaw = -PathSlope(SigmaCam - YAW_LAG) * YAW_GAIN;

            driftRoot.anchoredPosition = driftOrigin + new Vector2(swayX + yaw, bobY);
            driftRoot.localRotation = Quaternion.Euler(0f, 0f, roll);

            // 먼 산은 평행이동에는 거의 반응하지 않는다(회전은 원근상 그대로 따라간다).
            if (horizon != null)
            {
                horizon.anchoredPosition = horizonOrigin
                                           - new Vector2(swayX, bobY) * (1f - HORIZON_PARALLAX);
            }
        }
    }
}
