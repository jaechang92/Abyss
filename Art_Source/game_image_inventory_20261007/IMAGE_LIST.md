# 게임 전체 이미지 조사·제작 목록

2026-10-07. 사용자 요청: 게임 전반 조사 → 목록 → 한 항목씩 제작. 조사: 타이틀·로비·튜토리얼·Stage1~3·경로·전투/HUD·드래프트/교체·상점/이벤트·메타/유물·대화·도감·설정/저장·결과/엔딩. 코드·데이터·파일 조사이며 Unity 플레이 미실행.

## 재사용과 부족한 부분

| 영역 | 조사 근거 | 처리 |
|---|---|---|
| 타이틀·로비·NPC | Title12층 이미지, lobby_refuge_v2, NPC5종 portrait/idle | 유지·재사용 |
| 폼·적 | Forms4/Enemies14종 데이터 및 기존 전투 애니메이션 | 재사용. 전투 시트 재제작 제외 |
| 보스·도감·HUD | 직전 CodexBossHud 선택20종 | 재사용·연결대기 |
| 드래프트/교체/상점/설정/일시정지 | DraftImages6종, AbyssSkin | 공통 프레임 재사용 |
| 스킬 | 30종 중12종 icon참조,18종 icon:null | 누락18종 최우선 제작 |
| 유물 |6종 모두 icon:null | 실제CSV명칭대로6종 제작 |
| 무기 |4종 icon:null,전투sprite/회전시트 존재 | UI용4종 제작,전투sprite유지 |
| 경로 | RoomTypeDisplay6종 현재문자표시 | 전투/엘리트/휴식/상점4종 제작. 보스왕관/이벤트물음표는 직전이미지재사용 |
| 시너지 | fire/abyss/guard/blood_pact사용;frost/soul예약 | 현재4축제작,미사용2축은현재제작범위제외 |
| 메타 제단 | HP/공격/시작골드/무료리롤4종·스킬해금3 | 새유물/재화/리롤/스킬이미지재사용 |
| 대화/튜토리얼 | 기존NPC초상·동적문구 | 글영역비운가로프레임2종 |
| 저장/정산 | SaveStatusOverlay/ResultPanelPresenter | 저장표식·중립정산장식 |
| Stage1 | 사용자채택대표방·룸별환경작업·기존리프트재질 | 채택방향유지. 씬연결판정별도 |
| Stage2/3 | StageData존재,Assets/Art/Environment전용폴더없음 | 바이블근거배경후보각1. 모든룸/타일/콜라이더완성으로주장하지않음 |
| 프롤로그/엔딩/층전환 | 현재자막·동적연출·실제플레이어sprite | 재사용. 소설사건근거없이컷신임의추가없음 |

## 신규 제작 순서 — 45종

built-in imagegen 개별호출. PNG 텍스트/수치/쿨다운을굽지않는다. 기존파일덮어쓰기없음. 제작상태·런타임연결·사용자검증을별도관리.

