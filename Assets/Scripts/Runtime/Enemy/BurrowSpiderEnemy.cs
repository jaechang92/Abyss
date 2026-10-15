using Abyss.Runtime.Player;
using FSM.Core;
using UnityEngine;

namespace Abyss.Runtime.Enemy
{
    /// <summary>잠복은 그림만 바꾼다. 피해·경직·생명주기는 환경형 공통 FSM에 맡긴다.</summary>
    public sealed class BurrowSpiderEnemy : EnvironmentalEnemyBase
    {
        private const string BURIED = "BurrowSpider.Buried";
        private const string EMERGE_TELEGRAPH = "BurrowSpider.EmergeTelegraph";
        private const string EMERGE_STRIKE = "BurrowSpider.EmergeStrike";
        private const string SURFACE_CHASE = "BurrowSpider.SurfaceChase";
        private const string BURROW_TELEGRAPH = "BurrowSpider.BurrowTelegraph";
        private const float EMERGE_TIME = 0.7f;
        private const float STRIKE_TIME = 0.12f;
        private const float CHASE_TIME = 2f;
        private const float BURROW_TIME = 0.4f;
        private static readonly Vector2 StrikeSize = new Vector2(0.9f, 1f);

        [Header("임시 도형 표현 (최종 아트 아님)")]
        [SerializeField] private SpriteRenderer placeholderVisual;
        private bool isFirstBurial;
        private bool requiresTargetReturnRecovery;
        private int generation = -1;
        private Vector2 strikeCenter;
        private float strikeDirection = 1f;
        [Header("런타임 상태")]
        [SerializeField] private string runtimeStatus;

        protected override bool UsesLocalGround => true;
        protected override float EnvironmentRecoveryDuration => Mathf.Max(1.1f,
            Profile != null ? Profile.Recovery : 1.1f);

        protected override void Update()
        {
            base.Update();
            runtimeStatus = IsDead ? "Dead" : !IsEnvironmentInitialized ? "Waiting for Ground below feet"
                : !HasLivingTarget ? "Waiting for a living player" : Time.timeScale <= 0f ? "Paused"
                : CombatStateMachine.CurrentStateId;
        }

        protected override void RegisterEnvironmentStates(StateMachine machine)
        {
            machine.AddState(new NamedState(BURIED));
            machine.AddState(new NamedState(EMERGE_TELEGRAPH));
            machine.AddState(new NamedState(EMERGE_STRIKE));
            machine.AddState(new NamedState(SURFACE_CHASE));
            machine.AddState(new NamedState(BURROW_TELEGRAPH));
            // ExposedRecover는 공통 RECOVERY_STATE로 매핑한다.
        }

        protected override void OnEnvironmentInitialized()
        {
            isFirstBurial = true;
            // 초기화가 Target을 찾는 Start보다 먼저 호출돼도 실제 타깃 소실로 오인하지 않는다.
            requiresTargetReturnRecovery = false;
            Body.gravityScale = 0f;
            Body.freezeRotation = true;
        }

        protected override void OnEnvironmentRecovered(bool wasRelocated)
        {
            isFirstBurial = false;
        }

        protected override void OnEnvironmentReady()
        {
            // 타깃 소실은 공통 회복/경직 상태를 노출 대기로 바꾼다. 복귀 때 회복을 생략하지 않는다.
            if (requiresTargetReturnRecovery)
            {
                requiresTargetReturnRecovery = false;
                EnterRecoveryInPlace();
                return;
            }
            if (isFirstBurial)
            {
                isFirstBurial = false;
                EnterBuried();
                return;
            }
            SetEnvironmentExposed(true);
            EnterEnvironmentalState(SURFACE_CHASE, "move", CHASE_TIME);
        }

        protected override void EvaluateEnvironmentState(string current, float elapsed)
        {
            if (current == BURIED)
            {
                float detection = Data != null && EnvironmentalEnemyProfile.IsFinite(Data.detectionRange)
                    ? Mathf.Max(0f, Data.detectionRange) : 0f;
                if (elapsed >= Profile.BuriedWait
                    || Vector2.Distance(Body.position, Target.position) <= detection) BeginEmerge();
            }
            else if (current == EMERGE_TELEGRAPH && elapsed >= EMERGE_TIME)
            {
                if (!BeginStrike(generation, EMERGE_STRIKE, STRIKE_TIME)) EnterRecoveryInPlace();
                else SetEnvironmentExposed(true);
            }
            else if (current == SURFACE_CHASE)
            {
                if (elapsed >= CHASE_TIME || Mathf.Abs(Target.position.x - Body.position.x) <= 1f)
                {
                    BeginBurrow();
                }
            }
            else if (current == BURROW_TELEGRAPH && elapsed >= BURROW_TIME)
            {
                EnterBuried();
            }
        }

