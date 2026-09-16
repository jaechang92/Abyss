# void_archer — 애니메이션 생성 문구 기록

> 형식·문구 규칙은 `Art_Source/characters/knight_red/animation_prompts.md` 를 따른다(같은 인물 그룹).
> 계획·몸 판정은 `Art_Source/20_SUBJECTS/forms/_PLAN_SE.md`.
> 🔴 **생성하면 여기에 먼저 적는다** — 문구가 휘발되면 재현이 안 된다(knight_red 2026-09-15).

## 공통

```
character_id   38f6e24b-6f1b-4eff-be72-bf8c998c9060   (Archer Body)
group          566c2942-f7d2-4dfc-b424-8b18caf7150a
캔버스          92x92 · 방향 south-east · 1방향 + flipX
mode           v3 · 2 gen / 상태
정체성 어휘      purple hooded light leather armor with a quiver on the back, hood up with the face in deep shadow
```

## south-east (2026-09-17~)

| 상태 | frames | animation_name | group | 판정 |
|---|---|---|---|---|
| Idle | 9 | `idle_southeast_v1` | `1fd389cd-5897-4a65-a68c-59cfc843a1fb` | ✅ |
| Run(Walk) | 8 | `walk_southeast_v1` | `ca5a5510-84b7-4158-af32-f0553b55955d` | ✅ 발밑 76~78 |
| Hit | 9 | `hit_southeast_v1` | `23bba917-8644-476e-935f-d5322639601b` | ❌ 팔로 앞을 가리키며 끝남 |
| Hit | 5 | `hit_southeast_v2` | `e8b5dcd3-f9dd-43f1-bda4-0682ce3ec53e` | ❌ 뒤쪽 프레임에서 몸이 돌아 뒷모습·맨살 |
| Hit | 5 | `hit_southeast_v3` | `d268a294-2d25-41ba-b030-b53a259b311a` | ✅ 앞을 본 채 웅크려 막음 |
| Dead | 9 | `dead_southeast_v1` | `35f73042-2eb7-45cf-aacd-eb8fb6a8174e` | ✅ |
| AttackLight | 9 | `atk_light_southeast_v1` | `927904e7-dbc3-4dc4-a875-d469489d4525` | ⚠️ 손을 뺨까지 당김 · 앞팔이 덜 뻗음 — 채택 가능 |
| AttackHeavy | 9 | `atk_heavy_southeast_v1` | `060dff1e-0f01-4b6f-8a16-a06b8c68bb01` | ✅ 귀 뒤로 크게 당겼다 놓음 |
| Jump | 9 | `pose_jump_southeast_v1` | `5ec3abc1-6882-46ee-bd86-a90fd0ba95f4` | ✅ 후보 f3~f6 · 시작 walk f0 |
| Fall | 9 | `pose_fall_southeast_v1` | `b738f6e5-e06a-4976-a02b-cd01b0573e95` | ✅ 후보 f4~f7 |
| Dash | 9 | `pose_dash_southeast_v1` | `99c68600-6b39-46c4-bd8c-2c6202bc626a` | ✅ 후보 f5~f8 · 낮고 깊은 돌진(팔은 양옆으로) |

### Idle — idle_southeast_v1

```
purple hooded light leather armor with a quiver on the back, hood up with the face in deep shadow, standing still and breathing slowly, chest and shoulders rising and falling gently, both arms hanging relaxed straight down close to the body, feet planted flat
```

### Run(Walk) — walk_southeast_v1

```
purple hooded light leather armor with a quiver on the back, hood up with the face in deep shadow, walking forward at a light quick pace, both arms swinging naturally at the sides, legs striding clearly
```

### Hit — hit_southeast_v1 (폐기)

```
purple hooded light leather armor with a quiver on the back, hood up with the face in deep shadow, recoiling backward from a blow, head snapping back, both arms flung outward, torso bent away, feet sliding back
```

### Hit — hit_southeast_v2 (`frame_count=4` · keep=true)

```
purple hooded light leather armor with a quiver on the back, hood up with the face in deep shadow, jolted by a blow to the chest, the body rocking back away from the direction it faces, head bowed, both arms pulled in tight against the chest, knees bent, feet staying planted
```

📌 **Hit v1 은 SE 에서 앞쪽 동작으로 샜다** — 방패는 주먹을 뻗고 궁수는 팔로 앞을 가리키며 끝났다.
원인 추정: `arms flung outward` · `head snapping back` 이 3/4 에서 **앞으로 뻗는 팔**로 읽힌다.
v2 는 **팔을 가슴으로 모으고 발을 고정**, 프레임을 4장으로 줄였다(1 gen · 다른 동작으로 흘러갈 틈을 줄인다).

### Dead — dead_southeast_v1

```
purple hooded light leather armor with a quiver on the back, hood up with the face in deep shadow, collapsing forward to the ground, knees buckling first, the body going limp and settling flat
```

📌 **공격 문구 규칙 (2026-09-17)** — 몸은 무기 없는 state 라 **무기 이름을 안 쓴다**(`bow`·`shield` 를 쓰면 그림에 구워질 수 있다).
**팔·손의 움직임만** 적고, 무기를 쥐는 손은 앵커가 잡는 **오른손**(SE 에서 가까운 손)으로 통일한다. `slash`·`arc` 금지는 그대로.

### Hit — hit_southeast_v3 (`frame_count=4`)

```
purple hooded light leather armor with a quiver on the back, hood up with the face in deep shadow, flinching from a blow to the chest, shoulders hunching and the torso curling forward, both arms pulled in tight against the chest, knees bent, still facing the same way, feet planted
```

📌 v2 의 `rocking back away from the direction it faces` 가 **몸을 돌리게** 한 것으로 보고 뺐다 — 방향을 말하는 구절은 3/4 에서 회전으로 읽힐 수 있다.

### AttackLight — atk_light_southeast_v1

```
purple hooded light leather armor with a quiver on the back, hood up with the face in deep shadow, the right arm held straight out in front at shoulder height with the fist closed, the left hand pulling quickly back to the cheek and snapping open to release, a small recoil of the shoulders, feet planted apart
```

### AttackHeavy — atk_heavy_southeast_v1

```
purple hooded light leather armor with a quiver on the back, hood up with the face in deep shadow, the right arm held straight out in front at shoulder height with the fist closed, the left hand drawing slowly far back behind the ear and holding there with strain, the torso leaning back, then the left hand snapping open with a strong recoil, feet planted wide
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
purple hooded light leather armor with a quiver on the back, hood up with the face in deep shadow, leaping upward off the ground, the front knee lifted high with the shin hanging straight down so the boot sits directly under that knee, the back leg swept out behind with the knee bent and the heel kicked up toward the hip, the torso tipped forward over the lifted knee, both fists brought up in front of the chest
```

### Fall — pose_fall_southeast_v1

```
purple hooded light leather armor with a quiver on the back, hood up with the face in deep shadow, falling downward through the air, the body held frozen with both legs stretched straight down toward the ground and slightly apart, both arms spread out wide for balance
```

### Dash — pose_dash_southeast_v1

```
purple hooded light leather armor with a quiver on the back, hood up with the face in deep shadow, the body held frozen in a deep forward lunge almost horizontal, the front leg stretched far ahead and the back leg trailing straight behind, both arms swept back along the body
```
