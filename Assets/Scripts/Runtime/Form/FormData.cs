using GAS.Core;
using UnityEngine;

namespace Abyss.Runtime.Form
{
    /// <summary>
    /// 폼(직업) 정의 ScriptableObject. 프로토 최소 필드.
    /// 확장 필드(점프 횟수·대시 스타일·패시브·근/원거리 카테고리 등)는 P-14에서 추가.
    /// </summary>
    [CreateAssetMenu(fileName = "FormData", menuName = "Abyss/Data/Form Data")]
    public sealed class FormData : ScriptableObject
    {
        [Header("식별자")]
        [Tooltip("로깅·세이브에 쓰이는 고유 ID. 예: dark_blade, void_archer")]
        public string formId;

        [Header("표시 정보")]
        public string displayName;
        [TextArea(2, 4)] public string description;
        public Sprite icon;

        [Header("스탯 보정 (RunConfig.baseHp·moveSpeed 대비 배율)")]
        [Range(0.5f, 2f)] public float hpMultiplier = 1f;
        [Range(0.5f, 2f)] public float moveSpeedMultiplier = 1f;

        [Header("폼 전용 어빌리티 (P-14에서 채움)")]
        public AbilityData primaryAction;
        public AbilityData secondaryAction;
    }
}
