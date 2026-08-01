using System;
using System.Collections.Generic;
using UnityEngine;

namespace Abyss.Runtime.Stage
{
    /// <summary>
    /// 이벤트 선택지 하나가 일으키는 효과의 종류. 완주 루프 계획 1-1.
    ///
    /// 전부 <b>이미 있는 API</b>에 대응한다 — 이벤트를 위해 새 시스템을 만들지 않는다.
    /// 후속 1-3 휴식 룸(회복)·1-2 상점 룸(골드)도 같은 어휘를 그대로 쓴다.
    /// </summary>
    public enum EventEffectType
    {
        /// <summary>골드 획득. amount = 절대량.</summary>
        GoldGain,

        /// <summary>골드 지불. amount = 절대량. 잔액이 모자라면 그 선택지는 비활성화된다.</summary>
        GoldSpend,

        /// <summary>체력 회복. amount = 최대 HP 대비 %.</summary>
        HealPercent,

        /// <summary>체력 대가. amount = 최대 HP 대비 %. 최소 1은 남는다(선택으로 죽지 않는다).</summary>
        HpCostPercent,

        /// <summary>스킬 드래프트 1회. amount 무시.</summary>
        SkillDraft
    }

    /// <summary>효과 하나. 선택지는 이걸 여러 개 조합한다(예: 골드 지불 + 드래프트).</summary>
    [Serializable]
    public struct EventEffect
    {
        public EventEffectType type;

        [Tooltip("골드는 절대량, Percent 계열은 최대 HP 대비 백분율. SkillDraft는 무시.")]
        [Min(0)] public int amount;
    }

    /// <summary>이벤트 선택지 하나.</summary>
    [Serializable]
    public sealed class EventChoice
    {
        [Tooltip("버튼에 표시할 문구. 예: \"공물을 바친다\"")]
        public string label;

        [Tooltip("선택 후 보여줄 결과 문구. 효과가 없는 선택지에도 채워 넣을 것 — 빈 값이면 결과 화면이 허전하다.")]
        [TextArea(2, 4)] public string resultText;

        public List<EventEffect> effects = new();

        /// <summary>이 선택지가 요구하는 골드 총액(0이면 무료).</summary>
        public int GoldCost
        {
            get
            {
                int sum = 0;
                foreach (var e in effects)
                {
                    if (e.type == EventEffectType.GoldSpend) sum += Mathf.Max(0, e.amount);
                }
                return sum;
            }
        }
    }

    /// <summary>
    /// 이벤트 방 1개 정의 SO. 완주 루프 계획 1-1.
    ///
    /// 문구는 평문 한글로 둔다 — 콘텐츠 SO의 기존 관습(<c>SkillData.displayName</c>·<c>description</c>)과 같다.
    /// 다국어가 필요해지면 <c>LocalizationManager</c> 이관과 함께 일괄 전환한다.
    /// </summary>
    [CreateAssetMenu(fileName = "EventData", menuName = "Abyss/Data/Event Data")]
    public sealed class EventData : ScriptableObject
    {
        [Header("식별자")]
        public string eventId;

        [Header("표시")]
        public string title;
        [TextArea(3, 6)] public string description;

        [Tooltip("선택지 2~3개 권장. 하나는 대가 없이 지나가는 선택을 두는 편이 좋다.")]
        public List<EventChoice> choices = new();
    }
}
