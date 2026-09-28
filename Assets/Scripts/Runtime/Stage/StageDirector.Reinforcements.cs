using System.Collections.Generic;
using Abyss.Runtime.Audio;
using Abyss.Runtime.Enemy;
using Abyss.Runtime.Feedback;
using UnityEngine;

namespace Abyss.Runtime.Stage
{
    /// <summary>
    /// 맵 방의 조건부 추가 소환 — 조건 충족 → <b>바닥 표식 예고(게임 시간 0.6초)</b> → 등장(A2).
    /// 예전에는 표식과 적이 같은 프레임에 나와 예고가 아니었다. 최초 배치 무리(<see cref="SpawnMapOpening"/>)는 지연하지 않는다.
    ///
    /// 🔴 <b>예고 중인 무리도 미완료 전투다.</b> 남은 적이 0이어도 예고가 남아 있으면 클리어·보상·문을 열지 않는다
    /// (<see cref="CheckRoomProgress"/>). 한 무리는 대기(<see cref="pendingReinforcements"/>) → 예고(<see cref="telegraphs"/>)
    /// → 등장으로 한 방향으로만 옮겨 가므로 같은 무리가 두 번 발동하지 않는다.
    ///
    /// 타이머는 비동기 작업이 아니라 이 컴포넌트의 Update에서 <c>Time.deltaTime</c>으로 잰다 —
    /// 정지·모달(timeScale 0) 동안 저절로 멈추고, 방을 떠나면 목록을 비우는 것만으로 취소된다.
    /// 이전 작업의 finally가 새 방의 카운트를 건드릴 경로 자체가 없다. 방 세대(<see cref="roomEntryVersion"/>)는 이중 확인이다.
    ///
    /// 🔴 <b>방 전투 세션</b>(<see cref="isRoomSessionClosed"/>): 런 종료(사망·완주)·런 포기·디렉터 비활성화·치트 이동이
    /// 세션을 닫는다 — 예고와 대기 무리를 즉시 버리고, 닫힌 동안은 같은 방에서 새 예고·진행 판정·문 생성이 없다.
    /// 다시 여는 곳은 새 방 진입(<see cref="EnterRoom"/>) 하나뿐이다. 컴포넌트가 꺼졌다 켜져도 세션은 열리지 않고,
    /// 버린 무리는 목록에서 이미 지워졌으므로 뒤늦게 방출될 수 없다.
    /// </summary>
    public sealed partial class StageDirector
    {
        private const float REINFORCEMENT_TELEGRAPH_SECONDS = 0.6f;
        private const float SUMMON_TELEGRAPH_VOLUME = 0.7f;

        [Tooltip("추가 소환 예고음(무리당 1회). 기존 boss_telegraph.wav 재사용. 비우면 표식만 나온다.")]
        [SerializeField] private AudioClip summonTelegraphSfx;

        private readonly List<ReinforcementTelegraph> telegraphs = new();
        private readonly List<ReinforcementTelegraph> finishedTelegraphs = new();
        private readonly List<Reinforcement> triggeredReinforcements = new();

        // 방 전투 세션이 닫혔는가. 닫히면 새 방 진입 전까지 예고·진행 판정을 하지 않는다(위 요약 참조).
        private bool isRoomSessionClosed;

        /// <summary>예고 중인 한 무리. 등장할 적과 자리를 예고 시작 때 정해 둔다.</summary>
        private sealed class ReinforcementTelegraph
        {
            public readonly List<EnemyData> Enemies = new();
            public readonly List<Vector3> Positions = new();
            public readonly List<GameObject> Markers = new();
            public string Reason;
            public float Remaining;
            public int RoomVersion;
        }

        // ───────────────────────── 매 프레임 ─────────────────────────

        /// <summary>예고 타이머를 진행하고, 도달 조건(ReachX) 추가 소환을 본다.</summary>
        private void Update()
        {
            if (isRoomSessionClosed) return;
            TickReinforcementTelegraphs();
            CheckReachReinforcements();
        }

