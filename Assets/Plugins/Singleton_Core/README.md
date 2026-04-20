# Singleton_Core

Unity용 범용 싱글톤 매니저 패턴

## 특징

- **자동 인스턴스 관리**: 없으면 자동 생성
- **중복 방지**: 여러 인스턴스 자동 제거
- **씬 전환 안전**: DontDestroyOnLoad 자동 적용
- **디버그 지원**: 모든 싱글톤 추적 및 로그

## 사용 방법

### 1. 영구 싱글톤 (씬 전환 유지)

```csharp
public class GameManager : SingletonManager<GameManager>
{
    // 자동으로 DontDestroyOnLoad 적용

    protected override void OnAwake()
    {
        // 초기화 로직
    }
}

// 사용
GameManager.Instance.DoSomething();
if (GameManager.HasInstance) { ... }
```

### 2. 씬 싱글톤 (씬 전환 시 파괴)

```csharp
public class LevelManager : SceneSingletonManager<LevelManager>
{
    // 씬 전환 시 파괴됨

    protected override void OnAwake()
    {
        // 초기화 로직
    }
}
```

### 3. 안전한 접근

```csharp
// HasInstance 체크
if (GameManager.HasInstance)
{
    GameManager.Instance.DoSomething();
}

// 안전한 접근 (null 반환 가능)
var manager = GameManager.GetInstanceSafe();
manager?.DoSomething();
```

### 4. 디버그

```csharp
// 모든 싱글톤 로그
SingletonManager<GameManager>.LogAllSingletons();

// 모든 싱글톤 목록
var singletons = SingletonManager<GameManager>.GetAllSingletons();

// 특정 타입 확인
bool exists = SingletonManager<GameManager>.HasSingletonOfType<AudioManager>();
```

## 의존성

없음 (독립 패키지)

## 다른 프로젝트에서 사용

1. `Singleton_Core` 폴더 복사
2. 매니저 클래스에서 `SingletonManager<T>` 상속

## 라이선스

MIT License
