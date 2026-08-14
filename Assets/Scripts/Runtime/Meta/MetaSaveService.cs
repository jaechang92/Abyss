using System;
using System.Collections.Generic;
using System.IO;
using Abyss.Runtime.Run;
using SaveSystem_Core;
using Singleton_Core;
using UnityEngine;

namespace Abyss.Runtime.Meta
{
    /// <summary>
    /// 마지막 로드가 어떤 경로로 끝났는지. 치트 메뉴·테스트가 "세이브가 정상적으로 이어졌는가"를
    /// 확인하는 근거다 — 로그만으로는 사후에 알 수 없다.
    /// </summary>
    public enum MetaSaveLoadResult
    {
        /// <summary>아직 로드하지 않았다.</summary>
        NotLoaded,

        /// <summary>파일이 없어 새로 만들었다.</summary>
        NewFile,

        /// <summary>현재 스키마 버전 그대로 읽었다.</summary>
        UpToDate,

        /// <summary>구버전을 변환해 읽었다.</summary>
        Migrated,

        /// <summary>현재 코드보다 높은 버전이다. 원본은 백업됐다.</summary>
        FutureVersion,

        /// <summary>변환 단계가 없어 변환하지 못했다. 원본 값 그대로 사용 중이다.</summary>
        MigrationFailed,

        /// <summary>파싱에 실패해 새 세이브로 시작했다. 원본은 백업됐다.</summary>
        Corrupted
    }

    /// <summary>
    /// MetaSave 읽기/쓰기 게이트웨이. SaveSystem을 경유하고 상위 시스템은 이 API만 사용.
    /// AbyssBootstrap에서 SaveSystem 이후 RunManager 이전에 초기화해야 RunManager.EndRun 정산이 정상 동작.
    /// </summary>
    public sealed partial class MetaSaveService : SingletonManager<MetaSaveService>
    {
        private MetaSave current;
        private bool isLoaded;

        /// <summary>현재 메모리 상 메타 데이터 스냅샷. 외부는 읽기 전용으로만 사용.</summary>
        public MetaSave Current
        {
            get
            {
                EnsureLoaded();
                return current;
            }
        }

        /// <summary>로드 완료 여부. Analyst SoT 외 디버그용.</summary>
        public bool IsLoaded => isLoaded;

        /// <summary>
        /// 마지막 로드 경로. <see cref="MetaSaveLoadResult.Corrupted"/>·
        /// <see cref="MetaSaveLoadResult.FutureVersion"/>·<see cref="MetaSaveLoadResult.MigrationFailed"/>는
        /// 백업 파일이 생겼다는 뜻이다.
        /// </summary>
        public MetaSaveLoadResult LastLoadResult { get; private set; } = MetaSaveLoadResult.NotLoaded;

        /// <summary>
        /// 강제 재로드. 옵션 초기화 등 예외 경로에서만 사용.
        /// </summary>
        public MetaSave Reload()
        {
            isLoaded = false;
            EnsureLoaded();
            return current;
        }

        /// <summary>
        /// 디스크 기록. JsonUtility prettyPrint 유지(디버그 열람 편의).
        /// </summary>
        public bool Save()
        {
            EnsureLoaded();
            current.Touch();
            return SaveSystem.Instance.Save(current, MetaSave.FileName, prettyPrint: true);
        }

        /// <summary>
        /// abyss_shards 누적. 음수/0 무시. 자동 저장 여부는 호출자 선택.
        /// </summary>
        public void AddAbyssShards(int amount, bool autoSave = true)
        {
            if (amount <= 0) return;
            EnsureLoaded();
            current.abyssShardsTotal = Mathf.Max(0, current.abyssShardsTotal + amount);
            if (autoSave) Save();
        }

        /// <summary>
        /// 폼 해제. 중복 ID·빈 문자열 무시.
        /// </summary>
        public bool UnlockForm(string formId, bool autoSave = true)
        {
            if (string.IsNullOrEmpty(formId)) return false;
            EnsureLoaded();
            if (current.unlockedFormIds.Contains(formId)) return false;
            current.unlockedFormIds.Add(formId);
            if (autoSave) Save();
            return true;
        }

        // ⚠️ unlockedFormIds 조회 API는 두지 않는다. UnlockForm의 호출부가 코드베이스에 0곳이라
        // 이 목록은 **항상 비어 있고**, 조회 API가 있으면 "해금 여부"를 묻는 코드가 조용히 전부 false를
        // 받는다(각인사 내력 게이트가 실제로 이 함정에 빠졌다 — 2026-08-14).
        // 폼 보유 판정이 필요하면 IsFormDiscovered(실제로 써 본 폼)를 쓸 것.

