#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
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
    /// Stage1 전 방(StageData.steps 의 11곳 — 이벤트·상점·휴식·보스 포함) 지형 + 채택 아트 적용/복원. Abyss Tools ▸ 아트 탭 버튼으로 부른다.
    ///
    /// <list type="bullet">
    /// <item><b>Stage1 레이아웃 루트만</b> 다시 만든다 — 기존 바인딩 루트는 자식만 교체, 바인딩 없던 방(Bonefield·비전투 4곳)은 루트를 새로 붙인다.
    /// RoomLayoutBuilder · StageBuilder · 씬 빌더는 부르지 않는다.</item>
    /// <item>콜라이더 비교는 <b>Stage1 레이아웃 밖</b>에 적용한다 — 그 밖(지면·Stage2/3 레이아웃·기타)이 하나라도 달라지면 저장하지 않는다.
    /// Stage1 안쪽은 규격(<c>AllRoomsSpec.cs</c>)과 같은가·이동 가능한가를 새로 검사한다(<c>AllRoomsValidate.cs</c>).</item>
    /// <item>표현은 <see cref="StageEnvironmentPresenter"/> 의 스테이지 전체 모드 — Stage1 모든 방에서 새 배경·지면을 켜고 기존 Field·BossArena·지면 스킨을 끈다.
    /// 스테이지 밖에서는 기존 규칙대로 전부 꺼진다(Stage2/3 표시 불변).</item>
    /// </list>
    /// ⚠️ <see cref="ApplyToRunScene"/>(C2 기존 표현 재생성)이나 RoomLayoutBuilder 를 다시 돌리면 이 결과가 사라진다 — 그 뒤 적용을 다시 누른다.
    /// </summary>
    public static partial class Stage1EnvironmentWiring
    {
        private const string AllRoomsLog = "[Stage1AllRooms]";
        private const string NewLayoutRootPrefix = "RoomLayout_S1_";

        /// <summary>Stage1 방 하나 — 단계 번호(문 개수 계산용)와 에셋 이름.</summary>
        internal readonly struct Stage1Room
        {
            public readonly RoomData Room;
            public readonly int StepIndex;
            public readonly string AssetName;

            public Stage1Room(RoomData room, int stepIndex, string assetName)
            {
                Room = room;
                StepIndex = stepIndex;
                AssetName = assetName;
            }
        }

        // ───────────────────────────────────────────────────────────── 적용

        /// <summary>Stage1 전 방 지형·아트를 Run 씬에 적용하고 저장한다. 규격·검사 실패·Stage1 밖 콜라이더 변경이면 저장하지 않고 false.</summary>
        public static bool ApplyAllRooms()
        {
            if (!LoadStage1Rooms(out var stage, out var bossRoom, out var rooms)) return false;

            var fileReport = new C2Report("S1-ROOMART-FILE");
            CheckRoomArtFiles(fileReport);
            if (!fileReport.Finish())
            {
                Debug.LogWarning($"{AllRoomsLog} 적용 보류 — 채택 아트가 {RoomArtFolder} 에 규격대로 없다. 씬은 열지 않았다.");
                return false;
            }

            EnsureRoomArtImport();
            var importReport = new C2Report("S1-ROOMART-IMPORT");
            var sprites = CheckRoomArtImport(importReport);
            if (!importReport.Finish() || sprites == null)
            {
                Debug.LogWarning($"{AllRoomsLog} 적용 보류 — Unity 가 읽은 스프라이트가 규격과 다르다. 씬은 열지 않았다.");
                return false;
            }

            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return false;
            var scene = EditorSceneManager.OpenScene(RunScenePath, OpenSceneMode.Single);
            if (!FindAllRoomsTargets(scene, out var envRoot, out var presenter, out var ground, out var layouts, out var follow)) return Abort(scene);

            var groundRect = WorldRect(ground.GetComponent<BoxCollider2D>());
            if (!Near(groundRect.yMax, SpecGroundTop))
            {
                Debug.LogError($"{AllRoomsLog} 지면 윗면 {groundRect.yMax:0.###} ≠ 규격 {SpecGroundTop} — 지형 좌표가 어긋난다. 저장하지 않는다.");
                return Abort(scene);
            }

            RemoveRoomArtRoots(envRoot);
            var before = SnapshotColliders(scene);

            int groundLayer = EditorPlatformFactory.GetGroundLayer();
            var white = EditorPlatformFactory.LoadWhiteSquare();
            var frictionless = EditorPlatformFactory.GetOrCreateFrictionlessMaterial();
            var newSkins = new List<GameObject>();
            var newGrayboxes = new List<Renderer>();
            var stageRoots = new List<GameObject>();

            foreach (var entry in rooms)
            {
                var spec = FindRoomSpec(entry.AssetName);
                var root = FindOrCreateLayoutRoot(layouts, entry);
                stageRoots.Add(root);
                ClearChildren(root.transform);

                int n = 0;
                foreach (var t in spec.Terraces)
                    AddTerrain(root.transform, $"Terrace_{++n:D2}", t.Center, t.Size, true);
                foreach (var s in spec.Slabs)
                    AddTerrain(root.transform, $"Slab_{++n:D2}", s.Center, s.Size, false);
            }

            void AddTerrain(Transform parent, string name, Vector2 center, Vector2 size, bool isTerrace)
            {
                var go = EditorPlatformFactory.CreatePlatform(parent, name, center, size, groundLayer, white, frictionless,
                                                              EditorPlatformFactory.DefaultPlatformColor);
                var skin = BuildTerrainSkin(go.transform, sprites, isTerrace);
                if (skin != null) newSkins.Add(skin);
                var graybox = go.GetComponent<SpriteRenderer>();
                if (graybox != null) newGrayboxes.Add(graybox);
            }

            var so = new SerializedObject(presenter);
            var field = so.FindProperty("fieldRoot").objectReferenceValue as GameObject;
            var arena = so.FindProperty("bossArenaRoot").objectReferenceValue as GameObject;
            var sharedRoot = ReadArray<GameObject>(so.FindProperty("sharedRoots")).FirstOrDefault(go => go != null && go.name == "Shared");
            var groundSkin = sharedRoot != null ? sharedRoot.transform.Find(GroundSkinName) : null;
            if (field == null || arena == null || groundSkin == null)
            {
                Debug.LogError($"{AllRoomsLog} 대체 대상 부족 — Field {field != null} · BossArena {arena != null} · Shared/{GroundSkinName} {groundSkin != null}");
                return Abort(scene);
            }

            float cameraOffsetY = new SerializedObject(follow).FindProperty("offset").vector3Value.y;
            var artRoot = BuildStageArtRoot(envRoot.transform, sprites, groundRect, groundRect.yMax + cameraOffsetY);

            var shared = ReadArray<GameObject>(so.FindProperty("sharedRoots")).Where(go => go != null).Concat(newSkins).ToList();
            var grayboxes = ReadArray<Renderer>(so.FindProperty("grayboxRenderers")).Where(r => r != null).Concat(newGrayboxes).ToList();
            SetArray(so.FindProperty("sharedRoots"), shared.Cast<Object>().ToList());
            SetArray(so.FindProperty("grayboxRenderers"), grayboxes.Cast<Object>().ToList());
            so.FindProperty("roomArtRoom").objectReferenceValue = null;
            so.FindProperty("roomArtWholeStage").boolValue = true;
            so.FindProperty("roomArtRoot").objectReferenceValue = artRoot;
            SetArray(so.FindProperty("roomArtReplacedRoots"), new List<Object> { field, arena, groundSkin.gameObject });
            so.ApplyModifiedPropertiesWithoutUndo();
            // 에디터 미리보기는 기존 배경 — 런타임 표현 컴포넌트가 Stage1 방에서 켠다.
            artRoot.SetActive(false);

            string outside = DiffCollidersOutside(before, SnapshotColliders(scene), stageRoots.Select(r => PathOf(r.transform)).ToList());
            var check = new C2Report("S1-ALL-APPLY");
            ValidateStage1Layouts(check, scene, stage, bossRoom, rooms, layouts, sprites[KeyBackground], out string summary);
            check.Check("Stage1 밖 콜라이더 불변", outside == null, outside ?? "지면·Stage2/3 레이아웃·기타 변경 0");
            string groundFit = CheckGroundArtFit(artRoot.transform, groundRect);
            check.Check("공용 지면 그림 일치", groundFit == null, groundFit ?? "좌우·윗선·두께");
            if (!check.Finish())
            {
                Debug.LogError($"{AllRoomsLog} 검사 실패 — 저장하지 않는다(위 FAIL 참고).");
                return Abort(scene);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
            {
                Debug.LogError($"{AllRoomsLog} 씬 저장 실패");
                return false;
            }

            Debug.Log($"{AllRoomsLog} 적용 완료 — Stage1 {rooms.Count}방 · 지형 {newSkins.Count}개 · 루트 '{StageArtRootName}'\n{summary}\n{DescribeRoomArt(artRoot.transform)}");
            return true;
        }

        // ───────────────────────────────────────────────────────────── 복원

        /// <summary>
        /// Stage1 을 RoomLayoutBuilder 원래 좌표와 기존 C2 타일 스킨으로 되돌린다. 새로 붙인 방 루트·바인딩(원래 평지였던 방)은 지우고,
        /// 표현 컴포넌트의 방 아트 배선을 비운다. 그림 파일·임포트 설정은 남긴다. Stage1 밖 콜라이더가 바뀌면 저장하지 않는다.
        /// </summary>
        public static bool RestoreAllRooms()
        {
            if (!LoadStage1Rooms(out _, out _, out var rooms)) return false;
            var legacySprites = LoadSprites();
            if (legacySprites == null) return false;

            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return false;
            var scene = EditorSceneManager.OpenScene(RunScenePath, OpenSceneMode.Single);
            if (!FindAllRoomsTargets(scene, out var envRoot, out var presenter, out _, out var layouts, out _)) return Abort(scene);

            int removedArt = RemoveRoomArtRoots(envRoot);
            var before = SnapshotColliders(scene);
            var scopes = new List<string>();
            var restoredSkins = new List<GameObject>();
            var restoredGrayboxes = new List<Renderer>();
            var removedRoots = new List<string>();

            int groundLayer = EditorPlatformFactory.GetGroundLayer();
            var white = EditorPlatformFactory.LoadWhiteSquare();
            var frictionless = EditorPlatformFactory.GetOrCreateFrictionlessMaterial();
            int skinIndex = 0;

            var bindings = new SerializedObject(layouts);
            var bindingArray = bindings.FindProperty("bindings");
            // 뒤에서부터 — 바인딩을 지워도 앞 인덱스가 밀리지 않는다. 새 루트는 끝에 붙였으므로 다른 루트의 형제 순번도 그대로다.
            var ordered = new List<(int Index, RoomData Room, GameObject Root)>();
            for (int i = 0; i < bindingArray.arraySize; i++)
            {
                var element = bindingArray.GetArrayElementAtIndex(i);
                ordered.Add((i, element.FindPropertyRelative("room").objectReferenceValue as RoomData,
                             element.FindPropertyRelative("layoutRoot").objectReferenceValue as GameObject));
            }

            foreach (var (index, room, root) in ordered.OrderByDescending(b => b.Index))
            {
                var entry = rooms.FirstOrDefault(r => r.Room == room);
                if (entry.Room == null || root == null) continue;
                scopes.Add(PathOf(root.transform));

                if (!RoomLayoutBuilder.TryGetPlatforms(entry.AssetName, out _))
                {
                    removedRoots.Add(root.name);
                    Object.DestroyImmediate(root);
                    bindingArray.DeleteArrayElementAtIndex(index);
                }
            }
            bindings.ApplyModifiedPropertiesWithoutUndo();

            // 원래 C2 적용과 같은 순서(바인딩 순 · 자식 순)로 타일 스킨 번호를 매긴다.
            foreach (var (_, room, root) in ordered.OrderBy(b => b.Index))
            {
                var entry = rooms.FirstOrDefault(r => r.Room == room);
                if (entry.Room == null || root == null) continue;
                if (!RoomLayoutBuilder.TryGetPlatforms(entry.AssetName, out var legacy)) continue;

                ClearChildren(root.transform);
                for (int i = 0; i < legacy.Length; i++)
                {
                    var go = EditorPlatformFactory.CreatePlatform(root.transform, $"Platform_{i + 1:D2}", legacy[i].Pos, legacy[i].Size,
                                                                  groundLayer, white, frictionless, EditorPlatformFactory.DefaultPlatformColor);
                    var skin = BuildPlatformSkin(go.transform, legacySprites, skinIndex++);
                    if (skin != null) restoredSkins.Add(skin);
                    var graybox = go.GetComponent<SpriteRenderer>();
                    if (graybox != null) restoredGrayboxes.Add(graybox);
                }
            }

            var so = new SerializedObject(presenter);
            foreach (var root in ReadArray<GameObject>(so.FindProperty("roomArtReplacedRoots")))
            {
                // 적용은 대체 대상을 에디터에서 끄지 않지만, 손으로 꺼졌어도 기존 저장 모습(켜짐)으로 돌린다 — 보스 방 배경만 원래 꺼 둔다.
                if (root != null && root != so.FindProperty("bossArenaRoot").objectReferenceValue && !root.activeSelf) root.SetActive(true);
            }
            var shared = ReadArray<GameObject>(so.FindProperty("sharedRoots")).Where(go => go != null).Concat(restoredSkins).ToList();
            var grayboxes = ReadArray<Renderer>(so.FindProperty("grayboxRenderers")).Where(r => r != null).Concat(restoredGrayboxes).ToList();
            SetArray(so.FindProperty("sharedRoots"), shared.Cast<Object>().ToList());
            SetArray(so.FindProperty("grayboxRenderers"), grayboxes.Cast<Object>().ToList());
            so.FindProperty("roomArtRoom").objectReferenceValue = null;
            so.FindProperty("roomArtWholeStage").boolValue = false;
            so.FindProperty("roomArtRoot").objectReferenceValue = null;
            so.FindProperty("roomArtReplacedRoots").arraySize = 0;
            so.ApplyModifiedPropertiesWithoutUndo();

            string outside = DiffCollidersOutside(before, SnapshotColliders(scene), scopes);
            if (outside != null)
            {
                Debug.LogError($"{AllRoomsLog} 복원 중 Stage1 밖 콜라이더가 바뀌었다 — 저장하지 않는다.\n{outside}");
                return Abort(scene);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
            {
                Debug.LogError($"{AllRoomsLog} 씬 저장 실패");
                return false;
            }

            Debug.Log($"{AllRoomsLog} 복원 완료 — 원래 발판 {restoredSkins.Count}개(C2 타일 스킨) · 지운 방 루트 {removedRoots.Count}개({string.Join(", ", removedRoots)}) · " +
                      $"방 아트 루트 {removedArt}개 삭제. 적 배치·문·제단의 바닥 판정은 런타임 코드라 복원 대상이 아니다(평지에서는 예전과 같은 값).");
            return true;
        }

        // ───────────────────────────────────────────────────────────── 공용

        /// <summary>Stage1 단계 선택지를 순서대로(중복 없이) 모은다. 규격이 없는 방이 있으면 멈춘다.</summary>
        internal static bool LoadStage1Rooms(out StageData stage, out RoomData bossRoom, out List<Stage1Room> rooms)
        {
            stage = AssetDatabase.LoadAssetAtPath<StageData>(StageAssetPath);
            bossRoom = AssetDatabase.LoadAssetAtPath<RoomData>(BossRoomAssetPath);
            rooms = new List<Stage1Room>();
            if (stage == null || bossRoom == null)
            {
                Debug.LogError($"{AllRoomsLog} 데이터 없음: {StageAssetPath} / {BossRoomAssetPath}");
                return false;
            }

            for (int i = 0; i < stage.steps.Count; i++)
            {
                var options = stage.steps[i]?.options;
                if (options == null) continue;
                foreach (var room in options)
                {
                    if (room == null || rooms.Any(r => r.Room == room)) continue;
                    rooms.Add(new Stage1Room(room, i, Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(room))));
                }
            }

            var missing = rooms.Where(r => FindRoomSpec(r.AssetName) == null || !r.Room.IsMapRoom).Select(r => r.AssetName).ToList();
            if (missing.Count == 0 && rooms.Count > 0) return true;
            Debug.LogError($"{AllRoomsLog} 규격 없는 방 또는 맵 방이 아닌 방: {string.Join(", ", missing)} — AllRoomsSpec.cs 를 먼저 맞출 것.");
            return false;
        }

        private static bool FindAllRoomsTargets(Scene scene, out GameObject envRoot, out StageEnvironmentPresenter presenter,
                                                out GameObject ground, out RoomLayoutController layouts, out PlayerCameraFollow follow)
        {
            envRoot = FindRoot(scene, RootName);
            presenter = envRoot != null ? envRoot.GetComponent<StageEnvironmentPresenter>() : null;
            ground = FindRoot(scene, "Ground");
            layouts = Object.FindAnyObjectByType<RoomLayoutController>(FindObjectsInactive.Include);
            follow = Object.FindAnyObjectByType<PlayerCameraFollow>(FindObjectsInactive.Include);
            if (presenter != null && ground != null && layouts != null && follow != null) return true;

            Debug.LogError($"{AllRoomsLog} Run 씬 전제 없음 — 표현 컴포넌트 {presenter != null} · Ground {ground != null} · " +
                           $"RoomLayoutController {layouts != null} · PlayerCameraFollow {follow != null}. Stage1 환경 배선을 먼저 확인할 것.");
            return false;
        }

        /// <summary>방에 묶인 레이아웃 루트. 없으면 RoomLayouts 끝에 새 루트를 붙이고 바인딩을 추가한다(에디터 미리보기는 꺼 둔다).</summary>
        private static GameObject FindOrCreateLayoutRoot(RoomLayoutController layouts, Stage1Room entry)
        {
            var root = FindBoundLayoutRoot(layouts, entry.Room);
            if (root != null) return root;

            root = new GameObject(NewLayoutRootPrefix + entry.AssetName);
            root.transform.SetParent(layouts.transform, false);
            root.SetActive(false);

            var so = new SerializedObject(layouts);
            var bindings = so.FindProperty("bindings");
            int index = bindings.arraySize;
            bindings.arraySize = index + 1;
            var element = bindings.GetArrayElementAtIndex(index);
            element.FindPropertyRelative("room").objectReferenceValue = entry.Room;
            element.FindPropertyRelative("layoutRoot").objectReferenceValue = root;
            so.ApplyModifiedPropertiesWithoutUndo();
            return root;
        }

        internal static GameObject FindBoundLayoutRoot(RoomLayoutController layouts, RoomData room)
        {
            var bindings = new SerializedObject(layouts).FindProperty("bindings");
            for (int i = 0; i < bindings.arraySize; i++)
            {
                var element = bindings.GetArrayElementAtIndex(i);
                if (element.FindPropertyRelative("room").objectReferenceValue != room) continue;
                if (element.FindPropertyRelative("layoutRoot").objectReferenceValue is GameObject root) return root;
            }
            return null;
        }

        private static void ClearChildren(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--) Object.DestroyImmediate(parent.GetChild(i).gameObject);
        }

        /// <summary>표현 루트 아래 방 아트 루트(이전 대표방 전용 포함)를 지운다.</summary>
        private static int RemoveRoomArtRoots(GameObject envRoot)
        {
            var previous = envRoot.transform.Cast<Transform>().Where(t => t.name.StartsWith(RoomArtRootPrefix)).ToList();
            foreach (var t in previous) Object.DestroyImmediate(t.gameObject);
            return previous.Count;
        }

        /// <summary>콜라이더 비교 — 경로가 <paramref name="scopes"/> 아래인 것은 뺀다(그 안은 따로 검사한다).</summary>
        private static string DiffCollidersOutside(Dictionary<string, string> before, Dictionary<string, string> after, IList<string> scopes)
        {
            bool IsInScope(string key) => scopes.Any(scope => key == scope || key.StartsWith(scope + "/"));
            var outsideBefore = before.Where(pair => !IsInScope(pair.Key)).ToDictionary(pair => pair.Key, pair => pair.Value);
            var outsideAfter = after.Where(pair => !IsInScope(pair.Key)).ToDictionary(pair => pair.Key, pair => pair.Value);
            return DiffColliders(outsideBefore, outsideAfter);
        }

        /// <summary>저장하지 않은 변경을 버린다(씬 다시 열기).</summary>
        private static bool Abort(Scene scene)
        {
            EditorSceneManager.OpenScene(scene.path, OpenSceneMode.Single);
            return false;
        }
    }
}
#endif
