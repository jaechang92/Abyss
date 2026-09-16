# void_thrower — 애니메이션 생성 문구 기록

> 형식·문구 규칙은 `Art_Source/characters/knight_red/animation_prompts.md` 를 따른다(같은 인물 그룹).
> 계획·몸 판정은 `Art_Source/20_SUBJECTS/forms/_PLAN_SE.md`.
> 🔴 **생성하면 여기에 먼저 적는다** — 문구가 휘발되면 재현이 안 된다(knight_red 2026-09-15).

## 🔴 2026-09-17 몸 교체 — 현행은 `911388e0` (Thrower Body v4)

이전 몸 `0f43556f`(바닥에 닿는 종 망토)는 **폐기**했다. 그 문구가 ① 바닥 그림자를 굽고 ② 다리를 가리고
③ 망토 앞자락 사이에 다리를 아예 안 그려서, Idle 을 네 번 고쳐도(떠 보임 → 걷는 것처럼 보임) 이상했다(사용자 지적 셋).
아래 「옛 몸」 절은 그 기록이다.

```
character_id   911388e0-5b2f-4ea9-be9e-ad85955ec083   (Thrower Body v4 · 소스 2fbf625f 붉은 No Weapon)
정체성 어휘      dark charcoal hooded cloak ending at mid-calf, hood up with the face in deep shadow
키             60 · 발밑 77 · 그림자 0  (방패·궁수와 같은 체격)
후처리          없음 — stripShadow false · freezeBelow/overlay 안 씀
```

몸 문구:
```
charcoal black hooded cloak with thin tan trim, hood up with the face hidden in deep black shadow, keeping exactly the same
height, build and leg length, narrow at the shoulders and flaring out wide into a broad hem that ends at mid-calf, dark lower
legs and dark boots visible below the hem side by side, light armor underneath, both hands empty and open
```

| 몸 시도 | 결과 |
|---|---|
| v3 `c5c41e78` (소스 0f43556f) | ❌ 부츠는 보이나 **다리가 길어져 7px 큼**(키 67) · SE 에 그림자 |
| **v4 `911388e0` (소스 2fbf625f)** | ✅ 키 60 · 그림자 0 · 부츠 나란히 |

🔑 **키를 지키려면 키가 맞는 몸에서 파생한다.** v3 는 종 망토 몸에서 밑단을 올리라 했더니 다리를 늘려 채웠다.
붉은 기사 몸(방패·궁수와 같은 출발점)에서 새로 입히니 한 번에 맞았고, 문구에 `keeping exactly the same height` 를 넣었다.
📌 「바닥에 닿는다」류 어휘를 빼니 **그림자가 안 나왔다** — 형태어가 부수 효과를 데려오는 것의 반대 확인.

| 상태 | frames | animation_name | group | 판정 |
|---|---|---|---|---|
| Idle | 9 | `idle_southeast_v1` | `3d543d7a-3780-4a14-b82c-5c7d18808875` | ✅ + `lowerRegion` — **앞발이 3px 떠 있었다**(사용자 지적) · 두 발 아래끝 76 |
| Run(Walk) | 8 | `walk_southeast_v1` | `f296cfd9-2828-4bef-86d1-775ef85d7ee9` | ✅ |
| AttackLight | 9 | `atk_light_southeast_v1` | `f17e6b73-0678-4d76-a1b9-fcf85169e9d7` | ✅ |
| AttackHeavy | 9 | `atk_heavy_southeast_v1` | `7c265291-d92e-434f-98a9-bbadcf42a1ca` | ✅ |
| Hit | 5 | `hit_southeast_v1` | `4e1074c7-9181-4346-b97b-e24873608f35` | ✅ |
| Dead | 9 | `dead_southeast_v1` | `bfa42f1d-61ce-4930-9a42-a0025989539c` | ✅ |
| Jump | 9 (f3~6) | `pose_jump_southeast_v1` | `b7b2f2e6-b644-4858-af02-6f862d947d86` | ✅ 시작 = walk f0 |
| Fall | 9 | `pose_fall_southeast_v1` | `d610548d-5d65-4409-8db0-79c112a2a92c` | ❌ 서서 팔만 벌림 |
| Fall | 9 (f4~7) | `pose_fall_southeast_v2` | `3c83c178-bf4b-4cdc-846c-2364f1920de5` | ✅ 시작 = Jump f6 · 망토를 펼친 공중 |
| Dash | 9 (f3~7) | `pose_dash_southeast_v1` | `67ea0420-7558-4833-8f8b-fd99952c6189` | ✅ |

