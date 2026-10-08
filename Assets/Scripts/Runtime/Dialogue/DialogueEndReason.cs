namespace Abyss.Runtime.Dialogue
{
    /// <summary>
    /// 대화가 끝난 이유 — <see cref="DialogueUI.PlayTracked"/> 호출자만 받는다. 기존 <c>Play(..., Action)</c> 호출자는 그대로다.
    /// 정상 종료(<see cref="Completed"/>)만 「끝까지 들었다」로 처리해야 한다. 나머지는 잠금만 풀고 열람으로 치지 않는다.
    /// </summary>
    public enum DialogueEndReason
    {
        /// <summary>마지막 줄을 넘겨 정상으로 닫혔다.</summary>
        Completed,

        /// <summary>재생 중에 다른 대화가 Play로 자리를 가져갔다.</summary>
        Replaced,

        /// <summary>대화 UI가 비활성화·파괴됐거나(씬 이탈 포함) 표시할 화면(root)이 없다.</summary>
        Interrupted,

        /// <summary>재생할 줄이 없었다.</summary>
        Empty
    }
}
