# GAS Core FAQ (자주 묻는 질문)

## 목차
1. [일반적인 질문](#일반적인-질문)
2. [설치 및 설정](#설치-및-설정)
3. [어빌리티 생성](#어빌리티-생성)
4. [리소스 시스템](#리소스-시스템)
5. [성능 및 최적화](#성능-및-최적화)
6. [확장 및 커스터마이징](#확장-및-커스터마이징)
7. [트러블슈팅](#트러블슈팅)

---

## 일반적인 질문

### Q: GAS Core는 어떤 게임 장르에 적합한가요?
**A:** GAS Core는 범용적으로 설계되어 다양한 장르에서 사용할 수 있습니다:
- **2D/3D 액션 게임**: 전투 스킬, 특수 능력
- **RPG**: 스킬 트리, 마법 시스템
- **MOBA/RTS**: 유닛 어빌리티, 영웅 스킬
- **턴제 게임**: 스킬 기반 전투 시스템
- **플랫포머**: 특수 이동 능력, 아이템 효과

### Q: Unity의 최소 요구 버전은?
**A:** Unity 2022.3 LTS 이상을 권장합니다. async/await 패턴과 최신 C# 기능을 활용하기 때문입니다.

### Q: 다른 어빌리티 시스템과 비교했을 때 장점은?
**A:**
- **범용성**: 특정 장르에 종속되지 않음
- **모듈화**: 필요한 기능만 선택적 사용
- **확장성**: 인터페이스 기반 설계로 쉬운 확장
- **Unity 친화적**: ScriptableObject 기반 데이터 관리
- **현대적 설계**: async/await, 이벤트 기반 아키텍처

---

## 설치 및 설정

### Q: 프로젝트에 GAS Core를 어떻게 추가하나요?
**A:**
1. `GAS_Core` 폴더를 프로젝트의 `Assets` 폴더에 복사
2. Unity가 자동으로 스크립트를 컴파일할 때까지 대기
3. 게임 오브젝트에 `AbilitySystem` 컴포넌트 추가

### Q: 의존성이나 외부 패키지가 필요한가요?
**A:** 아니요. GAS Core는 Unity 내장 기능만 사용하므로 추가 패키지가 필요하지 않습니다.

### Q: 기존 프로젝트에 추가할 때 충돌 위험은?
**A:** GAS Core는 `GAS.Core` 네임스페이스를 사용하므로 다른 시스템과의 네임스페이스 충돌 위험이 거의 없습니다.

---

## 어빌리티 생성

### Q: 어빌리티 데이터를 어떻게 생성하나요?
**A:**
Unity Editor에서:
1. Project 창에서 우클릭
2. `Create → GAS → AbilityData` 선택
3. Inspector에서 어빌리티 속성 설정

코드에서:
```csharp
var abilityData = ScriptableObject.CreateInstance<AbilityData>();
abilityData.AbilityId = "fireball";
abilityData.AbilityName = "Fire Ball";
// ... 기타 설정
```

### Q: 복잡한 어빌리티 로직은 어떻게 구현하나요?
**A:** 여러 방법이 있습니다:
1. **커스텀 실행기**: `AbilityExecutor`를 상속하여 구현
2. **어빌리티 확장**: `Ability` 클래스를 상속하여 확장
3. **이벤트 활용**: 어빌리티 이벤트에 복잡한 로직 연결

### Q: 어빌리티 간 연계나 콤보 시스템을 만들 수 있나요?
**A:** 네, 태그 시스템과 이벤트를 활용하면 됩니다:
```csharp
// 어빌리티에 태그 설정
abilityData.AbilityTags.Add("Combo_Starter");

// 이벤트로 연계 감지
abilitySystem.OnAbilityUsed += (abilityId) => {
    if (HasTag(abilityId, "Combo_Starter")) {
        EnableComboWindow();
    }
};
```

---

## 리소스 시스템

### Q: 마나, 스태미나 외에 커스텀 리소스를 추가할 수 있나요?
**A:** 네, 문자열 기반으로 되어 있어 어떤 리소스든 추가 가능합니다:
```csharp
abilitySystem.SetMaxResource("Energy", 50f);
abilitySystem.SetMaxResource("Rage", 100f);
abilitySystem.SetMaxResource("Focus", 30f);
```

### Q: 리소스 자동 회복 속도를 런타임에 변경할 수 있나요?
**A:** 현재는 Inspector에서만 설정 가능하지만, 커스텀 리소스 매니저를 만들어 구현할 수 있습니다:
```csharp
public class CustomResourceManager : MonoBehaviour
{
    private IAbilitySystem abilitySystem;
    private float manaRegenRate = 5f;

    void Update()
    {
        abilitySystem.RestoreResource("Mana", manaRegenRate * Time.deltaTime);
    }
}
```

### Q: 리소스 부족 시 알림은 어떻게 처리하나요?
**A:** `CanUseAbility`를 사용하여 미리 확인하거나, 이벤트를 활용합니다:
```csharp
if (!abilitySystem.CanUseAbility("fireball"))
{
    // 리소스 부족 등의 이유로 사용 불가
    ShowNotEnoughResourcesMessage();
}
```

---

## 성능 및 최적화

### Q: 많은 수의 어빌리티가 있을 때 성능은 어떤가요?
**A:** GAS Core는 효율적으로 설계되었지만, 대량의 어빌리티가 있다면:
1. **Object Pooling**: 자주 사용되는 이펙트는 풀링 사용
2. **업데이트 최적화**: 쿨다운 업데이트 빈도 조절
3. **이벤트 구독 관리**: 불필요한 이벤트 구독 해제

### Q: 쿨다운 업데이트가 매 프레임 실행되어 성능에 영향을 주나요?
**A:** 기본적으로는 매 프레임 업데이트되지만, 다음과 같이 최적화할 수 있습니다:
```csharp
public class OptimizedAbilitySystem : AbilitySystem
{
    private float lastUpdateTime;
    private const float UPDATE_INTERVAL = 0.1f;

    protected override void Update()
    {
        if (Time.time - lastUpdateTime >= UPDATE_INTERVAL)
        {
            base.Update();
            lastUpdateTime = Time.time;
        }
    }
}
```

---

## 확장 및 커스터마이징

### Q: 기존 어빌리티 클래스를 확장하려면?
**A:** `Ability` 클래스를 상속하여 확장할 수 있습니다:
```csharp
public class EnhancedAbility : Ability
{
    private int enhancementLevel;

    protected override async Task ExecuteAbilityEffect(CancellationToken cancellationToken)
    {
        await base.ExecuteAbilityEffect(cancellationToken);

        // 추가 로직
        if (enhancementLevel > 0)
        {
            await ExecuteEnhancedEffect(cancellationToken);
        }
    }
}
```

### Q: 어빌리티 시스템 자체를 확장하려면?
**A:** `AbilitySystem` 클래스를 상속하거나 `IAbilitySystem` 인터페이스를 새로 구현할 수 있습니다:
```csharp
public class RPGAbilitySystem : AbilitySystem
{
    [SerializeField] private SkillTree skillTree;

    public void LearnSkill(string skillId)
    {
        if (skillTree.CanLearn(skillId))
        {
            var skillData = skillTree.GetSkillData(skillId);
            AddAbility(skillData);
        }
    }
}
```

### Q: 다른 시스템과 통합하려면?
**A:** 이벤트와 인터페이스를 활용하여 느슨한 결합을 유지합니다:
```csharp
// 사운드 시스템과 통합
abilitySystem.OnAbilityUsed += soundManager.PlayAbilitySound;

// UI 시스템과 통합
abilitySystem.OnResourceChanged += uiManager.UpdateResourceBar;

// 애니메이션 시스템과 통합
abilitySystem.OnAbilityUsed += animationController.PlayAbilityAnimation;
```

---

## 트러블슈팅

### Q: "CS1503" 오류가 발생해요
**A:** `AddAbility` 메서드 매개변수 타입 불일치 문제입니다. 다음을 확인해주세요:
```csharp
// 올바른 사용법
abilitySystem.AddAbility(abilityData); // IAbilityData 타입
abilitySystem.AddAbility(ability);     // IAbility 타입
```

### Q: 어빌리티가 실행되지 않아요
**A:** 다음을 순서대로 확인해보세요:
1. `CanUseAbility`로 사용 가능 여부 확인
2. 리소스가 충분한지 확인
3. 쿨다운 상태 확인
4. 태그로 인한 차단 여부 확인

```csharp
if (!abilitySystem.CanUseAbility("ability_id"))
{
    Debug.Log("Cannot use ability");
    // 이유 파악을 위한 디버깅
}
```

### Q: 이벤트가 호출되지 않아요
**A:** 이벤트 구독 시점과 객체 생명주기를 확인해주세요:
```csharp
void Start() // Awake보다는 Start에서 구독
{
    var abilitySystem = GetComponent<AbilitySystem>();
    abilitySystem.OnAbilityUsed += OnAbilityUsed;
}

void OnDestroy()
{
    // 메모리 누수 방지를 위한 구독 해제
    if (abilitySystem != null)
    {
        abilitySystem.OnAbilityUsed -= OnAbilityUsed;
    }
}
```

### Q: 리소스 값이 음수가 되어요
**A:** GAS Core는 기본적으로 리소스 값을 0 이하로 내려가지 않게 보호합니다. 만약 음수가 된다면:
1. 직접적인 리소스 조작을 확인
2. 커스텀 코드에서 검증 로직 추가

```csharp
public void SetResource(string resourceType, float value)
{
    float clampedValue = Mathf.Max(0, value);
    abilitySystem.SetResource(resourceType, clampedValue);
}
```

### Q: Unity 콘솔에 한글이 깨져서 나와요
**A:** 한글 주석이나 Debug.Log의 인코딩 문제입니다. 다음을 시도해보세요:
1. Visual Studio 인코딩을 UTF-8로 설정
2. Unity Editor의 로케일 설정 확인
3. 파일을 UTF-8로 다시 저장

### Q: 멀티플레이어에서 동기화 문제가 있어요
**A:** GAS Core는 로컬 전용입니다. 멀티플레이어에서는:
1. 어빌리티 사용을 RPC로 전송
2. 결과만 동기화하고 로직은 각 클라이언트에서 실행
3. 서버에서 검증 로직 구현

```csharp
[PunRPC]
void UseAbilityRPC(string abilityId)
{
    // 클라이언트에서 시각 효과만 재생
    PlayAbilityVisualEffects(abilityId);
}
```

### Q: 메모리 누수가 의심돼요
**A:** 다음을 확인해주세요:
1. 이벤트 구독 해제
2. Ability 객체의 Dispose 호출
3. CancellationToken 정리

```csharp
void OnDestroy()
{
    // 모든 어빌리티 정리
    foreach (var ability in abilitySystem.Abilities.Values)
    {
        if (ability is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}
```

---

## 추가 도움말

### 디버깅 팁
1. **로그 활용**: Debug.Log로 어빌리티 상태 추적
2. **Context Menu**: `[ContextMenu]`로 테스트 메서드 추가
3. **Inspector 확인**: 런타임에 어빌리티 시스템 상태 확인

### 성능 프로파일링
1. **Unity Profiler**: CPU 사용량 모니터링
2. **메모리 프로파일러**: 메모리 누수 확인
3. **프레임 디버거**: 이벤트 호출 빈도 확인

### 커뮤니티 지원
- GitHub Issues에 버그 리포트 및 기능 요청
- 사용 예제와 확장 사례 공유
- 코드 리뷰 및 개선 제안

더 자세한 질문이나 특정 사용 사례에 대한 도움이 필요하시면 언제든 문의해 주세요!