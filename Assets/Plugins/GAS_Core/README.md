# GAS Core - Generic Ability System

다양한 게임 장르에서 사용할 수 있는 범용 게임플레이 어빌리티 시스템입니다.

## 특징

- **장르 무관**: 2D 플랫포머, 3D 액션, RPG, RTS 등 다양한 장르에서 사용 가능
- **모듈화 설계**: 필요한 기능만 선택적으로 사용
- **확장 가능**: 인터페이스 기반으로 쉽게 확장 가능
- **비동기 지원**: async/await 패턴을 통한 현대적인 비동기 프로그래밍
- **리소스 시스템**: 마나, 스태미나 등 선택적 리소스 관리
- **태그 시스템**: 어빌리티 간 상호작용 제어

## 주요 컴포넌트

### Core 시스템
- `IAbilitySystem`: 어빌리티 시스템 인터페이스
- `AbilitySystem`: 기본 어빌리티 시스템 구현
- `IAbility`: 개별 어빌리티 인터페이스
- `Ability`: 기본 어빌리티 구현

### 데이터
- `IAbilityData`: 어빌리티 데이터 인터페이스
- `AbilityData`: ScriptableObject 기반 어빌리티 데이터

### 실행기
- `AbilityExecutor`: 어빌리티 실행 로직 추상 클래스
- `DamageExecutor`: 데미지 어빌리티 실행기
- `HealExecutor`: 힐링 어빌리티 실행기

### 인터페이스
- `IAbilityTarget`: 어빌리티 대상 인터페이스
- `IGameplayContext`: 게임플레이 컨텍스트 인터페이스
- `IGameplayEffect`: 게임플레이 효과 인터페이스

## 빠른 시작

### 1. 기본 설정

```csharp
// AbilitySystem 컴포넌트 추가
var abilitySystem = gameObject.AddComponent<AbilitySystem>();

// 어빌리티 데이터 생성 (ScriptableObject)
var abilityData = ScriptableObject.CreateInstance<AbilityData>();
abilityData.AbilityId = "fireball";
abilityData.AbilityName = "Fire Ball";
abilityData.CooldownDuration = 3f;

// 어빌리티 추가
abilitySystem.AddAbility(abilityData);
```

### 2. 어빌리티 사용

```csharp
// 어빌리티 사용 가능 여부 확인
if (abilitySystem.CanUseAbility("fireball"))
{
    // 어빌리티 사용
    abilitySystem.TryUseAbility("fireball");
}
```

### 3. 리소스 시스템 활용

```csharp
// 마나 시스템 설정
abilitySystem.SetMaxResource("Mana", 100f);
abilitySystem.SetResource("Mana", 100f);

// 리소스 소모 어빌리티 생성
abilityData.ResourceCosts.Add("Mana", 20f);
```

## 어빌리티 타입

- **Active**: 액티브 스킬
- **Passive**: 패시브 스킬
- **Toggle**: 토글 스킬
- **Channeled**: 채널링 스킬
- **Instant**: 즉시 발동
- **Buff/Debuff**: 버프/디버프

## 타겟팅 시스템

- **None**: 타겟 없음
- **Self**: 자신
- **SingleTarget**: 단일 대상
- **Area**: 범위
- **Cone**: 원뿔 범위
- **Line**: 직선 범위
- **AllEnemies/AllAllies**: 모든 적/아군

## 확장 방법

### 커스텀 어빌리티 실행기

```csharp
[CreateAssetMenu(fileName = "MyExecutor", menuName = "GAS/Executors/MyExecutor")]
public class MyCustomExecutor : AbilityExecutor
{
    public override async Task<bool> ExecuteAsync(GameObject caster, IAbilityData data, List<IAbilityTarget> targets)
    {
        // 커스텀 로직 구현
        return true;
    }
}
```

### 커스텀 게임플레이 효과

```csharp
public class MyGameplayEffect : MonoBehaviour, IGameplayEffect
{
    public string EffectId { get; set; }
    public float Duration { get; set; }

    public void Apply(IAbilityTarget target)
    {
        // 효과 적용 로직
    }

    public void Remove(IAbilityTarget target)
    {
        // 효과 제거 로직
    }
}
```

## 시스템 요구사항

- Unity 2022.3 이상
- .NET Framework 4.x 또는 .NET Standard 2.1

## 라이선스

MIT License

## 지원

문제가 발생하거나 기능 요청이 있으시면 GitHub Issues를 통해 알려주세요.