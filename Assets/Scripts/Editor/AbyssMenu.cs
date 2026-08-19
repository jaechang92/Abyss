#if UNITY_EDITOR
namespace Abyss.EditorTools
{
    /// <summary>
    /// 모든 에디터 메뉴 경로 단일 소스(SoT).
    /// 곳곳의 빌더에 흩어져 있던 "Tools/Abyss/..." 리터럴을 한 곳에 모아 가독성·일관성을 확보한다.
    /// 서브메뉴(Build / Generate / Clear)로 그룹화한다.
    /// MenuItem 속성 인자는 컴파일 타임 상수여야 하므로 const 문자열 조합(+)을 사용한다.
    /// </summary>
    internal static class AbyssMenu
    {
        private const string Root = "Tools/Abyss/";
        private const string Build = Root + "Build/";
        private const string Generate = Root + "Generate/";
        private const string Clear = Root + "Clear/";

        // Build — 씬·UI 구성
        public const string BuildTitleScene = Build + "Title Scene";
        public const string BuildLobbyScene = Build + "Lobby Scene";
        public const string BuildHud = Build + "HUD Children";
        public const string BuildDraftPanel = Build + "Draft Panel Children";
        public const string BuildResultPanel = Build + "Result Panel Children";
        public const string BuildDraftSystem = Build + "Draft System";
        public const string BuildStage1 = Build + "Stage 1 Content";
        public const string BuildStage2 = Build + "Stage 2 Content";
        public const string BuildStage3 = Build + "Stage 3 Content";
        public const string BuildStageDirector = Build + "StageDirector in Active Scene";
        public const string BuildRoomLayouts = Build + "Room Layouts in Active Scene";
        public const string BuildPlatforms = Build + "Test Platforms in Active Scene";
        public const string BuildFormAltar = Build + "Form Altar in Active Scene";

        // Generate — 에셋(콘텐츠/프리팹) 생성
        // Generate — 아트 임포트 설정
        public const string ApplyFormSpriteImport = Generate + "Form Sprite Import Settings";

        public const string GenerateContent = Generate + "Prototype Content";
        public const string GenerateWireSkills = Generate + "Wire Active Skill Abilities";
        public const string GeneratePrefabs = Generate + "Prototype Prefabs";
        public const string GenerateRebuildEnemies = Generate + "Rebuild Enemy Prefabs (Force)";
        public const string GenerateRebuildPlayer = Generate + "Rebuild Player Prefab (Force)";
        public const string GenerateSpriteImport = Generate + "Setup Enemy Sprite Import Settings";

        // Clear — 씬 오브젝트 제거
        public const string ClearPlatforms = Clear + "Test Platforms in Active Scene";
        public const string ClearRoomLayouts = Clear + "Room Layouts in Active Scene";

        // Input
        public const string PatchInputActions = Root + "Patch Input Actions";
    }
}
#endif