        /// <summary>
        /// 예고 타이머. <c>Time.deltaTime</c>은 정지·모달(timeScale 0) 중 0이라 예고가 멈췄다가,
        /// 재개 후 남은 시간을 마치고 등장한다. 시간이 다 된 무리를 먼저 목록에서 빼고 소환한다 —
        /// 소환 실패 뒤 진행 재판정이 새 예고를 넣어도 순회 중인 목록이 흔들리지 않게.
        /// </summary>
        private void TickReinforcementTelegraphs()
        {
            if (telegraphs.Count == 0) return;

            float delta = Time.deltaTime;
            finishedTelegraphs.Clear();
            for (int i = 0; i < telegraphs.Count; i++)
            {
                var telegraph = telegraphs[i];
                // 방을 떠나면 ClearMapObjects가 이미 비운다. 남아 있으면 낡은 것 — 소환하지 않고 버린다.
                if (telegraph.RoomVersion != roomEntryVersion)
                {
                    DestroyMarkers(telegraph);
                    telegraphs.RemoveAt(i);
                    i -= 1;
                    continue;
                }

                telegraph.Remaining -= delta;
                if (telegraph.Remaining > 0f) continue;
                telegraphs.RemoveAt(i);
                i -= 1;
                finishedTelegraphs.Add(telegraph);
            }

            if (finishedTelegraphs.Count == 0) return;

            bool isAnyFailed = false;
            for (int i = 0; i < finishedTelegraphs.Count; i++)
            {
                if (!SpawnTelegraphed(finishedTelegraphs[i])) isAnyFailed = true;
            }
            finishedTelegraphs.Clear();

            // 전부 실패한 무리가 있으면(데이터 오류) 남은 적이 안 생겨 방이 멈춘다 — 진행을 다시 판정한다.
            if (isAnyFailed) CheckRoomProgress();
        }

        /// <summary>도달 조건(ReachX) 추가 소환. 적이 감지 범위 밖에서 서 있듯, 맵 뒤쪽 무리는 다가갈 때 나온다.</summary>
        private void CheckReachReinforcements()
        {
            if (pendingReinforcements.Count == 0 || isRoomClearing) return;
            var player = ResolvePlayer();
            if (player == null) return;

            float playerX = player.transform.position.x;
            CollectTriggered(ReinforcementTrigger.ReachX, playerX);
            if (triggeredReinforcements.Count == 0) return;

            if (!BeginTriggered(value => $"지점 도달 x≥{value}")) CheckRoomProgress();
        }

        /// <summary>맵 방에서 한 마리 처치 — 누적 처치 수(KillCount) 추가 소환을 연다. 진행 판정은 호출자가 이어서 한다.</summary>
        private void RegisterMapKill()
        {
            if (isRoomSessionClosed || currentRoom == null || !currentRoom.IsMapRoom || isRoomClearing) return;
            mapKillCount += 1;

            CollectTriggered(ReinforcementTrigger.KillCount, mapKillCount);
            if (triggeredReinforcements.Count == 0) return;

            int kills = mapKillCount;
            BeginTriggered(_ => $"누적 처치 {kills}");
        }

        /// <summary>
        /// 남은 추가 소환 중 맨 앞을 조건과 무관하게 예고한다(도달 지점을 건너뛰어 방이 막히지 않게).
        /// 스폰할 적이 하나도 없는 무리는 건너뛰고 다음 것을 본다. 예고를 시작했으면 true.
        /// </summary>
        private bool ReleaseNextReinforcement(string reason)
        {
            while (pendingReinforcements.Count > 0)
            {
                var reinforcement = pendingReinforcements[0];
                pendingReinforcements.RemoveAt(0);
                if (BeginReinforcementTelegraph(reinforcement, reason)) return true;
            }
            return false;
        }

        /// <summary>
        /// 조건을 만족한 무리를 대기 목록에서 <b>먼저 전부 뺀다</b>(데이터 순서 유지). 동시에 만족한 무리도
        /// 이 한 번에 모두 처리되고, 뺀 뒤에만 예고하므로 같은 무리가 다시 걸리지 않는다.
        /// </summary>
        private void CollectTriggered(ReinforcementTrigger trigger, float progress)
        {
            triggeredReinforcements.Clear();
            for (int i = 0; i < pendingReinforcements.Count; i++)
            {
                var reinforcement = pendingReinforcements[i];
                if (reinforcement.trigger != trigger || progress < reinforcement.value) continue;
                triggeredReinforcements.Add(reinforcement);
                pendingReinforcements.RemoveAt(i);
                i -= 1;
            }
        }

