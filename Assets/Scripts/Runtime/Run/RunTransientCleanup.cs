using System;
using Abyss.Runtime.Combat;
using Abyss.Runtime.Feedback;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Abyss.Runtime.Run
{
    /// <summary>
    /// 런 경계의 일시 전투 객체 정리 — 날아가던 탄(직진·곡사)과 보스 예고·타격 링을 <b>조용히</b> 치운다.
    ///
    /// 🔑 <b>왜 필요한가</b>: 탄은 PoolManager(DontDestroyOnLoad) 풀 아래에 살아서 씬이 바뀌어도 함께 사라지지 않는다.
    /// 사망 결과 화면은 timeScale 0 이라 수명도 흐르지 않는다 — 그래서 로비·다음 런에 지난 런의 탄이 보였다.
    /// PoolManager.ReleaseAllPools 는 자리표시자라 쓰지 않고, 모든 풀을 비우지도 않는다(전투 탄과 링만).
    ///
    /// <b>계약</b>: 피해·폭발·이펙트·이벤트·난수·요약·메타를 건드리지 않는다. timeScale 0 에서도 즉시 끈다.
    /// 여러 번 불러도 안전하다(이미 회수된 것은 비활성이라 다시 잡히지 않는다). PoolManager 를 새로 만들지 않는다.
    /// 드문 경계(런 시작·종료·포기, 런 씬을 낀 전환 확정)에서만 부른다 — 프레임마다 부르지 않는다.
    /// </summary>
    public static class RunTransientCleanup
    {
        /// <summary>
        /// 활성 상태의 <see cref="Projectile"/>(양 진영)·<see cref="ArcProjectile"/>·<see cref="BossAreaEffect"/>를 즉시 회수한다.
        /// 풀에서 나온 것은 풀로, 아닌 것은 끈 뒤 파괴한다.
        /// </summary>
        /// <returns>이번 호출에서 치운 개수(테스트·로그용).</returns>
        public static int Clear()
        {
            int count = 0;

            // 탄을 링보다 먼저 치운다 — 순서는 결과에 영향이 없지만(회수는 아무것도 띄우지 않는다) 읽기 쉽게 원인부터.
            foreach (var projectile in Object.FindObjectsByType<Projectile>())
            {
                if (TryDiscard(projectile, p => p.DiscardSilently())) count++;
            }

            foreach (var shell in Object.FindObjectsByType<ArcProjectile>())
            {
                if (TryDiscard(shell, s => s.DiscardSilently())) count++;
            }

            foreach (var ring in Object.FindObjectsByType<BossAreaEffect>())
            {
                if (TryDiscard(ring, r => r.DiscardSilently())) count++;
            }

            return count;
        }

        /// <summary>
        /// 하나의 회수 실패가 나머지 정리와 호출자(런 종료 정산·씬 전환)를 멈추지 않게 한다.
        /// 예외는 삼키지 않고 로그로 남긴다.
        /// </summary>
        private static bool TryDiscard<T>(T target, Func<T, bool> discard) where T : Object
        {
            if (target == null) return false;
            try
            {
                return discard(target);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                return false;
            }
        }
    }
}
