using System;
using Abyss.Runtime.Audio;
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

        // 시간제 상태(공격·경직) — EnemyTimedState 참조. 동작 시간이 0 인 적은 옛 동작 그대로다.
        private float timedStateExitTime;
        private float strikeTime;
        private bool hasStruck = true;

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
            var others = FindObjectsByType<EnemyBase>();
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

            // 사거리 안에서 멈춰 있어도 대상 쪽을 본다 — 쿨다운 동안 등을 돌리고 있으면 다음 공격이 뒤로 나간다.
            float delta = target.position.x - transform.position.x;
            float dir = Mathf.Approximately(delta, 0f) ? 0f : Mathf.Sign(delta);
            visuals?.Face(dir);

            // 공격 사거리 안이면 정지하고 쿨다운 대기.
            // EvaluateTransitions는 사거리 안 + 쿨다운 중에도 Chase 상태를 유지시키므로
            // 이동 판정은 여기서 한 번 더 가드한다(거리 계산은 EvaluateTransitions과 동일한 2D 거리).
            if (Vector2.Distance(transform.position, target.position) <= data.attackRange)
            {
                StopHorizontal();
                return;
            }

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
            visuals?.Face(patrolDirection);
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

        /// <summary>지금 공격이 들어가면 줄 피해(보스 페이즈 배수 반영). 스탯 창 조회용 — 식은 <see cref="GetAttackDamage"/> 하나다.</summary>
        public int AttackDamage => GetAttackDamage();

        /// <summary>최대 HP. 초기화가 <c>data.baseHp</c> 를 그대로 쓰므로 같은 값을 읽는다.</summary>
        public int MaxHp => data != null ? data.baseHp : 0;

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
            // 사망 프레임에는 피격음 대신 사망음만 낸다 — 둘이 겹치면 마지막 타격이 뭉개진다.
            if (currentHp > 0) PlaySfx(data?.hitSfx);

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

            PlaySfx(data?.deathSfx);
            GameEvents.RaiseEnemyKilled(data, transform.position);
            // 사망 클립(부스러짐)을 보여 줄 시간. 애니메이션 없는 적은 기본값 0.3초 — 옛 동작 그대로다.
            Destroy(gameObject, data != null ? data.deathLingerDuration : 0.3f);
        }

        /// <summary>
        /// 효과음 1회 재생. <b>클립이 없으면 조용히 넘어간다</b> — 4-2 단계에서는 파일이
        /// 아직 대부분 없고, 없다고 로그를 남기면 적 하나 잡을 때마다 콘솔이 찬다.
        ///
        /// <see cref="AudioManager"/>는 <c>HasInstance</c>로 본다(자동 생성 안 함) —
        /// 부트스트랩을 안 거친 씬 단독 재생에서 소리 때문에 매니저가 생기지 않게.
        /// </summary>
        private static void PlaySfx(AudioClip clip)
        {
            if (clip == null || !AudioManager.HasInstance) return;
            AudioManager.Instance.PlaySfx(clip);
        }

        private void RegisterStates()
        {
            fsm.AddState(new NamedState(EnemyStateIds.Patrol, () => LogEnter(EnemyStateIds.Patrol)));
            fsm.AddState(new NamedState(EnemyStateIds.Chase, () => LogEnter(EnemyStateIds.Chase)));
            fsm.AddState(new NamedState(EnemyStateIds.Attack, () => { LogEnter(EnemyStateIds.Attack); BeginAttack(); }));
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

            // 경직은 시간제 게이트보다 먼저 본다 — 예비동작 중에 맞으면 공격이 끊긴다(타격 안 나감).
            if (staggerQueued)
            {
                staggerQueued = false;
                timedStateExitTime = Time.time + data.staggerDuration;
                fsm.ForceTransitionTo(EnemyStateIds.Stagger);
                return;
            }

            if (current == EnemyStateIds.Attack && EnemyTimedState.IsStrikeDue(hasStruck, Time.time, strikeTime))
            {
                Strike();
            }

            if (EnemyTimedState.IsHeld(current, Time.time, timedStateExitTime)) return;

            if (target == null)
            {
                if (current != EnemyStateIds.Patrol) fsm.ForceTransitionTo(EnemyStateIds.Patrol);
                return;
            }

            float distance = Vector2.Distance(transform.position, target.position);

            if (distance <= data.attackRange)
            {
                bool canAttack = Time.time >= lastAttackTime + data.attackCooldown;
                // 🔑 여기까지 왔으면 공격 상태라도 붙잡힌 시간이 끝났다 — 쿨다운이 공격 동작보다 짧으면
                //    Attack 에 멈춘 채 다시 못 들어가던 경로를 막는다(시간 0 인 적은 쿨다운 ≥ 0.1 이라 영향 없음).
                if (canAttack)
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
