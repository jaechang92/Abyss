namespace Abyss.Runtime.Combat
{
    /// <summary>
    /// 발사체가 적에게 닿은 순간을 발사자에게 알린다.
    ///
    /// 🔑 <b>근접은 입력 프레임에 「맞았다」가 확정되지만 발사체는 닿는 프레임에 확정된다.</b>
    /// 적중에 붙는 효과(심연 충전 · 히트스탑 · 흔들림)를 발사 시점에 처리하면 빗나가도 소비된다 —
    /// 그래서 발사체가 닿았을 때 발사자에게 돌려준다.
    ///
    /// 📌 람다가 아니라 인터페이스인 이유: 발사체는 풀링되므로 발사마다 클로저를 만들면 할당이 쌓인다.
    /// 발사자는 청취자 객체를 한 번 만들어 두고 계속 넘긴다.
    /// </summary>
    public interface IProjectileHitListener
    {
        /// <summary>
        /// 적 하나에 적중했다. 반환값이 실제로 들어갈 피해다.
        /// </summary>
        /// <param name="damage">발사 시점에 확정된 피해.</param>
        /// <param name="isFirstHit">이 발사체의 첫 적중인가. 관통 발사체는 한 발이 여러 번 적중한다.</param>
        int OnProjectileHit(int damage, bool isFirstHit);
    }
}
