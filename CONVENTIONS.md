# Abyss 코드·Git 규칙

코드 수정·Git 작업 때 참조한다. 작업 범위·검증 담당·세션 종료는 [AGENTS.md](AGENTS.md)를 따른다.
버전은 `ProjectSettings/ProjectVersion.txt`와 `Packages/manifest.json`, 파일 형식은 [.editorconfig](.editorconfig)를 기준으로 한다.

## 이름 규칙

| 대상 | 프로젝트 규칙 |
|---|---|
| 클래스·메서드·프로퍼티(접근 수준 무관)·public 필드 | PascalCase |
| private 필드·지역 변수·매개변수 | camelCase, `_` 접두어 사용하지 않음 (static readonly는 아래 예외) |
| private static readonly | 고정 값·설정은 PascalCase, 재사용 버퍼·작업 목록은 camelCase 권장. 둘 다 허용하며 `_` 접두어 금지 |
| `[SerializeField]` 필드 | camelCase |
| 상수 | UPPER_SNAKE_CASE |
| 인터페이스 | I + PascalCase |
| bool | 접근 수준에 맞춰 is/has/can 또는 Is/Has/Can 접두어 권장 |
| 이벤트 | On + 동사 형태 권장 |

기존 static readonly의 `BURN_COLOR` 같은 UPPER_SNAKE_CASE는 일괄 변경하지 않는다. 새 선언은 위 기준을 따르며, `const`의 UPPER_SNAKE_CASE 규칙은 유지한다.

직렬화 필드는 `[SerializeField] private`를 우선한다. 기존 public 직렬화 필드는 명명 규칙만을 이유로 일괄 변경하지 않는다. 기존 이름을 바꿀 때는 코드 참조뿐 아니라 씬·프리팹·데이터의 직렬화 연결 보존 방법을 확인한다. 외부 API·인터페이스 구현에 필요한 이름은 해당 계약을 따른다.

## 파일·구조

- C#은 `.editorconfig`에 따라 UTF-8 BOM, CRLF, 공백 4칸을 사용한다. 한글 주석을 허용한다.
- Unity 메타·에셋 파일은 UTF-8, LF를 유지한다. 관련 없는 파일의 포맷을 일괄 변경하지 않는다.
- C# 파일은 500줄 이내로 작성한다. 초과 시 책임 단위로 분리하고, 기존 클래스의 역할을 유지해야 할 때 partial을 활용한다. 줄 수만 줄이기 위해 여러 책임을 한 줄에 압축하지 않는다.
- 기존 대형 파일의 전체 분할은 별도 범위로 판단한다. 요청한 변경과 무관한 재구성을 함께 수행하지 않는다.

## Unity 구현

- 비동기는 Unity `Awaitable`을 사용한다. Coroutine은 사용하지 않으며 UniTask는 도입하지 않는다. 근거: [ADR-002](Docs/adr/002-unitask-defer.md).
- 사용 중인 Unity 버전의 API를 기준으로 수정한다. deprecated API는 경고가 지시하는 대안을 확인한다.
- API 교체 전 대상 타입과 동작을 확인한다. 객체 검색은 순서·비활성 객체 포함 여부·여러 인스턴스 중 선택 기준을 보존하며, 이름만 보고 일괄 치환하지 않는다.

## 컨벤션 검사기와의 관계

[CodeConventionChecker.cs](Assets/Plugins/CodeConvention_Core/Editor/CodeConventionChecker.cs)는 정규식 기반 보조 도구다. 검사 통과가 위 규칙 전체의 준수를 보장하지 않는다.

- 클래스·메서드·private 필드·인터페이스의 일부 위반은 Error, public 필드/프로퍼티·상수·bool·이벤트·SerializeField·500줄 초과는 Warning으로 보고한다.
- `PrivateFieldCamelCase`는 기존 일반 private/readonly 필드의 대문자 시작 검사 범위를 유지한다. static·배열 등 지원하지 않던 선언까지 확장하지 않는다. `Fader => ...` 같은 식 본문 프로퍼티는 필드 검사에서 제외한다.
- `PrivateFieldNoUnderscorePrefix`는 `_` 접두어를 별도의 Error로 탐지하며 static·readonly·volatile 및 단순 배열 선언을 포함한다. static readonly도 `_` 접두어는 금지한다. 지역 변수·매개변수와 복잡한 선언의 명명은 별도로 판단한다.
- 검사기의 경고 등급과 프로젝트의 필수/권장 구분은 다르다. 오탐 여부는 실제 선언과 계약을 보고 판단한다.
- 사용자가 확인할 때: `Tools > Code Convention > Convention Checker Window` 또는 Project 우클릭 `Check Code Convention`.
- Warning의 추천 이름·개별/일괄 미리보기·적용·최근 적용 묶음 복원을 제공한다. 자동 적용은 제한된 bool 지역 변수·지역 상수에 한하며 필드·직렬화 데이터는 IDE에서 연결을 확인한다. [사용 안내](Docs/technical/code-convention-tool.md).

## Git 브랜치

| 브랜치 | 역할 | 분기 기준 | 병합 대상 |
|---|---|---|---|
| `master` | 릴리스 가능한 안정 버전 | — | — |
| `develop` | 개발 통합 | master | master (마일스톤) |
| `feature/{기능명}` | 새 기능 | develop | develop |
| `fix/{버그명}` | 일반 수정 | develop | develop |
| `hotfix/{긴급명}` | 릴리스 긴급 수정 | master | master와 develop |

- `develop`·`master`에 직접 커밋하지 않는다. 커밋은 feature/fix/hotfix 작업 브랜치에서 한다.
- 브랜치명은 kebab-case로 작성한다. 예: `feature/sfx-system`, `fix/tint-layer-sync`.
- 작업 전 현재 브랜치와 미커밋 변경을 확인한다. 기존 변경이 있으면 임의로 전환·이동·폐기하지 않는다. 커밋 전에 이번 작업의 파일과 변경 부분을 구분한다.
- 사용자가 요청한 통합·배포 범위에서만 커밋·병합·푸시를 수행한다. 검증 결과와 미검증 항목을 구분하고, 사용자 확인을 AI 테스트 통과로 기록하지 않는다.
- 마일스톤은 develop → master로 병합한다. 긴급 수정은 hotfix → master 및 develop에 반영한다. 브랜치 삭제는 필요한 경우에만 한다.
- 위 표로 결정할 수 없는 병합 대상이나 기존 작업과의 충돌만 사용자에게 확인한다.
