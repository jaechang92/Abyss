using Abyss.Runtime.Combat;
using Abyss.Runtime.Feedback;
using Abyss.Runtime.Form;
using ObjectPool_Core;
using UnityEngine;

namespace Abyss.Runtime.Player
{
    /// <summary>
    /// 원거리 기본 공격(궁수 · 투척사). 기획: <c>Docs/game-design/15-ranged-basic-attack.md</c>.
    ///
    /// 🔑 <b>근접과 갈리는 곳은 「맞았다」가 확정되는 시점 하나다.</b> 근접은 입력 프레임에 확정되고
    /// (<c>CollectAndDamageEnemies</c>), 발사체는 닿는 프레임에 확정된다. 그래서 적중에 붙는 셋 —
    /// 심연 충전 · 히트스탑 · 흔들림 — 을 <see cref="RangedHitFeedback"/> 이 발사체에게서 돌려받아 처리한다.
    ///
    /// 🔴 <b>피해 식은 근접 것을 그대로 탄다.</b> 입력은 <c>LightAttackDamage</c>(버프·메타·무기 배율이 곱해진 값)이고
    /// 여기서는 폼 배율 하나만 곱한다(<see cref="RangedAttackSpec.ScaleDamage"/>).
    /// </summary>
    public sealed partial class PlayerCharacter
    {
        [Header("원거리 공격 피드백")]
        [Tooltip("발사 지점에 잠깐 뜨는 링 반경(유닛)")]
        [SerializeField, Min(0f)] private float rangedMuzzleRadius = 0.35f;
        [SerializeField, Min(0.01f)] private float rangedMuzzleDuration = 0.15f;

        // 발사체에 넘길 청취자. 풀링되는 발사체에 발사마다 새 객체를 넘기지 않도록 무게별로 하나씩만 만든다.
        private RangedHitFeedback lightRangedFeedback;
        private RangedHitFeedback heavyRangedFeedback;
        private FormData rangedFallbackWarnedForm;

        /// <summary>
        /// 현재 폼이 원거리면 발사하고 <c>true</c>. 근접 폼이거나 설정이 비어 있으면 <c>false</c> — 호출자가 근접으로 판정한다.
        /// </summary>
        private bool TryPerformRangedAttack(int meleeDamage, float hitstop, Vector2 shake, bool isHeavy)
        {
            FormData form = formController != null ? formController.CurrentForm : null;
            if (form == null || form.attackStyle != FormAttackStyle.Ranged) return false;

            RangedAttackSpec spec = isHeavy ? form.rangedHeavy : form.rangedLight;
            if (!spec.IsValid)
            {
                // 빌더를 아직 안 돌린 에셋이다. 공격이 통째로 사라지는 것보다 근접으로 물러나는 편이 낫다.
                if (rangedFallbackWarnedForm != form)
                {
                    rangedFallbackWarnedForm = form;
                    Debug.LogWarning($"[PlayerCharacter] {form.formId}: 원거리 설정이 비어 있어 근접으로 판정한다 " +
                                     "(발사체 프리팹 · 속도 · 수명). Prefab 빌더 → Content 빌더를 다시 실행할 것.");
                }
                return false;
            }

            int damage = RangedAttackSpec.ScaleDamage(meleeDamage, spec.damageScale);
            FireRangedProjectile(spec, damage, ResolveRangedFeedback(isHeavy, hitstop, shake), form.castColor);
            return true;
        }

