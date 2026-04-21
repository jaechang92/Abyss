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
            save.records ??= new MetaRecords();
            save.settings ??= new MetaSettings();
        }
    }
}
