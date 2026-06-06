using System.Collections.Generic;
using UnityEngine;

namespace Abyss.Runtime.Stage
{
    /// <summary>
    /// 멀티 스테이지 진행 순서를 정의하는 SO. StageData를 순차로 나열한다.
    /// StageDirector가 이 시퀀스를 참조해 한 스테이지 클리어 시 다음 스테이지로 진입하고,
    /// 마지막 스테이지 클리어 시 런을 종료한다(프로토 단일 런 = 1 시퀀스).
    /// </summary>
    [CreateAssetMenu(fileName = "StageSequenceData", menuName = "Abyss/Data/Stage Sequence Data")]
    public sealed class StageSequenceData : ScriptableObject
    {
        [Header("식별자")]
        public string sequenceId;
        public string displayName;

        [Header("스테이지 목록 (순차 진행)")]
        public List<StageData> stages = new();
    }
}
