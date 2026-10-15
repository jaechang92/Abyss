using System;
using Abyss.Runtime.Run;
using SaveSystem_Core;
using UnityEngine;

namespace Abyss.Runtime.Meta
{
    /// <summary>
    /// 사용자가 고르는 데이터 초기화 묶음. 7개 묶음은 서로 겹치지 않으며, <see cref="All"/>은
    /// 7개를 모두 고른 것과 같고 <b>세이브 전체를 새 세이브로 바꾼다</b>(목록에 없는 필드·향후 필드 포함).
    /// </summary>
    [Flags]
    public enum MetaResetCategory
    {
        None = 0,
        /// <summary>보유 유물 + 장착 슬롯. 심연 조각은 건드리지 않는다.</summary>
        Relics = 1 << 0,
        /// <summary>심연 조각(abyssShardsTotal)만.</summary>
        Currency = 1 << 1,
        /// <summary>심연의 제단 영구 강화 레벨만.</summary>
        PermanentUpgrades = 1 << 2,
        /// <summary>해금한 폼·스킬만.</summary>
        Unlocks = 1 << 3,
        /// <summary>도감 발견(폼·스킬·적) + 직전 런 요약 + 런 기록. 프롤로그·엔딩 시청 여부는 이야기 묶음 몫이라 유지한다.</summary>
        CodexAndRunRecords = 1 << 4,
        /// <summary>화자별 이야기 진행·레거시 이야기 필드·통행 기록 발견/반응·첫 플레이 튜토리얼·프롤로그/엔딩 시청 여부만.</summary>
        StoryAndTutorial = 1 << 5,
        /// <summary>설정 전체(새 기본값).</summary>
        Settings = 1 << 6,
        All = Relics | Currency | PermanentUpgrades | Unlocks | CodexAndRunRecords | StoryAndTutorial | Settings
    }

    /// <summary><see cref="MetaSaveService.TryResetData"/>의 거절·실패 이유.</summary>
    public enum MetaResetError
    {
        None,
        /// <summary>아무것도 고르지 않았거나 모르는 플래그가 섞였다.</summary>
        InvalidSelection,
        /// <summary>런 진행 중이다.</summary>
        RunActive,
        /// <summary>세이브를 읽을 수 없거나 초기화 후보를 만들지 못했다.</summary>
        Unavailable,
        /// <summary>세이브를 열지 못해 저장이 보류 중이다(IsSaveBlocked). 보류는 풀지 않는다.</summary>
        SaveBlocked,
        /// <summary>디스크 쓰기가 실패했다. 메모리·디스크 모두 초기화 전 그대로다.</summary>
        SaveFailed
    }

    /// <summary>
    /// <see cref="MetaSaveService"/>의 <b>선택 초기화</b> 부분.
    ///
    /// 기존 <see cref="ResetAll"/>(설정 이월·보류 해제 계약)은 그대로 두고 따로 둔다 — 그쪽은 디버그·치트 경로이고
    /// 이쪽은 사용자 확인을 거친 영구 삭제라 거절 조건(런 진행·저장 보류)과 실패 시 되돌림이 다르다.
    ///
    /// 🔑 <b>현재 객체를 고치지 않고 후보를 만든다.</b> JSON 깊은 복제로 후보를 만들고, 고른 필드만 새 세이브 값으로
    /// 바꾼 뒤 기존 저장 경로(<see cref="Save"/>, 임시 파일 → 교체)로 <b>한 번</b> 쓴다. 성공하면 후보가 Current가 되고,
    /// 실패·예외면 옛 객체를 그대로 되돌린다 — 옛 객체는 한 번도 변형되지 않았으므로 열린 UI가 쥔 목록 참조도 안전하다.
    /// </summary>
    public sealed partial class MetaSaveService
    {
        /// <summary>비어 있지 않고 알려진 플래그만 담았는가.</summary>
        public static bool IsValidResetSelection(MetaResetCategory categories) =>
            categories != MetaResetCategory.None && (categories & ~MetaResetCategory.All) == 0;

