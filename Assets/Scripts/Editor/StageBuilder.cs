#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Abyss.Runtime.Enemy;
using Abyss.Runtime.Form;
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
                "  · RoomData 6개 (Combat 5 + Boss 1)\n" +
                "  · StageData 1개 (Stage1_AbyssEntrance, 방 6개 순차)\n\n" +
                "이미 존재하는 SO는 건너뜁니다 (덮어쓰지 않음).",
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

            // 폼 보상(3번째 폼). Room3 클리어 시 제단 등장 — 미발견 시 보상 없이 진행(선택 사항).
            var rewardForm = AssetDatabase.LoadAssetAtPath<FormData>($"{AbyssPaths.Forms}/AncientShield.asset");
            if (rewardForm == null)
                Debug.LogWarning("[StageBuilder] AncientShield.asset 미발견 — Room3 폼 보상 미설정. ContentBuilder 먼저 실행 권장.");

            // Room 6개 생성
            var room1 = CreateOrLoadRoom("Room1_Intro",     RoomType.Combat, 5,
                new[] { (grunt, 2) });
            var room2 = CreateOrLoadRoom("Room2_Skirmish",  RoomType.Combat, 8,
                new[] { (grunt, 2), (archer, 1) });
            var room3 = CreateOrLoadRoom("Room3_Crowd",     RoomType.Combat, 12,
                new[] { (grunt, 3), (brute, 1) }, formReward: rewardForm);
            var room4 = CreateOrLoadRoom("Room4_Elite",     RoomType.Elite, 20,
                new[] { (elite, 1), (grunt, 2) });
            var room5 = CreateOrLoadRoom("Room5_Ambush",    RoomType.Combat, 18,
                new[] { (archer, 2), (brute, 2) });
            var room6 = CreateOrLoadRoom("Room6_Boss",      RoomType.Boss, 50,
                new[] { (boss, 1) });

            // Stage 생성
            var stage = CreateOrLoadStage("Stage1_AbyssEntrance", Stage1Id, Stage1Name,
                new[] { room1, room2, room3, room4, room5, room6 });

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
                "  · RoomData 6개 (Combat 4 + Elite 1[중간보스] + Boss 1)\n" +
                "  · StageData 1개 (Stage2_FlameCorridor, 방 6개 순차)\n" +
                "  · StageSequenceData (Stage1 → Stage2 순차 진행)\n\n" +
                "이미 존재하는 RoomData/StageData는 건너뜁니다(시퀀스는 갱신).",
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

            // Stage2 Room 6개 — Stage1 대비 적 수·강도 상향, 4번방 중간보스, 6번방 최종보스.
            var r1 = CreateOrLoadRoom("Stage2_Room1_Ignition", RoomType.Combat, 10,
                new[] { (grunt, 3) });
            var r2 = CreateOrLoadRoom("Stage2_Room2_Volley",   RoomType.Combat, 14,
                new[] { (archer, 2), (grunt, 2) });
            var r3 = CreateOrLoadRoom("Stage2_Room3_Phalanx",  RoomType.Combat, 18,
                new[] { (brute, 2), (grunt, 2) });
            var r4 = CreateOrLoadRoom("Stage2_Room4_Sentinel", RoomType.Elite, 30,
                new[] { (sentinel, 1), (brute, 1) });
            var r5 = CreateOrLoadRoom("Stage2_Room5_Gauntlet", RoomType.Combat, 24,
                new[] { (archer, 2), (brute, 2), (grunt, 2) });
            var r6 = CreateOrLoadRoom("Stage2_Room6_Serpent",  RoomType.Boss, 70,
                new[] { (serpent, 1) });

            var stage2 = CreateOrLoadStage("Stage2_FlameCorridor", Stage2Id, Stage2Name,
                new[] { r1, r2, r3, r4, r5, r6 });

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
            FormData formReward = null)
        {
            string path = $"{AbyssPaths.Rooms}/{fileName}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<RoomData>(path);
            if (existing != null)
            {
                // 기존 룸은 대부분 보존하되, formReward는 데이터 구동이므로 재실행 시 반영한다.
                if (existing.formReward != formReward)
                {
                    existing.formReward = formReward;
                    EditorUtility.SetDirty(existing);
                    Debug.Log($"[StageBuilder] formReward 갱신: {path} → {(formReward != null ? formReward.formId : "none")}");
                }
                else Debug.Log($"[StageBuilder] 건너뜀 (존재): {path}");
                return existing;
            }

            var so = ScriptableObject.CreateInstance<RoomData>();
            so.roomId = fileName.ToLowerInvariant();
            so.roomType = roomType;
            so.clearGoldReward = goldReward;
            so.formReward = formReward;
            so.enemies = entries.Select(e => new EnemySpawnEntry { data = e.data, count = e.count }).ToList();
            AssetDatabase.CreateAsset(so, path);
            Debug.Log($"[StageBuilder] 생성: {path}");
            return so;
        }

        private static StageData CreateOrLoadStage(string fileName, string stageId, string displayName, RoomData[] rooms)
        {
            string path = $"{AbyssPaths.Stages}/{fileName}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<StageData>(path);
            if (existing != null)
            {
                // 기존 StageData에 rooms만 갱신 (방 추가/순서 변경 반영)
                existing.stageId = stageId;
                existing.displayName = displayName;
                existing.rooms = rooms.ToList();
                EditorUtility.SetDirty(existing);
                Debug.Log($"[StageBuilder] rooms 갱신: {path}");
                return existing;
            }

            var so = ScriptableObject.CreateInstance<StageData>();
            so.stageId = stageId;
            so.displayName = displayName;
            so.rooms = rooms.ToList();
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
