using System;
using System.Collections.Generic;
using Abyss.Runtime.Camera;
using Abyss.Runtime.Enemy;
using Abyss.Runtime.Events;
using Abyss.Runtime.Feedback;
using Abyss.Runtime.Flow;
using Abyss.Runtime.Player;
using UnityEngine;

namespace Abyss.Runtime.Stage
{
    /// <summary>
    /// 스크롤 맵 방(<see cref="RoomData.IsMapRoom"/>) 담당 — 17-stage-flow-boss-presentation §1.
    ///
    /// 스컬 구조: <b>가두지 않는다.</b> 맵 곳곳에 배치된 적 + 조건부 추가 소환을 모두 잡으면 방이 클리어되고,
    /// 보상(기존 제단·골드) 뒤에 오른쪽 끝에 <b>보상 문</b>이 선다. 문에 들어가 다음 방을 고른다 —
    /// 옛 방의 갈림길 모달(<see cref="UI.NodeMapPanel"/>)을 월드로 옮긴 것이다.
    ///
    /// 옛 아레나 방(<c>mapLength 0</c>)은 이 파일을 거치지 않는다 — 스테이지 단위로 옮겨 가는 동안 둘이 섞여 돈다.
    /// 맵 좌표는 x=0을 중심으로 좌우 절반씩이다. 방마다 벽·문을 새로 만들고 방을 떠날 때 치운다.
    /// </summary>
    public sealed partial class StageDirector
    {
        private const float MAP_ENTRANCE_INSET = 2f;        // 입구: 왼쪽 벽에서 안쪽
        private const float MAP_SPREAD_LEFT_INSET = 7f;     // 흩어 스폰의 입구 쪽 여백 — 들어서자마자 맞지 않게
        private const float MAP_SPREAD_RIGHT_INSET = 3f;
        private const float DOOR_RIGHT_INSET = 3f;
        private const float DOOR_SPACING = 3.5f;
        private const float WALL_THICKNESS = 1f;
        private const float WALL_HEIGHT = 40f;
        private const float CAMERA_BELOW_GROUND = 5.5f;
        private const float CAMERA_ABOVE_GROUND = 30f;
        private const float GROUND_PROBE_TOP = 30f;
        private const float MAX_MAP_LENGTH_ON_CURRENT_GROUND = 44f;  // Run 씬 바닥 폭 45.6 안쪽
        private const float SUMMON_EFFECT_RADIUS = 0.9f;
        private const float SUMMON_EFFECT_SECONDS = 0.45f;
        private const float DROP_ENTRY_HEIGHT = 7f;           // 스테이지 추락 뒤 첫 방: 입구 위 이만큼에서 떨어진다

        private static readonly Color SummonEffectColor = new Color(0.62f, 0.38f, 0.95f);
        private static readonly Color StageDoorColor = new Color(0.62f, 0.38f, 0.95f);

        private readonly List<Reinforcement> pendingReinforcements = new();
        private readonly List<RoomExitDoor> exitDoors = new();
        private Transform mapRoot;
        private int mapKillCount;
        private bool wasMapRoom;
        private bool isDoorTransitioning;
        private bool isDropEntry;   // 추락 연출 직후의 방 진입 — 입구 위 공중에 세운다(EnterStage 한 번 동안만 참)
        // 방에 들어설 때마다 오른다. 문 전환이 페이드를 기다리는 사이 치트 이동 등으로 방이 바뀌면 그 전환을 버린다.
        private int roomEntryVersion;
        private PlayerCharacter cachedPlayer;
        private PlayerCameraFollow cachedCamera;
        private PhysicsMaterial2D wallMaterial;

        // ───────────────────────── 방 진입 ─────────────────────────

