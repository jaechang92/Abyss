using Abyss.Runtime.Enemy;
using Abyss.Runtime.Player;
using ObjectPool_Core;
using UnityEngine;

namespace Abyss.Runtime.Combat
{
    /// <summary>
    /// 직진 발사체. 적(isRanged)이 발사하며, 플레이어 또는 정적 지형에 닿으면 소멸한다.
    /// PoolManager 풀링(IPoolable). 발사 주체·아군 적·다른 발사체는 통과.
    /// 향후 플레이어/스킬 발사체로도 재활용 가능하도록 Combat 네임스페이스에 둔다.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public sealed class Projectile : MonoBehaviour, IPoolable
    {
        private Vector2 direction = Vector2.right;
        private int damage;
        private float speed;
        private float lifetime;
        private float aliveTimer;
        private Projectile prefabRef;
        private bool consumed;
        private ProjectileFaction faction = ProjectileFaction.HitsPlayer;
        private BurnPayload burn;

        /// <summary>
        /// 발사 초기화. prefabRef는 풀 반환 키로 사용(EnemyData.projectilePrefab 원본).
        /// faction은 누구를 맞힐지 결정 — 기본 HitsPlayer(적 발사체). 플레이어 스킬은 HitsEnemies.
        /// burn은 명중한 적에게 부여할 연소(기본 default = 연소 없음). 적 발사체는 이 인자를 넘기지 않아
        /// 기존 호출부가 그대로 동작한다.
        /// </summary>
        public void Launch(Vector2 dir, int dmg, float spd, float life, Projectile prefab,
                           ProjectileFaction faction = ProjectileFaction.HitsPlayer,
                           BurnPayload burn = default)
        {
            this.burn = burn;
            direction = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector2.right;
            damage = dmg;
            speed = spd;
            lifetime = life;
            prefabRef = prefab;
            this.faction = faction;
            aliveTimer = 0f;
            consumed = false;

            // 진행 방향으로 회전(시각). 화살형 스프라이트 도입 시 그대로 활용.
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);
        }

        private void Update()
        {
            if (consumed) return;

            transform.position += (Vector3)(direction * (speed * Time.deltaTime));

            aliveTimer += Time.deltaTime;
            if (aliveTimer >= lifetime) ReturnToPool();
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (consumed) return;

            if (faction == ProjectileFaction.HitsEnemies)
            {
                // 플레이어 발사 — 플레이어(발사 주체) 통과, 적 명중 시 피해 + 소멸.
                if (other.GetComponentInParent<PlayerCharacter>() != null) return;

                var enemy = other.GetComponentInParent<EnemyBase>();
                if (enemy != null)
                {
                    if (!enemy.IsDead)
                    {
                        // 연소를 먼저 건다 — 직격으로 죽는 적에게 불을 붙여 봐야 의미가 없고,
                        // 순서를 바꾸면 사망 처리 도중 상태이상이 붙는 경로가 생긴다.
                        if (burn.HasBurn) enemy.ApplyBurn(burn);
                        enemy.TakeDamage(damage);
                    }
                    ReturnToPool();
                    return;
                }
            }
            else
            {
                // 적 발사(기본) — 적(발사 주체·아군 적) 통과, 플레이어 명중 시 피해 + 소멸.
                if (other.GetComponentInParent<EnemyBase>() != null) return;

                var player = other.GetComponentInParent<PlayerCharacter>();
                if (player != null)
                {
                    if (!player.IsDead) player.TakeDamage(damage);
                    ReturnToPool();
                    return;
                }
            }

            // 정적 지형(트리거 아닌 콜라이더, Ground 등) 명중 → 소멸. 다른 트리거는 통과.
            if (!other.isTrigger) ReturnToPool();
        }

        private void ReturnToPool()
        {
            if (consumed) return;
            consumed = true;

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
