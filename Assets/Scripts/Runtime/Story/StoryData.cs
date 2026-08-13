using System;
using Abyss.Runtime.Dialogue;
using UnityEngine;

namespace Abyss.Runtime.Story
{
    /// <summary>스토리 챕터 1개. 해금 조건 충족 + 미시청 시 서사 NPC가 재생한다.</summary>
    [Serializable]
    public struct StoryChapter
    {
        [Tooltip("이 챕터의 식별 번호(화자 안에서 유일, 1부터 오름차순). 시청 기록에 이 값이 남는다.")]
        public int chapterStage;

        [Tooltip("직전 챕터 시청 이후 추가로 필요한 런 수(델타). 0이면 진전 없이 즉시.")]
        public int minRunCount;

        [Tooltip("직전 챕터 시청 이후 추가로 필요한 보스 처치 수(델타).")]
        public int minBossKills;

        [Tooltip("이 챕터를 열려면 발견(= 런에서 실제로 사용)돼 있어야 할 formId. 비우면 폼 조건 없음(기록자).")]
        public string requiredFormId;

        [Tooltip("챕터 대사(StringKey 참조).")]
        public DialogueLine[] lines;
    }

    /// <summary>
    /// 스토리 화자 1명의 데이터 SO. 진행도(화자별 시청 기록 + records)에 따라 챕터를 해금한다.
    ///
    /// 두 가지 모델을 한 자료 구조로 담는다 — 기록자는 <b>연재</b>(챕터가 순차 개방),
    /// 각인사는 <b>사전</b>(<see cref="StoryChapter.requiredFormId"/>로 항목마다 독립 개방).
    /// 해금 판정은 <see cref="StoryChapterSelector"/>가 한 곳에서 한다.
    /// </summary>
    [CreateAssetMenu(fileName = "StoryData", menuName = "Abyss/Data/Story Data")]
    public sealed class StoryData : ScriptableObject
    {
        [Tooltip("스토리 챕터 목록(chapterStage 오름차순 권장).")]
        public StoryChapter[] chapters;

        [Tooltip("볼 수 있는 새 챕터가 없을 때 재생할 기본 대사.")]
        public DialogueLine[] idleLines;
    }
}
