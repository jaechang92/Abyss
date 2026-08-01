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
    /// Stage 1 콘텐츠 일괄 생성 + 활성 씬에 StageDirector 배치 에디터 툴.
    /// 기획 01-gdd.md: 스테이지 1 = 5~7 일반 방 + 보스 방 1. 본 빌더는 6방(일반 5 + 보스 1) 구성.
    /// 이미 존재하는 SO 에셋은 덮어쓰지 않고 건너뜀.
    /// </summary>
    public static class StageBuilder
    {

        private const string Stage1Id = "stage_1_abyss_entrance";
        private const string Stage1Name = "균열의 입구";

        private const string Stage2Id = "stage_2_flame_corridor";
        private const string Stage2Name = "불꽃의 회랑";

        private const string SequenceFile = "MainRunSequence";
        private const string SequenceId = "main_run_sequence";
        private const string SequenceName = "본 런 시퀀스";

        [MenuItem(AbyssMenu.BuildStage1)]
        public static void BuildStage1Content()
        {
            bool proceed = EditorUtility.DisplayDialog(
                "StageBuilder",
                "Stage 1 콘텐츠 생성:\n" +
                "  · RoomData 8개 (Combat 5 + Event 2 + Rest 1 + Boss 1)\n" +
                "  · EventData 4종 + 휴식 2종 (Assets/Data/Events)\n" +
                "  · StageData 1개 (Stage1_AbyssEntrance, 7단계 · 분기 2곳)\n\n" +
                "이미 존재하는 SO는 건너뜁니다(분기 표시명·힌트는 갱신).",
                "생성", "취소");
            if (!proceed) return;

            EnsureDir(AbyssPaths.Rooms);
            EnsureDir(AbyssPaths.Stages);

            // EnemyData 5종 로드 (ContentBuilder 산출물)
            var grunt = LoadEnemyData("MeleeGrunt");
            var brute = LoadEnemyData("MeleeBrute");
            var archer = LoadEnemyData("RangedArcher");
            var elite = LoadEnemyData("EliteHunter");
            var boss = LoadEnemyData("BossAbyssKeeper");

            if (grunt == null || brute == null || archer == null || elite == null || boss == null)
            {
                EditorUtility.DisplayDialog(
                    "StageBuilder 실패",
                    $"EnemyData 누락. 먼저 '{AbyssMenu.GenerateContent}'를 실행하세요.",
                    "확인");
                return;
            }

            EventContentBuilder.EnsureAllEvents(out var brokenAltar, out _, out _, out var sealedDoor);
            EventContentBuilder.EnsureAllRests(out var restEmber, out _);

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
            var room4 = CreateOrLoadRoom("Room4_Elite",     RoomType.Elite, 20,
                new[] { (elite, 1), (grunt, 2) });
            var room5 = CreateOrLoadRoom("Room5_Ambush",    RoomType.Combat, 18,
                new[] { (archer, 2), (brute, 2) },
                displayName: "매복", hint: "보상 골드 18");
            var room5Alt = CreateOrLoadEventRoom("Room5_Alt_SealedDoor", sealedDoor,
                displayName: "봉인된 문", hint: "골드 또는 피가 필요하다");
            // 휴식은 보스 직전에 둔다 — "지금 내 HP로 보스를 잡을 수 있나"를 스스로 묻게 만드는 자리다.
            var roomRest = CreateOrLoadEventRoom("Room6_Rest_Ember", restEmber, RoomType.Rest);
            var room6 = CreateOrLoadRoom("Room6_Boss",      RoomType.Boss, 50,
                new[] { (boss, 1) });

            // 분기는 항상 '전투 vs 비전투'다 — 전투끼리 갈리면 고를 이유가 숫자뿐이라 선택이 되지 않는다.
            // 폼 보상·엘리트·휴식·보스는 고정이다. 놓치면 런의 밀도가 크게 달라지는 방들이다.
            var stage = CreateOrLoadStage("Stage1_AbyssEntrance", Stage1Id, Stage1Name, new[]
            {
                Step(room1),
                Step(room2, roomEvent),      // 분기 ①
                Step(room3),
                Step(room4),
                Step(room5, room5Alt),       // 분기 ②
                Step(roomRest),
                Step(room6),
            });

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorGUIUtility.PingObject(stage);
            Debug.Log("[StageBuilder] Stage 1 콘텐츠 생성 완료 — Assets/Data/Stages/Stage1_AbyssEntrance.asset");
        }

        [MenuItem(AbyssMenu.BuildStage2)]
        public static void BuildStage2Content()
        {
            bool proceed = EditorUtility.DisplayDialog(
                "StageBuilder",
                "Stage 2 '불꽃의 회랑' 콘텐츠 생성:\n" +
                "  · RoomData 10개 (Combat 5 + Event 2 + Rest 1 + Elite 1[중간보스] + Boss 1)\n" +
                "  · EventData 4종 + 휴식 2종 (Assets/Data/Events)\n" +
                "  · StageData 1개 (Stage2_FlameCorridor, 8단계 · 분기 2곳)\n" +
                "  · StageSequenceData (Stage1 → Stage2 순차 진행)\n\n" +
                "이미 존재하는 RoomData/StageData는 건너뜁니다(분기 표시·시퀀스는 갱신).",
                "생성", "취소");
            if (!proceed) return;

            EnsureDir(AbyssPaths.Rooms);
            EnsureDir(AbyssPaths.Stages);

            // Stage1 기존 적 4종 + Stage2 신규 적 2종 로드 (ContentBuilder 산출물).
            var grunt = LoadEnemyData("MeleeGrunt");
            var brute = LoadEnemyData("MeleeBrute");
            var archer = LoadEnemyData("RangedArcher");
            var sentinel = LoadEnemyData("MidBossSentinel");
            var serpent = LoadEnemyData("BossFlameSerpent");

            if (grunt == null || brute == null || archer == null || sentinel == null || serpent == null)
            {
                EditorUtility.DisplayDialog(
                    "StageBuilder 실패",
                    $"EnemyData 누락(Stage2 신규 적 포함). 먼저 '{AbyssMenu.GenerateContent}'를 실행하세요.",
                    "확인");
                return;
            }

            EventContentBuilder.EnsureAllEvents(out _, out var forgottenCache, out var abyssalSpring, out _);
            EventContentBuilder.EnsureAllRests(out _, out var restCamp);

            // Stage1 대비 적 수·강도 상향, 중간보스 1, 최종보스 1.
            // 분기 2곳은 중간보스를 앞뒤로 감싼다 — 대비(보급 vs 골드)와 수습(회복 vs 골드)이라
            // 같은 갈림길이라도 묻는 것이 다르다.
            var r1 = CreateOrLoadRoom("Stage2_Room1_Ignition", RoomType.Combat, 10,
                new[] { (grunt, 3) });
            var r2 = CreateOrLoadRoom("Stage2_Room2_Volley",   RoomType.Combat, 14,
                new[] { (archer, 2), (grunt, 2) });
            var r3 = CreateOrLoadRoom("Stage2_Room3_Phalanx",  RoomType.Combat, 18,
                new[] { (brute, 2), (grunt, 2) }, hasFormReward: true);
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
            var rRest = CreateOrLoadEventRoom("Stage2_Room6_Rest_Camp", restCamp, RoomType.Rest);
            var r6 = CreateOrLoadRoom("Stage2_Room6_Serpent",  RoomType.Boss, 70,
                new[] { (serpent, 1) });

            var stage2 = CreateOrLoadStage("Stage2_FlameCorridor", Stage2Id, Stage2Name, new[]
            {
                Step(r1),
                Step(r2),
                Step(r3),
                Step(rEventCache, r5),       // 분기 ① — 중간보스 대비
                Step(r4),
                Step(rEventSpring, r6Alt),   // 분기 ② — 중간보스 수습
                Step(rRest),
                Step(r6),
            });

            // Stage1 로드 후 멀티 스테이지 시퀀스(Stage1 → Stage2) 생성/갱신.
            var stage1 = AssetDatabase.LoadAssetAtPath<StageData>($"{AbyssPaths.Stages}/Stage1_AbyssEntrance.asset");
            if (stage1 == null)
            {
                Debug.LogWarning("[StageBuilder] Stage1_AbyssEntrance.asset 누락 — 시퀀스에 Stage2만 포함됩니다. " +
                                 "먼저 'Build Stage 1 Content' 권장.");
            }
            var sequence = CreateOrUpdateSequence(stage1, stage2);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorGUIUtility.PingObject(sequence);
            Debug.Log($"[StageBuilder] Stage 2 콘텐츠 + 시퀀스 생성 완료 — " +
                      $"{AbyssPaths.Stages}/Stage2_FlameCorridor.asset, 시퀀스 {sequence.stages.Count} 스테이지");
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

            // 멀티 스테이지 시퀀스 우선 로드. 없으면 Stage1 단독으로 시퀀스 자동 생성(하위호환).
            var sequence = AssetDatabase.LoadAssetAtPath<StageSequenceData>($"{AbyssPaths.Stages}/{SequenceFile}.asset");
            if (sequence == null)
            {
                var stage1 = AssetDatabase.LoadAssetAtPath<StageData>($"{AbyssPaths.Stages}/Stage1_AbyssEntrance.asset");
                if (stage1 == null)
                {
                    EditorUtility.DisplayDialog(
                        "StageBuilder 실패",
                        $"StageSequenceData·Stage1 모두 누락. 먼저 '{AbyssMenu.BuildStage1}'를 실행하세요.",
                        "확인");
                    return;
                }
                sequence = CreateOrUpdateSequence(stage1);
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

        private static EnemyData LoadEnemyData(string fileName)
        {
            var data = AssetDatabase.LoadAssetAtPath<EnemyData>($"{AbyssPaths.Enemies}/{fileName}.asset");
            if (data == null) Debug.LogError($"[StageBuilder] EnemyData 누락: {AbyssPaths.Enemies}/{fileName}.asset");
            return data;
        }

        private static RoomData CreateOrLoadRoom(
            string fileName, RoomType roomType, int goldReward, (EnemyData data, int count)[] entries,
            bool hasFormReward = false, string displayName = null, string hint = null)
        {
            string path = $"{AbyssPaths.Rooms}/{fileName}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<RoomData>(path);
            if (existing != null)
            {
                // 기존 룸은 대부분 보존하되, 보상 룸 지정은 데이터 구동이므로 재실행 시 반영한다.
                // 레거시 고정 폼(formReward)은 비워 StageDirector의 미보유 우선 추첨으로 넘긴다.
                if (existing.hasFormReward != hasFormReward || existing.formReward != null)
                {
                    existing.hasFormReward = hasFormReward;
                    existing.formReward = null;
                    EditorUtility.SetDirty(existing);
                    Debug.Log($"[StageBuilder] 폼 보상 룸 갱신: {path} → {(hasFormReward ? "보상 룸(추첨)" : "none")}");
                }
                ApplyChoiceDisplay(existing, path, displayName, hint);
                return existing;
            }

            var so = ScriptableObject.CreateInstance<RoomData>();
            so.roomId = fileName.ToLowerInvariant();
            so.roomType = roomType;
            so.clearGoldReward = goldReward;
            so.hasFormReward = hasFormReward;
            so.displayName = displayName;
            so.hint = hint;
            so.enemies = entries.Select(e => new EnemySpawnEntry { data = e.data, count = e.count }).ToList();
            AssetDatabase.CreateAsset(so, path);
            Debug.Log($"[StageBuilder] 생성: {path}");
            return so;
        }

        /// <summary>
        /// 분기 선택지 표시 정보를 기존 에셋에 반영한다.
        /// 방 배치가 바뀌면 어떤 방이 선택지로 노출되는지도 바뀌므로 **재실행으로 갱신되어야 한다**
        /// (폼 보상 지정과 같은 규약). 인자가 null이면 손대지 않는다 — 인스펙터에서 손으로 적은 문구를
        /// 빌더가 지우면 안 된다.
        /// </summary>
        private static void ApplyChoiceDisplay(RoomData room, string path, string displayName, string hint)
        {
            bool changed = false;
            if (displayName != null && room.displayName != displayName)
            {
                room.displayName = displayName;
                changed = true;
            }
            if (hint != null && room.hint != hint)
            {
                room.hint = hint;
                changed = true;
            }

            if (changed)
            {
                EditorUtility.SetDirty(room);
                Debug.Log($"[StageBuilder] 선택지 표시 갱신: {path} → \"{room.ChoiceTitle}\" / \"{room.hint}\"");
            }
            else Debug.Log($"[StageBuilder] 건너뜀 (존재): {path}");
        }

        /// <summary>
        /// 이벤트 방. 적을 두지 않는다 — 적이 있으면 전투가 끝나야 이벤트가 열려 '비전투 방'이 아니게 된다.
        /// 클리어 골드도 0이다(보상은 이벤트 선택이 준다).
        ///
        /// 기존 에셋이 있으면 eventData 참조만 갱신한다. 방 배치는 데이터 구동이라 재실행으로 반영되어야
        /// 하지만, 인스펙터에서 다른 값을 만졌을 수 있어 전체를 덮어쓰지는 않는다(폼 보상 룸과 같은 규약).
        /// </summary>
        private static RoomData CreateOrLoadEventRoom(
            string fileName, EventData eventData, RoomType roomType = RoomType.Event,
            string displayName = null, string hint = null)
        {
            string path = $"{AbyssPaths.Rooms}/{fileName}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<RoomData>(path);
            if (existing != null)
            {
                if (existing.eventData != eventData || existing.roomType != roomType)
                {
                    existing.eventData = eventData;
                    existing.roomType = roomType;
                    EditorUtility.SetDirty(existing);
                    Debug.Log($"[StageBuilder] {roomType} 룸 갱신: {path} → {(eventData != null ? eventData.eventId : "none")}");
                }
                ApplyChoiceDisplay(existing, path, displayName, hint);
                return existing;
            }

            var so = ScriptableObject.CreateInstance<RoomData>();
            so.roomId = fileName.ToLowerInvariant();
            so.roomType = roomType;
            so.clearGoldReward = 0;
            so.eventData = eventData;
            so.displayName = displayName;
            so.hint = hint;
            so.enemies = new List<EnemySpawnEntry>();
            AssetDatabase.CreateAsset(so, path);
            Debug.Log($"[StageBuilder] 생성: {path}");
            return so;
        }

        /// <summary>
        /// 진행 단계 하나. 방 1개면 고정 진행, 2개 이상이면 갈림길이다.
        /// 호출부에서 <c>Step(a)</c> / <c>Step(a, b)</c>로 읽히도록 params로 받는다.
        /// </summary>
        private static StageStep Step(params RoomData[] options)
        {
            return new StageStep { options = options.Where(r => r != null).ToList() };
        }

        private static StageData CreateOrLoadStage(string fileName, string stageId, string displayName, StageStep[] steps)
        {
            string path = $"{AbyssPaths.Stages}/{fileName}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<StageData>(path);
            if (existing != null)
            {
                // 기존 StageData에 steps만 갱신 (방 추가·순서 변경·분기 구성 반영)
                existing.stageId = stageId;
                existing.displayName = displayName;
                existing.steps = steps.ToList();
                EditorUtility.SetDirty(existing);
                Debug.Log($"[StageBuilder] steps 갱신: {path} ({steps.Length}단계)");
                return existing;
            }

            var so = ScriptableObject.CreateInstance<StageData>();
            so.stageId = stageId;
            so.displayName = displayName;
            so.steps = steps.ToList();
            AssetDatabase.CreateAsset(so, path);
            Debug.Log($"[StageBuilder] 생성: {path}");
            return so;
        }

        /// <summary>
        /// 멀티 스테이지 시퀀스 SO를 생성하거나(없으면), 기존 시퀀스의 stages 목록을 갱신한다.
        /// null 스테이지는 자동 제외(누락 자산 방어). 기존 SO는 식별자·목록만 덮어쓴다(멱등).
        /// </summary>
        private static StageSequenceData CreateOrUpdateSequence(params StageData[] stages)
        {
            var list = stages.Where(s => s != null).ToList();
            string path = $"{AbyssPaths.Stages}/{SequenceFile}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<StageSequenceData>(path);
            if (existing != null)
            {
                existing.sequenceId = SequenceId;
                existing.displayName = SequenceName;
                existing.stages = list;
                EditorUtility.SetDirty(existing);
                Debug.Log($"[StageBuilder] 시퀀스 갱신: {path} ({list.Count} 스테이지)");
                return existing;
            }

            var so = ScriptableObject.CreateInstance<StageSequenceData>();
            so.sequenceId = SequenceId;
            so.displayName = SequenceName;
            so.stages = list;
            AssetDatabase.CreateAsset(so, path);
            Debug.Log($"[StageBuilder] 시퀀스 생성: {path} ({list.Count} 스테이지)");
            return so;
        }

        private static void EnsureDir(string path)
        {
            if (!Directory.Exists(path)) Directory.CreateDirectory(path);
        }
    }
}
#endif
