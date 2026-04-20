# GAS Core API 레퍼런스

## 목차
1. [핵심 인터페이스](#핵심-인터페이스)
2. [주요 클래스](#주요-클래스)
3. [데이터 구조](#데이터-구조)
4. [열거형](#열거형)
5. [실행기](#실행기)
6. [이벤트](#이벤트)

---

## 핵심 인터페이스

### IAbilitySystem

어빌리티 시스템의 핵심 인터페이스입니다.

#### 프로퍼티
```csharp
IReadOnlyDictionary<string, IAbility> Abilities { get; }
```

#### 어빌리티 관리
```csharp
bool HasAbility(string abilityId);
bool TryGetAbility(string abilityId, out IAbility ability);
void AddAbility(IAbility ability);
void AddAbility(IAbilityData abilityData);
void RemoveAbility(string abilityId);
```

#### 어빌리티 실행
```csharp
bool CanUseAbility(string abilityId);
bool TryUseAbility(string abilityId);
void CancelAbility(string abilityId);
void CancelAllAbilities();
void ResetAllCooldowns();
```

#### 리소스 관리
```csharp
bool HasResource(string resourceType);
float GetMaxResource(string resourceType);
float GetResource(string resourceType);
void SetMaxResource(string resourceType, float maxValue);
void SetResource(string resourceType, float value);
bool ConsumeResource(string resourceType, float amount);
void RestoreResource(string resourceType, float amount);
```

#### 이벤트
```csharp
event Action<string> OnAbilityAdded;
event Action<string> OnAbilityRemoved;
event Action<string> OnAbilityUsed;
event Action<string> OnAbilityCancelled;
event Action<string, float> OnResourceChanged;
```

### IAbility

개별 어빌리티의 인터페이스입니다.

#### 프로퍼티
```csharp
string Id { get; }
string Name { get; }
string Description { get; }
AbilityState State { get; }
IAbilityData Data { get; }
bool IsOnCooldown { get; }
float CooldownRemaining { get; }
float CooldownProgress { get; }
```

#### 메서드
```csharp
bool CanExecute();
Task<bool> ExecuteAsync(CancellationToken cancellationToken = default);
void Cancel();
```

#### 이벤트
```csharp
event Action<IAbility> OnAbilityStarted;
event Action<IAbility> OnAbilityCompleted;
event Action<IAbility> OnAbilityCancelled;
event Action<IAbility> OnCooldownStarted;
event Action<IAbility> OnCooldownCompleted;
```

### IAbilityData

어빌리티 데이터의 인터페이스입니다.

#### 기본 정보
```csharp
string AbilityId { get; }
string AbilityName { get; }
string Description { get; }
AbilityType AbilityType { get; }
```

#### 실행 설정
```csharp
float CooldownDuration { get; }
float CastTime { get; }
float Duration { get; }
bool CanBeCancelled { get; }
```

#### 리소스 및 태그
```csharp
Dictionary<string, float> ResourceCosts { get; }
List<string> AbilityTags { get; }
List<string> CancelTags { get; }
List<string> BlockTags { get; }
```

#### 범위/타겟팅
```csharp
float Range { get; }
TargetType TargetType { get; }
```

### IAbilityTarget

어빌리티의 대상이 되는 객체의 인터페이스입니다.

#### 기본 정보
```csharp
GameObject GameObject { get; }
Transform Transform { get; }
bool IsAlive { get; }
bool IsTargetable { get; }
```

#### 체력 시스템
```csharp
float CurrentHealth { get; }
float MaxHealth { get; }
```

#### 팀/소속 정보
```csharp
string TeamId { get; }
bool IsHostileTo(IAbilityTarget other);
bool IsFriendlyTo(IAbilityTarget other);
```

#### 효과 적용
```csharp
void ApplyEffect(IGameplayEffect effect);
void RemoveEffect(string effectId);
bool HasEffect(string effectId);
IReadOnlyList<IGameplayEffect> GetActiveEffects();
```

#### 데미지/힐링
```csharp
void TakeDamage(float damage, IAbilityTarget source = null);
void Heal(float amount, IAbilityTarget source = null);
```

### IGameplayContext

게임플레이 상황을 관리하는 인터페이스입니다.

#### 기본 정보
```csharp
GameObject Owner { get; }
Transform Transform { get; }
bool IsAlive { get; }
bool CanAct { get; }
Vector3 Position { get; }
Vector3 Forward { get; }
```

#### 상태 관리
```csharp
bool IsInState(string stateName);
void SetState(string stateName, bool value);
```

#### 타겟팅
```csharp
Transform GetTarget();
void SetTarget(Transform target);
```

#### 커스텀 데이터
```csharp
T GetCustomData<T>(string key) where T : class;
void SetCustomData<T>(string key, T data) where T : class;
```

---

## 주요 클래스

### AbilitySystem : MonoBehaviour, IAbilitySystem

GAS의 핵심 구현 클래스입니다.

#### 인스펙터 설정
```csharp
[SerializeField] private List<AbilityData> initialAbilities;
[SerializeField] private bool useResourceSystem = true;
[SerializeField] private List<ResourceConfig> resourceConfigs;
```

#### 주요 메서드
```csharp
// IAbilitySystem의 모든 메서드를 구현
// 추가로 제공되는 유틸리티 메서드들
public void ResetAllCooldowns();
```

### Ability : IAbility, IDisposable

어빌리티의 기본 구현 클래스입니다.

#### 주요 메서드
```csharp
public void Initialize(IAbilityData abilityData, GameObject abilityOwner,
    IAbilitySystem system = null, IGameplayContext context = null);
public void UpdateCooldown(float deltaTime);
public void ResetCooldown();
```

#### 확장 가능한 가상 메서드
```csharp
protected virtual async Task ExecuteAbilityEffect(CancellationToken cancellationToken);
protected virtual async Task ExecuteActiveAbility(CancellationToken cancellationToken);
protected virtual void ExecuteInstantAbility();
protected virtual async Task ExecuteChanneledAbility(CancellationToken cancellationToken);
protected virtual void ExecuteToggleAbility();
protected virtual void ExecutePassiveAbility();
```

### AbilityCooldown

쿨다운을 관리하는 유틸리티 클래스입니다.

#### 프로퍼티
```csharp
float Duration { get; }
float RemainingTime { get; }
bool IsOnCooldown { get; }
float Progress { get; } // 0~1
```

#### 메서드
```csharp
void Initialize(float duration);
void StartCooldown();
void StartCooldown(float customDuration);
void Update(float deltaTime);
void Reset();
void ReduceCooldown(float amount);
void ReduceCooldownByPercent(float percent);
bool CanUse();
```

#### 이벤트
```csharp
event Action OnCooldownStarted;
event Action OnCooldownCompleted;
event Action<float> OnCooldownUpdated;
```

### DefaultGameplayContext : MonoBehaviour, IGameplayContext

기본 게임플레이 컨텍스트 구현체입니다.

#### 추가 메서드
```csharp
public void SetAlive(bool alive);
public void SetCanAct(bool canAct);
public void ClearAllStates();
public void ClearAllCustomData();
```

---

## 데이터 구조

### AbilityData : ScriptableObject, IAbilityData

Unity Inspector에서 설정 가능한 어빌리티 데이터입니다.

#### 추가 프로퍼티
```csharp
Sprite Icon { get; }
float Radius { get; }
float Angle { get; }
float DamageValue { get; }
DamageType DamageType { get; }
float HealValue { get; }
string AnimationTrigger { get; }
GameObject EffectPrefab { get; }
AudioClip SoundEffect { get; }
```

#### 유틸리티 메서드
```csharp
T GetCustomProperty<T>(string key, T defaultValue = default);
bool ValidateData();
float GetResourceCost(string resourceType);
bool HasTag(string tag);
```

### ResourceConfig

리소스 설정을 위한 Serializable 클래스입니다.

```csharp
[System.Serializable]
public class ResourceConfig
{
    public string resourceType;
    public float maxValue = 100f;
    public float initialValue = 100f;
    public float regenerationRate = 0f; // 초당 회복량
}
```

### CustomProperty

커스텀 프로퍼티를 위한 Serializable 클래스입니다.

```csharp
[System.Serializable]
public class CustomProperty
{
    public string key;
    public string value;
}
```

### ResourceCost

리소스 비용 정보를 위한 Serializable 클래스입니다.

```csharp
[System.Serializable]
public class ResourceCost
{
    public string resourceType;
    public float amount;
}
```

---

## 열거형

### AbilityType
```csharp
public enum AbilityType
{
    Active,         // 액티브 스킬
    Passive,        // 패시브 스킬
    Toggle,         // 토글 스킬
    Channeled,      // 채널링 스킬
    Instant,        // 즉시 발동
    Buff,           // 버프
    Debuff,         // 디버프
    Ultimate        // 궁극기
}
```

### AbilityState
```csharp
public enum AbilityState
{
    Ready,          // 사용 가능
    Casting,        // 시전 중
    Active,         // 활성 중
    Cooldown,       // 쿨다운 중
    Disabled,       // 비활성화
    Cancelled       // 취소됨
}
```

### TargetType
```csharp
public enum TargetType
{
    None,           // 타겟 없음
    Self,           // 자신
    SingleTarget,   // 단일 대상
    PointTarget,    // 지점 대상
    Directional,    // 방향성
    Area,           // 범위
    AllEnemies,     // 모든 적
    AllAllies,      // 모든 아군
    Cone,           // 원뿔 범위
    Line            // 직선 범위
}
```

### DamageType
```csharp
public enum DamageType
{
    Physical,       // 물리 데미지
    Magical,        // 마법 데미지
    True,           // 고정 데미지
    Healing,        // 힐링
    Shield,         // 실드
    Custom          // 커스텀
}
```

### EffectDurationType
```csharp
public enum EffectDurationType
{
    Instant,        // 즉시
    Duration,       // 지속 시간
    Permanent,      // 영구
    UntilRemoved,   // 제거될 때까지
    Stacks          // 스택
}
```

### ResourceType
```csharp
public enum ResourceType
{
    Health,         // 체력
    Mana,           // 마나
    Stamina,        // 스태미나
    Energy,         // 에너지
    Rage,           // 분노
    Focus,          // 집중력
    Custom          // 커스텀
}
```

### AbilityExecutionResult
```csharp
public enum AbilityExecutionResult
{
    Success,        // 성공
    Failed,         // 실패
    Cancelled,      // 취소
    NotEnoughResources, // 리소스 부족
    OnCooldown,     // 쿨다운 중
    InvalidTarget,  // 유효하지 않은 타겟
    OutOfRange,     // 범위 밖
    Blocked         // 차단됨
}
```

---

## 실행기

### AbilityExecutor : ScriptableObject

어빌리티 실행 로직을 담당하는 추상 클래스입니다.

#### 추상 메서드
```csharp
public abstract Task<bool> ExecuteAsync(GameObject caster, IAbilityData data, List<IAbilityTarget> targets);
```

#### 가상 메서드
```csharp
public virtual bool Validate(GameObject caster, IAbilityData data, List<IAbilityTarget> targets);
protected virtual bool IsValidTarget(GameObject caster, IAbilityTarget target, IAbilityData data);
protected virtual GameObject SpawnEffect(Vector3 position, Quaternion rotation = default);
protected virtual void PlaySound(Vector3 position);
```

#### 타겟팅 유틸리티
```csharp
protected virtual List<IAbilityTarget> FindTargetsInRange(Vector3 center, float range, LayerMask targetLayer);
protected virtual List<IAbilityTarget> FindTargetsInRange2D(Vector2 center, float range, LayerMask targetLayer);
protected virtual List<IAbilityTarget> FindTargetsInDirection(Vector3 origin, Vector3 direction, float range, float angle, LayerMask targetLayer);
protected virtual List<IAbilityTarget> FindTargetsInLine(Vector3 start, Vector3 end, float width, LayerMask targetLayer);
```

### DamageExecutor : AbilityExecutor

데미지를 적용하는 실행기입니다.

#### 설정
```csharp
[SerializeField] private float baseDamage = 10f;
[SerializeField] private DamageType damageType = DamageType.Physical;
[SerializeField] private bool scalingWithCaster = true;
[SerializeField] private TargetType targetType = TargetType.SingleTarget;
[SerializeField] private LayerMask targetLayer = -1;
```

### HealExecutor : AbilityExecutor

힐링을 적용하는 실행기입니다.

#### 설정
```csharp
[SerializeField] private float baseHealAmount = 20f;
[SerializeField] private bool scalingWithCaster = true;
[SerializeField] private bool canOverheal = false;
[SerializeField] private TargetType targetType = TargetType.Self;
[SerializeField] private LayerMask targetLayer = -1;
```

---

## 이벤트

### 시스템 레벨 이벤트

#### IAbilitySystem 이벤트
```csharp
// 어빌리티 관리
event Action<string> OnAbilityAdded;        // 어빌리티 추가됨
event Action<string> OnAbilityRemoved;      // 어빌리티 제거됨

// 어빌리티 사용
event Action<string> OnAbilityUsed;         // 어빌리티 사용됨
event Action<string> OnAbilityCancelled;    // 어빌리티 취소됨

// 리소스 변화
event Action<string, float> OnResourceChanged; // 리소스 변화
```

### 어빌리티 레벨 이벤트

#### IAbility 이벤트
```csharp
// 실행 관련
event Action<IAbility> OnAbilityStarted;     // 어빌리티 시작됨
event Action<IAbility> OnAbilityCompleted;   // 어빌리티 완료됨
event Action<IAbility> OnAbilityCancelled;   // 어빌리티 취소됨

// 쿨다운 관련
event Action<IAbility> OnCooldownStarted;    // 쿨다운 시작됨
event Action<IAbility> OnCooldownCompleted;  // 쿨다운 완료됨
```

### 쿨다운 이벤트

#### AbilityCooldown 이벤트
```csharp
event Action OnCooldownStarted;              // 쿨다운 시작
event Action OnCooldownCompleted;            // 쿨다운 완료
event Action<float> OnCooldownUpdated;       // 쿨다운 업데이트 (남은 시간)
```

---

## 사용 패턴

### 기본 사용 패턴
```csharp
// 1. AbilitySystem 컴포넌트 추가
var abilitySystem = gameObject.AddComponent<AbilitySystem>();

// 2. 어빌리티 데이터 생성 및 추가
var abilityData = ScriptableObject.CreateInstance<AbilityData>();
// ... 데이터 설정 ...
abilitySystem.AddAbility(abilityData);

// 3. 어빌리티 사용
if (abilitySystem.CanUseAbility("ability_id"))
{
    abilitySystem.TryUseAbility("ability_id");
}
```

### 이벤트 구독 패턴
```csharp
abilitySystem.OnAbilityUsed += (abilityId) => {
    Debug.Log($"Used: {abilityId}");
};

abilitySystem.OnResourceChanged += (resourceType, value) => {
    UpdateUI(resourceType, value);
};
```

### 확장 패턴
```csharp
// 커스텀 어빌리티
public class MyCustomAbility : Ability
{
    protected override async Task ExecuteAbilityEffect(CancellationToken cancellationToken)
    {
        // 커스텀 로직
    }
}

// 커스텀 실행기
[CreateAssetMenu(fileName = "MyExecutor", menuName = "GAS/Executors/MyExecutor")]
public class MyCustomExecutor : AbilityExecutor
{
    public override async Task<bool> ExecuteAsync(GameObject caster, IAbilityData data, List<IAbilityTarget> targets)
    {
        // 커스텀 실행 로직
        return true;
    }
}
```