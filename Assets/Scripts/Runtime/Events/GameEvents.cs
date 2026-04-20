using System;

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
        public static event Action<int, DraftTriggerReason> OnPlayerLevelUp;
        public static event Action OnDraftOpened;
        public static event Action OnDraftClosed;
        public static event Action OnBossKilled;

        public static void RaiseRunStarted() => OnRunStarted?.Invoke();
        public static void RaiseRunEnded() => OnRunEnded?.Invoke();
        public static void RaisePlayerDead() => OnPlayerDead?.Invoke();
        public static void RaisePlayerLevelUp(int newLevel, DraftTriggerReason reason) => OnPlayerLevelUp?.Invoke(newLevel, reason);
        public static void RaiseDraftOpened() => OnDraftOpened?.Invoke();
        public static void RaiseDraftClosed() => OnDraftClosed?.Invoke();
        public static void RaiseBossKilled() => OnBossKilled?.Invoke();

        /// <summary>
        /// 씬 재로드·에디터 재진입 시 구독 누수 방지용 초기화.
        /// </summary>
        public static void ClearAll()
        {
            OnRunStarted = null;
            OnRunEnded = null;
            OnPlayerDead = null;
            OnPlayerLevelUp = null;
            OnDraftOpened = null;
            OnDraftClosed = null;
            OnBossKilled = null;
        }
    }
}
