using Abyss.Runtime.Lobby;
using Abyss.Runtime.Player;
using Abyss.Runtime.UI;
using UnityEngine;

namespace Abyss.Runtime.Camera
{
    /// <summary>
    /// Run 축별 추적(C2, 22-skul-camera-character-scale §4). 같은 오브젝트에 켜진 <see cref="RunCameraProfile"/>이 있을 때만 쓴다.
    /// 좌표는 viewport(x 왼쪽0→오른쪽1, y 아래0→위1), 대상 위치는 발밑 원점이다.
    ///
    /// <list type="bullet">
    /// <item><b>가로:</b> 「발 x + 앞보기」 프레이밍 점이 안정 구간(0.42~0.58)을 벗어난 만큼만 SmoothDamp(0.15초), 안이면 속도 0으로 고정. 앞보기에 dead zone 을 한 번만 적용한다.</item>
    /// <item><b>앞보기:</b> 대상의 실제 가로 이동 속도(위치 변화)만 본다 — 시각 방향·공격 입력과 무관. 같은 방향 0.12초 지속 후 화면폭 6%,
    /// 전환 0.25초. 멈추면 그대로 두고, 반대 방향은 지속이 확인될 때까지 기존 방향을 유지한다.</item>
    /// <item><b>세로:</b> 지상이면 발 y 0.26 에 0.25초로 정착. 공중이면 0.25~0.60 안에서 속도 0으로 고정, 넘은 만큼만 위 0.30초/아래 0.18초로 경계까지 따라간다.</item>
    /// <item><b>우선순위:</b> 축별 추적 → 안전범위(x 0.20~0.80, y 0.18~0.82) 최소 보정 → 맵 경계. 연출·흔들림은 최종 합성에서 마지막.</item>
    /// <item><b>정지:</b> timeScale 0·저장 모달 동안 추적·앞보기 상태를 진전시키지 않는다. 새 방·순간이동 snap 은 정지 중에도 한다.</item>
    /// </list>
    /// 🔑 snap(대상 교체·경계 변경·Start·화면 크기/프로필/켜짐 변경·재활성·반 화면 폭/높이를 넘는 순간이동)은 속도·앞보기·방향 타이머·이전 대상 위치를 모두 지운다.
    /// </summary>
    public sealed partial class PlayerCameraFollow
    {
        private const float MIN_SMOOTH_TIME = 0.0001f;

        [Header("Run 추적 (같은 오브젝트에 RunCameraProfile 이 켜져 있을 때만)")]
        [SerializeField, Range(0f, 1f)] private float runDeadZoneMinX = 0.42f;
        [SerializeField, Range(0f, 1f)] private float runDeadZoneMaxX = 0.58f;
        [SerializeField, Min(0f)] private float runSmoothTimeX = 0.15f;
        [SerializeField, Min(0.01f)] private float lookAheadMinSpeed = 0.5f;
        [SerializeField, Min(0f)] private float lookAheadDelay = 0.12f;
        [SerializeField, Range(0f, 0.25f)] private float lookAheadWidthRatio = 0.06f;
        [SerializeField, Min(0f)] private float lookAheadSmoothTime = 0.25f;
        [SerializeField, Range(0f, 1f)] private float runAirBandMinY = 0.25f;
        [SerializeField, Range(0f, 1f)] private float runAirBandMaxY = 0.60f;
        [SerializeField, Min(0f)] private float runRiseSmoothTime = 0.30f;
        [SerializeField, Min(0f)] private float runFallSmoothTime = 0.18f;
        [SerializeField, Range(0f, 1f)] private float runGroundAnchorY = 0.26f;
        [SerializeField, Min(0f)] private float runGroundSmoothTime = 0.25f;
        [SerializeField, Range(0f, 1f)] private float runSafeMinX = 0.20f;
        [SerializeField, Range(0f, 1f)] private float runSafeMaxX = 0.80f;
        [SerializeField, Range(0f, 1f)] private float runSafeMinY = 0.18f;
        [SerializeField, Range(0f, 1f)] private float runSafeMaxY = 0.82f;

