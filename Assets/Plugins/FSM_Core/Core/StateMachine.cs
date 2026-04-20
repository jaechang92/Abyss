using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;
using FSM.Core.Utils;

namespace FSM.Core
{
    public class StateMachine : MonoBehaviour, IStateMachine
    {
        [Header("디버그 설정")]
        [SerializeField] private bool enableDebugLog = true;
        [SerializeField] private string initialStateId;

        [Header("상태 정보 (읽기 전용)")]
        [SerializeField] private string currentStateDisplay = "None";
        [SerializeField] private string previousStateDisplay = "None";
        [SerializeField] private float stateChangeTime = 0f;

        [Header("Inspector 헬퍼 (읽기 전용)")]
        [SerializeField] private StateMachineInspectorHelper inspectorHelper = new StateMachineInspectorHelper();

        private Dictionary<string, IState> states = new Dictionary<string, IState>();
        private List<ITransition> transitions = new List<ITransition>();
        private Dictionary<string, bool> eventTriggers = new Dictionary<string, bool>();

        private IState currentState;
        private string previousStateId = string.Empty;
        private CancellationTokenSource cancellationTokenSource;

        public string CurrentStateId => currentState?.Id ?? string.Empty;
        public string PreviousStateId => previousStateId;
        public IState CurrentState => currentState;
        public bool IsRunning { get; private set; }

        public IReadOnlyDictionary<string, IState> States => states;
        public IReadOnlyList<ITransition> Transitions => transitions;

        public event Action<string, string> OnStateChanged;
        public event Action<ITransition> OnTransitionStarted;
        public event Action<ITransition> OnTransitionCompleted;
        public event Action OnStarted;
        public event Action OnStopped;

        /// <summary>
        /// 상태 전환 전 비동기 이벤트 (FadeOut 등)
        /// </summary>
        public event Func<ITransition, Awaitable> OnBeforeTransitionAsync;

        /// <summary>
        /// 상태 전환 후 비동기 이벤트 (Scene 검증, FadeIn 등)
        /// </summary>
        public event Func<ITransition, Awaitable> OnAfterTransitionAsync;

        private void Awake()
        {
            cancellationTokenSource = new CancellationTokenSource();
        }

        private void Update()
        {
            if (IsRunning)
            {
                ((IStateMachine)this).Update();
            }
        }

        void IStateMachine.Update()
        {
            if (currentState != null)
            {
                currentState.OnUpdate(Time.deltaTime);
                CheckTransitions();
            }

#if UNITY_EDITOR
            // Inspector 헬퍼 업데이트
            UpdateInspectorDisplay();
            inspectorHelper.UpdateFromStateMachine(this);
#endif
        }

        private void CheckTransitions()
        {
            // 성능 최적화: for 루프 사용 및 어로케이션 최소화
            var currentStateId = CurrentStateId;
            for (int i = transitions.Count - 1; i >= 0; i--)
            {
                var transition = transitions[i];
                if (transition.IsEnabled && transition.FromStateId == currentStateId && transition.CanTransition())
                {
                    _ = TransitionToAsync(transition);
                    return;
                }
            }
        }

        public void AddState(IState state)
        {
            if (state == null || string.IsNullOrEmpty(state.Name)) return;

            if (states.ContainsKey(state.Name))
            {
                RemoveState(state.Name);
            }

            state.Initialize(state.Name, gameObject, this);
            states[state.Name] = state;

            if (enableDebugLog)
                Debug.Log($"[FSM] 상태 추가됨: {state.Name}");
        }

        public void RemoveState(string stateId)
        {
            if (states.TryGetValue(stateId, out var state))
            {
                // 현재 상태라면 정리 처리
                if (CurrentStateId == stateId && IsRunning)
                {
                    Stop();
                }

                // 관련 전환 제거
                var relatedTransitions = transitions
                    .Where(t => t.FromStateId == stateId || t.ToStateId == stateId)
                    .ToList();

                foreach (var transition in relatedTransitions)
                {
                    RemoveTransition(transition);
                }

                states.Remove(stateId);

                if (enableDebugLog)
                    Debug.Log($"[FSM] 상태 제거됨: {stateId}");
            }
        }

        public bool HasState(string stateId)
        {
            return states.ContainsKey(stateId);
        }

        public bool TryGetState(string stateId, out IState state)
        {
            return states.TryGetValue(stateId, out state);
        }

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
                Debug.Log($"[FSM] 전환 추가됨: {transition.FromStateId} -> {transition.ToStateId}");
        }