        /// <summary>
        /// 스킬 해제. 중복 ID·빈 문자열 무시.
        /// </summary>
        public bool UnlockSkill(string skillId, bool autoSave = true)
        {
            if (string.IsNullOrEmpty(skillId)) return false;
            EnsureLoaded();
            if (current.unlockedSkillIds.Contains(skillId)) return false;
            current.unlockedSkillIds.Add(skillId);
            if (autoSave) Save();
            return true;
        }

        // ───────────────────────── 도감 발견 (완주 루프 계획 3-1) ─────────────────────────
        //
        // 발견은 "해금"(unlockedFormIds 등)과 별개 목록이다 — MetaSave 주석 참조.
        //
        // 세 API 모두 신규 ID일 때만 저장한다. 발견 신호는 방에 들어갈 때마다 도착하지만
        // 중복은 Contains에서 조기 반환되므로, 디스크 쓰기는 게임 수명 동안 '항목 수'만큼만 일어난다.
        // 그래서 호출부가 저장 빈도를 신경 쓰지 않아도 되고 autoSave 기본값을 true로 둘 수 있다.

        /// <summary>도감에 폼 발견 등록. 신규 등록 시에만 true(그때만 저장한다).</summary>
        public bool DiscoverForm(string formId, bool autoSave = true) =>
            Discover(Current.discoveredFormIds, formId, autoSave);

        /// <summary>도감에 스킬 발견 등록. 신규 등록 시에만 true.</summary>
        public bool DiscoverSkill(string skillId, bool autoSave = true) =>
            Discover(Current.discoveredSkillIds, skillId, autoSave);

        /// <summary>도감에 적·보스 발견 등록. 신규 등록 시에만 true.</summary>
        public bool DiscoverEnemy(string enemyId, bool autoSave = true) =>
            Discover(Current.discoveredEnemyIds, enemyId, autoSave);

        public bool IsFormDiscovered(string formId) => Contains(Current.discoveredFormIds, formId);
        public bool IsSkillDiscovered(string skillId) => Contains(Current.discoveredSkillIds, skillId);
        public bool IsEnemyDiscovered(string enemyId) => Contains(Current.discoveredEnemyIds, enemyId);

        private bool Discover(List<string> target, string id, bool autoSave)
        {
            if (string.IsNullOrEmpty(id)) return false;
            if (target.Contains(id)) return false;
            target.Add(id);
            if (autoSave) Save();
            return true;
        }

        private static bool Contains(List<string> list, string id) =>
            !string.IsNullOrEmpty(id) && list != null && list.Contains(id);

        /// <summary>
        /// 직전 런 요약을 덮어쓴다. <see cref="RecordRunResult"/>(최고 기록 갱신)와 달리
        /// <b>비교 없이 항상 교체</b>한다 — "직전"은 최고가 아니라 가장 최근이기 때문이다.
        ///
        /// RunManager가 정산 도중 호출하며, 저장은 마지막 <see cref="RecordRunResult"/> 한 번으로 묶는다
        /// (autoSave 기본값을 false로 두지 않은 이유: 단독 호출자가 생겼을 때 저장을 잊는 쪽이
        /// 중복 저장보다 나쁘다).
        /// </summary>
        public void RecordLastRunSummary(RunSummary summary, bool autoSave = true)
        {
            if (summary == null) return;

            Current.lastRunSummary = summary;
            if (autoSave) Save();
        }

        /// <summary>
        /// 런 기록 갱신. 각 필드 최대값 유지 전략. autoSave=true 시 즉시 디스크 반영.
        /// RunManager.EndRun에서 RunStats/GoldShards 최종값과 함께 호출.
        ///
        /// 도달 지점도 <b>다른 필드들과 같은 규칙</b>(더 좋을 때만 갱신)을 따른다. 예전에는 여기만
        /// 비교 없이 덮어써서 이름은 <c>best</c>인데 실제로는 '직전'이었고, 도감이 그것을
        /// "최고 도달"로 보여주고 있었다.
        /// </summary>
        public void RecordRunResult(StageReach reached, float runDurationSeconds, int goldShards, int bossKillDelta, bool autoSave = true)
        {
            EnsureLoaded();
            var r = current.records;
            r.totalRunCount += 1;
            r.totalBossKillCount += Mathf.Max(0, bossKillDelta);
            if (reached.IsDeeperThan(r.bestReach)) r.bestReach = reached;
            if (runDurationSeconds > r.bestRunDurationSeconds) r.bestRunDurationSeconds = runDurationSeconds;
            if (goldShards > r.bestGoldShards) r.bestGoldShards = goldShards;
            if (autoSave) Save();
        }