        /// <summary>
        /// 고른 묶음을 처음 상태로 되돌리고 한 번 저장한다. 거절·실패면 false이고 메모리·디스크 모두 그대로다.
        /// 장면(타이틀·로비) 판정은 호출하는 UI 몫이다 — 서비스는 런 진행과 저장 보류만 막는다.
        /// </summary>
        public bool TryResetData(MetaResetCategory categories, out MetaResetError error)
        {
            if (!IsValidResetSelection(categories))
            {
                error = MetaResetError.InvalidSelection;
                return false;
            }
            // GetInstanceSafe — 확인하려고 RunManager를 만들지 않는다.
            var run = RunManager.GetInstanceSafe();
            if (run != null && run.IsRunActive)
            {
                error = MetaResetError.RunActive;
                return false;
            }

            EnsureLoaded();
            if (current == null)
            {
                error = MetaResetError.Unavailable;
                return false;
            }
            if (IsSaveBlocked)
            {
                error = MetaResetError.SaveBlocked;
                return false;
            }

            MetaSave candidate;
            try
            {
                candidate = CreateResetCandidate(current, categories);
            }
            catch (Exception e)
            {
                Debug.LogError($"[MetaSaveService] 초기화 후보를 만들지 못했다 — 아무것도 바꾸지 않는다: {e.Message}");
                error = MetaResetError.Unavailable;
                return false;
            }

            var previous = current;
            bool wasLoaded = isLoaded;
            var previousResult = LastLoadResult;
            bool previousUsedBackup = LastLoadUsedBackup;
            bool isSaved = false;
            try
            {
                // 저장 이벤트 구독자는 후보를 본다 — 저장되는 것이 후보이기 때문이다.
                current = candidate;
                isSaved = Save();
            }
            catch (Exception e)
            {
                // SaveSystem은 본 파일 교체를 끝낸 뒤 try 밖에서 OnSaved를 알린다 — 구독자 예외는 「쓰지 못했다」가 아니다.
                // 디스크가 이미 후보라면 옛 값으로 되돌리면 메모리와 디스크가 갈라지므로 후보를 유지한다.
                // 옛 세이브를 다시 써서 되돌리지 않는다(저장은 한 번뿐이어야 한다).
                isSaved = IsCandidateOnDisk(candidate);
                if (isSaved)
                    Debug.LogWarning($"[MetaSaveService] 데이터 초기화는 디스크에 반영됐지만 저장 알림 구독자가 예외를 냈다 — 초기화 결과를 유지한다: {e.GetType().Name}: {e.Message}");
                else
                    Debug.LogError($"[MetaSaveService] 데이터 초기화 저장 중 예외 — 초기화 전 상태로 되돌린다: {e.Message}");
            }
            finally
            {
                if (!isSaved)
                {
                    current = previous;
                    isLoaded = wasLoaded;
                    LastLoadResult = previousResult;
                    LastLoadUsedBackup = previousUsedBackup;
                }
            }

            error = isSaved ? MetaResetError.None : MetaResetError.SaveFailed;
            if (isSaved) Debug.Log($"[MetaSaveService] 데이터 초기화 완료: {categories}");
            return isSaved;
        }

        /// <summary>
        /// 초기화 후보를 만든다. <paramref name="source"/>는 변형하지 않는다(읽기만).
        /// <see cref="MetaResetCategory.All"/>은 완전히 새 세이브(현재 버전, 설정 기본값)를 돌려준다.
        /// 부분 초기화는 고르지 않은 필드가 JSON 값 기준으로 원본과 같고, 원본과 참조를 공유하지 않는다.
        /// </summary>
        public static MetaSave CreateResetCandidate(MetaSave source, MetaResetCategory categories)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (!IsValidResetSelection(categories)) throw new ArgumentException($"잘못된 초기화 선택: {categories}", nameof(categories));

            var fresh = MetaSave.CreateNew();
            if (categories == MetaResetCategory.All) return fresh;

            var candidate = JsonUtility.FromJson<MetaSave>(JsonUtility.ToJson(source));
            if (candidate == null) throw new InvalidOperationException("세이브를 복제하지 못했다.");
            NormalizeLists(candidate);
            candidate.relics ??= fresh.relics;
            candidate.equippedRelicIds ??= fresh.equippedRelicIds;

