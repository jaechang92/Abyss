using UnityEngine;

namespace Abyss.Runtime.Enemy
{
    /// <summary>
    /// 첫 보스(BossAbyssKeeper) 부채꼴 탄막의 <b>예고 → 발사</b> 일정표. MonoBehaviour 가 아니다 —
    /// <c>Time.time</c> 을 인자로 받아 EditMode 에서 시간축을 통째로 돌려 볼 수 있게 했다(<see cref="EnemyTimedState"/> 와 같은 이유).
    ///
    /// 규칙(D2 §2-2 · 총괄 C2 결정 3):
    /// <list type="bullet">
    /// <item>예고 시간 P1 0.6 · P2 0.5 · P3 0.4초 — <b>주기 안에</b> 넣는다. 발사 간격은 옛 볼리와 같은 <c>interval / phase</c>.</item>
    /// <item>근접 예비동작 중에는 예고를 <b>시작하지 않는다</b>(미룬다). 예고 중에는 근접이 못 들어온다 —
    /// 그쪽 판정은 <see cref="IsTelegraphing"/> 을 보는 <c>AbyssKeeperBoss.CanBeginAttack</c> 가 한다.</item>
    /// <item>예고를 시작했으면 <b>반드시 쏜다</b>(사거리 이탈로 취소하지 않는다) — 예고했는데 안 오면 예고를 믿지 않게 된다.</item>
    /// </list>
    /// </summary>
    public sealed class AbyssKeeperVolleySchedule
    {
        /// <summary>한 번의 판정 결과. 호출한 쪽이 표현(틴트·소리)과 발사를 맡는다.</summary>
        public enum Step { None, BeginTelegraph, Fire }

        // 페이즈 1·2·3 예고 시간(초). 검증용 값(총괄 결정 3) — 사람 플레이 승인 전이다.
        private static readonly float[] TelegraphByPhase = { 0.6f, 0.5f, 0.4f };

        private float lastFireTime = float.NegativeInfinity;
        private float fireTime;
        private bool isTelegraphing;

        /// <summary>탄막 예고 중인가. 참이면 근접 공격 상태 진입을 막는다.</summary>
        public bool IsTelegraphing => isTelegraphing;

        /// <summary>진행 중인 예고가 끝나는(= 발사하는) 시각. 예고 중이 아니면 의미 없다.</summary>
        public float FireTime => fireTime;

        /// <summary>마지막으로 쏜 시각. 아직 안 쐈으면 음의 무한대.</summary>
        public float LastFireTime => lastFireTime;

        /// <summary>
        /// 페이즈의 예고 시간. 범위 밖 페이즈는 가까운 끝으로 붙인다.
        /// 🔑 주기보다 길면 주기로 자른다 — 예고가 주기를 넘으면 「주기 불변」이 깨진다.
        /// </summary>
        public static float TelegraphTime(int phase, float interval)
        {
            int index = Mathf.Clamp(phase, 1, TelegraphByPhase.Length) - 1;
            return Mathf.Clamp(TelegraphByPhase[index], 0f, Mathf.Max(0f, interval));
        }

        /// <summary>
        /// 매 프레임 1회. <paramref name="isTargetInRange"/> 는 감지 범위 안인가(옛 볼리와 같은 조건),
        /// <paramref name="isMeleeWindupActive"/> 는 근접 예비동작 중인가.
        /// </summary>
        public Step Tick(float now, int phase, float baseInterval, bool isTargetInRange, bool isMeleeWindupActive)
        {
            if (isTelegraphing)
            {
                if (now < fireTime) return Step.None;

                isTelegraphing = false;
                lastFireTime = now;
                return Step.Fire;
            }

            if (!isTargetInRange || isMeleeWindupActive) return Step.None;

            float interval = baseInterval / Mathf.Max(1, phase);
            float telegraph = TelegraphTime(phase, interval);
            if (now < lastFireTime + interval - telegraph) return Step.None;

            isTelegraphing = true;
            fireTime = now + telegraph;
            return Step.BeginTelegraph;
        }

        /// <summary>진행 중 예고를 버린다(사망 등). 다음 예고는 주기 규칙대로 다시 잡힌다.</summary>
        public void Cancel()
        {
            isTelegraphing = false;
        }
    }
}
