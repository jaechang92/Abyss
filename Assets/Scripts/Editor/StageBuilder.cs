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
        private const string BuildMenu = "Tools/Abyss/Build Stage 1 Content";
        private const string SceneSetupMenu = "Tools/Abyss/Setup StageDirector in Active Scene";

        private const string EnemyDir = "Assets/Data/Enemies";
        private const string RoomDir = "Assets/Data/Rooms";
        private const string StageDir = "Assets/Data/Stages";

        private const string Stage1Id = "stage_1_abyss_entrance";
        private const string Stage1Name = "균열의 입구";

        [MenuItem(BuildMenu)]
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

            EnsureDir(RoomDir);
            EnsureDir(StageDir);

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
                    "EnemyData 누락. 먼저 'Tools/Abyss/Generate Prototype Content'를 실행하세요.",
                    "확인");
                return;
            }

            // Room 6개 생성
            var room1 = CreateOrLoadRoom("Room1_Intro",     RoomType.Combat, 5,
                new[] { (grunt, 2) });
            var room2 = CreateOrLoadRoom("Room2_Skirmish",  RoomType.Combat, 8,
                new[] { (grunt, 2), (archer, 1) });
            var room3 = CreateOrLoadRoom("Room3_Crowd",     RoomType.Combat, 12,
                new[] { (grunt, 3), (brute, 1) });
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

        [MenuItem(SceneSetupMenu)]
        public static void SetupStageDirectorInActiveScene()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.IsValid())
            {
                EditorUtility.DisplayDialog("StageBuilder", "활성 씬이 유효하지 않습니다.", "확인");
                return;
            }

            var stage = AssetDatabase.LoadAssetAtPath<StageData>($"{StageDir}/Stage1_AbyssEntrance.asset");
            if (stage == null)
            {
                EditorUtility.DisplayDialog(
                    "StageBuilder 실패",
                    "Stage1_AbyssEntrance.asset 누락. 먼저 'Tools/Abyss/Build Stage 1 Content'를 실행하세요.",
                    "확인");
                return;
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
            so.FindProperty("stage").objectReferenceValue = stage;
            so.FindProperty("startOnEnable").boolValue = true;
            so.FindProperty("delayBetweenRooms").floatValue = 2f;

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

            Debug.Log($"[StageBuilder] StageDirector 셋업 완료 — Stage='{stage.displayName}', SpawnPoints={spawnPoints.Count}개");
        }

        private static EnemyData LoadEnemyData(string fileName)
        {
            var data = AssetDatabase.LoadAssetAtPath<EnemyData>($"{EnemyDir}/{fileName}.asset");
            if (data == null) Debug.LogError($"[StageBuilder] EnemyData 누락: {EnemyDir}/{fileName}.asset");
            return data;
        }

        private static RoomData CreateOrLoadRoom(
            string fileName, RoomType roomType, int goldReward, (EnemyData data, int count)[] entries)
        {
            string path = $"{RoomDir}/{fileName}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<RoomData>(path);
            if (existing != null)
            {
                Debug.Log($"[StageBuilder] 건너뜀 (존재): {path}");
                return existing;
            }

            var so = ScriptableObject.CreateInstance<RoomData>();
            so.roomId = fileName.ToLowerInvariant();
            so.roomType = roomType;
            so.clearGoldReward = goldReward;
            so.enemies = entries.Select(e => new EnemySpawnEntry { data = e.data, count = e.count }).ToList();
            AssetDatabase.CreateAsset(so, path);
            Debug.Log($"[StageBuilder] 생성: {path}");
            return so;
        }

        private static StageData CreateOrLoadStage(string fileName, string stageId, string displayName, RoomData[] rooms)
        {
            string path = $"{StageDir}/{fileName}.asset";
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

        private static void EnsureDir(string path)
        {
            if (!Directory.Exists(path)) Directory.CreateDirectory(path);
        }
    }
}
#endif
