using Abyss.Runtime.Audio;
using Abyss.Runtime.Feedback;
using UnityEngine;

namespace Abyss.Runtime.Enemy
{
    /// <summary>
    /// 엘리트 변종 ① 광폭화(2026-10-06). HP가 임계 이하로 떨어지면 <b>한 번</b> 광폭화한다 —
    /// 이동·공격 빈도·피해가 오르고, 공격 동작 중에는 경직을 버린다.
    ///
    /// 플레이어에게 묻는 것: 「반쯤 깎은 뒤 그대로 밀어붙일 것인가, 물러나 거리를 벌릴 것인가」.
    /// 그래서 광폭화 순간을 링·펀치·색으로 크게 알린다 — 몰래 강해지면 판단할 기회가 없다.
    ///
    /// 그림은 중장 강적을 빌린다(<see cref="EnemyData.artSourceId"/>). 예비동작 시간도 같아 클립이 그대로 맞는다.
    /// </summary>
    public sealed class EliteBerserkerEnemy : EnemyBase
    {
        [Header("광폭화")]
        [Tooltip("광폭화가 일어나는 HP 비율(이하)")]
        [SerializeField, Range(0.1f, 0.9f)] private float enrageHpRatio = 0.5f;
        [SerializeField, Min(1f)] private float enragedMoveSpeedMultiplier = 1.5f;
        [Tooltip("광폭화 중 공격 쿨다운 배율(0.6 = 40% 단축)")]
        [SerializeField, Range(0.25f, 1f)] private float enragedAttackCooldownMultiplier = 0.6f;
        [SerializeField, Min(1f)] private float enragedDamageMultiplier = 1.3f;
        [Tooltip("광폭화 후 몸 기본색(곱). 데이터의 bodyTint보다 진하다.")]
        [SerializeField] private Color enragedTint = new(1f, 0.32f, 0.28f, 1f);

        [Header("광폭화 연출")]
        [SerializeField, Min(0f)] private float enrageRingRadius = 2.4f;
        [SerializeField, Min(0.05f)] private float enrageRingDuration = 0.45f;
        [SerializeField] private AudioClip enrageSfx;

        private bool isEnraged;

        /// <summary>광폭화했는가(디버그·스탯 창 조회용).</summary>
        public bool IsEnraged => isEnraged;

        protected override float MoveSpeedMultiplier => isEnraged ? enragedMoveSpeedMultiplier : 1f;
        protected override float AttackCooldownMultiplier => isEnraged ? enragedAttackCooldownMultiplier : 1f;

        /// <summary>
        /// 광폭화 중에는 공격 동작이 끊기지 않는다 — 「때려서 끊으면 된다」가 통하지 않는 것이 광폭화의 위협이다.
        /// 공격 동작 밖에서는 여전히 경직된다(<see cref="EnemyTimedState.IsStaggerIgnored"/>).
        /// </summary>
        protected override bool ResistsStaggerWhileAttacking => isEnraged;

        protected override void Awake()
        {
            base.Awake();
            OnHpChanged += HandleHpChangedForEnrage;
        }

        private void OnDestroy()
        {
            OnHpChanged -= HandleHpChangedForEnrage;
        }

        protected override int GetAttackDamage()
        {
            int damage = base.GetAttackDamage();
            return isEnraged ? Mathf.RoundToInt(damage * enragedDamageMultiplier) : damage;
        }

        private void HandleHpChangedForEnrage(int previous, int current)
        {
            if (isEnraged || IsDead || current <= 0 || MaxHp <= 0) return;
            if ((float)current / MaxHp > enrageHpRatio) return;

            isEnraged = true;
            SetVisualBaseColor(enragedTint);
            PunchVisual(0.25f, 0.3f);
            if (enrageRingRadius > 0f)
            {
                BossAreaEffect.Spawn(transform.position, enrageRingRadius, enragedTint, enrageRingDuration);
            }
            if (enrageSfx != null && AudioManager.HasInstance) AudioManager.Instance.PlaySfx(enrageSfx);
            Debug.Log($"[EliteBerserker] 광폭화 — HP {current}/{MaxHp}");
        }
    }
}