문구는 이 파일 아래 옛 몸 절의 판본에서 정체성 어휘만 바꿨다(Hit = v2 웅크림 · AtkL = v2 · 공중·대시 = pose).

🔴 **발밑 정렬은 「가장 아래 픽셀」 하나만 본다 — 다른 발이 떠 있어도 통과한다.** Idle 은 뒷발이 77 에 닿아 정렬이 맞았고,
앞발(x48~56)은 y73 에서 끝나 3px 떠 있었다. 3/4 시점에서는 **앞발이 화면상 더 아래**여야 하므로 반대로 그려진 것이다.
→ 레시피 `lowerRegion {x0 47, x1 57, top 71, bottom 73, by 3, fillFromRow 70}` — 부츠 줄만 내리고 틈은 정강이 줄로 잇는다.
📌 **두 발이 보이는 서 있는 상태는 발마다 아래끝을 따로 잰다.**
🔴 **틈을 메우는 줄은 한 프레임에서만 가져온다**(`fillFromFrame 0`). 처음엔 프레임마다 자기 y70 을 복사했는데,
f4~f8 은 그 줄을 **망토 밑단 외곽선**이 지나가서 검정 4px 이 세로로 세 줄 늘어났다(사용자 지적). 다리는 모든 프레임에서
안 움직이므로 깨끗한 f0 의 정강이를 쓰면 된다 — y71~73 검정이 프레임마다 9px(외곽선만)로 같아졌다.

---

# 옛 몸 `0f43556f` 기록 (폐기)

## 공통

```
character_id   0f43556f-6e97-4406-929d-eb79928070eb   (Thrower Body v2)
group          566c2942-f7d2-4dfc-b424-8b18caf7150a
캔버스          92x92 · 방향 south-east · 1방향 + flipX
mode           v3 · 2 gen / 상태
정체성 어휘      long charcoal black bell-shaped hooded cloak, hood up with the face in deep shadow
```

## south-east (2026-09-17~)

