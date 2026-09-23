#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using Abyss.Runtime.Camera;
using Abyss.Runtime.Stage;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Abyss.EditorTools
{
    /// <summary>
    /// <see cref="ApplyToRunScene"/> 결과 검증 — <b>읽기 전용</b>(씬을 저장하지 않는다. 표현 전환 시뮬레이션 뒤 씬을 다시 연다).
    /// <c>-executeMethod Abyss.EditorTools.Stage1EnvironmentWiring.ValidateRunSceneBatch</c> · 실패 시 종료 코드 1.
    /// 로그 접두어 <c>[C2-ENV]</c> 한 줄에 항목 하나 — PASS/FAIL 을 세면 된다.
    /// </summary>
    public static partial class Stage1EnvironmentWiring
    {
        private const string SequenceAssetPath = AbyssPaths.Stages + "/MainRunSequence.asset";
        private const float Epsilon = 0.001f;

        public static void ValidateRunSceneBatch()
        {
            bool isOk = ValidateRunScene();
            if (Application.isBatchMode) EditorApplication.Exit(isOk ? 0 : 1);
        }

        public static bool ValidateRunScene()
        {
            var report = new C2Report("C2-ENV");

            var stage = AssetDatabase.LoadAssetAtPath<StageData>(StageAssetPath);
            var bossRoom = AssetDatabase.LoadAssetAtPath<RoomData>(BossRoomAssetPath);
            var sequence = AssetDatabase.LoadAssetAtPath<StageSequenceData>(SequenceAssetPath);
            report.Check("데이터 로드", stage != null && bossRoom != null && sequence != null,
                         $"{StageAssetPath} · {BossRoomAssetPath} · {SequenceAssetPath}");
            if (!report.IsOk) return report.Finish();

            ValidateRules(report, sequence, stage, bossRoom);
            ValidateImport(report);

            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return false;
            var scene = EditorSceneManager.OpenScene(RunScenePath, OpenSceneMode.Single);

            var roots = scene.GetRootGameObjects().Where(go => go.name == RootName).ToList();
            report.Check("루트 1개", roots.Count == 1, $"'{RootName}' {roots.Count}개");
            var presenter = roots.Count == 1 ? roots[0].GetComponent<StageEnvironmentPresenter>() : null;
            report.Check("표현 컴포넌트", presenter != null, "StageEnvironmentPresenter");
            if (presenter == null) return report.Finish();

            var so = new SerializedObject(presenter);
            report.Check("스테이지 배선", so.FindProperty("stage").objectReferenceValue == stage, StageAssetPath);
            report.Check("보스 방 배선", so.FindProperty("bossRoom").objectReferenceValue == bossRoom, BossRoomAssetPath);

            var follow = Object.FindAnyObjectByType<PlayerCameraFollow>(FindObjectsInactive.Include);
            report.Check("시차 카메라", follow != null && so.FindProperty("parallaxCamera").objectReferenceValue == follow.transform,
                         "PlayerCameraFollow 의 Transform");

            var field = so.FindProperty("fieldRoot").objectReferenceValue as GameObject;
            var arena = so.FindProperty("bossArenaRoot").objectReferenceValue as GameObject;
            var shared = ReadArray<GameObject>(so.FindProperty("sharedRoots"));
            var grayboxes = ReadArray<Renderer>(so.FindProperty("grayboxRenderers"));

            ValidateLayers(report, shared, field, arena);

            var layouts = Object.FindAnyObjectByType<RoomLayoutController>(FindObjectsInactive.Include);
            var ground = scene.GetRootGameObjects().FirstOrDefault(go => go.name == "Ground");
            report.Check("Run 지형 구성", layouts != null && ground != null, "RoomLayoutController · Ground");
            if (layouts != null && ground != null)
            {
                ValidateSkins(report, layouts, stage, ground, shared, grayboxes);
                ValidateNoLeak(report, scene, roots[0], layouts, stage);
                ValidateSwitching(report, presenter, shared, field, arena, grayboxes);
            }

            // 시뮬레이션이 활성 상태를 건드렸다 — 저장하지 않고 버린다.
            EditorSceneManager.OpenScene(RunScenePath, OpenSceneMode.Single);
            return report.Finish();
        }

        // ───────────────────────────────────────────────────────────── 규칙(데이터)

        /// <summary>시퀀스의 모든 방을 규칙에 넣어 본다 — S1 방만 보이고, 보스 배경은 S1 보스 방 하나뿐.</summary>
        private static void ValidateRules(C2Report report, StageSequenceData sequence, StageData stage, RoomData bossRoom)
        {
            int stage1Rooms = 0, otherRooms = 0, bossRooms = 0;
            var wrong = new List<string>();
            foreach (var s in sequence.stages.Where(s => s != null))
            {
                foreach (var room in s.steps.Where(step => step?.options != null).SelectMany(step => step.options).Where(r => r != null))
                {
                    var look = StageEnvironmentRule.Resolve(stage, bossRoom, room);
                    bool isStage1 = s == stage;
                    if (isStage1) stage1Rooms++; else otherRooms++;
                    if (look == StageEnvironmentLook.BossArena) bossRooms++;

                    bool isExpected = isStage1
                        ? look == (room == bossRoom ? StageEnvironmentLook.BossArena : StageEnvironmentLook.Field)
                        : look == StageEnvironmentLook.Hidden;
                    if (!isExpected) wrong.Add($"{s.stageId}/{room.roomId} → {look}");
                }
            }
            report.Check("방별 표현 규칙", wrong.Count == 0 && bossRooms == 1 && stage1Rooms > 0 && otherRooms > 0,
                         $"S1 방 {stage1Rooms} · 그 외 {otherRooms} · 보스 배경 {bossRooms}곳" +
                         (wrong.Count > 0 ? " · 어긋남 " + string.Join(", ", wrong) : ""));
        }

        private static void ValidateImport(C2Report report)
        {
            var notFull = new List<string>();
            foreach (string path in TiledTexturePaths)
            {
                if (AssetImporter.GetAtPath(path) is not TextureImporter importer) { notFull.Add(path + "(없음)"); continue; }
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                if (settings.spriteMeshType != SpriteMeshType.FullRect) notFull.Add(path);
            }
            report.Check("Tiled 대상 Full Rect", notFull.Count == 0, notFull.Count == 0 ? "5장" : string.Join(", ", notFull));
        }

        // ───────────────────────────────────────────────────────────── 씬 구성

        private static void ValidateLayers(C2Report report, IList<GameObject> shared, GameObject field, GameObject arena)
        {
            var sharedRoot = shared.FirstOrDefault(go => go != null && go.name == "Shared");
            report.Check("공용 층", HasLayer(sharedRoot, "bg_sky") && sharedRoot.transform.Find("GroundSkin") != null,
                         "Shared/bg_sky + Shared/GroundSkin");
            report.Check("일반 방 배경", HasLayer(field, "bg_far") && HasLayer(field, "bg_near"), "Field/bg_far + bg_near");
            report.Check("보스 방 배경", HasLayer(arena, "bg_boss_arena") && !HasLayer(field, "bg_boss_arena"),
                         "BossArena/bg_boss_arena 만 (Field 에는 없음)");
        }

        private static bool HasLayer(GameObject root, string layerName)
        {
            if (root == null) return false;
            var layer = root.transform.Find(layerName);
            return layer != null && layer.GetComponent<ParallaxLayer>() != null
                   && layer.GetComponentsInChildren<SpriteRenderer>(true).Length == 3;
        }

        /// <summary>S1 발판마다 스킨 1개 · 콜라이더 사각형과 그림 윗선/폭 일치 · 그레이박스는 표현 컴포넌트가 관리.</summary>
        private static void ValidateSkins(C2Report report, RoomLayoutController layouts, StageData stage, GameObject ground,
                                          IList<GameObject> shared, IList<Renderer> grayboxes)
        {
            var platforms = CollectStagePlatforms(layouts, stage);
            var mismatches = new List<string>();
            int columns = 0;

            foreach (var platform in platforms)
            {
                var skins = platform.Cast<Transform>().Where(c => c.name == SkinName).ToList();
                if (skins.Count != 1) { mismatches.Add($"{PathOf(platform)} 스킨 {skins.Count}개"); continue; }
                if (!shared.Contains(skins[0].gameObject)) mismatches.Add($"{PathOf(platform)} 스킨이 표현 컴포넌트 관리 밖");

                var graybox = platform.GetComponent<SpriteRenderer>();
                if (graybox != null && !grayboxes.Contains(graybox)) mismatches.Add($"{PathOf(platform)} 그레이박스 관리 밖");

                var rect = WorldRect(platform.GetComponent<BoxCollider2D>());
                var drawn = skins[0].GetComponentsInChildren<SpriteRenderer>(true).Select(DrawnRect).ToList();
                if (drawn.Count == 0) { mismatches.Add($"{PathOf(platform)} 렌더러 없음"); continue; }

                float xMin = drawn.Min(r => r.xMin), xMax = drawn.Max(r => r.xMax), yMax = drawn.Max(r => r.yMax);
                bool isColumn = rect.height >= 1f;
                if (isColumn) columns++;
                bool isFit = Near(xMin, rect.xMin) && Near(xMax, rect.xMax) && Near(yMax, rect.yMax)
                             && (!isColumn || Near(drawn.Min(r => r.yMin), rect.yMin));
                if (!isFit)
                    mismatches.Add($"{PathOf(platform)} 콜라이더 {Fmt(rect)} vs 그림 x {xMin:0.###}~{xMax:0.###} 윗선 {yMax:0.###}");
            }

            report.Check("S1 발판 스킨 일치", platforms.Count > 0 && mismatches.Count == 0,
                         $"발판 {platforms.Count}개(기둥 {columns})" + (mismatches.Count > 0 ? "\n    " + string.Join("\n    ", mismatches) : ""));

            var groundRenderers = ground.GetComponentsInChildren<SpriteRenderer>(true);
            report.Check("지면 그레이박스 관리", groundRenderers.Length > 0 && groundRenderers.All(grayboxes.Contains),
                         $"Ground 렌더러 {groundRenderers.Length}개");
        }

        /// <summary>
        /// S1 그림이 표현 루트·S1 발판 스킨 <b>밖</b>에서 쓰이지 않는가. 이것이 「S2·S3 방에 S1 표현이 남지 않는다」의
        /// 씬 쪽 절반이다(나머지 절반은 표현 컴포넌트의 전환 규칙).
        /// </summary>
        private static void ValidateNoLeak(C2Report report, Scene scene, GameObject root, RoomLayoutController layouts, StageData stage)
        {
            var stagePlatforms = new HashSet<Transform>(CollectStagePlatforms(layouts, stage));
            var leaks = new List<string>();

            foreach (var sr in scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<SpriteRenderer>(true)))
            {
                if (sr.sprite == null) continue;
                string path = AssetDatabase.GetAssetPath(sr.sprite);
                if (!path.StartsWith(AbyssPaths.Stage1Art)) continue;
                if (sr.transform.IsChildOf(root.transform)) continue;

                var skin = FindAncestor(sr.transform, SkinName);
                if (skin != null && stagePlatforms.Contains(skin.parent)) continue;

                leaks.Add($"{PathOf(sr.transform)} ← {path}");
            }
            report.Check("S1 그림 누수 0", leaks.Count == 0, leaks.Count == 0 ? "표현 루트·S1 발판 밖 참조 없음" : string.Join("\n    ", leaks));
        }

        /// <summary>
        /// 표현 컴포넌트를 에디터에서 세 모습으로 돌려 보고 활성 상태를 센다(런타임 Apply 그대로 · 저장 안 함).
        /// 🔑 Hidden 에서 S1 렌더러가 하나라도 화면에 남으면 실패 — 「Stage2/3 에 S1 배경이 남으면 인수 불가」(결정 6).
        /// </summary>
        private static void ValidateSwitching(C2Report report, StageEnvironmentPresenter presenter,
                                              IList<GameObject> shared, GameObject field, GameObject arena, IList<Renderer> grayboxes)
        {
            var problems = new List<string>();
            foreach (StageEnvironmentLook look in System.Enum.GetValues(typeof(StageEnvironmentLook)))
            {
                presenter.Apply(look);

                bool expectShared = StageEnvironmentRule.ShowsShared(look);
                if (shared.Any(go => go != null && go.activeSelf != expectShared)) problems.Add($"{look}: 공용 층");
                if (field != null && field.activeSelf != StageEnvironmentRule.ShowsField(look)) problems.Add($"{look}: 일반 방 배경");
                if (arena != null && arena.activeSelf != StageEnvironmentRule.ShowsBossArena(look)) problems.Add($"{look}: 보스 방 배경");
                bool expectGraybox = StageEnvironmentRule.ShowsGraybox(look);
                if (grayboxes.Any(r => r != null && r.enabled != expectGraybox)) problems.Add($"{look}: 그레이박스");

                if (look == StageEnvironmentLook.Hidden)
                {
                    int visible = presenter.GetComponentsInChildren<SpriteRenderer>(false).Length
                                  + shared.Where(go => go != null && go.activeInHierarchy)
                                          .Sum(go => go.GetComponentsInChildren<SpriteRenderer>(false).Length);
                    if (visible > 0) problems.Add($"Hidden: S1 렌더러 {visible}개가 켜져 있음");
                }
            }
            report.Check("표현 전환(Field·BossArena·Hidden)", problems.Count == 0,
                         problems.Count == 0 ? "3모습 모두 규칙대로" : string.Join(" · ", problems));
        }

        // ───────────────────────────────────────────────────────────── 공용

        /// <summary>Tiled/Simple 렌더러가 그리는 월드 사각형(피벗 반영). 비활성이어도 계산된다.</summary>
        private static Rect DrawnRect(SpriteRenderer sr)
        {
            Vector2 size = sr.drawMode == SpriteDrawMode.Simple ? (Vector2)sr.sprite.bounds.size : sr.size;
            Vector2 pivot = new Vector2(sr.sprite.pivot.x / sr.sprite.rect.width, sr.sprite.pivot.y / sr.sprite.rect.height);
            Vector3 scale = sr.transform.lossyScale;
            size = new Vector2(size.x * Mathf.Abs(scale.x), size.y * Mathf.Abs(scale.y));
            Vector2 origin = (Vector2)sr.transform.position - Vector2.Scale(pivot, size);
            return new Rect(origin, size);
        }

        private static Transform FindAncestor(Transform t, string name)
        {
            for (var cursor = t; cursor != null; cursor = cursor.parent)
            {
                if (cursor.name == name) return cursor;
            }
            return null;
        }

        private static List<T> ReadArray<T>(SerializedProperty property) where T : Object
        {
            var result = new List<T>();
            for (int i = 0; i < property.arraySize; i++)
            {
                result.Add(property.GetArrayElementAtIndex(i).objectReferenceValue as T);
            }
            return result;
        }

        private static bool Near(float a, float b) => Mathf.Abs(a - b) <= Epsilon;

        private static string Fmt(Rect r) => $"x {r.xMin:0.###}~{r.xMax:0.###} y {r.yMin:0.###}~{r.yMax:0.###}";
    }
}
#endif
