using Abyss.Runtime.Enemy;
using Abyss.Runtime.Player;
using ObjectPool_Core;
using UnityEngine;
using Abyss.Runtime.Feedback;

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
        /// <summary>플레이어 적중 시 출처를 진행 반대로 되짚는 거리(유닛). 가드 정면 판정용.</summary>
        private const float SOURCE_BACKTRACK = 0.5f;

        private Vector2 direction = Vector2.right;
        private int damage;
        private float speed;
        private float lifetime;
        private float aliveTimer;
        private Projectile prefabRef;
        private bool consumed;
        private ProjectileFaction faction = ProjectileFaction.HitsPlayer;
        private BurnPayload burn;
        private IProjectileHitListener hitListener;
        private bool isPiercing;
        private readonly ProjectileHitLedger<EnemyBase> ledger = new();

        // 발사 세대. 발사·풀 반환마다 오른다 — 적중 처리 도중 회수·재대여되면 옛 처리가 새 발사를 건드리지 않게 한다.
        private int shotVersion;

        /// <summary>
        /// 발사 초기화. prefabRef는 풀 반환 키로 사용(EnemyData.projectilePrefab 원본).
        /// faction은 누구를 맞힐지 결정 — 기본 HitsPlayer(적 발사체). 플레이어 스킬은 HitsEnemies.
        /// burn은 명중한 적에게 부여할 연소(기본 default = 연소 없음). 적 발사체는 이 인자를 넘기지 않아
        /// 기존 호출부가 그대로 동작한다.
        ///
        /// 🔴 <b>pierceCount · hitListener 는 기본값이 곧 기존 동작이다</b>(관통 0 = 첫 적에서 소멸 · 통지 없음).
        /// 적 발사체와 스킬 발사체가 같은 클래스를 쓰므로 인자를 추가만 하고 의미는 안 바꾼다.
        /// 둘 다 적을 맞히는 발사체(HitsEnemies)에서만 쓰인다.
        /// </summary>
        /// <param name="pierceCount">첫 적 뒤로 더 뚫고 지나갈 적 수.</param>
        /// <param name="hitListener">적중을 돌려받을 발사자. 피해를 바꿀 수 있다(심연 충전).</param>
        public void Launch(Vector2 dir, int dmg, float spd, float life, Projectile prefab,
                           ProjectileFaction faction = ProjectileFaction.HitsPlayer,
                           BurnPayload burn = default,
                           int pierceCount = 0,
                           IProjectileHitListener hitListener = null)
        {
            shotVersion++;
            this.burn = burn;
            this.hitListener = hitListener;
            isPiercing = pierceCount > 0;
            ledger.Reset(pierceCount);
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
                    // 관통 발사체는 쓰러진 적을 지나간다. 관통 0 은 기존대로 시체에서도 사라진다.
                    if (enemy.IsDead)
                    {
                        if (!isPiercing) ReturnToPool();
                        return;
                    }

                    // 이미 맞힌 적(콜라이더 여럿 · 겹친 트리거)이면 피해 없이 지나간다.
                    if (!ledger.TryRegister(enemy, out bool isFirstHit)) return;

                    // 🔴 이 적중을 낸 발사(shot)를 붙잡는다. 아래 콜백·피해는 동기적으로 런을 끝낼 수 있고(마지막 보스),
                    // 그러면 런 경계 정리가 이 탄을 회수한다 — 같은 콜백 안에서 재대여·재발사되기도 한다.
                    // 그 뒤에는 옛 적중의 이펙트·통지·반환을 이어 가지 않는다(종료가 옛 런의 일을 취소한다).
                    int shot = shotVersion;
                    var listener = hitListener;
                    int finalDamage = listener != null
                        ? listener.OnProjectileHit(damage, isFirstHit)
                        : damage;
                    if (!IsShotLive(shot)) return;

                    // 연소를 먼저 건다 — 직격으로 죽는 적에게 불을 붙여 봐야 의미가 없고,
                    // 순서를 바꾸면 사망 처리 도중 상태이상이 붙는 경로가 생긴다.
                    if (burn.HasBurn) enemy.ApplyBurn(burn);
                    if (!IsShotLive(shot)) return;
                    int previousHp = enemy.CurrentHp;
                    bool isCritical = listener is IProjectileCriticalFeedback feedback && feedback.LastHitWasCritical;
                    enemy.TakeDamage(finalDamage);
                    if (!IsShotLive(shot)) return;
                    if (isCritical && enemy != null && enemy.CurrentHp < previousHp)
                        WorldArtFx.Play("critical-impact", enemy.transform.position, 1.25f, owner: transform);

                    // 피해를 준 뒤 「누구를 맞혔나」가 필요한 발사자에게만 알린다(P04 C 표식). 풀 반환 전에 —
                    // 반환하면 청취자 참조가 비워진다.
                    if (listener is IProjectileEnemyHitListener enemyHitListener)
                    {
                        enemyHitListener.OnProjectileEnemyHit(enemy, isFirstHit);
                        if (!IsShotLive(shot)) return;
                    }

                    if (ledger.IsExhausted) ReturnToPool();
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
                    // 출처 = 탄 위치에서 진행 반대로 조금. 탄이 몸을 지나 닿아도 날아온 쪽이 정면으로 잡힌다(16-shield-guard §5).
                    // 피격이 동기적으로 런을 끝내면 정리가 이미 회수했다 — 그 사이 재대여된 새 발사를 돌려보내지 않는다.
                    int shot = shotVersion;
                    if (!player.IsDead) player.TakeDamage(damage, (Vector2)transform.position - direction * SOURCE_BACKTRACK);
                    if (IsShotLive(shot)) ReturnToPool();
                    return;
                }
            }

            // 정적 지형(트리거 아닌 콜라이더, Ground 등) 명중 → 소멸. 다른 트리거는 통과.
            if (!other.isTrigger) ReturnToPool();
        }

        /// <summary>
        /// 런 경계 정리(<see cref="Abyss.Runtime.Run.RunTransientCleanup"/>) — 피해·연소·이펙트·통지 없이 즉시 회수한다.
        /// 날아가던 탄이 풀(DontDestroyOnLoad) 아래에서 씬을 넘어 살아남지 않게 한다.
        /// 이미 회수된 탄이면 아무것도 하지 않고 false — 여러 번 불러도 풀에 두 번 반환하지 않는다.
        /// </summary>
        public bool DiscardSilently()
        {
            if (consumed || !gameObject.activeSelf) return false;
            ReturnToPool();
            return true;
        }

        /// <summary>
        /// 적중 처리를 시작한 그 발사가 아직 살아 있는가. 회수(consumed)됐거나 재대여·재발사로 세대가 바뀌었으면 거짓.
        /// </summary>
        private bool IsShotLive(int shot) => !consumed && shotVersion == shot;

        private void ReturnToPool()
        {
            if (consumed) return;
            consumed = true;
            shotVersion++;
            // 풀에 있는 동안 발사자를 붙잡지 않는다 — 다음 발사가 덮어쓰지만, 그 사이 참조가 남으면 해제가 늦는다.
            ClearBorrowerState();

            if (prefabRef != null && PoolManager.HasInstance)
            {
                PoolManager.Instance.Release(prefabRef, this);
            }
            else
            {
                // 파괴는 프레임 끝이다 — 그 사이 이동·충돌이 남지 않게 먼저 끈다.
                gameObject.SetActive(false);
                Destroy(gameObject);
            }
        }

        /// <summary>지난 발사의 청취자·연소·적중 장부를 비운다. 새 발사는 <see cref="Launch"/>가 다시 채운다.</summary>
        private void ClearBorrowerState()
        {
            hitListener = null;
            burn = default;
            isPiercing = false;
            ledger.Reset(0);
        }

        // ----- IPoolable -----
        public void OnSpawn()
        {
            shotVersion++;
            consumed = false;
        }

        public void OnDespawn()
        {
            consumed = true;
            shotVersion++;
            ClearBorrowerState();
        }
    }
}
