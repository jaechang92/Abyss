using Abyss.Runtime.Enemy;
using Abyss.Runtime.Feedback;
using Abyss.Runtime.Form;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Abyss.Runtime.Player
{
    /// <summary>
    /// 방패병 가드 — 강공격 키를 누르고 있으면 정면을 막고, 막으면 자동 반격한다.
    /// 기획: <c>Docs/game-design/16-shield-guard.md</c>. 판정은 순수 함수 <see cref="GuardResolver"/> 가 한다.
    ///
    /// 🔴 <b>누르고 있는 것을 입력 메시지로 받을 수 없다.</b> <c>PlayerInput</c> SendMessages 는 Button 액션의
    /// <c>canceled</c>(뗌)를 보내지 않는다(패키지 <c>PlayerInput.OnActionTriggered</c>). 그래서 매 프레임
    /// 액션 상태를 직접 읽는다. <c>.inputactions</c> 인터랙션을 바꾸면 다른 폼의 X 에도 영향이 가서 건드리지 않는다.
    /// </summary>
    public sealed partial class PlayerCharacter
    {
        private const string HEAVY_ATTACK_ACTION = "AttackHeavy";

        [Header("가드 피드백")]
        [Tooltip("가드 중 본체 틴트. 가드 자세 그림이 없는 동안 「막고 있다」를 알리는 신호")]
        [SerializeField] private Color guardTintColor = new(1f, 0.85f, 0.4f, 1f);
        [SerializeField, Range(0f, 1f)] private float guardTintStrength = 0.35f;
        [SerializeField, Min(0f)] private float guardRingRadius = 0.7f;
        [SerializeField, Min(0.01f)] private float guardRingDuration = 0.18f;
        [SerializeField] private Vector2 guardShake = new(0.06f, 0.08f);
        [SerializeField] private Color justGuardRingColor = new(1f, 0.95f, 0.6f, 1f);
        [SerializeField, Min(0f)] private float justGuardRingRadius = 1.3f;
        [SerializeField, Min(0f)] private float justGuardHitstop = 0.06f;
        [SerializeField] private Vector2 justGuardShake = new(0.2f, 0.15f);

        private PlayerInput cachedPlayerInput;
        private InputAction heavyAttackAction;
        private bool heavyActionLookupAttempted;

        private bool isGuarding;
        private bool wasHeavyHeld;
        private bool isJustGuardArmed;
        private float guardStartTime;
        private float lastGuardReleaseTime = -999f;
        private bool isGuardTinted;

        // 점프 · 대시로 푼 직후 X 를 누른 채라도 몇 프레임은 다시 안 든다 — 땅을 떠나기 전 판정에서 곧바로
        // 가드로 돌아가 수평 이동이 묶이는 것을 막는다.
        private const float GUARD_RESUME_BLOCK = 0.15f;
        private float guardResumeBlockedUntil;

        /// <summary>지금 막고 있는가.</summary>
        public bool IsGuarding => isGuarding;

        /// <summary>지금 막으면 저스트 가드인가.</summary>
        public bool IsJustGuardOpen =>
            isGuarding && GuardResolver.IsJustGuardOpen(isJustGuardArmed, guardStartTime, Time.time, CurrentGuardSpec.justGuardWindow);

        /// <summary>현재 폼의 가드 설정. 폼이 없으면 꺼진 값.</summary>
        public FormGuardSpec CurrentGuardSpec =>
            formController != null && formController.CurrentForm != null ? formController.CurrentForm.guard : default;

        /// <summary>현재 폼이 강공격 키를 가드로 쓰는가.</summary>
        public bool UsesGuard => CurrentGuardSpec.isEnabled;

        /// <summary>
        /// 매 프레임 가드 시작 · 해제. <c>Update</c> 에서 <b>방향 전환보다 먼저</b> 부른다 — 가드 중에는 방향이 고정된다.
        /// </summary>
        private void UpdateGuard()
        {
            bool isHeld = ReadHeavyAttackHeld();
            bool isFreshPress = isHeld && !wasHeavyHeld;
            if (!isHeld && wasHeavyHeld) lastGuardReleaseTime = Time.time;
            wasHeavyHeld = isHeld;

            bool canGuard = UsesGuard && !isDead && isGrounded && !IsDashing && Time.time >= guardResumeBlockedUntil;

            if (isGuarding)
            {
                if (!isHeld || !canGuard) EndGuard();
            }
            else if (isHeld && canGuard)
            {
                // 🔑 저스트 창은 「새로 누른」 가드에만 열린다. 누른 채 착지하거나 반격 뒤 이어지는 가드는 일반 가드다.
                BeginGuard(isFreshPress && GuardResolver.IsJustGuardArmed(
                    Time.time, lastGuardReleaseTime, CurrentGuardSpec.justGuardRearmDelay));
            }

            UpdateGuardTint();
        }

        private void BeginGuard(bool armJustGuard)
        {
            isGuarding = true;
            isJustGuardArmed = armJustGuard;
            guardStartTime = Time.time;
        }

        /// <summary>점프 · 대시 입력이 가드를 끊는다. 누른 채여도 잠깐은 다시 안 든다.</summary>
        private void BreakGuardForMovement()
        {
            if (!isGuarding) return;
            EndGuard();
            guardResumeBlockedUntil = Time.time + GUARD_RESUME_BLOCK;
        }

        /// <summary>가드를 푼다.</summary>
        private void EndGuard()
        {
            if (!isGuarding) return;
            isGuarding = false;
            isJustGuardArmed = false;
        }

        private bool ReadHeavyAttackHeld()
        {
            if (!heavyActionLookupAttempted)
            {
                heavyActionLookupAttempted = true;
                cachedPlayerInput = GetComponent<PlayerInput>();
                heavyAttackAction = cachedPlayerInput != null && cachedPlayerInput.actions != null
                    ? cachedPlayerInput.actions.FindAction(HEAVY_ATTACK_ACTION)
                    : null;
                if (heavyAttackAction == null)
                {
                    Debug.LogWarning($"[PlayerCharacter] 입력 액션 '{HEAVY_ATTACK_ACTION}' 을 못 찾아 가드를 쓸 수 없다.");
                }
            }

            // PlayerInput 이 꺼져 있으면(일시정지 · 모달) 누르고 있어도 가드가 아니다.
            if (heavyAttackAction == null || cachedPlayerInput == null || !cachedPlayerInput.inputIsActive) return false;
            return heavyAttackAction.IsPressed();
        }

        /// <summary>출처가 있는 피해가 들어왔을 때의 가드 판정(<c>TakeDamage(int, Vector2)</c> 가 부른다).</summary>
        private GuardOutcome ResolveIncomingGuard(Vector2 sourcePosition)
        {
            if (!isGuarding || !UsesGuard) return GuardOutcome.None;

            bool isFromFront = GuardResolver.IsFromFront(transform.position.x, sourcePosition.x, facingSign);
            return GuardResolver.Resolve(isGuarding, isFromFront, IsJustGuardOpen);
        }

        /// <summary>막았다 — 피드백 + 자동 반격.</summary>
        private void OnGuardSucceeded(GuardOutcome outcome)
        {
            PlayGuardFeedback(outcome);
            if (isDead) return;
            TryCounterAttack();
        }

        /// <summary>
        /// 자동 반격(16-shield-guard §4). 남발을 막는 조건 셋:
        /// ① 앞 판정 박스 안에 적이 있을 때만 — 멀리서 날아온 탄을 막고 허공을 치지 않는다
        /// ② 강공격 쿨다운 공유 ③ 판정 · 피드백 · 애니메이션은 강공격 그대로(<c>PerformAttack</c>).
        /// 피해 식도 근접 것을 탄다 — 심연 충전 · 무기 배율이 따로 할 일 없이 붙는다.
        /// </summary>
        private void TryCounterAttack()
        {
            if (!CanAttackHeavy) return;
            if (CountEnemiesInAttackBox() == 0) return;

            lastAttackHeavyTime = Time.time;
            stateMachine?.TriggerAttackHeavy();

            int damage = Mathf.RoundToInt(HeavyAttackDamage * CurrentGuardSpec.counterDamageScale);
            PerformAttack(damage, heavyHitstop, heavyShake, isHeavy: true);
        }

        private int CountEnemiesInAttackBox()
        {
            if (attackPoint == null) return 0;

            overlapFilter.useTriggers = Physics2D.queriesHitTriggers;
            int count = Physics2D.OverlapBox(attackPoint.position, attackBoxSize, 0f, overlapFilter, overlapBuffer);
            int enemies = 0;
            for (int i = 0; i < count; i++)
            {
                var col = overlapBuffer[i];
                if (col == null) continue;
                var enemy = col.GetComponentInParent<EnemyBase>();
                if (enemy != null && !enemy.IsDead) enemies++;
            }
            return enemies;
        }

        private void PlayGuardFeedback(GuardOutcome outcome)
        {
            Vector3 front = attackPoint != null ? attackPoint.position : transform.position;

            if (outcome == GuardOutcome.JustGuarded)
            {
                // 「잘했다」가 일반 가드와 분명히 달라야 한다 — 큰 링 · 짧은 멈춤 · 강한 흔들림.
                BossAreaEffect.Spawn(front, justGuardRingRadius, justGuardRingColor, guardRingDuration);
                if (HitstopController.HasInstance) HitstopController.Instance.Trigger(justGuardHitstop);
                TriggerShake(justGuardShake);
                return;
            }

            BossAreaEffect.Spawn(front, guardRingRadius, guardTintColor, guardRingDuration);
            TriggerShake(guardShake);
        }

        /// <summary>
        /// 가드 중 본체 틴트. 공격 플래시가 켜져 있는 동안은 양보한다 — 플래시가 끝나면
        /// <c>UpdateAttackFlash</c> 가 기본색으로 돌려 놓고, 다음 프레임에 여기서 다시 칠한다.
        /// </summary>
        private void UpdateGuardTint()
        {
            // 🔴 가드와 무관한 프레임에는 SpriteRenderer 를 찾지도 않는다. 첫 프레임 Update 에서 찾으면
            // FormVisualPresenter(LateUpdate)가 색을 흰색으로 걷기 전이라, 프리팹에 남은 옛 틴트가
            // 「원래 색」으로 붙잡혀 가드 · 공격 플래시가 끝날 때 그 색으로 돌아갔다(2026-09-17 사용자 발견).
            if (!isGuarding && !isGuardTinted) return;
            if (attackFlashTimer > 0f) return;

            var sr = ResolvePlayerSr();
            if (sr == null) return;

            if (isGuarding)
            {
                // 틴트를 처음 칠하는 순간의 실제 색을 원래 색으로 잡는다 — 폼 교체로 바뀐 색도 따라간다.
                if (!isGuardTinted) CaptureBaseSpriteColor(sr);
                sr.color = Color.Lerp(baseSpriteColor, guardTintColor, guardTintStrength);
                isGuardTinted = true;
            }
            else if (isGuardTinted)
            {
                sr.color = baseSpriteColor;
                isGuardTinted = false;
            }
        }
    }
}
