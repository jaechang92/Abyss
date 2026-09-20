# void_caster — 애니메이션 생성 문구 기록

> 형식 · 문구 규칙은 `Art_Source/characters/knight_red/animation_prompts.md` 를 따른다.
> 기획 · 몸 판정은 `Art_Source/20_SUBJECTS/enemies/void_caster.md`. 조립·재생 규약은 `melee_grunt/animation_prompts.md` 끝 절과 같다.
> 🔴 **생성하면 여기에 먼저 적는다** — 문구가 휘발되면 재현이 안 된다.

## 공통

```
character_id   b29e29c2-ae42-42ee-98f9-862e1d69d338   (Void Caster R1 · pro · style 2fbf625f-f755-4d1d-a113-adadfdf97ce0)
캔버스          92x92 · south-east 한 방향 + flipX
mode           v3 · 8프레임 2~3 gen · 4프레임 1~2 gen
후처리          ✅ **desaturate 를 쓴다** — R1 은 따뜻한 회갈/살구색이라 얼굴이 「빈 면」이 아니라 「지운 얼굴」로 읽힌다.
                색 후보 C(채도 ×0.25 + hue 195°)를 채택했다(`_r1_color_test.png` · 사용자 결정).
                🔴 기존 `recolor`(붉음~주황 색상만 고름)로는 안 되고 **채도 스케일 + hue 고정**이 필요하다 — 도구 변경
정체성 어휘      a figure whose whole face is one flat blank panel with no eyes and no features, nothing covering its
                bare head, no feet and no legs beneath the ragged hem of its robe, one arm stretched forward pointing
```

🔑 **「얼굴이 빈 면」과 「발이 없다」를 매 문구 앞에 다시 쓴다.** 둘 다 R1 에서 한 번에 나온 성과인데
**v3 는 부자연스러운 자세를 못 버티고 「자연스러운 동작」으로 회귀한다**(세션 9 교훈 2 · 중장 강적의 「일어서서 걷는다」).
이 적에게 그 회귀는 **걷는 다리가 생기는 것**과 **얼굴에 눈이 생기는 것** 둘이다.

🔑 **`end_frame` 으로 시작 프레임을 되돌린다**(사망 제외). 원거리 사수 v1 이 f1 부터 자세가 풀린 것을
v2 에서 이 방법으로 잡았다 — 시작과 끝을 같게 주면 구조적으로 붙잡힌다.

🔴 **「선」은 「이미 그어졌다」로 쓴다.** 뼈 궁수 교훈 10 과 같은 자리다 — v3 에게 「발사」를 요구하면
흰 덩어리를 그린다. `a thin bright stroke already drawn in the air` 로 **완료된 형태**를 못박는다.
그리고 **획을 작게** 요구한다 — 크면 발사체 프리팹과 겹쳐 선이 둘이 된다.

🔴 **공격에서만 「제 얼굴을 짚은 손」을 되살려 본다.** R1 몸에서 그 손짓이 안 나왔는데(기획서 문구별 판정),
**몸을 다시 뽑지 않고 동작으로 고치는 것**이 세션 10 의 ⓚ 방식이다(뼈 궁수는 활 각도를 공격 v3 에서만 세워 4 gen 에 해결).
이동·피격·사망에는 필요 없으므로 **공격 한 상태에만 건다** — 실패해도 손실이 그 상태뿐이다.

## 상태 (FSM Patrol · Chase → 이동 / Attack / Stagger → 피격 / Dead)

