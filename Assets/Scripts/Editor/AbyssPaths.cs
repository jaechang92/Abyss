#if UNITY_EDITOR
namespace Abyss.EditorTools
{
    /// <summary>
    /// 에디터 도구가 사용하는 에셋/디렉터리 경로 단일 소스(SoT).
    /// 각 빌더에 흩어져 중복되던 "Assets/..." 경로 리터럴을 한 곳에 모은다.
    /// 폴더 구조 변경 시 이 파일만 고치면 된다. 개별 .asset 경로는 디렉터리 + 파일명으로 조합한다.
    /// 메뉴 경로는 <see cref="AbyssMenu"/>가 담당(역할 분리).
    /// </summary>
    internal static class AbyssPaths
    {
        // 데이터 (ScriptableObject) 디렉터리
        private const string Data = "Assets/Data";
        public const string Forms = Data + "/Forms";
        public const string Skills = Data + "/Skills";
        public const string Enemies = Data + "/Enemies";
        public const string Rooms = Data + "/Rooms";
        public const string Stages = Data + "/Stages";
        public const string Abilities = Data + "/Abilities";
        public const string Physics = Data + "/Physics";
        public const string Dialogue = Data + "/Dialogue";
        public const string RunConfigDir = "Assets/Resources/Data"; // P-14: 런타임 Resources.Load 대상(정규 위치)
        public const string MetaUpgrades = RunConfigDir + "/MetaUpgrades"; // 메타 영구 업그레이드 SO(런타임 Resources.LoadAll)

        // 프리팹 디렉터리
        private const string Prefabs = "Assets/Prefabs";
        public const string EnemyPrefabs = Prefabs + "/Enemies";
        public const string PlayerPrefabs = Prefabs + "/Player";
        public const string CombatPrefabs = Prefabs + "/Combat";

        // 아트 스프라이트
        public const string Sprites = "Assets/Art/Sprites";
        public const string EnemySprites = Sprites + "/Enemies";
        public const string SkillIcons = Sprites + "/SkillIcons";

        // 오디오
        public const string Sfx = "Assets/Audio/SFX";

        // 개별 에셋 파일
        public const string WhiteSquare = Sprites + "/WhiteSquare.png";
        public const string Frictionless = Physics + "/Frictionless.physicsMaterial2D";
        public const string EnemyProjectilePrefab = CombatPrefabs + "/EnemyProjectile.prefab";
        public const string LobbyScene = "Assets/Scenes/Lobby.unity";
        public const string InputActions = "Assets/InputSystem_Actions.inputactions";
    }
}
#endif
