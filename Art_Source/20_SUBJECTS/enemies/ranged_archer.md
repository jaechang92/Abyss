# `ranged_archer` — 원거리 사수 · 표적을 잃은 쏘는 것

> 표시 이름 **원거리 사수**(그대로) · 일반 · 배치 S1·S2·S3 **9방** · 데이터 HP 25 · 피해 12 · 속도 2 · 탐지 8 · 사거리 5 · 쿨다운 1.8
> 🔴 `isRanged = 1` — **애니메이션을 붙이는 첫 원거리 적**이다. 발사 위치가 새로 걸린다(⑤-ⓐ)
> 작성 2026-09-18 · 목록: `_ROSTER.md` · 화풍 기준: `melee_grunt.md` R3 · 직전 종: `melee_brute.md`

---

## ① 정체

**하늘을 보는 자세 그대로 굳은 사수.** 떨어지는 화살을 쏘아 떨어뜨리던 것이 일이었고,
하늘이 다 내려앉아 화살이 그친 뒤에도 **위를 겨눈 채 멈추지 못했다.**

목이 뒤로 굳어 **아직도 위를 본다.** 플레이어가 오면 겨눈 활만 억지로 비틀어 내려 쏘고,
얼굴은 끝내 하늘에 남아 있다 — **표적을 보지 않고 쏘는 것**이 이 적의 전부다.

## ② 소설 근거 (1권 18·19장 「아래로 가는 길」·「하늘을 보는 자세」)

> *사람 키의 두 배쯤 되는 화살이었다. 깃이 위에 있고 촉이 땅에 박혀 있었다. **위에서 떨어져 꽂힌 것이다.***
> *나무가 아니었다. 쇠도 아니었다. 만지면 손끝이 서늘하고, 힘을 주면 조금 휘었다가 돌아왔다.*

> *떨어지는 화살을 쏘아 떨어뜨리는 것이 그들의 일이 되었다.*
> ***맞히지 못한 화살에 맞아서가 아니었다. 위를 보다가 목이 굳어 죽었다더라.***

### 🔴 누운 형체는 이 적이 **아니다**

19장의 누운 형체는 **두 팔을 벌리고 활을 옆에 내려놓은** 사람이다 — *"이 사람은 놓았구나."*
하란이 활을 얻는 장면이고, **놓은 것**이 그 장면의 전부다.

`_ROSTER` 의 「옆으로 누운 몸에 당긴 활」을 그대로 그리면 **그 형체와 같은 그림**이 되어
플레이어가 자기 활의 출처를 적으로 마주하게 된다. 그래서 적은 **놓지 못한 쪽**으로 뒤집는다.

| | 19장의 형체 (적 아님) | `ranged_archer` (적) |
|---|---|---|
| 활 | **옆에 놓여 있다** · 시위를 한 번도 안 걸었다 | **손에 굳어 있다** · 시위가 당겨진 채 |
| 자세 | 등을 대고 **누워** 두 팔을 벌림 | **주저앉아 뒤로 젖혀짐** — 눕지 않았다 |
| 얼굴 | 위를 향함 (평온) | 위를 향함 (**목이 꺾여 굳음**) |
| 뜻 | 놓았다 | 못 놓았다 |

## ③ 시각 브리프