        /// <summary>
        /// 엔딩 도달 기록. 이미 본 상태면 저장을 건너뛴다 — 완주할 때마다 같은 값을 쓰지 않게.
        /// </summary>
        public void MarkEndingSeen(bool autoSave = true)
        {
            EnsureLoaded();
            if (current.records.hasSeenEnding) return;
            current.records.hasSeenEnding = true;
            if (autoSave) Save();
        }

        /// <summary>프롤로그 자막을 이미 봤는가. 세이브가 없으면 false(= 아직 안 봤다).</summary>
        public bool HasSeenPrologue
        {
            get
            {
                EnsureLoaded();
                return current.records.hasSeenPrologue;
            }
        }

        /// <summary>
        /// 프롤로그 시청 기록. <see cref="MarkEndingSeen"/>과 같은 형태로, 이미 본 상태면 저장을 건너뛴다.
        /// </summary>
        public void MarkPrologueSeen(bool autoSave = true)
        {
            EnsureLoaded();
            if (current.records.hasSeenPrologue) return;
            current.records.hasSeenPrologue = true;
            if (autoSave) Save();
        }

        /// <summary>
        /// 프롤로그를 다시 볼 수 있게 되돌린다. <b>치트 메뉴 전용</b> —
        /// 프롤로그는 세이브당 1회라, 이 경로가 없으면 확인할 때마다 세이브를 지워야 한다.
        /// </summary>
        public void ResetPrologueSeen(bool autoSave = true)
        {
            EnsureLoaded();
            if (!current.records.hasSeenPrologue) return;
            current.records.hasSeenPrologue = false;
            if (autoSave) Save();
        }

        /// <summary>
        /// 지정 업그레이드의 현재 레벨. 미보유/빈 ID는 0.
        /// </summary>
        public int GetUpgradeLevel(string upgradeId)
        {
            if (string.IsNullOrEmpty(upgradeId)) return 0;
            EnsureLoaded();
            foreach (var e in current.upgradeLevels)
            {
                if (e.upgradeId == upgradeId) return e.level;
            }
            return 0;
        }

        /// <summary>
        /// 업그레이드 1레벨 구매 시도. 최대 레벨 도달·잔액 부족 시 false(변경 없음).
        /// 성공 시 abyss_shards 차감 + 레벨++ 후 즉시 저장(autoSave).
        /// </summary>
        public bool TryPurchaseUpgrade(MetaUpgradeData data, bool autoSave = true)
        {
            if (data == null || string.IsNullOrEmpty(data.upgradeId)) return false;
            EnsureLoaded();

            int level = GetUpgradeLevel(data.upgradeId);
            if (level >= data.MaxLevel) return false;             // 최대 도달

            int cost = data.CostForNextLevel(level);
            if (cost < 0 || current.abyssShardsTotal < cost) return false;  // 잔액 부족

            current.abyssShardsTotal -= cost;
            SetUpgradeLevel(data.upgradeId, level + 1);
            if (autoSave) Save();
            return true;
        }

        private void SetUpgradeLevel(string upgradeId, int level)
        {
            for (int i = 0; i < current.upgradeLevels.Count; i++)
            {
                if (current.upgradeLevels[i].upgradeId != upgradeId) continue;
                var e = current.upgradeLevels[i];
                e.level = level;
                current.upgradeLevels[i] = e;
                return;
            }
            current.upgradeLevels.Add(new MetaUpgradeEntry { upgradeId = upgradeId, level = level });
        }

        // 스토리 진행도(화자별) API는 MetaSaveService.Story.cs — 500줄 규약으로 분리.

        /// <summary>
        /// 옵션 창 등에서 볼륨 변경 시 호출. 일괄 저장은 호출자가 컨트롤.
        /// </summary>
        public void UpdateSettings(float masterVolume, float bgmVolume, float sfxVolume, bool autoSave = true)
        {
            EnsureLoaded();
            var s = current.settings;
            s.masterVolume = Mathf.Clamp01(masterVolume);
            s.bgmVolume = Mathf.Clamp01(bgmVolume);
            s.sfxVolume = Mathf.Clamp01(sfxVolume);
            if (autoSave) Save();
        }

