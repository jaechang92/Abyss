using System;
using System.Collections.Generic;
using UnityEngine;

namespace FSM.Core
{
    /// <summary>
    /// StateMachine의 전환 규칙·이벤트 트리거 파트.
    ///
    /// 여기서 다루는 세 가지 규약:
    /// ① <b>전환 잠금</b> — 비동기 전환이 진행 중이면 자동 전환을 멈춘다.
    /// ② <b>우선순위</b> — 후보 중 Priority가 가장 높은 하나를 고른다(등록 순서가 아니다).
    /// ③ <b>트리거 수명</b> — 이벤트 트리거는 다음 Update 한 번까지만 유효하다.
    /// </summary>
    public partial class StateMachine
    {
        private List<ITransition> transitions = new List<ITransition>();

        /// <summary>이벤트 ID → 트리거된 프레임. 프레임을 함께 들고 있어야 수명을 판정할 수 있다.</summary>
        private readonly Dictionary<string, int> eventTriggerFrames = new Dictionary<string, int>();

        /// <summary>만료 대상 수집용 재사용 버퍼(순회 중 Dictionary 수정 불가 + GC 회피).</summary>
        private readonly List<string> expiredEventBuffer = new List<string>();

        /// <summary>
        /// 비동기 전환이 진행 중인지. 진행 중에는 자동 전환·조건부 전환이 발동하지 않는다.
        /// </summary>
        private bool isTransitioning;

        /// <summary>
        /// 전환 세대 번호. 새 전환이 시작되거나 강제 전이·정지가 끼어들면 증가하고,
        /// 진행 중이던 비동기 전환은 자기 세대가 낡았음을 보고 남은 단계를 포기한다.
        /// </summary>
        private int transitionGeneration;

        public IReadOnlyList<ITransition> Transitions => transitions;

        /// <summary>비동기 전환 진행 중 여부(외부에서 입력 차단 등에 참고).</summary>
        public bool IsTransitioning => isTransitioning;

        // ====== 전환 잠금 / 세대 관리 ======

        /// <summary>
        /// 전환 시작. 잠금을 걸고 새 세대 번호를 발급한다.
        /// 진행 중이던 전환이 있었다면 그 세대는 이 시점부터 낡은 것이 되어 남은 단계를 포기한다.
        /// </summary>
        private int BeginTransition()
        {
            isTransitioning = true;
            return ++transitionGeneration;
        }

        /// <summary>
        /// 전환 종료. <b>자기 세대가 아직 최신일 때만</b> 잠금을 푼다 —
        /// 중간에 더 새로운 전환이 시작됐다면 잠금의 소유권은 그쪽에 있다.
        /// </summary>
        private void EndTransition(int generation)
        {
            if (generation == transitionGeneration) isTransitioning = false;
        }

        /// <summary>이 전환이 중간에 무효화됐는지(강제 전이·정지·다른 전환 시작).</summary>
        private bool IsStaleTransition(int generation) => generation != transitionGeneration;

        // ====== 자동 전환 ======

        /// <summary>
        /// 현재 상태에서 나가는 전환 중 조건을 만족하는 것을 골라 실행한다.
        ///
        /// ⚠ <b>재진입 가드</b>: 전환이 진행 중이면 아무것도 하지 않는다. 없으면 페이드처럼 긴 비동기 훅이
        /// 걸린 동안에도 Update가 계속 돌고 currentState는 아직 이전 상태라, 같은 전환이 매 프레임 재발동한다.
        ///
        /// ⚠ <b>우선순위 선택</b>: 예전에는 리스트를 역순 순회해 '나중에 등록된 것'이 이겼다.
        /// Priority 필드는 받아만 두고 쓰이지 않았다. 지금은 최댓값을 고른다.
        /// Priority는 런타임에 바뀔 수 있으므로 정렬해 두지 않고 매번 훑는다(후보 수가 적다).
        /// 동점이면 먼저 등록된 쪽이 이긴다.
        /// </summary>
        private void CheckTransitions()
        {
            if (isTransitioning) return;

            var currentStateId = CurrentStateId;
            ITransition best = null;

            for (int i = 0; i < transitions.Count; i++)
            {
                var transition = transitions[i];
                if (transition == null || !transition.IsEnabled) continue;
                if (transition.FromStateId != currentStateId) continue;
                if (best != null && transition.Priority <= best.Priority) continue;
                if (!transition.CanTransition()) continue;

                best = transition;
            }

            if (best == null) return;

            best.NotifyTaken();
            _ = TransitionToAsync(best);
        }

        /// <summary>
        /// 전환 실행. 각 await 뒤에 세대를 확인해, 도중에 강제 전이나 정지가 끼어들었으면
        /// 남은 단계(상태 교체·후처리 훅·완료 통지)를 건너뛴다 — 낡은 전환이 새 상태를 덮어쓰지 못하게 한다.
        /// </summary>
        private async Awaitable TransitionToAsync(ITransition transition)
        {
            int generation = BeginTransition();

            try
            {
                OnTransitionStarted?.Invoke(transition);

                // 전환 전 비동기 처리 (FadeOut 등)
                if (OnBeforeTransitionAsync != null)
                {
                    await OnBeforeTransitionAsync.Invoke(transition);
                    if (IsStaleTransition(generation)) return;
                }

                var fromStateId = CurrentStateId;
                await ChangeStateAsync(transition.ToStateId);
                if (IsStaleTransition(generation)) return;

                // 전환 후 비동기 처리 (Scene 검증, FadeIn 등)
                if (OnAfterTransitionAsync != null)
                {
                    await OnAfterTransitionAsync.Invoke(transition);
                    if (IsStaleTransition(generation)) return;
                }

                OnTransitionCompleted?.Invoke(transition);

                if (enableDebugLog)
                    Debug.Log($"[FSM] 전환 완료: {fromStateId} -> {transition.ToStateId}");
            }
            catch (OperationCanceledException)
            {
                // 정상 종료 경로 — 로그 없음.
            }
            catch (Exception e)
            {
                // fire-and-forget 호출이라 잡지 않으면 예외가 조용히 사라진다.
                Debug.LogError($"[FSM] 전환 {transition.FromStateId} -> {transition.ToStateId} 중 오류: {e.Message}");
            }
            finally
            {
                EndTransition(generation);
            }
        }

