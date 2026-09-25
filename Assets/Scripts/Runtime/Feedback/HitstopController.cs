using Singleton_Core;
using UnityEngine;

namespace Abyss.Runtime.Feedback
{
    /// <summary>
    /// Time.timeScale 기반 히트스탑 제어. Critic S5 절충 — PrimeTween 미사용.
    /// 드래프트·결과 패널 등 이미 timeScale=0인 상태에서는 skip (충돌 방지).
    /// </summary>
    public sealed class HitstopController : SingletonManager<HitstopController>
    {
        private float restoreScale;
        private float restoreAt;
        private bool isActive;

        // 슬로모션(보스 처치 등). 히트스탑과 같은 규칙 — 정지 중이면 걸지 않고, 원복은 자기가 건 값일 때만.
        private float slowScale = 1f;
        private float slowUntil;
        private float slowRestoreScale = 1f;
        private bool isSlowRequested;   // 히트스탑이 끝난 뒤 이어서 걸 슬로
        private bool isSlowActive;      // 지금 timeScale을 슬로가 쥐고 있다

        /// <summary>
        /// durationSeconds 만큼 Time.timeScale=0. 현재 이미 pause면 무시.
        /// 다중 호출 시 더 긴 지속시간이 이김.
        /// </summary>
        public void Trigger(float durationSeconds)
        {
            if (durationSeconds <= 0f) return;

            if (isActive)
            {
                restoreAt = Mathf.Max(restoreAt, Time.realtimeSinceStartup + durationSeconds);
                return;
            }

            if (Time.timeScale < 0.01f) return;

            restoreScale = Time.timeScale;
            Time.timeScale = 0f;
            restoreAt = Time.realtimeSinceStartup + durationSeconds;
            isActive = true;
        }

        public void TriggerMs(int milliseconds) => Trigger(milliseconds / 1000f);

        /// <summary>
        /// 실시간 <paramref name="durationSeconds"/> 동안 Time.timeScale=<paramref name="scale"/>(0~1 사이).
        /// 막타 히트스탑이 걸려 있으면 그것이 끝난 뒤 이어서 건다 — 둘이 겹쳐 정지가 길어지지 않게.
        /// 정지(드래프트·결과 등, timeScale 0) 중이면 무시한다. <see cref="CancelActive"/>가 함께 취소한다.
        /// </summary>
        public void TriggerSlow(float scale, float durationSeconds)
        {
            if (durationSeconds <= 0f || scale <= 0f || scale >= 1f) return;

            slowScale = scale;
            if (isActive)
            {
                slowUntil = restoreAt + durationSeconds;
                isSlowRequested = true;
                return;
            }
            if (Time.timeScale < 0.01f) return;

            if (!isSlowActive) slowRestoreScale = Time.timeScale;
            slowUntil = Time.realtimeSinceStartup + durationSeconds;
            Time.timeScale = slowScale;
            isSlowActive = true;
        }

        /// <summary>
        /// 진행 중인 히트스탑·슬로를 원복 없이 취소한다. 게임플로우 정지(드래프트/결과)처럼 외부가 timeScale을
        /// 권위적으로 제어할 때, 히트스탑의 지연 원복(restoreScale=1)이 그 정지를 덮어써 적이 다시 움직이는
        /// 문제를 막는다. timeScale 자체는 건드리지 않고 원복 예약만 해제한다.
        /// </summary>
        public void CancelActive()
        {
            isActive = false;
            isSlowRequested = false;
            isSlowActive = false;
        }

        private void Update()
        {
            float now = Time.realtimeSinceStartup;

            if (isActive && now >= restoreAt)
            {
                // 히트스탑 창 동안 외부(치트 배속 등)가 timeScale을 0이 아닌 값으로 바꿨다면
                // 그 값을 존중하고 덮어쓰지 않는다. 히트스탑이 세팅한 0일 때만 원복.
                if (Mathf.Approximately(Time.timeScale, 0f))
                {
                    float restored = restoreScale > 0f ? restoreScale : 1f;
                    if (isSlowRequested && now < slowUntil)
                    {
                        // 막타 정지 → 이어서 슬로. 슬로가 끝나면 히트스탑 이전 배율로 돌아간다.
                        slowRestoreScale = restored;
                        Time.timeScale = slowScale;
                        isSlowActive = true;
                    }
                    else
                    {
                        Time.timeScale = restored;
                    }
                }
                isActive = false;
                isSlowRequested = false;
            }

            if (isSlowActive && !isActive && now >= slowUntil)
            {
                // 슬로 중에 외부가 배율을 바꿨다면(정지·치트) 그 값을 존중한다.
                if (Mathf.Approximately(Time.timeScale, slowScale)) Time.timeScale = slowRestoreScale;
                isSlowActive = false;
            }
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            // 히트스탑 활성 중 파괴 시 timeScale=0 고착 방지.
            if (isActive && Mathf.Approximately(Time.timeScale, 0f))
            {
                Time.timeScale = restoreScale > 0f ? restoreScale : 1f;
            }
            else if (isSlowActive && Mathf.Approximately(Time.timeScale, slowScale))
            {
                Time.timeScale = slowRestoreScale;
            }
        }
    }
}
