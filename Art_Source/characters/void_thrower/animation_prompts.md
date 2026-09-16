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
| Hit | 5 | `hit_southeast_v2` | `2f39b682-0eca-4f18-8b95-155676120cea` | 대기 (방패·궁수 v1 실패를 보고 v2 문구로 바로) |
| Dead | 9 | `dead_southeast_v1` | `0d706a45-ffef-4cc0-b51c-e2a41f15779a` | 대기 |

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

📌 밑단 실제 위치가 y=82 라 다른 폼(77)과 5px 차이가 난다 — `--max-shift 8` 이 필요한 이유다.
Idle·Walk 17장에서 확인: 그림자 전부 제거 · 망토 안쪽 구멍 없음 · 떨어진 점 1~6px 제거.

### Hit — hit_southeast_v2 (`frame_count=4` · keep=true)

```
long charcoal black bell-shaped hooded cloak, hood up with the face in deep shadow, jolted by a blow to the chest, the body rocking back away from the direction it faces, head bowed, both arms pulled in tight against the chest, the wide hem of the cloak swaying
```

### Dead — dead_southeast_v1

```
long charcoal black bell-shaped hooded cloak, hood up with the face in deep shadow, collapsing forward to the ground, knees buckling first, the body going limp and settling flat, the wide cloak spreading over the body
```
