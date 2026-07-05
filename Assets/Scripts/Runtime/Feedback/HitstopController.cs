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

        private void Update()
        {
            if (!isActive) return;
            if (Time.realtimeSinceStartup < restoreAt) return;

            // 히트스탑 창 동안 외부(치트 배속 등)가 timeScale을 0이 아닌 값으로 바꿨다면
            // 그 값을 존중하고 덮어쓰지 않는다. 히트스탑이 세팅한 0일 때만 원복.
            if (Mathf.Approximately(Time.timeScale, 0f))
            {
                Time.timeScale = restoreScale > 0f ? restoreScale : 1f;
            }
            isActive = false;
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            // 히트스탑 활성 중 파괴 시 timeScale=0 고착 방지.
            if (isActive && Mathf.Approximately(Time.timeScale, 0f))
            {
                Time.timeScale = restoreScale > 0f ? restoreScale : 1f;
            }
        }
    }
}