        /// <summary>
        /// 화면 설정 저장(해상도·전체화면). 적용은 호출자(SettingsPanel)가 담당한다.
        /// 해상도는 인덱스가 아니라 실제 값으로 저장한다 — 인덱스는 기기마다 목록이 달라 복원이 어긋난다.
        /// </summary>
        public void UpdateScreenSettings(int width, int height, bool isFullscreen, bool autoSave = true)
        {
            EnsureLoaded();
            var s = current.settings;
            s.screenWidth = Mathf.Max(0, width);
            s.screenHeight = Mathf.Max(0, height);
            s.isFullscreen = isFullscreen;
            if (autoSave) Save();
        }

        /// <summary>
        /// 디버그 리셋. 확인 다이얼로그는 호출자 책임.
        /// 설정(볼륨·화면)은 진행도가 아니므로 이월한다 — 세이브 초기화가 볼륨까지 되돌리면
        /// 사용자는 잃을 이유가 없는 것을 잃는다.
        /// </summary>
        public void ResetAll(bool autoSave = true)
        {
            var preservedSettings = current != null ? current.settings : null;
            current = MetaSave.CreateNew();
            if (preservedSettings != null) current.settings = preservedSettings;
            isLoaded = true;
            LastLoadResult = MetaSaveLoadResult.NewFile;
            if (autoSave) Save();
        }

        /// <summary>
        /// 디버그: 해제 상태 로그.
        /// </summary>
        [ContextMenu("Debug: Log Meta Summary")]
        private void DebugLogSummary()
        {
            EnsureLoaded();
            Debug.Log($"[MetaSaveService] v{current.version}(코드 v{MetaSave.CurrentVersion}, 로드={LastLoadResult}) abyss={current.abyssShardsTotal} forms={string.Join(",", current.unlockedFormIds)} skills={current.unlockedSkillIds.Count}개 runs={current.records.totalRunCount}");
        }

        private void EnsureLoaded()
        {
            if (isLoaded && current != null) return;

            string path = SaveSystem.Instance.GetFilePath(MetaSave.FileName);
            bool loadedFromDisk = false;

            if (!File.Exists(path))
            {
                current = MetaSave.CreateNew();
                LastLoadResult = MetaSaveLoadResult.NewFile;
                Debug.Log("[MetaSaveService] 신규 MetaSave 생성");
            }
            else
            {
                var loaded = SaveSystem.Instance.Load<MetaSave>(MetaSave.FileName);
                if (loaded == null)
                {
                    // 파싱 실패. 예전에는 여기서 빈 인스턴스로 넘어갔는데, 그러면 곧이어 일어나는
                    // 아무 저장(Discover*·UpdateSettings 등)이 원본을 덮어써 복구 가능성까지 지웠다.
                    // 파일이 깨진 것과 진행도가 사라지는 것은 별개여야 한다 — 사람이 손볼 수 있게 먼저 치워 둔다.
                    string backup = BackupSaveFile(path, $"corrupt-{DateTime.Now:yyyyMMdd-HHmmss}", overwrite: true);
                    current = MetaSave.CreateNew();
                    LastLoadResult = MetaSaveLoadResult.Corrupted;
                    Debug.LogError(
                        $"[MetaSaveService] 메타 세이브 파싱 실패 → 새 세이브로 시작한다. " +
                        $"원본 백업: {backup ?? "실패"}");
                }
                else
                {
                    current = loaded;
                    loadedFromDisk = true;
                }
            }

            // 마이그레이션 단계가 하위 컬렉션을 만질 수 있으므로 변환보다 먼저 채운다.
            NormalizeLists(current);
            // 아래 Save()가 EnsureLoaded를 재귀 호출하지 않도록 먼저 세운다.
            isLoaded = true;

            if (loadedFromDisk) ApplyMigration(path);
        }

