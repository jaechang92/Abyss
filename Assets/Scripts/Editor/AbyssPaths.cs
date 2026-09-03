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
        public const string Rooms = Data + "/Rooms";
        public const string Stages = Data + "/Stages";
        public const string Events = Data + "/Events"; // 이벤트 방 EventData SO(룸이 직접 참조 — 자동 로드 아님)
        public const string Shops = Data + "/Shops";   // 상점 방 ShopData SO(룸이 직접 참조 — 자동 로드 아님)
        public const string Abilities = Data + "/Abilities";
        public const string Physics = Data + "/Physics";
        public const string Dialogue = Data + "/Dialogue";
        public const string Story = Data + "/Story"; // 서사(기록자) StoryData SO
        public const string RunConfigDir = "Assets/Resources/Data"; // P-14: 런타임 Resources.Load 대상(정규 위치)
        public const string MetaUpgrades = RunConfigDir + "/MetaUpgrades"; // 메타 영구 업그레이드 SO(런타임 Resources.LoadAll)
        public const string Relics = RunConfigDir + "/Relics"; // 유물 SO(런타임 RelicCatalog.LoadAll) — 폴더에 추가 시 자동 편입
        public const string Skills = RunConfigDir + "/Skills"; // 드래프트 스킬 SO(런타임 SkillCatalog.LoadAll) — 폴더에 추가 시 자동 편입
        public const string Forms = RunConfigDir + "/Forms"; // 폼 SO(런타임 FormCatalog.LoadAll) — 폴더에 추가 시 자동 편입
        // 적 SO는 방·스테이지가 직접 참조하므로 원래 Assets/Data 아래에 있었지만, 도감(3-1)이 타이틀 씬에서
        // 열려야 해서 Resources로 옮겼다 — 타이틀에는 StageData·RoomData 참조가 하나도 로드되지 않으므로
        // 전체 적 목록을 얻는 길이 Resources.LoadAll뿐이다(런타임 EnemyCatalog.LoadAll).
        public const string Enemies = RunConfigDir + "/Enemies";

        // 프리팹 디렉터리
        private const string Prefabs = "Assets/Prefabs";
        public const string EnemyPrefabs = Prefabs + "/Enemies";
        public const string PlayerPrefabs = Prefabs + "/Player";
        public const string CombatPrefabs = Prefabs + "/Combat";

        // 아트 스프라이트
        public const string Sprites = "Assets/Art/Sprites";
        public const string EnemySprites = Sprites + "/Enemies";
        public const string SkillIcons = Sprites + "/SkillIcons";
        public const string Props = Sprites + "/Props"; // 월드 오브젝트 스프라이트(제단 등)
        // 배경 레이어는 Sprite가 아니라 Texture로 임포트한다(RawImage.uvRect 스크롤 대상).
        public const string TitleBackdrop = "Assets/Art/Backgrounds/Title";

        // 오디오
        public const string Sfx = "Assets/Audio/SFX";

        // 개별 에셋 파일
        public const string WhiteSquare = Sprites + "/WhiteSquare.png";
        public const string FormAltarSprite = Props + "/form_altar.png";
        public const string Frictionless = Physics + "/Frictionless.physicsMaterial2D";
        public const string EnemyProjectilePrefab = CombatPrefabs + "/EnemyProjectile.prefab";
        public const string TitleScene = "Assets/Scenes/Title.unity";
        public const string LobbyScene = "Assets/Scenes/Lobby.unity";
        public const string InputActions = "Assets/InputSystem_Actions.inputactions";
    }
}
#endif
