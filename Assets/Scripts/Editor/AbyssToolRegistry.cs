#if UNITY_EDITOR
using System;
using System.Collections.Generic;

namespace Abyss.EditorTools
{
    /// <summary>
    /// 이 프로젝트의 에디터 도구 <b>목록</b>. <see cref="AbyssToolsWindow"/>가 이것만 보고 화면을 그린다.
    ///
    /// 🔴 <b>예전에는 도구가 33개의 MenuItem 속성으로 흩어져 있었다.</b> 무엇이 있는지 알려면
    /// 메뉴 바를 네 그룹(Build/Generate/Clear/Wire)에 걸쳐 훑어야 했고, 셋은 아예 그룹 규칙 밖에 있었다.
    /// <b>목록이 코드 어디에도 없었다</b>는 것이 문제의 뿌리다 — 무엇이 무엇과 비슷한지 읽을 자리가 없었다.
    ///
    /// 🔑 <b>그래서 목록을 데이터로 만든다.</b> 도구를 더하는 일은 여기 한 줄을 더하는 일이고,
    /// 화면·검색·분류는 창이 알아서 한다. 설명을 필수로 둔 것도 같은 이유다 —
    /// 이름만으로는 안 읽히는 도구가 있었다는 것이 원래 불만이었다.
    /// </summary>
    internal static class AbyssToolRegistry
    {
        private static Group[] groups;

        public static Group[] All => groups ??= Build();

        /// <summary>도구 하나.</summary>
        internal readonly struct AbyssTool
        {
            public readonly string Name;
            public readonly string Summary;
            public readonly Action Run;

            /// <summary>
            /// 되돌리기 어려운 도구(강제 재생성·삭제). 창이 눈에 띄게 표시하고 확인을 한 번 받는다 —
            /// 버튼이 줄줄이 늘어선 화면에서 <b>오클릭이 가장 비싼</b> 것들이다.
            /// </summary>
            public readonly bool IsDestructive;

            public AbyssTool(string name, string summary, Action run, bool isDestructive = false)
            {
                Name = name;
                Summary = summary;
                Run = run;
                IsDestructive = isDestructive;
            }
        }

        /// <summary>도구 묶음. 탭 이름 + 제목 + 한 줄 설명 + 도구들.</summary>
        internal sealed class Group
        {
            /// <summary>
            /// 상단 탭에 찍을 짧은 이름. <see cref="Title"/>과 따로 두는 이유는 <b>가로 폭</b>이다 —
            /// 탭 다섯이 한 줄에 들어가야 하므로 제목을 그대로 쓰면 창을 좁혔을 때 글자가 잘린다.
            /// 잘린 탭 이름은 아무 표시 없이 못 읽게 된다.
            /// </summary>
            public string Tab;

            public string Title;
            public string Note;
            public AbyssTool[] Tools;
        }