        /// <summary>모아 둔 무리를 차례로 예고한다. 하나라도 예고를 시작했으면 true.</summary>
        private bool BeginTriggered(System.Func<float, string> reason)
        {
            bool isAnyStarted = false;
            for (int i = 0; i < triggeredReinforcements.Count; i++)
            {
                var reinforcement = triggeredReinforcements[i];
                if (BeginReinforcementTelegraph(reinforcement, reason(reinforcement.value))) isAnyStarted = true;
            }
            triggeredReinforcements.Clear();
            return isAnyStarted;
        }

        // ───────────────────────── 예고 · 등장 ─────────────────────────

        /// <summary>
        /// 한 무리의 예고를 시작한다 — 등장 자리마다 바닥 표식, 예고음은 무리당 1회.
        /// 스폰할 적이 하나도 없으면(데이터 오류) 예고 없이 false — 호출자가 진행을 다시 판정한다.
        /// </summary>
        private bool BeginReinforcementTelegraph(Reinforcement reinforcement, string reason)
        {
            var room = currentRoom;
            if (isRoomSessionClosed || room == null || reinforcement == null) return false;

            float half = room.mapLength * 0.5f;
            float y = GetSpawnPoint(0).position.y;
            var telegraph = new ReinforcementTelegraph
            {
                Reason = reason,
                Remaining = REINFORCEMENT_TELEGRAPH_SECONDS,
                RoomVersion = roomEntryVersion
            };

            foreach (var placement in reinforcement.enemies)
            {
                if (placement == null || !IsSpawnable(placement.data, room)) continue;
                float x = ClampToMap(placement.x, half);
                telegraph.Enemies.Add(placement.data);
                telegraph.Positions.Add(new Vector3(x, y, 0f));

                // 바닥 표식 — 등장 자리를 먼저 보인다. 맵 루트 아래에 둬 방을 치울 때 함께 치워지게 한다.
                var marker = BossAreaEffect.Spawn(new Vector3(x, ProbeGroundY(x), 0f), SUMMON_EFFECT_RADIUS, SummonEffectColor,
                    REINFORCEMENT_TELEGRAPH_SECONDS, BossAreaEffect.Mode.Telegraph);
                if (marker == null) continue;
                marker.transform.SetParent(ResolveMapRoot(), true);
                telegraph.Markers.Add(marker.gameObject);
            }

            if (telegraph.Enemies.Count == 0)
            {
                Debug.LogWarning($"[StageDirector] {room.roomId} 추가 소환({reason}) — 스폰할 적이 없어 예고 없이 건너뜀");
                return false;
            }

            telegraphs.Add(telegraph);
            if (summonTelegraphSfx != null && AudioManager.HasInstance)
            {
                AudioManager.Instance.PlaySfx(summonTelegraphSfx, SUMMON_TELEGRAPH_VOLUME);
            }

            Debug.Log($"[StageDirector] {room.roomId} 추가 소환 예고({reason}) — {telegraph.Enemies.Count}마리 " +
                      $"{REINFORCEMENT_TELEGRAPH_SECONDS}s · 예고 {telegraphs.Count} · 대기 {pendingReinforcements.Count}");
            return true;
        }

        /// <summary>예고가 끝난 무리를 등장시킨다. 한 마리라도 추적됐으면 true.</summary>
        private bool SpawnTelegraphed(ReinforcementTelegraph telegraph)
        {
            DestroyMarkers(telegraph);

            int before = activeEnemies.Count;
            for (int i = 0; i < telegraph.Enemies.Count; i++)
            {
                SpawnTracked(telegraph.Enemies[i], telegraph.Positions[i]);
            }

            int spawned = activeEnemies.Count - before;
            string roomId = currentRoom != null ? currentRoom.roomId : "null";
            Debug.Log($"[StageDirector] {roomId} 추가 소환 등장({telegraph.Reason}) — {spawned}/{telegraph.Enemies.Count}마리 " +
                      $"· 예고 {telegraphs.Count} · 대기 {pendingReinforcements.Count}");
            return spawned > 0;
        }