        /// <summary>
        /// 이벤트 기반 전환 추가 (편의 메서드)
        /// </summary>
        public void AddTransition(string fromStateId, string toStateId, string eventId, int priority = 0)
        {
            var transition = new EventBasedTransition($"{fromStateId}_{toStateId}_{eventId}", fromStateId, toStateId, eventId, this, priority);
            AddTransition(transition);
        }

        /// <summary>
        /// 이벤트 트리거
        /// </summary>
        public void TriggerEvent(string eventId)
        {
            eventTriggers[eventId] = true;
        }

        /// <summary>
        /// 이벤트 상태 확인
        /// </summary>
        public bool IsEventTriggered(string eventId)
        {
            return eventTriggers.TryGetValue(eventId, out bool triggered) && triggered;
        }

        /// <summary>
        /// 이벤트 소비 (한 번 확인 후 리셋)
        /// </summary>
        public bool ConsumeEvent(string eventId)
        {
            if (eventTriggers.TryGetValue(eventId, out bool triggered) && triggered)
            {
                eventTriggers[eventId] = false;
                return true;
            }
            return false;
        }

        public void RemoveTransition(ITransition transition)
        {
            if (transitions.Remove(transition))
            {
                if (enableDebugLog)
                    Debug.Log($"[FSM] 전환 제거됨: {transition.FromStateId} -> {transition.ToStateId}");
            }
        }

        public bool CanTransitionTo(string stateId)
        {
            if (!HasState(stateId) || !IsRunning) return false;

            var currentStateId = CurrentStateId;
            for (int i = 0; i < transitions.Count; i++)
            {
                var t = transitions[i];
                if (t.IsEnabled && t.FromStateId == currentStateId && t.ToStateId == stateId && t.CanTransition())
                {
                    return true;
                }
            }
            return false;
        }

