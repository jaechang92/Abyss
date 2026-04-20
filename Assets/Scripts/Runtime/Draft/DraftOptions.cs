using System.Collections.Generic;
using Abyss.Runtime.Events;

namespace Abyss.Runtime.Draft
{
    /// <summary>
    /// 드래프트 세션 1회분 카드 묶음. 3지선다 (프로토 확정).
    /// </summary>
    public sealed class DraftOptions
    {
        public IReadOnlyList<SkillData> Cards { get; }
        public DraftTriggerReason Reason { get; }
        public int RerollIndex { get; }

        public DraftOptions(IReadOnlyList<SkillData> cards, DraftTriggerReason reason, int rerollIndex)
        {
            Cards = cards;
            Reason = reason;
            RerollIndex = rerollIndex;
        }
    }
}
