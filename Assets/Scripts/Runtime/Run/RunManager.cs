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
        private RunEndReason lastRunEndReason = RunEndReason.Death;

        // 직전에 끝난 런의 요약. StartNewRun에서 초기화하지 않는다 —
        // 다음 런이 시작돼도 "직전 런"으로 남아야 도감 기록 탭이 보여줄 것이 있다.
        private RunSummary lastRunSummary = new();

        // 직전에 플레이타임을 누적한 폼. RegisterFormPlaytime은 매 프레임 호출되므로
        // '폼이 바뀐 프레임'만 골라내는 표식으로 쓴다(사용 등록·도감 발견을 프레임마다 하지 않게).
        private string lastPlaytimeFormId = string.Empty;

        public int CurrentLevel => currentLevel;
        public int CurrentExp => currentExp;
        public int ExpToNextLevel => CalcExpRequirement(currentLevel);
        public int GoldShards => goldShards;
        public RunStats Stats => stats;
        public bool IsRunActive => isRunActive;
        public int LastRunAbyssShardsEarned => lastRunAbyssShardsEarned;

        /// <summary>
        /// 직전에 끝난 런의 요약. 결과·엔딩 화면이 표시에 쓰고, 같은 값이 세이브에도 들어간다.
        /// 런이 아직 한 번도 끝나지 않았으면 <c>hasRecord = false</c>인 빈 요약이다(null 아님).
        /// </summary>
        public RunSummary LastRunSummary => lastRunSummary;

        /// <summary>
        /// 직전 런이 끝난 사유. <see cref="GameEvents.OnRunEnded"/> 구독자가 조회한다.
        ///
        /// 이벤트 인자로 싣지 않은 이유: 구독자가 4곳(흐름 FSM·결과 패널·애널리틱스·시너지 HUD)인데
        /// 사유가 필요한 곳은 결과 패널 하나뿐이다. 이미 <see cref="LastRunAbyssShardsEarned"/>가
        /// 같은 규약("이벤트는 신호, 값은 조회")을 쓰고 있어 여기에 맞춘다.
        /// </summary>
        public RunEndReason LastRunEndReason => lastRunEndReason;

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
            lastRunEndReason = RunEndReason.Death;
            lastPlaytimeFormId = string.Empty;   // 새 런의 시작 폼도 '변경'으로 잡혀 사용 등록되게
            stats.Reset();
            isRunActive = true;
            GameEvents.RaiseGoldShardsChanged(goldShards);
            GameEvents.RaiseRunStarted();
            Debug.Log("[RunManager] 런 시작 — isRunActive = true");
        }

        /// <summary>
        /// 런 종료·메타 정산. 사유를 생략하면 사망으로 간주한다 —
        /// 완주(<see cref="RunEndReason.Cleared"/>)는 호출자가 명시해야 엔딩이 열린다.
        /// </summary>
        public void EndRun(RunEndReason reason = RunEndReason.Death)
        {
            if (!isRunActive)
            {
                // 이중 호출 가드 — 정산은 한 번만.
                return;
            }

            isRunActive = false;
            // 구독자가 조회하므로 이벤트 발행보다 먼저 확정해야 한다.
            lastRunEndReason = reason;
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

            // 요약은 심연 조각 적립 뒤에 만든다 — 화면의 "(누적 M)"이 이번 획득분을 포함한 값이어야 한다.
            // 결과·엔딩 화면도 이 객체를 그대로 읽으므로, 보이는 값과 저장되는 값이 어긋날 방법이 없다.
            lastRunSummary = stats.ToSummary(lastRunAbyssShardsEarned, meta.Current.abyssShardsTotal);
            meta.RecordLastRunSummary(lastRunSummary, autoSave: false);

            meta.RecordRunResult(stats.stageReached, stats.totalElapsedSeconds, goldShards, bossKillsThisRun, autoSave: true);
            Debug.Log($"[RunManager] 메타 정산: abyss +{lastRunAbyssShardsEarned} (gold={goldShards}, rate={AbyssShardsConversionRate:F2}) / 누적 {meta.Current.abyssShardsTotal}");
        }

        /// <summary>
        /// FormController가 매 프레임 호출해 현재 폼 플레이타임 누적.
        /// 활성 폼이 바뀐 프레임에는 사용 등록(<see cref="RegisterFormUsage"/>)도 함께 한다 —
        /// 여기가 "이 폼으로 플레이했다"는 사실이 도착하는 유일한 지점이라, 폼 교체 이벤트만 듣던
        /// 기존 경로가 놓치던 <b>시작 폼</b>까지 덮는다.
        /// </summary>
        public void RegisterFormPlaytime(string formId, float delta)
        {
            if (!isRunActive || string.IsNullOrEmpty(formId) || delta <= 0f) return;

            if (formId != lastPlaytimeFormId)
            {
                lastPlaytimeFormId = formId;
                RegisterFormUsage(formId);
            }

            if (!stats.formPlaytimeSeconds.ContainsKey(formId)) stats.formPlaytimeSeconds[formId] = 0f;
            stats.formPlaytimeSeconds[formId] += delta;
        }

        /// <summary>
        /// "이 폼을 사용했다"의 단일 처리 지점 — 런 통계 누적 + 도감 발견 등록.
        ///
        /// 두 소비자(플레이타임 누적·폼 교체 이벤트)가 각자 formsUsed에 넣으면 같은 규약이
        /// 두 곳으로 갈라진다. 도감 발견까지 붙으면서 소비자가 늘었으므로 여기로 모았다.
        /// </summary>
        private void RegisterFormUsage(string formId)
        {
            if (string.IsNullOrEmpty(formId)) return;
            if (!stats.formsUsed.Contains(formId)) stats.formsUsed.Add(formId);
            MetaSaveService.Instance.DiscoverForm(formId);
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

        private void HandleEnemyKilled(Abyss.Runtime.Enemy.EnemyData _, Vector3 __)
        {
            if (isRunActive) stats.enemiesKilled += 1;
        }

        private void HandleSkillDrafted(SkillData skill, DraftTriggerReason _)
        {
            if (skill == null) return;
            stats.draftedSkillIds.Add(skill.skillId);
            stats.totalDraftCount += 1;
            if (!string.IsNullOrEmpty(skill.formBound)) stats.formExclusiveDraftCount += 1;

            // 도감은 '획득' 기준으로 등록한다 — 드래프트에 제시된 것까지 세면 리롤만으로 도감이 차서
            // 진척 표식으로서의 의미를 잃는다.
            MetaSaveService.Instance.DiscoverSkill(skill.skillId);
        }

        private void HandleFormSwapped(FormData previous, FormData next)
        {
            if (previous != null) RegisterFormUsage(previous.formId);
            if (next != null) RegisterFormUsage(next.formId);
        }

        private void HandleRoomEntered(RoomData room)
        {
            if (room == null) return;
            stats.stageReached = room.roomId;
            DiscoverRoomEnemies(room);
        }

        /// <summary>
        /// 방에 배치된 적·보스를 도감에 발견 등록한다(완주 루프 계획 3-1).
        ///
        /// 기준을 '처치'가 아니라 <b>입장</b>으로 둔 이유: 처치 기준이면 최종 보스에게 죽은 플레이어의
        /// 도감에는 방금 2분간 싸운 보스가 <c>???</c>로 남는다. 도감이 기록하는 것은 전과가 아니라
        /// 만난 사실이므로(계획서 "처음 만난 시점에 해금") 입장이 옳은 시점이다.
        /// </summary>
        private static void DiscoverRoomEnemies(RoomData room)
        {
            if (room.enemies == null) return;

            var meta = MetaSaveService.Instance;
            for (int i = 0; i < room.enemies.Count; i++)
            {
                var entry = room.enemies[i];
                if (entry?.data == null) continue;
                meta.DiscoverEnemy(entry.data.enemyId);
            }
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

        /// <summary>완주 경로 테스트용. 최종 보스까지 20분 넘게 걸려 엔딩만 확인하기 어렵다.</summary>
        [ContextMenu("Debug: Clear Run (엔딩)")]
        private void DebugClearRun()
        {
            if (!isRunActive) StartNewRun();
            EndRun(RunEndReason.Cleared);
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
