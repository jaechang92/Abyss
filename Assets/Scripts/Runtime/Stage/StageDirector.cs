using System.Collections.Generic;
using Abyss.Runtime.Enemy;
using Abyss.Runtime.Events;
using Abyss.Runtime.Run;
using UnityEngine;

namespace Abyss.Runtime.Stage
{
    /// <summary>
    /// 멀티 스테이지 순차 진행 제어. 방 내 적이 모두 사망하면 자동으로 다음 방으로 진행하고,
    /// 한 스테이지의 마지막 방을 클리어하면 다음 스테이지로 진입한다(시퀀스 = StageSequenceData).
    /// 시퀀스의 마지막 스테이지까지 클리어하면 런을 종료한다.
    /// Critic S3 반영: 노드 분기 없이 스테이지별 선형 5~7방 하드코딩.
    /// </summary>
    public sealed class StageDirector : MonoBehaviour
    {
        [SerializeField] private StageSequenceData sequence;
        [SerializeField] private Transform[] spawnPoints;
        [SerializeField] private bool startOnEnable = true;
        [SerializeField, Min(0f)] private float delayBetweenRooms = 2f;
        [SerializeField, Min(0f)] private float delayBetweenStages = 3f;

        private int currentStageIndex = -1;
        private int currentRoomIndex = -1;
        private readonly List<EnemyBase> activeEnemies = new();
        private bool isRoomClearing;  // ProceedToNextRoom 지연 창 동안 룸 이중 클리어(보상 중복·방 스킵) 방지

        public StageSequenceData Sequence => sequence;
        public StageData CurrentStage => IsValidStage(currentStageIndex) ? sequence.stages[currentStageIndex] : null;
        public RoomData CurrentRoom => IsValidRoom(currentRoomIndex) ? CurrentStage.rooms[currentRoomIndex] : null;
        public int CurrentStageIndex => currentStageIndex;
        public int CurrentRoomIndex => currentRoomIndex;
        public int RemainingRooms => CurrentStage != null ? Mathf.Max(0, CurrentStage.rooms.Count - 1 - currentRoomIndex) : 0;
        public int RemainingStages => sequence != null ? Mathf.Max(0, sequence.stages.Count - 1 - currentStageIndex) : 0;

        private void OnEnable()
        {
            GameEvents.OnEnemyKilled += HandleEnemyKilled;
            if (startOnEnable) Invoke(nameof(StartSequence), 0.2f);
        }

        private void OnDisable()
        {
            GameEvents.OnEnemyKilled -= HandleEnemyKilled;
            CancelInvoke();
        }

        public void StartSequence()
        {
            if (sequence == null || sequence.stages.Count == 0)
            {
                Debug.LogWarning("[StageDirector] Sequence 또는 stages 미설정");
                return;
            }

            RunManager.Instance?.StartNewRun();
            currentStageIndex = 0;
            EnterStage(currentStageIndex);
        }

        private void EnterStage(int index)
        {
            if (!IsValidStage(index)) return;

            var stage = sequence.stages[index];
            if (stage == null || stage.rooms.Count == 0)
            {
                Debug.LogWarning($"[StageDirector] Stage {index + 1} 또는 rooms 미설정 — 건너뜀");
                ProceedToNextStage();
                return;
            }

            Debug.Log($"[StageDirector] Stage {index + 1}/{sequence.stages.Count} 진입: {stage.displayName} ({stage.rooms.Count}방)");
            currentRoomIndex = 0;
            EnterRoom(currentRoomIndex);
        }

        public void ProceedToNextRoom()
        {
            isRoomClearing = false;  // 다음 방으로 넘어가며 클리어 상태 해제
            currentRoomIndex += 1;

            if (CurrentStage == null || currentRoomIndex >= CurrentStage.rooms.Count)
            {
                // 현재 스테이지의 모든 방 클리어 → 스테이지 클리어 처리 후 다음 스테이지로.
                if (CurrentStage != null) GameEvents.RaiseStageCleared(CurrentStage);
                ProceedToNextStage();
                return;
            }

            EnterRoom(currentRoomIndex);
        }

