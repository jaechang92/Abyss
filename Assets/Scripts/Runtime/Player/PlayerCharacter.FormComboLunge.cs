using Abyss.Runtime.Enemy;
using Abyss.Runtime.Form;
using Abyss.Runtime.Physics;
using UnityEngine;

namespace Abyss.Runtime.Player
{
    /// <summary>
    /// P04 <b>B 원거리 → 근접 접근</b>. 원거리 기본 공격 폼에서 근접 기본 공격 폼으로 <b>교체가 끝난 순간</b>
    /// 바라보는 쪽으로 전진 입력을 유지하고 있으면 준비 창이 열리고, 지상에서 1회 짧게 다가간다.
    ///
    /// 🔴 <b>대시가 아니다.</b> 대시 타이머 · 쿨다운 · 잔상 · 대시 상태를 건드리지 않고, 무적도 없다.
    /// 🔑 수평 속도는 <c>FixedUpdateMovement</c> 한 곳에서만 쓴다 — 대시 다음, 일반 이동 대신(<see cref="TryApplyLungeVelocity"/>).
    /// 속도로 움직이므로 벽 등 물리 충돌은 그대로 막히고, 발 앞에 땅이 없으면 멈춘다.
    /// 투척사도 원거리 폼이라 투척사 → 근접은 접근 대상이다. 근접 → 투척사(이탈)는 아무 보너스도 없다.
    /// </summary>
    public sealed partial class PlayerCharacter
    {
        private static readonly Color LungeLabelColor = new(0.55f, 0.95f, 1f, 1f);
        private const float LUNGE_INPUT_THRESHOLD = 0.5f;
        private const float LUNGE_REVERSE_THRESHOLD = 0.1f;
        private const float LUNGE_EDGE_LOOKAHEAD = 0.35f;
        // 시각 전용: 이 속도(u/s) 이상으로 실제 진행할 때만 속도선, 이 거리(u) 이상 움직였을 때만 종료 페이드.
        // 벽 · 낭떠러지에 막혀 제자리인 접근을 성공 도약처럼 보이지 않게 한다.
        private const float LUNGE_FX_MIN_SPEED = 0.5f;
        private const float LUNGE_FX_MIN_TRAVEL = 0.15f;

        private bool isLungeReady;
        private float lungeReadyEnd;
        private int lungeDirection;
        private int lungeEpoch;

        private bool isLunging;
        private float lungeEnd;
        private float lungeStartX;
        private float lungeSpeed;
        private float lungeMaxDistance;

        /// <summary>B 접근 준비 창이 열려 있는가(디버그 · 표시 조회용).</summary>
        public bool IsLungeReady => isLungeReady;

        /// <summary>B 접근 이동 중인가. 대시와 별개다(<see cref="IsDashing"/> 는 거짓).</summary>
        public bool IsLunging => isLunging;

        private static bool IsRangedToMeleeLungeOn
        {
            get { var cfg = ComboConfig; return cfg != null && cfg.isRangedToMeleeLungeEnabled; }
        }

        /// <summary>원거리 기본 공격 폼 → 다른 근접 기본 공격 폼인가.</summary>
        private static bool IsRangedToMeleeSwap(FormData previous, FormData next)
        {
            return previous != null && next != null && !IsSameForm(previous, next)
                && previous.attackStyle == FormAttackStyle.Ranged
                && next.attackStyle == FormAttackStyle.Melee;
        }

        private bool IsForwardHeld(int direction) => moveInput.x * direction > LUNGE_INPUT_THRESHOLD;

        /// <summary>FormController.OnSwapCompleted — 교체 연출까지 <b>실제로 끝난</b> 순간.</summary>
        private void HandleSwapCompletedForLunge(FormData previous, FormData next)
        {
            if (!IsRangedToMeleeLungeOn || !IsRangedToMeleeSwap(previous, next)) return;

            if (isDead)
            {
                LogCombo("B", "준비 안 됨 — 사망");
                return;
            }
            if (!IsForwardHeld(facingSign))
            {
                LogCombo("B", "준비 안 됨 — 교체 완료 때 전진 입력 없음");
                return;
            }

            CancelLunge(null);
            float window = ComboConfig.lungeReadyWindow;
            isLungeReady = true;
            lungeReadyEnd = Time.time + window;
            lungeDirection = facingSign;
            lungeEpoch = comboEpoch;
            RefreshComboPlayerLabel();
            LogCombo("B", $"접근 준비 {window:F2}s ({previous.formId} → {next.formId}, 방향 {lungeDirection})");
        }

        /// <summary>매 프레임 — 취소 조건 → (이동 중이면 시간 종료) → (준비면 지상에서 출발).</summary>
        private void UpdateLunge()
        {
            if (!isLungeReady && !isLunging) return;

            string reason = ResolveLungeCancelReason();
            if (reason != null)
            {
                CancelLunge(reason);
                return;
            }

            if (isLunging)
            {
                if (Time.time >= lungeEnd) EndLunge("시간 종료");
                else UpdateLungeMotionFx();
                return;
            }

            if (Time.time > lungeReadyEnd)
            {
                CancelLunge("창 만료(지상에 못 닿음)");
                return;
            }

            if (isGrounded) StartLunge();
        }

