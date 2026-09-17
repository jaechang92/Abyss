using System;
using Abyss.Runtime.Combat;
using UnityEngine;

namespace Abyss.Runtime.Form
{
    /// <summary>
    /// 원거리 기본 공격 한 종(약 또는 강)의 설정. 기획: <c>Docs/game-design/15-ranged-basic-attack.md</c>.
    ///
    /// 🔑 <b>사거리는 따로 적지 않는다</b> — <see cref="Range"/> = 속도 × 수명에서 파생된다.
    /// 사거리를 필드로 두면 속도나 수명을 바꿀 때 둘이 어긋난다.
    /// </summary>
    [Serializable]
    public struct RangedAttackSpec
    {
        [Tooltip("플레이어 전용 발사체 프리팹. 비면 근접으로 물러난다(경고 1회).")]
        public Projectile projectilePrefab;

        [Tooltip("유닛/초")]
        [Min(0f)] public float speed;

        [Tooltip("초. 사거리 = 속도 × 수명")]
        [Min(0f)] public float lifetime;

        [Tooltip("첫 적 뒤로 더 뚫고 지나갈 적 수. 0 = 첫 적에서 멈춘다")]
        [Min(0)] public int pierceCount;

        [Tooltip("근접 피해(버프·메타·무기 배율이 이미 곱해진 값)에 곱할 배율. 사거리의 대가다")]
        [Range(0f, 2f)] public float damageScale;

        [Tooltip("장착한 무기 그림을 발사체에 입힌다(투척사). 끄면 프리팹 그림 그대로(궁수 화살)")]
        public bool useWeaponSprite;

        [Tooltip("그림이 오른쪽을 향하게 돌리는 보정(도). 무기 그림은 칼끝이 오른쪽 위 45°라 -45")]
        public float spriteAngleOffset;

        /// <summary>사거리(유닛). 속도 × 수명.</summary>
        public float Range => speed * lifetime;

        /// <summary>발사할 수 있는 설정인가. 프리팹이 없거나 날아가지 못하면 <c>false</c>.</summary>
        public bool IsValid => projectilePrefab != null && speed > 0f && lifetime > 0f;

        /// <summary>
        /// 근접 피해 → 원거리 피해. 🔴 <b>피해 식을 새로 만들지 않는다</b> — 입력은
        /// <c>PlayerCharacter.LightAttackDamage</c>(세 층 배율이 이미 곱해진 값)이고 여기선 배율 하나만 곱한다.
        /// 0 으로 떨어지지 않게 최소 1.
        /// </summary>
        public static int ScaleDamage(int meleeDamage, float damageScale)
        {
            return Mathf.Max(1, Mathf.RoundToInt(meleeDamage * damageScale));
        }
    }
}
