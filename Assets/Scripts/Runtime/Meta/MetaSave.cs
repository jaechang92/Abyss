using System;
using System.Collections.Generic;
using Abyss.Runtime.Run;
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

        /// <summary>
        /// 현재 코드가 읽고 쓰는 스키마 버전. 스키마 SoT — 이 값과 <see cref="MetaSaveMigration"/>의
        /// 단계표는 항상 함께 움직인다(버전을 N으로 올리면 N-1 → N 단계가 반드시 있어야 한다).
        ///
        /// <b>올려야 할 때</b>: 필드 제거·개명, 타입/단위/의미 변경 등 기존 값을 그대로 읽으면
        /// 틀리게 되는 변경. <b>올리지 않아도 되는 때</b>: 순수 필드 추가 — JsonUtility가 없는 필드를
        /// 생성자 기본값으로 남기므로 기본값이 곧 기존 동작이면 변환할 것이 없다.
        /// </summary>
        public const int CurrentVersion = 2;

        /// <summary>
        /// 취급 가능한 가장 낮은 스키마 버전. JSON에 `version`이 없거나 0·음수인 파일은
        /// 이 버전으로 간주해 마이그레이션 출발점으로 삼는다.
        ///
        /// ⚠️ <b><see cref="SaveDataBase.version"/>의 필드 초기값과 같아야 한다.</b> 그 초기값이
        /// 곧 "version 키가 없는 JSON을 읽었을 때의 값"이기 때문이다. SaveDataBase는 범용 코어라
        /// 이 상수를 참조할 수 없어 결합이 암묵적이다 —
        /// `MetaSaveMigrationTests.DefaultConstructor_StaysAtMinimumVersion`이 어긋남을 잡는다.
        /// </summary>
        public const int MinimumVersion = 1;

        /// <summary>
        /// 새 세이브 인스턴스 생성. <b>현재 버전을 찍는 유일한 지점</b>이다.
        ///
        /// `new MetaSave()`가 아니라 팩토리를 두는 이유: 생성자에서 <see cref="CurrentVersion"/>을
        /// 찍으면 JsonUtility가 역직렬화할 때도 그 값이 먼저 들어가, `version` 키가 <b>없는</b> 낡은
        /// 파일이 "이미 최신"으로 위장한다. 필드 초기값은 <see cref="MinimumVersion"/>으로 두고
        /// (= 버전 표기가 없으면 가장 오래된 스키마), 진짜 새 파일만 여기서 현재 버전으로 올린다.
        /// </summary>
        public static MetaSave CreateNew() => new() { version = CurrentVersion };

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

        /// <summary>
        /// 직전에 끝난 런의 8줄 요약. 도감 기록 탭이 보여준다.
        ///
        /// <see cref="records"/>(최고 기록의 누적)와 별개다 — 저쪽은 "역대 최고"라 갱신될 때만 바뀌고,
        /// 이쪽은 "방금 무슨 일이 있었나"라 매 런 덮인다. 한 곳에 섞으면 최고 기록이 직전 런으로
        /// 내려앉거나 그 반대가 된다.
        /// 기존 세이브에는 이 필드가 없지만 JsonUtility가 생성자 기본값을 유지하므로 마이그레이션이 필요 없다.
        /// </summary>
        public RunSummary lastRunSummary = new();

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
        /// 저장 시간 스탬프 + 버전 하한 보정.
        ///
        /// <b>여기서 <see cref="CurrentVersion"/>을 찍지 않는다.</b> 버전을 올리는 권한은
        /// <see cref="CreateNew"/>(새 파일)와 <see cref="MetaSaveMigration"/>(변환 완료)만 갖는다.
        /// 저장할 때마다 현재 버전을 찍으면, 변환에 실패한 세이브가 "변환된 척" 기록되어
        /// 다음 로드에서 마이그레이션을 건너뛴다.
        /// </summary>
        public void Touch()
        {
            UpdateSaveTime();
            if (version < MinimumVersion) version = MinimumVersion;
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

        /// <summary>
        /// 역대 최고 도달 지점. <b>비교해서 더 깊을 때만</b> 갱신된다
        /// (<see cref="MetaSaveService.RecordRunResult"/>).
        ///
        /// 옛 <see cref="bestStageId"/>를 대체한다 — 저쪽은 이름과 달리 "직전 런의 최종 도달"이었고,
        /// 담기는 값도 스테이지가 아니라 roomId였다. 그래서 스테이지 3까지 갔다가 다음 런에서
        /// 1스테이지에 죽으면 화면의 "최고 도달"이 뒷걸음질쳤다.
        /// </summary>
        public StageReach bestReach;

        /// <summary>
        /// ⚠️ <b>v1 레거시 — 읽지 말 것.</b> 새 코드는 <see cref="bestReach"/>만 쓴다.
        ///
        /// 그런데도 필드를 남긴 이유는 <b>마이그레이션의 입력이기 때문</b>이다. JsonUtility는
        /// 클래스에 없는 JSON 키를 그냥 버리므로, 필드를 지우는 순간 값은 변환기가 객체를 받기
        /// <b>전에</b> 사라진다 — 즉 이 직렬화 방식에서 "필드 제거"와 "값 이전"은 같은 버전에
        /// 담을 수 없다. v1 → v2 변환이 이 값을 <see cref="bestReach"/>로 옮기고 빈 문자열로 비운다.
        /// 실제 삭제는 v2 세이브만 남았다고 볼 수 있는 시점에 별도 버전으로 한다.
        /// </summary>
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