        private RunCameraProfile runProfile;
        private Transform cachedTarget;
        private PlayerCharacter targetPlayer;
        private LobbyPlayerController targetLobbyPlayer;

        private float runVelocityX;
        private float runVelocityY;
        private float lookAhead;
        private float lookAheadVelocity;
        private int lookAheadDirection;
        private int pendingDirection;
        private float pendingDirectionTime;
        private Vector3 previousTargetPosition;

        // 낡은 offset·속도를 남기지 않도록 화면 크기·프로필·켜짐 상태를 기억해 바뀌면 snap 한다.
        private bool lastRunMotionActive;
        private float lastViewAspect;
        private float lastViewOrtho;
        private RunCameraProfileId lastRunProfile;

        private bool IsRunMotionActive => runProfile != null && runProfile.enabled && ViewCamera != null;

        // 정지(DraftOpen·Pause 등 timeScale 0)와 timeScale 을 멈추지 않는 저장 모달, 로비 NPC·메뉴 입력 잠금(전역 정지 없음),
        // 연출 임대(Presentation) — 연출이 카메라를 쥔 동안은 연출 세기만 최종 합성한다.
        private bool IsRunMotionFrozen => Time.timeScale <= 0f || SaveStatusOverlay.IsModalOpen
                                          || (targetLobbyPlayer != null && targetLobbyPlayer.InputLocked)
                                          || IsPresentationHeld;

        // Run 은 PlayerCharacter, 로비는 LobbyPlayerController 의 접지 상태를 쓴다. 그 외 대상은 「항상 지상」(발 앵커 정착만)으로 본다.
        private bool IsTargetGrounded
        {
            get
            {
                if (targetPlayer != null) return targetPlayer.IsGrounded;
                if (targetLobbyPlayer != null) return targetLobbyPlayer.IsGrounded;
                return true;
            }
        }

        private void OnEnable()
        {
            // 꺼진 동안 대상이 멀리 갔을 수 있다. 다시 켜지면 대상 위치로 맞추고 낡은 속도·앞보기를 버린다.
            if (!IsRunMotionActive) return;
            smoothedInitialized = false;
            ResetRunMotionState();
        }

        private void RefreshTargetCache()
        {
            if (ReferenceEquals(cachedTarget, target)) return;
            cachedTarget = target;
            targetPlayer = target != null ? target.GetComponentInParent<PlayerCharacter>() : null;
            targetLobbyPlayer = target != null ? target.GetComponentInParent<LobbyPlayerController>() : null;
            ResetRunMotionState();
            if (IsRunMotionActive) smoothedInitialized = false;
        }

        // 켜짐·화면 비율·ortho·프로필 중 하나라도 바뀌었으면 true. 현재 값을 기록한다.
        private bool ConsumeRunViewChange(bool runMotion)
        {
            bool changed = runMotion != lastRunMotionActive;
            if (runMotion)
            {
                var camera = ViewCamera;
                changed |= camera.aspect != lastViewAspect
                           || camera.orthographicSize != lastViewOrtho
                           || runProfile.Profile != lastRunProfile;
            }
            RecordRunView(runMotion);
            return changed;
        }

        private void RecordRunView(bool runMotion)
        {
            lastRunMotionActive = runMotion;
            if (!runMotion) return;
            var camera = ViewCamera;
            lastViewAspect = camera.aspect;
            lastViewOrtho = camera.orthographicSize;
            lastRunProfile = runProfile.Profile;
        }

        private void ResetForViewChange()
        {
            smoothedInitialized = false;
            velocity = Vector3.zero;
            ResetRunMotionState();
        }

        private void ResetRunMotionState()
        {
            runVelocityX = 0f;
            runVelocityY = 0f;
            ClearLookAhead();
            previousTargetPosition = target != null ? target.position : Vector3.zero;
        }

        // 앞보기 값·속도·확정 방향·대기 방향 타이머를 모두 지운다.
        private void ClearLookAhead()
        {
            lookAhead = 0f;
            lookAheadVelocity = 0f;
            lookAheadDirection = 0;
            pendingDirection = 0;
            pendingDirectionTime = 0f;
        }

