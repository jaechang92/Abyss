using UnityEngine;
using UnityEngine.InputSystem;

namespace Abyss.Runtime.Player
{
    /// <summary>
    /// 전투 입력 훅. 실제 공격 판정·데미지는 P-14 이후 GAS Ability로 위임.
    /// 프로토 단계는 쿨다운 관리 + 디버그 로그만 수행.
    /// </summary>
    public sealed partial class PlayerCharacter
    {
        [Header("공격 쿨다운 (실제 판정은 GAS Ability 위임)")]
        [SerializeField, Min(0f)] private float attackCooldownLight = 0.3f;
        [SerializeField, Min(0f)] private float attackCooldownHeavy = 0.8f;

        private float lastAttackLightTime = -999f;
        private float lastAttackHeavyTime = -999f;

        public bool CanAttackLight => Time.time >= lastAttackLightTime + attackCooldownLight;
        public bool CanAttackHeavy => Time.time >= lastAttackHeavyTime + attackCooldownHeavy;

        private void OnAttack(InputValue value)
        {
            if (!value.isPressed) return;
            if (!CanAttackLight) return;

            lastAttackLightTime = Time.time;
            stateMachine?.TriggerAttackLight();
        }

        private void OnAttackHeavy(InputValue value)
        {
            if (!value.isPressed) return;
            if (!CanAttackHeavy) return;

            lastAttackHeavyTime = Time.time;
            stateMachine?.TriggerAttackHeavy();
        }
    }
}
