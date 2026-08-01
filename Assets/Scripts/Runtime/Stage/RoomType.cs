namespace Abyss.Runtime.Stage
{
    /// <summary>
    /// 방 유형. 프로토 단일 스테이지 구성(전투 4~5 + 엘리트 1 + 보스 1)에
    /// 완주 루프 계획 Phase 1의 비전투 방(이벤트)이 더해진다.
    /// </summary>
    public enum RoomType
    {
        Combat,
        Elite,
        Boss,

        /// <summary>비전투 선택 방. 적을 두지 않고 <see cref="EventData"/>를 제시한다.</summary>
        Event
    }
}