        private void SnapRunMotion(Vector3 targetPosition)
        {
            ResetRunMotionState();
            previousTargetPosition = targetPosition;
            float viewHeight = ViewCamera.orthographicSize * 2f;
            var anchor = new Vector3(
                targetPosition.x,
                targetPosition.y + (0.5f - Mathf.Clamp01(runGroundAnchorY)) * viewHeight,
                targetPosition.z + offset.z);
            ApplyRoomFramingAnchor(ref anchor, viewHeight);
            smoothedPosition = ConstrainPosition(anchor);
            smoothedInitialized = true;
            RecordRunView(true);
        }

        private void UpdateRunMotion()
        {
            Vector3 targetPosition = target.position;
            // 정비 공간은 앞보기를 쓰지 않는다 — 정지·snap·화면 변경 경로와 무관하게 매 프레임 0 으로 둔다.
            if (IsMaintenanceFramingActive) ClearLookAhead();
            if (!smoothedInitialized)
            {
                SnapRunMotion(targetPosition);
                return;
            }

            var camera = ViewCamera;
            float viewHeight = camera.orthographicSize * 2f;
            float viewWidth = viewHeight * camera.aspect;

            // 예고 없는 순간이동(한 프레임에 반 화면 폭 또는 반 화면 높이를 넘는 변위)은 snap 한다.
            // 정지 판정보다 먼저 보므로 정지 중 순간이동도 추적·앞보기를 진전시키지 않고 바로 맞춘다.
            Vector3 displacement = targetPosition - previousTargetPosition;
            if (Mathf.Abs(displacement.x) > viewWidth * 0.5f || Mathf.Abs(displacement.y) > viewHeight * 0.5f)
            {
                SnapRunMotion(targetPosition);
                return;
            }

            if (IsRunMotionFrozen)
            {
                // 상태는 진전시키지 않는다. 재개 첫 프레임에 정지 중 변위를 이동 속도로 읽지 않도록 위치만 갱신한다.
                previousTargetPosition = targetPosition;
                return;
            }

            float deltaTime = Time.deltaTime;
            if (deltaTime <= 0f) return;

            // 방 구도(PlayerCameraFollow.RoomFraming — 보스방·정비 공간): 방이 화면에 들어오면 x 방 중심 고정(앞보기 진행 안 함),
            // 안 들어오면 x 는 아래 C2 추적(정비 공간은 앞보기 0). y 는 어느 쪽이든 바닥 발 앵커 고정 — 점프·대시로 기준 구도가 움직이지 않는다.
            bool roomFraming = IsRoomFramingActive;
            bool roomFits = roomFraming && RoomFramingFitsView(viewWidth);
            if (!roomFits && !IsMaintenanceFramingActive) UpdateLookAhead(targetPosition.x, viewWidth, deltaTime);

            var next = new Vector3(
                roomFits ? RoomFramingCenterX : FollowRunX(targetPosition.x, viewWidth, deltaTime),
                roomFraming ? RoomFloorAnchorY(viewHeight) : FollowRunY(targetPosition.y, viewHeight, deltaTime),
                targetPosition.z + offset.z);
            if (roomFits) runVelocityX = 0f;
            else next.x = KeepInSafeRange(next.x, targetPosition.x, viewWidth, runSafeMinX, runSafeMaxX);
            if (roomFraming) runVelocityY = 0f;
            else next.y = KeepInSafeRange(next.y, targetPosition.y, viewHeight, runSafeMinY, runSafeMaxY);

            // 맵 경계가 안전범위보다 우선한다. 경계에 막힌 축의 속도는 버려 반대로 돌아설 때 밀리지 않게 한다.
            Vector3 constrained = ConstrainPosition(next);
            if (constrained.x != next.x) runVelocityX = 0f;
            if (constrained.y != next.y) runVelocityY = 0f;
            smoothedPosition = constrained;
            previousTargetPosition = targetPosition;
        }

