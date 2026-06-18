namespace Abyss.Runtime.Combat
{
    /// <summary>
    /// 발사체 진영. 누구를 맞히고 누구를 통과할지 결정한다.
    /// 기존 적 발사체는 HitsPlayer(기본), 플레이어 스킬 발사체는 HitsEnemies.
    /// </summary>
    public enum ProjectileFaction
    {
        /// <summary>적이 발사 — 플레이어를 맞히고 적은 통과(기존 동작).</summary>
        HitsPlayer,

        /// <summary>플레이어가 발사 — 적을 맞히고 플레이어는 통과.</summary>
        HitsEnemies
    }
}
