#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using Abyss.Runtime.Camera;
using Abyss.Runtime.Enemy;
using Abyss.Runtime.Form;
using Abyss.Runtime.Player;
using Abyss.Runtime.Stage;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Abyss.EditorTools
{
    /// <summary>
    /// Stage1 전 방 적용 결과 검증 — <b>읽기 전용</b>(표현 전환 시뮬레이션 뒤 저장하지 않고 씬을 다시 연다). 로그 접두어 <c>[S1-ALL]</c>.
    /// <list type="bullet">
    /// <item>Stage1 11방: 콜라이더 = 규격 · 스킨 일치 · 필수 경로 오르기 한도(실제 점프 값) · 착지 폭 · 석판 머리 공간 · 선택 석판 도달 점프 수 ·
    /// 입구/문/제단 평탄 · 적 등장 바닥·가장자리 거리 · 보스 평탄 폭 · 지형 분포 · 배경 덮임.</item>
    /// <item>Stage2/3: 레이아웃 콜라이더가 RoomLayoutBuilder 원래 값 그대로인가(이전 불변 검사), 새 아트 스킨이 없는가.</item>
    /// <item>표현 전환: Stage1 모든 방 → Stage2 방 → 스테이지 밖.</item>
    /// </list>
    /// </summary>
    public static partial class Stage1EnvironmentWiring
    {
        private const string PlayerPrefabPath = AbyssPaths.PlayerPrefabs + "/Player.prefab";

        /// <summary>Stage1 전 방 검증(메뉴). 미적용이면 배선 항목이 FAIL 이다.</summary>
        public static bool ValidateAllRooms()
        {
            if (!LoadStage1Rooms(out var stage, out var bossRoom, out var rooms)) return false;

            var report = new C2Report("S1-ALL");
            CheckRoomArtFiles(report);
            var sprites = CheckRoomArtImport(report);

            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return false;
            var scene = EditorSceneManager.OpenScene(RunScenePath, OpenSceneMode.Single);
            if (!FindAllRoomsTargets(scene, out var envRoot, out var presenter, out var ground, out var layouts, out _))
                return report.Finish();

            var so = new SerializedObject(presenter);
            var artRoot = so.FindProperty("roomArtRoot").objectReferenceValue as GameObject;
            bool isWired = artRoot != null && so.FindProperty("roomArtWholeStage").boolValue && artRoot.transform.parent == envRoot.transform;
            report.Check("전 방 아트 배선", isWired, isWired ? $"스테이지 전체 모드 → {artRoot.name}" : "미적용 — Apply 전 상태");

            if (isWired)
            {
                string groundFit = CheckGroundArtFit(artRoot.transform, WorldRect(ground.GetComponent<BoxCollider2D>()));
                report.Check("공용 지면 그림 일치", groundFit == null, groundFit ?? "좌우·윗선·두께 ≥ 5.5");
                var layers = artRoot.GetComponentsInChildren<ParallaxLayer>(true);
                var layer = layers.Length == 1 ? new SerializedObject(layers[0]) : null;
                bool isLayerOk = layer != null &&
                                 Mathf.Approximately(layer.FindProperty("depth").floatValue, RoomArtBackgroundDepth) &&
                                 Mathf.Approximately(layer.FindProperty("wrapWidth").floatValue, 0f);
                report.Check("배경 층", isLayerOk, $"시차 층 {layers.Length}개(한 장 · 깊이 {RoomArtBackgroundDepth} · 반복 없음)");
            }

            if (sprites != null) ValidateStage1Layouts(report, scene, stage, bossRoom, rooms, layouts, sprites[KeyBackground], out _);
            ValidateOtherStageLayouts(report, layouts, rooms);
            if (isWired) ValidateAllRoomsSwitching(report, presenter, so, stage, bossRoom, rooms, artRoot);

            EditorSceneManager.OpenScene(RunScenePath, OpenSceneMode.Single);
            return report.Finish();
        }

        // ───────────────────────────────────────────────────────────── Stage1 방별

        /// <summary>Stage1 방마다 한 줄 PASS/FAIL. <paramref name="summary"/> 는 방별 요약(적용 로그용).</summary>
        internal static void ValidateStage1Layouts(C2Report report, Scene scene, StageData stage, RoomData bossRoom, List<Stage1Room> rooms,
                                                   RoomLayoutController layouts, Sprite background, out string summary)
        {
            var jump = LoadJumpModel(scene);
            bool isJumpOk = RequiredRiseMax <= jump.SingleApex * 0.6f + Epsilon;
            report.Check("도약 계산(실제 플레이어·폼 값)", isJumpOk, jump.Describe());

            var follow = Object.FindAnyObjectByType<PlayerCameraFollow>(FindObjectsInactive.Include);
            float cameraOffsetY = follow != null ? new SerializedObject(follow).FindProperty("offset").vector3Value.y : 2f;
            float restCameraY = SpecGroundTop + cameraOffsetY;

            var lines = new List<string>();
            foreach (var entry in rooms)
            {
                var spec = FindRoomSpec(entry.AssetName);
                var problems = new List<string>();
                var info = new List<string>();

                var root = FindBoundLayoutRoot(layouts, entry.Room);
                if (root == null) problems.Add("레이아웃 바인딩 없음");
                else CheckSceneMatchesSpec(root, spec, problems);

                CheckRoomGeometry(entry, spec, stage, bossRoom, jump, problems, info);

                float maxTop = spec.Terraces.Select(t => t.Top).Concat(spec.Slabs.Select(s => s.Top)).DefaultIfEmpty(SpecGroundTop).Max();
                float maxCameraY = maxTop + cameraOffsetY + jump.ApexFor(jump.MaxJumps);
                string cover = CheckBackgroundCoverage(background, entry.Room.mapLength, SpecGroundTop, restCameraY, maxCameraY, out string coverage);
                if (cover != null) problems.Add(cover);
                info.Add(coverage);

                string head = $"{entry.AssetName}(길이 {entry.Room.mapLength}) — {spec.Role}";
                report.Check(head, problems.Count == 0,
                             (problems.Count > 0 ? "\n    ✗ " + string.Join("\n    ✗ ", problems) : "") + "\n    " + string.Join("\n    ", info));
                lines.Add($"  {entry.AssetName}: {(problems.Count == 0 ? "PASS" : "FAIL")} · {info.FirstOrDefault()}");
            }
            summary = string.Join("\n", lines);
        }

        /// <summary>씬 루트의 지형이 규격과 같은가 — 콜라이더 사각형·Ground 레이어·스킨(아트 종류) 일치.</summary>
        private static void CheckSceneMatchesSpec(GameObject root, Stage1RoomSpec spec, List<string> problems)
        {
            var wanted = spec.Terraces.Select(t => (Box: new Rect(t.Center - t.Size * 0.5f, t.Size), IsTerrace: true))
                             .Concat(spec.Slabs.Select(s => (Box: new Rect(s.Center - s.Size * 0.5f, s.Size), IsTerrace: false))).ToList();
            var actual = root.transform.Cast<Transform>().Where(c => c.GetComponent<BoxCollider2D>() != null).ToList();
            if (actual.Count != wanted.Count) problems.Add($"지형 {actual.Count}개 · 규격 {wanted.Count}개");

            int groundLayer = LayerMask.NameToLayer(EditorPlatformFactory.GroundLayerName);
            foreach (var platform in actual)
            {
                var rect = WorldRect(platform.GetComponent<BoxCollider2D>());
                int match = wanted.FindIndex(w => Near(w.Box.xMin, rect.xMin) && Near(w.Box.xMax, rect.xMax) &&
                                                  Near(w.Box.yMin, rect.yMin) && Near(w.Box.yMax, rect.yMax));
                if (match < 0) { problems.Add($"규격 밖 지형 {PathOf(platform)} {Fmt(rect)}"); continue; }
                if (platform.gameObject.layer != groundLayer) problems.Add($"{platform.name} Ground 레이어 아님");
                string fit = CheckTerrainSkinFit(platform, wanted[match].IsTerrace);
                if (fit != null) problems.Add(fit);
                wanted.RemoveAt(match);
            }
            foreach (var w in wanted) problems.Add($"규격 지형 없음 {Fmt(w.Box)}");
        }

        /// <summary>규격만으로 하는 이동·배치 검사(씬과 같은지는 위에서 따로 본다).</summary>
        private static void CheckRoomGeometry(Stage1Room entry, Stage1RoomSpec spec, StageData stage, RoomData bossRoom,
                                              Stage1JumpModel jump, List<string> problems, List<string> info)
        {
            var room = entry.Room;
            float half = room.mapLength * 0.5f;
            var segments = SpecSegments(spec, half);

            // 범위·겹침
            foreach (var t in spec.Terraces)
                if (t.XMin < -half - Epsilon || t.XMax > half + Epsilon || t.Top <= SpecGroundTop) problems.Add($"테라스 {t.XMin}~{t.XMax} 범위/높이 이상");
            foreach (var s in spec.Slabs)
                if (s.XMin < -half || s.XMax > half) problems.Add($"석판 x {s.X} 맵 밖");
            for (int i = 0; i < spec.Terraces.Length; i++)
                for (int j = i + 1; j < spec.Terraces.Length; j++)
                    if (spec.Terraces[i].XMin < spec.Terraces[j].XMax - Epsilon && spec.Terraces[j].XMin < spec.Terraces[i].XMax - Epsilon)
                        problems.Add($"테라스 겹침 {i}·{j}");

            // 필수 경로(입구 → 출구, 바닥 구간을 왼쪽부터 걷는다)
            float riseLimit = Mathf.Min(RequiredRiseMax, jump.SingleApex * 0.6f);
            int rises = 0, drops = 0;
            float maxRise = 0f;
            for (int i = 0; i < segments.Count; i++)
            {
                var seg = segments[i];
                if (seg.XMax - seg.XMin < MinLandingWidth - Epsilon) problems.Add($"착지면 {seg.XMin}~{seg.XMax} 폭 {seg.XMax - seg.XMin} < {MinLandingWidth}");
                if (i == 0) continue;
                float delta = seg.Top - segments[i - 1].Top;
                if (delta > Epsilon) { rises++; maxRise = Mathf.Max(maxRise, delta); }
                else if (delta < -Epsilon) drops++;
                if (delta > riseLimit + Epsilon) problems.Add($"x {seg.XMin} 오르기 {delta:0.##} > 한도 {riseLimit:0.##}");
            }
            if (rises == 0 || drops == 0) problems.Add($"기본 경로 높이 변화 부족(오르기 {rises} · 내려오기 {drops})");
            string path = string.Join(" → ", segments.Select(s => $"[{s.XMin:0.#}~{s.XMax:0.#}] {s.Top:0.##}"));
            info.Add($"필수 경로 {path} · 오르기 {rises}회(최대 {maxRise:0.##}, 최저 폼 1회 최고 {jump.SingleApex:0.##}의 {maxRise / jump.SingleApex:P0}) · 내려오기 {drops}회");

            // 선택 석판 — 머리 공간과 도달 점프 수
            var optional = new List<string>();
            foreach (var s in spec.Slabs)
            {
                float floorUnder = SampleMaxFloor(spec, s.XMin - 0.5f, s.XMax + 0.5f);
                float headroom = s.Bottom - floorUnder;
                if (headroom < jump.PlayerHeight + HeadroomMargin - Epsilon)
                    problems.Add($"석판 x {s.X} 아래 머리 공간 {headroom:0.##} < {jump.PlayerHeight + HeadroomMargin:0.##}(기본 보행을 막음)");

                float launch = SampleMaxFloor(spec, s.XMin - 3f, s.XMax + 3f);
                float rise = s.Top - launch;
                int need = 1;
                while (need <= jump.MaxJumps && rise > jump.ApexFor(need) * OptionalReachRatio) need++;
                if (need > 2) problems.Add($"석판 x {s.X} 도달에 점프 {need}회 필요(선택 경로는 이단 이하)");
                optional.Add($"석판 x {s.XMin:0.#}~{s.XMax:0.#} 윗면 {s.Top} ← 발판 {launch:0.##}에서 {rise:0.##} 오르기 = 점프 {need}회");
            }
            info.Add(optional.Count > 0 ? "선택 경로 " + string.Join(" · ", optional) : "선택 경로 없음(서비스/보스 평탄 우선)");

            // 입구·문·제단
            float entrance = EntranceX(half);
            CheckFlat(segments, entrance - EntranceClear, entrance + EntranceClear, true, $"입구 x {entrance}", problems);
            var doors = DoorXs(half, DoorCountAfter(stage, entry.StepIndex));
            foreach (float door in doors) CheckFlat(segments, door - DoorClear, door + DoorClear, true, $"문 x {door}", problems);
            string altar = "";
            if (room.IsFormRewardRoom || room.IsWeaponRewardRoom)
            {
                CheckFlat(segments, -AltarClear, AltarClear, false, "제단 x 0", problems);
                float altarFloor = SpecFloorAt(spec, 0f);
                foreach (var slab in spec.Slabs.Where(o => o.XMax > -AltarClear && o.XMin < AltarClear && o.Bottom < altarFloor + AltarHeadroom))
                    problems.Add($"제단 위 석판 x {slab.X}(머리 위 {AltarHeadroom} 안)");
                altar = $" · 제단 x 0 바닥 {altarFloor:0.##}(씬 높이 +{altarFloor - SpecGroundTop:0.##})";
            }
            info.Add($"입구 x {entrance} · 문 x {string.Join("/", doors)}(바닥 {SpecGroundTop}){altar}");

            // 적
            var spawns = CollectSpawns(room, half);
            var spawnInfo = new List<string>();
            foreach (var (data, x, label) in spawns)
            {
                var seg = SegmentAt(segments, x);
                float edge = Mathf.Min(x - seg.XMin, seg.XMax - x);
                bool isRanged = data != null && data.isRanged;
                if (edge < SpawnEdgeMin - Epsilon) problems.Add($"{label} x {x} 면 가장자리 {edge:0.##} < {SpawnEdgeMin}");
                foreach (var slab in spec.Slabs.Where(o => x > o.XMin - 1f && x < o.XMax + 1f && o.Bottom - seg.Top < jump.PlayerHeight + HeadroomMargin))
                    problems.Add($"{label} x {x} 위 석판 x {slab.X} 가 가깝다");
                if (data != null && data.isRanged == false && seg.XMax - seg.XMin < MinLandingWidth) problems.Add($"{label} 근접 적 전투면 폭 부족");
                spawnInfo.Add($"{label} {data?.enemyId ?? "?"}{(isRanged ? "(원거리)" : "")} x {x} → 바닥 {seg.Top:0.##}[{seg.XMin:0.#}~{seg.XMax:0.#}] 등장 y {seg.Top + 0.5f:0.##}");
            }
            if (room.roomType == RoomType.Boss || room == bossRoom)
            {
                foreach (var (_, x, _) in spawns)
                {
                    var seg = SegmentAt(segments, x);
                    if (seg.XMax - seg.XMin < BossFlatMin) problems.Add($"보스 전투면 폭 {seg.XMax - seg.XMin} < {BossFlatMin}");
                }
            }
            info.Add(spawnInfo.Count > 0 ? "적 " + string.Join(" · ", spawnInfo) : "적 없음(비전투 — 서비스 모달은 입구에서 바로 열린다)");

            // 분포 — 긴 방은 입구~출구 전 구간에 지형이 있어야 한다
            if (room.mapLength >= 36f)
            {
                float minX = spec.Terraces.Select(t => t.XMin).Concat(spec.Slabs.Select(s => s.XMin)).DefaultIfEmpty(0f).Min();
                float maxX = spec.Terraces.Select(t => t.XMax).Concat(spec.Slabs.Select(s => s.XMax)).DefaultIfEmpty(0f).Max();
                if (minX > -half + 8f || maxX < half - 12f) problems.Add($"지형 분포 x {minX}~{maxX} — 입구·출구 쪽이 비었다");
                info.Add($"지형 분포 x {minX}~{maxX}");
            }
        }

        private static float SampleMaxFloor(Stage1RoomSpec spec, float xMin, float xMax)
        {
            float max = SpecGroundTop;
            for (float x = xMin; x <= xMax + Epsilon; x += 0.25f) max = Mathf.Max(max, SpecFloorAt(spec, x));
            return max;
        }

        /// <summary>[a, b] 가 한 구간 안이고(평탄) — <paramref name="mustBeGround"/> 면 공용 지면 높이인가.</summary>
        private static void CheckFlat(List<(float XMin, float XMax, float Top)> segments, float a, float b, bool mustBeGround, string label, List<string> problems)
        {
            var seg = SegmentAt(segments, (a + b) * 0.5f);
            if (seg.XMin > a + Epsilon || seg.XMax < b - Epsilon) problems.Add($"{label} 주변 [{a}~{b}] 평탄하지 않음");
            else if (mustBeGround && !Near(seg.Top, SpecGroundTop)) problems.Add($"{label} 바닥 {seg.Top} ≠ 기본 높이 {SpecGroundTop}");
        }

        /// <summary>다음 단계 선택지 수 = 보상 문 수. 스테이지 마지막 방은 다음 스테이지 문 하나.</summary>
        private static int DoorCountAfter(StageData stage, int stepIndex)
        {
            int next = stepIndex + 1;
            if (next >= stage.steps.Count) return 1;
            return stage.steps[next]?.options?.Count(o => o != null) ?? 1;
        }

        /// <summary>첫 무리(배치 또는 흩어 스폰 — StageDirector.Map 과 같은 식)와 추가 소환의 적·x.</summary>
        private static List<(EnemyData Data, float X, string Label)> CollectSpawns(RoomData room, float half)
        {
            var result = new List<(EnemyData, float, string)>();
            float Clamp(float x) => Mathf.Clamp(x, -half + 0.5f, half - 0.5f);

            if (room.placements.Count > 0)
            {
                foreach (var p in room.placements.Where(p => p != null)) result.Add((p.data, Clamp(p.x), "첫 무리"));
            }
            else
            {
                var spread = room.enemies.Where(e => e != null).SelectMany(e => Enumerable.Repeat(e.data, e.count)).ToList();
                float left = -half + 7f, right = half - 3f;   // MAP_SPREAD_LEFT/RIGHT_INSET
                for (int i = 0; i < spread.Count; i++)
                {
                    float t = spread.Count == 1 ? 0.5f : i / (spread.Count - 1f);
                    result.Add((spread[i], Clamp(Mathf.Lerp(left, right, t)), "흩어 스폰"));
                }
            }

            foreach (var r in room.reinforcements.Where(r => r != null))
            {
                string label = r.trigger == ReinforcementTrigger.ReachX ? $"추가(x≥{r.value})" : $"추가(처치 {r.value})";
                foreach (var p in r.enemies.Where(p => p != null)) result.Add((p.data, Clamp(p.x), label));
            }
            return result;
        }

        /// <summary>
        /// 도약 모델 — Run 씬 플레이어(프리팹 인스턴스 오버라이드 포함) + 폼 에셋 전부. 씬에 없으면 프리팹.
        /// 중력 = |Physics2D.gravity.y| × gravityScale. 키 = BoxCollider2D 높이 × 배율.
        /// </summary>
        private static Stage1JumpModel LoadJumpModel(Scene scene)
        {
            var player = scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<PlayerCharacter>(true)).FirstOrDefault();
            string source = "Run 씬 플레이어";
            if (player == null)
            {
                player = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath)?.GetComponent<PlayerCharacter>();
                source = PlayerPrefabPath;
            }

            float jumpForce = 12f, moveSpeed = 6f, gravityScale = 3f, height = 2f;
            if (player != null)
            {
                var so = new SerializedObject(player);
                jumpForce = so.FindProperty("jumpForce").floatValue;
                moveSpeed = so.FindProperty("moveSpeed").floatValue;
                if (player.TryGetComponent<Rigidbody2D>(out var body)) gravityScale = body.gravityScale;
                if (player.TryGetComponent<BoxCollider2D>(out var box)) height = box.size.y * Mathf.Abs(player.transform.lossyScale.y);
            }
            else source = "플레이어 없음 — 기본값";

            var forms = AssetDatabase.FindAssets("t:FormData", new[] { AbyssPaths.Forms })
                                     .Select(g => AssetDatabase.LoadAssetAtPath<FormData>(AssetDatabase.GUIDToAssetPath(g)))
                                     .Where(f => f != null).ToList();
            float minMultiplier = forms.Count > 0 ? forms.Min(f => f.moveSpeedMultiplier) : 1f;
            int minJumps = forms.Count > 0 ? forms.Min(f => Mathf.Max(1, f.jumpCount)) : 1;
            int maxJumps = forms.Count > 0 ? forms.Max(f => Mathf.Max(1, f.jumpCount)) : 2;
            string formList = string.Join(", ", forms.Select(f => $"{f.formId} 점프{f.jumpCount}·속도×{f.moveSpeedMultiplier}"));

            float gravity = Mathf.Abs(Physics2D.gravity.y) * gravityScale;
            return new Stage1JumpModel(jumpForce, gravity, moveSpeed * minMultiplier, height, minJumps, maxJumps,
                                       $"{source} · 폼 {forms.Count}개: {formList}");
        }

        // ───────────────────────────────────────────────────────────── Stage2/3 · 전환

        /// <summary>Stage1 이 아닌 바인딩 — 콜라이더가 RoomLayoutBuilder 원래 값 그대로이고 새 아트 스킨이 없는가.</summary>
        private static void ValidateOtherStageLayouts(C2Report report, RoomLayoutController layouts, List<Stage1Room> rooms)
        {
            var problems = new List<string>();
            int checkedRooms = 0;
            var bindings = new SerializedObject(layouts).FindProperty("bindings");
            for (int i = 0; i < bindings.arraySize; i++)
            {
                var element = bindings.GetArrayElementAtIndex(i);
                var room = element.FindPropertyRelative("room").objectReferenceValue as RoomData;
                var root = element.FindPropertyRelative("layoutRoot").objectReferenceValue as GameObject;
                if (room == null || root == null || rooms.Any(r => r.Room == room)) continue;

                string assetName = System.IO.Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(room));
                if (!RoomLayoutBuilder.TryGetPlatforms(assetName, out var legacy)) { problems.Add($"{assetName} 원래 규격 없음"); continue; }
                checkedRooms++;

                var rects = root.transform.Cast<Transform>().Select(c => c.GetComponent<BoxCollider2D>()).Where(c => c != null).Select(WorldRect).ToList();
                if (rects.Count != legacy.Length) problems.Add($"{assetName} 발판 {rects.Count}개 ≠ 원래 {legacy.Length}개");
                foreach (var (pos, size) in legacy)
                {
                    if (!rects.Any(r => Near(r.center.x, pos.x) && Near(r.center.y, pos.y) && Near(r.width, size.x) && Near(r.height, size.y)))
                        problems.Add($"{assetName} 발판 ({pos.x},{pos.y},{size.x}x{size.y}) 바뀜");
                }
                if (root.GetComponentsInChildren<Transform>(true).Any(t => t.name == TerraceArtName || t.name == SlabArtName))
                    problems.Add($"{assetName} 에 Stage1 새 아트 스킨이 있다");
            }
            report.Check("Stage2/3 레이아웃 불변", problems.Count == 0 && checkedRooms > 0,
                         problems.Count == 0 ? $"{checkedRooms}방 — RoomLayoutBuilder 원래 좌표 그대로 · 새 아트 없음" : string.Join(" · ", problems));
        }

        /// <summary>Stage1 11방 → Stage2 방 → 스테이지 밖 → Stage1 첫 방 순으로 표현을 돌린다(저장 안 함).</summary>
        private static void ValidateAllRoomsSwitching(C2Report report, StageEnvironmentPresenter presenter, SerializedObject so,
                                                      StageData stage, RoomData bossRoom, List<Stage1Room> rooms, GameObject artRoot)
        {
            var replaced = ReadArray<GameObject>(so.FindProperty("roomArtReplacedRoots")).Where(go => go != null).ToList();
            var grayboxes = ReadArray<Renderer>(so.FindProperty("grayboxRenderers"));
            var sequence = AssetDatabase.LoadAssetAtPath<StageSequenceData>(SequenceAssetPath);
            var otherRoom = sequence != null
                ? sequence.stages.Where(s => s != null && s != stage).SelectMany(s => s.steps).Where(step => step?.options != null)
                          .SelectMany(step => step.options).FirstOrDefault(r => r != null)
                : null;

            var order = rooms.Select(r => r.Room).Concat(new[] { otherRoom, null, rooms[0].Room }).ToList();
            var problems = new List<string>();
            if (replaced.Count < 3) problems.Add($"대체 대상 {replaced.Count}개(일반 배경·보스 배경·지면 스킨 필요)");
            if (otherRoom == null) problems.Add("비교할 Stage2/3 방 없음");

            foreach (var entered in order)
            {
                var look = StageEnvironmentRule.Resolve(stage, bossRoom, entered);
                presenter.Apply(look, entered);
                bool isStage1 = look != StageEnvironmentLook.Hidden;
                string tag = entered != null ? entered.roomId : "스테이지 밖";

                if (artRoot.activeSelf != isStage1 || presenter.IsRoomArtShown != isStage1) problems.Add($"{tag}: 방 아트 {artRoot.activeSelf}");
                if (isStage1 && replaced.Any(go => go.activeInHierarchy)) problems.Add($"{tag}: 옛 배경·지면 스킨이 보인다");
                if (grayboxes.Any(r => r != null && r.enabled != !isStage1)) problems.Add($"{tag}: 그레이박스");
                if (!isStage1 && presenter.GetComponentsInChildren<SpriteRenderer>(false).Length > 0) problems.Add($"{tag}: Stage1 렌더러가 켜져 있다");
            }
            report.Check("표현 전환(Stage1 전 방·Stage2/3·밖·복귀)", problems.Count == 0,
                         problems.Count == 0 ? $"{order.Count}단계 모두 — Stage1 에서만 새 배경·지면, 밖에서는 기존 규칙" : string.Join(" · ", problems));
        }
    }
}
#endif