        private void UpdateLookAhead(float targetX, float viewWidth, float deltaTime)
        {
            float deltaX = targetX - previousTargetPosition.x;
            int moveDirection = 0;
            float speedX = deltaX / deltaTime;
            if (Mathf.Abs(speedX) >= Mathf.Max(lookAheadMinSpeed, 0.01f)) moveDirection = speedX > 0f ? 1 : -1;

            if (moveDirection == 0 || moveDirection == lookAheadDirection)
            {
                // 멈춤·짧은 탭은 대기 타이머만 지운다. 이미 정한 앞보기 방향은 유지한다.
                pendingDirection = 0;
                pendingDirectionTime = 0f;
            }
            else
            {
                if (moveDirection != pendingDirection)
                {
                    pendingDirection = moveDirection;
                    pendingDirectionTime = 0f;
                }
                pendingDirectionTime += deltaTime;
                if (pendingDirectionTime >= lookAheadDelay)
                {
                    lookAheadDirection = moveDirection;
                    pendingDirection = 0;
                    pendingDirectionTime = 0f;
                }
            }

            float goal = lookAheadDirection * viewWidth * Mathf.Clamp(lookAheadWidthRatio, 0f, 0.25f);
            lookAhead = Mathf.SmoothDamp(lookAhead, goal, ref lookAheadVelocity,
                SafeSmoothTime(lookAheadSmoothTime), Mathf.Infinity, deltaTime);
        }

        private float FollowRunX(float targetX, float viewWidth, float deltaTime)
        {
            float centerX = smoothedPosition.x;
            OrderedRange(runDeadZoneMinX, runDeadZoneMaxX, out float low, out float high);
            float framingX = targetX + lookAhead;
            float left = centerX + (low - 0.5f) * viewWidth;
            float right = centerX + (high - 0.5f) * viewWidth;

            // 안정 구간(경계 포함) 안이면 남은 속도로 흘러가지 않도록 속도를 버리고 그대로 둔다.
            if (framingX >= left && framingX <= right)
            {
                runVelocityX = 0f;
                return centerX;
            }

            float desired = framingX < left ? centerX + (framingX - left) : centerX + (framingX - right);
            return Mathf.SmoothDamp(centerX, desired, ref runVelocityX,
                SafeSmoothTime(runSmoothTimeX), Mathf.Infinity, deltaTime);
        }

        private float FollowRunY(float footY, float viewHeight, float deltaTime)
        {
            float centerY = smoothedPosition.y;
            float desired = centerY;
            float smooth = runRiseSmoothTime;

            if (IsTargetGrounded)
            {
                desired = footY + (0.5f - Mathf.Clamp01(runGroundAnchorY)) * viewHeight;
                smooth = runGroundSmoothTime;
            }
            else
            {
                OrderedRange(runAirBandMinY, runAirBandMaxY, out float low, out float high);
                float footViewport = (footY - centerY) / viewHeight + 0.5f;
                if (footViewport >= low && footViewport <= high)
                {
                    // band(경계 포함) 안이면 착지·상승 중 남은 속도로 흘러가지 않도록 속도를 버리고 고정한다.
                    runVelocityY = 0f;
                    return centerY;
                }

                if (footViewport > high)
                {
                    desired += (footViewport - high) * viewHeight;
                }
                else if (footViewport < low)
                {
                    desired += (footViewport - low) * viewHeight;
                    smooth = runFallSmoothTime;
                }
            }

            return Mathf.SmoothDamp(centerY, desired, ref runVelocityY,
                SafeSmoothTime(smooth), Mathf.Infinity, deltaTime);
        }

        // 대상이 안전범위 밖이면 범위 경계에 오도록 중심을 최소량만 옮긴다.
        private static float KeepInSafeRange(float center, float point, float viewSize, float min, float max)
        {
            OrderedRange(min, max, out float low, out float high);
            float viewport = (point - center) / viewSize + 0.5f;
            if (viewport < low) return center - (low - viewport) * viewSize;
            if (viewport > high) return center + (viewport - high) * viewSize;
            return center;
        }

        private static void OrderedRange(float a, float b, out float low, out float high)
        {
            low = Mathf.Clamp01(Mathf.Min(a, b));
            high = Mathf.Clamp01(Mathf.Max(a, b));
        }

        private static float SafeSmoothTime(float value) => Mathf.Max(MIN_SMOOTH_TIME, value);
    }
}
