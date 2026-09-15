# knight_red — 애니메이션 생성 문구 기록

> 🔴 **이 파일이 생긴 이유** (2026-09-15).
> 방향을 `east` → `south-east` 로 바꾸려고 보니 **기존 9상태를 만든 문구가 어디에도 없었다.**
> PixelLab 목록에는 `[type=custom-dark black cape, hood up with ]` 로 **잘려서** 남는다.
> 문구가 결과를 크게 가르는 모델인데(`slash` 한 단어가 참격을 굽는다) 그 입력이 휘발됐던 것이다.
> **앞으로 캐릭터 애니메이션을 생성하면 여기에 먼저 적는다.**

## 공통

```
character_id   2fbf625f-f755-4d1d-a113-adadfdf97ce0   (No Weapon state)
group          566c2942-f7d2-4dfc-b424-8b18caf7150a   (9 states)
캔버스          92x92 · 발밑 y=77 · 피벗 0.163
mode           v3  (template 은 정렬이 달라 쓰지 않는다 — difftest_walk 가 발중심 9px 왼쪽이었다)
비용           2 gen / 상태 / 방향  (≤96px)
```

## 문구 규칙 (실측으로 얻은 것)

| | |
|---|---|
| 🔴 `slash` · `arc` 금지 | 참격 이펙트를 **그림에 구워 버린다**. Light 의 `sweeps across` 는 깨끗했고 Heavy 의 `overhead slash` + `wide arc` 는 흰 호가 구워졌다 (2026-09-14) |
| 부정문 대신 긍정문 | `face never visible` 은 안 먹고 `a smooth faceless helm` 은 먹는다. 「하지 마라」가 아니라 **「무엇이 보이는가」** |
| 중요한 것을 앞에 | 길면 희석되는 모델이다. 정체성 어휘를 뒤에 두면 안 먹는다 |
| 정체성 어휘를 매번 반복 | `dark black cape, hood up with the face in deep shadow` 로 시작한다. 빼면 다른 사람이 나온다 |
| 자세를 **안 움직이는 부분까지** 적는다 | Idle v1 은 `hands held open and visible low at the sides` 때문에 **팔이 벌어졌다**. 숨쉬기는 팔이 안 움직이는 것이 요점이라 v2 에서 `arms hanging relaxed straight down close to the body` 로 바꿨다 |

## south-east (2026-09-15~)

| 상태 | frames | animation_name | group | 판정 |
|---|---|---|---|---|
| Idle | 9 | `idle_southeast_v2` | `decd8cd1-0397-47d5-8f6a-55a157e78a4c` | ✅ 채택 |
| Run(Walk) | 8 | `walk_southeast_v1` | `876d7d65-fcec-40f0-a54a-7613016c4e7b` | ⚠️ 발밑 3px 흔들림 |
| AttackLight | 9 | `atk_light_southeast_v1` | `7f481741-d6b9-400a-851f-e2ba710eb079` | ✅ |
| AttackHeavy | 9 | `atk_heavy_southeast_v1` | `73d1919e-4222-4bd3-9067-92f7972413dc` | 미확인 |
| Hit | 9 | `hit_southeast_v1` | `c57624bb-4c8a-4121-a76c-3e0227b3d8f6` | 미확인 |
| Dead | 9 | `dead_southeast_v1` | `3eab8e96-545a-42cc-96e8-9868a84e2854` | 미확인 |
| Dash | 5 | `loop_dash_southeast_v1` | `90f8766d-758a-40f3-b060-994a5359135f` | ✅ 자세+루프 |
| Jump | 4 | `pose_jump_southeast_v4` f5~f8 | `47c05395-3341-42be-8468-d83d6b95e5a5` | ✅ v1~v3 폐기 |
| Fall | 4 | `loop_fall_southeast_v1` | `1d66d1af-9495-41d9-87ee-8764cfec6d11` | ✅ 자세+루프 |

🔴 **공중·대시는 한 번에 안 된다 — 2단 공정이다.**

```
① pose_*   자세를 '멈춘 상태'로 서술해 여러 프레임 뽑고, 가장 좋은 한 장을 고른다
② loop_*   그 한 장을 custom_start_frame_base64 로 넣고 "몸은 그대로, 망토만" 을 시킨다
```

`dash_southeast_v1` · `jump_southeast_v1` · `fall_southeast_v1`(폐기)은 **동작**을 서술했다가
셋 다 **서 있는 그림**이 나왔다. 2026-09-13 에 east 에서 같은 자리를 밟고
「공중·대시는 자세 하나 + 망토 루프였다」로 적어 둔 그대로다.

🔑 **92x92 PNG 는 base64 로 2.7KB 라 인라인으로 충분하다.** 스키마가 경고하는 잘림은 큰 이미지 얘기다.

🔑 **프레임 수는 `frame_count` 와 `keep_first_frame` 이 함께 정한다.**
`keep_first_frame=true` 면 참조 프레임이 f0 으로 남아 `frame_count+1` 장이 된다.
루프(Run·Jump·Fall)는 `false` 로 둬 시작 포즈가 중복되지 않게 한다.
`frame_count` 는 **짝수만** 받는다 — 5프레임(Dash)은 `frame_count=4` + `keep=true` 로 만든다.

