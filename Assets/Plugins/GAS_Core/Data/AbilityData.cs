using UnityEngine;

namespace GAS.Core
{
    /// <summary>
    /// 어빌리티 데이터 ScriptableObject 베이스
    /// 디자이너가 Unity Editor에서 편집 가능
    /// Fire Magic, Ice Magic 등이 이 클래스를 상속
    /// </summary>
    [CreateAssetMenu(fileName = "AbilityData", menuName = "GASPT/Abilities/Base")]
    public class AbilityData : ScriptableObject
    {
        // ====== 기본 정보 ======

        [Header("기본 정보")]
        [Tooltip("어빌리티 고유 식별자 (예: FireMagic)")]
        public string abilityName;

        [Tooltip("어빌리티 설명")]
        [TextArea(3, 5)]
        public string description;

        [Tooltip("어빌리티 아이콘 (UI 표시용)")]
        public Sprite icon;


        // ====== 쿨다운 ======

        [Header("쿨다운")]
        [Tooltip("쿨다운 시간 (초)")]
        [Range(0f, 60f)]
        public float cooldownDuration;


        // ====== 실행 조건 ======

        [Header("실행 조건")]
        [Tooltip("소유자가 살아있어야 실행 가능")]
        public bool requiresAlive = true;

        [Tooltip("소유자가 행동 가능해야 실행 가능 (스턴, 경직 제외)")]
        public bool requiresCanAct = true;


        // ====== 헬퍼 메서드 ======

        /// <summary>
        /// 데이터 유효성 검증
        /// </summary>
        /// <returns>true: 유효, false: 무효</returns>
        public virtual bool Validate()
        {
            if (string.IsNullOrEmpty(abilityName))
            {
                Debug.LogError($"[AbilityData] abilityName이 비어있습니다: {name}");
                return false;
            }

            if (cooldownDuration < 0f)
            {
                Debug.LogError($"[AbilityData] {abilityName}: cooldownDuration이 음수입니다.");
                return false;
            }

            return true;
        }

        /// <summary>
        /// 디버그 정보 출력
        /// </summary>
        public virtual void DebugPrint()
        {
            Debug.Log($"[AbilityData] {abilityName}");
            Debug.Log($"  - Description: {description}");
            Debug.Log($"  - Cooldown: {cooldownDuration}초");
            Debug.Log($"  - RequiresAlive: {requiresAlive}");
            Debug.Log($"  - RequiresCanAct: {requiresCanAct}");
        }
    }
}
