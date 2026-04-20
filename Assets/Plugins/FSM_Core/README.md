# FSM Core - Unity용 범용 유한 상태 머신

FSM Core는 Unity에서 사용할 수 있는 범용적이고 확장 가능한 유한 상태 머신(Finite State Machine) 시스템입니다. 기존 GAS Core와 완벽하게 통합되어 게임플레이 어빌리티와 상태 관리를 효율적으로 처리할 수 있습니다.

## 🌟 주요 특징

### ⚡ 범용적 설계
- **모든 게임 장르 지원**: 2D/3D 액션, RPG, 플랫포머, AI 시스템 등
- **Unity 친화적**: MonoBehaviour 기반으로 쉬운 통합
- **확장성**: 인터페이스 기반 설계로 커스텀 확장 용이

### 🔗 GAS Core 통합
- **어빌리티 기반 상태**: 상태 진입 시 자동 어빌리티 실행
- **상태 기반 어빌리티 제한**: 특정 상태에서만 어빌리티 사용 가능
- **리소스 조건**: 마나, 체력 등 리소스 기반 상태 전환

### 🎯 고성능 & 안정성
- **비동기 처리**: async/await 패턴으로 부드러운 상태 전환
- **예외 처리**: 안전한 상태 전환과 오류 복구
- **메모리 효율성**: 적절한 객체 생명주기 관리

## 📁 폴더 구조

```
FSM_Core/
├── Interfaces/           # 핵심 인터페이스
│   ├── IStateMachine.cs  # 상태머신 인터페이스
│   ├── IState.cs         # 상태 인터페이스
│   ├── ITransition.cs    # 전환 인터페이스
│   └── ICondition.cs     # 조건 인터페이스
├── Core/                 # 핵심 구현
│   ├── StateMachine.cs   # 메인 상태머신
│   ├── State.cs          # 상태 구현
│   ├── Transition.cs     # 전환 구현
│   └── Condition.cs      # 조건 구현
├── Integration/          # GAS Core 통합
│   └── GASFSMIntegration.cs
├── Examples/             # 사용 예제
│   └── CharacterFSMExample.cs
└── README.md            # 이 파일
```

## 🚀 빠른 시작

### 1. 기본 FSM 설정

```csharp
public class SimpleFSM : MonoBehaviour
{
    private StateMachine fsm;

    void Start()
    {
        fsm = gameObject.AddComponent<StateMachine>();

        // 상태 추가
        fsm.AddState(new SimpleState("idle"));
        fsm.AddState(new SimpleState("moving"));

        // 전환 추가
        var transition = new ConditionalTransition("idle_to_moving", "idle", "moving",
            () => Input.GetKey(KeyCode.W));
        fsm.AddTransition(transition);

        // 시작
        fsm.Start("idle");
    }
}
```

### 2. 커스텀 상태 생성

```csharp
public class AttackState : State
{
    protected override async Task OnEnterState(CancellationToken cancellationToken)
    {
        Debug.Log("공격 시작!");
        // 공격 애니메이션 재생
        // 어빌리티 실행 등
    }

    protected override void OnUpdateState(float deltaTime)
    {
        // 매 프레임 실행되는 로직
    }

    protected override async Task OnExitState(CancellationToken cancellationToken)
    {
        Debug.Log("공격 종료!");
    }
}
```

### 3. GAS와 통합 사용

```csharp
public class IntegratedExample : MonoBehaviour
{
    [SerializeField] private StateMachine fsm;
    [SerializeField] private AbilitySystem abilitySystem;

    void Start()
    {
        // 어빌리티 상태 추가
        fsm.AddAbilityState("fireball_state", "fireball");

        // 리소스 기반 전환 추가
        fsm.AddResourceTransition("idle", "low_mana", "Mana", 20f,
            ResourceCondition.ComparisonType.LessThan);
    }
}
```

## 🎮 사용 사례

### 캐릭터 상태 관리
```csharp
// Idle ↔ Move ↔ Attack ↔ Jump
fsm.AddState(new IdleState());
fsm.AddState(new MoveState());
fsm.AddState(new AttackState());
fsm.AddState(new JumpState());
```

### AI 행동 패턴
```csharp
// Patrol → Chase → Attack → Retreat
fsm.AddState(new PatrolState());
fsm.AddState(new ChaseState());
fsm.AddState(new AttackState());
fsm.AddState(new RetreatState());
```

### 게임 플로우 제어
```csharp
// Menu → Playing → Paused → GameOver
fsm.AddState(new MenuState());
fsm.AddState(new PlayingState());
fsm.AddState(new PausedState());
fsm.AddState(new GameOverState());
```

## 🔧 고급 기능

### 조건부 전환
```csharp
// 복잡한 조건
var complexTransition = new Transition("complex", "stateA", "stateB");
complexTransition.AddCondition(new TimeCondition("wait", 2f));
complexTransition.AddCondition(new FunctionCondition("custom",
    () => someVariable > threshold));
```

### 우선순위 기반 전환
```csharp
// 높은 우선순위가 먼저 체크됨
var highPriority = new Transition("emergency", "any", "dead", priority: 100);
var normalPriority = new Transition("normal", "idle", "move", priority: 1);
```

### 상태 기반 어빌리티 제한
```csharp
var stateBasedSystem = gameObject.AddComponent<StateBasedAbilitySystem>();
// 특정 상태에서만 특정 어빌리티 사용 가능
```

## 📚 API 참조

### IStateMachine
- `CurrentState`: 현재 활성 상태
- `AddState()`: 상태 추가
- `AddTransition()`: 전환 추가
- `TryTransitionTo()`: 조건부 상태 전환
- `ForceTransitionTo()`: 강제 상태 전환

### IState
- `OnEnter()`: 상태 진입 시 호출
- `OnUpdate()`: 매 프레임 호출
- `OnExit()`: 상태 종료 시 호출

### ITransition
- `CanTransition()`: 전환 가능 여부
- `AddCondition()`: 조건 추가

## 🎯 모범 사례

1. **상태는 단일 책임을 가져야 합니다**
2. **전환 조건은 명확하고 단순하게 작성하세요**
3. **상태 진입/종료 시 필요한 정리 작업을 수행하세요**
4. **디버깅을 위해 로그를 적절히 활용하세요**
5. **성능상 중요한 Update 로직은 최적화하세요**

## 🔄 GAS Core와의 차이점

- **GAS Core**: 어빌리티와 리소스 관리에 특화
- **FSM Core**: 상태 전환과 흐름 제어에 특화
- **함께 사용 시**: 완전한 게임플레이 시스템 구축 가능

## 📈 성능 고려사항

- 상태 업데이트는 매 프레임 실행되므로 최적화 필요
- 조건 체크는 가벼운 연산으로 구성
- 불필요한 전환 생성 지양
- 메모리 누수 방지를 위한 적절한 정리

## 🤝 기여하기

버그 리포트, 기능 요청, 코드 기여를 환영합니다!

1. 이슈 생성
2. 포크 후 브랜치 생성
3. 변경사항 커밋
4. 풀 리퀘스트 생성

## 📄 라이선스

MIT License

---

더 자세한 사용법과 예제는 `Examples/` 폴더를 참고하세요!