| 상태 | frames | animation_name | group | 판정 |
|---|---|---|---|---|
| Idle | 9 | `idle_southeast_v1` | `fa2df079-50d8-47d4-a85a-b444ca093052` | ❌ 그림자를 걷자 발이 바닥선 2~3px 위에서 끝나 **떠 보였다**(사용자 지적) |
| Idle | 9 | `idle_southeast_v2` | `24b34f1d-bcb6-4d0e-8291-efd30060314d` | ❌ 문구만 바꿔 같은 자세 · 그림자 테두리 호가 남음 |
| Idle | 8 | `idle_southeast_v3` | `793203fe-3513-47f5-a73d-74650e942b13` | ❌ 부츠는 딛지만 **두 발을 걷듯이 옮긴다**(사용자 지적) |
| Idle | 8 | `idle_southeast_v4` | `3d25454f-1931-42b2-8601-3ad5a448385e` | ❌ 발은 고정됐지만 **보폭 자세라 여전히 걷는 것처럼** 읽힌다(사용자 지적) |
| **Idle** | 9 | **`idle_southeast_v1` + 하체 고정 + 손으로 그린 두 다리** | — | ✅ 채택 — `sheet_recipe.json` · `idle_legs_patch.png` |
| Run(Walk) | 8 | `walk_southeast_v1` | `2fa839ac-0a42-4043-8421-d2068c7f6ae0` | ✅ 🔴 **그림자 후처리 필수** · 다리 보임 |
| Hit | 5 | `hit_southeast_v2` | `2f39b682-0eca-4f18-8b95-155676120cea` | ✅ (방패·궁수 v1 실패를 보고 v2 문구로 바로) |
| Dead | 9 | `dead_southeast_v1` | `0d706a45-ffef-4cc0-b51c-e2a41f15779a` | ✅ 그림자 후처리 8장 |
| AttackLight | 9 | `atk_light_southeast_v1` | `2b77b046-35a4-44a2-b629-0351b85630d7` | ❌ 팔이 거의 안 움직임 |
| AttackLight | 9 | `atk_light_southeast_v2` | `fab33446-d156-4716-b779-6a165b32fe30` | ⚠️ 잠정 — 가운데(f5~7) 몸이 옆으로 돌고 끝 한 장에서 크게 던짐. 강공격과 끝이 닮았다 |
| AttackHeavy | 9 | `atk_heavy_southeast_v1` | `ee00055b-7379-4c97-8e96-70ab13192536` | ✅ 젖혔다 던지며 망토가 뒤로 펼쳐짐 — 삼각 |
| Jump | 9 | `pose_jump_southeast_v1` | `592c33ba-cf57-469c-9299-8867bdf64597` | ✅ 후보 f4~f7 · 시작 walk f4 |
| Fall | 9 | `pose_fall_southeast_v1` | `5c722846-0162-42d1-9364-67fd6fedd211` | ✅ 후보 f5~f8 · 망토가 위로 크게 부풂 |
| Dash | 9 | `pose_dash_southeast_v1` | `327c239a-a922-436e-a3f1-19f90605142e` | ✅ 후보 f4~f7 · 망토가 뒤로 흐름 |

### Idle — idle_southeast_v1

```
long charcoal black bell-shaped hooded cloak, hood up with the face in deep shadow, standing still and breathing slowly, chest and shoulders rising and falling gently, both arms hanging relaxed straight down close to the body, the wide hem of the cloak resting still on the ground
```

### Run(Walk) — walk_southeast_v1

```
long charcoal black bell-shaped hooded cloak, hood up with the face in deep shadow, walking forward at a steady pace, both arms swinging naturally at the sides, the wide hem of the cloak swaying and sweeping forward with each step
```

## 🔴 모든 프레임에 후처리 — 구워진 바닥 그림자

종 모양 망토(「밑단이 바닥에 닿는다」)가 **단색 `#141412` 바닥 그림자**를 불렀다. 프레임마다 생겼다 없어지고
발밑을 y=84 까지 끌어내린다. **받은 프레임마다** 아래 순서로 돌린다(2026-09-17 사용자 결정 — 재생성 대신 gen 0).

```
python Tools/ArtPipeline/strip_ground_shadow.py <프레임폴더>
python Tools/ArtPipeline/align_feet.py <프레임폴더> --target 77 --max-shift 8     # 지상 상태만
```

🔴 **정정 (2026-09-17)** — 「밑단이 y=82 라 5px 낮다」는 **100x100 캔버스에서 잰 값**이었다. 투척사 애니메이션만 v3 가 캔버스를 100 으로 키웠고,
가운데 92 로 떼면 발밑은 77~80 이다. 시트 조립은 `build_form_sheets.py`(레시피 `sheet_recipe.json`)가 한다.
Idle·Walk 17장에서 확인: 그림자 전부 제거 · 망토 안쪽 구멍 없음 · 떨어진 점 1~6px 제거.

### Hit — hit_southeast_v2 (`frame_count=4` · keep=true)

```
long charcoal black bell-shaped hooded cloak, hood up with the face in deep shadow, jolted by a blow to the chest, the body rocking back away from the direction it faces, head bowed, both arms pulled in tight against the chest, the wide hem of the cloak swaying
```

### Dead — dead_southeast_v1

```
long charcoal black bell-shaped hooded cloak, hood up with the face in deep shadow, collapsing forward to the ground, knees buckling first, the body going limp and settling flat, the wide cloak spreading over the body
```

