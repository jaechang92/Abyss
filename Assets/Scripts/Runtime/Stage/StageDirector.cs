using System.Collections.Generic;
using Abyss.Runtime.Enemy;
using Abyss.Runtime.Events;
using Abyss.Runtime.Form;
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

        [Tooltip("폼 보상 룸에서 활성화할 제단(FormAltarBuilder가 배선). 비우면 보상 룸이 있어도 게이트 없이 진행.")]
        [SerializeField] private FormAltar formAltar;

        // RunManager 등 다른 시스템의 Awake/OnEnable이 먼저 자리잡도록 시퀀스 시작을 한 틱 지연한다.
        private const float SEQUENCE_START_DELAY = 0.2f;

        private int currentStageIndex = -1;
        private int currentRoomIndex = -1;
        private readonly List<EnemyBase> activeEnemies = new();
        private bool isRoomClearing;  // ProceedToNextRoom 지연 창 동안 룸 이중 클리어(보상 중복·방 스킵) 방지
        private bool awaitingFormReward;  // 보상 룸 클리어 후 제단 상호작용을 기다리는 동안 자동 진행 보류

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
            GameEvents.OnFormRewardResolved += HandleFormRewardResolved;
            if (startOnEnable) Invoke(nameof(StartSequence), SEQUENCE_START_DELAY);
        }

        private void OnDisable()
        {
            GameEvents.OnEnemyKilled -= HandleEnemyKilled;
            GameEvents.OnFormRewardResolved -= HandleFormRewardResolved;
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
                // 인스펙터에서 슬롯을 비우면 요소가 null일 수 있음 → transform 폴백으로 NRE 방지.
                var sp = spawnPoints[index % spawnPoints.Length];
                if (sp != null) return sp;
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

            // 폼 보상 룸: 제단을 활성화하고 상호작용(획득/거절)까지 자동 진행을 보류한다.
            // 모달이 timeScale=0으로 정지시키므로 게이트 중에는 세계가 멈춘다. 제단 미배선이면 게이트 없이 진행(스톨 방지).
            if (room.formReward != null && formAltar != null)
            {
                formAltar.Configure(room.formReward);
                awaitingFormReward = true;
                Debug.Log($"[StageDirector] 폼 보상 룸 — 제단 활성화({room.formReward.formId}), 상호작용까지 진행 보류");
                return;
            }

            Invoke(nameof(ProceedToNextRoom), delayBetweenRooms);
        }

        // 폼 보상 모달이 닫히면(획득/거절) 게이트를 풀고 제단을 숨긴 뒤 다음 방으로 진행한다.
        private void HandleFormRewardResolved()
        {
            if (!awaitingFormReward) return;
            awaitingFormReward = false;

            if (formAltar != null) formAltar.gameObject.SetActive(false);
            Debug.Log("[StageDirector] 폼 보상 해결 — 다음 방 진행");
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
