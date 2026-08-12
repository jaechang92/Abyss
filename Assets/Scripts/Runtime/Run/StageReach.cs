using System;

namespace Abyss.Runtime.Run
{
    /// <summary>
    /// "런이 어디까지 내려갔는가"의 값 타입. 런 중 누적(<see cref="RunStats.reached"/>),
    /// 끝난 런의 요약(<see cref="RunSummary.reached"/>), 역대 최고 기록(<c>MetaRecords.bestReach</c>)이
    /// 모두 이 타입을 쓴다 — 세 곳이 각자 표현을 두면 "최고"와 "직전"을 비교할 방법이 사라진다.
    ///
    /// <b>번호는 1부터다(0 = 기록 없음).</b> 0-based 인덱스를 그대로 저장하면 안 되는 이유는
    /// JsonUtility가 <b>키가 없는 필드를 0으로 채우기</b> 때문이다 — 이 필드를 모르는 옛 세이브가
    /// 곧바로 "스테이지 1 도달"로 둔갑한다. 저장되는 값의 '없음'은 반드시 0이어야 하고,
    /// 그래서 경계(<c>StageDirector</c>)에서 인덱스 +1로 바꿔 넣는다.
    ///
    /// <b>단계 번호는 없을 수 있다</b>(0 = 미상). v1 세이브에서 복원한 기록이 그렇다 — 옛 값은
    /// roomId 하나뿐이었고 방 번호는 단계 번호가 아니다(Stage1은 방 번호가 6까지인데 단계는 9개다).
    /// </summary>
    [Serializable]
    public struct StageReach
    {
        /// <summary>도달 스테이지 번호(1부터). 0이면 기록 없음.</summary>
        public int stageNumber;

        /// <summary>스테이지 안에서 도달한 단계 번호(1부터). 0이면 미상.</summary>
        public int stepNumber;

        /// <summary>
        /// 그 스테이지의 표시명 스냅샷(예: "왕좌의 잔해"). 비어 있으면 번호로 표시한다.
        ///
        /// 인덱스만 저장하고 표시명은 나중에 조회하는 안을 쓰지 않았다 — 도감은 <b>타이틀 씬에서도</b>
        /// 열리는데 그 씬에는 스테이지 시퀀스가 로드되지 않는다(적 SO를 Resources로 옮기게 만든 것과
        /// 같은 제약이다). 굳혀 두면 조회가 아예 필요 없고, 나중에 스테이지 이름이 바뀌어도
        /// 기록은 그때 불린 이름을 유지한다.
        /// </summary>
        public string stageName;

        /// <summary>실제 도달 기록이 있는가.</summary>
        public bool HasRecord => stageNumber > 0;

        public static StageReach At(int stageNumber, int stepNumber, string stageName) => new()
        {
            stageNumber = Math.Max(0, stageNumber),
            stepNumber = Math.Max(0, stepNumber),
            stageName = stageName ?? string.Empty
        };

        /// <summary>
        /// 이 도달이 <paramref name="other"/>보다 깊은가. 스테이지가 먼저이고, 같으면 단계로 가른다.
        /// 기록이 없는 쪽(<c>stageNumber == 0</c>)은 어떤 기록보다도 얕다.
        /// </summary>
        public bool IsDeeperThan(StageReach other)
        {
            if (stageNumber != other.stageNumber) return stageNumber > other.stageNumber;
            return stepNumber > other.stepNumber;
        }

        /// <summary>
        /// 화면 표기. 도감 기록 탭과 런 요약이 같은 문구를 쓴다 — 두 화면이 같은 기록을 다르게
        /// 부르면 어느 쪽이 맞는지 판단할 근거가 없다.
        /// </summary>
        public string Describe(string emptyValue)
        {
            if (!HasRecord) return emptyValue;

            string stage = string.IsNullOrEmpty(stageName) ? $"스테이지 {stageNumber}" : stageName;
            return stepNumber > 0 ? $"{stage} {stepNumber}단계" : stage;
        }
    }
}
