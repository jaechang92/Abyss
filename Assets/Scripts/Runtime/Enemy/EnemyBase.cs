using System;
using Abyss.Runtime.Combat;
using Abyss.Runtime.Events;
using Abyss.Runtime.Player;
using Abyss.Runtime.Run;
using FSM.Core;
using ObjectPool_Core;
using UnityEngine;

namespace Abyss.Runtime.Enemy
{
    /// <summary>
    /// 적 공통 MonoBehaviour. FSM 5상태 등록 + HP/피격/보상 관리.
    /// 실제 이동·공격 로직은 각 상태 Enter 콜백에서 확장(후속 태스크).
    /// 엘리트 처치 시 RunManager.NotifyEliteKilled로 EliteBonus 드래프트 트리거.
    /// </summary>
    [RequireComponent(typeof(StateMachine))]
    [RequireComponent(typeof(Rigidbody2D))]
    public partial class EnemyBase : MonoBehaviour
    {
        [SerializeField] private EnemyData data;
        [SerializeField] private Transform target;

        [Header("디버그")]
        [SerializeField] private bool logStateChanges;

        private StateMachine fsm;
        private Rigidbody2D body;
        private EnemyVisuals visuals;

        [SerializeField] private int currentHp;
        private float lastAttackTime = -999f;
        private bool staggerQueued;
        private bool isDead;
        private Vector2 spawnPosition;
        private int patrolDirection = 1;

        public EnemyData Data => data;
        public Rigidbody2D Body => body;
        public Transform Target => target;
        public int CurrentHp => currentHp;
        public bool IsDead => isDead;

        public event Action<int, int> OnHpChanged;

        protected virtual void Awake()
        {
            fsm = GetComponent<StateMachine>();
            body = GetComponent<Rigidbody2D>();
            visuals = GetComponent<EnemyVisuals>();
            if (data != null) currentHp = data.baseHp;
            RegisterStates();
        }

        protected virtual void Start()
        {
            if (target == null)
            {
                var player = FindAnyObjectByType<PlayerCharacter>();
                if (player != null) target = player.transform;
            }
            spawnPosition = transform.position;

            var myCols = GetComponentsInChildren<Collider2D>(true);

            // Player ↔ Enemy 콜리전 무시 — 측면 접촉으로 인한 마찰·상승 버그 방지.
            // 공격 판정은 EvaluateTransitions가 Vector2.Distance 기반이라 데미지에는 영향 없음.
            if (target != null)
            {
                var playerCols = target.GetComponentsInChildren<Collider2D>(true);
                for (int i = 0; i < playerCols.Length; i++)
                {
                    for (int j = 0; j < myCols.Length; j++)
                    {
                        if (playerCols[i] == null || myCols[j] == null) continue;
                        Physics2D.IgnoreCollision(playerCols[i], myCols[j], true);
                    }
                }
            }

            // Enemy ↔ Enemy 콜리전 무시 — 적끼리 서로 밀어내며 이동 방해하는 문제 방지.
            // IgnoreCollision은 양방향이므로 자기 자신만 처리해도 기존 적과의 쌍이 모두 등록됨.
            var others = FindObjectsByType<EnemyBase>(FindObjectsSortMode.None);
            for (int o = 0; o < others.Length; o++)
            {
                if (others[o] == null || others[o] == this) continue;
                var otherCols = others[o].GetComponentsInChildren<Collider2D>(true);
                for (int i = 0; i < otherCols.Length; i++)
                {
                    for (int j = 0; j < myCols.Length; j++)
                    {
                        if (otherCols[i] == null || myCols[j] == null) continue;
                        Physics2D.IgnoreCollision(otherCols[i], myCols[j], true);
                    }
                }
            }

            fsm.StartStateMachine(EnemyStateIds.Patrol);
        }

        protected virtual void Update()
        {
            // 상태이상은 FSM 가동 여부와 무관하게 흘러야 한다(FSM 미시작 개체도 타면 탄다).
            UpdateBurn();

            if (fsm == null || !fsm.IsRunning) return;
            EvaluateTransitions();
        }