            if (Has(categories, MetaResetCategory.Relics))
            {
                candidate.relics = fresh.relics;
                candidate.equippedRelicIds = fresh.equippedRelicIds;
            }
            if (Has(categories, MetaResetCategory.Currency)) candidate.abyssShardsTotal = fresh.abyssShardsTotal;
            if (Has(categories, MetaResetCategory.PermanentUpgrades)) candidate.upgradeLevels = fresh.upgradeLevels;
            if (Has(categories, MetaResetCategory.Unlocks))
            {
                candidate.unlockedFormIds = fresh.unlockedFormIds;
                candidate.unlockedSkillIds = fresh.unlockedSkillIds;
            }
            // 이야기 묶음보다 먼저 — 둘 다 골랐으면 아래에서 프롤로그·엔딩 시청 여부까지 새 값이 된다.
            if (Has(categories, MetaResetCategory.CodexAndRunRecords))
            {
                candidate.discoveredFormIds = fresh.discoveredFormIds;
                candidate.discoveredSkillIds = fresh.discoveredSkillIds;
                candidate.discoveredEnemyIds = fresh.discoveredEnemyIds;
                candidate.lastRunSummary = fresh.lastRunSummary;
                // 시청 기록(프롤로그·엔딩)은 이야기 묶음 소속이라 남긴다.
                bool hasSeenPrologue = candidate.records.hasSeenPrologue;
                bool hasSeenEnding = candidate.records.hasSeenEnding;
                candidate.records = fresh.records;
                // Both selections share fresh.records here. Do not restore viewing flags into that
                // fresh object when the story group must clear them immediately below.
                if (!Has(categories, MetaResetCategory.StoryAndTutorial))
                {
                    candidate.records.hasSeenPrologue = hasSeenPrologue;
                    candidate.records.hasSeenEnding = hasSeenEnding;
                }
            }
            if (Has(categories, MetaResetCategory.StoryAndTutorial))
            {
                candidate.storyProgress = fresh.storyProgress;
                candidate.storyStage = fresh.storyStage;
                candidate.storyRunSnapshot = fresh.storyRunSnapshot;
                candidate.storyBossSnapshot = fresh.storyBossSnapshot;
                candidate.discoveredPassageIds = fresh.discoveredPassageIds;
                candidate.viewedPassageReactionIds = fresh.viewedPassageReactionIds;
                candidate.hasCompletedFirstPlayTutorial = fresh.hasCompletedFirstPlayTutorial;
                candidate.hasSkippedFirstPlayTutorial = fresh.hasSkippedFirstPlayTutorial;
                candidate.records.hasSeenPrologue = fresh.records.hasSeenPrologue;
                candidate.records.hasSeenEnding = fresh.records.hasSeenEnding;
            }
            if (Has(categories, MetaResetCategory.Settings)) candidate.settings = fresh.settings;
            return candidate;
        }

        private static bool Has(MetaResetCategory categories, MetaResetCategory flag) => (categories & flag) == flag;

        /// <summary>
        /// 본 파일이 저장된 후보와 JSON 값이 같은가. SaveSystem.Load·LoadWithBackup은 이벤트를 내고 상태를 갱신하므로 쓰지 않고,
        /// 그 내부 읽기(private TryReadFile)가 쓰는 같은 파일 조작으로 한 번만 읽는다 — 이벤트·메타데이터 변경·쓰기 없음.
        /// 읽기·해석 중 어떤 예외든 「확인 불가 = 반영되지 않음」으로 본다.
        /// </summary>
        private static bool IsCandidateOnDisk(MetaSave candidate)
        {
            try
            {
                if (candidate == null || !SaveSystem.HasInstance) return false;
                var saveSystem = SaveSystem.Instance;
                string json = saveSystem.FileOperations.ReadAllText(saveSystem.GetFilePath(MetaSave.FileName));
                if (string.IsNullOrWhiteSpace(json)) return false;
                var disk = JsonUtility.FromJson<MetaSave>(json);
                return disk != null && JsonUtility.ToJson(disk) == JsonUtility.ToJson(candidate);
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
