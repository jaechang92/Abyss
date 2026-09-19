# bone_archer — 애니메이션 생성 문구 기록

> 형식 · 문구 규칙은 `Art_Source/characters/knight_red/animation_prompts.md` 를 따른다.
> 기획 · 몸 판정은 `Art_Source/20_SUBJECTS/enemies/bone_archer.md`. 조립·재생 규약은 `melee_grunt/animation_prompts.md` 끝 절과 같다.
> 🔴 **생성하면 여기에 먼저 적는다** — 문구가 휘발되면 재현이 안 된다.

## 공통

```
character_id   1780e4c8-3e0a-4dc9-a6ef-ef430f3fb27c   (Bone Archer R2 · pro · style 2fbf625f-f755-4d1d-a113-adadfdf97ce0)
캔버스          92x92 · south-east 한 방향 + flipX
mode           v3 · 8프레임 2 gen · 4프레임 1 gen
후처리          ❌ 없음 — R2 는 명도 ≥99A6AC 6.2% 로 캐릭터 대역에 이미 들어가 있다.
                🔴 어둡게 내리면 한 단 만에 0.3% 로 무너진다(`_r2_color_test.png`) — 색 변환을 **하지 않는 것**이 규약이다
정체성 어휘      a headless ash figure, its neck ending in a low broken stump, the lower body merged into a mound of
                loose ash on the ground, both thin arms stretched forward drawing a small bow
```

🔑 **「머리가 없다」를 매 문구 앞에 다시 쓴다.** R1 이 후드로 머리를 채운 것이 이 적의 유일한 큰 실패였고,
그것을 이긴 것은 **공통 화풍 문구에서 `an oversized head` 를 뺀 것 + 결손을 주어 자리에 둔 것** 둘이었다.
애니메이션은 화풍 문구를 안 쓰므로 **결손을 주어에 두는 쪽만 남는다** — 그래서 매번 맨 앞에 쓴다.

🔑 **「재 더미에 잠긴 하반신」도 매번 쓴다.** 중장 강적의 「일어서서 걷는다」 실패와 같은 위험이 있다 —
v3 는 부자연스러운 자세를 못 버티고 **서 있는 사람으로 회귀한다.** 이 적은 다리가 없는 것이 정체다.

## 상태 (FSM Patrol · Chase → 이동 / Attack / Stagger → 피격 / Dead)

| 상태 | frames | animation_name | 문구(정체성 어휘 뒤) | group | 보간 | 판정 |
|---|---|---|---|---|---|---|
| 이동 | 9 (1+8) | `move_southeast_v1` | sliding forward over the ground in small heavy lurches as the mound of ash drags along under it, the torso staying low and pitched forward the whole time, both arms keeping the bow drawn the whole time | `9bc023eb-f07a-4a45-a718-7e6827342176` | end=R2 | ✅ **채택** |
| 공격 | 9 (1+8) | `attack_southeast_v1` | pulling the bowstring back and then loosing the arrow as the string snaps forward, flakes of ash breaking off its shoulders at that moment, the torso sinking lower, then settling back into the same low drawn stance | `325b4856-5dcb-4afd-bd05-6ff94dc6530e` | end=R2 | ✅ **채택** |
| 피격 | 5 (1+4) | `hit_southeast_v1` | jolted backward with the torso rocking back, a burst of ash flakes breaking off where it was struck, the lower body staying merged with the mound the whole time, then settling low again | `107de027-1367-4aae-9710-a05aea025148` | end=R2 | ❌ **f1 에 흰 폭발** |
| 사망 | 9 (1+8) | `dead_southeast_v1` | keeping the bow drawn through the first half, then the bowstring going slack so the bow slips from its hands and falls, the body crumbling downward from the stump of the neck and sinking into the mound of loose ash until it lies flat on the ground | `539bee6e-dd63-45aa-8afb-2a359cb94da6` | 없음 | ✅ **채택** |

