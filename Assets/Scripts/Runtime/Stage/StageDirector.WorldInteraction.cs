using System;
using Abyss.Runtime.Draft;
using Abyss.Runtime.Events;
using Abyss.Runtime.UI;
using UnityEngine;

namespace Abyss.Runtime.Stage
{
    /// <summary>
    /// 월드 상호작용 이벤트 방(R1 — 21-room-reward-flow §4·5). <see cref="RoomData.interactionMode"/>가
    /// <see cref="RoomInteractionMode.WorldInteraction"/>인 맵 이벤트 방만 이 길을 탄다. 나머지는 기존(legacy) 자동 모달 그대로다.
    ///
    /// 흐름: 클리어(비전투는 진입 즉시) → 맵 가운데에 오브젝트 → 접근·G → 기존 <see cref="EventRoomPanel"/> →
    /// [확인] → UI 닫힘 → 드래프트 → 모달이 모두 닫히면 해결 → 기존 보상 문(<see cref="ContinueAfterRoom"/>).
    ///
    /// 🔴 <b>legacy 게이트(<c>isRoomGateHeld</c>)를 잡지 않는다.</b> 인자 없는 <c>OnEventResolved</c>·폼/무기 해결 신호가
    /// 이 방의 출구를 열 수 없게 하려는 것이다. 완료는 <see cref="RoomInteractionSession"/>과 방 진입 세대로만 받는다.
    /// 런 종료·치트 이동·디렉터 비활성화(<c>CloseRoomSession</c>)는 세대를 올리므로 늦은 콜백은 버려진다 — 자동 해결은 없다.
    /// </summary>
    public sealed partial class StageDirector
    {
        private RoomInteractionSession worldSession;
        private RoomEventInteractable worldEventObject;

        // 런 모달(DraftOpen 차용 — 이벤트·상점·드래프트·폼 교체 등)과 일시정지. FSM 전이와 같은 신호를 그대로 비춘다.
        private bool isRunModalOpen;
        private bool isRunPaused;

        private DraftSessionController cachedDraftSession;
        private bool hasSearchedDraftSession;

        /// <summary>월드 상호작용 입력을 막아야 하는가 — 모달·정지·드래프트 세션·문 전환 중.</summary>
        private bool IsWorldInputBlocked => isRunModalOpen || isRunPaused || isDoorTransitioning || IsDraftSessionActive();

        private void SubscribeWorldInteraction(bool isSubscribe)
        {
            if (isSubscribe)
            {
                GameEvents.OnDraftOpened += HandleWorldModalOpened;
                GameEvents.OnDraftClosed += HandleWorldModalClosed;
                GameEvents.OnGamePaused += HandleWorldPaused;
                GameEvents.OnGameResumed += HandleWorldResumed;
                return;
            }

            GameEvents.OnDraftOpened -= HandleWorldModalOpened;
            GameEvents.OnDraftClosed -= HandleWorldModalClosed;
            GameEvents.OnGamePaused -= HandleWorldPaused;
            GameEvents.OnGameResumed -= HandleWorldResumed;
        }

        private void HandleWorldModalOpened() => isRunModalOpen = true;
        private void HandleWorldModalClosed() => isRunModalOpen = false;
        private void HandleWorldPaused() => isRunPaused = true;
        private void HandleWorldResumed() => isRunPaused = false;

        /// <summary>새 방 진입에서 부른다. 오브젝트 자체는 맵 루트 정리(<see cref="ClearMapObjects"/>)가 치운다.</summary>
        private void ResetWorldInteraction()
        {
            worldSession = null;
            worldEventObject = null;
        }

        /// <summary>
        /// 월드 이벤트 방이면 오브젝트를 세우고 true — 호출자는 legacy 자동 모달·자동 진행을 하지 않는다.
        /// 조건이 안 맞으면(opt-in 아님·맵 방 아님·선택지 없음) false로 legacy 길에 맡긴다(스톨보다 기존 동작이 낫다).
        /// </summary>
        private bool TryBeginWorldEvent(RoomData room)
        {
            if (room == null || room.interactionMode != RoomInteractionMode.WorldInteraction) return false;

            if (!room.IsEventRoom || !room.IsMapRoom)
            {
                Debug.LogWarning($"[StageDirector] {room.roomId} 월드 상호작용은 맵 이벤트 방만 지원 — legacy 경로로 진행");
                return false;
            }
            var data = room.eventData;
            if (data.choices == null || data.choices.Count == 0)
            {
                Debug.LogWarning($"[StageDirector] {room.roomId} 이벤트 선택지 없음 — legacy 경로로 진행");
                return false;
            }

            var session = new RoomInteractionSession(roomEntryVersion, $"{room.roomId}/{data.eventId}", room);
            worldSession = session;
            worldEventObject = RoomEventInteractable.Create(ResolveMapRoot(), new Vector3(0f, ProbeFloorY(0f), 0f),
                string.IsNullOrEmpty(data.title) ? room.ChoiceTitle : data.title, ResolveEventObjectVisual(),
                () => CanUseWorldEvent(session), () => HandleWorldEventInteract(session), data.eventId);

            Debug.Log($"[StageDirector] 월드 이벤트 준비: {session.InteractionId} — 접근·상호작용 대기(자동 개방 없음)");
            return true;
        }