### Idle — v2 (채택 예정)

```
dark black cape, hood up with the face in deep shadow, standing still and breathing slowly,
chest and shoulders rising and falling gently, both arms hanging relaxed straight down close
to the body, feet planted flat, cape hanging straight down
```
`frame_count=8` · `keep_first_frame=true` → 9프레임

### Idle — v1 (폐기 · `cd17fea1-a9df-45d2-9bc9-091a8c5984e5`)

```
dark black cape, hood up with the face in deep shadow, standing at rest and breathing slowly,
weight settled evenly on both feet, both gauntleted hands held open and visible low at the sides,
cape hanging still behind
```
❌ **팔이 벌어져 「준비자세」가 됐다** (bbox 폭 36→46px). 사용자 반려 —
숨쉬기로 다시. 📌 **손을 보이게 하려고 넣은 구절이 자세를 바꿨다.**

### Run (Walk) — v1

```
dark black cape, hood up with the face in deep shadow, walking forward at a steady pace,
both gauntleted arms swinging naturally at the sides, legs striding clearly, cape trailing behind
```
`frame_count=8` · `keep_first_frame=false` → 8프레임 (기존 Run 과 같은 수. 루프라 참조 프레임을 안 남긴다)

### AttackLight — v1

```
dark black cape, hood up with the face in deep shadow, the right arm sweeps across the body
from back to front at waist height, torso turning with the motion, the left arm held back for
balance, cape trailing
```
`frame_count=8` · `keep_first_frame=true` → 9프레임

## east (2026-09-13~14 · 문구 유실)

`idle_east_v3` · `atk_light_east_v3` · `atk_heavy_east_v3b` · `hit_east_v3b` · `dash_east_v3b` ·
`jump_east_v3` · `fall_east_v3` · `dead_east_v3` · `difftest_walk`(템플릿).

🔴 **문구가 남아 있지 않다.** 알려진 것은 `atk_heavy` 2차에서 `strike` + `come down hard` 로
흰 호를 없앴다는 것뿐이다(2026-09-14 devlog). 재현이 필요하면 **다시 세워야 한다.**

### AttackHeavy — v1

```
dark black cape, hood up with the face in deep shadow, raising both arms high overhead then
bringing a heavy two-handed strike straight down in front, the body dropping low as it comes
down hard, cape flaring out
```
📌 `strike` + `comes down hard` 를 쓴 것은 의도다 — `slash`·`arc` 를 쓰면 흰 호가 구워진다(2026-09-14).

### Hit — v1

```
dark black cape, hood up with the face in deep shadow, recoiling backward from a blow,
head snapping back, both arms flung outward, torso bent away, feet sliding back,
cape whipping forward
```

### Dead — v1

```
dark black cape, hood up with the face in deep shadow, collapsing forward to the ground,
knees buckling first, the body going limp and settling flat, cape draping over the body
```
📌 사망 시 **무기는 손을 떠나 튕겨 나간다**(사용자 결정 2026-09-15). 그래서 이 상태의 손 앵커는
안 채운다 — 자세 문제가 아니라 손에 무기가 없는 것이 맞다.

### Dash — v1

```
dark black cape, hood up with the face in deep shadow, leaning far forward into a fast ground
dash, one leg stretched ahead and one trailing behind, both arms swept back along the body,
cape streaming straight back
```

### Jump — v1

```
dark black cape, hood up with the face in deep shadow, rising upward through the air after a
jump, knees tucked up, both arms lifted and held close, cape streaming downward below the body
```

### Fall — v1

```
dark black cape, hood up with the face in deep shadow, falling downward through the air,
both legs reaching down toward the ground, arms spread out for balance, cape streaming upward
above the body
```

---

## 🔴 south-east 로 옮기며 알게 된 것 (2026-09-15)

**① 공중감은 부양이 아니라 자세에서 온다.**
점프 자세가 안 나와서 「발을 떼라」에 힘을 준 문구를 두 번 썼는데 둘 다 실패했다.
그러다 기존 east 를 재보니 **east jump 의 발밑도 77~79 로 땅에 붙어 있었다.**
전제가 틀렸던 것이다. 세 번째에 부양을 빼고 **접기**만 말하니 나왔다.

```
❌ both boots lifted clear off the ground and tucked under        -> 서 있는 그림
❌ one knee driven up high ... the other boot kicks back far       -> 앞차기
✅ both knees folded up tight so each boot is pulled up right
   under the thigh, the whole body curled into a compact ball      -> 높이 60 -> 45
```

**② 「동작」이 아니라 「멈춘 자세」로 쓴다.**
`leaning far forward into a fast ground dash` 는 안 먹고
`the body held frozen in a deep forward lunge almost horizontal` 는 먹었다.
v3 는 시작 프레임에서 크게 못 벗어나므로 **가야 할 곳을 그림처럼 묘사**해야 한다.

