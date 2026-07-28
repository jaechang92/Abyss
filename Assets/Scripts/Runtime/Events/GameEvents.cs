using System;
using System.Collections.Generic;
using Abyss.Runtime.Draft;
using Abyss.Runtime.Enemy;
using Abyss.Runtime.Form;
using Abyss.Runtime.Stage;

namespace Abyss.Runtime.Events
{
    /// <summary>
    /// 드래프트 트리거 사유. Analyst MF-2 확정 — RunManager가 단일 발행자.
    /// </summary>
    public enum DraftTriggerReason
    {
        LevelUp,
        BossBonus,
        EliteBonus,
        RoomReward
    }

    /// <summary>
    /// 프로토 단계 정적 이벤트 버스.
    /// EA 이후 이벤트 채널이 30개를 넘기면 EventBus 클래스로 전환 (Architect §4).
    /// 발행자·구독자 매트릭스: Docs/game-design/01-gdd.md.
    /// </summary>
    public static class GameEvents
    {
        public static event Action OnRunStarted;
        public static event Action OnRunEnded;
        public static event Action OnPlayerDead;
        public static event Action<int, string> OnExpGained;
        public static event Action<int, DraftTriggerReason> OnPlayerLevelUp;
        public static event Action OnDraftOpened;
        public static event Action OnDraftClosed;
        public static event Action OnBossKilled;
        public static event Action<FormData, FormData> OnFormSwapped;
        public static event Action<int> OnGoldShardsChanged;
        public static event Action<DraftOptions> OnDraftOptionsReady;
        public static event Action<SkillData, DraftTriggerReason> OnSkillDrafted;
        public static event Action<SkillData, IReadOnlyList<SkillData>> OnDraftSlotReplaceRequested;
        public static event Action<FormData> OnFormRewardOffered;
        public static event Action OnFormRewardResolved; // 폼 보상 모달이 닫힘(획득/거절 모두) — 보상 룸 게이트 해제용
        public static event Action<EnemyData> OnEnemyKilled;
        public static event Action<RoomData> OnRoomEntered;
        public static event Action<RoomData> OnRoomCleared;
        public static event Action<StageData> OnStageCleared;

        // 일시정지는 '요청'과 '사실'을 분리한다.
        // 요청(Requested)은 입력이 발행하고, 수락 여부는 GameFlowController가 현재 상태를 보고 판정한다
        // (드래프트·결과 화면처럼 이미 정지된 상태에서는 거부된다).
        // 구독자(UI·InputRouter)는 반드시 '사실'(Paused/Resumed)을 구독해야 한다 — 요청을 구독하면
        // 거부된 요청에도 반응해 드래프트 중 게임플레이 입력이 살아나는 식의 어긋남이 생긴다.
        public static event Action OnPauseRequested;
        public static event Action OnResumeRequested;
        public static event Action OnGamePaused;
        public static event Action OnGameResumed;

        public static void RaiseRunStarted() => OnRunStarted?.Invoke();
        public static void RaiseRunEnded() => OnRunEnded?.Invoke();
        public static void RaisePlayerDead() => OnPlayerDead?.Invoke();
        public static void RaiseExpGained(int amount, string source) => OnExpGained?.Invoke(amount, source);
        public static void RaisePlayerLevelUp(int newLevel, DraftTriggerReason reason) => OnPlayerLevelUp?.Invoke(newLevel, reason);
        public static void RaiseDraftOpened() => OnDraftOpened?.Invoke();
        public static void RaiseDraftClosed() => OnDraftClosed?.Invoke();
        public static void RaiseBossKilled() => OnBossKilled?.Invoke();
        public static void RaiseFormSwapped(FormData previous, FormData next) => OnFormSwapped?.Invoke(previous, next);
        public static void RaiseGoldShardsChanged(int newAmount) => OnGoldShardsChanged?.Invoke(newAmount);
        public static void RaiseDraftOptionsReady(DraftOptions options) => OnDraftOptionsReady?.Invoke(options);
        public static void RaiseSkillDrafted(SkillData skill, DraftTriggerReason reason) => OnSkillDrafted?.Invoke(skill, reason);
        public static void RaiseDraftSlotReplaceRequested(SkillData incoming, IReadOnlyList<SkillData> currentActives) => OnDraftSlotReplaceRequested?.Invoke(incoming, currentActives);
        public static void RaiseFormRewardOffered(FormData incoming) => OnFormRewardOffered?.Invoke(incoming);
        public static void RaiseFormRewardResolved() => OnFormRewardResolved?.Invoke();
        public static void RaiseEnemyKilled(EnemyData data) => OnEnemyKilled?.Invoke(data);
        public static void RaiseRoomEntered(RoomData room) => OnRoomEntered?.Invoke(room);
        public static void RaiseRoomCleared(RoomData room) => OnRoomCleared?.Invoke(room);
        public static void RaiseStageCleared(StageData stage) => OnStageCleared?.Invoke(stage);
        public static void RaisePauseRequested() => OnPauseRequested?.Invoke();
        public static void RaiseResumeRequested() => OnResumeRequested?.Invoke();
        public static void RaiseGamePaused() => OnGamePaused?.Invoke();
        public static void RaiseGameResumed() => OnGameResumed?.Invoke();
    }
}