| | 배정 | 근거 |
|---|---|---|
| **도형** | **위로 뻗은 대각선** — 주저앉은 몸에서 활과 팔이 비스듬히 위를 가리킨다. 근접 병사(가로로 낮게 긴 몸) · 중장 강적(둥근 덩어리)와 셋 다 갈린다 | `08-silhouette §5` · 「적은 서 있지 않다」 |
| **자세** | 무릎을 꿇은 채 **상체가 뒤로 젖혀져** 있고, 두 팔은 **머리 위 비스듬히** 활을 당겨 하늘을 겨눈다. 목이 완전히 뒤로 꺾여 얼굴이 위를 본다 | *위를 보다가 목이 굳어 죽었다* |
| **정체 표지** | ① **당겨진 채 굳은 활**(시위가 팽팽 · 화살이 걸려 있다) ② **뒤로 꺾인 목** ~~③ 몸 주위 땅에 깃이 위인 긴 화살~~ | 원문 셋 |
| | 🔴 **③ 은 뺐다(2026-09-18)** — 애니메이션에서 프레임마다 깜빡여 못 쓴다(⑤-ⓔ). 「화살의 들판」은 **몸이 아니라 배경·스테이지가 맡을 자리**다 | Unity 검증 |
| **색** | 🔑 **앞 둘과 갈린다** — 갑옷이 아니라 **바랜 흙빛 가죽과 천** 차림. 계통 ① 이지만 쇠의 벌판이 아니라 **화살의 들판** 출신이다 | 근접 병사·중장 강적이 둘 다 건메탈이라 세 번째도 쇠면 한 덩어리로 읽힌다 |
| **강조점** | **활에 걸린 화살과 땅의 화살** — 원문의 *"나무도 쇠도 아닌"* 것이라 **창백한 회백색**. 몸의 흙빛과 대비되어 「쏘는 것」이 먼저 읽힌다 | `_ROSTER` 강조점 규칙 · 플레이어 축 색(붉음·보라·금·주황)과 안 겹침 |
| **얼굴** | 후드 없음 · 얼굴 자리는 **어두운 빈 면**(뼈색 눈 강조 없음 — 위를 봐서 SE 시점에서 얼굴이 거의 안 보인다) | 계통 ① 은 「빈 것」 |
| **크기** | 몸 높이 **56~64px**(일반 상한) · 활 끝까지 포함해 캔버스 92 안. 🔴 **활이 세로를 먹는다** — 몸은 그보다 작게 잡힐 것 | `08-silhouette §1` |

## ④ 애니메이션

| 상태 | 동작 |
|---|---|
| 이동 | **무릎으로 끈다** — 겨눈 팔은 그대로 위에 둔 채 상체만 좌우로 흔들리며 조금씩 앞으로. 활은 절대 안 내린다 |
| 공격 | 🔑 **겨눈 활을 아래로 억지로 비틀어 내렸다가 쏘고, 곧바로 위로 되돌아간다** — 얼굴은 내내 위를 본다. 되돌아가는 것이 회복 구간 |
| | 🔑 **화살은 포물선으로 떨어진다**(2026-09-18 사용자 결정 · `arcExplodes=false`). 조준점이 「발사 시점의 플레이어 자리」라 **서 있으면 맞고 움직이면 피한다** — 「표적을 보지 않고 쏘는 것」이라는 정체와 맞는다. 터지지 않고 예고 링도 없다 |
| 피격 | 몸이 뒤로 흔들리고 **땅에 박힌 화살들이 떨린다** |
| 사망 | **당겨져 있던 시위가 풀리며 활을 놓는다** — 팔이 내려오고 몸이 옆으로 무너진다. *"이 사람은 놓았구나"* 의 반향 |

## ⑤ 미결

| | 무엇 | 언제 |
|---|---|---|
| ~~ⓐ~~ | ~~발사 위치~~ → ✅ **`EnemyData.projectileOrigin` = (0.9, 0.6)** (공격 f4 화살촉 실측). 발사 지점을 `ResolveSpawnPos` 한 곳으로 모았고 x 는 바라보는 쪽으로 뒤집는다. 🔴 곡사의 `up × 0.7` 은 안전장치라 폴백을 방식별로 나눴다 — Bug-048 | 2026-09-18 |
| ~~ⓑ~~ | ~~공격 동작 시간~~ → ✅ **windup 0.5 + recovery 0.6 = 1.1초**. 9프레임 → 8.18fps 라 **f4(활을 앞으로 겨눈 발사 프레임)가 0.49초** — 피해 시각과 그림이 맞는다. 쿨다운 1.8 안에 들어간다 | 2026-09-18 |
| ~~ⓒ~~ | ~~콜라이더~~ → ✅ **1.3x1.6** (활 제외 · 몸 약 42x52px). 그림 높이 83px 중 위 절반이 머리 위로 뻗은 활이다 | 2026-09-18 |
| ~~ⓓ~~ | ~~활 끝이 잘리는가~~ → ✅ 안 잘린다(밑여백 4 · 시트 칸 124 안에서 여유) · 「위를 겨눔」은 R2 에서 읽힌다 | 2026-09-18 |
| ~~ⓔ~~ | ~~땅에 박힌 화살 깜빡임~~ → 🔴 **Unity 검증에서 사용자가 지적** → ✅ **화살을 없앴다.** 후처리로는 몸과 안 갈려(recolor 가 채도를 낮춰 회백색 화살과 흙빛 몸이 붙는다) **시작 프레임에서 지우고 v3 로 다시 뽑았다**(7 gen) | 2026-09-18 |
| ⓕ | ⚠️ **목이 안 꺾였고 흰 눈 두 점이 남았다** — R1·R2 두 회차 다 문구가 안 먹었다. 정체의 절반(「위를 겨눔」)은 활이 맡고 있어 감수 가능한지 | 플레이에서 |

