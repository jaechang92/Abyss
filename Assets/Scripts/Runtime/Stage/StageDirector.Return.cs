using System;
using Abyss.Runtime.Events;
using Abyss.Runtime.Flow;
using Abyss.Runtime.Run;
using Abyss.Runtime.UI;
using UnityEngine;

namespace Abyss.Runtime.Stage
{
    /// <summary>보스 연출 완료 → 기존 보상 → 최종 귀환문 직접 사용 → 기존 완주 경로.</summary>
    public sealed partial class StageDirector
    {
        private static readonly Color ReturnDoorColor = new Color(0.45f, 0.85f, 0.8f);
        private bool isBossPresentationReady = true;
        private bool isBossPresentationDegraded;
        private bool isReturnCommitted;
        private int doorTransitionTicket;
        private int pendingReturnDoorGeneration = -1;
        private RoomExitDoor returnDoor;
        private BossPresenter.DeathPresentationTicket roomDeathTicket;

        public int RoomGeneration => roomEntryVersion;
        public bool IsBossPresentationDegraded => isBossPresentationDegraded;

        internal bool OwnsRoomGeneration(RoomData room, int generation) =>
            this != null && isActiveAndEnabled && !isRoomSessionClosed && currentRoom == room &&
            roomEntryVersion == generation && RunManager.HasInstance && RunManager.Instance.IsRunActive &&
            (!SceneFlowController.HasInstance || !SceneFlowController.Instance.IsLoading);

        private bool IsFinalMapStep() => currentRoom != null && currentRoom.IsMapRoom && CurrentStage != null &&
            sequence != null && currentStageIndex == sequence.stages.Count - 1 &&
            currentStepIndex == CurrentStage.steps.Count - 1;

        private void ResetReturnForRoom()
        {
            isReturnCommitted = false;
            isBossPresentationReady = true;
            isBossPresentationDegraded = false;
            roomDeathTicket = null;
            returnDoor = null;
            pendingReturnDoorGeneration = -1;
        }

        private bool TryWaitForBossPresentation(RoomData room)
        {
            if (room.roomType != RoomType.Boss && !hasRoomBossEnemy) return false;
            isBossPresentationReady = false;
            _ = WaitForBossPresentationAsync(room, roomEntryVersion);
            return true;
        }

        private async Awaitable WaitForBossPresentationAsync(RoomData room, int generation)
        {
            try
            {
                // 처치 이벤트 구독 순서에 의존하지 않는다. 같은 룸에서 생성된 ticket만 캡처한다.
                await Awaitable.NextFrameAsync(destroyCancellationToken);
                if (!OwnsRoomGeneration(room, generation)) return;
                var ticket = BossPresenter.CaptureDeathPresentation(this, room, generation);
                roomDeathTicket = ticket;
                if (ticket == null)
                {
                    isBossPresentationDegraded = true;
                    Debug.LogWarning($"[StageDirector] {room.roomId} 처치 연출 ticket 미배선 — 기존 진행 정책 적용, 연출 인수 결함");
                }
                while (ticket != null && !ticket.IsCompleted)
                {
                    if (!OwnsRoomGeneration(room, generation)) return;
                    if (ticket.IsCancelled)
                    {
                        if (!ticket.IsPresenterDeactivated) return;
                        if (TryUseInterruptedPresentationFallback(room, generation, ticket)) return;
                        // 같은 활성 방에서 아직 정리 중이면 완료 조건을 다음 프레임 다시 확인한다.
                        await Awaitable.NextFrameAsync(destroyCancellationToken);
                        continue;
                    }
                    await Awaitable.NextFrameAsync(destroyCancellationToken);
                }
                if (!OwnsRoomGeneration(room, generation) || ticket != null && ticket.IsCancelled) return;
                isBossPresentationReady = true;
                FinishRoomReward(room);
            }
            catch (OperationCanceledException) { }
        }

        private bool TryUseInterruptedPresentationFallback(RoomData room, int generation, BossPresenter.DeathPresentationTicket ticket)
        {
            if (!ticket.IsPresenterDeactivated || !OwnsRoomGeneration(room, generation) || !isRoomClearing ||
                activeEnemies.Count > 0 || telegraphs.Count > 0 || pendingReinforcements.Count > 0) return false;
            // 같은 활성 방의 표현 컴포넌트 중단만 기존 진행으로 복구한다. 실제 연출 완료로 기록하지 않는다.
            isBossPresentationDegraded = true;
            roomDeathTicket = null;
            isBossPresentationReady = true;
            Debug.LogWarning($"[StageDirector] {room.roomId} Presenter 중단 — 처치 연출 미완료(degraded), 보상 진행 복구");
            FinishRoomReward(room);
            return true;
        }

        private void FinishRoomReward(RoomData room)
        {
            if (!OwnsRoomGeneration(room, roomEntryVersion) || !isRoomClearing || !IsBossPresentationReady()) return;
            if (TryOpenRewardAltar(room)) return;
            ContinueAfterRoom();
        }

