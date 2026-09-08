using Abyss.Runtime.Enemy;
using Abyss.Runtime.Feedback;
using Abyss.Runtime.Player;
using ObjectPool_Core;
using UnityEngine;

namespace Abyss.Runtime.Combat
{
    /// <summary>
    /// 곡사 폭발탄. 목표 지점으로 포물선을 그리며 날아가 착탄 시 광역 피해를 준다.
    ///
    /// <see cref="Projectile"/>(직진)을 확장하지 않고 따로 둔 이유는 <b>회귀 범위</b>다.
    /// 직진탄은 적 원거리 공격과 플레이어 스킬이 모두 쓰는 검증된 경로라, 거기에 중력·폭발 분기를
    /// 얹으면 곡사와 무관한 발사체까지 전부 영향권에 들어온다. 풀링·피해 처리가 두 벌이 되는 비용은
    /// 지불할 만하다.
    ///
    /// <b>적 전용이다.</b> 플레이어 스킬용 곡사탄이 생기면 그때 진영 개념을 도입할 것 —
    /// 지금 넣으면 소비자가 하나뿐인 분기가 된다.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public sealed class ArcProjectile : MonoBehaviour, IPoolable
    {
        /// <summary>포물선에 적용할 중력 가속도(음수). 씬 물리와 무관하게 자체 적분한다.</summary>
        private const float ARC_GRAVITY = -20f;

        /// <summary>착탄 폭발 링 색(불꽃 계열). 표시 규약을 Draft 쪽에서 끌어오지 않으려 상수로 둔다.</summary>
        private static readonly Color ExplosionColor = new(1f, 0.46f, 0.18f);

        private Vector2 velocity;
        private int damage;
        private float explosionRadius;
        private float lifetime;
        private float aliveTimer;
        private ArcProjectile prefabRef;
        private bool consumed;

        private static readonly Collider2D[] blastBuffer = new Collider2D[16];
        private static ContactFilter2D blastFilter = ContactFilter2D.noFilter;

        /// <summary>
        /// 목표 지점에 <paramref name="flightTime"/>초 뒤 떨어지도록 발사한다.
        ///
        /// 발사각이 아니라 <b>비행 시간</b>을 입력으로 받는 이유: 각도를 주면 사거리에 따라
        /// "도달 불가"가 생겨 조용히 빗나가는 경우를 호출부가 처리해야 한다. 시간을 고정하면
        /// 해가 항상 존재하고(<c>vx = dx/T</c>, <c>vy = dy/T - ½gT</c>), 덤으로 <b>예고 시간이
        /// 거리와 무관하게 일정</b>해져 플레이어가 회피 리듬을 익힐 수 있다.
        /// </summary>
        public void Launch(Vector2 targetPos, int dmg, float flightTime, float radius, ArcProjectile prefab)
        {
            damage = dmg;
            explosionRadius = Mathf.Max(0.1f, radius);
            prefabRef = prefab;
            consumed = false;
            aliveTimer = 0f;

            float t = Mathf.Max(0.1f, flightTime);
            Vector2 delta = targetPos - (Vector2)transform.position;
            velocity = new Vector2(delta.x / t, delta.y / t - 0.5f * ARC_GRAVITY * t);

            // 수명은 비행 시간보다 넉넉히 — 지형에 먼저 맞으면 그때 터지고, 아니면 예정 시각에 터진다.
            lifetime = t;

            // 착탄 예고. 예고가 없으면 회피가 운이 된다(보스 패턴에서 얻은 규칙).
            // 폭발과 같은 반경으로 띄워야 "저기까지가 위험"이 정확해진다.
            BossAreaEffect.Spawn(targetPos, explosionRadius, ExplosionColor, t, BossAreaEffect.Mode.Telegraph);
        }

        private void Update()
        {
            if (consumed) return;

            float dt = Time.deltaTime;
            velocity += new Vector2(0f, ARC_GRAVITY * dt);
            transform.position += (Vector3)(velocity * dt);

            // 진행 방향으로 회전 — 포물선을 그리는 동안 탄이 기울어져 궤적이 읽힌다.
            float angle = Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);

            aliveTimer += dt;
            if (aliveTimer >= lifetime) Explode();
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (consumed) return;

            // 발사 주체·아군 적은 통과 — 머리 위로 쏘는 구도라 아군을 스치는 일이 잦다.
            if (other.GetComponentInParent<EnemyBase>() != null) return;

            // 플레이어 직격 또는 지형 착탄. 어느 쪽이든 터지는 건 같다(광역이라 직격 보너스가 없다).
            if (other.GetComponentInParent<PlayerCharacter>() != null || !other.isTrigger) Explode();
        }

        /// <summary>
        /// 착탄 폭발. 반경 안의 플레이어에게 1회 피해. 적은 때리지 않는다 —
        /// 적 포격이 적을 죽이면 플레이어가 유인 플레이로 방을 청소하게 되고, 그건 다른 게임이다.
        /// </summary>
        private void Explode()
        {
            if (consumed) return;
            consumed = true;

            BossAreaEffect.Spawn(transform.position, explosionRadius, ExplosionColor);

            blastFilter.useTriggers = Physics2D.queriesHitTriggers;
            int count = Physics2D.OverlapCircle((Vector2)transform.position, explosionRadius, blastFilter, blastBuffer);

            for (int i = 0; i < count; i++)
            {
                var col = blastBuffer[i];
                if (col == null) continue;

                var player = col.GetComponentInParent<PlayerCharacter>();
                if (player == null || player.IsDead) continue;

                player.TakeDamage(damage);
                break; // 플레이어는 하나뿐 — 콜라이더가 여러 개여도 한 번만 맞는다.
            }

            ReturnToPool();
        }

        private void ReturnToPool()
        {
            if (prefabRef != null && PoolManager.HasInstance)
                PoolManager.Instance.Release(prefabRef, this);
            else
                Destroy(gameObject);
        }

        // ----- IPoolable -----
        public void OnSpawn() => consumed = false;
        public void OnDespawn() => consumed = true;
    }
}
