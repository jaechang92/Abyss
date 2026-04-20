# Changelog

## [1.0.0] - 2024-09-20

### Added
- 범용 어빌리티 시스템 초기 릴리즈
- `IAbilitySystem` 및 `AbilitySystem` 구현
- `IAbility` 및 `Ability` 구현
- `AbilityData` ScriptableObject 기반 데이터 시스템
- 선택적 리소스 시스템 (마나, 스태미나 등)
- 태그 기반 어빌리티 상호작용 시스템
- 비동기 어빌리티 실행 지원
- 쿨다운 시스템
- 타겟팅 시스템 (단일, 범위, 방향성 등)
- 실행기 시스템 (`AbilityExecutor`, `DamageExecutor`, `HealExecutor`)
- 게임플레이 컨텍스트 시스템
- 기본 게임플레이 효과 인터페이스

### Features
- **장르 무관 설계**: 2D/3D, 다양한 게임 장르에서 사용 가능
- **모듈화**: 필요한 기능만 선택적으로 사용
- **확장성**: 인터페이스 기반으로 쉽게 확장
- **타입 안전성**: 강력한 타입 시스템
- **성능 최적화**: 효율적인 메모리 사용과 실행 속도

### Documentation
- 포괄적인 README 문서
- API 참조 문서 (코드 주석)
- 사용 예제 및 가이드

### Technical Details
- Unity 2022.3+ 지원
- .NET Standard 2.1 호환
- async/await 패턴 활용
- ScriptableObject 기반 데이터 관리
- 이벤트 기반 시스템
- 메모리 효율적인 설계