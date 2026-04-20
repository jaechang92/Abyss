using System.Collections.Generic;
using UnityEngine;

namespace Abyss.Runtime.Stage
{
    /// <summary>
    /// 스테이지 정의 SO. 방을 순차로 나열 (프로토엔 노드 분기 없음 — Critic S3 반영).
    /// </summary>
    [CreateAssetMenu(fileName = "StageData", menuName = "Abyss/Data/Stage Data")]
    public sealed class StageData : ScriptableObject
    {
        [Header("식별자")]
        public string stageId;
        public string displayName;

        [Header("방 목록 (순차 진행)")]
        public List<RoomData> rooms = new();
    }
}
