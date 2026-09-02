namespace Abyss.Runtime.UI
{
    /// <summary>
    /// 화면을 덮는 UI 레이어의 정렬 순위 SoT.
    ///
    /// 그전까지 이 값들은 <b>10개 파일에 흩어진 리터럴</b>이었고, 서로를 주석 안의 숫자로
    /// 참조했다(<i>"일시정지(200)보다 아래"</i>). 층을 하나 끼워 넣으려면 관련 없는 파일의 주석까지
    /// 훑어야 했고, 한 곳만 고치면 주석과 코드가 조용히 어긋난다 — 겹침 순서는 실행해 보기 전에는
    /// 틀린 것이 드러나지 않는 종류의 값이라 특히 위험하다.
    ///
    /// 값 사이를 50~100씩 벌려 둔 이유는 <b>새 층이 사이에 들어올 자리</b>를 남기기 위해서다.
    /// <see cref="RunResult"/>가 실제로 그 자리에 들어왔다.
    /// </summary>
    public static class UiSortingOrder
    {
        /// <summary>씬 캔버스 기본 층 — HUD·DraftPanel이 계층 순서대로 그려진다.</summary>
        public const int Hud = 0;

        /// <summary>
        /// 모달·방 진행 패널(스킬 교체·폼 보상·이벤트·상점·노드맵). 드래프트 패널 위를 덮는다.
        /// 한 층을 공유하는 이유는 이들이 동시에 열리지 않기 때문이다 — 서로 겹칠 일이 없으면
        /// 층을 나눠 봐야 순서에 의미가 생기지 않고 관리할 상수만 늘어난다.
        /// </summary>
        public const int Modal = 100;

        /// <summary>
        /// 런 종료 결과 패널.
        ///
        /// <b><see cref="Modal"/>보다 위여야 한다.</b> 런이 끝난 시점에 열려 있던 모달은 이미 의미가
        /// 없는 화면인데, 그것이 결과 패널을 덮으면 [즉시 재시작]·[로비로]에 손이 닿지 않아
        /// <b>빠져나갈 수단이 사라진다</b>. 현재는 모달 표시 중 게임이 정지되어 런이 끝날 수 없으므로
        /// 실제로 겹치지 않지만, 그것은 <i>다른 시스템의 성질에 기댄 안전</i>이라 정지 규칙이 바뀌면
        /// 함께 깨진다. 층으로 못 박아 두면 그 결합이 끊긴다.
        /// </summary>
        public const int RunResult = 150;

        /// <summary>
        /// 일시정지 패널과 로비 메뉴. 결과 패널보다도 위다 — 정지 패널이 가려지면 재개·타이틀·종료가
        /// 모두 막혀 게임을 빠져나갈 방법이 없어진다.
        ///
        /// 두 패널이 값을 공유하는 것은 <b>씬이 달라 공존하지 않기</b> 때문이다(일시정지=Run, 메뉴=Lobby).
        /// </summary>
        public const int Menu = 200;

        /// <summary>도감. 로비 메뉴를 닫지 않고 그 위에 덮는다 — 닫으면 메뉴로 돌아온다.</summary>
        public const int Codex = 300;

        /// <summary>프롤로그·엔딩 자막 시퀀스. 결과 패널(<see cref="RunResult"/>)보다 위.</summary>
        public const int Sequence = 400;

        /// <summary>설정 패널. 일시정지·로비 메뉴 <b>양쪽에서 열리므로</b> 그 위여야 한다.</summary>
        public const int Settings = 500;

        /// <summary>씬 전환 페이드. 최상단 — 전환 중에는 그 무엇도 페이드 위로 새어 나오면 안 된다.</summary>
        public const int ScreenFade = 32000;
    }
}
