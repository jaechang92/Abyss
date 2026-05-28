using System.Collections.Generic;
using Abyss.Runtime.Enemy;
using Abyss.Runtime.Events;
using Abyss.Runtime.Run;
using UnityEngine;

namespace Abyss.Runtime.Stage
{
    /// <summary>
    /// 스테이지 순차 진행 제어. 방 내 적이 모두 사망하면 자동으로 다음 방으로 진행.
    /// Critic S3 반영: 노드 분기 없이 선형 5~7방 하드코딩.
    /// </summary>
    public sealed class StageDirector : MonoBehaviour
    {
        [SerializeField] private StageData stage;
        [SerializeField] private Transform[] spawnPoints;
        [SerializeField] private bool startOnEnable = true;
        [SerializeField, Min(0f)] private float delayBetweenRooms = 2f;

        private int currentRoomIndex = -1;
        private readonly List<EnemyBase> activeEnemies = new();

        public StageData Stage => stage;
        public RoomData CurrentRoom => IsValidRoom(currentRoomIndex) ? stage.rooms[currentRoomIndex] : null;
        public int CurrentRoomIndex => currentRoomIndex;
        public int RemainingRooms => stage != null ? Mathf.Max(0, stage.rooms.Count - 1 - currentRoomIndex) : 0;

        private void OnEnable()
        {
            GameEvents.OnEnemyKilled += HandleEnemyKilled;
            if (startOnEnable) Invoke(nameof(StartStage), 0.2f);
        }

        private void OnDisable()
        {
            GameEvents.OnEnemyKilled -= HandleEnemyKilled;
            CancelInvoke();
        }

        public void StartStage()
        {
            if (stage == null || stage.rooms.Count == 0)
            {
                Debug.LogWarning("[StageDirector] Stage 또는 rooms 미설정");
                return;
            }

            currentRoomIndex = 0;
            RunManager.Instance?.StartNewRun();
            EnterRoom(currentRoomIndex);
        }

        public void ProceedToNextRoom()
        {
            currentRoomIndex += 1;

            if (currentRoomIndex >= stage.rooms.Count)
            {
                GameEvents.RaiseStageCleared(stage);
                RunManager.Instance?.EndRun();
                return;
            }

            EnterRoom(currentRoomIndex);
        }

        private void EnterRoom(int index)
        {
            if (!IsValidRoom(index)) return;

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

            if (activeEnemies.Count == 0 && CurrentRoom != null)
            {
                HandleRoomCleared(CurrentRoom);
            }
        }

        private void HandleRoomCleared(RoomData room)
        {
            Debug.Log($"[StageDirector] Room 클리어: {room.roomId}");
            GameEvents.RaiseRoomCleared(room);

            if (room.clearGoldReward > 0)
            {
                RunManager.Instance?.GainGoldShards(room.clearGoldReward);
            }

            Invoke(nameof(ProceedToNextRoom), delayBetweenRooms);
        }

        private bool IsValidRoom(int index)
        {
            return stage != null && index >= 0 && index < stage.rooms.Count;
        }
    }
}
