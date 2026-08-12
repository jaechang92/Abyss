using System.Collections.Generic;
using UnityEngine;

namespace Abyss.Runtime.Meta
{
    /// <summary>
    /// 마이그레이션 실행 결과. 호출자(<see cref="MetaSaveService"/>)가 백업·저장 여부를 정하는 근거다.
    /// </summary>
    public enum MetaSaveMigrationOutcome
    {
        /// <summary>이미 현재 버전 — 변환할 것이 없다.</summary>
        UpToDate,

        /// <summary>구버전을 현재 버전까지 끌어올렸다. 호출자는 원본을 백업하고 디스크에 다시 써야 한다.</summary>
        Migrated,

        /// <summary>현재 코드보다 높은 버전 — 변환하지 않고 그대로 둔다.</summary>
        FutureVersion,

        /// <summary>중간 단계가 비어 현재 버전에 도달할 수 없다. <b>아무것도 적용하지 않았다.</b></summary>
        Incomplete
    }

    /// <summary>
    /// <see cref="MetaSave"/> 스키마 버전 변환기.
    ///
    /// 세이브 파일에 적힌 <c>version</c>을 <see cref="MetaSave.CurrentVersion"/>까지
    /// <b>한 단계씩</b> 끌어올린다. v1 → v3 직행 변환을 따로 두지 않는 이유는, 버전이 늘어날수록
    /// 조합이 제곱으로 늘고 그중 대부분은 실행된 적 없는 경로가 되기 때문이다. 한 단계씩 이으면
    /// 어떤 출발 버전에서 오든 지나가는 코드가 같다.
    ///
    /// <b>필드를 추가하기만 했다면 여기에 손댈 필요가 없다.</b> JsonUtility가 JSON에 없는 필드를
    /// 생성자 기본값으로 남기므로, 기본값이 곧 기존 동작이면 변환할 것이 없다
    /// (`records.hasSeenEnding`·`discovered*Ids`·`lastRunSummary` 선례).
    /// 이 변환기가 필요한 것은 <b>기존 값을 그대로 읽으면 틀리게 되는</b> 변경이다 —
    /// 필드 제거·개명, 타입·단위·의미 변경.
    /// </summary>
    public static class MetaSaveMigration
    {
        /// <summary>버전 N 세이브를 N+1 스키마로 바꾸는 변환 1건.</summary>
        public delegate void UpgradeStep(MetaSave save);

        /// <summary>
        /// 키 = <b>변환 전</b> 버전, 값 = 그 버전을 +1로 올리는 변환.
        ///
        /// 배열이 아니라 사전인 이유: 순서를 실수로 바꿔도 엉뚱한 단계가 조용히 적용되지 않는다.
        /// 어느 단계가 비면 <see cref="MetaSaveMigrationOutcome.Incomplete"/>로 드러난다.
        /// </summary>
        private static readonly Dictionary<int, UpgradeStep> UpgradeSteps = new()
        {
            // v1이 현재 스키마다 — 아직 단계가 없다.
            //
            // 첫 파괴적 변경이 생기면 (1) 여기에 { 1, UpgradeV1ToV2 } 를 추가하고
            // (2) MetaSave.CurrentVersion 을 2로 올린다. 둘 중 하나만 하면 각각
            // Incomplete(단계 누락) 또는 변환 미실행으로 드러나므로 조용히 어긋나지 않는다.
        };

        /// <summary>
        /// 세이브를 현재 스키마 버전까지 끌어올린다. 기본 단계표를 사용한다.
        /// </summary>
        /// <param name="save">디스크에서 막 읽어들인 인스턴스. 제자리에서 변환된다.</param>
        /// <param name="fileVersion">파일에 적혀 있던 원래 버전(하한 보정 <b>전</b>). 백업 파일명·로그용.</param>
        public static MetaSaveMigrationOutcome Run(MetaSave save, out int fileVersion) =>
            Run(save, MetaSave.CurrentVersion, UpgradeSteps, out fileVersion);

        /// <summary>
        /// 목표 버전과 단계표를 지정해 실행한다.
        ///
        /// 이 오버로드는 테스트를 위한 이음매다. 단계가 아직 0개라 기본 단계표만으로는
        /// <see cref="MetaSaveMigrationOutcome.Migrated"/> 경로가 한 번도 실행되지 않는데,
        /// <b>한 번도 실행된 적 없는 코드는 동작한다고 말할 수 없다.</b>
        /// 실제 단계가 생겼을 때 처음 검증하는 것은 순서가 거꾸로다.
        /// </summary>
        public static MetaSaveMigrationOutcome Run(
            MetaSave save,
            int targetVersion,
            IReadOnlyDictionary<int, UpgradeStep> steps,
            out int fileVersion)
        {
            fileVersion = save != null ? save.version : 0;
            if (save == null) return MetaSaveMigrationOutcome.Incomplete;

            int from = save.version;

            // 버전 표기가 없거나(JsonUtility가 필드 초기값을 남긴 경우) 손상된 값(0·음수)은
            // 가장 오래된 스키마로 간주한다. 예전에는 Touch()가 이 보정을 했는데,
            // 저장 시점에 고치면 '읽을 때 무엇으로 볼 것인가'를 알 수 없다 — 버전 판단은 여기 한 곳에서만 한다.
            if (from < MetaSave.MinimumVersion)
            {
                Debug.LogWarning($"[MetaSaveMigration] 세이브 버전이 {from} — 최초 스키마(v{MetaSave.MinimumVersion})로 간주한다.");
                from = MetaSave.MinimumVersion;
                save.version = from;
            }

            if (from > targetVersion) return MetaSaveMigrationOutcome.FutureVersion;
            if (from == targetVersion) return MetaSaveMigrationOutcome.UpToDate;

            // 적용 전에 전 구간을 먼저 확인한다 — 중간에 멈추면 '절반만 변환된' 상태가 디스크에
            // 남을 수 있고, 그 상태는 어느 버전으로도 설명되지 않아 다음 로드에서 복구할 길이 없다.
            // 여기서 걸러 두면 부분 적용은 아예 존재하지 않는다.
            for (int v = from; v < targetVersion; v++)
            {
                if (steps != null && steps.ContainsKey(v)) continue;

                Debug.LogError(
                    $"[MetaSaveMigration] v{v} → v{v + 1} 변환 단계가 없어 v{from} 세이브를 " +
                    $"v{targetVersion}로 올릴 수 없다. 변환을 적용하지 않고 원본 상태를 유지한다.");
                return MetaSaveMigrationOutcome.Incomplete;
            }

            for (int v = from; v < targetVersion; v++)
            {
                steps[v](save);
                // 단계마다 버전을 올린다 — 중간 단계가 자기 앞 버전을 다시 읽어도 일관되게 보인다.
                save.version = v + 1;
            }

            return MetaSaveMigrationOutcome.Migrated;
        }
    }
}