| ID | 이미지 | 출력파일 | 필요성 근거 | 제작 상태 |
|---|---|---|---|---|
| IMG-001 | 연소 강화 | skills/burn-enhancement-v1.png | Assets/Resources/Data/Skills/Skill_02_BurnEnhancement.asset | 제작 / 연결·검증대기 |
| IMG-002 | 폭발 신학 | skills/explosive-theology-v1.png | Assets/Resources/Data/Skills/Skill_03_ExplosiveTheology.asset | 제작 / 연결·검증대기 |
| IMG-003 | 불꽃 갑옷 | skills/flame-armor-v1.png | Assets/Resources/Data/Skills/Skill_04_FlameArmor.asset | 제작 / 연결·검증대기 |
| IMG-004 | 잔상 | skills/afterimage-v1.png | Assets/Resources/Data/Skills/Skill_06_Afterimage.asset | 제작 / 연결·검증대기 |
| IMG-005 | 심연 충전 | skills/abyss-charge-v1.png | Assets/Resources/Data/Skills/Skill_07_AbyssCharge.asset | 제작 / 연결·검증대기 |
| IMG-006 | 심연 동료 | skills/abyss-ally-v2.png | Assets/Resources/Data/Skills/Skill_09_AbyssAlly.asset | 제작 / 연결·검증대기 |
| IMG-007 | 영혼 회수 | skills/soul-reclaim-v1.png | Assets/Resources/Data/Skills/Skill_17_SoulReclaim.asset | 제작 / 연결·검증대기 |
| IMG-008 | 반격 태세 | skills/counter-stance-v1.png | Assets/Resources/Data/Skills/Skill_18_CounterStance.asset | 제작 / 연결·검증대기 |
| IMG-009 | 피의 분노 | skills/blood-rage-v1.png | Assets/Resources/Data/Skills/Skill_20_BloodRage.asset | 제작 / 연결·검증대기 |
| IMG-010 | 최후의 일격 | skills/final-stand-v1.png | Assets/Resources/Data/Skills/Skill_21_FinalStand.asset | 제작 / 연결·검증대기 |
| IMG-011 | 흡혈 인장 | skills/vampiric-seal-v1.png | Assets/Resources/Data/Skills/Skill_22_VampiricSeal.asset | 제작 / 연결·검증대기 |
| IMG-012 | 핏빛 광채 | skills/crimson-radiance-v1.png | Assets/Resources/Data/Skills/Skill_23_CrimsonRadiance.asset | 제작 / 연결·검증대기 |
| IMG-013 | 죽음의 약속 | skills/deaths-promise-v1.png | Assets/Resources/Data/Skills/Skill_24_DeathsPromise.asset | 제작 / 연결·검증대기 |
| IMG-014 | 혈영 | skills/blood-shade-v1.png | Assets/Resources/Data/Skills/Skill_25_BloodShade.asset | 제작 / 연결·검증대기 |
| IMG-015 | 신중한 시선 | skills/keen-eye-v1.png | Assets/Resources/Data/Skills/Skill_26_KeenEye.asset | 제작 / 연결·검증대기 |
| IMG-016 | 신성 보호 | skills/holy-ward-v1.png | Assets/Resources/Data/Skills/Skill_27_HolyWard.asset | 제작 / 연결·검증대기 |
| IMG-017 | 황금 손길 | skills/golden-touch-v1.png | Assets/Resources/Data/Skills/Skill_28_GoldenTouch.asset | 제작 / 연결·검증대기 |
| IMG-018 | 운명의 가호 | skills/fates-favor-v1.png | Assets/Resources/Data/Skills/Skill_29_FatesFavor.asset | 제작 / 연결·검증대기 |
| IMG-019 | 돌이 된 심장 | relics/stone-heart-v1.png | Assets/Resources/Data/Relics/StoneHeart.asset | 제작 / 연결·검증대기 |
| IMG-020 | 닳은 숫돌 | relics/whetstone-v1.png | Assets/Resources/Data/Relics/Whetstone.asset | 제작 / 연결·검증대기 |
| IMG-021 | 터진 전대 | relics/coin-pouch-v1.png | Assets/Resources/Data/Relics/CoinPouch.asset | 제작 / 연결·검증대기 |
| IMG-022 | 꺼지지 않은 심지 | relics/ember-core-v1.png | Assets/Resources/Data/Relics/EmberCore.asset | 제작 / 연결·검증대기 |
| IMG-023 | 깊은 광맥의 조각 | relics/deep-vein-v1.png | Assets/Resources/Data/Relics/DeepVein.asset | 제작 / 연결·검증대기 |
| IMG-024 | 먼저 본 자의 눈 | relics/seers-eye-v1.png | Assets/Resources/Data/Relics/SeersEye.asset | 제작 / 연결·검증대기 |
| IMG-025 | 녹슨 검 | weapons/rusted-blade-v1.png | Assets/Resources/Data/Weapons/RustedBlade.asset | 제작 / 연결·검증대기 |
| IMG-026 | 낡은 활 | weapons/worn-bow-v1.png | Assets/Resources/Data/Weapons/WornBow.asset | 제작 / 연결·검증대기 |
| IMG-027 | 찌그러진 방패 | weapons/dented-shield-v1.png | Assets/Resources/Data/Weapons/DentedShield.asset | 제작 / 연결·검증대기 |
| IMG-028 | 이 빠진 단검 | weapons/chipped-dagger-v1.png | Assets/Resources/Data/Weapons/ChippedDagger.asset | 제작 / 연결·검증대기 |
| IMG-029 | 전투 방 표식 | routes/combat-v1.png | Assets/Scripts/Runtime/Stage/RoomTypeDisplay.cs | 제작 / 연결·검증대기 |
| IMG-030 | 엘리트 방 표식 | routes/elite-v1.png | Assets/Scripts/Runtime/Stage/RoomTypeDisplay.cs | 제작 / 연결·검증대기 |
| IMG-031 | 휴식 방 표식 | routes/rest-v1.png | Assets/Scripts/Runtime/Stage/RoomTypeDisplay.cs | 제작 / 연결·검증대기 |
| IMG-032 | 상점 방 표식 | routes/shop-v1.png | Assets/Scripts/Runtime/Stage/RoomTypeDisplay.cs | 제작 / 연결·검증대기 |
| IMG-033 | 골드 파편 | common/gold-shards-v1.png | Assets/Scripts/Runtime/UI/GoldCounterPresenter.cs | 제작 / 연결·검증대기 |
| IMG-034 | 심연 파편 | common/abyss-shards-v1.png | Assets/Scripts/Runtime/Meta/MetaSaveService.cs | 제작 / 연결·검증대기 |
| IMG-035 | 리롤 표식 | common/reroll-v1.png | Assets/Scripts/Runtime/UI/DraftPanelPresenter.cs | 제작 / 연결·검증대기 |
| IMG-036 | 저장 표식 | common/save-record-v1.png | Assets/Scripts/Runtime/UI/SaveStatusOverlay.cs | 제작 / 연결·검증대기 |
| IMG-037 | 불꽃 축 | synergy/fire-v1.png | Assets/Scripts/Runtime/Draft/SynergyAxis.cs | 제작 / 연결·검증대기 |
| IMG-038 | 심연 축 | synergy/abyss-v1.png | Assets/Scripts/Runtime/Draft/SynergyAxis.cs | 제작 / 연결·검증대기 |
| IMG-039 | 수호 축 | synergy/guard-v1.png | Assets/Scripts/Runtime/Draft/SynergyAxis.cs | 제작 / 연결·검증대기 |
| IMG-040 | 피의 서약 축 | synergy/blood-pact-v1.png | Assets/Scripts/Runtime/Draft/SynergyAxis.cs | 제작 / 연결·검증대기 |
| IMG-041 | 대화창 프레임 | ui/dialogue-frame-v1.png | Assets/Scripts/Runtime/Dialogue/DialogueUI.cs | 제작 / 연결·검증대기 |
| IMG-042 | 튜토리얼 안내 프레임 | ui/tutorial-frame-v1.png | Assets/Scripts/Runtime/UI/FirstPlayTutorialPanel.cs | 제작 / 연결·검증대기 |
| IMG-043 | 런 정산 장식 | ui/run-result-emblem-v1.png | Assets/Scripts/Runtime/UI/ResultPanelPresenter.cs | 제작 / 연결·검증대기 |
| IMG-044 | Stage2 배경 후보 | environment/stage2-background-v1.png | Art_Source/10_BIBLE/01-world-identity.md | 제작 / 연결·검증대기 |
| IMG-045 | Stage3 배경 후보 | environment/stage3-background-v2.png | Art_Source/10_BIBLE/01-world-identity.md | 제작 / 연결·검증대기 |

프롬프트·진행은 [tasks.json](tasks.json), 파일/alpha/crop/GUID대조는manifest.json, 전체비교는gallery.html.

## 이미지 제작 뒤 필요한 작업

신규이미지와기존20종의실제게임연결은별도최소코드작업. 미발견숨김/수치/키바인딩/구매·취소/패드계약보존. Stage2/3배경후보는후속레벨디자인/룸크기/전경·타일분리검증필요. 이미지2개가전체맵완성을의미하지않는다. 실기검증·간트기능완료율은이미지생산만으로올리지않는다.

