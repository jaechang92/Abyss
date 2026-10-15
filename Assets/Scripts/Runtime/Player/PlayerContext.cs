namespace Abyss.Runtime.Player
{
    /// <summary>
    /// 같은 <see cref="PlayerCharacter"/>가 놓인 <b>실행 맥락</b>. 씬 인스턴스의 직렬화 값으로 명시한다.
    ///
    /// 🔑 로비와 Run 의 캐릭터는 같은 컴포넌트·같은 이동·같은 FSM 을 쓴다(2026-10-12 사용자 지정).
    /// 갈리는 것은 <b>런 기록·보상 계층에 손을 대도 되는가</b> 하나뿐이라, 씬 이름·싱글톤 존재 여부로
    /// 추측하지 않고 이 값 하나로 판정한다. RunManager 는 DontDestroyOnLoad 라 로비에도 살아 있을 수 있어
    /// "있으면 런" 판정은 지난 런 기록을 로비에서 덮는다.
    ///
    /// 기본값은 <see cref="Run"/> — Player.prefab 은 이 필드를 직렬화하지 않아도 기존 동작 그대로다.
    /// </summary>
    public enum PlayerContext
    {
        /// <summary>던전 런. 모든 기록·보상·튜토리얼·일시정지 요청이 기존대로 동작한다.</summary>
        Run = 0,

        /// <summary>허브 로비. 이동·점프·대시·FSM·애니메이션은 공유하고 런 기록·보상 계층에는 손대지 않는다.</summary>
        Lobby = 1,
    }
}
