#if UNITY_EDITOR
namespace Abyss.EditorTools
{
    /// <summary>
    /// 에디터 도구 <b>이름</b>의 단일 출처(SoT). <see cref="AbyssToolsWindow"/>의 버튼 라벨이자,
    /// 「먼저 ○○를 실행하세요」 같은 안내 문구가 가리키는 이름이다.
    ///
    /// 🔴 <b>예전에는 이것이 메뉴 경로였다</b>(<c>AbyssMenu</c>, <c>"Tools/Abyss/Build/..."</c>).
    /// 도구가 33개까지 늘면서 메뉴 바가 4개 그룹으로 흩어졌고, 무엇이 무엇인지 이름만으로는
    /// 읽히지 않았다(2026-09-16 사용자 지적). 도구는 <b>창 하나</b>로 모으고,
    /// 여기에는 <b>접두어 없는 이름만</b> 남긴다.
    ///
    /// 🔑 <b>이름을 따로 두는 이유는 안내 문구다.</b> 「PlayerInteractor 가 없다 → 먼저 ○○를
    /// 실행할 것」처럼, 한 도구가 다른 도구를 가리키는 자리가 여럿이다. 그 문자열이 복제되면
    /// 도구 이름을 바꿀 때 안내만 옛 이름으로 남는다 — 오류가 안 나는 종류다.
    /// </summary>
    internal static class AbyssToolNames
    {
        // 씬 구성
        public const string BuildTitleScene = "Title Scene";
        public const string BuildLobbyScene = "Lobby Scene";
        public const string BuildHud = "HUD Children";
        public const string BuildDraftPanel = "Draft Panel Children";
        public const string BuildResultPanel = "Result Panel Children";
        public const string BuildDraftSystem = "Draft System";
        public const string BuildStageDirector = "StageDirector in Active Scene";
        public const string BuildRoomLayouts = "Room Layouts in Active Scene";
        public const string BuildPlatforms = "Test Platforms in Active Scene";
        public const string BuildFormAltar = "Form Altar in Active Scene";
        public const string BuildWeaponAltar = "Weapon Altar in Active Scene";
        public const string BuildArtTestStage = "Art Test Stage (stage1)";

        // 콘텐츠(SO)
        public const string GenerateContent = "Prototype Content";
        public const string BuildStage1 = "Stage 1 Content";
        public const string BuildStage2 = "Stage 2 Content";
        public const string BuildStage3 = "Stage 3 Content";
        public const string GenerateShopContent = "Shop Content (add missing items)";
        public const string GenerateEventContent = "Event Content (add missing choices)";
        public const string GenerateRelicContent = "Relic Content (add missing relics)";
        public const string GenerateWireSkills = "Wire Active Skill Abilities";

        // 프리팹·애니메이션
        public const string GeneratePrefabs = "Prototype Prefabs";
        public const string GenerateRebuildEnemies = "Rebuild Enemy Prefabs (Force)";
        public const string GenerateRebuildPlayer = "Rebuild Player Prefab (Force)";
        public const string GeneratePlayerAnimation = "Player Animation Clips";

        // 아트 임포트
        public const string ApplyFormSpriteImport = "Form Sprite Import Settings";
        public const string ApplyEnvironmentArtImport = "Environment Art Import Settings";
        public const string ImportWeaponSprites = "Weapon Sprites";
        public const string ImportWeaponAnchorSet = "Weapon Anchor Set";
        public const string WireWeaponAngles = "Wire Weapon Angle Sprites";

        // 정리(되돌리는 쪽)
        public const string ClearPlatforms = "Clear Test Platforms";
        public const string ClearRoomLayouts = "Clear Room Layouts";

        // 디버그(조회)
        public const string OpenStatInspector = "Stat Inspector";
    }
}
#endif
