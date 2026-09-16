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
| Hit | 5 | `hit_southeast_v2` | `e8b5dcd3-f9dd-43f1-bda4-0682ce3ec53e` | 대기 |
| Dead | 9 | `dead_southeast_v1` | `35f73042-2eb7-45cf-aacd-eb8fb6a8174e` | ✅ |

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
