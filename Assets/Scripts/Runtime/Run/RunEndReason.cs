namespace Abyss.Runtime.Run
{
    /// <summary>
    /// 런이 끝난 사유. 완주 루프 계획 2-2.
    ///
    /// 사망과 완주는 지금까지 같은 <see cref="RunManager.EndRun"/> 경로로 흘러 결과 화면에서도
    /// 구분되지 않았다. 엔딩은 완주에만 붙어야 하므로 사유를 값으로 남긴다.
    ///
    /// 기본값을 <see cref="Death"/>로 둔 이유: 이 열거형이 생기기 전의 모든 종료는 사망이었고,
    /// 사유를 명시하지 않은 호출(디버그 메뉴 등)이 실수로 엔딩을 띄우면 안 된다.
    /// </summary>
    public enum RunEndReason
    {
        /// <summary>플레이어 사망.</summary>
        Death = 0,

        /// <summary>시퀀스의 마지막 스테이지까지 클리어 — 엔딩 대상.</summary>
        Cleared = 1,

        /// <summary>
        /// 일시정지 메뉴에서 타이틀로 나가며 런을 버림.
        ///
        /// <b>정산하지 않는다</b> — 포기하면 심연 조각을 버린다(사용자 결정 2026-09-22).
        /// 그래서 이 사유는 <see cref="RunManager.EndRun"/>이 아니라
        /// <see cref="RunManager.AbandonRun"/>만 남긴다.
        /// </summary>
        Abandoned = 2,
    }
}