        private bool IsCurrentWorldSession(RoomInteractionSession session) =>
            this != null && session != null && session == worldSession &&
            session.EntryVersion == roomEntryVersion && !isRoomSessionClosed;

        private bool CanUseWorldEvent(RoomInteractionSession session) =>
            IsCurrentWorldSession(session) && session.State == RoomInteractionState.Ready && !IsWorldInputBlocked;

        private void HandleWorldEventInteract(RoomInteractionSession session)
        {
            if (!CanUseWorldEvent(session) || !session.TryBeginPanel()) return;

            Debug.Log($"[StageDirector] 월드 이벤트 상호작용: {session.InteractionId} — 선택 UI 열기");
            if (EventRoomPanel.OpenForWorld(session.Room.eventData, () => HandleWorldEventConfirmed(session), consumed =>
            {
                if (IsCurrentWorldSession(session) && worldEventObject != null) worldEventObject.SetArtConsumed(consumed);
            })) return;

            session.CancelPanel();
            Debug.LogWarning($"[StageDirector] {session.InteractionId} 선택 UI를 열지 못했다 — 다시 상호작용 가능");
        }

        /// <summary>패널이 닫히고 드래프트 지급까지 끝난 직후(패널의 [확인]). 해결은 모달이 모두 닫힌 뒤다.</summary>
        private void HandleWorldEventConfirmed(RoomInteractionSession session)
        {
            if (!IsCurrentWorldSession(session) || !session.TryConfirm())
            {
                Debug.Log($"[StageDirector] 무효화된 월드 이벤트 확인 — 무시({session?.InteractionId})");
                return;
            }

            if (worldEventObject != null) worldEventObject.MarkResolved();
            _ = ResolveWorldEventAfterModalsAsync(session);
        }

        /// <summary>
        /// 이어진 드래프트(여러 번이면 연달아)가 모두 닫힐 때까지 프레임 단위로 기다린 뒤 해결하고 출구를 세운다.
        /// 드래프트는 닫히는 호출 안에서 다음 것을 다시 열므로 같은 프레임 판정이 아니라 다음 프레임에 본다.
        /// 정지(timeScale 0) 중에도 프레임은 돈다.
        /// </summary>
        private async Awaitable ResolveWorldEventAfterModalsAsync(RoomInteractionSession session)
        {
            try
            {
                do
                {
                    await Awaitable.NextFrameAsync(destroyCancellationToken);
                    if (!IsCurrentWorldSession(session)) return;
                }
                while (isRunModalOpen || IsDraftSessionActive());
            }
            catch (OperationCanceledException)
            {
                return;   // 디렉터 파괴(씬 전환) — 할 일 없다.
            }

            if (!session.TryResolve()) return;
            Debug.Log($"[StageDirector] 월드 이벤트 해결: {session.InteractionId} — 출구 대기");
            ContinueAfterRoom();
        }

        private bool IsDraftSessionActive()
        {
            if (cachedDraftSession == null && !hasSearchedDraftSession)
            {
                cachedDraftSession = FindAnyObjectByType<DraftSessionController>();
                hasSearchedDraftSession = true;
            }
            return cachedDraftSession != null && cachedDraftSession.IsSessionActive;
        }

        /// <summary>임시 표현 — 씬의 기존 제단(폼 → 무기 순) 그림을 빌린다. 없으면 null(색 사각형).</summary>
        private SpriteRenderer ResolveEventObjectVisual()
        {
            var source = formAltar != null ? formAltar.GetComponentInChildren<SpriteRenderer>(true) : null;
            if (source == null && weaponAltar != null) source = weaponAltar.GetComponentInChildren<SpriteRenderer>(true);
            return source;
        }
    }
}
