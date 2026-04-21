# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## 프로젝트 개요

Abyss는 Unity 6 6000.0.68f1 기반의 게임 프로젝트이다. URP 17.0.4를 사용한다.

## 기술 스택

- **엔진**: Unity 6 6000.0.68f1
- **렌더 파이프라인**: URP 17.0.4
- **입력 시스템**: Input System (New Input System)
- **비동기**: Unity `Awaitable` (UniTask 미도입 — ADR-002, Coroutine 사용 금지)
- **언어**: C# (.NET Standard 2.1)

## 프로젝트 구조

```
Assets/
  Art/                 # 아트 리소스 (Sprites, Tiles)
  Audio/               # 사운드 (BGM, SFX)
  Data/                # ScriptableObject 데이터
  Editor/              # 에디터 전용 스크립트
  Plugins/
    CodeConvention_Core/Editor/  # 코드 컨벤션 검사 에디터 도구
  Prefabs/             # 프리팹
  Resources/Data/      # 런타임 로드 데이터
  Scenes/              # 씬 파일
  Scripts/             # 소스 코드
  Settings/            # URP 렌더 파이프라인 설정
```

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

## 개발 규칙

- Coroutine 사용 금지, Unity `Awaitable` 사용 (UniTask 미도입 — ADR-002 참조)
- 변수명에 언더스코어(`_`) 접두어 사용하지 않음 (camelCase 그대로)
- 한글 주석 허용, UTF-8 인코딩 유지
- 한 파일 500줄 초과 시 분할 작성

## 세션 종료 체크리스트

세션을 완료로 선언하기 전에 반드시 아래 항목을 수행한다:

1. `memory/NEXT_TASKS.md`를 남은 작업 목록으로 갱신
2. `memory/SESSION_HISTORY.md`에 이번 세션에서 수행한 작업 내역 기록
3. 모든 git 커밋/병합이 원격(push)까지 반영되었는지 확인
4. 미커밋 변경사항(`git status`)이 남아 있지 않은지 확인

> `/end-session`, ralph 루프, autopilot/ultrawork 등 지속 실행 모드가 종료될 때에도 위 체크리스트를 동일하게 적용한다.