        private string ResolveLungeCancelReason()
        {
            if (!IsRangedToMeleeLungeOn) return "스위치 꺼짐";
            if (isDead) return "사망";
            if (lungeEpoch != comboEpoch) return "룸/런 변경";
            if (moveInput.x * lungeDirection < -LUNGE_REVERSE_THRESHOLD) return "역입력";
            if (!IsForwardHeld(lungeDirection)) return "입력 해제";
            if (IsDashing) return "대시";
            if (isGuarding) return "가드";
            if (isLunging && !isGrounded) return "공중";
            return null;
        }

        private void StartLunge()
        {
            var cfg = ComboConfig;
            isLungeReady = false;
            isLunging = true;
            lungeEnd = Time.time + cfg.lungeDuration;
            lungeStartX = body != null ? body.position.x : transform.position.x;
            lungeMaxDistance = cfg.lungeDistance;
            lungeSpeed = cfg.lungeDistance / cfg.lungeDuration;
            RefreshComboPlayerLabel();
            LogCombo("B", $"접근 시작 (최대 {lungeMaxDistance:F2}u / {cfg.lungeDuration:F2}s)");
        }

        /// <summary>이동을 마쳤다(성공 종료). 이동 거리를 남긴다.</summary>
        private void EndLunge(string reason)
        {
            if (!isLunging) return;
            isLunging = false;
            float traveled = body != null ? Mathf.Abs(body.position.x - lungeStartX) : 0f;
            LogCombo("B", $"접근 종료 — {reason} (이동 {traveled:F2}u)");

            // 실제로 움직였을 때만 짧게 흐려지며 끝낸다. 제자리였으면 즉시 지운다.
            if (traveled >= LUNGE_FX_MIN_TRAVEL && isActiveAndEnabled) ResolveComboPlayerFx().FadeTrail(lungeDirection);
            else if (comboPlayerFx != null) comboPlayerFx.ClearLunge();
        }

        /// <summary>이동 중 매 프레임 — 진행 방향 속도가 실제로 나올 때만 발밑 속도선을 켠다(물리 결과 기준).</summary>
        private void UpdateLungeMotionFx()
        {
            bool isMoving = body != null && body.linearVelocity.x * lungeDirection > LUNGE_FX_MIN_SPEED;
            if (isMoving) ResolveComboPlayerFx().SetMoving(true, lungeDirection);
            else if (comboPlayerFx != null) comboPlayerFx.SetMoving(false, lungeDirection);
        }

        /// <summary>준비 · 이동을 거둔다. <paramref name="reason"/> 이 null 이면 로그 없이 조용히.</summary>
        private void CancelLunge(string reason)
        {
            if (!isLungeReady && !isLunging) return;

            bool wasLunging = isLunging;
            isLungeReady = false;
            isLunging = false;
            RefreshComboPlayerLabel();
            if (comboPlayerFx != null) comboPlayerFx.ClearLunge();  // 취소는 페이드 없이 즉시

            if (reason != null) LogCombo("B", $"{(wasLunging ? "접근 중단" : "준비 취소")} — {reason}");
        }

        /// <summary>
        /// <c>FixedUpdateMovement</c> 가 대시 다음에 부른다. 이동 중이면 수평 속도를 여기서 정하고 <c>true</c>
        /// (일반 이동이 덮어쓰지 않게 호출자가 바로 돌아간다). 수직 속도는 건드리지 않는다.
        /// </summary>
        private bool TryApplyLungeVelocity()
        {
            if (!isLunging || body == null) return false;

            float remaining = lungeMaxDistance - Mathf.Abs(body.position.x - lungeStartX);
            if (remaining <= 0.001f)
            {
                EndLunge("최대 거리");
                return false;
            }

            if (!HasGroundAhead(lungeDirection))
            {
                EndLunge("낭떠러지 앞 정지");
                body.linearVelocity = new Vector2(0f, body.linearVelocity.y);
                return true;
            }

            // 마지막 스텝은 남은 거리만큼만 — 최대 거리를 넘지 않는다.
            float speed = Mathf.Min(lungeSpeed, remaining / Time.fixedDeltaTime);
            body.linearVelocity = new Vector2(lungeDirection * speed, body.linearVelocity.y);
            return true;
        }

        /// <summary>발 앞(진행 방향으로 조금)에 땅이 있는가. 판정 규칙은 <c>UpdateGrounded</c> 와 같다(적 · 자기 제외).</summary>
        private bool HasGroundAhead(int direction)
        {
            if (groundCheck == null) return false;

            Vector2 probe = (Vector2)groundCheck.position + new Vector2(direction * LUNGE_EDGE_LOOKAHEAD, 0f);
            int hitCount = GroundProbe.Overlap(probe, groundCheckRadius, groundLayer, groundProbeBuffer);
            for (int i = 0; i < hitCount; i++)
            {
                var col = groundProbeBuffer[i];
                if (col == null) continue;
                if (col.GetComponentInParent<EnemyBase>() != null) continue;
                if (col.transform == transform || col.transform.IsChildOf(transform)) continue;
                return true;
            }
            return false;
        }
    }
}