        // ====== 전환 등록 ======

        public void AddTransition(ITransition transition)
        {
            if (transition == null) return;

            if (!HasState(transition.FromStateId) || !HasState(transition.ToStateId))
            {
                Debug.LogWarning($"[FSM] 전환 추가 실패: 상태 {transition.FromStateId} 또는 {transition.ToStateId}가 존재하지 않음");
                return;
            }

            transitions.Add(transition);

            if (enableDebugLog)
                Debug.Log($"[FSM] 전환 추가됨: {transition.FromStateId} -> {transition.ToStateId} (우선순위 {transition.Priority})");
        }

        /// <summary>
        /// 이벤트 기반 전환 추가 (편의 메서드)
        /// </summary>
        public void AddTransition(string fromStateId, string toStateId, string eventId, int priority = 0)
        {
            var transition = new EventBasedTransition($"{fromStateId}_{toStateId}_{eventId}", fromStateId, toStateId, eventId, this, priority);
            AddTransition(transition);
        }

        public void RemoveTransition(ITransition transition)
        {
            if (transitions.Remove(transition))
            {
                if (enableDebugLog)
                    Debug.Log($"[FSM] 전환 제거됨: {transition.FromStateId} -> {transition.ToStateId}");
            }
        }

        // ====== 조건부 전환 질의 / 요청 ======

        /// <summary>
        /// 지정 상태로 갈 수 있는지 <b>조회만</b> 한다. ITransition.CanTransition이 순수 조회로 바뀌면서
        /// 이 질의가 이벤트 트리거를 소비하던 문제가 사라졌다.
        /// </summary>
        public bool CanTransitionTo(string stateId)
        {
            if (!HasState(stateId) || !IsRunning) return false;

            var currentStateId = CurrentStateId;
            for (int i = 0; i < transitions.Count; i++)
            {
                var t = transitions[i];
                if (t == null || !t.IsEnabled) continue;
                if (t.FromStateId != currentStateId || t.ToStateId != stateId) continue;
                if (t.CanTransition()) return true;
            }
            return false;
        }

        /// <summary>
        /// 지정 상태로의 전환을 요청한다. 조건을 만족하는 후보 중 우선순위가 가장 높은 것을 쓴다.
        /// 전환이 이미 진행 중이면 거절한다(false) — 자동 전환과 같은 재진입 가드를 적용한다.
        /// </summary>
        public bool TryTransitionTo(string stateId)
        {
            if (!HasState(stateId) || !IsRunning) return false;
            if (isTransitioning) return false;

            var currentStateId = CurrentStateId;
            ITransition best = null;

            for (int i = 0; i < transitions.Count; i++)
            {
                var transition = transitions[i];
                if (transition == null || !transition.IsEnabled) continue;
                if (transition.FromStateId != currentStateId || transition.ToStateId != stateId) continue;
                if (best != null && transition.Priority <= best.Priority) continue;
                if (!transition.CanTransition()) continue;

                best = transition;
            }

            if (best == null) return false;

            best.NotifyTaken();
            _ = TransitionToAsync(best);
            return true;
        }

        // ====== 이벤트 트리거 ======

        /// <summary>
        /// 이벤트 트리거. <b>수명은 다음 Update 한 번</b>이다 —
        /// 소비되지 않은 트리거가 계속 남아 있으면, 나중에 해당 FromState에 진입하는 순간
        /// 의도치 않은 전환이 발동한다(예전 동작).
        /// </summary>
        public void TriggerEvent(string eventId)
        {
            if (string.IsNullOrEmpty(eventId)) return;
            eventTriggerFrames[eventId] = Time.frameCount;
        }

        /// <summary>
        /// 이벤트 상태 확인(조회만). 전환 후보 평가에 쓰이므로 부작용이 없어야 한다.
        /// </summary>
        public bool IsEventTriggered(string eventId)
        {
            return !string.IsNullOrEmpty(eventId) && eventTriggerFrames.ContainsKey(eventId);
        }

        /// <summary>
        /// 이벤트 소비 (한 번 확인 후 리셋). 전환이 실제로 채택됐을 때만 호출된다.
        /// </summary>
        public bool ConsumeEvent(string eventId)
        {
            if (string.IsNullOrEmpty(eventId)) return false;
            return eventTriggerFrames.Remove(eventId);
        }

        /// <summary>전체 트리거 폐기(정지 시).</summary>
        public void ClearEventTriggers()
        {
            eventTriggerFrames.Clear();
        }

        /// <summary>
        /// Update 말미에 호출. 이번 프레임에 설정되지 않은 트리거는 이미 <see cref="CheckTransitions"/>의
        /// 처리 기회를 받았다는 뜻이므로 폐기한다.
        /// </summary>
        private void ExpireStaleEventTriggers()
        {
            if (eventTriggerFrames.Count == 0) return;

            int frame = Time.frameCount;
            expiredEventBuffer.Clear();

            foreach (var pair in eventTriggerFrames)
            {
                if (pair.Value != frame) expiredEventBuffer.Add(pair.Key);
            }

            for (int i = 0; i < expiredEventBuffer.Count; i++)
            {
                eventTriggerFrames.Remove(expiredEventBuffer[i]);
            }
        }
    }
}