---

## 생성 기록

| 회차 | 도구 · 설정 | id | 판정 |
|---|---|---|---|
| R1 몸 | `create_character` **pro** · style `2fbf625f-f755-4d1d-a113-adadfdf97ce0` · size 92 · view side · 25 gen | `51e3f6b8-4ef9-44b8-821d-3c5916d7e4d1` | ❌ **하늘을 안 겨눈다** — 정체가 안 보인다 |

```
R1 문구 (주어를 「당긴 활」로 두고 자세를 뒤에 붙였다)
chunky stylized pixel art with an oversized head and upper body and short limbs, clean cel shading with 3 to 4 flat tones per material,
strong light and dark contrast, smooth surfaces without scratches or speckles; a drawn bow aimed straight up at the sky, held overhead in
both hands by a kneeling archer, the archer's upper body bent far backward and the neck cracked back so the face points up at the sky,
frozen in that aiming pose, worn faded earth-brown leather and cloth wrappings, a pale greyish-white arrow nocked on the taut bowstring,
two long pale greyish-white arrows stuck in the ground alongside with fletching pointing up, a dark empty face
```

### R1 실측 (SE · `_r1_compare.png` — 왼쪽부터 사수 R1 · 중장 강적 R2 · 근접 병사 R3(색 변환 전))

```
              폭   높이   채움   색   밑여백
근접 병사 R3    73    61   52.3%  37
중장 강적 R2    78    58   47.3%  28    15
원거리 사수 R1  59    75   44.7%  24    11    <- 높이 75 는 활 포함 · 밑여백이 앞 둘(15)보다 4px 낮다
```

| 문구 | R1 |
|---|---|
| chunky stylized · cel shading · strong contrast | ✅ 앞 둘과 같은 화풍 · 잡티 없음 · 24색 |
| 🔑 **faded earth-brown leather and cloth** | ✅ **가장 큰 성과** — 흙빛 가죽이 앞 둘의 건메탈과 확실히 갈린다. 「쇠의 벌판 것이 아니다」가 색으로 읽힌다 |
| kneeling | ✅ 한쪽 무릎이 땅에 · 서 있지 않다 |
| a drawn bow · taut bowstring | ✅ 활이 크고 또렷하다 · 시위가 당겨져 있다 |
| arrows stuck in the ground with fletching up | ✅ 오른쪽에 한 대 · 어깨 위로 화살 셋(화살통) |
| 🔴 **aimed straight up at the sky** | ❌ **활이 앞을 겨눈다**(위로 약 45°) — 「하늘을 보는 자세」가 없다 |
| 🔴 **upper body bent far backward · neck cracked back** | ❌ **상체가 앞으로 기울었다** — 정상 사격 자세다 |
| a dark empty face | ⚠️ 두건은 어둡지만 **흰 눈 두 점**이 있다 — 살아 있는 사람으로 읽힌다 |
| 도형 | ⚠️ 위로 뻗은 대각선이 아니라 **앞으로 겨눈 삼각** — 플레이어 폼의 삼각(투척)과 겹칠 수 있다 |

🔑 **중장 강적 R1 과 같은 실패다** — 정체 표지(창 / 하늘 겨눔)를 **문구 뒤쪽에 두면 안 그려진다.**
brute 는 주어를 「창」으로 바꿔 R2 에서 나왔다. 여기서도 주어를 **「위를 향한 활과 젖혀진 몸」**으로 바꾼다.

### R2 — 주어를 「위로 든 활」로 · 「하늘」을 기하 형태어로 (2026-09-18)

