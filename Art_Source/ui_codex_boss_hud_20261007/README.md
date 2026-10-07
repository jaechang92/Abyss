# 도감·보스 초상·HUD 이미지 — 2026-10-07

사용자 요청: 도감 이미지, 보스 이미지, HUD 전반 이미지 제작. 보스 용도는 사용자 응답에 따라 **도감·등장 화면용 초상**. built-in image_gen으로 PNG 21개를 생성했고 수호자 v2를 포함한 **20종을 선택**했다. 원본 픽셀 편집·축소·재색칠 없음.

- [전체 이미지 보기](gallery.html)
- [전체 프롬프트](prompts.json)
- [파일·GUID·크기·alpha·적용 crop 좌표](manifest.json)
- Unity 준비본: `Assets/Art/UI/CodexBossHud/` — Single Sprite / Full Rect / Point / PPU32 / alpha / mipmap 없음 / Default 무압축 / NPOT 원본 크기.
- **이미지 제작·파일 대조 완료 / 게임 연결 미진행 / Unity 실제 표시 사용자 검증대기.** 기존 런타임 화면은 아직 바뀌지 않는다. Resources 자동 로딩 대상으로 추가하지 않았다.

## 선택 이미지

| 구분 | 파일 | 용도 |
|---|---|---|
| 보스 | bosses/abyss-keeper-portrait-v2.png | boss_abyss_keeper · 심연의 수호자 |
| 보스 | bosses/flame-serpent-portrait-v1.png | boss_flame_serpent · 화염 뱀 |
| 보스 | bosses/thronebound-portrait-v1.png | boss_thronebound · 왕좌의 영혼 |
| 보스 | bosses/sentinel-portrait-v1.png | midboss_sentinel · 감시자 거인 |
| 보스 | bosses/throne-warden-portrait-v1.png | midboss_throne_warden · 왕좌의 파수관 |
| HUD | hud/player-health-frame-v1.png | 플레이어 체력바 |
| HUD | hud/boss-health-frame-v1.png | 보스 체력바 |
| HUD | hud/form-slot-frame-v1.png | 현재·예비 폼 |
| HUD | hud/skill-slot-frame-v1.png | 액티브 스킬 2슬롯 |
| HUD | hud/currency-frame-v1.png | 재화 아이콘·수치 구획 |
| HUD | hud/synergy-frame-v1.png | 시너지 아이콘 액자 |
| 도감 | codex/entry-frame-v1.png | 목록 타일 공통 액자 |
| 도감 | codex/portrait-frame-v1.png | 상세 초상 공통 액자 |
| 도감 | codex/undiscovered-v1.png | 미발견 공통 표시 — 구매 잠금 의미 없음 |
| 도감 | codex/tab-{form,skill,enemy,boss,relic,records}-v1.png | 실제 6분류 아이콘 |

기존 폼·스킬·일반 적·유물의 개별 콘텐츠 이미지는 재사용한다. 이번에는 전체 개별 콘텐츠를 새로 그린 것이 아니라 공통 도감 이미지와 보스 5종을 제작했다.

## 미술·사용 규칙

회청색 철재·상아색 가장자리·절제된 산화 황동을 공통 UI 재질로 사용. 폼에 새 고유 색상을 배정하지 않는다. 보스 4종은 기존 작은 스프라이트의 특징을 확장했고, 감시자는 최신 `20_SUBJECTS/enemies/midboss_sentinel.md`와 실제 이동 시트를 따라 빈 투구·녹슨 쇠·양날·다리 없는 축으로 제작했다. 옛 cyan 코어 임시 이미지와 구분한다.

초상은 전투 스프라이트/애니메이션이 아니다. 원본별 비율이 다르므로 preserveAspect로 배치한다. 수호자 v1은 위쪽 후드가 원본 가장자리에 닿아 보관용으로 남기고 v2를 사용한다. 왕좌의 영혼·감시자의 부드러운 반투명 외곽 효과는 원본에 포함되어 있으므로 밝은 배경에서의 느낌은 Unity에서 확인해야 한다.

프레임 중심 alpha=0 확인. 체력 fill/잔상/페이즈 표시, 숫자·이름·쿨다운·버튼 입력은 기존 UI 레이어로 유지한다. 도감 숨김 항목에 실제 보스 초상을 노출하지 않는다. 분류 아이콘은 작은 크기에서 세부 무늬가 사라질 수 있어 32~40px 실제 표시 확인 필요.

## 연결 시 주의

PNG 전체에는 생성 여백이 있다. `manifest.json`의 `crop_hint_top_left`는 좌상단 원점의 `[x,y,width,height]` 참고값이며 픽셀 파일은 자르지 않았다. Unity rect 원점은 좌하단이므로 `y = textureHeight - topY - height`로 변환한다. 적용 전 여백·반투명 외곽 유지 여부를 화면별로 결정한다.

- 플레이어 체력: 현재 HudArtSkin의 360×44 영역 기준. fill 내부 여백을 프레임에 맞춘다.
- 보스 체력: 생성 dense 영역 비율은 약12:1. 목표20:1에 Simple로 억지로 늘리지 않는다. 양끝을 고정하고 중앙 직선 구간만 9-slice/분리 연결한다. 실제 BossPresenter bar 크기를 연결 시 확인한다.
- 폼: 현재88/64, 스킬80, 재화232×48 기준. 실제 아이콘·수치 영역과 프레임이 겹치지 않도록 한다.
- 도감: 현재 본체1480×860, 타일150×150, 5×3 목록, 탭152×46. 액자는 기존 클릭 영역을 유지하고 이름 라벨을 가리지 않는다. 신규 초상 상세 영역360×480은 제안이며 현재 코드의 적용값으로 단정하지 않는다.
- 현재 CodexPanel에는 보스 초상 전용 연결이 없고 BossPresenter 등장 화면도 텍스트 중심. 초상을 넣을 때 두 화면이 동일 enemyId 매핑을 사용하도록 최소 연결한다. 전투 enemy sprite/애니메이션은 바꾸지 않는다.
- 상태 색·raycast·선택·미발견·페이지/카테고리 전환·다국어·720p/1080p UI 겹침을 보존한다.

## 확인 범위

PNG20종 RGBA/alpha0 존재, 선택본 dense 윤곽 원본 가장자리 접촉 없음, 원본↔Unity 복사본 SHA256 일치, 신규 meta GUID 중복 없음 확인. 이 파일 검사는 Unity 임포트·게임 표시·플레이 검증을 대체하지 않는다. 테스트·빌드·Unity 실행 없음. 코드·씬·기존 콘텐츠 연결·이전 미커밋 변경 보존. 간트 콘텐츠 수/기능 완료율은 변경하지 않았다.
