using System.Collections.Generic;
using Abyss.Runtime.Player;
using FSM.Core;
using UnityEngine;

namespace Abyss.Runtime.Enemy
{
    /// <summary>한 FSM을 공유하는 환경형 기반. 종별 상태와 물리는 후속 파생형이 구현한다.</summary>
    public abstract partial class EnvironmentalEnemyBase : EnemyBase
    {
        protected const string EXPOSED_WAIT_STATE = "Environmental.ExposedWait";
        protected const string RECOVERY_STATE = "Environmental.Recovery";
        private const int QUERY_CAPACITY = 64;

        [SerializeField] private EnvironmentalEnemyProfile profile;
        [SerializeField] private Collider2D physicalBody;
        private readonly HashSet<PlayerCharacter> hitPlayers = new HashSet<PlayerCharacter>();
        private readonly List<GameObject> ownedMarkers = new List<GameObject>();
        private readonly RaycastHit2D[] castResults = new RaycastHit2D[QUERY_CAPACITY];
        private readonly Collider2D[] overlapResults = new Collider2D[QUERY_CAPACITY];
        private string roomId;
        private Rect roomBounds;
        private Vector2 originalPosition;
        private Vector2 accessibleFloorAnchor;
        private Vector2 safePosition;
        private Vector2 bodySize;
        private Vector2 bodyOffset;
        private int groundMask;
        private int obstacleMask;
        private bool hasContext;
        private bool isInitialized;
        private bool hasReportedMissingContext;
        private bool requiresRecovery;
        private int patternGeneration;
        private bool hasPattern;
        private bool hasStartedStrike;
        private string patternOwnerState;
        private float preparationEndsAt;
        private float strikeEndsAt;
        private float nextPatternAt;
        private float stateEnteredAt;
        private float stateDeadline;
        private bool hasPreStartDeadline;
        private int environmentStateRevision;
        private Vector2 lastProgressPosition;
        private float lastProgressAt;

        public EnvironmentalEnemyProfile Profile => profile;
        public bool IsEnvironmentInitialized => isInitialized;
        public string EnvironmentRoomId => roomId;
        protected Vector2 SafePosition => safePosition;
        protected Rect EnvironmentBounds => roomBounds;
        protected Vector2 EnvironmentBodySize => bodySize;
        protected Vector2 EnvironmentBodyOffset => bodyOffset;
        protected int EnvironmentGroundMask => groundMask;
        protected int EnvironmentObstacleMask => obstacleMask;
        protected virtual float EnvironmentRecoveryDuration => profile != null ? profile.Recovery : 1.1f;
        protected override string InitialStateId => isInitialized && CombatStateMachine != null
            && CombatStateMachine.HasState(CombatStateMachine.CurrentStateId)
                ? CombatStateMachine.CurrentStateId : EXPOSED_WAIT_STATE;
        protected bool HasLivingTarget
        {
            get
            {
                if (Target == null || !Target.gameObject.activeInHierarchy) return false;
                var player = Target.GetComponentInParent<PlayerCharacter>();
                return player != null && player.isActiveAndEnabled && !player.IsDead;
            }
        }

        protected sealed override void RegisterAdditionalStates(StateMachine machine)
        {
            base.RegisterAdditionalStates(machine);
            machine.AddState(new NamedState(EXPOSED_WAIT_STATE));
            machine.AddState(new NamedState(RECOVERY_STATE));
            RegisterEnvironmentStates(machine);
            machine.OnStateChanged += ObserveEnvironmentState;
        }

        /// <summary>지형 활성화 후 호출. floorAnchor는 룸 제작자가 지정한 접근 가능한 바닥 표면 seed다.</summary>
        public bool InitializeEnvironment(string initializedRoomId, Rect bounds, Vector2 floorAnchor,
            LayerMask solidGroundMask, LayerMask solidObstacleMask)
        {
            CancelPattern();
            isInitialized = false;
            roomId = initializedRoomId;
            roomBounds = bounds;
            accessibleFloorAnchor = floorAnchor;
            groundMask = solidGroundMask.value;
            obstacleMask = solidObstacleMask.value;
            originalPosition = transform.position;
            hasContext = true;
            if (IsDead || !isActiveAndEnabled || string.IsNullOrWhiteSpace(roomId)
                || Data == null || profile == null || physicalBody == null
                || !physicalBody.enabled || physicalBody.isTrigger || !physicalBody.transform.IsChildOf(transform)
                || Body == null || !Body.simulated || Body.bodyType != RigidbodyType2D.Dynamic)
            {
                FailInitialization("Missing roomId/EnemyData/profile/dynamic Rigidbody2D/enabled solid physicalBody.");
                return false;
            }
            Bounds bodyBounds = physicalBody.bounds;
            foreach (Collider2D collider in GetComponentsInChildren<Collider2D>())
            {
                if (collider.enabled && !collider.isTrigger) bodyBounds.Encapsulate(collider.bounds);
            }
            bodySize = bodyBounds.size;
            bodyOffset = (Vector2)bodyBounds.center - (Vector2)transform.position;
            if (!ResolveSafePosition(originalPosition, out safePosition, out string failure))
            {
                FailInitialization(failure);
                return false;
            }
            SynchronizePosition(safePosition);
            isInitialized = true;
            SetEnvironmentExposed(true);
            OnEnvironmentInitialized();
            EnterEnvironmentalState(RECOVERY_STATE, "move", EnvironmentRecoveryDuration);
            return true;
        }

