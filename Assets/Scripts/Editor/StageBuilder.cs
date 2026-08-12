#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Abyss.Runtime.Enemy;
using Abyss.Runtime.Stage;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Abyss.EditorTools
{
    /// <summary>
    /// 스테이지 콘텐츠 일괄 생성 + 활성 씬에 StageDirector 배치 에디터 툴.
    /// 여기는 <b>메뉴 진입점 — 어떤 방을 어떤 순서·분기로 둘 것인가</b>만 담는다.
    /// SO를 실제로 만들고 갱신하는 헬퍼는 <c>StageBuilder.Assets.cs</c>(partial)에 있다.
    ///
    /// 방 구성은 전투·엘리트·보스에 비전투 방(이벤트·상점·휴식)이 더해져
    /// Stage1 8단계 / Stage2 9단계 / Stage3 9단계다(각 분기 2곳).
    /// 이미 존재하는 SO 에셋은 덮어쓰지 않고 건너뛴다.
    ///
    /// 세 빌더는 <b>아무 순서로 여러 번 돌려도 결과가 같다</b> — 시퀀스는 인자가 아니라
    /// 디스크에 있는 스테이지 에셋에서 재구성한다(<c>RebuildSequence</c>).
    /// </summary>
    public static partial class StageBuilder
    {

        private const string Stage1File = "Stage1_AbyssEntrance";
        private const string Stage1Id = "stage_1_abyss_entrance";
        private const string Stage1Name = "균열의 입구";

        private const string Stage2File = "Stage2_FlameCorridor";
        private const string Stage2Id = "stage_2_flame_corridor";
        private const string Stage2Name = "불꽃의 회랑";

        private const string Stage3File = "Stage3_ThroneRuins";
        private const string Stage3Id = "stage_3_throne_ruins";
        private const string Stage3Name = "왕좌의 잔해";

        /// <summary>런 진행 순서. 시퀀스는 이 배열을 그대로 따른다(디스크에 있는 것만 포함).</summary>
        private static readonly string[] StageFilesInOrder = { Stage1File, Stage2File, Stage3File };

        private const string SequenceFile = "MainRunSequence";
        private const string SequenceId = "main_run_sequence";
        private const string SequenceName = "본 런 시퀀스";

        [MenuItem(AbyssMenu.BuildStage1)]
        public static void BuildStage1Content()
        {
            bool proceed = EditorUtility.DisplayDialog(
                "StageBuilder",
                "Stage 1 콘텐츠 생성:\n" +
                "  · RoomData 10개 (Combat 6 + Event 2 + Shop 1 + Rest 1 + Boss 1)\n" +
                "  · EventData 6종 + 휴식 3종 (Assets/Data/Events)\n" +
                "  · ShopData 3종 (Assets/Data/Shops)\n" +
                "  · StageData 1개 (Stage1_AbyssEntrance, 9단계 · 분기 2곳)\n\n" +
                "이미 존재하는 SO는 건너뜁니다(분기 표시명·힌트는 갱신).",
                "생성", "취소");
            if (!proceed) return;

            EnsureDir(AbyssPaths.Rooms);
            EnsureDir(AbyssPaths.Stages);

            // EnemyData 5종 로드 (ContentBuilder 산출물)
            var grunt = LoadEnemyData("MeleeGrunt");
            var brute = LoadEnemyData("MeleeBrute");
            var archer = LoadEnemyData("RangedArcher");
            var boneArcher = LoadEnemyData("BoneArcher");
            var elite = LoadEnemyData("EliteHunter");
            var boss = LoadEnemyData("BossAbyssKeeper");

            if (grunt == null || brute == null || archer == null || boneArcher == null || elite == null || boss == null)
            {
                EditorUtility.DisplayDialog(
                    "StageBuilder 실패",
                    $"EnemyData 누락. 먼저 '{AbyssMenu.GenerateContent}'를 실행하세요.",
                    "확인");
                return;
            }

            EventContentBuilder.EnsureAllEvents(out var brokenAltar, out _, out _, out var sealedDoor, out _, out _);
            EventContentBuilder.EnsureAllRests(out var restEmber, out _, out _);
            ShopContentBuilder.EnsureAllShops(out var peddler, out _, out _);

            // 방 생성. Room3_Crowd는 폼 보상 룸(제시 폼은 StageDirector가 FormCatalog에서 미보유 우선 추첨).
            // 분기 선택지로 노출되는 방에는 표시명·힌트를 붙인다 — 선택 근거가 화면에 있어야 갈림길이 성립한다.
            var room1 = CreateOrLoadRoom("Room1_Intro",     RoomType.Combat, 5,
                new[] { (grunt, 2) });
            var room2 = CreateOrLoadRoom("Room2_Skirmish",  RoomType.Combat, 8,
                new[] { (grunt, 2), (archer, 1) },
                displayName: "소규모 교전", hint: "보상 골드 8");
            var roomEvent = CreateOrLoadEventRoom("Room3_Event_BrokenAltar", brokenAltar,
                displayName: "깨진 제단", hint: "대가를 치르면 응답한다");
            var room3 = CreateOrLoadRoom("Room3_Crowd",     RoomType.Combat, 12,
                new[] { (grunt, 3), (brute, 1) }, hasFormReward: true);
            // 뼈 궁수 도입 방. 폼 보상 직후에 둔다 — 새 폼을 쥔 직후가 새 위협을 배우기 좋은 자리다.
            // 근접 병사를 하나 섞어 "쫓아오는 것을 상대하며 먼 것을 처리"라는 이 적의 과제를 만든다.
            var roomBone = CreateOrLoadRoom("Room4_Bonefield", RoomType.Combat, 14,
                new[] { (boneArcher, 2), (grunt, 1) });
            var room4 = CreateOrLoadRoom("Room4_Elite",     RoomType.Elite, 20,
                new[] { (elite, 1), (grunt, 2) });
            var room5 = CreateOrLoadRoom("Room5_Ambush",    RoomType.Combat, 18,
                new[] { (archer, 2), (brute, 2) },
                displayName: "매복", hint: "보상 골드 18");
            var room5Alt = CreateOrLoadEventRoom("Room5_Alt_SealedDoor", sealedDoor,
                displayName: "봉인된 문", hint: "골드 또는 피가 필요하다");
            // 상점은 엘리트·2번째 분기를 지난 뒤, 휴식 앞에 둔다 — 골드가 가장 많이 모인 시점이고,
            // 보스 직전 '준비 구간(상점 → 휴식)'을 만들어 무엇을 사고 어떻게 쉴지 한 묶음으로 계획하게 한다.
            // 분기에 걸지 않고 고정한 이유: 상점은 골드의 유일한 소비처라 지나칠 수 있으면 화폐가 다시 죽는다.
            var roomShop = CreateOrLoadShopRoom("Room6_Shop_Peddler", peddler);
            // 휴식은 보스 직전에 둔다 — "지금 내 HP로 보스를 잡을 수 있나"를 스스로 묻게 만드는 자리다.
            var roomRest = CreateOrLoadEventRoom("Room6_Rest_Ember", restEmber, RoomType.Rest);
            var room6 = CreateOrLoadRoom("Room6_Boss",      RoomType.Boss, 50,
                new[] { (boss, 1) });

            // 분기는 항상 '전투 vs 비전투'다 — 전투끼리 갈리면 고를 이유가 숫자뿐이라 선택이 되지 않는다.
            // 폼 보상·엘리트·상점·휴식·보스는 고정이다. 놓치면 런의 밀도가 크게 달라지는 방들이다.
            var stage = CreateOrLoadStage(Stage1File, Stage1Id, Stage1Name, new[]
            {
                Step(room1),
                Step(room2, roomEvent),      // 분기 ①
                Step(room3),
                Step(roomBone),              // 뼈 궁수 도입
                Step(room4),
                Step(room5, room5Alt),       // 분기 ②
                Step(roomShop),
                Step(roomRest),
                Step(room6),
            });

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorGUIUtility.PingObject(stage);
            Debug.Log($"[StageBuilder] Stage 1 콘텐츠 생성 완료 — {AbyssPaths.Stages}/{Stage1File}.asset");
        }

        [MenuItem(AbyssMenu.BuildStage2)]
        public static void BuildStage2Content()
        {
            bool proceed = EditorUtility.DisplayDialog(
                "StageBuilder",
                "Stage 2 '불꽃의 회랑' 콘텐츠 생성:\n" +
                "  · RoomData 12개 (Combat 6 + Event 2 + Shop 1 + Rest 1 + Elite 1[중간보스] + Boss 1)\n" +
                "  · EventData 6종 + 휴식 3종 (Assets/Data/Events)\n" +
                "  · ShopData 3종 (Assets/Data/Shops)\n" +
                "  · StageData 1개 (Stage2_FlameCorridor, 10단계 · 분기 2곳)\n" +
                "  · StageSequenceData (디스크의 스테이지를 순서대로 재구성)\n\n" +
                "이미 존재하는 RoomData/StageData는 건너뜁니다(분기 표시·시퀀스는 갱신).",
                "생성", "취소");
            if (!proceed) return;

            EnsureDir(AbyssPaths.Rooms);
            EnsureDir(AbyssPaths.Stages);

            // Stage1 기존 적 4종 + Stage2 신규 적 2종 로드 (ContentBuilder 산출물).
            var grunt = LoadEnemyData("MeleeGrunt");
            var brute = LoadEnemyData("MeleeBrute");
            var archer = LoadEnemyData("RangedArcher");
            var caster = LoadEnemyData("VoidCaster");
            var sentinel = LoadEnemyData("MidBossSentinel");
            var serpent = LoadEnemyData("BossFlameSerpent");

            if (grunt == null || brute == null || archer == null || caster == null || sentinel == null || serpent == null)
            {
                EditorUtility.DisplayDialog(
                    "StageBuilder 실패",
                    $"EnemyData 누락(Stage2 신규 적 포함). 먼저 '{AbyssMenu.GenerateContent}'를 실행하세요.",
                    "확인");
                return;
            }

            EventContentBuilder.EnsureAllEvents(out _, out var forgottenCache, out var abyssalSpring, out _, out _, out _);
            EventContentBuilder.EnsureAllRests(out _, out var restCamp, out _);
            ShopContentBuilder.EnsureAllShops(out _, out var ashTrader, out _);

            // Stage1 대비 적 수·강도 상향, 중간보스 1, 최종보스 1.
            // 분기 2곳은 중간보스를 앞뒤로 감싼다 — 대비(보급 vs 골드)와 수습(회복 vs 골드)이라
            // 같은 갈림길이라도 묻는 것이 다르다.
            var r1 = CreateOrLoadRoom("Stage2_Room1_Ignition", RoomType.Combat, 10,
                new[] { (grunt, 3) });
            var r2 = CreateOrLoadRoom("Stage2_Room2_Volley",   RoomType.Combat, 14,
                new[] { (archer, 2), (grunt, 2) });
            var r3 = CreateOrLoadRoom("Stage2_Room3_Phalanx",  RoomType.Combat, 18,
                new[] { (brute, 2), (grunt, 2) }, hasFormReward: true);
            // 공허 술사 도입. 3연사는 "계속 움직여라"를 가르치는데, 중장 강적이 함께 있으면
            // 멈춰 서서 때릴 수밖에 없어 교훈이 뒤집힌다 — 그래서 근접은 빠른 쪽(근접 병사)만 붙였다.
            var rCaster = CreateOrLoadRoom("Stage2_Room3_Caster", RoomType.Combat, 20,
                new[] { (caster, 2), (grunt, 2) });
            var rEventCache = CreateOrLoadEventRoom("Stage2_Room4_Event_Cache", forgottenCache,
                displayName: "잊힌 보급함", hint: "중간보스 전 보급");
            var r5 = CreateOrLoadRoom("Stage2_Room5_Gauntlet", RoomType.Combat, 24,
                new[] { (archer, 2), (brute, 2), (grunt, 2) },
                displayName: "연속 교전", hint: "보상 골드 24");
            var r4 = CreateOrLoadRoom("Stage2_Room4_Sentinel", RoomType.Elite, 30,
                new[] { (sentinel, 1), (brute, 1) });
            var rEventSpring = CreateOrLoadEventRoom("Stage2_Room5_Event_Spring", abyssalSpring,
                displayName: "심연의 샘", hint: "회복 또는 대가");
            var r6Alt = CreateOrLoadRoom("Stage2_Room6_Alt_Emberfall", RoomType.Combat, 28,
                new[] { (archer, 3), (brute, 1) },
                displayName: "잿불 낙하", hint: "보상 골드 28");
            // Stage1과 같은 자리(2번째 분기 뒤 · 휴식 앞). 가격은 더 비싸다 — 이 구간의 골드 수입이 2배 이상이라
            // 같은 값이면 뒤로 갈수록 상점이 사실상 공짜가 된다.
            var rShop = CreateOrLoadShopRoom("Stage2_Room6_Shop_AshTrader", ashTrader);
            var rRest = CreateOrLoadEventRoom("Stage2_Room6_Rest_Camp", restCamp, RoomType.Rest);
            var r6 = CreateOrLoadRoom("Stage2_Room6_Serpent",  RoomType.Boss, 70,
                new[] { (serpent, 1) });

            var stage2 = CreateOrLoadStage(Stage2File, Stage2Id, Stage2Name, new[]
            {
                Step(r1),
                Step(r2),
                Step(r3),
                Step(rCaster),               // 공허 술사 도입
                Step(rEventCache, r5),       // 분기 ① — 중간보스 대비
                Step(r4),
                Step(rEventSpring, r6Alt),   // 분기 ② — 중간보스 수습
                Step(rShop),
                Step(rRest),
                Step(r6),
            });

            var sequence = RebuildSequence();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorGUIUtility.PingObject(sequence);
            Debug.Log($"[StageBuilder] Stage 2 콘텐츠 + 시퀀스 생성 완료 — " +
                      $"{AbyssPaths.Stages}/{Stage2File}.asset, 시퀀스 {sequence.stages.Count} 스테이지");
        }

        [MenuItem(AbyssMenu.BuildStage3)]
        public static void BuildStage3Content()
        {
            bool proceed = EditorUtility.DisplayDialog(
                "StageBuilder",
                "Stage 3 '왕좌의 잔해' 콘텐츠 생성:\n" +
                "  · RoomData 12개 (Combat 6 + Event 2 + Shop 1 + Rest 1 + Elite 1[중간보스] + Boss 1)\n" +
                "  · EventData 6종 + 휴식 3종 (Assets/Data/Events)\n" +
                "  · ShopData 3종 (Assets/Data/Shops)\n" +
                "  · StageData 1개 (Stage3_ThroneRuins, 10단계 · 분기 2곳)\n" +
                "  · StageSequenceData (디스크의 스테이지를 순서대로 재구성)\n\n" +
                "먼저 'Prototype Content'와 'Prototype Prefabs'를 실행해\n" +
                "MidBossThroneWarden·BossThronebound가 만들어져 있어야 합니다.\n\n" +
                "이미 존재하는 RoomData/StageData는 건너뜁니다(분기 표시·시퀀스는 갱신).",
                "생성", "취소");
            if (!proceed) return;

            EnsureDir(AbyssPaths.Rooms);
            EnsureDir(AbyssPaths.Stages);

            var grunt = LoadEnemyData("MeleeGrunt");
            var brute = LoadEnemyData("MeleeBrute");
            var archer = LoadEnemyData("RangedArcher");
            var mortar = LoadEnemyData("FlameMortar");
            var elite = LoadEnemyData("EliteHunter");
            var warden = LoadEnemyData("MidBossThroneWarden");
            var thronebound = LoadEnemyData("BossThronebound");

            if (grunt == null || brute == null || archer == null || mortar == null || elite == null || warden == null || thronebound == null)
            {
                EditorUtility.DisplayDialog(
                    "StageBuilder 실패",
                    $"EnemyData 누락(Stage3 신규 적 포함). 먼저 '{AbyssMenu.GenerateContent}'를 실행하세요.",
                    "확인");
                return;
            }

            EventContentBuilder.EnsureAllEvents(out _, out _, out _, out _, out var hollowCrown, out var oathStone);
            EventContentBuilder.EnsureAllRests(out _, out _, out var restThrone);
            ShopContentBuilder.EnsureAllShops(out _, out _, out var graveRobber);

            // 마지막 스테이지다. 적 밀도를 Stage2보다 올리고 엘리트를 일반 방에도 섞는다
            // — 여기까지 온 빌드는 이미 스킬 4~6개를 갖췄으므로 앞 스테이지 강도면 밋밋하다.
            var r1 = CreateOrLoadRoom("Stage3_Room1_Gate",      RoomType.Combat, 14,
                new[] { (grunt, 3), (archer, 1) });
            var r2 = CreateOrLoadRoom("Stage3_Room2_Corridor",  RoomType.Combat, 18,
                new[] { (brute, 2), (archer, 2) });
            var r3 = CreateOrLoadRoom("Stage3_Room3_Court",     RoomType.Combat, 22,
                new[] { (grunt, 3), (brute, 2) }, hasFormReward: true);
            // 화염 박격포 도입. 곡사는 붙으면 무력해지므로 "먼저 도달해야 하는 적"이고,
            // 중장 강적을 앞에 세워 그 접근을 방해한다 — 이 방의 과제가 곧 이 적의 공략법이다.
            var rMortar = CreateOrLoadRoom("Stage3_Room3_Bombard", RoomType.Combat, 26,
                new[] { (mortar, 2), (brute, 1) });
            var rEventCrown = CreateOrLoadEventRoom("Stage3_Room4_Event_Crown", hollowCrown,
                displayName: "빈 왕관", hint: "머리를 내주면 값이 크다");
            var r4Alt = CreateOrLoadRoom("Stage3_Room4_Alt_Guard", RoomType.Combat, 30,
                new[] { (elite, 1), (archer, 2) },
                displayName: "근위대 잔당", hint: "보상 골드 30");
            var rWarden = CreateOrLoadRoom("Stage3_Room5_Warden", RoomType.Elite, 40,
                new[] { (warden, 1), (brute, 2) });
            var rEventOath = CreateOrLoadEventRoom("Stage3_Room6_Event_Oath", oathStone,
                displayName: "맹세의 돌", hint: "회복 또는 골드 대가");
            var r6Alt = CreateOrLoadRoom("Stage3_Room6_Alt_Hall",  RoomType.Combat, 34,
                new[] { (brute, 3), (archer, 2) },
                displayName: "대회랑", hint: "보상 골드 34");
            var rShop = CreateOrLoadShopRoom("Stage3_Room7_Shop_GraveRobber", graveRobber);
            var rRest = CreateOrLoadEventRoom("Stage3_Room7_Rest_ThroneHall", restThrone, RoomType.Rest);
            var r7 = CreateOrLoadRoom("Stage3_Room8_Thronebound", RoomType.Boss, 100,
                new[] { (thronebound, 1) });

            // 구조는 Stage1·2와 같다(분기 2곳 · 중간보스를 앞뒤로 감쌈 · 상점 → 휴식 → 보스).
            // 마지막 스테이지라고 형식을 바꾸지 않는다 — 여기까지 익힌 리듬을 그대로 쓰게 두는 편이
            // 최종 보스에 집중하기 좋다.
            var stage3 = CreateOrLoadStage(Stage3File, Stage3Id, Stage3Name, new[]
            {
                Step(r1),
                Step(r2),
                Step(r3),
                Step(rMortar),               // 화염 박격포 도입
                Step(rEventCrown, r4Alt),    // 분기 ① — 중간보스 대비
                Step(rWarden),
                Step(rEventOath, r6Alt),     // 분기 ② — 중간보스 수습
                Step(rShop),
                Step(rRest),
                Step(r7),
            });

            var sequence = RebuildSequence();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorGUIUtility.PingObject(stage3);
            Debug.Log($"[StageBuilder] Stage 3 콘텐츠 + 시퀀스 생성 완료 — " +
                      $"{AbyssPaths.Stages}/{Stage3File}.asset, 시퀀스 {sequence.stages.Count} 스테이지");
        }

        [MenuItem(AbyssMenu.BuildStageDirector)]
        public static void SetupStageDirectorInActiveScene()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.IsValid())
            {
                EditorUtility.DisplayDialog("StageBuilder", "활성 씬이 유효하지 않습니다.", "확인");
                return;
            }

            // 멀티 스테이지 시퀀스 우선 로드. 없으면 디스크의 스테이지로 자동 구성(하위호환).
            var sequence = AssetDatabase.LoadAssetAtPath<StageSequenceData>($"{AbyssPaths.Stages}/{SequenceFile}.asset");
            if (sequence == null)
            {
                sequence = RebuildSequence();
                if (sequence.stages.Count == 0)
                {
                    EditorUtility.DisplayDialog(
                        "StageBuilder 실패",
                        $"StageSequenceData·StageData 모두 누락. 먼저 '{AbyssMenu.BuildStage1}'를 실행하세요.",
                        "확인");
                    return;
                }
                AssetDatabase.SaveAssets();
            }

            // 기존 StageDirector 검색
            var existing = UnityEngine.Object.FindAnyObjectByType<StageDirector>();
            GameObject directorGo;
            if (existing != null)
            {
                directorGo = existing.gameObject;
                Debug.Log($"[StageBuilder] 기존 StageDirector 재사용: {directorGo.name}");
            }
            else
            {
                directorGo = new GameObject("StageDirector");
                directorGo.AddComponent<StageDirector>();
                Undo.RegisterCreatedObjectUndo(directorGo, "Create StageDirector");
                Debug.Log("[StageBuilder] StageDirector GameObject 신규 생성");
            }

            // SpawnPoints 자식 5개 생성 (없을 때만)
            var spawnRoot = directorGo.transform.Find("SpawnPoints");
            if (spawnRoot == null)
            {
                var go = new GameObject("SpawnPoints");
                Undo.RegisterCreatedObjectUndo(go, "Create SpawnPoints root");
                spawnRoot = go.transform;
                spawnRoot.SetParent(directorGo.transform, false);
            }

            var spawnPoints = new List<Transform>();
            float[] xPositions = { -6f, -3f, 0f, 3f, 6f };
            for (int i = 0; i < xPositions.Length; i++)
            {
                string spName = $"Spawn_{i + 1:D2}";
                var sp = spawnRoot.Find(spName);
                if (sp == null)
                {
                    var go = new GameObject(spName);
                    Undo.RegisterCreatedObjectUndo(go, $"Create {spName}");
                    sp = go.transform;
                    sp.SetParent(spawnRoot, false);
                }
                sp.localPosition = new Vector3(xPositions[i], 0f, 0f);
                spawnPoints.Add(sp);
            }

            // SerializedObject로 private SerializeField 와이어링
            var director = directorGo.GetComponent<StageDirector>();
            var so = new SerializedObject(director);
            so.FindProperty("sequence").objectReferenceValue = sequence;
            so.FindProperty("startOnEnable").boolValue = true;
            so.FindProperty("delayBetweenRooms").floatValue = 2f;
            so.FindProperty("delayBetweenStages").floatValue = 3f;

            var spArrayProp = so.FindProperty("spawnPoints");
            spArrayProp.arraySize = spawnPoints.Count;
            for (int i = 0; i < spawnPoints.Count; i++)
            {
                spArrayProp.GetArrayElementAtIndex(i).objectReferenceValue = spawnPoints[i];
            }
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(director);

            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = directorGo;
            EditorGUIUtility.PingObject(directorGo);

            // 기존 수동 배치 적 안내
            var manualEnemies = UnityEngine.Object.FindObjectsByType<EnemyBase>(FindObjectsSortMode.None)
                .Where(e => e.transform.root != directorGo.transform)
                .ToArray();
            if (manualEnemies.Length > 0)
            {
                Debug.LogWarning($"[StageBuilder] 씬에 수동 배치된 적 {manualEnemies.Length}개 감지. " +
                                 "StageDirector가 자동 스폰하므로 수동 인스턴스는 제거 권장.");
            }

            Debug.Log($"[StageBuilder] StageDirector 셋업 완료 — Sequence='{sequence.displayName}' ({sequence.stages.Count} 스테이지), SpawnPoints={spawnPoints.Count}개");
        }
    }
}
#endif
