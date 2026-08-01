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
        Event,

        /// <summary>
        /// 휴식 방. 동작은 <see cref="Event"/>와 같은 경로를 타고(둘 다 <c>eventData</c>로 구동),
        /// 이 값은 <b>표시용 구분</b>이다 — 노드 맵(1-4)에서 모닥불과 물음표를 다른 아이콘으로 보여주려면
        /// 타입이 갈라져 있어야 한다. 이벤트와 달리 손해 보는 선택지를 두지 않는다.
        /// </summary>
        Rest
    }
}