        /// <summary>
        /// 첫 화면이 그려지기 전에 첫 방을 준비한다. 시퀀스는 다른 시스템을 기다려 0.2초 뒤 시작하는데,
        /// 그 사이 플레이어가 씬 배치 자리(가운데)에 보였다가 입구로 순간이동하는 것이 보였다(2026-09-26 사용자 보고).
        /// 방마다 입구가 맵 길이에서 나오므로 별도 스폰 지점 대신 첫 방의 입구를 여기서 미리 잡는다.
        /// 첫 단계가 갈림길이면 어느 방인지 모르므로 하지 않는다. 실제 진입(EnterRoom)이 같은 준비를 다시 해도 결과는 같다.
        /// </summary>
        private void Start()
        {
            if (!startOnEnable || sequence == null || sequence.stages.Count == 0) return;
            var stage = sequence.stages[0];
            if (stage == null || stage.steps.Count == 0) return;
            var step = stage.steps[0];
            if (step == null || step.IsBranch || step.First == null) return;
            PrepareRoomMap(step.First);
        }

        /// <summary>방 진입 준비. 직전 방의 벽·문·추가 소환을 치우고, 맵 방이면 벽·입구 배치·카메라 경계를 잡는다.</summary>
        private void PrepareRoomMap(RoomData room)
        {
            roomEntryVersion += 1;
            ClearMapObjects();

            var follow = ResolveCamera();
            float groundY = ProbeGround(0f, out var ground);
            // 바닥 좌우 끝 — 옛 방의 벽 자리이자 맵 길이의 상한. 값은 바닥 충돌체에서 읽는다(Run 씬 바닥 폭 45.6).
            float groundLeft = ground != null ? ground.bounds.min.x : -MAX_MAP_LENGTH_ON_CURRENT_GROUND * 0.5f;
            float groundRight = ground != null ? ground.bounds.max.x : MAX_MAP_LENGTH_ON_CURRENT_GROUND * 0.5f;

            if (!room.IsMapRoom)
            {
                // 옛 아레나 방에도 바닥 끝에 벽을 세운다. 예전에는 벽이 없어 바닥 끝(±22.8)이 낭떠러지였다 —
                // 한 화면 안에서만 싸워 드러나지 않다가, 맵 방에서 넘어와 걸어가면 떨어졌다(2026-09-26 사용자 보고).
                // 맵 방에서 넘어왔으면 아레나 한가운데로 돌려놓는다.
                if (wasMapRoom) MovePlayerTo(new Vector2(0f, groundY));
                CreateWall(groundLeft - WALL_THICKNESS * 0.5f, groundY);
                CreateWall(groundRight + WALL_THICKNESS * 0.5f, groundY);
                if (follow != null) follow.SetEnvironmentBounds(
                    new Vector2(groundLeft - WALL_THICKNESS, groundY - CAMERA_BELOW_GROUND),
                    new Vector2(groundRight + WALL_THICKNESS, groundY + CAMERA_ABOVE_GROUND));
                wasMapRoom = false;
                return;
            }

            wasMapRoom = true;
            float half = room.mapLength * 0.5f;
            if (-half < groundLeft || half > groundRight)
            {
                Debug.LogWarning($"[StageDirector] {room.roomId} mapLength {room.mapLength} — Run 바닥 폭({groundRight - groundLeft:0.#})을 넘는다. " +
                                 "벽 안쪽이라도 바닥 끝 밖은 허공이다(바닥 확장은 A3)");
            }

            CreateWall(-half - WALL_THICKNESS * 0.5f, groundY);
            CreateWall(half + WALL_THICKNESS * 0.5f, groundY);

            foreach (var reinforcement in room.reinforcements)
            {
                if (reinforcement != null) pendingReinforcements.Add(reinforcement);
            }

            // 추락으로 들어온 첫 방이면 입구 위 공중에 세운다 — 막이 걷히면 떨어져 착지한다.
            MovePlayerTo(new Vector2(-half + MAP_ENTRANCE_INSET, groundY + (isDropEntry ? DROP_ENTRY_HEIGHT : 0f)));
            if (follow != null)
            {
                follow.SetEnvironmentBounds(
                    new Vector2(-half - WALL_THICKNESS, groundY - CAMERA_BELOW_GROUND),
                    new Vector2(half + WALL_THICKNESS, groundY + CAMERA_ABOVE_GROUND));
            }
        }

