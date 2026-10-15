namespace Abyss.Runtime.Interaction
{
    /// <summary>
    /// 지금 상호작용을 받으면 안 되는 상태임을 알리는 것.
    /// <see cref="PlayerInteractor"/>가 같은 오브젝트에서 찾아 게이트로 쓴다.
    ///
    /// 🔑 <b>왜 인터페이스인가</b> — 예전에는 <c>PlayerInteractor</c>가
    /// <c>LobbyPlayerController</c>를 직접 들고 <c>InputLocked</c>를 봤다.
    /// 그래서 상호작용 파이프라인 전체가 <b>로비 전용</b>이 됐고,
    /// 런 플레이어에는 붙일 수 없어 제단이 반응하지 않았다.
    /// 막는 조건은 씬마다 다르지만 <b>막아야 한다는 사실</b>은 같으므로, 그 하나만 인터페이스로 뽑는다.
    ///
    /// 📌 같은 오브젝트의 모든 구현을 합산하며, 구현이 없으면 막지 않는다.
    /// 공유 PlayerCharacter는 직접 입력 잠금과 런 입력 모드·상위 모달을 반영한다.
    /// LobbyPlayerController 어댑터의 기존 잠금 규약도 함께 유지한다.
    /// </summary>
    public interface IInteractionBlocker
    {
        /// <summary>true면 이번 상호작용 입력을 무시한다.</summary>
        bool BlocksInteraction { get; }
    }
}
