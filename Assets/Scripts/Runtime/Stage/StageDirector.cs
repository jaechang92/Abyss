using System.Collections.Generic;
using Abyss.Runtime.Enemy;
using Abyss.Runtime.Events;
using Abyss.Runtime.Form;
using Abyss.Runtime.Run;
using Abyss.Runtime.UI;
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
        private int currentStepIndex = -1;

        // 이번 단계에서 실제로 들어간 방. 분기 단계에서는 플레이어가 고른 것이라
        // 인덱스만으로는 역산할 수 없어 참조를 들고 있어야 한다.
        private RoomData currentRoom;

        private readonly List<EnemyBase> activeEnemies = new();
        private bool isRoomClearing;  // ProceedToNextRoom 지연 창 동안 룸 이중 클리어(보상 중복·방 스킵) 방지
        private FormController cachedFormController;  // 미보유 폼 판정용 지연 조회 캐시

        // 룸 클리어 후 플레이어 상호작용(폼 제단·이벤트 선택)을 기다리는 동안 자동 진행을 보류하는 게이트.
        // 보류 사유가 둘이 되면서 사유별 플래그 대신 하나로 합쳤다 — 해제 규약이 갈라지면
        // 한쪽만 고쳐 방이 영영 안 넘어가는 스톨이 난다.
        private bool isRoomGateHeld;

        public StageSequenceData Sequence => sequence;
        public StageData CurrentStage => IsValidStage(currentStageIndex) ? sequence.stages[currentStageIndex] : null;
        public RoomData CurrentRoom => currentRoom;
        public int CurrentStageIndex => currentStageIndex;
        public int CurrentStepIndex => currentStepIndex;
        public int RemainingRooms => CurrentStage != null ? Mathf.Max(0, CurrentStage.steps.Count - 1 - currentStepIndex) : 0;
        public int RemainingStages => sequence != null ? Mathf.Max(0, sequence.stages.Count - 1 - currentStageIndex) : 0;

        private void OnEnable()
        {
            GameEvents.OnEnemyKilled += HandleEnemyKilled;
            GameEvents.OnFormRewardResolved += HandleFormRewardResolved;
            GameEvents.OnEventResolved += HandleEventResolved;
            if (startOnEnable) Invoke(nameof(StartSequence), SEQUENCE_START_DELAY);
        }

        private void OnDisable()
        {
            GameEvents.OnEnemyKilled -= HandleEnemyKilled;
            GameEvents.OnFormRewardResolved -= HandleFormRewardResolved;
            GameEvents.OnEventResolved -= HandleEventResolved;
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
            if (stage == null || stage.steps.Count == 0)
            {
                Debug.LogWarning($"[StageDirector] Stage {index + 1} 또는 steps 미설정 — 건너뜀");
                ProceedToNextStage();
                return;
            }

            Debug.Log($"[StageDirector] Stage {index + 1}/{sequence.stages.Count} 진입: {stage.displayName} ({stage.steps.Count}단계)");
            currentStepIndex = 0;
            EnterStep(currentStepIndex);
        }

        public void ProceedToNextRoom()
        {
            isRoomClearing = false;  // 다음 단계로 넘어가며 클리어 상태 해제
            currentStepIndex += 1;

            if (CurrentStage == null || currentStepIndex >= CurrentStage.steps.Count)
            {
                // 현재 스테이지의 모든 단계 클리어 → 스테이지 클리어 처리 후 다음 스테이지로.
                if (CurrentStage != null) GameEvents.RaiseStageCleared(CurrentStage);
                ProceedToNextStage();
                return;
            }

            EnterStep(currentStepIndex);
        }

        /// <summary>
        /// 한 단계 진입. 선택지가 둘 이상이면 갈림길 패널을 띄우고 선택을 기다린다.
        ///
        /// 룸 게이트를 쓰지 않는 이유: 게이트는 "방을 클리어한 뒤 다음으로 넘어가는 것"을 보류하는데,
        /// 여기는 이미 넘어온 뒤라 단계 인덱스가 증가한 상태다. 게이트를 잡으면 해제 시
        /// <see cref="ProceedToNextRoom"/>이 다시 돌아 단계를 한 번 더 건너뛴다.
        /// 대신 패널 콜백이 곧바로 방 진입으로 이어진다(정지는 패널이 DraftOpen으로 건다).
        /// </summary>
        private void EnterStep(int index)
        {
            var stage = CurrentStage;
            if (stage == null || !IsValidStep(index)) return;

            var step = stage.steps[index];
            if (step == null || step.First == null)
            {
                Debug.LogWarning($"[StageDirector] Step {index + 1} 선택지 없음 — 건너뜀");
                ProceedToNextRoom();
                return;
            }

            if (!step.IsBranch)
            {
                EnterRoom(step.First);
                return;
            }

            Debug.Log($"[StageDirector] 갈림길 — 선택 대기 ({step.options.Count}갈래)");
            NodeMapPanel.Open(step.options, HandleNodePicked);
        }

        // 갈림길 선택 결과. 유효한 방이 없으면(데이터 오류) 스톨 대신 다음 단계로 넘긴다.
        private void HandleNodePicked(RoomData picked)
        {
            if (picked == null)
            {
                Debug.LogWarning("[StageDirector] 갈림길 선택 결과가 비었다 — 다음 단계로 진행");
                ProceedToNextRoom();
                return;
            }
            EnterRoom(picked);
        }

        private void ProceedToNextStage()
        {
            currentStageIndex += 1;

            if (currentStageIndex >= sequence.stages.Count)
            {
                // 시퀀스의 모든 스테이지 클리어 → 완주로 런 종료(엔딩 경로).
                // 트리거를 '시퀀스의 마지막'으로 두면 스테이지 3이 추가돼도 여기는 그대로다.
                Debug.Log("[StageDirector] 모든 스테이지 클리어 — 완주");
                RunManager.Instance?.EndRun(RunEndReason.Cleared);
                return;
            }

            Debug.Log($"[StageDirector] 다음 스테이지로 진행 ({delayBetweenStages}s 후)");
            Invoke(nameof(EnterCurrentStage), delayBetweenStages);
        }

        // Invoke 대상용 무인자 래퍼 (currentStageIndex는 ProceedToNextStage에서 이미 증가).
        private void EnterCurrentStage() => EnterStage(currentStageIndex);

        private void EnterRoom(RoomData room)
        {
            if (room == null) return;

            currentRoom = room;
            int total = CurrentStage != null ? CurrentStage.steps.Count : 0;
            Debug.Log($"[StageDirector] Step {currentStepIndex + 1}/{total} 진입: {room.roomId} ({room.roomType})");
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

            // 이벤트 룸: 선택 모달을 열고 선택이 끝날 때까지 자동 진행을 보류한다.
            // 모달이 DraftOpen 상태로 정지시키므로 게이트 중에는 세계가 멈춘다.
            if (room.IsEventRoom)
            {
                isRoomGateHeld = true;
                Debug.Log($"[StageDirector] 이벤트 룸 — 선택 대기: {room.eventData.eventId}");
                EventRoomPanel.Open(room.eventData);
                return;
            }

            // 상점 룸: 진열 모달을 열고 [떠난다]까지 자동 진행을 보류한다.
            // 게이트도 해제 신호도 이벤트 룸과 같은 것을 쓴다 — 비전투 방의 규약은 하나다.
            if (room.IsShopRoom)
            {
                isRoomGateHeld = true;
                Debug.Log($"[StageDirector] 상점 룸 — 구매 대기: {room.shopData.shopId}");
                ShopRoomPanel.Open(room.shopData);
                return;
            }

            // 폼 보상 룸: 제단을 활성화하고 상호작용(획득/거절)까지 자동 진행을 보류한다.
            // 제단 미배선이거나 제시할 폼이 없으면 게이트 없이 진행(스톨 방지).
            if (room.IsFormRewardRoom && formAltar != null)
            {
                var reward = ResolveRewardForm(room);
                if (reward != null)
                {
                    formAltar.Configure(reward);
                    isRoomGateHeld = true;
                    Debug.Log($"[StageDirector] 폼 보상 룸 — 제단 활성화({reward.formId}), 상호작용까지 진행 보류");
                    return;
                }

                Debug.LogWarning($"[StageDirector] 폼 보상 룸({room.roomId})이나 제시할 폼이 없어 게이트 없이 진행 — FormCatalog 비어 있음 여부 확인");
            }

            Invoke(nameof(ProceedToNextRoom), delayBetweenRooms);
        }

        /// <summary>
        /// 보류 중이던 룸 게이트를 풀고 다음 방으로 진행한다.
        /// 보류하지 않은 상태에서 온 해제 신호는 무시한다 — 다른 경로(로비 제단 등)의 같은 이벤트가
        /// 방을 이중으로 넘기지 않게.
        /// </summary>
        private void ReleaseRoomGate(string reason)
        {
            if (!isRoomGateHeld) return;
            isRoomGateHeld = false;

            Debug.Log($"[StageDirector] {reason} — 다음 방 진행");
            Invoke(nameof(ProceedToNextRoom), delayBetweenRooms);
        }

        /// <summary>
        /// 보상 룸이 제시할 폼을 결정한다.
        /// 1) 룸에 고정 폼이 지정되어 있으면 그대로(레거시·의도적 고정 보상).
        /// 2) 아니면 FormCatalog에서 플레이어 미보유 폼을 우선 추첨.
        /// 3) 미보유가 없으면(전 폼 보유) 카탈로그 전체에서 현재 슬롯 밖 폼을 추첨, 그마저 없으면 null(게이트 스킵).
        /// </summary>
        private FormData ResolveRewardForm(RoomData room)
        {
            if (room.formReward != null) return room.formReward;

            var catalog = FormCatalog.All;
            if (catalog == null || catalog.Length == 0) return null;

            var form = ResolveFormController();
            var owned = form != null ? form.GetOwnedForms() : null;

            var candidates = FormCatalog.GetExcluding(owned);
            if (candidates.Count > 0)
            {
                return candidates[UnityEngine.Random.Range(0, candidates.Count)];
            }

            // 전 폼 보유 상태 — 중복이라도 제시해 보상 룸이 빈손이 되지 않게 한다.
            // GetExcluding과 동일하게 null 엔트리를 배제한 뒤 뽑는다(카탈로그에 깨진 참조가 섞여도 안전).
            var fallback = FormCatalog.GetExcluding(null);
            return fallback.Count > 0 ? fallback[UnityEngine.Random.Range(0, fallback.Count)] : null;
        }

        /// <summary>
        /// 씬의 FormController를 지연 조회해 캐시한다(런 씬에서 플레이어는 1명).
        /// StageDirector는 플레이어 생성 순서에 의존하지 않으므로 직렬화 참조 대신 필요 시점에 찾는다.
        /// </summary>
        private FormController ResolveFormController()
        {
            if (cachedFormController == null)
            {
                cachedFormController = FindAnyObjectByType<FormController>();
            }
            return cachedFormController;
        }

        // 폼 보상 모달이 닫히면(획득/거절) 제단을 숨기고 게이트를 푼다.
        private void HandleFormRewardResolved()
        {
            if (!isRoomGateHeld) return;

            if (formAltar != null) formAltar.gameObject.SetActive(false);
            ReleaseRoomGate("폼 보상 해결");
        }

        // 비전투 방(이벤트·휴식·상점)의 상호작용이 끝나면 게이트를 푼다. 그 결과로 스킬 드래프트가
        // 열렸더라도 ProceedToNextRoom은 스케일 시간 Invoke라 드래프트가 닫힐 때까지 알아서 기다린다.
        private void HandleEventResolved() => ReleaseRoomGate("비전투 방 해결");

        private bool IsValidStage(int index)
        {
            return sequence != null && index >= 0 && index < sequence.stages.Count;
        }

        private bool IsValidStep(int index)
        {
            var stage = CurrentStage;
            return stage != null && index >= 0 && index < stage.steps.Count;
        }
    }
}