📌 **공격 문구 규칙 (2026-09-17)** — 몸은 무기 없는 state 라 **무기 이름을 안 쓴다**(`bow`·`shield` 를 쓰면 그림에 구워질 수 있다).
**팔·손의 움직임만** 적고, 무기를 쥐는 손은 앵커가 잡는 **오른손**(SE 에서 가까운 손)으로 통일한다. `slash`·`arc` 금지는 그대로.

### AttackLight — atk_light_southeast_v1 (폐기)

```
long charcoal black bell-shaped hooded cloak, hood up with the face in deep shadow, the right arm whipping quickly forward from beside the shoulder and releasing at chest height with a flick of the wrist, the left arm held back for balance, the wide hem of the cloak swaying
```

📌 `flick of the wrist` 같은 **작은 동작어**는 v3 가 거의 안 움직인다 — 시작과 끝 자세를 크게 벌려 적어야 한다.

### AttackLight — atk_light_southeast_v2

```
long charcoal black bell-shaped hooded cloak, hood up with the face in deep shadow, the right hand drawn back beside the ear, then the right arm snapping far forward to full length with the hand opening at the end, the torso turning into the throw, the left arm swinging back, the wide hem of the cloak swaying
```

### AttackHeavy — atk_heavy_southeast_v1

```
long charcoal black bell-shaped hooded cloak, hood up with the face in deep shadow, the torso twisting far back with the right arm drawn back behind the head, then lunging forward and hurling the right arm forward and down with full force, the left arm swinging back, the wide cloak flaring out behind the body
```

## 공중·대시 — pose 한 번으로 (2026-09-17)

`knight_red` 의 2단 공정(① `pose_*` → ② `loop_*`) 중 **①만** 쓴다 — 거기서 「pose 의 뒤쪽 프레임이
이미 자세를 유지한 채 망토만 흔든다 · Dash 도 gen 1 을 아낄 수 있었다」고 적어 뒀다.
🔴 **프레임 범위는 후보다.** R3(Unity 임포트)에서 **Jump·Fall 을 짝으로** 키·발밑을 재서 확정한다.

| | 시작 프레임 |
|---|---|
| Jump | 다리가 가장 벌어진 `walk` 프레임(하단 15줄 폭으로 잼) — URL 로 넘김 |
| Fall · Dash | 회전 이미지(기본). 🔴 방패는 이것으로 **서 있는 그림**이 나와 v2 에서 시작 프레임을 줬다 |

📌 **번들 시트 `/spritesheet` 는 인증 없이 받아진다**(생성 중이면 423). `get_character` 출력이 애니메이션마다
URL 9줄씩 늘어 수만 자가 되므로 **판정용으로는 번들이 훨씬 싸다.** 칸 크기가 캐릭터마다 다르다(92·100·104) —
pivot-centred 이므로 92x92 는 `((cw-92)/2, (ch-92)/2)` 에서 뗀다.
📌 **동시 작업 한도는 8개다.** 9번째는 `need 1 job slots` 로 거부된다(과금 없음).

🔴 투척사는 공중·대시에도 **그림자 후처리**를 돌린다(`align_feet` 는 공중 상태에 쓰지 않는다).

### Jump — pose_jump_southeast_v1

```
long charcoal black bell-shaped hooded cloak, hood up with the face in deep shadow, leaping upward off the ground, the front knee lifted high with the shin hanging straight down, the back leg swept out behind with the knee bent, the torso tipped forward, both fists brought up in front of the chest, the wide cloak swept back and streaming down below the body
```

### Fall — pose_fall_southeast_v1

```
long charcoal black bell-shaped hooded cloak, hood up with the face in deep shadow, falling downward through the air, the body held frozen with both legs stretched straight down, both arms spread out wide for balance, the wide cloak billowing upward above the body
```

### Dash — pose_dash_southeast_v1

```
long charcoal black bell-shaped hooded cloak, hood up with the face in deep shadow, the body held frozen in a deep forward lunge almost horizontal, the front leg stretched far ahead and the back leg trailing behind, both arms swept back along the body, the wide cloak streaming straight back
```

### Idle — idle_southeast_v3 (시작 = walk f3 · `keep_first_frame=false` · 8장)