        protected sealed override bool TryEvaluateSpecialTransitions(string current)
        {
            if (Time.timeScale <= 0f) return true;
            if (UsesLocalGround)
            {
                int revisionBeforeGround = environmentStateRevision;
                if (!RefreshLocalGround() || environmentStateRevision != revisionBeforeGround) return true;
            }
            if (!hasContext && !hasReportedMissingContext)
            {
                hasReportedMissingContext = true;
                originalPosition = transform.position;
                ReportFailure("InitializeEnvironment must be called after room geometry activation.");
            }
            if (!isInitialized)
            {
                CancelPattern();
                SetEnvironmentExposed(true);
                if (current != EXPOSED_WAIT_STATE) EnterEnvironmentalState(EXPOSED_WAIT_STATE, "move", 3f);
                return true;
            }
            if (!HasLivingTarget)
            {
                CancelPattern();
                SetEnvironmentExposed(true);
                if (current != EXPOSED_WAIT_STATE) EnterEnvironmentalState(EXPOSED_WAIT_STATE, "move", 3f);
                return true;
            }
            if (requiresRecovery)
            {
                requiresRecovery = false;
                EnterRecoveryInPlace();
                return true;
            }
            if (hasPattern && (!CanOwnPattern(patternGeneration) || (hasStartedStrike && Time.time >= strikeEndsAt)))
            {
                EnterRecoveryInPlace();
                return true;
            }
            if (current == EnemyStateIds.Stagger)
            {
                if (CanFinishEnvironmentStagger) EnterRecoveryInPlace();
                else if (Time.time - stateEnteredAt >= profile.StuckTimeout) RecoverToSafety();
                return true;
            }
            if (current == RECOVERY_STATE && Time.time < stateDeadline) return true;
            if (current == EXPOSED_WAIT_STATE || current == RECOVERY_STATE)
            {
                OnEnvironmentReady();
                return true;
            }
            int revision = environmentStateRevision;
            float deadline = stateDeadline;
            int generation = patternGeneration;
            bool patternWasActive = hasPattern;
            bool strikeWasActive = hasStartedStrike;
            EvaluateEnvironmentState(current, Time.time - stateEnteredAt);
            // 상한 프레임의 정상 전이를 먼저 허용하고, 콜백이 바꾼 전투 상태를 덮어쓰지 않는다.
            if (IsDead || !isActiveAndEnabled || !isInitialized || requiresRecovery || !HasLivingTarget
                || IsCombatInterruptionPending || Time.timeScale <= 0f || CombatStateMachine == null
                || !CombatStateMachine.IsRunning || environmentStateRevision != revision
                || CombatStateMachine.CurrentStateId != current || stateDeadline != deadline
                || patternGeneration != generation || hasPattern != patternWasActive
                || hasStartedStrike != strikeWasActive) return true;
            if (Time.time >= stateDeadline) RecoverToSafety();
            return true;
        }

        protected sealed override bool TryMoveSpecialState(string current, float dt)
        {
            // 발밑 지면 대기 중에는 중력 낙하를 유지한다.
            if (UsesLocalGround && !isInitialized) return true;
            if (IsCombatInterruptionPending || requiresRecovery || !isInitialized || !HasLivingTarget || Time.timeScale <= 0f
                || current == EXPOSED_WAIT_STATE)
            {
                if (IsCombatInterruptionPending || !HasLivingTarget) CancelPattern();
                StopEnvironmentMotion();
                return true;
            }
            if (current == EnemyStateIds.Stagger) { MoveEnvironmentStagger(dt); return true; }
            if (current == RECOVERY_STATE) { MoveEnvironmentRecovery(dt); return true; }
            if (hasPattern && (!CanOwnPattern(patternGeneration) || (hasStartedStrike && Time.time >= strikeEndsAt)))
            {
                EnterRecoveryInPlace();
                return true;
            }
            if (ExpectsEnvironmentMovement(current))
            {
                if (Vector2.Distance(Body.position, lastProgressPosition) >= profile.MovementProgressDistance)
                {
                    lastProgressPosition = Body.position;
                    lastProgressAt = Time.time;
                }
                else if (Time.time - lastProgressAt >= profile.StuckTimeout)
                {
                    RecoverToSafety();
                    return true;
                }
            }
            MoveEnvironmentState(current, dt);
            return true;
        }