        /// <summary>
        /// 맵 방의 첫 무리. placements가 있으면 그 좌표에, 없으면 enemies를 입구 여백 뒤 구간에 고르게 흩는다
        /// (옛 방을 mapLength만 넣어 옮길 수 있게). 스폰 의도 수를 돌려준다.
        /// </summary>
        private int SpawnMapOpening(RoomData room)
        {
            float half = room.mapLength * 0.5f;
            float y = GetSpawnPoint(0).position.y;
            int expected = 0;

            if (room.placements.Count > 0)
            {
                foreach (var placement in room.placements)
                {
                    if (placement == null || !IsSpawnable(placement.data, room)) continue;
                    expected += 1;
                    SpawnTracked(placement.data, new Vector3(ClampToMap(placement.x, half), y, 0f));
                }
                return expected;
            }

            var spread = new List<EnemyData>();
            foreach (var entry in room.enemies)
            {
                if (entry == null || !IsSpawnable(entry.data, room)) continue;
                for (int i = 0; i < entry.count; i++) spread.Add(entry.data);
            }

            float left = -half + MAP_SPREAD_LEFT_INSET;
            float right = half - MAP_SPREAD_RIGHT_INSET;
            for (int i = 0; i < spread.Count; i++)
            {
                float t = spread.Count == 1 ? 0.5f : i / (spread.Count - 1f);
                SpawnTracked(spread[i], new Vector3(ClampToMap(Mathf.Lerp(left, right, t), half), y, 0f));
            }
            return spread.Count;
        }

        // ───────────────────────── 추가 소환 ─────────────────────────

        /// <summary>도달 조건(ReachX) 추가 소환을 본다. 적이 감지 범위 밖에서 서 있듯, 맵 뒤쪽 무리는 다가갈 때 나온다.</summary>
        private void Update()
        {
            if (pendingReinforcements.Count == 0 || isRoomClearing) return;
            var player = ResolvePlayer();
            if (player == null) return;

            float playerX = player.transform.position.x;
            for (int i = 0; i < pendingReinforcements.Count; i++)
            {
                var reinforcement = pendingReinforcements[i];
                if (reinforcement.trigger != ReinforcementTrigger.ReachX || playerX < reinforcement.value) continue;
                pendingReinforcements.RemoveAt(i);
                SpawnReinforcement(reinforcement, $"지점 도달 x≥{reinforcement.value}");
                return;
            }
        }

        /// <summary>맵 방에서 한 마리 처치 — 누적 처치 수(KillCount) 추가 소환을 연다.</summary>
        private void RegisterMapKill()
        {
            if (currentRoom == null || !currentRoom.IsMapRoom || isRoomClearing) return;
            mapKillCount += 1;

            for (int i = 0; i < pendingReinforcements.Count; i++)
            {
                var reinforcement = pendingReinforcements[i];
                if (reinforcement.trigger != ReinforcementTrigger.KillCount || mapKillCount < reinforcement.value) continue;
                pendingReinforcements.RemoveAt(i);
                SpawnReinforcement(reinforcement, $"누적 처치 {mapKillCount}");
                i -= 1;
            }
        }

        /// <summary>남은 추가 소환 중 맨 앞을 조건과 무관하게 부른다. 부를 것이 없으면 false.</summary>
        private bool ReleaseNextReinforcement(string reason)
        {
            if (pendingReinforcements.Count == 0) return false;
            var reinforcement = pendingReinforcements[0];
            pendingReinforcements.RemoveAt(0);
            SpawnReinforcement(reinforcement, reason);
            return true;
        }

        private void SpawnReinforcement(Reinforcement reinforcement, string reason)
        {
            var room = currentRoom;
            if (room == null) return;

            float half = room.mapLength * 0.5f;
            float y = GetSpawnPoint(0).position.y;
            int before = activeEnemies.Count;
            int expected = 0;

            foreach (var placement in reinforcement.enemies)
            {
                if (placement == null || !IsSpawnable(placement.data, room)) continue;
                expected += 1;
                var position = new Vector3(ClampToMap(placement.x, half), y, 0f);
                // 소환 표식 — 어디서 나오는지 먼저 보인다(본격 예고 연출은 A2).
                BossAreaEffect.Spawn(position, SUMMON_EFFECT_RADIUS, SummonEffectColor, SUMMON_EFFECT_SECONDS, BossAreaEffect.Mode.Telegraph);
                SpawnTracked(placement.data, position);
            }

            int spawned = activeEnemies.Count - before;
            Debug.Log($"[StageDirector] {room.roomId} 추가 소환({reason}) — {spawned}/{expected}마리");

            // 전부 실패하면(데이터 오류) 남은 적이 안 생겨 방이 멈춘다 — 다음 판정을 바로 돌린다.
            if (spawned == 0) CheckRoomProgress();
        }

