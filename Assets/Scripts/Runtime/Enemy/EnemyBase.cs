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
        private int currentHp;
        private float lastAttackTime = -999f;
        private bool staggerQueued;
        private bool isDead;

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
            fsm.StartStateMachine(EnemyStateIds.Patrol);
        }

        protected virtual void Update()
        {
            if (fsm == null || !fsm.IsRunning) return;
            EvaluateTransitions();
        }

        public void TakeDamage(int amount)
        {
            if (isDead || amount <= 0) return;

            int previous = currentHp;
            currentHp = Mathf.Max(0, currentHp - amount);
            OnHpChanged?.Invoke(previous, currentHp);

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
                if (data.isElite) RunManager.Instance.NotifyEliteKilled();
            }

            GameEvents.RaiseEnemyKilled(data);
            Destroy(gameObject, 0.3f);
        }

        private void RegisterStates()
        {
            fsm.AddState(new NamedState(EnemyStateIds.Patrol, () => LogEnter(EnemyStateIds.Patrol)));
            fsm.AddState(new NamedState(EnemyStateIds.Chase, () => LogEnter(EnemyStateIds.Chase)));
            fsm.AddState(new NamedState(EnemyStateIds.Attack, () => LogEnter(EnemyStateIds.Attack)));
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

        private void OnDrawGizmosSelected()
        {
            if (data == null) return;
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, data.detectionRange);
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, data.attackRange);
        }
    }
}
