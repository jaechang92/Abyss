using UnityEngine;

namespace Abyss.Runtime.Player
{
    /// <summary>
    /// 시간제 버프 파트. 스킬(BuffEffect)이 ApplyTimedBuff로 이동속도·공격력 배율을 일시 적용하고,
    /// UpdateBuff가 만료 시 1배로 원복한다. Movement(moveSpeed)·Combat(공격 데미지)이 배율을 참조한다.
    /// 프로토 범위: 단일 버프 슬롯(재적용 시 덮어씀), 이동속도·공격력 2종.
    /// </summary>
    public sealed partial class PlayerCharacter
    {
        private float buffMoveMult = 1f;
        private float buffAtkMult = 1f;
        private float buffTimer;

        /// <summary>이동속도 배율(1 = 기본). Movement가 참조.</summary>
        public float MoveSpeedMultiplier => buffMoveMult;

        /// <summary>공격력 배율(1 = 기본). Combat이 참조.</summary>
        public float AttackMultiplier => buffAtkMult;

        /// <summary>버프 활성 여부(잔여 시간 &gt; 0).</summary>
        public bool HasActiveBuff => buffTimer > 0f;

        /// <summary>버프 잔여 시간(초).</summary>
        public float BuffRemaining => Mathf.Max(0f, buffTimer);

        /// <summary>
        /// 시간제 버프 적용(재적용 시 덮어씀). moveMult/atkMult는 1이면 해당 항목 변화 없음.
        /// duration이 0 이하면 무시(즉시 효과는 BuffEffect의 Heal이 담당).
        /// </summary>
        public void ApplyTimedBuff(float moveMult, float atkMult, float duration)
        {
            if (duration <= 0f) return;

            buffMoveMult = Mathf.Max(0f, moveMult);
            buffAtkMult = Mathf.Max(0f, atkMult);
            buffTimer = duration;
            Debug.Log($"[PlayerCharacter] 버프 적용 — 이동 x{buffMoveMult:F2} / 공격 x{buffAtkMult:F2} / {duration:F1}초");
        }

        // PlayerCharacter.Update에서 매 프레임 호출. 만료 시 1배 원복.
        private void UpdateBuff()
        {
            if (buffTimer <= 0f) return;

            buffTimer -= Time.deltaTime;
            if (buffTimer <= 0f)
            {
                buffMoveMult = 1f;
                buffAtkMult = 1f;
            }
        }
    }
}
