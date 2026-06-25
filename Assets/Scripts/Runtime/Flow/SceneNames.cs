namespace Abyss.Runtime.Flow
{
    /// <summary>
    /// 씬 이름 단일 소스(SoT). 빌드 설정(Build Settings)의 씬 이름과 일치해야 한다.
    /// buildIndex 하드코딩 대신 이 상수를 사용해 씬 순서 변경에 안전하게 한다.
    /// 씬 분리: Bootstrap(시스템 초기화) → Lobby(런 시작 전) → Run(게임플레이).
    /// </summary>
    public static class SceneNames
    {
        public const string Bootstrap = "Bootstrap";
        public const string Lobby = "Lobby";
        public const string Run = "Run";
    }
}