        // ───────────────────────── 세션 · 취소 ─────────────────────────

        /// <summary>새 방 진입(<see cref="EnterRoom"/>)에서만 부른다 — 세션을 열고 스폰 실패 표시를 새 방 기준으로 지운다.</summary>
        private void OpenRoomSession()
        {
            isRoomSessionClosed = false;
            hasRoomSpawnFailure = false;
        }

        /// <summary>
        /// 방 전투 세션을 닫는다 — 예고·대기 무리를 즉시 버리고 CLEAR 표시를 숨긴다.
        /// 방 세대도 올려 페이드 중인 문 전환(<see cref="EnterThroughDoorAsync"/>·<see cref="FallToNextStageAsync"/>)이
        /// 닫힌 뒤에 방을 들이지 않게 한다. 다시 여는 것은 <see cref="OpenRoomSession"/>뿐이다.
        /// </summary>
        private void CloseRoomSession(string reason)
        {
            isRoomSessionClosed = true;
            roomEntryVersion += 1;
            nodePickVersion += 1;  // 열려 있던 갈림길 선택도 무효 — 치트 이동은 이 뒤 EnterStep이 새 세대로 다시 연다
            CancelReinforcementTelegraphs(reason);
            if (pendingReinforcements.Count > 0)
            {
                Debug.Log($"[StageDirector] 대기 중인 추가 소환 {pendingReinforcements.Count}건 폐기({reason})");
                pendingReinforcements.Clear();
            }
            UI.RoomClearBanner.Hide();
        }

        /// <summary>
        /// 런이 끝났다(사망·완주·포기). 예약된 자동 진행(Invoke)도 함께 끊는다 — 남아 있으면 결과 화면 뒤에서
        /// 다음 방에 들어가 세션을 다시 연다. 사망은 <c>OnPlayerDead</c> → RunManager.EndRun → <c>OnRunEnded</c>가
        /// 같은 호출 안에서 이어지지만, 런이 활성이 아니어서 EndRun이 무시되는 경우를 위해 사망도 직접 듣는다.
        /// </summary>
        private void EndRoomSessionForRun(string reason)
        {
            // 사망이면 OnPlayerDead·OnRunEnded 두 번 온다. 둘 다 멱등이라 가드하지 않는다(치트 이동으로 이미 닫혔어도 Invoke는 끊는다).
            CancelInvoke();
            CloseRoomSession(reason);
        }

        private void HandleRunEnded() => EndRoomSessionForRun("런 종료");
        private void HandleRunAbandoned() => EndRoomSessionForRun("런 포기");
        private void HandlePlayerDead() => EndRoomSessionForRun("플레이어 사망");

        /// <summary>
        /// 남은 예고를 모두 버리고 표식을 치운다 — 방 이동·재입장(<see cref="ClearMapObjects"/>)과
        /// 세션 닫기(<see cref="CloseRoomSession"/>)가 부른다. 버린 무리는 소환하지 않는다.
        /// </summary>
        private void CancelReinforcementTelegraphs(string reason)
        {
            finishedTelegraphs.Clear();
            triggeredReinforcements.Clear();
            if (telegraphs.Count == 0) return;

            for (int i = 0; i < telegraphs.Count; i++) DestroyMarkers(telegraphs[i]);
            Debug.Log($"[StageDirector] 추가 소환 예고 {telegraphs.Count}건 취소({reason})");
            telegraphs.Clear();
        }

        private static void DestroyMarkers(ReinforcementTelegraph telegraph)
        {
            for (int i = 0; i < telegraph.Markers.Count; i++)
            {
                if (telegraph.Markers[i] != null) Destroy(telegraph.Markers[i]);
            }
            telegraph.Markers.Clear();
        }
    }
}
