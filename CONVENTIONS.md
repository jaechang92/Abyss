# CONVENTIONS.md

Abyss 프로젝트의 코드 컨벤션과 Git 브랜치 전략. 사람과 에이전트가 공유하는 규칙 문서이며,
`CLAUDE.md`가 `@CONVENTIONS.md`로 임포트해 세션마다 자동 로드한다.

## 코드 컨벤션 (CodeConventionChecker 기반)

프로젝트에 내장된 컨벤션 검사기(`Assets/Plugins/CodeConvention_Core/Editor/`)가 아래 규칙을 강제한다:

| 대상 | 규칙 | 심각도 |
|------|------|--------|
| 클래스명 | PascalCase | Error |
| 메서드명 | PascalCase | Error |
| private 필드 | camelCase 또는 _camelCase | Error |
| public 필드/프로퍼티 | PascalCase | Warning |
| 상수 | UPPER_SNAKE_CASE | Warning |
| bool 변수 | is/has/can 접두어 권장 | Warning |
| 인터페이스 | I 접두어 필수 | Error |
| 이벤트 | On + 동사 형식 권장 | Warning |
| SerializeField | camelCase | Warning |
| 파일 라인 수 | 최대 500줄 | Warning |

Unity 에디터에서 `Tools > Code Convention > Convention Checker Window` 또는 Project 윈도우 우클릭 `Check Code Convention`으로 검사 가능.

## Unity 6 주의사항

- `Rigidbody.velocity` 대신 `Rigidbody.linearVelocity` 사용
- `FindObjectOfType<T>()` 대신 `FindAnyObjectByType<T>()` 사용
- 기타 CS0618 deprecated 경고 발생하는 API는 최신 대체 API 사용

## 개발 규칙

- Coroutine 사용 금지, Unity `Awaitable` 사용 (UniTask 미도입 — ADR-002 참조)
- 변수명에 언더스코어(`_`) 접두어 사용하지 않음 (camelCase 그대로)
- 한글 주석 허용, UTF-8 인코딩 유지
- 한 파일 500줄 초과 시 분할 작성

## Git 브랜치 전략 (Simplified Git Flow)

| 브랜치 | 용도 | 분기 기준 | 병합 대상 |
|--------|------|-----------|-----------|
| `master` | 안정 빌드 (릴리스 가능 상태만) | - | - |
| `develop` | 개발 통합 브랜치 | master | master (마일스톤) |
| `feature/{기능명}` | 새 기능 개발 | develop | develop |
| `fix/{버그명}` | 버그 수정 | develop | develop |
| `hotfix/{긴급명}` | 긴급 수정 | master | master + develop |

### 브랜치 운용 규칙

- **`develop` 및 `master`에 직접 커밋 절대 금지** — 모든 작업은 반드시 feature/fix 브랜치에서 수행
- 새 작업 시작 시 develop에서 `feature/` 또는 `fix/` 브랜치를 먼저 분기한 뒤 커밋 시작
- 작업 완료 → develop으로 병합 → feature/fix 브랜치 삭제
- 마일스톤 단위로 develop → master 병합 (master 병합은 반드시 develop 또는 hotfix 경유)
- 브랜치명은 kebab-case 사용 (예: `feature/sfx-system`, `fix/tint-layer-sync`)
- 병합 방향이나 브랜치 컨벤션이 불명확하면 사용자에게 먼저 확인할 것
