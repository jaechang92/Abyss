namespace Abyss.Runtime.Enemy
{
    /// <summary>
    /// 적의 등급. <b>상호배타적 4단계</b>이고, 이것이 분류의 SoT다.
    ///
    /// 원래는 <c>isElite</c>·<c>isBoss</c> 두 bool이 이 축을 표현하고 있었다. 그런데 중간보스는
    /// 엘리트 보상 트리거(<c>NotifyEliteKilled</c>)를 재활용하려고 <c>isElite=1</c>로 둔 것이지
    /// <b>엘리트라서 그런 게 아니었다</b> — 플래그가 분류가 아니라 <b>동작 스위치</b>로 쓰이고 있었다.
    ///
    /// 그래서 분류를 묻는 소비자(도감)가 틀린 답을 받았다. 미드보스 2종이 '적' 탭에 엘리트로 나왔고,
    /// 그걸 고치겠다고 <c>isBoss</c>를 켜면 처치 판정·프리팹 크기·발사체 연결까지 함께 바뀐다.
    ///
    /// → 등급은 여기서 한 번만 정하고, 동작 스위치는 <see cref="EnemyData.IsBoss"/>·
    /// <see cref="EnemyData.IsElite"/>가 이 값에서 <b>파생</b>한다. 두 값이 어긋날 수 없다.
    ///
    /// ⚠️ 숫자는 직렬화된다(에셋의 <c>tier:</c>). <b>순서를 바꾸거나 중간에 끼워 넣지 말 것</b> —
    /// 새 등급이 필요하면 뒤에 붙인다.
    /// </summary>
    public enum EnemyTier
    {
        /// <summary>일반 적. 잡몹.</summary>
        Normal = 0,

        /// <summary>엘리트. 처치 시 EliteBonus 드래프트가 열린다.</summary>
        Elite = 1,

        /// <summary>
        /// 중간보스. <b>보상은 엘리트와 같고</b>(같은 트리거 재활용) <b>표시는 보스 쪽</b>이다.
        /// 보스 처치 수(<c>totalBossKillCount</c>)에는 들어가지 않는다 — 그 수치가
        /// 기록자 챕터 해금 조건이라 미드보스로 열리면 연재 순서가 무너진다.
        /// </summary>
        MidBoss = 2,

        /// <summary>스테이지 최종 보스. 처치 시 보스 처치 수가 오른다.</summary>
        Boss = 3,
    }
}