```
long charcoal black bell-shaped hooded cloak, hood up with the face in deep shadow, the body held still in place in a relaxed standing stance, both dark boots planted firmly on the ground slightly apart and showing below the cloak, breathing slowly with the chest rising and falling gently, arms hanging relaxed at the sides, the cloak swaying only slightly
```

🔴 **그림자를 걷으면 그림자가 가리던 것이 드러난다.** v1 의 발은 원래도 바닥에 안 닿았고, 구워진 바닥 그림자가
그 틈을 메워 서 있는 것처럼 보였을 뿐이다. 수치(bbox 아래끝 77)는 **밑단 끝**이 닿아서 맞았다 — 발이 아니었다.
📌 v2 는 「부츠가 보인다」를 문구로 빌었고 안 됐다. v3 모드는 시작 프레임에서 크게 못 벗어난다 —
**부츠가 이미 바닥을 딛고 있는 Walk 프레임에서 출발**하니 됐다(방패 Fall 과 같은 교훈).

### Idle — idle_southeast_v4 (시작 = 끝 = v3 f5 · base64 · 8장) + 하체 고정

```
long charcoal black bell-shaped hooded cloak, hood up with the face in deep shadow, standing completely still with both boots fixed flat on the ground and not moving at all, the legs motionless, only the chest and shoulders rising and falling gently with slow breathing, the cloak hanging still with a faint sway
```

🔴 **시작·끝 프레임을 같게 줘도 v3 는 중간에서 다리를 옮긴다.** 「루프 = 같은 키 포즈 둘」(09-13 규칙)은 **자세를
대체로 유지**할 뿐 발을 못 박는다 — v3·v4 모두 발 영역 프레임간 변화가 30~120px 였다.
🔑 **gen 0 으로 확정한다** — `build_form_sheets.py` 레시피의 `freezeBelow {row 66, frame 0}`.
행별 변화량을 재니 다리는 **y=64 부터** 움직였다. 66 아래를 f0 으로 덮으니 발 변화 **0** · 몸통은 호흡(20~74px)이 남는다.
📌 **서 있는 상태에서 「움직이지 마라」는 생성에 빌지 말고 후처리로 보장한다.**

### ✅ Idle 최종 — v1 몸 + 손으로 그린 두 다리 (gen 0)

v1 하체를 픽셀로 뜯어보니 **망토 앞자락 사이(x42~48 · y68~76)가 통째로 비어 있었다** — 다리가 아예 없었고,
구워진 바닥 그림자가 그 자리를 채워 서 있는 것처럼 보였을 뿐이다.

```
레시피 idle   idle_southeast_v1 · freezeBelow y=66 (f0) · overlay idle_legs_patch.png · removeStrays
결과          9장 · 발밑 77 · 발 영역 프레임간 변화 0 · 몸통은 v1 호흡 그대로
```

- 다리 조각 색은 이 캐릭터 Walk 의 실제 픽셀에서 뽑았다 — 외곽 `#020202` · 바지 `(37,38,43)`/`(48,49,55)` · 부츠 `(29,30,34)` · 끈 `(160,113,68)`
- 앞다리 x43~46 · 뒷다리 x48~51(오른 자락이 끝나는 y72 부터 보인다) · 발끝은 SE 로 한 칸
- `removeStrays` 가 v1 에 남아 있던 **그림자 테두리 호(1px)**를 걷었다(프레임당 11px)

🔑 **보폭은 멈춰도 걷는 것으로 읽힌다.** v3·v4 는 발을 고정해도 한 발 앞·한 발 뒤라 걷는 중에 멈춘 모습이었다.
Idle 은 「발이 안 움직인다」가 아니라 **「두 발이 나란하다」**가 요점이다 — 시작 프레임을 Walk 에서 가져온 것이 그 자세를 끌고 왔다.
📌 **생성이 끝내 못 그리는 몇 픽셀은 손으로 그려 레시피에 남긴다.** 네 번 뽑는 것보다 확실하고 재현된다.

