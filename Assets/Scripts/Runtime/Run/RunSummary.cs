using System;
using System.Collections.Generic;

namespace Abyss.Runtime.Run
{
    /// <summary>
    /// 끝난 런 1회의 결과 요약. 결과 화면·엔딩 통계·도감 기록 탭이 공유하는 표시용 값 타입이다.
    ///
    /// <see cref="RunStats"/>와 나눈 이유는 <b>수명이 다르기 때문</b>이다. RunStats는 진행 중인 런의
    /// 누적기라 매 프레임 자란다. 이쪽은 런이 끝난 순간에 확정돼 다음 런이 시작돼도 남아야 한다.
    ///
    /// <b>JsonUtility로 직렬화된다</b> — 그래서 RunStats를 그대로 저장할 수 없었다.
    /// <c>formPlaytimeSeconds</c>가 <c>Dictionary</c>라 직렬화 대상이 아니기 때문인데,
    /// 정작 화면이 필요로 하는 것은 사전 전체가 아니라 <b>거기서 뽑은 주 사용 폼과 비율</b>뿐이다.
    /// 파생값만 담기로 하자 직렬화 문제가 사라졌다 — 담을 수 없는 자료구조를 담으려 애쓸 게 아니라
    /// 정말 필요한 것이 무엇인지 다시 볼 일이었다.
    /// </summary>
    [Serializable]
    public sealed class RunSummary
    {
        /// <summary>
        /// 실제로 기록된 런인지. 첫 실행처럼 아직 런을 끝낸 적이 없으면 false다.
        ///
        /// 값으로 판별하지 않는 이유: 모든 수치가 0인 요약은 "기록 없음"과 "0킬로 즉사한 런"을
        /// 구분하지 못한다. 후자도 엄연한 기록이므로 별도 플래그로 나눈다.
        /// 기존 세이브에는 이 필드가 없지만 JsonUtility가 기본값 false를 유지하므로
        /// 마이그레이션이 필요 없다(<c>records.hasSeenEnding</c> 선례).
        /// </summary>
        public bool hasRecord;

        public int enemiesKilled;
        public int maxCombo;

        /// <summary>가장 오래 사용한 폼 ID. 없으면 빈 문자열.</summary>
        public string dominantFormId = string.Empty;

        /// <summary>주 사용 폼의 점유 비율(0~1). 런 총 경과시간 대비 — <see cref="RunStats.GetFormRatio"/>와 같은 분모.</summary>
        public float dominantFormRatio;

        public List<string> formsUsed = new();
        public List<string> draftedSkillIds = new();

        /// <summary>마지막으로 들어간 방의 roomId. 진행 깊이는 <see cref="reached"/>가 쥔다.</summary>
        public string stageReached = string.Empty;

        /// <summary>
        /// 이 런이 도달한 가장 깊은 지점. 이 필드가 없던 옛 요약은 JsonUtility가 0으로 채우고,
        /// 0은 곧 "기록 없음"이라 <see cref="UI.RunSummaryText.Stage"/>가 roomId 표기로 되돌아간다
        /// — 옛 기록이 사라지지 않고, 마이그레이션도 필요 없다.
        /// </summary>
        public StageReach reached;

        public float elapsedSeconds;

        /// <summary>이 런으로 획득한 심연 조각. 정산 시점 값이라 나중에 조회하면 달라진다.</summary>
        public int abyssEarned;

        /// <summary>정산 직후의 누적 심연 조각. 화면이 "이번 +N (누적 M)"을 그대로 보여줄 수 있게 함께 굳힌다.</summary>
        public int abyssTotal;
    }
}