        private void EnterBuried()
        {
            StopEnvironmentMotion();
            SetEnvironmentExposed(false);
            EnterEnvironmentalState(BURIED, "buried", Mathf.Min(2.5f, Profile.BuriedWait));
        }

        private void BeginBurrow()
        {
            StopEnvironmentMotion();
            EnterEnvironmentalState(BURROW_TELEGRAPH, "burrow", BURROW_TIME);
            SetPlaceholderShape(new Vector2(0.8f, 0.4f), new Color(0.7f, 0.5f, 0.2f));
        }

        private void BeginEmerge()
        {
            StopEnvironmentMotion();
            strikeDirection = Target.position.x < Body.position.x ? -1f : 1f;
            Vector2 bodyCenter = Body.position + EnvironmentBodyOffset;
            strikeCenter = bodyCenter + new Vector2(strikeDirection
                * (EnvironmentBodySize.x * 0.5f + StrikeSize.x * 0.5f),
                -EnvironmentBodySize.y * 0.5f + Profile.FloorSkin + StrikeSize.y * 0.5f);
            EnterEnvironmentalState(EMERGE_TELEGRAPH, "emerge", EMERGE_TIME);
            generation = BeginPattern("emerge", EMERGE_TIME);
            if (generation < 0)
            {
                EnterRecoveryInPlace();
                return;
            }
            SetPlaceholderShape(new Vector2(0.8f, 0.3f), new Color(0.9f, 0.65f, 0.2f));
            if (placeholderVisual == null || placeholderVisual.sprite == null) return;
            var marker = new GameObject("EmergeTelegraph_0.9x1.0_Temporary");
            var renderer = marker.AddComponent<SpriteRenderer>();
            renderer.sprite = placeholderVisual.sprite;
            renderer.color = new Color(1f, 0.75f, 0.1f, 0.35f);
            renderer.sortingOrder = placeholderVisual.sortingOrder + 1;
            FitSprite(renderer, StrikeSize, strikeCenter);
            OwnPatternMarker(marker);
        }

        protected override void MoveEnvironmentState(string current, float dt)
        {
            StopEnvironmentMotion();
            if (current == EMERGE_STRIKE)
            {
                ApplyPatternSweep(generation, strikeCenter, strikeCenter, StrikeSize);
                return;
            }
            if (current != SURFACE_CHASE) return;
            float delta = Target.position.x - Body.position.x;
            float speed = Data != null && EnvironmentalEnemyProfile.IsFinite(Data.moveSpeed)
                ? Mathf.Clamp(Data.moveSpeed * MoveSpeedMultiplier, 0f, 3f) : 0f;
            float step = Mathf.Min(Mathf.Abs(delta), speed * dt);
            if (step <= 0f) return;
            Vector2 desired = Body.position + Vector2.right * Mathf.Sign(delta) * step;
            if (!CanMoveOnLocalGround(desired)) { BeginBurrow(); return; }
            Body.linearVelocity = new Vector2(Mathf.Sign(delta) * step / dt, 0f);
        }

        protected override bool ExpectsEnvironmentMovement(string current) => current == SURFACE_CHASE;

        protected override void SetEnvironmentExposed(bool isExposed)
        {
            if (isExposed && !HasLivingTarget) requiresTargetReturnRecovery = true;
            SetPlaceholderShape(isExposed ? new Vector2(0.8f, 0.6f) : new Vector2(0.8f, 0.22f),
                isExposed ? new Color(0.55f, 0.48f, 0.42f) : new Color(0.38f, 0.28f, 0.18f));
        }

        private void SetPlaceholderShape(Vector2 size, Color color)
        {
            if (placeholderVisual == null || placeholderVisual.sprite == null) return;
            Vector2 center = (Vector2)transform.position + new Vector2(0f, (size.y - 0.6f) * 0.5f);
            FitSprite(placeholderVisual, size, center);
            var visuals = GetComponent<EnemyVisuals>();
            if (visuals != null) visuals.SetBaseColor(color);
            else placeholderVisual.color = color;
            placeholderVisual.enabled = true;
        }

        private static void FitSprite(SpriteRenderer renderer, Vector2 size, Vector2 center)
        {
            Bounds bounds = renderer.sprite.bounds;
            if (bounds.size.x <= 0f || bounds.size.y <= 0f) return;
            Vector3 scale = new Vector3(size.x / bounds.size.x, size.y / bounds.size.y, 1f);
            renderer.transform.localScale = scale;
            renderer.transform.position = new Vector3(center.x - bounds.center.x * scale.x,
                center.y - bounds.center.y * scale.y, renderer.transform.position.z);
        }
    }
}