        protected void EnterEnvironmentalState(string stateId, string rowId, float duration)
        {
            if (CombatStateMachine == null || !CombatStateMachine.HasState(stateId))
            {
                ReportFailure($"Unregistered environmental state: {stateId}.");
                CancelPattern();
                return;
            }
            if (hasPattern && patternOwnerState != stateId) CancelPattern();
            float bounded = EnvironmentalEnemyProfile.IsFinite(duration) ? Mathf.Clamp(duration, 0.01f, 3f) : 3f;
            CombatStateMachine.ForceTransitionTo(stateId);
            stateDeadline = Time.time + bounded;
            hasPreStartDeadline = !CombatStateMachine.IsRunning;
            if (!string.IsNullOrEmpty(rowId)) BeginStatePresentation(rowId);
        }

        protected int BeginPattern(string rowId, float preparation)
        {
            CancelPattern();
            if (!isInitialized || requiresRecovery || Time.time < nextPatternAt || !HasLivingTarget
                || IsDead || !isActiveAndEnabled || Time.timeScale <= 0f
                || IsCombatInterruptionPending
                || CombatStateMachine == null || !CombatStateMachine.IsRunning
                || CombatStateMachine.CurrentStateId == EnemyStateIds.Stagger) return -1;
            hasPattern = true;
            patternOwnerState = CombatStateMachine.CurrentStateId;
            preparation = EnvironmentalEnemyProfile.IsFinite(preparation) ? Mathf.Clamp(preparation, 0.1f, 3f) : 0.6f;
            preparationEndsAt = Time.time + preparation;
            EndStatePresentation();
            BeginPatternPresentation(rowId, preparation);
            return patternGeneration;
        }

        /// <summary>예고 이후 1회만 개방. 방향/경로는 파생형이 예고 시 확정하며 타격 때 재조준하지 않는다.</summary>
        protected bool BeginStrike(int generation, string strikeState, float duration)
        {
            if (!CanOwnPattern(generation) || hasStartedStrike || Time.time < preparationEndsAt
                || !CombatStateMachine.HasState(strikeState)) return false;
            patternOwnerState = strikeState;
            CombatStateMachine.ForceTransitionTo(strikeState);
            if (!CanOwnPattern(generation)) return false;
            hasStartedStrike = true;
            float bounded = EnvironmentalEnemyProfile.IsFinite(duration) ? Mathf.Clamp(duration, 0.01f, 1.2f) : 0.01f;
            strikeEndsAt = Time.time + bounded;
            float cooldown = Data != null && EnvironmentalEnemyProfile.IsFinite(Data.attackCooldown)
                ? Mathf.Max(0f, Data.attackCooldown) : 1.5f;
            nextPatternAt = Time.time + cooldown;
            stateDeadline = strikeEndsAt;
            StrikePatternPresentation(faceTarget: false);
            PlayPatternAttackSfx();
            return true;
        }

        protected bool IsPatternCurrent(int generation) => CanOwnPattern(generation);

        protected bool TryGetCeilingPlacement(out Vector2 ceilingRoot, out string failure)
        {
            ceilingRoot = safePosition;
            failure = "Environment must be initialized before resolving a ceiling.";
            return isInitialized && EnvironmentalSpawnResolver.TryResolveCeiling(roomBounds, safePosition,
                bodySize, bodyOffset, groundMask, obstacleMask, transform, profile, out ceilingRoot, out failure);
        }

        protected void OwnPatternMarker(GameObject marker)
        {
            if (marker == null) return;
            if (!hasPattern) { marker.SetActive(false); Destroy(marker); return; }
            if (!ownedMarkers.Contains(marker)) ownedMarkers.Add(marker);
        }

