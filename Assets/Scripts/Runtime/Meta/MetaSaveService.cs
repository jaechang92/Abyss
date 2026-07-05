using System.Collections.Generic;
using SaveSystem_Core;
using Singleton_Core;
using UnityEngine;

namespace Abyss.Runtime.Meta
{
    /// <summary>
    /// MetaSave 읽기/쓰기 게이트웨이. SaveSystem을 경유하고 상위 시스템은 이 API만 사용.
    /// AbyssBootstrap에서 SaveSystem 이후 RunManager 이전에 초기화해야 RunManager.EndRun 정산이 정상 동작.
    /// </summary>
    public sealed class MetaSaveService : SingletonManager<MetaSaveService>
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

        /// <summary>
        /// 런 기록 갱신. 각 필드 최대값 유지 전략. autoSave=true 시 즉시 디스크 반영.
        /// RunManager.EndRun에서 RunStats/GoldShards 최종값과 함께 호출.
        /// </summary>
        public void RecordRunResult(string reachedStageId, float runDurationSeconds, int goldShards, int bossKillDelta, bool autoSave = true)
        {
            EnsureLoaded();
            var r = current.records;
            r.totalRunCount += 1;
            r.totalBossKillCount += Mathf.Max(0, bossKillDelta);
            if (!string.IsNullOrEmpty(reachedStageId)) r.bestStageId = reachedStageId;
            if (runDurationSeconds > r.bestRunDurationSeconds) r.bestRunDurationSeconds = runDurationSeconds;
            if (goldShards > r.bestGoldShards) r.bestGoldShards = goldShards;
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

        /// <summary>마지막으로 시청한 스토리 챕터 단계(0 = 미시청).</summary>
        public int StoryStage
        {
            get
            {
                EnsureLoaded();
                return current.storyStage;
            }
        }

        /// <summary>
        /// 스토리 진행 단계를 전진시키고, 시청 시점의 런/보스 누적 스냅샷을 기록한다.
        /// 다음 챕터는 이 스냅샷 이후의 추가 진전으로 해금된다. 현재 단계 이하 값은 무시(후퇴 방지).
        /// </summary>
        public void AdvanceStory(int stage, int runSnapshot, int bossSnapshot, bool autoSave = true)
        {
            EnsureLoaded();
            if (stage <= current.storyStage) return;
            current.storyStage = stage;
            current.storyRunSnapshot = runSnapshot;
            current.storyBossSnapshot = bossSnapshot;
            if (autoSave) Save();
        }

        /// <summary>
        /// 디버그/치트: 스토리 단계를 임의 설정(후퇴 포함). 스냅샷은 0으로 리셋해
        /// 델타 조건을 누적 기준으로 만든다(치트로 다음 챕터를 곧바로 열람하기 위함).
        /// </summary>
        public void DebugSetStoryStage(int stage, bool autoSave = true)
        {
            EnsureLoaded();
            current.storyStage = Mathf.Max(0, stage);
            current.storyRunSnapshot = 0;
            current.storyBossSnapshot = 0;
            if (autoSave) Save();
        }

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
        /// 디버그 리셋. 확인 다이얼로그는 호출자 책임.
        /// </summary>
        public void ResetAll(bool autoSave = true)
        {
            current = new MetaSave();
            isLoaded = true;
            if (autoSave) Save();
        }

        /// <summary>
        /// 디버그: 해제 상태 로그.
        /// </summary>
        [ContextMenu("Debug: Log Meta Summary")]
        private void DebugLogSummary()
        {
            EnsureLoaded();
            Debug.Log($"[MetaSaveService] abyss={current.abyssShardsTotal} forms={string.Join(",", current.unlockedFormIds)} skills={current.unlockedSkillIds.Count}개 runs={current.records.totalRunCount}");
        }

        private void EnsureLoaded()
        {
            if (isLoaded && current != null) return;

            var path = SaveSystem.Instance.GetFilePath(MetaSave.FileName);
            if (System.IO.File.Exists(path))
            {
                current = SaveSystem.Instance.Load<MetaSave>(MetaSave.FileName);
                if (current == null)
                {
                    Debug.LogWarning("[MetaSaveService] 기존 메타 파일 로드 실패 → 빈 인스턴스로 폴백");
                    current = new MetaSave();
                }
            }
            else
            {
                current = new MetaSave();
                Debug.Log("[MetaSaveService] 신규 MetaSave 생성");
            }

            NormalizeLists(current);
            isLoaded = true;
        }

        private static void NormalizeLists(MetaSave save)
        {
            save.unlockedFormIds ??= new List<string>();
            save.unlockedSkillIds ??= new List<string>();
            save.upgradeLevels ??= new List<MetaUpgradeEntry>();
            save.records ??= new MetaRecords();
            save.settings ??= new MetaSettings();
        }
    }
}