| 회차 | 도구 · 설정 | id | 판정 |
|---|---|---|---|
| R2 몸 | pro · style `2fbf625f-f755-4d1d-a113-adadfdf97ce0` · size 92 · view side · 25 gen | `11999797-4f13-4808-8228-294407898e7b` | ✅ 자세 합격 · 🔴 색이 플레이어 축 색으로 끌려감 → **recolor 로 해결** |

🔑 **「하늘」이라고 쓰지 않았다.** 투명 캔버스에는 하늘이 없다 — 공중 애니메이션에서 `midair` 가
발을 못 떼게 했던 것과 같은 함정이다(메모리 `project_pixellab_character_weapon`).
대신 **기하 형태어**로 썼다: `held vertically high overhead` · `bow limbs pointing straight up and straight down` ·
`the arrow pointing straight up`. **이게 먹었다.**

```
R2 문구
chunky stylized pixel art with an oversized head and upper body and short limbs, clean cel shading with 3 to 4 flat tones per material,
strong light and dark contrast, smooth surfaces without scratches or speckles; a large bow held vertically high overhead with both arms
stretched straight up, the bow limbs pointing straight up and straight down, the drawn bowstring and a pale greyish-white arrow pointing
straight up, the archer kneeling on both knees underneath with its spine arched far backward, the chin lifted so the face tilts up and back,
worn faded earth-brown leather and cloth wrappings, two long pale greyish-white arrows stuck in the ground beside the knees with the
fletching up, a hollow dark face with no eyes
```

### R2 실측 (SE · `_r2_compare.png` · 색 후보 `_r2_color_test.png`)

```
              폭   높이   채움   색   밑여백
중장 강적 R2    78    58   47.3%  28    15
사수 R1        59    75   44.7%  24    11
사수 R2        66    83   50.6%  36     4    <- 높이 83 은 활 포함 · 🔴 위 여백 5px
```

| 문구 | R1 | R2 |
|---|---|---|
| 🔑 **bow held vertically · arrow pointing straight up** | ❌ 앞을 겨눔 | ✅ **활이 세로로 서고 화살이 곧게 위를 향한다** — 「하늘을 보는 자세」가 한눈에 읽힌다 |
| kneeling on both knees | 한 무릎 | ✅ 두 무릎 |
| arrows stuck in the ground, fletching up | 한 대 | ✅ **좌우 두 대** · 깃이 위 |
| spine arched far backward | ❌ | ⚠️ 활을 들어 상체가 조금 젖혀졌지만 **척추가 꺾이지는 않았다** |
| 🔴 the chin lifted so the face tilts up | ❌ | ❌ **얼굴이 여전히 정면(SE)을 본다** — 목 꺾임은 두 회차 다 실패 |
| a hollow dark face with no eyes | ⚠️ 흰 눈 둘 | ⚠️ **흰 눈 둘 그대로** — 「눈 없음」은 두 회차 다 안 먹었다 |
| 🔴 **worn faded earth-brown leather** | ✅ 흙빛 | ❌ **붉은 갑옷 + 붉은 망토** — style 캐릭터(붉은 기사)로 끌려갔다. 색 36개 |

🔴 **R2 는 자세를 얻고 색을 잃었다.** 그리고 잃은 색이 하필 **플레이어 축 색(붉음)** 이라
`_ROSTER §1` 「적 색은 플레이어 축 색과 떨어뜨린다」에 정면으로 걸린다.

✅ **`recolor` 후처리로 해결한다 — 재생성 없이 gen 0.** 근접 병사 R3 가 같은 경로였다(붉음 → 건메탈 h200).
`build_form_sheets.py` 의 `recolor` 가 **번들 시트 전체에 한 번** 적용되어 모든 프레임이 같은 값을 받는다.
후보 4종을 `_r2_color_test.png` 에 구웠다 — 전부 붉음이 사라지고 흙빛이 된다.

| 후보 | hue · satScale · valScale | 인상 |
|---|---|---|
| A | 30° · 0.55 · 0.88 | 흙빛 — 중간 |
| B | 28° · 0.40 · 0.95 | 바랜 갈색 — 오래된 느낌 |
| C | 38° · 0.50 · 1.00 | 황토 — 밝고 또렷 |
| D | 22° · 0.28 · 0.92 | 회갈 — 가장 바램 · 건메탈에 가까움 |
