using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;
using FSM.Core.Utils;

namespace FSM.Core
{
    /// <summary>
    /// 하이브리드 FSM. 두 가지 전이 경로를 함께 지원한다.
    ///
    /// - <b>동기</b>(<see cref="ForceTransitionTo"/>): 즉시성이 필요한 전투용. 호출 즉시 상태가 바뀐다.
    /// - <b>비동기</b>(자동 전환·<see cref="ForceTransitionToAsync"/>): 페이드 같은 훅이 끼는 흐름용.
    ///
    /// 전환 규칙·이벤트 트리거 API는 partial 파일 <c>StateMachine.Transitions.cs</c>에 있다.
    /// </summary>
    public partial class StateMachine : MonoBehaviour, IStateMachine
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

        private IState currentState;
        private string previousStateId = string.Empty;
        private CancellationTokenSource cancellationTokenSource;

        public string CurrentStateId => currentState?.Id ?? string.Empty;
        public string PreviousStateId => previousStateId;
        public IState CurrentState => currentState;
        public bool IsRunning { get; private set; }

        public IReadOnlyDictionary<string, IState> States => states;

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

            // 이번 Update에서 처리 기회를 받은 트리거는 폐기한다(무한 잔류 방지).
            ExpireStaleEventTriggers();

#if UNITY_EDITOR
            // Inspector 헬퍼 업데이트
            UpdateInspectorDisplay();
            inspectorHelper.UpdateFromStateMachine(this);
#endif
        }

        // ====== 상태 등록 ======

        /// <summary>
        /// 상태 등록. 등록 키는 <see cref="IState.Name"/>이다.
        ///
        /// ⚠ 기본 <see cref="State"/>는 <c>Name => Id</c>이고 Id는 <see cref="IState.Initialize"/>에서야 정해지므로,
        /// Name을 오버라이드하지 않은 상태는 이름이 비어 있다. 예전에는 이 경우 조용히 무시됐다
        /// (<see cref="SimpleState"/>는 아예 등록이 불가능했다). 지금은 경고를 남기고,
        /// id를 직접 주는 <see cref="AddState(string, IState)"/> 오버로드를 안내한다.
        /// </summary>
        public void AddState(IState state)
        {
            if (state == null)
            {
                Debug.LogWarning("[FSM] 상태 추가 실패: state가 null");
                return;
            }

            AddState(state.Name, state);
        }

        /// <summary>
        /// id를 명시해 상태를 등록한다. Name을 오버라이드하지 않는 상태(SimpleState 등)를 쓸 때 필요하다.
        /// </summary>
        public void AddState(string stateId, IState state)
        {
            if (state == null)
            {
                Debug.LogWarning("[FSM] 상태 추가 실패: state가 null");
                return;
            }

            if (string.IsNullOrEmpty(stateId))
            {
                Debug.LogWarning($"[FSM] 상태 추가 실패: id가 비어 있음 ({state.GetType().Name}). " +
                                 "State.Name을 오버라이드하거나 AddState(id, state)로 명시할 것");
                return;
            }

            if (states.ContainsKey(stateId))
            {
                RemoveState(stateId);
            }

            state.Initialize(stateId, gameObject, this);

            // 등록 키와 Id가 어긋나면 CurrentStateId(=Id) 기반 비교가 조용히 실패한다.
            // 기본 State는 Initialize에서 Id를 그대로 받으므로 여기 걸리는 건 커스텀 IState 구현뿐이다.
            if (state.Id != stateId)
            {
                Debug.LogWarning($"[FSM] 상태 '{stateId}' 등록 후 Id가 '{state.Id}'로 다름 — Initialize 구현 확인 필요. " +
                                 "등록 키와 Id가 다르면 전환 매칭이 어긋난다");
            }

            states[stateId] = state;

            if (enableDebugLog)
                Debug.Log($"[FSM] 상태 추가됨: {stateId}");
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
            return !string.IsNullOrEmpty(stateId) && states.ContainsKey(stateId);
        }

        public bool TryGetState(string stateId, out IState state)
        {
            if (string.IsNullOrEmpty(stateId))
            {
                state = null;
                return false;
            }
            return states.TryGetValue(stateId, out state);
        }

        // ====== 강제 전이 ======

        /// <summary>
        /// 동기 상태 전환 (Combat용 - 즉시 전환).
        /// 진행 중인 비동기 전환이 있어도 <b>막지 않는다</b> — 즉시성이 이 API의 존재 이유이기 때문이다.
        /// 대신 세대 번호를 올려, 뒤늦게 재개될 비동기 전환이 이 결과를 덮어쓰지 못하게 한다.
        /// </summary>
        public void ForceTransitionTo(string stateId)
        {
            if (!HasState(stateId))
            {
                Debug.LogWarning($"[FSM] {stateId}로 강제 전환 실패: 상태가 존재하지 않음");
                return;
            }

            BeginTransition();
            ChangeStateSync(stateId);
            // 동기 전환은 await가 없어 여기서 이미 완료 상태다.
            isTransitioning = false;
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

            int generation = BeginTransition();
            try
            {
                await ChangeStateAsync(stateId);
            }
            finally
            {
                EndTransition(generation);
            }
        }

        // ====== 시작 / 중지 ======

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
            _ = EnterInitialStateAsync(targetStateId);
            OnStarted?.Invoke();

            if (enableDebugLog)
                Debug.Log($"[FSM] 초기 상태로 시작됨: {targetStateId}");
        }

        /// <summary>
        /// 초기 상태 진입. 전환 잠금을 거는 이유: <see cref="ChangeStateAsync"/>는 currentState를 먼저 세우고
        /// OnEnter를 await하므로, 그 사이에 Update가 돌면 아직 진입도 안 끝난 상태에서 자동 전환이 발동한다.
        /// </summary>
        private async Awaitable EnterInitialStateAsync(string stateId)
        {
            int generation = BeginTransition();
            try
            {
                await ChangeStateAsync(stateId);
            }
            finally
            {
                EndTransition(generation);
            }
        }

        public void Stop()
        {
            if (!IsRunning) return;

            IsRunning = false;

            // 진행 중인 비동기 전환 무효화 + 잠금 해제. 정지 후에도 잠금이 남으면 재시작이 막힌다.
            BeginTransition();
            isTransitioning = false;
            ClearEventTriggers();

            _ = ExitCurrentStateAsync();
            OnStopped?.Invoke();

            // 중지 시 이전 상태도 리셋
            previousStateId = string.Empty;

            if (enableDebugLog)
                Debug.Log("[FSM] 중지됨");
        }

        // ====== 상태 교체 ======

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