        /// <summary>세대당 플레이어 1회. 상자 전체 이동 경로를 지형 앞에서 잘라 단일 피해 경로로 보낸다.</summary>
        protected int ApplyPatternSweep(int generation, Vector2 fromCenter, Vector2 toCenter, Vector2 size)
        {
            if (!CanOwnPattern(generation) || !hasStartedStrike || Time.time >= strikeEndsAt
                || !EnvironmentalSpawnResolver.IsFinite(fromCenter) || !EnvironmentalSpawnResolver.IsFinite(toCenter)
                || !EnvironmentalSpawnResolver.IsFinite(size) || size.x <= 0f || size.y <= 0f) return 0;
            Vector2 travel = toCenter - fromCenter;
            float distance = travel.magnitude;
            Vector2 direction = distance > 0.0001f ? travel / distance : Vector2.right;
            var terrainFilter = new ContactFilter2D { useLayerMask = true, layerMask = obstacleMask, useTriggers = false };
            int terrainCount = Physics2D.BoxCast(fromCenter, size, 0f, direction, terrainFilter, castResults, distance);
            if (terrainCount >= castResults.Length) return 0;
            float cappedDistance = distance;
            for (int i = 0; i < terrainCount; i++)
            {
                Collider2D collider = castResults[i].collider;
                if (collider == null || collider.transform.IsChildOf(transform)) continue;
                if (castResults[i].distance <= 0f) return 0;
                cappedDistance = Mathf.Min(cappedDistance, Mathf.Max(0f, castResults[i].distance - profile.FloorSkin));
            }
            var playerFilter = new ContactFilter2D { useLayerMask = false, useTriggers = true };
            int count;
            if (distance > 0.0001f)
            {
                count = Physics2D.BoxCast(fromCenter, size, 0f, direction, playerFilter, castResults, cappedDistance);
                if (count >= castResults.Length) return 0;
                for (int i = 0; i < count; i++) overlapResults[i] = castResults[i].collider;
            }
            else
            {
                count = Physics2D.OverlapBox(fromCenter, size, 0f, playerFilter, overlapResults);
                if (count >= overlapResults.Length) return 0;
            }
            int hits = 0;
            for (int i = 0; i < count; i++)
            {
                if (!CanOwnPattern(generation) || Time.time >= strikeEndsAt) break;
                var player = overlapResults[i] != null ? overlapResults[i].GetComponentInParent<PlayerCharacter>() : null;
                if (player == null || !player.isActiveAndEnabled || player.IsDead || hitPlayers.Contains(player)
                    || !HasClearDamageLine(overlapResults[i], terrainFilter)) continue;
                hitPlayers.Add(player);
                player.TakeDamage(AttackDamage, transform.position);
                hits++;
            }
            return hits;
        }

        private bool HasClearDamageLine(Collider2D playerCollider, ContactFilter2D filter)
        {
            Vector2 origin = physicalBody.bounds.center;
            Vector2 delta = playerCollider.ClosestPoint(origin) - origin;
            int count = Physics2D.Raycast(origin, delta.normalized, filter, castResults, delta.magnitude);
            if (count >= castResults.Length) return false;
            for (int i = 0; i < count; i++)
            {
                Collider2D collider = castResults[i].collider;
                if (collider != null && !collider.transform.IsChildOf(transform)) return false;
            }
            return true;
        }

        protected void CancelPattern()
        {
            if (hasPattern) patternGeneration++;
            hasPattern = false;
            hasStartedStrike = false;
            patternOwnerState = null;
            hitPlayers.Clear();
            for (int i = 0; i < ownedMarkers.Count; i++)
            {
                if (ownedMarkers[i] == null) continue;
                ownedMarkers[i].SetActive(false);
                Destroy(ownedMarkers[i]);
            }
            ownedMarkers.Clear();
            CancelPatternPresentation();
            EndStatePresentation();
            StopEnvironmentMotion();
        }

        protected void RecoverToSafety()
        {
            CancelPattern();
            SetEnvironmentExposed(true);
            if (UsesLocalGround)
            {
                if (RefreshLocalGround()) EnterRecoveryInPlace();
                return;
            }
            if (!isInitialized) return;
            if (!ResolveSafePosition(safePosition, out Vector2 resolved, out string failure))
            {
                FailInitialization($"Recovery failed: {failure}");
                return;
            }
            safePosition = resolved;
            SynchronizePosition(safePosition);
            OnEnvironmentRecovered(wasRelocated: true);
            EnterEnvironmentalState(RECOVERY_STATE, "move", EnvironmentRecoveryDuration);
        }

        protected void EnterRecoveryInPlace()
        {
            CancelPattern();
            SetEnvironmentExposed(true);
            if (!isInitialized) return;
            if (!CanRecoverInPlace) { RecoverToSafety(); return; }
            OnEnvironmentRecovered(wasRelocated: false);
            EnterEnvironmentalState(RECOVERY_STATE, "move", EnvironmentRecoveryDuration);
        }