        private bool IsBossPresentationReady() => isBossPresentationReady &&
            (roomDeathTicket == null || roomDeathTicket.IsCompleted && !roomDeathTicket.IsCancelled);

        private bool CanUseReturnDoor(int generation) => OwnsRoomGeneration(currentRoom, generation) &&
            IsFinalMapStep() && isRoomClearing && !isReturnCommitted && !isRoomGateHeld &&
            (rewardSession == null || rewardSession.IsResolved && !rewardSession.IsInvalidated) &&
            IsBossPresentationReady() && !isRunModalOpen && !isRunPaused && !IsDraftSessionActive() &&
            !SaveStatusOverlay.IsCapturingInput && !MenuButtonNavigation.IsUpperOverlayOpen &&
            !SettingsPanel.WasClosedThisFrame && !CodexPanel.WasClosedThisFrame &&
            !ShopRoomPanel.IsOpen && !EventRoomPanel.IsOpen && !NodeMapPanel.IsOpen && Time.timeScale > 0f &&
            (!SceneFlowController.HasInstance || !SceneFlowController.Instance.IsLoading);

        private bool TryOpenReturnDoor(float half)
        {
            if (!IsFinalMapStep()) return false;
            // 최종 맵 방은 완료 조건이 덜 충족돼도 자동 진행으로 폴백하지 않는다.
            if (returnDoor != null) return true;
            if (!CanUseReturnDoor(roomEntryVersion))
            {
                if (pendingReturnDoorGeneration != roomEntryVersion)
                {
                    pendingReturnDoorGeneration = roomEntryVersion;
                    _ = WaitToOpenReturnDoorAsync(half, roomEntryVersion);
                }
                return true;
            }
            int generation = roomEntryVersion;
            float x = half - DOOR_RIGHT_INSET;
            returnDoor = RoomExitDoor.Create(ResolveMapRoot(), new Vector3(x, ProbeFloorY(x), 0f), "ExitDoor_Return",
                "▲  귀환", "엔딩으로", ReturnDoorColor, () => HandleReturnDoorChosen(generation), isReturnDoor: true);
            exitDoors.Add(returnDoor);
            Debug.Log($"[StageDirector] {currentRoom.roomId} 최종 보상 해결 — 귀환문 직접 사용 대기");
            return true;
        }

        private async Awaitable WaitToOpenReturnDoorAsync(float half, int generation)
        {
            try
            {
                while (OwnsRoomGeneration(currentRoom, generation) && !isReturnCommitted && returnDoor == null)
                {
                    await Awaitable.NextFrameAsync(destroyCancellationToken);
                    if (!OwnsRoomGeneration(currentRoom, generation)) return;
                    if (!CanUseReturnDoor(generation)) continue;
                    TryOpenReturnDoor(half);
                    return;
                }
            }
            catch (OperationCanceledException) { }
            finally
            {
                if (this != null && roomEntryVersion == generation && pendingReturnDoorGeneration == generation)
                    pendingReturnDoorGeneration = -1;
            }
        }

        private void HandleReturnDoorChosen(int generation)
        {
            if (isDoorTransitioning || !CanUseReturnDoor(generation))
            {
                RestoreReturnDoor(generation);
                return;
            }
            _ = ReturnThroughDoorAsync(generation);
        }

        private void RestoreReturnDoor(int generation)
        {
            if (!OwnsRoomGeneration(currentRoom, generation) || isReturnCommitted) return;
            if (returnDoor != null) returnDoor.RestoreUse();
        }

        private int BeginDoorTransition()
        {
            isDoorTransitioning = true;
            return ++doorTransitionTicket;
        }

        private void EndDoorTransition(int ticket)
        {
            if (this != null && ticket == doorTransitionTicket) isDoorTransitioning = false;
        }

        private async Awaitable ReturnThroughDoorAsync(int generation)
        {
            int ticket = BeginDoorTransition();
            var fader = ResolveFader();
            var token = destroyCancellationToken;
            try
            {
                if (fader != null) await fader.FadeOutAsync();
                token.ThrowIfCancellationRequested();
                if (ticket == doorTransitionTicket && CanUseReturnDoor(generation))
                {
                    // 발행 전 latch. StageCleared 구독자의 재진입도 public 진행 경로가 받지 않는다.
                    isReturnCommitted = true;
                    CancelInvoke();
                    AdvanceToNextRoom();
                }
                else RestoreReturnDoor(generation);

                if (ticket == doorTransitionTicket && fader != null &&
                    (!SceneFlowController.HasInstance || !SceneFlowController.Instance.IsLoading))
                    await fader.FadeInAsync();
            }
            catch (OperationCanceledException) { RestoreReturnDoor(generation); }
            finally { EndDoorTransition(ticket); }
        }
    }
}