        public bool TryTransitionTo(string stateId)
        {
            if (!HasState(stateId) || !IsRunning) return false;

            var currentStateId = CurrentStateId;
            for (int i = 0; i < transitions.Count; i++)
            {
                var transition = transitions[i];
                if (transition.IsEnabled && transition.FromStateId == currentStateId && transition.ToStateId == stateId && transition.CanTransition())
                {
                    _ = TransitionToAsync(transition);
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 동기 상태 전환 (Combat용 - 즉시 전환)
        /// </summary>
        public void ForceTransitionTo(string stateId)
        {
            if (!HasState(stateId))
            {
                Debug.LogWarning($"[FSM] {stateId}로 강제 전환 실패: 상태가 존재하지 않음");
                return;
            }

            ChangeStateSync(stateId);
        }

        /// <summary>
        /// 비동기 상태 전환 (GameFlow용 - 대기 가능)
        /// </summary>
        public async Awaitable ForceTransitionToAsync(string stateId)
        {
            if (!HasState(stateId))
            {
                Debug.LogWarning($"[FSM] {stateId}로 강제 전환 실패: 상태가 존재하지 않음");
                return;
            }

            await ChangeStateAsync(stateId);
        }

        public void StartStateMachine(string initialStateId = null)
        {
            if (IsRunning) return;

            var targetStateId = initialStateId ?? this.initialStateId;
            if (string.IsNullOrEmpty(targetStateId))
            {
                targetStateId = states.Keys.FirstOrDefault();
            }

            if (string.IsNullOrEmpty(targetStateId))
            {
                Debug.LogWarning("[FSM] 시작할 수 없음: 사용 가능한 상태가 없음");
                return;
            }

            IsRunning = true;
            _ = ChangeStateAsync(targetStateId);
            OnStarted?.Invoke();

            if (enableDebugLog)
                Debug.Log($"[FSM] 초기 상태로 시작됨: {targetStateId}");
        }

        public void Stop()
        {
            if (!IsRunning) return;

            IsRunning = false;
            _ = ExitCurrentStateAsync();
            OnStopped?.Invoke();

            // 중지 시 이전 상태도 리셋
            previousStateId = string.Empty;

            if (enableDebugLog)
                Debug.Log("[FSM] 중지됨");
        }

        private async Awaitable TransitionToAsync(ITransition transition)
        {
            OnTransitionStarted?.Invoke(transition);

            // 전환 전 비동기 처리 (FadeOut 등)
            if (OnBeforeTransitionAsync != null)
            {
                await OnBeforeTransitionAsync.Invoke(transition);
            }

            var fromStateId = CurrentStateId;
            await ChangeStateAsync(transition.ToStateId);

            // 전환 후 비동기 처리 (Scene 검증, FadeIn 등)
            if (OnAfterTransitionAsync != null)
            {
                await OnAfterTransitionAsync.Invoke(transition);
            }

            OnTransitionCompleted?.Invoke(transition);

            if (enableDebugLog)
                Debug.Log($"[FSM] 전환 완료: {fromStateId} -> {transition.ToStateId}");
        }

        /// <summary>
        /// 동기 상태 전환 (Combat용)
        /// </summary>
        private void ChangeStateSync(string newStateId)
        {
            if (!states.TryGetValue(newStateId, out var newState))
            {
                Debug.LogWarning($"[FSM] 상태 {newStateId}를 찾을 수 없음");
                return;
            }

            var oldStateId = CurrentStateId;

            // 현재 상태 동기 종료
            ExitCurrentStateSync();

            // 이전 상태 업데이트
            previousStateId = oldStateId;

            // 새 상태 진입
            currentState = newState;
            try
            {
                currentState.OnEnterSync();
                OnStateChanged?.Invoke(oldStateId, newStateId);

                // 상태 변경 시간 기록
                stateChangeTime = Time.time;

                if (enableDebugLog)
                    Debug.Log($"[FSM] 상태 변경됨(동기): {oldStateId} -> {newStateId}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[FSM] 상태 {newStateId} 진입 중 오류: {e.Message}");
                currentState = null;
            }
        }

        /// <summary>
        /// 비동기 상태 전환 (GameFlow용)
        /// </summary>
        private async Awaitable ChangeStateAsync(string newStateId)
        {
            if (!states.TryGetValue(newStateId, out var newState))
            {
                Debug.LogWarning($"[FSM] 상태 {newStateId}를 찾을 수 없음");
                return;
            }

            var oldStateId = CurrentStateId;

            // 현재 상태 종료
            await ExitCurrentStateAsync();

            // 이전 상태 업데이트
            previousStateId = oldStateId;

            // 새 상태 진입
            currentState = newState;
            try
            {
                await currentState.OnEnter(cancellationTokenSource.Token);
                OnStateChanged?.Invoke(oldStateId, newStateId);

                // 상태 변경 시간 기록
                stateChangeTime = Time.time;

                if (enableDebugLog)
                    Debug.Log($"[FSM] 상태 변경됨(비동기): {oldStateId} -> {newStateId}");
            }
            catch (System.OperationCanceledException)
            {
                // CancellationToken이 취소된 경우 (정상적인 종료 상황)
                currentState = null;
            }
            catch (Exception e)
            {
                Debug.LogError($"[FSM] 상태 {newStateId} 진입 중 오류: {e.Message}");
                currentState = null;
            }
        }

        /// <summary>
        /// 동기 상태 종료 (Combat용)
        /// </summary>
        private void ExitCurrentStateSync()
        {
            if (currentState != null)
            {
                try
                {
                    currentState.OnExitSync();
                }
                catch (Exception e)
                {
                    Debug.LogError($"[FSM] 상태 {currentState.Id} 종료 중 오류: {e.Message}");
                }
                finally
                {
                    currentState = null;
                }
            }
        }

        /// <summary>
        /// 비동기 상태 종료 (GameFlow용)
        /// </summary>
        private async Awaitable ExitCurrentStateAsync()
        {
            if (currentState != null)
            {
                try
                {
                    await currentState.OnExit(cancellationTokenSource.Token);
                }
                catch (System.OperationCanceledException)
                {
                    // CancellationToken이 취소된 경우 (정상적인 종료 상황)
                    // 에러가 아니므로 로그 출력 안 함
                }
                catch (Exception e)
                {
                    Debug.LogError($"[FSM] 상태 {currentState.Id} 종료 중 오류: {e.Message}");
                }
                finally
                {
                    currentState = null;
                }
            }
        }

        #if UNITY_EDITOR
        private void UpdateInspectorDisplay()
        {
            currentStateDisplay = string.IsNullOrEmpty(CurrentStateId) ? "None" : CurrentStateId;
            previousStateDisplay = string.IsNullOrEmpty(previousStateId) ? "None" : previousStateId;
        }
        #endif

        private void OnDestroy()
        {
            Stop();
            cancellationTokenSource?.Cancel();
            cancellationTokenSource?.Dispose();
        }
    }
}
