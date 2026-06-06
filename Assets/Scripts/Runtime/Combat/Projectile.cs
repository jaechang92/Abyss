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

        /// <summary>
        /// 발사 초기화. prefabRef는 풀 반환 키로 사용(EnemyData.projectilePrefab 원본).
        /// </summary>
        public void Launch(Vector2 dir, int dmg, float spd, float life, Projectile prefab)
        {
            direction = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector2.right;
            damage = dmg;
            speed = spd;
            lifetime = life;
            prefabRef = prefab;
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

            // 적(발사 주체·아군 적) 통과.
            if (other.GetComponentInParent<EnemyBase>() != null) return;

            // 플레이어 명중 → 데미지 + 소멸.
            var player = other.GetComponentInParent<PlayerCharacter>();
            if (player != null)
            {
                if (!player.IsDead) player.TakeDamage(damage);
                ReturnToPool();
                return;
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
