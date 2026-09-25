# 로비 NPC 아트 v1

2026-09-25. 안내자, 기록자, 각인사, 제단지기, 유물 상인 5명의 스프라이트, idle, 투명 배경 일러스트.

## 결과

- 게임 에셋: `Assets/Art/NPCs/<id>/`. 96×96 셀, 4프레임, 4fps, 1초 반복. 32 PPU, 발 기준 pivot (48,12), Point 필터.
- 각 폴더의 `idle-sheet.png`, `manifest.json`, `*_idle.anim`, `.controller`, `portrait.png`를 Unity에 연결했다.
- 안내자·기록자·각인사는 기존 대화의 화자 키에 따라 일러스트가 바뀐다. 미등록 화자와 대화 종료 시 그림을 숨긴다.
- 제단지기·유물 상인은 기존 대화가 없어 제단·상점 화면에 표시한다. 각인사 폼 선택 화면에도 그림이 있다.
- 로비 빌더에 적용 단계를 연결해 씬 재생성 시에도 유지한다. NPC 상호작용 루트 위치·배율·충돌체는 보존했다.

## 원본과 검수

내장 image_gen으로 독립 캐릭터를 생성했다. 캐릭터별 `idle-anchor.png` → component-row 생성 → 크로마 제거 및 개체 추출 → 96px atlas 합성 순서다. `cast.json`, 각 `run/`의 요청·프롬프트·원본·프레임·manifest가 제작 기록이다. 고해상도 그림을 축소한 스프라이트라 수작업 픽셀 정리와 동일하다고 보장하지 않는다.

5종 모두 프레임 추출 및 atlas 검사 통과. 프레임별 바닥 위치와 팔다리, 마지막→첫 자세를 contact sheet와 GIF 미리보기에서 확인했다. 로비 렌더의 잔여 마젠타 검사 통과. `review/`는 Unity 에디터에서 프레임과 UI를 샘플링한 정적 렌더이며 실제 플레이 녹화가 아니다.

Unity 배치 적용·컴파일 성공 (`apply.log`). 신규 EditMode 테스트 8건은 작성했으나, 사용 중인 Unity 에디터가 같은 프로젝트를 점유해 배치 테스트가 시작되지 못했다 (`tests.log`). 통과 결과 없음. 에디터 Test Runner에서 `DialoguePortraitTests`, `LobbyNpcArtTests`를 실행할 수 있다. 실제 상호작용과 서비스 패널은 플레이 검수가 남아 있다.

## 미리보기

`index.html`을 이 폴더를 루트로 한 로컬 HTTP 서버에서 열면 5명의 GIF·프레임·일러스트와 적용 화면을 볼 수 있다. 상세 프레임 링크는 이번 세션의 임시 서버 주소다. 다시 열 때 sprite-gen의 `serve_curation.py --run-dir <id>/run --port 0 --no-open --lang ko` 출력 주소로 갱신한다.

## sprite-gen 결과

각 id: guide, chronicler, engraver, altar_keeper, relic_merchant

```text
sprite_gen_done=guide,chronicler,engraver,altar_keeper,relic_merchant
folder=D:/JaeChang/Abyss/Art_Source/npc_cast_v1/<id>/run
engine=component-row
files=sprite-request,raw,frames,atlas,manifest
qa_note=5종 alpha/atlas 검사 통과; idle 미리보기 검수; Unity 자동 테스트는 에디터 점유로 미실행.
```
