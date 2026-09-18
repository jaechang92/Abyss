# ranged_archer — 애니메이션 생성 문구 기록

> 형식 · 문구 규칙은 `Art_Source/characters/knight_red/animation_prompts.md` 를 따른다.
> 기획 · 몸 판정은 `Art_Source/20_SUBJECTS/enemies/ranged_archer.md`. 조립·재생 규약은 `melee_grunt/animation_prompts.md` 끝 절과 같다.
> 🔴 **생성하면 여기에 먼저 적는다** — 문구가 휘발되면 재현이 안 된다.

## 공통

```
character_id   11999797-4f13-4808-8228-294407898e7b   (Ranged Archer R2 · pro · style 2fbf625f-f755-4d1d-a113-adadfdf97ce0)
캔버스          92x92 · south-east 한 방향 + flipX
mode           v3 · 8프레임 2 gen · 4프레임 1 gen
후처리          🔴 recolor 색 B (hue 28° · 채도 ×0.40 · 명도 ×0.95) — R2 몸이 붉은 갑옷이라 플레이어 축 색과 겹친다
                근접 병사와 같은 경로. 번들 시트 전체에 한 번 적용되므로 프레임마다 같은 값을 받는다
정체성 어휘      a kneeling archer holding a large bow vertically high overhead with both arms stretched straight up,
                a pale arrow pointing straight up, two pale arrows stuck in the ground beside the knees
```

🔑 **「하늘」을 쓰지 않는다.** 투명 캔버스에 하늘이 없어 R2 몸에서도 기하 형태어(`vertically`, `straight up`)로
써야 먹었다. 애니메이션 문구도 같은 규칙을 따른다.

## 상태 (FSM Patrol · Chase → 이동 / Attack / Stagger → 피격 / Dead)

| 상태 | frames | animation_name | 문구(정체성 어휘 뒤) | group | 판정 |
|---|---|---|---|---|---|
| 이동 | 9 (1+8) | `move_southeast_v1` | shuffling forward slowly on its knees in small heavy lurches, the body rocking from side to side, both arms staying stretched straight up the whole time so the bow keeps pointing straight up | `fc75c077-de7d-40e9-91d7-12f847bf2e7c` | ❌ v1 — f1 부터 활이 내려온다 |
| 공격 | 9 (1+8) | `attack_southeast_v1` | twisting the bow down from overhead to point forward and loosing the arrow as the bowstring snaps forward, then swinging both arms back up so the bow returns to pointing straight up | `6aa4f682-0dcc-4a92-9a14-bb810e7ee122` | ❌ v1 — f1 부터 활이 내려온다 |
| 피격 | 5 (1+4) | `hit_southeast_v1` | jolted backward with the torso rocking back, both arms staying stretched straight up, then settling low again | `be325c58-0f0d-47ce-b865-17d13eff1510` | ❌ v1 — f1 부터 활이 내려온다 |
| 사망 | 9 (1+8) | `dead_southeast_v1` | the bowstring going slack so the bow slips from its hands and falls, both arms dropping and the body collapsing sideways onto the ground | `5467c946-4c60-4473-bc56-19f38b1e601e` | ❌ v1 — f1 부터 활이 내려온다 |

🔑 **이 적의 공격은 앞 둘과 성격이 다르다** — 근접 둘은 「덮치는 순간」이 피해 시각이었지만,
여기서는 **활을 내려 쏘는 순간**이 발사 시각이고 그 뒤 **위로 되돌아가는 것이 회복**이다.
그래서 문구에 `then swinging both arms back up so the bow returns to pointing straight up` 을 반드시 넣었다 —
되돌아가지 않으면 다음 루프(이동)와 자세가 안 이어진다.

📌 **이동은 brute 의 교훈을 그대로 적용했다** — 「일어서서 걷는」 실패를 `staying low the whole time` 류로 막았다.
여기서는 `both arms staying stretched straight up the whole time` 으로 **활이 내려오는 것**을 막는다.

## v1 판정 (2026-09-18) — 🔴 네 상태 모두 f1 부터 활이 내려온다

미리보기: `_anim_v1_preview.png` (2배) · `_anim_v1_preview_small.png` · 초록선 = footY 92

**f0(참조 = R2 몸)만 활이 세로로 서 있고, f1 부터 전부 활이 앞으로 기울어진다.**
R2 에서 25 gen 을 들여 얻은 「위를 겨눔」이 애니메이션에서 통째로 사라졌다.

