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
| Hit | 5 | `hit_southeast_v2` | `ea070374-7bdb-4e21-a88d-810b01b4fd51` | 대기 |
| Dead | 9 | `dead_southeast_v1` | `77e0a807-7281-4674-a639-432afda5d0ed` | ✅ |

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