        private void ProceedToNextStage()
        {
            currentStageIndex += 1;

            if (currentStageIndex >= sequence.stages.Count)
            {
                // 시퀀스의 모든 스테이지 클리어 → 런 종료.
                Debug.Log("[StageDirector] 모든 스테이지 클리어 — 런 종료");
                RunManager.Instance?.EndRun();
                return;
            }

            Debug.Log($"[StageDirector] 다음 스테이지로 진행 ({delayBetweenStages}s 후)");
            Invoke(nameof(EnterCurrentStage), delayBetweenStages);
        }

        // Invoke 대상용 무인자 래퍼 (currentStageIndex는 ProceedToNextStage에서 이미 증가).
        private void EnterCurrentStage() => EnterStage(currentStageIndex);

        private void EnterRoom(int index)
        {
            var stage = CurrentStage;
            if (stage == null || !IsValidRoom(index)) return;

            var room = stage.rooms[index];
            Debug.Log($"[StageDirector] Room {index + 1}/{stage.rooms.Count} 진입: {room.roomId} ({room.roomType})");
            GameEvents.RaiseRoomEntered(room);
            SpawnEnemies(room);
        }

        private void SpawnEnemies(RoomData room)
        {
            activeEnemies.Clear();
            int spawnIndex = 0;

            foreach (var entry in room.enemies)
            {
                if (entry.data == null || entry.data.spawnPrefab == null)
                {
                    Debug.LogWarning($"[StageDirector] 적 스폰 누락: entry={entry?.data?.enemyId ?? "null"}");
                    continue;
                }

                for (int i = 0; i < entry.count; i++)
                {
                    Transform spawn = GetSpawnPoint(spawnIndex);
                    spawnIndex += 1;

                    var go = Instantiate(entry.data.spawnPrefab, spawn.position, Quaternion.identity);
                    var enemy = go.GetComponent<EnemyBase>();
                    if (enemy != null) activeEnemies.Add(enemy);
                }
            }

            if (activeEnemies.Count == 0)
            {
                Debug.Log($"[StageDirector] 빈 방 감지 → 즉시 클리어: {room.roomId}");
                HandleRoomCleared(room);
            }
        }

        private Transform GetSpawnPoint(int index)
        {
            if (spawnPoints != null && spawnPoints.Length > 0)
            {
                return spawnPoints[index % spawnPoints.Length];
            }
            return transform;
        }

        private void HandleEnemyKilled(EnemyData _)
        {
            // EnemyBase.Die는 Destroy(gameObject, 0.3f) 지연 파괴라 이 시점에는 e != null.
            // IsDead 플래그로 즉시 제거해야 마지막 적 사망 시 룸이 즉시 클리어됨.
            activeEnemies.RemoveAll(e => e == null || e.IsDead);

            if (!isRoomClearing && activeEnemies.Count == 0 && CurrentRoom != null)
            {
                HandleRoomCleared(CurrentRoom);
            }
        }

        private void HandleRoomCleared(RoomData room)
        {
            // 지연 창 중 재진입 방지 — 골드 이중 지급·ProceedToNextRoom 이중 Invoke(방 스킵) 차단.
            if (isRoomClearing) return;
            isRoomClearing = true;

            Debug.Log($"[StageDirector] Room 클리어: {room.roomId}");
            GameEvents.RaiseRoomCleared(room);

            if (room.clearGoldReward > 0)
            {
                RunManager.Instance?.GainGoldShards(room.clearGoldReward);
            }

            Invoke(nameof(ProceedToNextRoom), delayBetweenRooms);
        }

        private bool IsValidStage(int index)
        {
            return sequence != null && index >= 0 && index < sequence.stages.Count;
        }

        private bool IsValidRoom(int index)
        {
            var stage = CurrentStage;
            return stage != null && index >= 0 && index < stage.rooms.Count;
        }
    }
}
