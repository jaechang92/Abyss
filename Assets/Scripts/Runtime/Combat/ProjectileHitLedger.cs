using System.Collections.Generic;

namespace Abyss.Runtime.Combat
{
    /// <summary>
    /// 발사체 한 발의 적중 장부 — 관통 수와 이미 맞힌 대상을 센다.
    ///
    /// 🔴 <b>같은 적을 두 번 맞히지 않는다.</b> 적 하나가 콜라이더를 여러 개 갖거나 트리거가 겹친 채 지나가면
    /// <c>OnTriggerEnter2D</c> 가 한 적에 여러 번 들어온다. 관통 발사체는 그걸 막을 기억이 필요하다.
    ///
    /// 📌 <b>Unity 타입을 안 쓰는 순수 클래스</b>라 GameObject 없이 테스트한다.
    /// 대상은 참조 동일성으로 비교한다(인스턴스 ID 는 Unity 6.6 에서 쓸 수 없다).
    /// </summary>
    public sealed class ProjectileHitLedger<T> where T : class
    {
        private readonly List<T> hitTargets = new();
        private int remainingHits = 1;

        /// <summary>더 맞힐 수 없다 — 발사체가 사라질 때다.</summary>
        public bool IsExhausted => remainingHits <= 0;

        /// <summary>지금까지 맞힌 서로 다른 대상 수.</summary>
        public int HitCount => hitTargets.Count;

        /// <summary>새 발사에 맞춰 비운다.</summary>
        /// <param name="pierceCount">첫 적 뒤로 더 뚫고 지나갈 적 수. 0 = 첫 적에서 멈춘다.</param>
        public void Reset(int pierceCount)
        {
            hitTargets.Clear();
            remainingHits = (pierceCount < 0 ? 0 : pierceCount) + 1;
        }

        /// <summary>
        /// 적중을 장부에 올린다. 이미 맞힌 대상이거나 더 맞힐 수 없으면 <c>false</c>.
        /// </summary>
        /// <param name="isFirstHit">이번이 이 발사체의 첫 적중이면 <c>true</c>.</param>
        public bool TryRegister(T target, out bool isFirstHit)
        {
            isFirstHit = false;
            if (target == null || IsExhausted) return false;
            if (hitTargets.Contains(target)) return false;

            isFirstHit = hitTargets.Count == 0;
            hitTargets.Add(target);
            remainingHits--;
            return true;
        }
    }
}
