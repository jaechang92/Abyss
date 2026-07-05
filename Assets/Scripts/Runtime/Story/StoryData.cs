using System;
using Abyss.Runtime.Dialogue;
using UnityEngine;

namespace Abyss.Runtime.Story
{
    /// <summary>스토리 챕터 1개. 해금 조건 충족 + 미시청 시 서사 NPC가 재생한다.</summary>
    [Serializable]
    public struct StoryChapter
    {
        [Tooltip("시청 시 storyStage가 이 값이 된다(1부터 오름차순).")]
        public int chapterStage;

        [Tooltip("직전 챕터 시청 이후 추가로 필요한 런 수(델타). 0이면 진전 없이 즉시.")]
        public int minRunCount;

        [Tooltip("직전 챕터 시청 이후 추가로 필요한 보스 처치 수(델타).")]
        public int minBossKills;

        [Tooltip("챕터 대사(StringKey 참조).")]
        public DialogueLine[] lines;
    }

    /// <summary>
    /// 서사 NPC 스토리 데이터 SO. 진행도(storyStage + records)에 따라 챕터를 순차 해금한다.
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