**③ 손을 보이게 하려고 넣은 말이 자세를 바꾼다.**
Idle v1 의 `hands held open and visible low at the sides` 가 팔을 벌려 버렸다.
south-east 는 **3/4 시점 자체가 두 손을 보장**하므로 문구로 강제할 필요가 없다.

**④ 번들 엔드포인트는 생성 중이면 거부한다.**
`GET /mcp/characters/{id}/spritesheet` 이 `Character has N animation(s) still being generated`
를 낸다. **큐를 비우고 한 번에 받는** 순서로 일해야 한다. 이쪽이 `get_character` 보다 훨씬 싸다.

**⑤ 번들 칸은 100x96, 낱장 URL 은 92x92 다.**
번들에서 **오프셋 (4,2) 로 92x92 를 떼면 낱장과 픽셀 단위로 동일**하다(교차검증함).
2026-09-14 의 「서버 시트 칸은 100x96」이 여기서 나온 것이다.

**⑥ 발밑은 상태 사이에서도 맞춰야 한다.**
`loop_dash` 만 78 이고 나머지가 77 이라 그대로 두면 **대시 전환 때 1px 튄다.**
`Tools/ArtPipeline/align_feet.py` 로 맞춘다.

**⑦ 🔴 공중 자세는 짝으로 판정한다 — Jump 혼자 보면 틀린다.**

Jump 를 세 번 만들었고 **처음 반려한 v2 가 정답이었다.**

```
             높이(bbox)      Fall 과의 차이
east  jump   59              0~1px      <- 기준
v3 웅크림    45~48           약 15px    🔴 정점에서 실루엣이 튄다
v2           61              0~1px      ✅
```

v3(공처럼 마는 자세)는 **단독으로 보면 「확실히 공중이다」라서 좋아 보인다.** 그런데 게임에서는
`Jump -> 정점 -> Fall` 로 이어지므로, 낙하 자세와 **키가 같아야** 연속으로 읽힌다.
v2 를 「앞차기 같다」며 반려한 것은 **한 장씩 본 판단**이었다.

📌 **east 의 짝이 정답지였다** — jump 는 다리를 들어올리고 fall 은 다리를 펴고 팔을 벌린다.
둘 다 **선 키 그대로**이고 달라지는 것은 팔다리와 망토 방향뿐이다.
「동작은 레퍼런스부터 볼 것」의 공중 판본이고, 여기서는 레퍼런스가 **우리 자신의 east 자산**이었다.

⚠️ 그래서 `loop_jump_southeast_v1`(1 gen)은 버렸다. 루프도 필요 없었다 —
`pose_*` 의 뒤쪽 4프레임이 이미 자세를 유지한 채 망토만 흔든다.
**Dash 도 같은 방법으로 gen 1 을 아낄 수 있었다.**

---

## 🔴 Jump — 네 번 만들고서야 나왔다 (2026-09-15)

| | 문구의 요지 | 결과 |
|---|---|---|
| v1 | `both boots lifted clear off the ground and tucked under` | 서 있는 그림 |
| v2 | `one knee driven up high ... other boot kicks back far behind` | **앞발차기** |
| v3 | `both knees folded up tight ... curled into a compact ball` | 공처럼 말림 — Fall 과 키가 15px 차이 |
| **v4** | 아래 | ✅ |

```
leaping upward off the ground, the front knee lifted high with the shin hanging straight down
so the boot sits directly under that knee, the back leg swept out behind with the knee bent
and the heel kicked up toward the hip, the torso tipped forward over the lifted knee,
both fists brought up in front of the chest, cape swept back and streaming down
```
시작 프레임: **`walk` f0**(다리가 34px 로 가장 벌어진 프레임)을 `custom_start_frame_base64` 로.

### 🔑 점프와 발차기를 가르는 한 줄

**무릎은 올리되 정강이는 내린다.**

```
정강이가 앞을 향한다        -> 앞발차기
무릎 아래로 접혀 발이 무릎 바로 밑  -> 점프
```

v1~v3 의 문구에 이 구절이 **하나도 없었다.** 「발을 떼라」·「접어라」·「무릎을 올려라」는 다 썼는데
**정강이 방향**을 말한 적이 없다. east jump 를 12배로 확대해 다리 기하를 뜯어보고서야 찾았다.

같이 따라오는 것: **몸통을 앞으로 기울이고 · 뒷다리는 뒤로 스윙하며 무릎을 굽히고 · 망토는 뒤·아래로.**

### 📌 시작 프레임이 자세를 가둔다

v1~v3 는 전부 **서 있는 회전 정지컷**에서 출발했다. v3 모드는 시작 프레임에서 크게 못 벗어나므로
다리가 나란히 선 그림에서 출발하면 **다리를 앞뒤로 못 벌린다.**
다리가 이미 벌어진 `walk` 프레임에서 출발하니 한 번에 나왔다.
**목표 자세에 가까운 프레임을 시작점으로 준다** — 이것이 문구보다 강하다.