        // ───────────────────────── 보상 문 ─────────────────────────

        /// <summary>
        /// 맵 방을 마쳤을 때 다음 단계의 방마다 문을 하나씩 세운다. 세웠으면 true — 호출자는 자동 진행을 하지 않는다.
        /// 스테이지 마지막 방이면 「다음 스테이지」 문 하나를 세운다. 시퀀스의 마지막 방(완주)은 세우지 않는다 —
        /// 완주는 예전 경로(ProceedToNextRoom → EndRun)가 곧바로 맡는다.
        /// </summary>
        private bool TryOpenExitDoors()
        {
            var room = currentRoom;
            var stage = CurrentStage;
            int nextStep = currentStepIndex + 1;
            if (room == null || !room.IsMapRoom || stage == null) return false;

            float half = room.mapLength * 0.5f;
            if (nextStep >= stage.steps.Count) return TryOpenStageDoor(half);

            var options = new List<RoomData>();
            var step = stage.steps[nextStep];
            if (step?.options != null)
            {
                foreach (var option in step.options)
                {
                    if (option != null) options.Add(option);
                }
            }
            if (options.Count == 0) return false;

            for (int i = 0; i < options.Count; i++)
            {
                // 오른쪽 끝부터 왼쪽으로 늘어선다 — 첫 선택지가 가장 안쪽(출구 쪽)이다.
                float x = half - DOOR_RIGHT_INSET - i * DOOR_SPACING;
                var door = RoomExitDoor.Create(ResolveMapRoot(), new Vector3(x, ProbeGroundY(x), 0f), options[i], HandleDoorChosen);
                exitDoors.Add(door);
            }

            Debug.Log($"[StageDirector] {room.roomId} 보상 문 {options.Count}개 — 선택 대기");
            return true;
        }

        /// <summary>스테이지 마지막 방 — 다음 스테이지 문 하나. 다음 스테이지가 없으면(완주) 세우지 않는다.</summary>
        private bool TryOpenStageDoor(float half)
        {
            int nextStage = currentStageIndex + 1;
            if (!IsValidStage(nextStage)) return false;

            var stage = sequence.stages[nextStage];
            string title = stage != null && !string.IsNullOrEmpty(stage.displayName) ? stage.displayName : $"Stage {nextStage + 1}";
            float x = half - DOOR_RIGHT_INSET;
            var door = RoomExitDoor.Create(ResolveMapRoot(), new Vector3(x, ProbeGroundY(x), 0f), "ExitDoor_NextStage",
                "▼  더 깊이", title, StageDoorColor, HandleStageDoorChosen);
            exitDoors.Add(door);

            Debug.Log($"[StageDirector] 스테이지 마지막 방 — 다음 스테이지 문({title}) 대기");
            return true;
        }

        private void HandleDoorChosen(RoomData chosen)
        {
            if (isDoorTransitioning || chosen == null) return;
            _ = EnterThroughDoorAsync($"보상 문 선택: {chosen.roomId}", () =>
            {
                isRoomClearing = false;
                currentStepIndex += 1;
                EnterRoom(chosen);
            });
        }

        private void HandleStageDoorChosen()
        {
            if (isDoorTransitioning) return;
            _ = FallToNextStageAsync();
        }

