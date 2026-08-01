using System;
using System.Collections.Generic;
using UnityEngine;

namespace Abyss.Runtime.Stage
{
    /// <summary>
    /// 스테이지 진행의 한 단계. 완주 루프 계획 1-4.
    ///
    /// <b>선형 구간과 분기를 같은 것으로 다룬다</b> — 옵션이 1개면 고정 진행, 2개 이상이면 갈림길이다.
    /// 분기 테이블을 따로 두면 진행 순서의 SoT가 둘로 갈라져, 방을 추가할 때 어느 쪽을 고쳐야 하는지
    /// 매번 판단해야 한다. 이전 구조(<c>List&lt;RoomData&gt;</c>)는 "옵션 1개짜리 단계의 나열"로 흡수된다.
    /// </summary>
    [Serializable]
    public sealed class StageStep
    {
        [Tooltip("이 단계에서 갈 수 있는 방. 1개면 고정 진행, 2개 이상이면 분기 선택지가 뜬다.")]
        public List<RoomData> options = new();

        /// <summary>선택지가 둘 이상인가(= 노드 맵을 띄울 단계인가).</summary>
        public bool IsBranch => options != null && options.Count > 1;

        /// <summary>유효한 첫 방. 옵션이 비었거나 전부 null이면 null.</summary>
        public RoomData First
        {
            get
            {
                if (options == null) return null;
                foreach (var room in options)
                {
                    if (room != null) return room;
                }
                return null;
            }
        }
    }

    /// <summary>
    /// 스테이지 정의 SO. 단계를 순차로 나열하며, 단계 하나가 갈림길이 될 수 있다(1-4).
    /// </summary>
    [CreateAssetMenu(fileName = "StageData", menuName = "Abyss/Data/Stage Data")]
    public sealed class StageData : ScriptableObject
    {
        [Header("식별자")]
        public string stageId;
        public string displayName;

        [Header("진행 단계 (순차, 단계별 분기 가능)")]
        public List<StageStep> steps = new();
    }
}
