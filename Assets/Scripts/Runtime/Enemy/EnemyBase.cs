using System;
using Abyss.Runtime.Events;
using Abyss.Runtime.Player;
using Abyss.Runtime.Run;
using FSM.Core;
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
    public class EnemyBase : MonoBehaviour
    {
        [SerializeField] private EnemyData data;
        [SerializeField] private Transform target;

        [Header("디버그")]
        [SerializeField] private bool logStateChanges;

        private StateMachine fsm;
        private Rigidbody2D body;
        private EnemyVisuals visuals;
        private int currentHp;
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
            fsm.StartStateMachine(EnemyStateIds.Patrol);
        }

        protected virtual void Update()
        {
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

            var player = target.GetComponent<PlayerCharacter>();
            if (player != null && !player.IsDead)
            {
                int damage = GetAttackDamage();
                player.TakeDamage(damage);
            }
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
            if (isDead || amount <= 0) return;

            int previous = currentHp;
            currentHp = Mathf.Max(0, currentHp - amount);
            OnHpChanged?.Invoke(previous, currentHp);

            visuals?.Flash();

            if (currentHp <= 0) Die();
            else staggerQueued = true;
        }

        private void Die()
        {
            if (isDead) return;
            isDead = true;

            if (data != null && RunManager.HasInstance)
            {
                RunManager.Instance.GainExp(data.expReward, data.enemyId);
                RunManager.Instance.GainGoldShards(data.goldReward);
                if (data.isBoss) RunManager.Instance.NotifyBossKilled();
                else if (data.isElite) RunManager.Instance.NotifyEliteKilled();
            }

            GameEvents.RaiseEnemyKilled(data);
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