        /// <summary>
        /// 디스크에서 읽은 세이브의 스키마 버전을 처리한다.
        ///
        /// 네 경우 모두 <b>원본 백업이 먼저다</b>. 변환이 틀렸거나(Migrated) 현재 코드가 모르는
        /// 필드가 있는(FutureVersion) 세이브는 다음 저장 한 번으로 되돌릴 수 없게 되는데,
        /// 백업 파일 하나면 그 되돌림이 항상 가능하다.
        /// </summary>
        private void ApplyMigration(string path)
        {
            var outcome = MetaSaveMigration.Run(current, out int fileVersion);

            switch (outcome)
            {
                case MetaSaveMigrationOutcome.UpToDate:
                    LastLoadResult = MetaSaveLoadResult.UpToDate;
                    break;

                case MetaSaveMigrationOutcome.Migrated:
                    LastLoadResult = MetaSaveLoadResult.Migrated;
                    BackupSaveFile(path, $"v{fileVersion}", overwrite: false);
                    Debug.Log($"[MetaSaveService] 세이브 스키마 v{fileVersion} → v{MetaSave.CurrentVersion} 변환 완료");
                    Save();
                    break;

                case MetaSaveMigrationOutcome.FutureVersion:
                    LastLoadResult = MetaSaveLoadResult.FutureVersion;
                    BackupSaveFile(path, $"v{fileVersion}", overwrite: false);
                    // 저장을 잠그는 안은 기각했다 — 그러면 이번 세션의 진행이 통째로 사라진다.
                    // 계속 쓰되 백업으로 복구 가능성만 남긴다. 잃는 쪽을 사용자가 고를 수 있어야 한다.
                    Debug.LogError(
                        $"[MetaSaveService] 세이브 버전 v{fileVersion}이 현재 코드(v{MetaSave.CurrentVersion})보다 높다. " +
                        $"모르는 필드는 다음 저장에서 사라진다 — 원본은 백업해 두었다.");
                    break;

                case MetaSaveMigrationOutcome.Incomplete:
                    LastLoadResult = MetaSaveLoadResult.MigrationFailed;
                    BackupSaveFile(path, $"v{fileVersion}", overwrite: false);
                    // 변환은 하나도 적용되지 않았다(하한 보정 외에는 version도 그대로).
                    // Touch()가 CurrentVersion을 찍지 않으므로, 단계를 채워 다시 실행하면 그때 정상 변환된다.
                    Debug.LogError(
                        $"[MetaSaveService] v{fileVersion} 세이브를 변환하지 못했다(단계 누락). " +
                        $"원본 값 그대로 사용한다 — MetaSaveMigration 단계표를 확인할 것.");
                    break;
            }
        }

        /// <summary>
        /// 세이브 파일을 <c>abyss_meta.{tag}.bak</c>으로 복사한다. 실패해도 로드를 막지 않는다
        /// (백업은 안전망이지 진행 조건이 아니다).
        /// </summary>
        /// <param name="overwrite">
        /// false면 같은 이름의 백업이 이미 있을 때 건너뛴다 — 버전 태그 백업은 <b>먼저 남긴 것이
        /// 더 온전하다</b>(두 번째 실행 시점의 파일은 이미 현재 코드가 덮어쓴 뒤일 수 있다).
        /// 손상 백업은 타임스탬프라 이름이 겹치지 않으므로 true를 쓴다.
        /// </param>
        private static string BackupSaveFile(string sourcePath, string tag, bool overwrite)
        {
            try
            {
                string dir = Path.GetDirectoryName(sourcePath);
                string stem = Path.GetFileNameWithoutExtension(sourcePath);
                string backupPath = Path.Combine(dir ?? string.Empty, $"{stem}.{tag}.bak");

                if (!overwrite && File.Exists(backupPath)) return backupPath;

                File.Copy(sourcePath, backupPath, overwrite: true);
                Debug.Log($"[MetaSaveService] 세이브 백업 생성: {backupPath}");
                return backupPath;
            }
            catch (Exception e)
            {
                Debug.LogError($"[MetaSaveService] 세이브 백업 실패: {e.Message}");
                return null;
            }
        }

        private static void NormalizeLists(MetaSave save)
        {
            save.unlockedFormIds ??= new List<string>();
            save.unlockedSkillIds ??= new List<string>();
            save.discoveredFormIds ??= new List<string>();
            save.discoveredSkillIds ??= new List<string>();
            save.discoveredEnemyIds ??= new List<string>();
            save.upgradeLevels ??= new List<MetaUpgradeEntry>();
            save.storyProgress ??= new List<StoryProgressEntry>();
            // 항목이 참조 타입이라 안쪽 리스트도 null일 수 있다 — 바깥만 채우면
            // 시청 기록을 Add하는 순간 NullReference로 터진다.
            foreach (var progress in save.storyProgress)
            {
                if (progress != null) progress.viewedChapterStages ??= new List<int>();
            }
            save.records ??= new MetaRecords();
            save.lastRunSummary ??= new RunSummary();
            save.lastRunSummary.formsUsed ??= new List<string>();
            save.lastRunSummary.draftedSkillIds ??= new List<string>();
            save.settings ??= new MetaSettings();
        }
    }
}
