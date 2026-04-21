using System;
using System.Collections.Generic;
using SaveSystem_Core;

namespace Abyss.Runtime.Meta
{
    /// <summary>
    /// 런 영속 메타 데이터. SaveSystem.Save/Load 경유해 `abyss_meta.json` 단일 파일로 저장.
    /// 프로토 범위(P-21)는 뼈대만 — 해제 시스템·메타 상점은 EA에서 구현.
    /// </summary>
    [Serializable]
    public sealed class MetaSave : SaveDataBase
    {
        public const string FileName = "abyss_meta.json";

        /// <summary>해제된 폼 ID 목록. formId 기준.</summary>
        public List<string> unlockedFormIds = new();

        /// <summary>해제된 스킬 ID 목록. skillId 기준.</summary>
        public List<string> unlockedSkillIds = new();

        /// <summary>메타 화폐 누적 총량. 런 종료 시 `abyssShardsConversionRate` 환산분 추가.</summary>
        public int abyssShardsTotal;

        /// <summary>런 기록(최고 스테이지·최장 런 등).</summary>
        public MetaRecords records = new();

        /// <summary>영속 설정(볼륨 등). UI 옵션 창 연결 예정.</summary>
        public MetaSettings settings = new();

        /// <summary>
        /// 직렬화 버전 갱신 + 저장 시간 스탬프.
        /// 호출 시 `version = 1`, 필드 추가 시 마이그레이션 경로 필요.
        /// </summary>
        public void Touch()
        {
            UpdateSaveTime();
            if (version <= 0) version = 1;
        }
    }

    /// <summary>
    /// 런 간 누적 기록. 프로토 범위(P-21)는 최소 2필드, 확장은 MF-6 리더보드 연계 시 추가.
    /// </summary>
    [Serializable]
    public sealed class MetaRecords
    {
        /// <summary>전체 런 시도 횟수.</summary>
        public int totalRunCount;

        /// <summary>전체 보스 처치 횟수.</summary>
        public int totalBossKillCount;

        /// <summary>최고 도달 스테이지 ID(roomId 기준).</summary>
        public string bestStageId = string.Empty;

        /// <summary>최장 런 경과 시간(unscaled seconds).</summary>
        public float bestRunDurationSeconds;

        /// <summary>최고 획득 goldShards(런 내 최대치).</summary>
        public int bestGoldShards;
    }

    /// <summary>
    /// 영속 설정(볼륨 등). 실제 UI 연결은 P-22 이후.
    /// </summary>
    [Serializable]
    public sealed class MetaSettings
    {
        public float masterVolume = 1f;
        public float bgmVolume = 1f;
        public float sfxVolume = 1f;
    }
}
