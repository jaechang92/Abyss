# void_thrower — 애니메이션 생성 문구 기록

> 형식·문구 규칙은 `Art_Source/characters/knight_red/animation_prompts.md` 를 따른다(같은 인물 그룹).
> 계획·몸 판정은 `Art_Source/20_SUBJECTS/forms/_PLAN_SE.md`.
> 🔴 **생성하면 여기에 먼저 적는다** — 문구가 휘발되면 재현이 안 된다(knight_red 2026-09-15).

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
| Idle | 9 | `idle_southeast_v1` | `fa2df079-50d8-47d4-a85a-b444ca093052` | ✅ 🔴 **그림자 후처리 필수** |
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
