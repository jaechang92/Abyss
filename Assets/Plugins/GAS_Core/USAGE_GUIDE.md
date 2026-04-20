# GAS Core 사용 가이드

## 목차
1. [빠른 시작](#빠른-시작)
2. [기본 설정](#기본-설정)
3. [어빌리티 생성](#어빌리티-생성)
4. [리소스 시스템](#리소스-시스템)
5. [이벤트 시스템](#이벤트-시스템)
6. [커스텀 실행기](#커스텀-실행기)
7. [고급 기능](#고급-기능)
8. [예제 시나리오](#예제-시나리오)

---

## 빠른 시작

### 1. 기본 컴포넌트 추가

```csharp
// 게임 오브젝트에 AbilitySystem 컴포넌트 추가
var abilitySystem = gameObject.AddComponent<AbilitySystem>();
```

### 2. 어빌리티 데이터 생성

Unity Editor에서 `Create → GAS → AbilityData`로 ScriptableObject 생성

### 3. 어빌리티 사용

```csharp
// 어빌리티 추가
abilitySystem.AddAbility(abilityData);

// 어빌리티 사용
if (abilitySystem.CanUseAbility("fireball"))
{
    abilitySystem.TryUseAbility("fireball");
}
```

---

## 기본 설정

### AbilitySystem 컴포넌트 설정

```csharp
public class PlayerController : MonoBehaviour
{
    private IAbilitySystem abilitySystem;

    void Start()
    {
        // AbilitySystem 가져오기
        abilitySystem = GetComponent<AbilitySystem>();

        // 이벤트 등록
        abilitySystem.OnAbilityUsed += OnAbilityUsed;
        abilitySystem.OnResourceChanged += OnResourceChanged;
    }

    private void OnAbilityUsed(string abilityId)
    {
        Debug.Log($"Ability used: {abilityId}");
    }

    private void OnResourceChanged(string resourceType, float newValue)
    {
        Debug.Log($"{resourceType}: {newValue}");
    }
}
```

### 인스펙터 설정

**Initial Abilities**: 시작 시 자동으로 등록될 어빌리티들
**Use Resource System**: 리소스 시스템 사용 여부
**Resource Configs**: 마나, 스태미나 등 리소스 설정

---

## 어빌리티 생성

### AbilityData 설정

```
Basic Info:
- Ability Id: "fireball"
- Ability Name: "Fire Ball"
- Description: "화염구를 발사합니다"

Ability Type:
- Ability Type: Active
- Target Type: SingleTarget

Execution Settings:
- Cooldown Duration: 3.0
- Cast Time: 1.0
- Duration: 0.0
- Can Be Cancelled: true

Resource Costs:
- Mana: 20

Range/Targeting:
- Range: 10.0
- Radius: 2.0

Effects:
- Damage Value: 50.0
- Damage Type: Magical
```

### 코드로 어빌리티 생성

```csharp
// Runtime에 어빌리티 데이터 생성
var fireballData = ScriptableObject.CreateInstance<AbilityData>();
fireballData.AbilityId = "fireball";
fireballData.AbilityName = "Fire Ball";
fireballData.AbilityType = AbilityType.Active;
fireballData.CooldownDuration = 3f;
fireballData.DamageValue = 50f;

// 리소스 비용 설정
fireballData.ResourceCosts.Add("Mana", 20f);

// 어빌리티 시스템에 추가
abilitySystem.AddAbility(fireballData);
```

---

## 리소스 시스템

### 리소스 설정

```csharp
// 최대 리소스 설정
abilitySystem.SetMaxResource("Mana", 100f);
abilitySystem.SetMaxResource("Stamina", 100f);

// 현재 리소스 설정
abilitySystem.SetResource("Mana", 100f);
abilitySystem.SetResource("Stamina", 80f);
```

### 리소스 관리

```csharp
// 리소스 확인
float currentMana = abilitySystem.GetResource("Mana");
float maxMana = abilitySystem.GetMaxResource("Mana");

// 리소스 소모
bool consumed = abilitySystem.ConsumeResource("Mana", 20f);

// 리소스 회복
abilitySystem.RestoreResource("Mana", 10f);
```

### 리소스 자동 회복 설정

인스펙터에서 Resource Configs 설정:
```
Resource Type: "Mana"
Max Value: 100
Initial Value: 100
Regeneration Rate: 5  // 초당 5씩 회복
```

---

## 이벤트 시스템

### 기본 이벤트

```csharp
public class UIManager : MonoBehaviour
{
    void Start()
    {
        var abilitySystem = FindObjectOfType<AbilitySystem>();

        // 어빌리티 관련 이벤트
        abilitySystem.OnAbilityUsed += UpdateCooldownUI;
        abilitySystem.OnAbilityAdded += AddAbilityToUI;
        abilitySystem.OnAbilityRemoved += RemoveAbilityFromUI;

        // 리소스 관련 이벤트
        abilitySystem.OnResourceChanged += UpdateResourceUI;
    }

    private void UpdateCooldownUI(string abilityId)
    {
        // 쿨다운 UI 업데이트
    }

    private void UpdateResourceUI(string resourceType, float value)
    {
        // 리소스 바 업데이트
    }
}
```

### 어빌리티별 세부 이벤트

```csharp
// 특정 어빌리티의 이벤트 등록
if (abilitySystem.TryGetAbility("fireball", out var ability))
{
    ability.OnAbilityStarted += OnFireballStarted;
    ability.OnAbilityCompleted += OnFireballCompleted;
    ability.OnCooldownStarted += OnFireballCooldownStarted;
}

private void OnFireballStarted(IAbility ability)
{
    // 시전 시작 효과
    PlayCastAnimation();
}

private void OnFireballCompleted(IAbility ability)
{
    // 시전 완료 효과
    SpawnFireballEffect();
}
```

---

## 커스텀 실행기

### 기본 실행기 생성

```csharp
[CreateAssetMenu(fileName = "TeleportExecutor", menuName = "GAS/Executors/TeleportExecutor")]
public class TeleportExecutor : AbilityExecutor
{
    [Header("텔레포트 설정")]
    [SerializeField] private float teleportDistance = 5f;
    [SerializeField] private LayerMask obstacleLayer;

    public override async Task<bool> ExecuteAsync(GameObject caster, IAbilityData data, List<IAbilityTarget> targets)
    {
        try
        {
            // 실행 전 처리 (이펙트, 사운드)
            await OnPreExecute(caster, data);

            // 텔레포트 로직
            Vector3 teleportPosition = CalculateTeleportPosition(caster);

            // 장애물 체크
            if (IsValidTeleportPosition(teleportPosition))
            {
                caster.transform.position = teleportPosition;

                // 텔레포트 이펙트
                SpawnEffect(teleportPosition);

                return true;
            }

            return false;
        }
        catch (Exception e)
        {
            Debug.LogError($"Teleport execution failed: {e.Message}");
            return false;
        }
    }

    private Vector3 CalculateTeleportPosition(GameObject caster)
    {
        Vector3 direction = caster.transform.forward;
        return caster.transform.position + direction * teleportDistance;
    }

    private bool IsValidTeleportPosition(Vector3 position)
    {
        return !Physics.CheckSphere(position, 0.5f, obstacleLayer);
    }
}
```

### 복잡한 실행기 예제

```csharp
[CreateAssetMenu(fileName = "ChainLightningExecutor", menuName = "GAS/Executors/ChainLightningExecutor")]
public class ChainLightningExecutor : AbilityExecutor
{
    [Header("체인 라이트닝 설정")]
    [SerializeField] private int maxChainCount = 3;
    [SerializeField] private float chainRange = 8f;
    [SerializeField] private float damageReduction = 0.8f; // 체인마다 80%로 감소

    public override async Task<bool> ExecuteAsync(GameObject caster, IAbilityData data, List<IAbilityTarget> targets)
    {
        if (targets.Count == 0) return false;

        var currentTarget = targets[0];
        var chainedTargets = new HashSet<IAbilityTarget>();
        float currentDamage = (data as AbilityData)?.DamageValue ?? 100f;

        for (int chain = 0; chain < maxChainCount && currentTarget != null; chain++)
        {
            // 현재 타겟에게 데미지
            currentTarget.TakeDamage(currentDamage);
            chainedTargets.Add(currentTarget);

            // 라이트닝 이펙트
            SpawnLightningEffect(GetPreviousTarget(chainedTargets), currentTarget);

            // 다음 타겟 찾기
            currentTarget = FindNextChainTarget(currentTarget, chainedTargets);
            currentDamage *= damageReduction;

            // 체인 딜레이
            await Task.Delay(200);
        }

        return true;
    }

    private IAbilityTarget FindNextChainTarget(IAbilityTarget currentTarget, HashSet<IAbilityTarget> chainedTargets)
    {
        var nearbyTargets = FindTargetsInRange(currentTarget.Transform.position, chainRange, LayerMask.GetMask("Enemy"));

        foreach (var target in nearbyTargets)
        {
            if (!chainedTargets.Contains(target))
            {
                return target;
            }
        }

        return null;
    }
}
```

---

## 고급 기능

### 태그 시스템

```csharp
// 어빌리티 데이터에서 태그 설정
abilityData.AbilityTags.Add("Fire");
abilityData.AbilityTags.Add("Projectile");
abilityData.CancelTags.Add("Silenced");
abilityData.BlockTags.Add("Stunned");

// 게임플레이 컨텍스트에서 상태 확인
var context = GetComponent<IGameplayContext>();
context.SetState("Silenced", true); // 침묵 상태

// 침묵 상태에서는 CancelTags에 "Silenced"가 있는 어빌리티 사용 불가
```

### 커스텀 게임플레이 컨텍스트

```csharp
public class RPGGameplayContext : MonoBehaviour, IGameplayContext
{
    [SerializeField] private PlayerStats stats;
    [SerializeField] private StatusEffectManager statusEffects;

    public bool CanAct => !statusEffects.HasEffect("Stun") && !statusEffects.HasEffect("Sleep");

    public bool IsInState(string stateName)
    {
        return statusEffects.HasEffect(stateName);
    }

    // 커스텀 데이터 예제
    public T GetCustomData<T>(string key) where T : class
    {
        switch (key)
        {
            case "Stats":
                return stats as T;
            case "StatusEffects":
                return statusEffects as T;
            default:
                return null;
        }
    }
}
```

### 어빌리티 확장

```csharp
public class EnhancedAbility : Ability
{
    private int enhancementLevel;

    protected override async Task ExecuteAbilityEffect(CancellationToken cancellationToken)
    {
        // 기본 효과 실행
        await base.ExecuteAbilityEffect(cancellationToken);

        // 강화 레벨에 따른 추가 효과
        if (enhancementLevel > 0)
        {
            await ExecuteEnhancedEffect(cancellationToken);
        }
    }

    private async Task ExecuteEnhancedEffect(CancellationToken cancellationToken)
    {
        // 강화된 효과 로직
        float bonusDamage = enhancementLevel * 10f;
        // 추가 데미지 적용
    }

    public void Enhance()
    {
        enhancementLevel++;
        Debug.Log($"Ability enhanced to level {enhancementLevel}");
    }
}
```

---

## 예제 시나리오

### 시나리오 1: 기본 전투 시스템

```csharp
public class BasicCombatExample : MonoBehaviour
{
    [SerializeField] private AbilityData[] combatAbilities;
    private IAbilitySystem abilitySystem;

    void Start()
    {
        SetupAbilitySystem();
        SetupCombatAbilities();
    }

    void Update()
    {
        HandleInput();
    }

    private void SetupAbilitySystem()
    {
        abilitySystem = GetComponent<AbilitySystem>();

        // 리소스 설정
        abilitySystem.SetMaxResource("Mana", 100f);
        abilitySystem.SetResource("Mana", 100f);
    }

    private void SetupCombatAbilities()
    {
        foreach (var ability in combatAbilities)
        {
            abilitySystem.AddAbility(ability);
        }
    }

    private void HandleInput()
    {
        if (Input.GetKeyDown(KeyCode.Q)) TryUseAbility("basic_attack");
        if (Input.GetKeyDown(KeyCode.W)) TryUseAbility("fireball");
        if (Input.GetKeyDown(KeyCode.E)) TryUseAbility("heal");
        if (Input.GetKeyDown(KeyCode.R)) TryUseAbility("ultimate");
    }

    private void TryUseAbility(string abilityId)
    {
        if (abilitySystem.CanUseAbility(abilityId))
        {
            abilitySystem.TryUseAbility(abilityId);
        }
        else
        {
            Debug.Log($"Cannot use {abilityId}");
        }
    }
}
```

### 시나리오 2: RPG 스킬 시스템

```csharp
public class RPGSkillSystem : MonoBehaviour
{
    [SerializeField] private SkillTree skillTree;
    private IAbilitySystem abilitySystem;
    private Dictionary<string, int> skillLevels = new Dictionary<string, int>();

    void Start()
    {
        abilitySystem = GetComponent<AbilitySystem>();
        LoadLearnedSkills();
    }

    public void LearnSkill(string skillId)
    {
        if (skillTree.CanLearnSkill(skillId))
        {
            var skillData = skillTree.GetSkillData(skillId);
            abilitySystem.AddAbility(skillData);
            skillLevels[skillId] = 1;

            Debug.Log($"Learned skill: {skillData.AbilityName}");
        }
    }

    public void UpgradeSkill(string skillId)
    {
        if (skillLevels.ContainsKey(skillId))
        {
            skillLevels[skillId]++;
            var upgradedData = skillTree.GetUpgradedSkillData(skillId, skillLevels[skillId]);

            // 기존 스킬 제거 후 업그레이드된 스킬 추가
            abilitySystem.RemoveAbility(skillId);
            abilitySystem.AddAbility(upgradedData);

            Debug.Log($"Upgraded {skillId} to level {skillLevels[skillId]}");
        }
    }
}
```

### 시나리오 3: 멀티플레이어 동기화

```csharp
public class NetworkedAbilitySystem : MonoBehaviour
{
    private IAbilitySystem abilitySystem;

    void Start()
    {
        abilitySystem = GetComponent<AbilitySystem>();
        abilitySystem.OnAbilityUsed += OnAbilityUsedLocal;
    }

    private void OnAbilityUsedLocal(string abilityId)
    {
        // 네트워크로 다른 플레이어들에게 전송
        SendAbilityUsedRPC(abilityId);
    }

    [PunRPC] // Photon 예제
    void SendAbilityUsedRPC(string abilityId)
    {
        // 다른 클라이언트에서 어빌리티 시각 효과만 재생
        PlayAbilityVisualEffects(abilityId);
    }

    private void PlayAbilityVisualEffects(string abilityId)
    {
        // 네트워크를 통해 받은 어빌리티의 시각 효과만 재생
        // 실제 게임플레이 로직은 각자의 클라이언트에서 처리
    }
}
```

---

## 디버깅 및 최적화

### 디버깅 도구

```csharp
public class GASDebugger : MonoBehaviour
{
    private IAbilitySystem abilitySystem;

    void Start()
    {
        abilitySystem = GetComponent<AbilitySystem>();
    }

    [ContextMenu("Print All Abilities")]
    public void PrintAllAbilities()
    {
        foreach (var ability in abilitySystem.Abilities)
        {
            Debug.Log($"Ability: {ability.Value.Name}, State: {ability.Value.State}, " +
                     $"Cooldown: {ability.Value.CooldownRemaining:F1}s");
        }
    }

    [ContextMenu("Print Resources")]
    public void PrintResources()
    {
        var resourceTypes = new[] { "Mana", "Stamina", "Energy" };
        foreach (var type in resourceTypes)
        {
            if (abilitySystem.HasResource(type))
            {
                Debug.Log($"{type}: {abilitySystem.GetResource(type):F1}/{abilitySystem.GetMaxResource(type):F1}");
            }
        }
    }
}
```

### 성능 최적화

```csharp
public class OptimizedAbilitySystem : AbilitySystem
{
    private float lastUpdateTime;
    private const float UPDATE_INTERVAL = 0.1f; // 10fps로 업데이트

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

이 가이드를 통해 GAS Core 시스템을 효과적으로 활용하실 수 있습니다. 추가 질문이나 특정 기능에 대한 더 자세한 설명이 필요하시면 언제든 말씀해 주세요!