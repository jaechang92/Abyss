using Abyss.Runtime.Enemy;

namespace Abyss.Runtime.Combat
{
    /// <summary>
    /// 발사체가 <b>어느 적</b>에게 피해를 준 뒤인지 알아야 하는 발사자용 추가 통지(P04 C 표식).
    ///
    /// 🔑 <see cref="IProjectileHitListener"/> 의 시그니처를 바꾸지 않고 추가만 한다 — 기존 청취자(심연 충전 ·
    /// 히트스탑)는 그대로다. 발사체는 청취자가 이 인터페이스도 구현할 때만 부른다.
    /// </summary>
    public interface IProjectileEnemyHitListener
    {
        /// <summary>
        /// 적 하나에 피해를 적용한 직후. 이 피해로 죽었으면 <c>enemy.IsDead</c> 가 참이다.
        /// </summary>
        /// <param name="enemy">맞은 적.</param>
        /// <param name="isFirstHit">이 발사체의 첫 적중인가. 관통 후속 타격은 false.</param>
        void OnProjectileEnemyHit(EnemyBase enemy, bool isFirstHit);
    }
}
