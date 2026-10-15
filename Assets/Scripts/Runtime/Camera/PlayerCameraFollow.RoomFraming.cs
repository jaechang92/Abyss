using Abyss.Runtime.Flow;
using Abyss.Runtime.UI;
using FSM.Core;
using UnityEngine;

namespace Abyss.Runtime.Camera
{
    /// <summary>
    /// 방 구도(C3a 보스방 · C3b 정비 공간, 22-skul-camera-character-scale §5). <c>StageDirector.Map</c>이 보스방·정비 공간에서만 명시적으로 켠다 — 로비·일반방은 C2 그대로.
    /// 두 구도는 서로 배타다(<see cref="RoomFramingMode"/>). 한쪽을 켜면 다른 쪽은 꺼진다.
    ///
    /// <list type="bullet">
    /// <item><b>입력:</b> 실제 플레이 가능 좌우(맵 방 ±mapLength/2, 옛 아레나는 바닥 충돌체 좌우)와 바닥 y.
    /// 배경 렌더 경계(<see cref="SetEnvironmentBounds"/>, 벽 여백 포함)와 별개다.</item>
    /// <item><b>화면이 방을 담으면:</b> x 방 중심 고정, y 바닥 발 앵커(runGroundAnchorY) 고정. 점프·대시·앞보기로 움직이지 않는다.</item>
    /// <item><b>못 담으면:</b> x 는 C2 안정구간·안전범위 추적(보스방만 앞보기, 정비 공간은 앞보기 항상 0), y 는 같은 바닥 앵커 고정. 줌·지형·방 폭은 바꾸지 않는다.</item>
    /// <item>적합 여부·y 는 매 프레임 현재 화면(비율·ortho)으로 다시 계산한다. 환경 경계(<see cref="ConstrainPosition"/>)가 마지막에 우선한다.</item>
    /// <item>Run 축별 추적(<see cref="RunCameraProfile"/>)일 때만 적용된다.</item>
    /// <item><b>흔들림:</b> 연출 임대·모달(게임 흐름 정지 상태·저장 모달·로비 입력 잠금) 동안 남은 흔들림을 버린다.
    /// 히트스탑(흐름은 RunActive 그대로)·슬로는 평소대로 흔든다.</item>
    /// </list>
    /// 🔑 새 방·경계 변경은 구도를 해제하고 이전 연출 임대·흔들림을 버린다 — 보스방·정비 공간이면 호출자가 다시 선언한다.
    /// 카메라 비활성은 임대·흔들림만 버리고 구도는 유지한다(같은 방에서 다시 켜질 수 있다).
    /// </summary>
    public sealed partial class PlayerCameraFollow
    {
        // 방 폭과 화면 폭이 같을 때(30 대 30) float 오차로 추적 쪽에 떨어지지 않게.
        private const float ROOM_FIT_TOLERANCE = 0.01f;

        private enum RoomFramingMode
        {
            None,
            Boss,
            Maintenance
        }

        private RoomFramingMode roomFramingMode;
        private float roomFramingMinX;
        private float roomFramingMaxX;
        private float roomFramingFloorY;

        private StateMachine gameFlowMachine;
        private bool gameFlowResolved;

        /// <summary>보스방 구도가 켜져 있는지. 정비 공간 구도는 포함하지 않는다.</summary>
        public bool IsBossFramingActive => roomFramingMode == RoomFramingMode.Boss;

        /// <summary>정비 공간 구도가 켜져 있는지.</summary>
        public bool IsMaintenanceFramingActive => roomFramingMode == RoomFramingMode.Maintenance;

        private bool IsRoomFramingActive => roomFramingMode != RoomFramingMode.None;

        private float RoomFramingCenterX => (roomFramingMinX + roomFramingMaxX) * 0.5f;

        /// <summary>
        /// 보스방 구도를 켠다(정비 공간 구도는 꺼진다). <paramref name="arenaMinX"/>~<paramref name="arenaMaxX"/>는 실제 플레이 가능 좌우(벽 안쪽),
        /// <paramref name="floorY"/>는 바닥 윗면. 경계(<see cref="SetEnvironmentBounds"/>) 뒤에 부르고, 대상 배치가 끝났으면 <see cref="SnapToTarget"/>으로 맞춘다.
        /// </summary>
        public void SetBossRoomFraming(float arenaMinX, float arenaMaxX, float floorY) =>
            SetRoomFraming(RoomFramingMode.Boss, arenaMinX, arenaMaxX, floorY);

        /// <summary>
        /// 정비 공간 구도를 켠다(보스방 구도는 꺼진다). 입력·호출 순서는 <see cref="SetBossRoomFraming"/>과 같다.
        /// 화면이 방을 담으면 고정, 못 담으면 앞보기 없는 C2 가로 추적 + 바닥 앵커 고정.
        /// </summary>
        public void SetMaintenanceRoomFraming(float minX, float maxX, float floorY) =>
            SetRoomFraming(RoomFramingMode.Maintenance, minX, maxX, floorY);