## v1 판정 (2026-09-19) — 셋 채택 · 피격만 재생성

미리보기: `_anim_v1_preview.png` (2배 · 위부터 이동 · 공격 · 피격 · 사망)

| 상태 | 결과 |
|---|---|
| 이동 | ✅ **몸이 낮게 유지된다** — 중장 강적의 「일어서서 걷는다」 실패를 피했다. 재 더미가 바닥에 붙어 있고 f8 이 f0 로 돌아와 루프가 안 튄다. ⚠️ 전진감은 약하지만 실제 이동은 Transform 이 한다 |
| 공격 | ✅ **f4~f7 에 재 조각이 어깨 위로 튄다** — 「쏠 때마다 부스러진다」가 그림에 들어갔다. f1~2 당김 → f3~5 발사 → f6~8 되돌아감 |
| 피격 | ❌ **f1 에 흰 폭발 같은 큰 덩어리.** 밝은 픽셀 134 → **275 로 두 배**다. 5프레임짜리라 1/5 을 차지해 확 튄다 |
| 사망 | ✅ **f4~f6 몸이 무너지고 f7~f8 에 재 더미만 남는다** — 요구한 그대로. 보간을 안 쓴 것이 맞았다 |

### 정렬 · 칸 (실측)

```
        아래끝 y                              번들 칸
move    84 x9        전부 일관 → align each    116x100
attack  86 x9        전부 일관 → align each    124x104
hit     84 x5        전부 일관 → align each    112x100
dead    90 x9        전부 일관 → align first   112x112
```

🔴 **번들 칸이 상태마다 다르다** — 이 적은 네 상태가 116x100 · 124x104 · 112x100 · 112x112 로 **전부 다르다.**
사수는 회차마다 달랐는데(120x120 → 120x124 → 136x124) 여기서는 **한 회차 안에서 갈린다.**
레시피 `cell 124` 는 그대로 맞다 — PIL 이 음수 crop 을 투명 패딩으로 처리한다.
📌 다만 `attack` 이 124x104 라 **가로가 칸 크기와 같다** — 여유가 0 이므로 조립 뒤 잘림을 확인할 것.

### 🔴 공격 v2 — 활이 아래를 겨눈다 (2026-09-19 · 사용자 Unity 지적 · 2 gen)

> *"활의 방향이 정면이 아니고 아래쪽으로 발사하는데 projectile 은 플레이어 쪽으로 발사되고 있어"*

**R2 몸의 활이 약 45° 아래로 기울어 있다**(`_bow_zoom.png`). 시위도 같은 각이라 화살은 앞아래로 나가는데,
발사체는 플레이어를 향해 수평으로 날아간다 — **그림과 판정이 어긋난다.**

🔑 **원인은 내가 쓴 R2 문구다.** 도형을 「앞으로 무너지는 대각선」으로 못박으려고
`the torso pitched steeply forward and downward with the shoulders lower than the hips` 를 넣었는데,
**상체를 내리자 조준까지 같이 내려갔다.** 자세만 기울이고 조준은 수평으로 두라는 말을 안 한 것이다.

📌 **다음 적에 그대로 온다 — 자세를 기울이는 문구는 「무엇이 안 기우는가」를 같이 써야 한다.**

### 🔑 그런데 몸을 다시 뽑을 필요는 없었다

**이동 · 피격 · 사망에서는 기운 활이 결함이 아니다** — 활을 내린 채 끌려오는 것이 오히려
「다 타버린 것」이라는 정체와 맞는다. **어긋나는 것은 쏘는 순간 하나뿐**이므로
**공격 애니메이션에서만 활을 수평으로 들어올렸다 내리면** 된다.

원거리 사수가 「겨눈 활을 아래로 비틀어 내렸다가 쏘고 되돌아간다」로 푼 것과 **같은 구조**이고,
비용은 **몸 재생성 25~40 gen 대신 2 gen** 이다. R2 가 어렵게 얻은 머리 결손·명도를 다시 추첨할 이유도 없다.

