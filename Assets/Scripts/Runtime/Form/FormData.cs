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

        [Tooltip("제단에서 해금해야 쓸 수 있는가. 기본 false = 처음부터 열려 있다.")]
        public bool requiresMetaUnlock;

        [Header("표시 정보")]
        public string displayName;
        [TextArea(2, 4)] public string description;
        public Sprite icon;

        [Header("스탯 보정 (RunConfig.baseHp·moveSpeed 대비 배율)")]
        [Range(0.5f, 2f)] public float hpMultiplier = 1f;
        [Range(0.5f, 2f)] public float moveSpeedMultiplier = 1f;
        [Tooltip("폼별 다단 점프 횟수. FormA(암흑검사)=2, FormB(공허궁수)=3 (기획 02-form-change-system.md)")]
        [Min(1)] public int jumpCount = 2;

        [Header("폼 전용 어빌리티 (P-14에서 채움)")]
        public AbilityData primaryAction;
        public AbilityData secondaryAction;

        [Header("폼 스킬 개시 연출")]
        [Tooltip("이 폼의 formBound 전용 스킬 발동 시 본체 개시 플래시 색상. " +
                 "기본값은 공용 청록 — 폼별로 지정하면 발동한 폼을 색으로 구분한다.")]
        public Color castColor = new(0.6f, 0.9f, 1f, 1f);
    }
}