        private void FireRangedProjectile(RangedAttackSpec spec, int damage, RangedHitFeedback feedback, Color muzzleColor)
        {
            Vector3 spawnPos = attackPoint != null ? attackPoint.position : transform.position;
            Vector2 dir = new(facingSign, 0f);

            // PoolManager 는 Instance 접근 시 자동 생성된다(스킬 ProjectileEffect 와 같은 규약).
            Projectile proj = PoolManager.Instance.Get(spec.projectilePrefab, spawnPos, Quaternion.identity);
            if (proj == null) return;

            proj.Launch(dir, damage, spec.speed, spec.lifetime, spec.projectilePrefab,
                        ProjectileFaction.HitsEnemies, default, spec.pierceCount, feedback);

            // Launch 가 진행 방향으로 돌려 둔 뒤에 그림 보정을 얹는다.
            if (!Mathf.Approximately(spec.spriteAngleOffset, 0f))
            {
                proj.transform.rotation *= Quaternion.Euler(0f, 0f, spec.spriteAngleOffset);
            }

            ApplyRangedSprite(proj, spec);
            BossAreaEffect.Spawn(spawnPos, rangedMuzzleRadius, muzzleColor, rangedMuzzleDuration);
        }

        /// <summary>
        /// 투척사는 <b>장착한 무기 그림</b>을 던진다 — 무기를 바꾸면 날아가는 것도 바뀐다.
        /// 🔑 발사 순간 무기에서 읽는다. 데이터에 복사해 두면 무기 교체 때 어긋난다.
        /// 풀에서 나온 발사체는 지난 그림을 들고 있으므로, 무기가 없으면 프리팹 그림으로 되돌린다.
        /// </summary>
        private void ApplyRangedSprite(Projectile proj, RangedAttackSpec spec)
        {
            if (!spec.useWeaponSprite) return;

            var sr = proj.GetComponent<SpriteRenderer>();
            if (sr == null) return;

            Sprite weaponSprite = CurrentWeapon != null ? CurrentWeapon.sprite : null;
            if (weaponSprite != null)
            {
                sr.sprite = weaponSprite;
                return;
            }

            var prefabSr = spec.projectilePrefab.GetComponent<SpriteRenderer>();
            if (prefabSr != null) sr.sprite = prefabSr.sprite;
        }

        private RangedHitFeedback ResolveRangedFeedback(bool isHeavy, float hitstop, Vector2 shake)
        {
            if (isHeavy)
            {
                heavyRangedFeedback ??= new RangedHitFeedback(this);
                heavyRangedFeedback.Configure(hitstop, shake);
                return heavyRangedFeedback;
            }

            lightRangedFeedback ??= new RangedHitFeedback(this);
            lightRangedFeedback.Configure(hitstop, shake);
            return lightRangedFeedback;
        }

        /// <summary>
        /// 발사체 적중을 돌려받아 근접과 같은 적중 효과를 건다.
        ///
        /// ⚠️ <b>관통 발사체는 한 발이 여러 번 적중한다.</b> 심연 충전과 히트스탑 · 흔들림은 <b>첫 적중에만</b> —
        /// 충전은 「다음 공격 한 번」이 약속이고, 관통할 때마다 멈추면 화면이 끊긴다.
        /// </summary>
        private sealed class RangedHitFeedback : IProjectileHitListener
        {
            private readonly PlayerCharacter owner;
            private float hitstop;
            private Vector2 shake;

            public RangedHitFeedback(PlayerCharacter owner)
            {
                this.owner = owner;
            }

            /// <summary>
            /// 인스펙터 값이 런타임에 바뀌어도 따라가도록 발사마다 다시 넣는다.
            /// 날아가는 중인 발사체도 같은 객체를 보므로, 발사 사이에 값이 바뀌면 그 값으로 적중한다 — 차이는 체감 밖이다.
            /// </summary>
            public void Configure(float hitstopSeconds, Vector2 shakeMagDuration)
            {
                hitstop = hitstopSeconds;
                shake = shakeMagDuration;
            }

            public int OnProjectileHit(int damage, bool isFirstHit)
            {
                if (owner == null || !isFirstHit) return damage;

                // 심연 충전은 적중이 확정된 뒤에 소비한다 — 빗나간 화살로 장전이 풀리지 않는다(근접과 같은 약속).
                int finalDamage = owner.ConsumeAbyssCharge(damage);

                if (HitstopController.HasInstance) HitstopController.Instance.Trigger(hitstop);
                owner.TriggerShake(shake);
                return finalDamage;
            }
        }
    }
}