| 상태 | frames | animation_name | 문구(정체성 어휘 뒤) | group | 보간 | 판정 |
|---|---|---|---|---|---|---|
| 공격 | 9 (1+8) | `attack_southeast_v2` | **raising the bow up until its limbs point straight up and straight down and the nocked arrow points straight forward and level**, the bowstring pulled straight back and then released so the string snaps forward **while the bow stays level**, flakes of ash breaking off its shoulders at that moment, then **lowering the bow back down to the same low slanted resting angle it started from** | `06e0d268-8212-4f4b-960c-20730a49f697` | end=R2 | ⚠️ **활은 섰는데 f5 에 흰 화살 덩어리** |
| 공격 | 9 (1+8) | `attack_southeast_v3` | raising the bow up until its limbs point straight up and straight down and **the bow stands upright and level**, pulling **the empty bowstring** straight back and letting **the now-empty bowstring** snap forward, **small** flakes of ash breaking off its shoulders at that moment, then lowering the bow back down to the same low slanted resting angle it started from | `0cba0c68-4c6c-406f-9587-d0d5ee82a2c6` | end=R2 | ✅ **채택** |

### v2 · v3 판정 (`_anim_attack_v1_v2.png` · `_anim_attack_v2_v3.png` · `_attack_f5_zoom.png`)

✅ **v2 에서 활 방향이 해결됐다** — f3~f6 에 활대가 위아래를 향해 **활이 수직으로 선다.**
측면에서 활이 수직이면 **화살은 수평**이고, 그것이 플레이어를 향해 날아가는 발사체와 맞는다.
활 우끝 x 도 90 → 95 로 앞으로 더 뻗어 「들어올린다」가 동작으로 들어갔다.

❌ **그런데 v2 f5 에 몸 절반을 덮는 흰 화살이 그려졌다.**

```
밝은px(L>=200)   f0  f1  f2  f3  f4   f5  f6  f7  f8
v2                6   7   0   8   7  364  68   1   1     <- f5 만 50배
v3                6  12   1   2   9    7   1   5   0     <- 고르다
```

🔑 **이건 취향이 아니라 이중 화살이다.** 실제 화살은 `projectilePrefab` 이 별도로 날아가므로
그림에 큰 화살을 또 그리면 **화살이 둘**이 된다. 피격 v1 의 흰 폭발과 **같은 실패**이고 원인도 같다 —
**v3 에게 발사를 요구하면 흰 덩어리로 그린다.**

✅ **`the empty bowstring` · `the now-empty bowstring` 으로 「화살이 이미 떠났다」를 못박아 해결**(2 gen).
v3 는 흰 덩어리 없이 **활에 걸린 얇은 화살**만 그렸고(`_attack_f5_zoom.png`), 활은 더 크게 뻗는다(우끝 96 → 105).

📌 **발사 프레임은 f4 → f5 로 밀렸다.** v3 는 f3~f5 가 수직 구간이고 f5 에서 시위가 풀린다.

```
attackWindup + attackRecovery = 1.35 (변화 없음 · 쿨다운 2.4 안)
5/9 x 1.35 = 0.75 = windup      ->  windup 0.75 + recovery 0.6   (v1 의 0.6 + 0.75 에서 뒤집힘)
```

📌 **`projectileOrigin` 도 다시 쟀다**: 공격 f5 화살촉 = 칸 좌표 **(104, 54)**.
피벗 (62, 92) · PPU 32 · 콜라이더 높이 1.9 로 환산 → **(1.31, 0.24)** → `(1.3, 0.24)`.
🔴 **내린 자세에서 잰 옛 값 (0.95, 0.15) 은 「화살이 안 나가는 자세」의 값이었다** —
**발사 위치는 쏘는 프레임에서 재야 한다.**