| 상태 | frames | animation_name | 문구(정체성 어휘 뒤) | group | 보간 | 판정 |
|---|---|---|---|---|---|---|
| 이동 | 9 (1+8) | `move_southeast_v1` | gliding forward without walking, the ragged hem swaying slowly under it, the whole body drifting a little up and down, both arms staying exactly where they are the whole time | `1072d437-9439-4f0c-82a5-b2269e404982` | end=R1 | ✅ **채택** |
| 공격 | 9 (1+8) | `attack_southeast_v1` | the pointing hand flicking forward three times in quick succession, a thin bright stroke already drawn in the air just past the fingertips each time, the other hand lifting to rest against the lower edge of the blank face panel and staying there, then settling back into the same pointing stance | `653ff240-669d-4566-bee8-e200d9cf6ad1` | end=R1 | ❌ **불합격** → v2 |
| 피격 | 5 (1+4) | `hit_southeast_v1` | a single short line appearing across the blank face panel and fading away again, the body rocking back only slightly, the hem swaying once, then settling back into the same stance | `01062364-0ac9-4b06-b4a5-9be36089b274` | end=R1 | ✅ **채택** |
| 사망 | 9 (1+8) | `dead_southeast_v1` | the blank face panel going dim first, then the robe unravelling downward from the shoulders and sinking, the pointing arm dropping last, until nothing is left standing | `3ccacc78-98f6-4531-8a15-caee2212f8c3` | 없음 | ✅ **채택** |

## v1 판정 (2026-09-20) — 셋 채택 · **공격만 재생성**

미리보기: `_anim_v1_desat.png`(색 C 적용 · 위부터 이동 · 공격 · 피격 · 사망) · `_anim_v1_raw.png`(변환 전) ·
확대 `_attack_v1_zoom.png`(f0·1·4·5·6) · `_hit_v1_zoom.png`

| 상태 | 결과 |
|---|---|
| 이동 | ✅ **채택.** 🔑 **다리가 안 생겼다** — 밑단이 흔들리며 미끄러진다. v3 의 「걷는 사람 회귀」를 정체성 어휘가 막았다. 얼굴판도 9프레임 내내 비어 있다 |
| 공격 | ❌ **불합격 — 셋 다 틀렸다**(아래) |
| 피격 | ✅ **채택.** 🔑 **얼굴판에 가로선 하나가 f1~f3 에 생겼다 f4 에 사라진다** — ①「이름 하나가 선 하나」가 그대로 연출됐다. ⚠️ 선이 얼굴 밖 허공까지 뻗고, f1 왼쪽에 2px 조각이 떠 있다. ⚠️ 몸이 거의 안 움직여 피격감은 플레이에서 볼 것 |
| 사망 | ✅ **채택.** 위에서부터 무너져 얼굴판이 f5 부터 사라지고 옷만 남는다 — ④ 「위에서부터 지워진다」 그대로. 다리도 안 생겼다 |

### 🔴 공격 v1 이 틀린 셋 — 전부 「획」을 그리게 한 대가다

```
f1  긴 흰 선이 몸 뒤쪽(왼쪽)으로 뻗는다      <- 가리키는 방향의 반대다
f4  손 앞에 흰 덩어리                        <- 「획」이 아니라 뭉쳤다
f5  얼굴판을 가로지르는 가로선 + 덩어리      <- 🔴 「선이 하나도 없다」 정면 위반
f6  또 뒤쪽으로 긴 선
```

1. 🔴 **방향이 반대다.** `just past the fingertips` 로 위치를 묶으려 했는데 안 먹었고, v3 는 **몸 주위를 지나는 긴 획**으로 읽었다
2. 🔴 **캔버스가 132x92 로 커졌다**(다른 셋은 92x92). 선이 캔버스를 키운 것이고, 그대로 두면 **발사체 프리팹과 선이 둘**이 된다 — 기획서 ④ 가 미리 적어 둔 바로 그 위험이 났다
3. 🔴 **얼굴판에 선이 그어졌다.** 정체가 「선이 하나도 없다」인데 공격할 때마다 제 얼굴에 선이 생긴다 — **피격이 맡기로 한 연출을 공격이 가져갔다**

🔑 **뼈 궁수 교훈 10 과 같은 자리인데 방향이 반대다.** 거기서는 「발사」를 요구하니 흰 덩어리를 그려서
**`the now-empty bowstring` 으로 「이미 떠났다=없다」를 못박아** 풀었다. 여기서는 「이미 그어졌다」라고 썼는데도
**그린다** — `already drawn` 이 「없다」가 아니라 **「그려진 상태」**로 읽힌 것이다.

