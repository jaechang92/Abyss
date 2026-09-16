# ancient_shield — 애니메이션 생성 문구 기록

> 형식·문구 규칙은 `Art_Source/characters/knight_red/animation_prompts.md` 를 따른다(같은 인물 그룹).
> 계획·몸 판정은 `Art_Source/20_SUBJECTS/forms/_PLAN_SE.md`.
> 🔴 **생성하면 여기에 먼저 적는다** — 문구가 휘발되면 재현이 안 된다(knight_red 2026-09-15).

## 공통

```
character_id   2de47574-d60c-4f47-bd0d-0204065ed52a   (Shield Body v2)
group          566c2942-f7d2-4dfc-b424-8b18caf7150a
캔버스          92x92 · 방향 south-east · 1방향 + flipX
mode           v3 · 2 gen / 상태
정체성 어휘      golden hooded heavy plate armor, hood up with the face in deep shadow
```

## south-east (2026-09-17~)

| 상태 | frames | animation_name | group | 판정 |
|---|---|---|---|---|
| Idle | 9 | `idle_southeast_v1` | `331eedff-7d2b-41dc-9358-f0af87c6a414` | ✅ (뒤쪽 프레임 망토 날림) |
| Run(Walk) | 8 | `walk_southeast_v1` | `fa4b9cd9-1fb6-4572-b005-42d7bc75f32f` | ✅ ⚠️ 발밑 73~77 → `align_feet` |
| Hit | 9 | `hit_southeast_v1` | `a62c6a65-0c0f-4526-8ecd-473d8aa220ec` | ❌ 주먹을 앞으로 뻗으며 끝남 |
| Hit | 5 | `hit_southeast_v2` | `ea070374-7bdb-4e21-a88d-810b01b4fd51` | ✅ 웅크리며 팔을 모음 |
| Dead | 9 | `dead_southeast_v1` | `77e0a807-7281-4674-a639-432afda5d0ed` | ✅ |
| AttackLight | 9 | `atk_light_southeast_v1` | `617529fb-fbdb-4f91-a2a5-1ba1ae5fd503` | ✅ 밀쳐치기 |
| AttackHeavy | 9 | `atk_heavy_southeast_v1` | `d3349417-0c53-4152-8fad-ec8cdec99088` | ✅ 들어 올렸다 낮게 내려찍기 |
| Jump | 9 | `pose_jump_southeast_v1` | `3cebc983-5cd9-417b-9679-724828ebee98` | ✅ 후보 f4~f7 · 시작 walk f5 |
| Fall | 9 | `pose_fall_southeast_v1` | `8c9965b3-dc68-4871-aa15-89c8e10f00ec` | ❌ 서서 팔만 뻗음 |
| Fall | 9 | `pose_fall_southeast_v2` | `424cd629-027e-4c1b-8b1c-271ab0e1bb26` | ✅ 후보 f4~f7 · 시작 Jump f6(base64) · 키 60~61 = Jump 와 짝 |
| Dash | 9 | `pose_dash_southeast_v1` | `0009fe02-6617-4cc3-a776-d7d296108fc0` | ❌ 약간 숙일 뿐 |
| Dash | 9 | `pose_dash_southeast_v2` | `83012eb3-f120-4322-8797-812edae9c667` | ⚠️ 잠정 — 서서 걷는 쪽에 가깝다. 후보 f3~f6 · 시작 walk f5 · 플레이에서 판단 |

### Idle — idle_southeast_v1

```
golden hooded heavy plate armor, hood up with the face in deep shadow, standing still and breathing slowly, chest and broad pauldrons rising and falling gently, both arms hanging relaxed straight down close to the body, feet planted flat, golden cape hanging straight down
```

### Run(Walk) — walk_southeast_v1

```
golden hooded heavy plate armor, hood up with the face in deep shadow, walking forward at a slow heavy pace, both gauntleted arms swinging slightly at the sides, legs striding clearly with weight, golden cape trailing behind
```

### Hit — hit_southeast_v1 (폐기)

```
golden hooded heavy plate armor, hood up with the face in deep shadow, recoiling backward from a blow, head snapping back, both arms flung outward, torso bent away, heavy feet sliding back a little, golden cape whipping forward
```

### Hit — hit_southeast_v2 (`frame_count=4` · keep=true)

```
golden hooded heavy plate armor, hood up with the face in deep shadow, jolted by a blow to the chest, the body rocking back away from the direction it faces, head bowed, both arms pulled in tight against the chest, knees bent, heavy feet staying planted
```

📌 **Hit v1 은 SE 에서 앞쪽 동작으로 샜다** — 방패는 주먹을 뻗고 궁수는 팔로 앞을 가리키며 끝났다.
원인 추정: `arms flung outward` · `head snapping back` 이 3/4 에서 **앞으로 뻗는 팔**로 읽힌다.
v2 는 **팔을 가슴으로 모으고 발을 고정**, 프레임을 4장으로 줄였다(1 gen · 다른 동작으로 흘러갈 틈을 줄인다).

### Dead — dead_southeast_v1

```
golden hooded heavy plate armor, hood up with the face in deep shadow, collapsing forward to the ground, knees buckling first, the heavy armored body going limp and settling flat, golden cape draping over the body
```

📌 **공격 문구 규칙 (2026-09-17)** — 몸은 무기 없는 state 라 **무기 이름을 안 쓴다**(`bow`·`shield` 를 쓰면 그림에 구워질 수 있다).
**팔·손의 움직임만** 적고, 무기를 쥐는 손은 앵커가 잡는 **오른손**(SE 에서 가까운 손)으로 통일한다. `slash`·`arc` 금지는 그대로.

### AttackLight — atk_light_southeast_v1

```
golden hooded heavy plate armor, hood up with the face in deep shadow, shoving forward hard, the right forearm driving straight out in front at chest height, the body leaning into the push as the front foot steps forward, the left arm held close to the side, golden cape trailing
```

### AttackHeavy — atk_heavy_southeast_v1

```
golden hooded heavy plate armor, hood up with the face in deep shadow, raising the right arm high overhead then bringing it straight down hard in front, the heavy body dropping low as it comes down, knees bending deep, the left arm braced at the side, golden cape flaring out
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

### Jump — pose_jump_southeast_v1

```
golden hooded heavy plate armor, hood up with the face in deep shadow, leaping upward off the ground, the front knee lifted high with the shin hanging straight down so the boot sits directly under that knee, the back leg swept out behind with the knee bent and the heel kicked up toward the hip, the torso tipped forward over the lifted knee, both fists brought up in front of the chest, golden cape swept back and streaming down
```

### Fall — pose_fall_southeast_v2 (시작 = 이 폼 Jump f6)

```
golden hooded heavy plate armor, hood up with the face in deep shadow, falling downward through the air, the body held frozen high above the ground with both legs stretched straight down and slightly apart, both arms spread out wide for balance, golden cape streaming upward above the body
```

📌 v1(같은 문구 · 회전 이미지에서 시작)은 **서 있는 그림**이었다. 무거운 판금 몸이 자세 변화를 가장 덜 받는다 —
**문구가 아니라 시작 프레임이 공중을 열었다**(knight_red Jump 교훈과 같다).

### Dash — pose_dash_southeast_v2 (시작 = walk f5)

```
golden hooded heavy plate armor, hood up with the face in deep shadow, bursting forward like a sprinter off the line, the torso held frozen low and nearly parallel to the ground, the front knee bent deep far ahead and the back leg stretched straight out behind, both arms swept back along the body, golden cape streaming straight back
```