| 상태 | 결과 |
|---|---|
| 이동 | ❌ f1~f8 활이 앞으로 기운 채 흔들린다 — 「위 겨눔」 없음 |
| 공격 | ⚠️ f0 위 → f1~2 내려옴 → **f3~8 앞으로 겨눈 채 유지**. 내린 것까지는 맞는데 **위로 안 돌아온다**(회복 구간 없음) |
| 피격 | ❌ f1~f4 활이 기울어짐 |
| 사망 | ⚠️ f5~f8 몸이 무너지고 활이 떨어진다(동작 자체는 좋다) · 다만 f1 부터 이미 활이 내려와 있다 |

🔑 **`both arms staying stretched straight up the whole time` 이 안 먹었다.**
brute 의 「일어서서 걷는다」와 같은 실패이고, 원인도 같다 — **v3 는 부자연스러운 자세를 유지하지 못하고
「자연스러운 동작」으로 회귀한다.** 팔을 머리 위로 든 자세는 v3 에게 「사격 자세로 돌아가라」는 신호였다.

📌 색 변환(B 바랜 갈색)은 **모든 프레임에 정확히 먹었다** — 이쪽은 문제없다.
📌 정렬: raw 이동량 -12~-13 은 사수가 앞 둘보다 캔버스에서 11px 낮게 앉아 생기는 **일관된** 값이라 무해.
   🔴 다만 **번들 칸이 120x120 이다**(근접 병사·중장 강적은 124) — 레시피 `cell 124` 는 그대로 맞다.
   PIL 이 음수 crop 을 투명 패딩으로 처리해 120 내용이 124 칸 가운데에 놓이고 잘림은 0 이다.

## v2 — 보간 방식 (2026-09-18 · 7 gen · 누적 64)

🔑 **`end_frame_url` 에 R2 몸(SE rotation)을 주면 시작과 끝이 같은 자세가 되어 중간 이탈이 줄어든다.**
플레이어 상승·낙하 루프 3단계에서 확인된 방법이다(메모리 `project_pixellab_character_weapon`).
`custom_start_frame` 은 생략한다 — 비우면 그 방향의 rotation 이 그대로 시작 프레임이다.

| 상태 | frames | animation_name | 문구 | group | 보간 | 판정 |
|---|---|---|---|---|---|---|
| 이동 | 9 | `move_southeast_v2` | shuffling forward slowly on its knees in small heavy lurches, the body rocking from side to side, both arms **locked** stretched straight up the whole time so the bow stays pointing straight up | `39f8abe1-bc29-4b1e-aa53-dc60b3f8302b` | end=R2 | ✅ **활이 세로로 선다** · f8 이 f0 로 돌아와 루프가 안 튄다 · ⚠️ f2·f3·f7 에서 화살이 흐려짐 |
| 공격 | 9 | `attack_southeast_v2` | twisting the bow down from overhead to point forward and loosing the arrow, then swinging both arms straight back up so the bow **ends** pointing straight up again | `b63dad41-1b06-4157-9c9c-ef45c5d8232d` | end=R2 | ✅ **정확히 요구한 동작** — f0 위 → f3~f4 앞으로 겨눔(발사) → f5~f8 되돌아감 |
| 피격 | 5 | `hit_southeast_v2` | jolted backward with the torso rocking back, both arms **locked** stretched straight up so the bow stays pointing straight up, then settling low again | `bac99b1f-cb7d-49ac-921e-3ccd1b228a34` | end=R2 | ✅ 활이 세로 유지 · f2 에서 몸이 낮아지며 움찔 |
| 사망 | 9 | `dead_southeast_v2` | keeping both arms locked stretched straight up with the bow pointing straight up **through the first half**, then the bowstring going slack so the bow slips from its hands and falls, the arms dropping and the body collapsing sideways onto the ground | `8b9f3561-74b2-4042-b2b7-fa77a797e8b8` | 없음 | ✅ f0~f4 활이 서 있고 f5~f7 내려오며 f8 에 **활을 놓고 웅크려 쓰러진다** |

📌 **사망만 보간을 안 썼다** — 끝 자세가 「무너진 몸」이라 시작과 같을 수 없다.
대신 `through the first half` 로 전반부를 붙잡았고, 그것으로 충분했다.

⚠️ **땅에 박힌 화살이 프레임마다 깜빡인다**(이동 f2·f3·f7 · 피격 f1·f3·f4). 짧은 상태라 눈에 띌지는 Unity 에서 본다.

### 정렬

```
move   -12 -12 -12 -13 -13 -13 -12 -12 -12     전부 일관 → align each
attack -12 x9        hit -12 x5        dead -12 x9
```

🔴 **번들 칸이 두 번 바뀌었다**: 120x120(몸만) → **120x124**(애니메이션 추가 후).
레시피 `cell 124` 는 그대로 맞다 — PIL 이 음수 crop 을 투명 패딩으로 처리해 잘림이 0 이다.
메모리 「칸 크기를 JSON 에서 읽을 것」이 여기서도 유효하다.