        /// <summary>
        /// 다음 스테이지로 떨어진다 — 검게 덮고 → 추락 연출(<see cref="UI.StageFallPanel"/>, 정지) → 다시 덮고 →
        /// 검은 막 뒤에서 스테이지 진입(입구 위 공중에 세움) → 걷으면 플레이어가 떨어져 착지한다.
        /// 예전 경로(ProceedToNextRoom)의 스테이지 사이 3초 대기는 이 연출이 대신한다.
        /// </summary>
        private async Awaitable FallToNextStageAsync()
        {
            isDoorTransitioning = true;
            int version = roomEntryVersion;
            int nextStage = currentStageIndex + 1;
            var stage = IsValidStage(nextStage) ? sequence.stages[nextStage] : null;
            string title = stage != null && !string.IsNullOrEmpty(stage.displayName) ? stage.displayName : string.Empty;
            var fader = ResolveFader();

            try
            {
                if (fader != null) await fader.FadeOutAsync();

                bool isCurrent = this != null && version == roomEntryVersion;
                if (isCurrent)
                {
                    // 막 뒤에서 연출을 열고(정지) 막을 걷는다 — 떨어지는 장면이 드러난다.
                    var player = ResolvePlayer();
                    var fall = UI.StageFallPanel.PlayAsync(nextStage + 1, title, CapturePlayerSprite(player),
                        player != null && player.FacingSign < 0);
                    if (fader != null) await fader.FadeInAsync();
                    await fall;
                    if (fader != null) await fader.FadeOutAsync();
                    UI.StageFallPanel.Close();

                    if (this != null && version == roomEntryVersion)
                    {
                        Debug.Log($"[StageDirector] 다음 스테이지로 추락: Stage {nextStage + 1}");
                        isRoomClearing = false;
                        if (CurrentStage != null) GameEvents.RaiseStageCleared(CurrentStage);
                        currentStageIndex += 1;
                        isDropEntry = true;
                        EnterStage(currentStageIndex);
                        isDropEntry = false;
                    }
                }

                if (fader != null) await fader.FadeInAsync();
            }
            catch (OperationCanceledException)
            {
                // 페이더·패널 파괴(씬 전환·종료) — 할 일 없다.
            }
            finally
            {
                isDoorTransitioning = false;
            }
        }

        /// <summary>추락 연출에 쓸 지금의 플레이어 그림(폼마다 다르다). Visual 자식의 SpriteRenderer.</summary>
        private static Sprite CapturePlayerSprite(PlayerCharacter player)
        {
            if (player == null) return null;
            var renderer = player.GetComponentInChildren<SpriteRenderer>();
            return renderer != null ? renderer.sprite : null;
        }

        private static ScreenFader ResolveFader()
        {
            var flow = SceneFlowController.HasInstance ? SceneFlowController.Instance : null;
            return flow != null && !flow.IsLoading ? flow.SharedFader : null;
        }

        /// <summary>문으로 다음 곳에 들어간다 — 검게 덮고, <paramref name="enter"/>로 진입하고, 걷어낸다.</summary>
        private async Awaitable EnterThroughDoorAsync(string log, Action enter)
        {
            isDoorTransitioning = true;
            int version = roomEntryVersion;
            var fader = ResolveFader();

            try
            {
                if (fader != null) await fader.FadeOutAsync();

                // 페이드 사이 치트 이동·씬 전환이 있었으면 이 문은 이미 낡았다. 그래도 막은 걷어야 한다.
                if (this != null && version == roomEntryVersion)
                {
                    Debug.Log($"[StageDirector] {log}");
                    enter();
                }

                if (fader != null) await fader.FadeInAsync();
            }
            catch (OperationCanceledException)
            {
                // 페이더 파괴(앱 종료 등) — 할 일 없다.
            }
            finally
            {
                isDoorTransitioning = false;
            }
        }

        // ───────────────────────── 보조 ─────────────────────────

        /// <summary>
        /// 맵 방의 보상 제단(폼·무기)을 맵 가운데로 옮긴다 — 스컬처럼 보상은 맵 한가운데 나타나고, 문은 오른쪽 끝에 선다.
        /// 제단은 Run 씬에 고정 좌표로 놓여 있어(무기 x 19.48 · 폼 13.21) 짧은 맵에서는 <b>벽 밖</b>에 섰다.
        /// 닿을 수 없는 제단은 보상 게이트를 영영 안 풀어 문이 서지 않는다(2026-09-26 사용자 보고 — Stage1 보스 방).
        /// 높이는 씬 값 그대로 둔다(제단마다 피벗이 다르다). 옛 방은 건드리지 않는다.
        /// </summary>
        private void PlaceRewardAltar(Transform altar)
        {
            if (altar == null || currentRoom == null || !currentRoom.IsMapRoom) return;
            var position = altar.position;
            altar.position = new Vector3(0f, position.y, position.z);
        }