        protected virtual void FixedUpdate()
        {
            if (body == null || isDead) return;
            if (fsm == null || !fsm.IsRunning) return;

            string current = fsm.CurrentStateId;
            if (current == EnemyStateIds.Chase)
            {
                MoveTowardTarget();
            }
            else if (current == EnemyStateIds.Patrol)
            {
                PatrolStep();
            }
            else
            {
                StopHorizontal();
            }
        }

        private void MoveTowardTarget()
        {
            if (target == null || data == null) return;

            // 공격 사거리 안이면 정지하고 쿨다운 대기.
            // EvaluateTransitions는 사거리 안 + 쿨다운 중에도 Chase 상태를 유지시키므로
            // 이동 판정은 여기서 한 번 더 가드한다(거리 계산은 EvaluateTransitions과 동일한 2D 거리).
            if (Vector2.Distance(transform.position, target.position) <= data.attackRange)
            {
                StopHorizontal();
                return;
            }

            float delta = target.position.x - transform.position.x;
            float dir = Mathf.Approximately(delta, 0f) ? 0f : Mathf.Sign(delta);
            body.linearVelocity = new Vector2(dir * data.moveSpeed, body.linearVelocity.y);
        }

        private void StopHorizontal()
        {
            body.linearVelocity = new Vector2(0f, body.linearVelocity.y);
        }

        /// <summary>
        /// spawn 지점 기준 좌우 왕복. patrolRadius=0이면 정지 (보스 등 수동 제어 개체).
        /// </summary>
        private void PatrolStep()
        {
            if (data == null || data.patrolRadius <= 0f)
            {
                StopHorizontal();
                return;
            }

            float offsetX = transform.position.x - spawnPosition.x;
            if (offsetX >= data.patrolRadius && patrolDirection > 0) patrolDirection = -1;
            else if (offsetX <= -data.patrolRadius && patrolDirection < 0) patrolDirection = 1;

            float speed = data.moveSpeed * data.patrolSpeedMultiplier;
            body.linearVelocity = new Vector2(patrolDirection * speed, body.linearVelocity.y);
        }

        private void PerformAttack()
        {
            if (target == null || data == null) return;

            // 곡사가 우선한다. 프리팹 연결이 없으면 아래 직진탄으로 자연히 폴백되므로,
            // 배선이 빠져도 이 적이 무해해지지는 않는다.
            if (data.isRanged && data.usesArcProjectile && data.arcProjectilePrefab != null)
            {
                FireArcShell();
                return;
            }

            // 원거리 적: 발사체 발사(즉발 대신). projectilePrefab 미연결 시 근접으로 폴백.
            if (data.isRanged && data.projectilePrefab != null)
            {
                if (data.burstCount > 1) FireBurstAsync();
                else FireProjectile();
                return;
            }

            var player = target.GetComponent<PlayerCharacter>();
            if (player != null && !player.IsDead)
            {
                int damage = GetAttackDamage();
                player.TakeDamage(damage);
            }
        }

        /// <summary>
        /// 타겟 방향으로 발사체 1발 발사(원거리 적 직격용).
        /// </summary>
        private void FireProjectile()
        {
            Vector2 toTarget = (Vector2)target.position - (Vector2)transform.position;
            SpawnProjectile(toTarget);
        }