## v3 — 땅에 박힌 화살 제거 (2026-09-18 · 7 gen · 누적 71)

🔴 **사용자 지적(Unity 검증): 주변에 박힌 화살이 깜빡인다 → 없앨 것.**

### 🔑 후처리로는 못 지웠다 — 시작 프레임에서 지우고 다시 뽑았다

시트 32장에서 화살만 지우려고 세 가지를 시도했고 **전부 실패**했다.

| 시도 | 왜 실패했나 |
|---|---|
| 색(창백한 회백)으로 식별 | `recolor` 가 **몸 채도를 ×0.4 로 낮춰** 흙빛 몸과 회백색 화살이 안 갈린다 |
| recolor **전** 원본에서 식별 | 몸의 **회색 갑옷**도 저채도라 무릎·허리를 같이 먹는다 |
| 1px 침식 후 연결 요소 분리 | 프레임마다 결과가 달랐다(f2·f3·f5 는 0px) — **오히려 깜빡임이 심해진다** |

🔑 **v3 는 매 프레임 새로 그리므로 화살이 몸에 붙고 위치·모양·두께가 변한다.** 32장에 일관된 규칙이 없다.
반면 **시작 프레임 한 장은 손으로 정확히 지울 수 있다.** 그래서 `_r2_southeast_noarrows.png`
(사각 영역 `(12~27, 60~92)` · `(70~86, 63~92)` 로 화살 두 대만 제거 · 몸·활 온전 · 남은 덩어리 1개)를 만들고
그것을 `custom_start_frame_base64` 로 넣어 4종을 다시 뽑았다.

📌 base64 는 **무손실 팔레트 PNG**(36색)로 5145 → 2585바이트, b64 3448자. RGBA 그대로면 7500자라 잘림 위험이 있다.
📌 🔴 **문구가 v2 와 같으면 재큐잉되지 않는다**(`already queued or complete`). 문구를 바꿔야 한다.

| 상태 | frames | animation_name | 문구 | group | 보간 |
|---|---|---|---|---|---|
| 이동 | 9 | `move_southeast_v3` | dragging itself forward on its knees in small heavy lurches with the body rocking from side to side, both arms locked stretched straight up the whole time so the bow stays pointing straight up, **bare ground with no arrows in it** | `34eeaf6a-bc51-4644-bf4f-92da1980f658` | end=noarrows |
| 공격 | 9 | `attack_southeast_v3` | swinging the bow down from overhead to aim forward and loosing the arrow, then sweeping both arms straight back up so the bow ends pointing straight up again, bare ground with no arrows in it | `9e47217b-82c5-40e3-87fc-515b4d307adf` | end=noarrows |
| 피격 | 5 | `hit_southeast_v3` | rocked backward by a blow with the torso tipping back, both arms locked stretched straight up, then settling low again, bare ground with no arrows in it | `4ea92d76-7602-404c-aeac-aa7fa5081bf0` | end=noarrows |
| 사망 | 9 | `dead_southeast_v3` | holding both arms locked stretched straight up at first, then the bowstring going slack so the bow slips from its hands and drops, the arms falling and the body crumpling sideways onto the ground, bare ground with no arrows in it | `46d5c198-03f9-4336-9b88-864a71144892` | 없음 |

### 결과 (`_anim_v3_preview.png`)

- ✅ **네 상태 모두 땅의 화살이 사라졌다.** 프레임별 가로 범위가 활 움직임을 따라 매끄럽게 변할 뿐 튀지 않는다
- ✅ 공격 구조는 v2 와 같다 — f0 위 → **f4~f5 발사** → f7~f8 되돌아감. **동작 시간 0.5 + 0.6 그대로 유효**
- ✅ 정렬이 더 안정적이다: 발밑 **104 전부 일관**(v2 는 move 세 장이 105)
- 🔴 번들 칸이 **136x124** 로 또 바뀌었다(120x120 → 120x124 → 136x124). `cell 124` 는 여전히 맞다(잘림 0)

## 생성 기록

```
몸   R1 51e3f6b8 ❌(앞을 겨눔) · R2 11999797 ✅ 채택        = 50 gen
애니 v1 4종 ❌(f1 부터 활이 내려옴)                          =  7 gen
     v2 4종 ✅ 자세는 합격 · ❌ 땅 화살 깜빡임(사용자)        =  7 gen
     v3 4종 ✅ 채택 — 화살 지운 시작 프레임                   =  7 gen
                                                       누적  71 gen
```
