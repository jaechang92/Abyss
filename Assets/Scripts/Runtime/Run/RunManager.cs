using Abyss.Runtime.Draft;
using Abyss.Runtime.Events;
using Abyss.Runtime.Form;
using Abyss.Runtime.Stage;
using Singleton_Core;
using UnityEngine;

namespace Abyss.Runtime.Run
{
    /// <summary>
    /// 런 상태·진행 관리. 경험치/레벨/드래프트 트리거의 단일 발행자.
    /// Analyst MF-2 확정 — draft_trigger_reason 소유권은 RunManager.
    /// 경험치 커브는 임시 기본값 사용, 정식 수치는 P-14 RunConfig SO로 교체 예정.
    /// </summary>
    public sealed class RunManager : SingletonManager<RunManager>
    {
        [Header("임시 경험치 커브 (P-14에서 RunConfig SO로 교체)")]
        [SerializeField] private int baseExpToLevel = 100;
        [SerializeField] private float expGrowthPerLevel = 1.2f;

        private int currentLevel = 1;
        private int currentExp;
        private int goldShards;
        private readonly RunStats stats = new();
        private bool isRunActive;

        public int CurrentLevel => currentLevel;
        public int CurrentExp => currentExp;
        public int ExpToNextLevel => CalcExpRequirement(currentLevel);
        public int GoldShards => goldShards;
        public RunStats Stats => stats;
        public bool IsRunActive => isRunActive;

        /// <summary>
        /// 런 내 화폐 획득. Analyst 확정 — abyss_shards(메타)는 별도 MetaSave(P-21).
        /// </summary>
        public void GainGoldShards(int amount)
        {
            if (amount == 0) return;
            goldShards = Mathf.Max(0, goldShards + amount);
            GameEvents.RaiseGoldShardsChanged(goldShards);
        }

        /// <summary>
        /// 리롤 등 소비. 잔액 부족 시 false 반환(상태 불변).
        /// </summary>
        public bool SpendGoldShards(int amount)
        {
            if (amount <= 0) return true;
            if (goldShards < amount) return false;
            goldShards -= amount;
            GameEvents.RaiseGoldShardsChanged(goldShards);
            return true;
        }

        /// <summary>
        /// 경험치 획득 요청. 레벨업이 누적 발생해도 while 루프로 연쇄 처리.
        /// </summary>
        public void GainExp(int amount, string source)
        {
            if (amount <= 0) return;

            currentExp += amount;
            GameEvents.RaiseExpGained(amount, source);

            while (currentExp >= ExpToNextLevel)
            {
                currentExp -= ExpToNextLevel;
                currentLevel += 1;
                GameEvents.RaisePlayerLevelUp(currentLevel, DraftTriggerReason.LevelUp);
            }
        }

        /// <summary>
        /// 보너스 레벨 1회분 즉시 지급. 처치 보상 이벤트에서 사용.
        /// </summary>
        public void GrantBonusLevel(DraftTriggerReason reason)
        {
            currentLevel += 1;
            GameEvents.RaisePlayerLevelUp(currentLevel, reason);
        }

        /// <summary>
        /// 보스 처치 이벤트 훅. OnBossKilled 발행 + BossBonus 레벨 1회 보장.
        /// </summary>
        public void NotifyBossKilled()
        {
            GameEvents.RaiseBossKilled();
            GrantBonusLevel(DraftTriggerReason.BossBonus);
        }

        /// <summary>
        /// 엘리트 처치 이벤트 훅. EliteBonus 레벨 1회 지급.
        /// </summary>
        public void NotifyEliteKilled()
        {
            GrantBonusLevel(DraftTriggerReason.EliteBonus);
        }

        public void StartNewRun()
        {
            if (isRunActive) return;

            currentLevel = 1;
            currentExp = 0;
            goldShards = 0;
            stats.Reset();
            isRunActive = true;
            GameEvents.RaiseGoldShardsChanged(goldShards);
            GameEvents.RaiseRunStarted();
            Debug.Log("[RunManager] 런 시작 — isRunActive = true");
        }

        public void EndRun()
        {
            isRunActive = false;
            GameEvents.RaiseRunEnded();
        }

        /// <summary>
        /// FormController가 매 프레임 호출해 현재 폼 플레이타임 누적.
        /// </summary>
        public void RegisterFormPlaytime(string formId, float delta)
        {
            if (!isRunActive || string.IsNullOrEmpty(formId) || delta <= 0f) return;
            if (!stats.formPlaytimeSeconds.ContainsKey(formId)) stats.formPlaytimeSeconds[formId] = 0f;
            stats.formPlaytimeSeconds[formId] += delta;
        }

        private void OnEnable()
        {
            GameEvents.OnEnemyKilled += HandleEnemyKilled;
            GameEvents.OnSkillDrafted += HandleSkillDrafted;
            GameEvents.OnFormSwapped += HandleFormSwapped;
            GameEvents.OnRoomEntered += HandleRoomEntered;
            GameEvents.OnPlayerDead += HandlePlayerDead;
        }

        private void OnDisable()
        {
            GameEvents.OnEnemyKilled -= HandleEnemyKilled;
            GameEvents.OnSkillDrafted -= HandleSkillDrafted;
            GameEvents.OnFormSwapped -= HandleFormSwapped;
            GameEvents.OnRoomEntered -= HandleRoomEntered;
            GameEvents.OnPlayerDead -= HandlePlayerDead;
        }

        private void Update()
        {
            if (isRunActive) stats.totalElapsedSeconds += Time.unscaledDeltaTime;
        }

        private void HandleEnemyKilled(Abyss.Runtime.Enemy.EnemyData _)
        {
            if (isRunActive) stats.enemiesKilled += 1;
        }

        private void HandleSkillDrafted(SkillData skill, DraftTriggerReason _)
        {
            if (skill != null) stats.draftedSkillIds.Add(skill.skillId);
        }

        private void HandleFormSwapped(FormData previous, FormData next)
        {
            if (previous != null && !stats.formsUsed.Contains(previous.formId)) stats.formsUsed.Add(previous.formId);
            if (next != null && !stats.formsUsed.Contains(next.formId)) stats.formsUsed.Add(next.formId);
        }

        private void HandleRoomEntered(RoomData room)
        {
            if (room != null) stats.stageReached = room.roomId;
        }

        private void HandlePlayerDead()
        {
            if (isRunActive) EndRun();
        }

        [ContextMenu("Debug: End Run")]
        private void DebugEndRun()
        {
            if (!isRunActive)
            {
                StartNewRun();
            }
            EndRun();
        }

        [ContextMenu("Debug: Gain 100 Exp")]
        private void DebugGainExp()
        {
            GainExp(100, "debug");
        }

        [ContextMenu("Debug: Notify Boss Killed")]
        private void DebugBossKilled()
        {
            if (!isRunActive) StartNewRun();
            NotifyBossKilled();
        }

        private int CalcExpRequirement(int level)
        {
            return Mathf.RoundToInt(baseExpToLevel * Mathf.Pow(expGrowthPerLevel, level - 1));
        }
    }
}