        /// <summary>
        /// 연사. 탄 사이 간격은 Coroutine 금지 규약(ADR-002)에 따라 Awaitable로 벌린다.
        ///
        /// 매 발 <see cref="FireProjectile"/>로 <b>조준을 다시 한다</b> — 첫 발 방향으로 세 발을
        /// 몰아 쏘면 옆으로 한 걸음만 움직여도 전부 빗나가 연사라는 위협이 성립하지 않는다.
        /// 대신 사이를 벌려 두었으므로 계속 움직이면 뒷발은 피할 수 있다.
        /// </summary>
        private async void FireBurstAsync()
        {
            try
            {
                int shots = Mathf.Max(1, data.burstCount);
                for (int i = 0; i < shots; i++)
                {
                    // 연사 도중 죽거나 타겟이 사라질 수 있다 — 대기 구간을 사이에 두면
                    // "그동안 세상이 바뀌었을 수 있다"를 매번 확인해야 한다.
                    if (isDead || target == null || data == null) return;

                    FireProjectile();

                    if (i < shots - 1 && data.burstInterval > 0f)
                    {
                        await Awaitable.WaitForSecondsAsync(data.burstInterval, destroyCancellationToken);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // 파괴·씬 전환으로 취소됨. 남은 탄은 쏘지 않는다.
            }
        }

        /// <summary>
        /// 곡사 폭발탄 1발. 조준점은 <b>발사 시점의 플레이어 위치</b>다 —
        /// 착탄까지 <see cref="EnemyData.arcFlightTime"/>초가 걸리므로 그 자리에 서 있으면 맞고,
        /// 움직이면 피한다. 이것이 이 적의 유일한 회피 규칙이라 예고 링과 함께 읽히게 했다.
        /// </summary>
        private void FireArcShell()
        {
            if (data.arcProjectilePrefab == null || target == null) return;

            // 자기 콜라이더 위에서 출발 — 발밑에서 나오면 발사 즉시 지형에 닿아 터진다.
            Vector2 spawnPos = (Vector2)transform.position + Vector2.up * 0.7f;

            var shell = PoolManager.Instance.Get(data.arcProjectilePrefab, (Vector3)spawnPos, Quaternion.identity);
            shell.Launch((Vector2)target.position, GetAttackDamage(),
                         data.arcFlightTime, data.arcExplosionRadius, data.arcProjectilePrefab);
        }

        /// <summary>
        /// 지정 방향으로 발사체를 풀에서 꺼내 발사하는 공용 진입점(원거리 직격·보스 탄막 공유).
        /// 자기 콜라이더와 겹치지 않도록 사거리의 일부만큼 앞에서 생성하고,
        /// Projectile 측에서도 EnemyBase를 통과 처리한다. projectilePrefab 미연결 시 무동작.
        /// 데미지는 GetAttackDamage()를 사용하므로 보스 페이즈 배율이 그대로 반영된다.
        /// </summary>
        protected void SpawnProjectile(Vector2 direction)
        {
            if (data == null || data.projectilePrefab == null) return;

            Vector2 dir = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
            Vector2 spawnPos = (Vector2)transform.position + dir * (data.attackRange * 0.3f);

            var proj = PoolManager.Instance.Get(data.projectilePrefab, (Vector3)spawnPos, Quaternion.identity);
            proj.Launch(dir, GetAttackDamage(), data.projectileSpeed, data.projectileLifetime, data.projectilePrefab);
        }

        /// <summary>
        /// 파생 보스 패턴(회전베기·꼬리치기 등)이 연출용으로 스프라이트 플래시를 트리거.
        /// 피격 시 TakeDamage가 호출하는 것과 동일한 EnemyVisuals.Flash를 공유한다.
        /// </summary>
        protected void FlashVisual()
        {
            visuals?.Flash();
        }

        /// <summary>패턴 예고(telegraph) 등에서 일정 시간 색조를 유지.</summary>
        protected void TintVisual(Color color, float duration)
        {
            visuals?.Tint(color, duration);
        }

        /// <summary>강타 모션 — 스프라이트 스케일 펀치.</summary>
        protected void PunchVisual(float magnitude, float duration)
        {
            visuals?.PunchScale(magnitude, duration);
        }

        /// <summary>
        /// 페이즈 배수 등을 반영한 실제 공격력. BossEnemy가 override.
        /// </summary>
        protected virtual int GetAttackDamage()
        {
            return data != null ? data.baseDamage : 0;
        }

        public void TakeDamage(int amount)
        {
            ApplyDamage(amount, causesStagger: true);
        }

        /// <summary>
        /// 피해 적용 공통 경로. causesStagger가 false면 HP만 깎고 FSM 경직을 걸지 않는다
        /// (연소 등 지속 피해용 — 1초마다 경직이 걸리면 DoT가 하드 CC가 된다).
        /// </summary>
        private void ApplyDamage(int amount, bool causesStagger)
        {
            if (isDead || amount <= 0) return;

            int previous = currentHp;
            currentHp = Mathf.Max(0, currentHp - amount);
            OnHpChanged?.Invoke(previous, currentHp);

            visuals?.Flash();

            if (currentHp <= 0) Die();
            else if (causesStagger) staggerQueued = true;
        }

        private void Die()
        {
            if (isDead) return;
            isDead = true;

            if (data != null && RunManager.HasInstance)
            {
                RunManager.Instance.GainExp(data.expReward, data.enemyId);
                RunManager.Instance.GainGoldShards(data.goldReward);
                if (data.IsBoss) RunManager.Instance.NotifyBossKilled();
                else if (data.IsElite) RunManager.Instance.NotifyEliteKilled();
            }

            GameEvents.RaiseEnemyKilled(data, transform.position);
            Destroy(gameObject, 0.3f);
        }

        private void RegisterStates()
        {
            fsm.AddState(new NamedState(EnemyStateIds.Patrol, () => LogEnter(EnemyStateIds.Patrol)));
            fsm.AddState(new NamedState(EnemyStateIds.Chase, () => LogEnter(EnemyStateIds.Chase)));
            fsm.AddState(new NamedState(EnemyStateIds.Attack, () => { LogEnter(EnemyStateIds.Attack); PerformAttack(); }));
            fsm.AddState(new NamedState(EnemyStateIds.Stagger, () => LogEnter(EnemyStateIds.Stagger)));
            fsm.AddState(new NamedState(EnemyStateIds.Dead, () => LogEnter(EnemyStateIds.Dead)));
        }

        private void LogEnter(string stateId)
        {
            if (logStateChanges) Debug.Log($"[Enemy:{(data != null ? data.enemyId : name)}] {stateId}");
        }

        private void EvaluateTransitions()
        {
            if (data == null) return;

            string current = fsm.CurrentStateId;

            if (isDead)
            {
                if (current != EnemyStateIds.Dead) fsm.ForceTransitionTo(EnemyStateIds.Dead);
                return;
            }
            if (current == EnemyStateIds.Dead) return;

            if (staggerQueued)
            {
                staggerQueued = false;
                fsm.ForceTransitionTo(EnemyStateIds.Stagger);
                return;
            }

            if (target == null)
            {
                if (current != EnemyStateIds.Patrol) fsm.ForceTransitionTo(EnemyStateIds.Patrol);
                return;
            }

            float distance = Vector2.Distance(transform.position, target.position);

            if (distance <= data.attackRange)
            {
                bool canAttack = Time.time >= lastAttackTime + data.attackCooldown;
                if (canAttack && current != EnemyStateIds.Attack)
                {
                    lastAttackTime = Time.time;
                    fsm.ForceTransitionTo(EnemyStateIds.Attack);
                    return;
                }
                if (!canAttack && current != EnemyStateIds.Chase)
                {
                    fsm.ForceTransitionTo(EnemyStateIds.Chase);
                }
                return;
            }

            if (distance <= data.detectionRange)
            {
                if (current != EnemyStateIds.Chase) fsm.ForceTransitionTo(EnemyStateIds.Chase);
                return;
            }

            if (current != EnemyStateIds.Patrol) fsm.ForceTransitionTo(EnemyStateIds.Patrol);
        }

        [ContextMenu("Debug: Take 10 Damage")]
        private void DebugTakeDamage()
        {
            TakeDamage(10);
        }

        private void OnDrawGizmosSelected()
        {
            if (data == null) return;
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, data.detectionRange);
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, data.attackRange);

            if (data.patrolRadius > 0f)
            {
                Gizmos.color = Color.cyan;
                Vector3 origin = Application.isPlaying ? (Vector3)spawnPosition : transform.position;
                Gizmos.DrawLine(origin + Vector3.left * data.patrolRadius, origin + Vector3.right * data.patrolRadius);
                Gizmos.DrawWireCube(origin + Vector3.left * data.patrolRadius, new Vector3(0.15f, 0.8f, 0f));
                Gizmos.DrawWireCube(origin + Vector3.right * data.patrolRadius, new Vector3(0.15f, 0.8f, 0f));
            }
        }
    }
}
