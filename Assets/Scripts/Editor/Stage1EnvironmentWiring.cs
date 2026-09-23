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
    /// C2 — Stage1 기존 환경 아트(배경·지면·발판)를 <b>Run 씬</b>에 붙이는 제한적 진입점. 메뉴 없이
    /// <c>-executeMethod Abyss.EditorTools.Stage1EnvironmentWiring.ApplyToRunSceneBatch</c> 로 부른다.
    ///
    /// 🔴 <b>하는 일은 「표시」뿐이다.</b>
    /// <list type="bullet">
    /// <item>지형·충돌·발판 좌표는 안 바꾼다 — 스킨은 콜라이더 오브젝트의 <b>자식 렌더러</b>로만 붙고,
    /// 적용 전후 모든 콜라이더의 월드 사각형을 비교해 하나라도 달라지면 저장하지 않는다.</item>
    /// <item>그레이박스 렌더러는 지우지 않는다 — <see cref="StageEnvironmentPresenter"/> 가 Stage1 방에서만 끄고 나머지 방에서 켠다.
    /// 그래서 S2·S3 방은 지금과 같은 모습이고, S1 그림이 남지 않는다.</item>
    /// <item>RoomLayoutBuilder · ArtTestStageBuilder · PrefabBuilder 는 부르지 않는다. 씬을 새로 만들지 않는다.</item>
    /// </list>
    ///
    /// 멱등: 다시 부르면 <c>Stage1Environment</c> 루트와 <c>Stage1Skin</c> 자식을 지우고 같은 규칙으로 다시 만든다.
    /// 검증은 <c>Stage1EnvironmentWiring.Validate.cs</c>.
    /// </summary>
    public static partial class Stage1EnvironmentWiring
    {
        public const string RunScenePath = "Assets/Scenes/Run.unity";
        public const string RootName = "Stage1Environment";
        public const string SkinName = "Stage1Skin";
        public const string StageAssetPath = AbyssPaths.Stages + "/Stage1_AbyssEntrance.asset";
        public const string BossRoomAssetPath = AbyssPaths.Rooms + "/Room6_Boss.asset";

        private const float Ppu = EnvironmentArtImporter.PixelsPerUnit;

        /// <summary>배경 확대 배율 — ArtTest 와 같은 값(정수여야 픽셀이 안 찌그러진다). 159x95 → 19.9x11.9유닛 ≥ 화면 20x11.25 세로.</summary>
        private const int BackgroundUpscale = 4;

        /// <summary>지면 윗줄 아래에 깔 속 타일 줄 수. 카메라 최저(지면 위 2유닛 추적)에서 화면 아래가 비지 않을 만큼.</summary>
        private const int GroundInteriorRows = 5;

        // 깊이(ParallaxLayer 규약 — 0이 가장 멀다). 🔑 bg_near 는 ArtTest 에서 전경(1.15)이었지만 여기서는
        // 캐릭터 뒤(0.6)로 둔다 — 전투 가독성이 우선이다(총괄 결정 6 · D 명세 R2 「적이 종유석 앞에서」).
        private const float DepthSky = 0f;
        private const float DepthFar = 0.25f;
        private const float DepthNear = 0.6f;
        private const float DepthArena = 0.1f;

        // 정렬 — 전부 캐릭터(0 이상)·그레이박스 발판(-1)보다 뒤.
        private const int OrderSky = -40;
        private const int OrderFar = -30;
        private const int OrderArena = -30;
        private const int OrderNear = -20;
        private const int OrderTerrain = -5;

        // 타일 그리기(Tiled)가 제대로 반복되려면 Full Rect 메시여야 한다. 이 다섯 장만 바꾼다.
        internal static readonly string[] TiledTexturePaths =
        {
            AbyssPaths.Stage1Art + "/tile_platform_1.png",
            AbyssPaths.Stage1Art + "/tile_platform_2.png",
            AbyssPaths.Stage1Art + "/tile_platform_3.png",
            AbyssPaths.Stage1Tiles + "/ground_top.png",
            AbyssPaths.Stage1Tiles + "/ground_interior.png",
        };

        private static readonly string[] PlatformSprites = { "tile_platform_1", "tile_platform_2", "tile_platform_3" };

        /// <summary>배치 모드 진입점. 실패하면 종료 코드 1.</summary>
        public static void ApplyToRunSceneBatch()
        {
            bool isOk = ApplyToRunScene();
            if (Application.isBatchMode) EditorApplication.Exit(isOk ? 0 : 1);
        }

        /// <summary>Run 씬에 Stage1 환경 표현을 붙이고 저장한다. 실패하면 저장하지 않고 false.</summary>
        public static bool ApplyToRunScene()
        {
            var stage = AssetDatabase.LoadAssetAtPath<StageData>(StageAssetPath);
            var bossRoom = AssetDatabase.LoadAssetAtPath<RoomData>(BossRoomAssetPath);
            if (stage == null || bossRoom == null)
            {
                Debug.LogError($"[Stage1EnvironmentWiring] 데이터 없음: {StageAssetPath} / {BossRoomAssetPath}");
                return false;
            }
            if (!StageEnvironmentRule.Contains(stage, bossRoom))
            {
                Debug.LogError("[Stage1EnvironmentWiring] 보스 방이 Stage1 단계 선택지에 없다 — 데이터가 바뀌었는지 확인할 것.");
                return false;
            }

            var sprites = LoadSprites();
            if (sprites == null) return false;

            EnsureFullRect();

            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return false;
            var scene = EditorSceneManager.OpenScene(RunScenePath, OpenSceneMode.Single);

            var ground = FindRoot(scene, "Ground");
            var layouts = Object.FindAnyObjectByType<RoomLayoutController>(FindObjectsInactive.Include);
            var follow = Object.FindAnyObjectByType<PlayerCameraFollow>(FindObjectsInactive.Include);
            if (ground == null || layouts == null || follow == null)
            {
                Debug.LogError($"[Stage1EnvironmentWiring] Run 씬 구성 요소 없음 — Ground {ground != null} · " +
                               $"RoomLayoutController {layouts != null} · PlayerCameraFollow {follow != null}");
                return false;
            }

            // 이전 산출물(콜라이더 없음)을 먼저 지우고 잰다 — 루트 순번이 당겨져 경로 키가 어긋나지 않게.
            RemovePrevious(scene);
            var before = SnapshotColliders(scene);

            var platforms = CollectStagePlatforms(layouts, stage);
            var groundRect = WorldRect(ground.GetComponent<BoxCollider2D>());
            float cameraOffsetY = new SerializedObject(follow).FindProperty("offset").vector3Value.y;
            float restCameraY = groundRect.yMax + cameraOffsetY;

            var root = new GameObject(RootName);
            SceneManager.MoveGameObjectToScene(root, scene);

            var shared = CreateChild(root.transform, "Shared");
            var field = CreateChild(root.transform, "Field");
            var arena = CreateChild(root.transform, "BossArena");

            float referenceHeight = sprites["bg_sky"].rect.height;
            AddBackground(shared, sprites["bg_sky"], DepthSky, OrderSky, 40f, restCameraY, referenceHeight);
            AddBackground(field, sprites["bg_far"], DepthFar, OrderFar, 30f, restCameraY, referenceHeight);
            AddBackground(field, sprites["bg_near"], DepthNear, OrderNear, 20f, restCameraY, referenceHeight);
            AddBackground(arena, sprites["bg_boss_arena"], DepthArena, OrderArena, 30f, restCameraY, referenceHeight);

            BuildGroundSkin(shared, groundRect, sprites["ground_top"], sprites["ground_interior"]);

            var skinRoots = new List<GameObject> { shared.gameObject };
            var grayboxes = new List<Renderer>(ground.GetComponentsInChildren<SpriteRenderer>(true));
            for (int i = 0; i < platforms.Count; i++)
            {
                var platform = platforms[i];
                var skin = BuildPlatformSkin(platform, sprites, i);
                if (skin == null) continue;
                skinRoots.Add(skin);

                var graybox = platform.GetComponent<SpriteRenderer>();
                if (graybox != null) grayboxes.Add(graybox);
            }

            var presenter = root.AddComponent<StageEnvironmentPresenter>();
            var so = new SerializedObject(presenter);
            so.FindProperty("stage").objectReferenceValue = stage;
            so.FindProperty("bossRoom").objectReferenceValue = bossRoom;
            SetArray(so.FindProperty("sharedRoots"), skinRoots.Cast<Object>().ToList());
            so.FindProperty("fieldRoot").objectReferenceValue = field.gameObject;
            so.FindProperty("bossArenaRoot").objectReferenceValue = arena.gameObject;
            SetArray(so.FindProperty("grayboxRenderers"), grayboxes.Cast<Object>().ToList());
            so.FindProperty("parallaxCamera").objectReferenceValue = follow.transform;
            so.FindProperty("initialLook").enumValueIndex = (int)StageEnvironmentLook.Field;
            so.ApplyModifiedPropertiesWithoutUndo();

            // 에디터 미리보기는 일반 방 모습. 그레이박스는 켠 채 저장한다 — 런타임 Awake 가 끈다
            // (표현 컴포넌트가 빠진 채 저장돼도 흰 지형이 남아 있어야 진행이 가능하다).
            arena.gameObject.SetActive(false);

            var after = SnapshotColliders(scene);
            string diff = DiffColliders(before, after);
            if (diff != null)
            {
                Debug.LogError($"[Stage1EnvironmentWiring] 콜라이더가 바뀌었다 — 저장하지 않는다.\n{diff}");
                EditorSceneManager.OpenScene(RunScenePath, OpenSceneMode.Single);   // 저장 안 한 변경 버림
                return false;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
            {
                Debug.LogError("[Stage1EnvironmentWiring] 씬 저장 실패");
                return false;
            }

            Debug.Log($"[Stage1EnvironmentWiring] 완료 — {RunScenePath}\n" +
                      $"  발판 스킨 {platforms.Count}개 · 그레이박스 렌더러 {grayboxes.Count}개 · 콜라이더 {after.Count}개 변경 0\n" +
                      $"  카메라 기준 y {restCameraY:0.###} (지면 윗면 {groundRect.yMax:0.###} + 추적 offset {cameraOffsetY:0.###})");
            return true;
        }

        // ───────────────────────────────────────────────────────────── 준비

        private static Dictionary<string, Sprite> LoadSprites()
        {
            var result = new Dictionary<string, Sprite>();
            var names = new[] { "bg_sky", "bg_far", "bg_near", "bg_boss_arena" }.Concat(PlatformSprites);
            foreach (string name in names)
            {
                result[name] = AssetDatabase.LoadAssetAtPath<Sprite>($"{AbyssPaths.Stage1Art}/{name}.png");
            }
            result["ground_top"] = AssetDatabase.LoadAssetAtPath<Sprite>($"{AbyssPaths.Stage1Tiles}/ground_top.png");
            result["ground_interior"] = AssetDatabase.LoadAssetAtPath<Sprite>($"{AbyssPaths.Stage1Tiles}/ground_interior.png");

            var missing = result.Where(pair => pair.Value == null).Select(pair => pair.Key).ToList();
            if (missing.Count == 0) return result;

            Debug.LogError($"[Stage1EnvironmentWiring] 스프라이트 없음: {string.Join(", ", missing)}");
            return null;
        }

        /// <summary>Tiled 그리기 대상 다섯 장만 Full Rect 로. 이미 그러면 재임포트하지 않는다.</summary>
        private static void EnsureFullRect()
        {
            foreach (string path in TiledTexturePaths)
            {
                if (AssetImporter.GetAtPath(path) is not TextureImporter importer) continue;

                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                if (settings.spriteMeshType == SpriteMeshType.FullRect) continue;

                settings.spriteMeshType = SpriteMeshType.FullRect;
                importer.SetTextureSettings(settings);
                importer.SaveAndReimport();
                Debug.Log($"[Stage1EnvironmentWiring] Full Rect 로 재임포트: {path}");
            }
        }

        private static GameObject FindRoot(Scene scene, string name)
            => scene.GetRootGameObjects().FirstOrDefault(go => go.name == name);

        /// <summary>이전 실행의 산출물만 지운다 — 루트와 발판 아래 스킨 자식.</summary>
        private static void RemovePrevious(Scene scene)
        {
            foreach (var go in scene.GetRootGameObjects().Where(go => go.name == RootName).ToList())
            {
                Object.DestroyImmediate(go);
            }

            foreach (var t in scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<Transform>(true)).ToList())
            {
                if (t != null && t.name == SkinName) Object.DestroyImmediate(t.gameObject);
            }
        }

        /// <summary>Stage1 방에 묶인 레이아웃 루트의 발판(콜라이더가 붙은 직계 자식). 판정은 StageData 선택지.</summary>
        internal static List<Transform> CollectStagePlatforms(RoomLayoutController layouts, StageData stage)
        {
            var result = new List<Transform>();
            var bindings = new SerializedObject(layouts).FindProperty("bindings");
            for (int i = 0; i < bindings.arraySize; i++)
            {
                var element = bindings.GetArrayElementAtIndex(i);
                var room = element.FindPropertyRelative("room").objectReferenceValue as RoomData;
                var layoutRoot = element.FindPropertyRelative("layoutRoot").objectReferenceValue as GameObject;
                if (layoutRoot == null || !StageEnvironmentRule.Contains(stage, room)) continue;

                foreach (Transform child in layoutRoot.transform)
                {
                    if (child.GetComponent<BoxCollider2D>() != null) result.Add(child);
                }
            }
            return result;
        }

        // ───────────────────────────────────────────────────────────── 배경

        /// <summary>
        /// 배경 한 층 = 확대한 같은 그림 3장 가로 이음 + 시차(ArtTest 와 같은 구성).
        /// 🔑 y 는 「지면 위 기준 카메라 위치에서 그림 중심이 화면 중심」이 되게 잡는다: 레이어 y = base + cam·(1-depth)
        /// 이므로 base = depth·기준y. 그리고 <b>아래 가장자리를 맞춘다</b> — bg_near 만 96px(나머지 95px)이라
        /// 중앙 피벗이면 반 픽셀씩 위아래로 튀어나온다. 원본은 자르지 않는다(manifest 결정).
        /// </summary>
        private static void AddBackground(Transform parent, Sprite sprite, float depth, int order, float z,
                                          float restCameraY, float referenceHeight)
        {
            var layer = new GameObject(sprite.name);
            layer.transform.SetParent(parent, false);

            float bottomAlign = (sprite.rect.height - referenceHeight) * 0.5f / Ppu * BackgroundUpscale;
            layer.transform.position = new Vector3(0f, depth * restCameraY + bottomAlign, z);
            layer.transform.localScale = Vector3.one * BackgroundUpscale;

            float tileWidth = sprite.rect.width / Ppu;
            for (int i = -1; i <= 1; i++)
            {
                var piece = new GameObject($"{sprite.name}_{i + 1}");
                piece.transform.SetParent(layer.transform, false);
                piece.transform.localPosition = new Vector3(tileWidth * i, 0f, 0f);

                var sr = piece.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;
                sr.sortingOrder = order;
            }

            layer.AddComponent<ParallaxLayer>().Configure(depth, tileWidth * BackgroundUpscale);
        }

        // ───────────────────────────────────────────────────────────── 지면·발판

        /// <summary>지면 콜라이더 사각형 그대로 윗줄(ground_top)을 덮고, 아래로 속 타일을 깐다.</summary>
        private static void BuildGroundSkin(Transform parent, Rect groundRect, Sprite top, Sprite interior)
        {
            var skin = CreateChild(parent, "GroundSkin");
            float width = groundRect.width;

            CreateTiled(skin, "GroundTop", top, new Vector2(width, 1f),
                        new Vector2(groundRect.center.x, groundRect.yMax - 0.5f), OrderTerrain);
            CreateTiled(skin, "GroundInterior", interior, new Vector2(width, GroundInteriorRows),
                        new Vector2(groundRect.center.x, groundRect.yMax - 1f - GroundInteriorRows * 0.5f), OrderTerrain);
        }

        /// <summary>
        /// 발판 하나에 스킨. 얇은 발판(높이 &lt; 1)은 tile_platform 을 가로로 반복하고 <b>그림 윗선 = 콜라이더 윗면</b>,
        /// 기둥(높이 ≥ 1, Room4_Elite)은 지면 타일을 세워 콜라이더 사각형을 정확히 덮는다.
        /// 스킨은 발판의 자식이고 부모 배율을 되돌려(월드 배율 1) 픽셀이 늘어나지 않는다.
        /// </summary>
        private static GameObject BuildPlatformSkin(Transform platform, Dictionary<string, Sprite> sprites, int index)
        {
            var rect = WorldRect(platform.GetComponent<BoxCollider2D>());
            Vector3 lossy = platform.lossyScale;
            if (Mathf.Approximately(lossy.x, 0f) || Mathf.Approximately(lossy.y, 0f)) return null;

            var skin = new GameObject(SkinName);
            skin.transform.SetParent(platform, false);
            skin.transform.localScale = new Vector3(1f / lossy.x, 1f / lossy.y, 1f);
            skin.transform.position = platform.position;

            if (rect.height < 1f)
            {
                var sprite = sprites[PlatformSprites[index % PlatformSprites.Length]];
                float spriteHeight = sprite.rect.height / Ppu;
                // 발밑(BottomCenter) 피벗 — Tiled 사각형은 피벗에서 위로 spriteHeight 만큼 선다.
                CreateTiled(skin.transform, "Slab", sprite, new Vector2(rect.width, spriteHeight),
                            new Vector2(rect.center.x, rect.yMax - spriteHeight), OrderTerrain);
            }
            else
            {
                CreateTiled(skin.transform, "ColumnTop", sprites["ground_top"], new Vector2(rect.width, 1f),
                            new Vector2(rect.center.x, rect.yMax - 0.5f), OrderTerrain);
                float body = rect.height - 1f;
                if (body > 0f)
                    CreateTiled(skin.transform, "ColumnBody", sprites["ground_interior"], new Vector2(rect.width, body),
                                new Vector2(rect.center.x, rect.yMin + body * 0.5f), OrderTerrain);
            }
            return skin;
        }

        private static void CreateTiled(Transform parent, string name, Sprite sprite, Vector2 size, Vector2 worldPosition, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(worldPosition.x, worldPosition.y, 0f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.drawMode = SpriteDrawMode.Tiled;
            sr.tileMode = SpriteTileMode.Continuous;
            sr.size = size;
            sr.sortingOrder = order;
        }

        private static Transform CreateChild(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        private static void SetArray(SerializedProperty property, IList<Object> values)
        {
            property.arraySize = values.Count;
            for (int i = 0; i < values.Count; i++)
            {
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }
        }

        // ───────────────────────────────────────────────────────────── 콜라이더 보존 확인

        /// <summary>콜라이더의 월드 사각형. 에디터에서 물리 없이 계산한다(비활성 레이아웃도 잰다).</summary>
        internal static Rect WorldRect(BoxCollider2D collider)
        {
            if (collider == null) return default;
            var t = collider.transform;
            Vector2 center = t.TransformPoint(collider.offset);
            Vector2 size = Vector2.Scale(collider.size, t.lossyScale);
            size = new Vector2(Mathf.Abs(size.x), Mathf.Abs(size.y));
            return new Rect(center - size * 0.5f, size);
        }

        /// <summary>씬의 모든 BoxCollider2D — 경로 → (사각형, 레이어, 활성).</summary>
        internal static Dictionary<string, string> SnapshotColliders(Scene scene)
        {
            var result = new Dictionary<string, string>();
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var collider in root.GetComponentsInChildren<BoxCollider2D>(true))
                {
                    string key = PathOf(collider.transform);
                    var r = WorldRect(collider);
                    result[key] = $"{r.x:0.####},{r.y:0.####},{r.width:0.####},{r.height:0.####} " +
                                  $"layer {collider.gameObject.layer} enabled {collider.enabled} trigger {collider.isTrigger}";
                }
            }
            return result;
        }

        private static string DiffColliders(Dictionary<string, string> before, Dictionary<string, string> after)
        {
            var lines = new List<string>();
            foreach (var pair in before)
            {
                if (!after.TryGetValue(pair.Key, out string now)) lines.Add($"  사라짐 {pair.Key}");
                else if (now != pair.Value) lines.Add($"  바뀜 {pair.Key}: {pair.Value} → {now}");
            }
            foreach (var key in after.Keys.Where(key => !before.ContainsKey(key)))
            {
                lines.Add($"  생김 {key}");
            }
            return lines.Count == 0 ? null : string.Join("\n", lines);
        }

        /// <summary>계층 경로 + 형제 순번. 이름이 같은 발판(Platform_01 이 레이아웃마다 있다)도 갈린다.</summary>
        internal static string PathOf(Transform t)
        {
            var names = new List<string>();
            for (var cursor = t; cursor != null; cursor = cursor.parent) names.Add($"{cursor.name}[{cursor.GetSiblingIndex()}]");
            names.Reverse();
            return string.Join("/", names);
        }
    }
}
#endif
