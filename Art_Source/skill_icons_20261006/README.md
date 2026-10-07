# 신규 액티브 아이콘 2종

2026-10-06 사용자 이어서 진행 요청. 최신 NEXT_TASKS의 Codex 이미지 담당 후속 처리.

- 혈맹의 칼날: 현재 HP10%를 바쳐 전방 베기. 붉은 베기+세 희생 방울.
- 시간 왜곡: 자신의 이동·기본 공격 가속. 달리는 발+잔상+깨진 시계 호.
- built-in image_gen으로 제작. 원본1254×1254 RGBA, 투명도0..255 확인. 원본 PNG 의미 변경 없이 Assets/Art/Sprites/SkillIcons에 복사.
- Single Sprite, Point/no mipmap/no compression/PPU32. Skill_19와Skill_30의 icon 필드만 신규 GUID/fileID21300000으로 연결. 기존 수치·해금·설명 변경 없음.
- ContentBuilder.Icons.cs에 두 파일명/skillId 매핑이 이미 존재함을 확인. 게임 코드 추가 없음. 빌더 재실행 후에도 매핑 가능.
- 큰 원본의 픽셀 면은 시각 검토했으나 실제32/80픽셀 UI 렌더 가독성·임포트 결과는 Unity 사용자 검증대기. 정식32px 제작본과 동일 해상도로 주장하지 않음.
- Unity·빌드·테스트·Git 반영 미실행. 에디터 임포트 후 스킬19/30의 icon 연결과 드래프트/도감/HUD 실제 표시 확인 필요.
- manifest.json에 경로키/크기/GUID 기록. 이번은 이미 구현·검증된 스킬의 누락 이미지 보완이며 콘텐츠 수/기능 완료 판정이 늘지 않아 간트 진척률은 변경하지 않음.

## 프롬프트 요지

혈맹: transparent pixel-art crimson curved slash, pale steel core, three sacrifice droplets; readable32–80px, no text/frame/skull/gore.

시간: transparent pixel-art ivory running boot, two cool slate-blue echoes, broken clock arc; self movement/attack acceleration, no hourglass/text/glow.

간트: 콘텐츠 수/완료 상태·목표·수식은 보존하고 Content Gantt D28의 아이콘 대기 설명만 제작·연결/표시 검증대기로 갱신. Excel 렌더/재계산 미실행.
