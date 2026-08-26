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
    /// 📌 구현이 없으면(<c>GetComponent</c> 결과 null) 막지 않는다.
    /// 잠금 개념이 없는 플레이어에게 잠금 구현을 강요하지 않기 위해서다 —
    /// 런 플레이어(<c>PlayerCharacter</c>)에는 아직 입력 잠금 개념 자체가 없다.
    /// </summary>
    public interface IInteractionBlocker
    {
        /// <summary>true면 이번 상호작용 입력을 무시한다.</summary>
        bool BlocksInteraction { get; }
    }
}