        /// <summary>방 구도를 해제한다(일반방). 이미 꺼져 있으면 아무것도 하지 않는다.</summary>
        public void ClearRoomFraming()
        {
            if (!IsRoomFramingActive) return;
            roomFramingMode = RoomFramingMode.None;
            smoothedInitialized = false;
            ResetRunMotionState();
        }

        private void SetRoomFraming(RoomFramingMode mode, float minX, float maxX, float floorY)
        {
            roomFramingMinX = Mathf.Min(minX, maxX);
            roomFramingMaxX = Mathf.Max(minX, maxX);
            roomFramingFloorY = floorY;
            roomFramingMode = mode;
            smoothedInitialized = false;
            ResetRunMotionState();
        }

        /// <summary>
        /// 대상 위치로 지금 바로 최종 구도를 맞춘다(방 진입: 대상 배치·경계·구도 설정 뒤). 다음 LateUpdate 를 기다리지 않으므로
        /// 페이드가 걷힐 때 이전 방 구도가 한 프레임 남거나 새 방으로 건너오는 모습이 보이지 않는다.
        /// 대상이 아직 없거나(카메라 Start 전) 꺼져 있으면 하지 않는다 — Start·OnEnable 이 같은 구도로 맞춘다.
        /// </summary>
        public void SnapToTarget()
        {
            if (target == null || !isActiveAndEnabled) return;
            RefreshTargetCache();
            velocity = Vector3.zero;
            if (IsRunMotionActive)
            {
                SnapRunMotion(target.position);
            }
            else
            {
                smoothedPosition = ConstrainPosition(target.position + offset);
                smoothedInitialized = true;
            }
            transform.position = ConstrainPosition(smoothedPosition);
        }

        private bool RoomFramingFitsView(float viewWidth) =>
            viewWidth + ROOM_FIT_TOLERANCE >= roomFramingMaxX - roomFramingMinX;

        private float RoomFloorAnchorY(float viewHeight) =>
            roomFramingFloorY + (0.5f - Mathf.Clamp01(runGroundAnchorY)) * viewHeight;

        // snap 기준점을 방 구도로 바꾼다 — 담기면 x 방 중심, y 는 항상 바닥 앵커. 꺼져 있으면 그대로.
        private void ApplyRoomFramingAnchor(ref Vector3 anchor, float viewHeight)
        {
            if (!IsRoomFramingActive) return;
            if (RoomFramingFitsView(viewHeight * ViewCamera.aspect)) anchor.x = RoomFramingCenterX;
            anchor.y = RoomFloorAnchorY(viewHeight);
        }

        // 새 방·경계 변경 공통 정리: 구도 해제(보스방·정비 공간이면 호출자가 다시 선언), 이전 연출 임대 무효화, 남은 흔들림·속도 제거.
        private void ResetRoomTransition()
        {
            roomFramingMode = RoomFramingMode.None;
            InvalidatePresentation();
            if (shake != null) shake.ClearShake();
            velocity = Vector3.zero;
            ResetRunMotionState();
        }

        private void OnDisable()
        {
            // 꺼지면 연출 임대·흔들림·속도를 남기지 않는다. 방 구도는 유지 — 같은 방에서 다시 켜지면 그대로 쓴다.
            InvalidatePresentation();
            if (shake != null) shake.ClearShake();
            velocity = Vector3.zero;
            ResetRunMotionState();
        }

        // ───────────────────────── 흔들림 보류 ─────────────────────────

        // 연출 임대·모달 동안은 남은 흔들림을 섞지 않고 버린다 — 끝난 뒤 옛 흔들림이 다시 재생되지 않게.
        private Vector3 ResolveShakeOffset()
        {
            if (shake == null) return Vector3.zero;
            if (IsShakeHeld)
            {
                shake.ClearShake();
                return Vector3.zero;
            }
            return shake.CurrentShakeOffset;
        }

        // timeScale 0 자체는 보지 않는다 — 히트스탑도 timeScale 0 이고, 그 흔들림은 기존 타격감이다.
        private bool IsShakeHeld => IsPresentationHeld || SaveStatusOverlay.IsModalOpen
                                    || (targetLobbyPlayer != null && targetLobbyPlayer.InputLocked)
                                    || IsGameFlowModal;

        // Run 게임 흐름이 RunActive 가 아니면(DraftOpen·Paused·Result) 모달 정지다. 로비처럼 흐름이 없으면 false.
        private bool IsGameFlowModal
        {
            get
            {
                if (!gameFlowResolved)
                {
                    var flow = FindAnyObjectByType<GameFlowController>();
                    gameFlowMachine = flow != null ? flow.GetComponent<StateMachine>() : null;
                    gameFlowResolved = true;
                }
                if (gameFlowMachine == null) return false;
                string state = gameFlowMachine.CurrentStateId;
                return !string.IsNullOrEmpty(state) && state != GameFlowStateIds.RunActive;
            }
        }
    }
}