        private void ClearMapObjects()
        {
            pendingReinforcements.Clear();
            exitDoors.Clear();
            mapKillCount = 0;
            if (mapRoot == null) return;
            for (int i = mapRoot.childCount - 1; i >= 0; i--) Destroy(mapRoot.GetChild(i).gameObject);
        }

        private Transform ResolveMapRoot()
        {
            if (mapRoot == null) mapRoot = new GameObject("RoomMap").transform;
            return mapRoot;
        }

        /// <summary>맵 끝 벽. 적도 막는다(지형과 같은 Ground 층). 마찰 0 — 벽에 붙어 매달리지 않게.</summary>
        private void CreateWall(float x, float groundY)
        {
            var go = new GameObject("MapWall");
            go.transform.SetParent(ResolveMapRoot(), false);
            go.transform.position = new Vector3(x, groundY + WALL_HEIGHT * 0.5f - 1f, 0f);
            int layer = LayerMask.NameToLayer("Ground");
            if (layer >= 0) go.layer = layer;

            var box = go.AddComponent<BoxCollider2D>();
            box.size = new Vector2(WALL_THICKNESS, WALL_HEIGHT);
            if (wallMaterial == null) wallMaterial = new PhysicsMaterial2D("MapWall") { friction = 0f, bounciness = 0f };
            box.sharedMaterial = wallMaterial;
        }

        /// <summary>
        /// x 위치의 바닥 윗면 높이. 위에서 아래로 쏘아 맞은 것 중 <b>가장 낮은</b> 면을 쓴다 —
        /// 방 레이아웃의 공중 발판이 먼저 맞아 문·플레이어가 발판 위에 서지 않게.
        /// </summary>
        private float ProbeGroundY(float x) => ProbeGround(x, out _);

        /// <summary><see cref="ProbeGroundY"/>와 같고, 맞은 바닥 충돌체도 돌려준다(없으면 null).</summary>
        private float ProbeGround(float x, out Collider2D ground)
        {
            int mask = LayerMask.GetMask("Ground");
            var hits = Physics2D.RaycastAll(new Vector2(x, GROUND_PROBE_TOP), Vector2.down, GROUND_PROBE_TOP * 2f, mask);
            float lowest = float.MaxValue;
            ground = null;
            foreach (var hit in hits)
            {
                if (hit.collider == null || hit.collider.isTrigger || mapRoot != null && hit.collider.transform.IsChildOf(mapRoot)) continue;
                if (hit.point.y >= lowest) continue;
                lowest = hit.point.y;
                ground = hit.collider;
            }
            return lowest < float.MaxValue ? lowest : GetSpawnPoint(0).position.y;
        }

        private static float ClampToMap(float x, float half) => Mathf.Clamp(x, -half + 0.5f, half - 0.5f);

        /// <summary>플레이어를 옮긴다(발밑 원점). 물리 위치·속도까지 맞춰야 다음 물리 틱에 되돌아가지 않는다.</summary>
        private void MovePlayerTo(Vector2 feet)
        {
            var player = ResolvePlayer();
            if (player == null) return;

            player.transform.position = new Vector3(feet.x, feet.y, player.transform.position.z);
            if (player.TryGetComponent<Rigidbody2D>(out var body))
            {
                body.position = feet;
                body.linearVelocity = Vector2.zero;
            }
        }

        private PlayerCharacter ResolvePlayer()
        {
            if (cachedPlayer == null) cachedPlayer = FindAnyObjectByType<PlayerCharacter>();
            return cachedPlayer;
        }

        private PlayerCameraFollow ResolveCamera()
        {
            if (cachedCamera == null) cachedCamera = FindAnyObjectByType<PlayerCameraFollow>();
            return cachedCamera;
        }
    }
}