📌 **기하 형태어를 그대로 썼다** — `limbs point straight up and straight down` 은 원거리 사수 R2 가
「위를 겨눔」을 얻은 어휘다. 측면에서 **활대가 위아래를 향하면 화살은 수평**이 된다.
📌 정체성 어휘의 `drawing a small bow` 를 **`holding a small bow`** 로 바꿨다 — 「당긴 채」가 시작부터 고정되면
당겼다 놓는 동작이 안 생긴다(v1 의 ⓙ).
🔴 **발사 프레임이 바뀌므로 `attackWindup` 과 `projectileOrigin` 을 다시 잰다.**

### 피격 v2 (2026-09-19 · 1 gen)

| 상태 | frames | animation_name | 문구(정체성 어휘 뒤) | group | 보간 | 판정 |
|---|---|---|---|---|---|---|
| 피격 | 5 (1+4) | `hit_southeast_v2` | jolted backward with the torso rocking back **and the shoulder caving inward where it was struck**, the lower body staying merged with the mound the whole time, then settling low again into the same drawn stance | `3930896b-0a38-4a84-8621-a1d5ebb525fd` | end=R2 | ⬜ 판정 전 |

🔑 **`a burst of ash flakes breaking off` 를 뺐다.** 「흩어지는 재」를 문구로 요구하면 v3 가
**흰 덩어리**로 그린다 — 공격에서는 작은 조각으로 잘 나왔지만 피격은 5프레임뿐이라 한 프레임이 크게 온다.
대신 **`the shoulder caving inward`** 로 **몸 안쪽의 변형**을 요구했다. 기획서 ④ 의 「맞은 자리가 파인다」에 오히려 더 가깝다.

🔑 **보간은 원거리 사수 v2 가 확립한 방식 그대로다** — `end_frame_url` 에 R2 몸(SE rotation)을 주면
시작과 끝이 같은 자세가 되어 중간 이탈이 줄고 루프가 안 튄다. `custom_start_frame` 은 생략한다
(비우면 그 방향의 rotation 이 그대로 시작 프레임이다).

📌 **사망만 보간을 안 쓴다** — 끝 자세가 「재 더미에 잠긴 몸」이라 시작과 같을 수 없다.
대신 `through the first half` 로 전반부를 붙잡았다(사수 v2 와 같은 처방).

### 🔴 기획서 ④ 와 한 군데 어긋난다 — 공격 회복 (2026-09-19)

기획서는 공격을 **「놓으면 상체가 한 뼘 내려앉고, 회복 구간에 다시 일어서지 못한다」**로 적었다.
**그걸 그대로 쓰면 루프가 깨진다** — 공격이 끝난 자세가 이동 루프의 시작 자세와 달라지고,
원거리 사수 v1 이 정확히 그 이유로 실패했다(「위로 안 돌아온다 → 다음 루프와 자세가 안 이어진다」).

그래서 **「내려앉음」은 중간 프레임의 사건으로 두고, 마지막은 같은 자세로 되돌린다**
(`the torso sinking lower, then settling back into the same low drawn stance`).
「쏠수록 작아진다」는 **한 번의 공격 안에서가 아니라 연출·이펙트가 맡을 자리**다 — 지금 범위 밖.

## 🔴 잘못 넣은 회차 (2026-09-19 · 2 gen 버림)

첫 이동 호출에서 도구 호출 구문을 잘못 써 **문구 끝에 `</action_description><parameter name=...>` 잡음이 섞였다.**
그룹 `ee1f7582-dc33-4603-b55c-217b924705af` 로 큐에 들어갔고, 생성이 끝나는 대로 `delete_animation` 으로 지운다.

📌 **같은 문구는 재큐잉이 안 되므로**(`already queued or complete`) 다시 넣은 이동 문구는
끝에 `the whole time` 을 한 번 더 붙여 문구를 달리했다 — 의미는 같다.
