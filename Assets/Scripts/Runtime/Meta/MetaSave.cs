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

        // ── 도감 발견 목록 (완주 루프 계획 3-1) ──
        // unlocked* 를 재사용하지 않는다: 그쪽은 "플레이에 쓸 수 있다"(해금)이고 이쪽은 "만난 적 있다"(발견)로
        // 의미가 다르다. 한 목록에 섞으면 도감을 채우는 것이 폼 해금이 되거나, 해금 폼이 도감에서 이미
        // 발견 처리되는 식으로 두 규칙이 서로를 오염시킨다.
        // 기존 세이브에는 이 필드들이 없지만 JsonUtility가 생성자 기본값(빈 리스트)을 유지하므로
        // 마이그레이션이 필요 없다(records.hasSeenEnding 선례).

        /// <summary>도감에 발견 등록된 폼 ID 목록. formId 기준.</summary>
        public List<string> discoveredFormIds = new();

        /// <summary>도감에 발견 등록된 스킬 ID 목록. skillId 기준.</summary>
        public List<string> discoveredSkillIds = new();

        /// <summary>도감에 발견 등록된 적·보스 ID 목록. enemyId 기준(보스도 EnemyData라 한 목록을 공유).</summary>
        public List<string> discoveredEnemyIds = new();

        /// <summary>메타 화폐 누적 총량. 런 종료 시 `abyssShardsConversionRate` 환산분 추가.</summary>
        public int abyssShardsTotal;

        /// <summary>심연의 제단 영구 업그레이드 레벨 목록. upgradeId 기준.</summary>
        public List<MetaUpgradeEntry> upgradeLevels = new();

        /// <summary>마지막으로 시청한 스토리 챕터 단계(0 = 미시청). 서사 NPC가 다음 챕터 해금에 사용.</summary>
        public int storyStage;

        /// <summary>마지막 챕터 시청 시점의 누적 런 수 스냅샷. 다음 챕터는 이 이후 추가 진전으로 해금.</summary>
        public int storyRunSnapshot;

        /// <summary>마지막 챕터 시청 시점의 누적 보스 처치 스냅샷.</summary>
        public int storyBossSnapshot;

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
    /// 메타 업그레이드 1건의 저장 항목. JsonUtility가 Dictionary를 직렬화하지 못하므로
    /// List&lt;MetaUpgradeEntry&gt;로 upgradeId→level 매핑을 보관한다.
    /// </summary>
    [Serializable]
    public struct MetaUpgradeEntry
    {
        public string upgradeId;
        public int level;
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

        /// <summary>
        /// 엔딩(크레딧)을 끝까지 또는 건너뛰기로 도달했는가. 완주 루프 계획 2-3의 "엔딩을 봤는가".
        /// 기존 세이브에는 이 필드가 없지만 JsonUtility가 기본값 false로 채우므로 마이그레이션이 필요 없다.
        /// </summary>
        public bool hasSeenEnding;
    }

    /// <summary>
    /// 영속 설정(볼륨·화면). SettingsPanel이 읽고 쓴다.
    ///
    /// MetaSave 안에 두는 이유: 별도 파일로 분리하면 스키마 마이그레이션과 기존
    /// UpdateSettings API 폐기 비용이 드는데, 얻는 것은 "세이브 삭제 시 설정 보존" 하나뿐이다.
    /// 그 하나는 MetaSaveService.ResetAll이 설정만 이월하는 것으로 국소 해결한다.
    ///
    /// 해상도는 인덱스가 아니라 실제 값으로 저장한다 — 인덱스는 기기·드라이버마다
    /// 목록이 달라 다른 환경에서 엉뚱한 해상도로 복원된다.
    /// </summary>
    [Serializable]
    public sealed class MetaSettings
    {
        // 기본값은 AudioManager의 기존 기본값과 일치시킨다 — 어긋나면 첫 실행 볼륨이 달라진다.
        public float masterVolume = 0.7f;
        public float bgmVolume = 0.7f;
        public float sfxVolume = 0.8f;

        // 0이면 "미설정" — 최초 실행 시 현재 화면 설정을 그대로 쓰고 덮어쓰지 않는다.
        public int screenWidth;
        public int screenHeight;
        public bool isFullscreen = true;
    }
}