📌 **그래서 v2 는 획을 아예 요구하지 않는다.** 선은 **발사체 프리팹이 그리게 두고**(기획서 ⑤-ⓗ),
애니메이션은 **손동작만** 맡는다. 허공이 비어 있다는 것을 **긍정 형태**로 쓴다(`the air around it left completely empty`).

🔴 **「제 얼굴을 짚은 손」도 v1 에서 안 나왔다**(왼팔이 가슴 앞 그대로). 몸 R1 에 이어 **두 번째 실패**다 —
v2 에서 문구를 강하게 바꿔 한 번 더 시도하고, 그래도 안 되면 **기획서 ③·④ 를 몸에 맞게 고친다**(표지 셋 → 둘).

## 공격 v2 판정 (2026-09-20) — ✅ **채택** (`_anim_attack_v2.png` · 2 gen · group `380d3352-e847-4f87-9f4e-82369af32872`)

```
v2 문구 (획을 요구하지 않는다 · 허공이 비어 있음을 긍정 형태로)
…the left hand raised up to press its fingertips against the lower edge of its own blank face panel and held there
through every frame, the right arm snapping forward and back three times in quick succession, the fingers flicking out
sharply at the end of each thrust, the space in front of the hand staying clear and the blank face panel staying
perfectly smooth the whole time, then settling back into the same pointing stance
```

| | v1 | v2 |
|---|---|---|
| 🔴 허공의 긴 선 | ❌ f1·f6 에 몸 **뒤쪽**으로 긴 흰 선 | ✅ **없다.** 획을 요구하지 않으니 안 그린다 |
| 🔴 칸 크기 | ❌ **132x92**(선이 칸을 키웠다) | ✅ **92x92** — 다른 셋과 같다 |
| 🔴 얼굴판에 선 | ❌ f5 에 가로선 | ✅ **9프레임 내내 깨끗하다** |
| 팔 동작 | 덩어리에 가려 안 보임 | ✅ f1~2 접었다 f3~6 내지르고 f7~8 돌아온다 |
| ⚠️ 「세 번」 | — | ⚠️ **큰 동작 한 번**으로 읽힌다. 8프레임에 3회는 회당 2.7프레임이라 잘게 안 나온다 |
| 🔴 제 얼굴을 짚은 손 | ❌ | ❌ **세 번째 실패** — 아래 |

🔑 **「이미 그어졌다」가 아니라 「그리지 않는다」가 답이었다.** 뼈 궁수 교훈 10 은 「발사」→흰 덩어리를
`the now-empty bowstring`(=없다)으로 풀었는데, 여기서는 `already drawn`(=그려진 상태)이 **여전히 그리라는 말**이었다.
**v3 에게 「무엇이 있다」를 쓰면 그린다 — 없어야 하는 것은 「무엇이 비어 있다」로 써야 한다**(`the space … staying clear`).
📌 이 적의 선은 **발사체 프리팹이 그린다**(기획서 ⑤-ⓗ) — 애니메이션은 손동작만 맡는 것이 처음부터 맞았다.

### 🔴 「제 얼굴을 짚은 손」은 **포기한다** — 세 번 실패했다

몸 R1(`fingertips touching the lower edge … without covering it`) · 공격 v1(`the other hand lifting to rest …`) ·
공격 v2(`the left hand raised up to press its fingertips … held there through every frame`) — **점점 강하게 썼는데 셋 다 안 나왔다.**
왼팔은 매번 **가슴 앞에 접힌 자세**로 돌아간다. style 캐릭터(붉은 기사)의 기본 팔 자세이고,
**한 팔이 이미 앞으로 뻗어 있으면 나머지 한 팔은 몸에 붙인다**가 이 모델의 강한 습관으로 보인다.

📌 **기획서 ③ 정체 표지를 셋에서 둘로 줄인다** — ①빈 얼굴판 ②발이 없다. 원문의 손짓은 ④ 공격의
**가리키는 팔**이 혼자 맡는다(그쪽은 세 번 다 또렷하게 나왔다). 🔴 더 시도하지 않는다 — 몸을 다시 뽑아도 같은 습관이 온다.