        private bool ResolveSafePosition(Vector2 desired, out Vector2 resolved, out string failure)
        {
            return EnvironmentalSpawnResolver.TryResolve(roomBounds, desired, accessibleFloorAnchor, bodySize,
                bodyOffset, groundMask, obstacleMask, transform, profile, out resolved, out failure);
        }

        private bool CanOwnPattern(int generation) => hasPattern && generation == patternGeneration
            && isInitialized && !requiresRecovery && !IsDead && isActiveAndEnabled && Time.timeScale > 0f
            && physicalBody != null && physicalBody.enabled && !physicalBody.isTrigger
            && HasLivingTarget && !IsCombatInterruptionPending
            && CombatStateMachine != null && CombatStateMachine.IsRunning
            && CombatStateMachine.CurrentStateId == patternOwnerState;

        private void ObserveEnvironmentState(string previous, string current)
        {
            environmentStateRevision++;
            if (hasPattern && current != patternOwnerState) CancelPattern();
            stateEnteredAt = Time.time;
            // Start 이전 초기화가 설정한 기한은 같은 초기 상태의 재진입에서도 보존한다.
            bool isStartingInitializedState = hasPreStartDeadline && isInitialized
                && CombatStateMachine.IsRunning && previous == current;
            if (!isStartingInitializedState) stateDeadline = Time.time + 3f;
            if (CombatStateMachine.IsRunning) hasPreStartDeadline = false;
            lastProgressAt = Time.time;
            lastProgressPosition = Body != null ? Body.position : (Vector2)transform.position;
        }

        private void SynchronizePosition(Vector2 position)
        {
            StopEnvironmentMotion();
            Body.position = position;
            transform.position = new Vector3(position.x, position.y, transform.position.z);
        }

        private void FailInitialization(string failure)
        {
            isInitialized = false;
            CancelPattern();
            SetEnvironmentExposed(true);
            ReportFailure(failure);
            if (CombatStateMachine != null && CombatStateMachine.IsRunning)
                EnterEnvironmentalState(EXPOSED_WAIT_STATE, "move", 3f);
        }

        private void ReportFailure(string failure)
        {
            Debug.LogError($"[EnvironmentalSpawn] roomId={roomId ?? "<unset>"} enemyId={Data?.enemyId ?? name} "
                + $"original={originalPosition} reason={failure}", this);
        }

        protected sealed override void OnCombatInterrupted() { CancelPattern(); SetEnvironmentExposed(true); }
        protected sealed override void OnCombatDisabled()
        {
            CancelPattern();
            SetEnvironmentExposed(true);
            requiresRecovery = true;
        }
        protected sealed override void OnCombatDestroyed()
        {
            CancelPattern();
            if (CombatStateMachine != null) CombatStateMachine.OnStateChanged -= ObserveEnvironmentState;
        }
        protected sealed override void OnDying() { CancelPattern(); SetEnvironmentExposed(true); }
        protected sealed override void StopDeadMotion()
        {
            CancelPattern();
            SetEnvironmentExposed(true);
            StopEnvironmentMotion();
        }

        protected virtual void StopEnvironmentMotion()
        {
            if (Body == null) return;
            Body.linearVelocity = Vector2.zero;
            Body.angularVelocity = 0f;
        }
        protected virtual void RegisterEnvironmentStates(StateMachine machine) { }
        protected virtual void OnEnvironmentInitialized() { }
        protected virtual void OnEnvironmentReady() { }
        protected virtual void OnEnvironmentRecovered(bool wasRelocated) { }
        protected virtual bool CanRecoverInPlace => physicalBody != null
            && (UsesLocalGround ? isInitialized
                : roomBounds.Contains(physicalBody.bounds.min) && roomBounds.Contains(physicalBody.bounds.max));
        protected virtual bool CanFinishEnvironmentStagger => true;
        protected virtual void MoveEnvironmentStagger(float dt) { StopEnvironmentMotion(); }
        protected virtual void MoveEnvironmentRecovery(float dt) { StopEnvironmentMotion(); }
        protected virtual void SetEnvironmentExposed(bool isExposed) { }
        protected virtual void EvaluateEnvironmentState(string current, float elapsed) { }
        protected virtual void MoveEnvironmentState(string current, float dt) { StopEnvironmentMotion(); }
        protected virtual bool ExpectsEnvironmentMovement(string current) => false;
    }
}