        private static Group[] Build() => new[]
        {
            new Group
            {
                Tab = "씬",
                Title = "씬 구성",
                Note = "활성 씬에 오브젝트를 놓거나, 씬 자체를 만든다.",
                Tools = new[]
                {
                    new AbyssTool(AbyssToolNames.BuildTitleScene,
                        "타이틀 씬을 만들고 빌드 설정에 등록한다.", TitleSceneBuilder.Build),
                    new AbyssTool(AbyssToolNames.BuildLobbyScene,
                        "허브 로비 씬을 만들고 빌드 설정에 등록한다.", LobbySceneBuilder.Build),
                    new AbyssTool(AbyssToolNames.BuildHud,
                        "Hierarchy 에서 HUDPresenter 를 고른 뒤 실행 — HUD 자식 UI를 채운다.", HudBuilder.Build),
                    new AbyssTool(AbyssToolNames.BuildDraftPanel,
                        "드래프트 패널의 자식 UI를 채운다.", DraftPanelBuilder.Build),
                    new AbyssTool(AbyssToolNames.BuildResultPanel,
                        "결과 패널의 자식 UI를 채운다.", ResultPanelBuilder.Build),
                    new AbyssTool(AbyssToolNames.BuildDraftSystem,
                        "드래프트 세션 컨트롤러와 배선을 활성 씬에 놓는다.", DraftSystemBuilder.Setup),
                    new AbyssTool(AbyssToolNames.BuildStageDirector,
                        "활성 씬에 StageDirector 를 놓고 스테이지 시퀀스를 물린다.",
                        StageBuilder.SetupStageDirectorInActiveScene),
                    new AbyssTool(AbyssToolNames.BuildRoomLayouts,
                        "방 지형(발판·벽)을 활성 씬에 생성한다.", RoomLayoutBuilder.BuildRoomLayouts),
                    new AbyssTool(AbyssToolNames.BuildPlatforms,
                        "임시 테스트 발판을 놓는다. 룸 레이아웃과 겹치면 그쪽을 먼저 지울 것.",
                        PlatformBuilder.BuildTestPlatforms),
                    new AbyssTool(AbyssToolNames.BuildFormAltar,
                        "폼 보상 제단을 놓고 StageDirector 에 배선한다.",
                        FormAltarBuilder.BuildFormAltarInActiveScene),
                    new AbyssTool(AbyssToolNames.BuildWeaponAltar,
                        "무기 보상 제단을 놓고 StageDirector 에 배선한다.",
                        WeaponAltarBuilder.BuildWeaponAltarInActiveScene),
                    new AbyssTool(AbyssToolNames.BuildArtTestStage,
                        "아트 확인용 임시 스테이지를 구성한다.", ArtTestStageBuilder.Build),
                },
            },

            new Group
            {
                Tab = "콘텐츠",
                Title = "콘텐츠 (SO 에셋)",
                Note = "이미 있는 에셋은 보존하고 빠진 것만 채운다 — 손으로 맞춘 수치를 되돌리지 않는다.",
                Tools = new[]
                {
                    new AbyssTool(AbyssToolNames.GenerateContent,
                        "적·폼·스킬 등 기본 콘텐츠 SO를 만든다. 다른 도구가 이걸 먼저 요구한다.",
                        ContentBuilder.Generate),
                    new AbyssTool(AbyssToolNames.BuildStage1,
                        "스테이지 1의 방·이벤트·상점·시퀀스를 만든다.", StageBuilder.BuildStage1Content),
                    new AbyssTool(AbyssToolNames.BuildStage2,
                        "스테이지 2의 방·이벤트·상점·시퀀스를 만든다.", StageBuilder.BuildStage2Content),
                    new AbyssTool(AbyssToolNames.BuildStage3,
                        "스테이지 3의 방·이벤트·상점·시퀀스를 만든다.", StageBuilder.BuildStage3Content),
                    new AbyssTool(AbyssToolNames.GenerateShopContent,
                        "상점 3종을 점검하고 빠진 품목만 덧붙인다.",
                        ShopContentBuilder.GenerateShopContentMenu),
                    new AbyssTool(AbyssToolNames.GenerateEventContent,
                        "이벤트 6종·휴식 3종을 점검하고 빠진 선택지만 덧붙인다.",
                        EventContentBuilder.GenerateEventContentMenu),
                    new AbyssTool(AbyssToolNames.GenerateRelicContent,
                        "유물 SO를 점검하고 빠진 것만 만든다.", RelicContentBuilder.Generate),
                    new AbyssTool(AbyssToolNames.GenerateWireSkills,
                        "액티브 스킬 SO에 어빌리티 구현을 물린다(콘텐츠 생성 없이 배선만).",
                        ContentBuilder.WireAbilitiesOnly),
                },
            },

            new Group
            {
                Tab = "프리팹",
                Title = "프리팹 · 애니메이션",
                Note = "(Force) 는 기존 프리팹을 지우고 다시 만든다 — 씬·에셋의 참조가 끊길 수 있다.",
                Tools = new[]
                {
                    new AbyssTool(AbyssToolNames.GeneratePrefabs,
                        "플레이어·적 프리팹을 만든다(이미 있으면 보존). 적 스프라이트 임포트 설정도 같이 맞춘다.",
                        PrefabBuilder.Build),
                    new AbyssTool(AbyssToolNames.GenerateRebuildEnemies,
                        "적 프리팹을 지우고 다시 만든다.", PrefabBuilder.RebuildEnemies, isDestructive: true),
                    new AbyssTool(AbyssToolNames.GenerateRebuildPlayer,
                        "플레이어 프리팹을 지우고 다시 만든다.", PrefabBuilder.RebuildPlayer, isDestructive: true),
                    new AbyssTool(AbyssToolNames.GeneratePlayerAnimation,
                        "플레이어 애니메이션 클립·컨트롤러·폼 오버라이드를 만든다.",
                        PlayerAnimationBuilder.BuildPlayerAnimation),
                },
            },

            new Group
            {
                Tab = "아트",
                Title = "아트 임포트",
                Note = "그림을 넣은 뒤 임포트 설정(Point 필터·PPU·피벗)을 맞추는 도구들.",
                Tools = new[]
                {
                    new AbyssTool(AbyssToolNames.ApplyFormSpriteImport,
                        "폼 스프라이트의 임포트 설정을 일괄 적용한다.", FormSpriteImporter.Apply),
                    new AbyssTool(AbyssToolNames.ApplyEnvironmentArtImport,
                        "배경·타일 등 환경 아트의 임포트 설정을 일괄 적용한다.", EnvironmentArtImporter.Apply),
                    new AbyssTool(AbyssToolNames.ImportWeaponSprites,
                        "고른 무기 시트를 낱장으로 자르고 피벗(자루)을 맞춘다.",
                        WeaponSpriteImporter.ImportPicked),
                    new AbyssTool(AbyssToolNames.ImportWeaponAnchorSet,
                        "손 앵커 데이터를 읽어 WeaponAnchorSet 에셋으로 만든다.",
                        WeaponAnchorImporter.Import),
                    new AbyssTool(AbyssToolNames.WireWeaponAngles,
                        "Project 에서 각도 스트립을 고른 뒤 실행 — WeaponData.angleSprites 에 꽂는다.",
                        WeaponAngleWirer.Wire),
                },
            },

            new Group
            {
                Tab = "정리",
                Title = "정리",
                Note = "놓은 것을 되돌린다. 활성 씬만 건드린다.",
                Tools = new[]
                {
                    new AbyssTool(AbyssToolNames.ClearPlatforms,
                        "임시 테스트 발판을 지운다.", PlatformBuilder.ClearTestPlatforms, isDestructive: true),
                    new AbyssTool(AbyssToolNames.ClearRoomLayouts,
                        "생성한 방 지형을 지운다.", RoomLayoutBuilder.ClearRoomLayouts, isDestructive: true),
                },
            },

            // 🔑 맨 뒤에 붙인다 — 탭 선택이 EditorPrefs 에 색인으로 남으므로, 앞에 끼우면 기존 탭이 한 칸씩 밀린다.
            new Group
            {
                Tab = "디버그",
                Title = "디버그 · 조회",
                Note = "값을 바꾸지 않고 보기만 한다. 플레이 중에 쓴다.",
                Tools = new[]
                {
                    new AbyssTool(AbyssToolNames.OpenStatInspector,
                        "선택한 플레이어·적의 스탯 창을 연다. 공격 배율을 버프·메타·무기 층으로 나눠 보여주고, 무기 강화·보유 목록도 나온다.",
                        StatInspectorWindow.Open),
                },
            },
        };

        /// <summary>검색어에 걸리는 도구만 추린다. 이름과 설명을 함께 본다(대소문자 무시).</summary>
        public static List<AbyssTool> Filter(Group group, string query)
        {
            var result = new List<AbyssTool>();
            if (group == null || group.Tools == null) return result;

            bool all = string.IsNullOrWhiteSpace(query);
            for (int i = 0; i < group.Tools.Length; i++)
            {
                var tool = group.Tools[i];
                if (all
                    || tool.Name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0
                    || tool.Summary.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    result.Add(tool);
                }
            }
            return result;
        }
    }
}
#endif
