using Abyss.Runtime.Draft;
using Abyss.Runtime.Events;
using Abyss.Runtime.Form;
using Abyss.Runtime.Meta;
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
        [Header("런 설정 (P-14 — RunConfig SO 단일 소스)")]
        [Tooltip("비워두면 Resources/Data/RunConfig를 자동 로드")]
        [SerializeField] private RunConfig config;

        // config 미할당·로드 실패 시 폴백 기본값
        private const int DEFAULT_BASE_EXP = 100;
        private const float DEFAULT_EXP_GROWTH = 1.2f;
        private const float DEFAULT_ABYSS_RATE = 0.2f;

        private int currentLevel = 1;
        private int currentExp;
        private int goldShards;
        private int bossKillsThisRun;
        private readonly RunStats stats = new();
        private bool isRunActive;
        private int lastRunAbyssShardsEarned;

        public int CurrentLevel => currentLevel;
        public int CurrentExp => currentExp;
        public int ExpToNextLevel => CalcExpRequirement(currentLevel);
        public int GoldShards => goldShards;
        public RunStats Stats => stats;
        public bool IsRunActive => isRunActive;
        public int LastRunAbyssShardsEarned => lastRunAbyssShardsEarned;
        public RunConfig Config => config;

        private int BaseExpToLevel => config != null ? config.baseExpToLevel : DEFAULT_BASE_EXP;
        private float ExpGrowthPerLevel => config != null ? config.expGrowthPerLevel : DEFAULT_EXP_GROWTH;
        private float AbyssShardsConversionRate => config != null ? config.abyssShardsConversionRate : DEFAULT_ABYSS_RATE;

        protected override void OnAwake()
        {
            // RunConfig SoT: SerializeField 오버라이드 우선, 없으면 공유 RunConfigProvider.Current.
            config = RunConfigProvider.Resolve(config);
        }

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
            bossKillsThisRun += 1;
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
            bossKillsThisRun = 0;
            lastRunAbyssShardsEarned = 0;
            stats.Reset();
            isRunActive = true;
            GameEvents.RaiseGoldShardsChanged(goldShards);
            GameEvents.RaiseRunStarted();
            Debug.Log("[RunManager] 런 시작 — isRunActive = true");
        }

        public void EndRun()
        {
            if (!isRunActive)
            {
                // 이중 호출 가드 — 정산은 한 번만.
                return;
            }

            isRunActive = false;
            SettleMetaProgress();
            GameEvents.RaiseRunEnded();
        }

        /// <summary>
        /// 런 종료 시 메타 진행 정산. MetaSaveService는 코어 저장 싱글톤이므로 Instance로 접근해
        /// Bootstrap 미경유(로비 단독 플레이 등)에서도 정산 결과가 유실되지 않게 한다(싱글턴 접근 정책).
        /// 환산: goldShards * abyssShardsConversionRate (반올림, 음수 가드).
        /// </summary>
        private void SettleMetaProgress()
        {
            lastRunAbyssShardsEarned = Mathf.Max(0, Mathf.RoundToInt(goldShards * AbyssShardsConversionRate));

            var meta = MetaSaveService.Instance;
            if (lastRunAbyssShardsEarned > 0)
            {
                meta.AddAbyssShards(lastRunAbyssShardsEarned, autoSave: false);
            }
            meta.RecordRunResult(stats.stageReached, stats.totalElapsedSeconds, goldShards, bossKillsThisRun, autoSave: true);
            Debug.Log($"[RunManager] 메타 정산: abyss +{lastRunAbyssShardsEarned} (gold={goldShards}, rate={AbyssShardsConversionRate:F2}) / 누적 {meta.Current.abyssShardsTotal}");
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
            if (skill == null) return;
            stats.draftedSkillIds.Add(skill.skillId);
            stats.totalDraftCount += 1;
            if (!string.IsNullOrEmpty(skill.formBound)) stats.formExclusiveDraftCount += 1;
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
            // 하한 1 보장 — RunConfig 값 조합(expGrowthPerLevel<1 등)으로 요구치가 0으로
            // 반올림되면 GainExp의 while 루프가 감소 없이 영구 참이 되어 프리즈한다.
            return Mathf.Max(1, Mathf.RoundToInt(BaseExpToLevel * Mathf.Pow(ExpGrowthPerLevel, level - 1)));
        }
    }
}
