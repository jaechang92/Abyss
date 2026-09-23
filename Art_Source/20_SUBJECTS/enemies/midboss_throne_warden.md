# `midboss_throne_warden` — 왕좌의 파수관 · 아무도 앉지 못하게 하는 것

> 표시 이름 **왕좌의 파수관**(그대로) · **중간보스** · 배치 **S3** `Stage3_Room4_Alt_Guard` **2파**(파수관 1 + 중장 강적 2) ·
> 1파는 엘리트 사냥꾼 1 + 원거리 사수 2
> 데이터 HP **300** · 피해 34 · 속도 3.0 · 사거리 **2.2** · 쿨다운 1.3 · `tier MidBoss`
> 🔴 **패턴 클래스를 감시자와 공유한다** — `MidBossSentinelBoss`(회전베기, `PrefabBuilder.Enemies.cs:187`).
> **동작이 같으므로 형태로 갈라야 한다.** 감시자는 가로로 넓다(103x89 · 폭/높이 1.16)
> 🔴 **프리팹 확대 1.45 를 이 종을 넣을 때 뺀다**(`PrefabBuilder.Enemies.cs:85` — 감시자·정예는 이미 뺐다)
> 작성 2026-09-23 · 목록: `_ROSTER.md` · 화풍 기준: `melee_grunt.md` R3 · 직전 종: `midboss_sentinel.md`

---

## ① 정체

**빈 자리 그 자체.** 아무도 앉지 못하게 하는 것.

- **보스(왕좌의 영혼) = 그 자리에 앉았던 것 / 파수관 = 그 자리를 지키는 것.**
- 형태(사용자 결정): **빈 옥좌가 일어선 것.** 높고 좁은 등받이가 몸통이고, 낮고 넓은 받침이 아래를 깔고 있다.

## ② 근거 — 게임 설정 + 2권 26장 「지붕 위의 의자」 (사용자 결정)

「왕좌」는 **게임 쪽 설정**이다 — S3 = 「왕좌의 잔해」(`StageBuilder.cs:39`) · 보스 「왕좌의 영혼」 ·
엔딩 *"무너진 왕좌"*. **소설 3권 전체에 「왕좌」는 0회**다.

어휘는 2권 26장에서 빌린다:
- 지붕 가운데 의자 하나 · 오래 앉아 **한쪽만 닳은 자리**
- *"이 의자에 앉으면 남은 게 다 보인다"*
- 의자 발치에서 지붕 끝까지 **획**(열둘씩 · 돌 열네 장)
- 🔑 하란은 **앉지 않았다**(*"앉으면 안 될 것 같다"*)

📌 **나무 의자는 어휘만 빌린다** — 재료는 S3 규약(쇠 · 녹 붉음)을 따른다(`_ROSTER §1` 재료색 셋).

### 🔴 `_ROSTER` 의 옛 항목은 틀렸다 (2026-09-22 확인 · 2026-09-23 정정)

| # | 무엇이 틀렸나 |
|---|---|
| 1 | 근거 「1권 26장 도시 노릇 하는 것」은 **`elite_hunter` 가 이미 가져갔다**(`elite_hunter.md` ② 분담표) |
| 2 | 26장 끝~27장의 **「성문 앞에 선 자」는 오르곤**이고 **이미 플레이어 폼 `ancient_shield`** 다(`story-narrative.md:270` · `GameText.csv:72`). 적으로 쓸 수 없다 |
| 3 | 「문짝이 가슴 · 기둥이 팔 · 건물이 일어선 몸」은 **1권 26·27장에 한 번도 안 나온다** — 지어낸 것 |
| 4 | 「왕좌」는 게임 설정이다(위). 소설 근거로 적으면 안 된다 |

## ③ 시각 브리프

- 높고 좁은 등받이 = 몸통 · 가운데에서 좌우로 뻗은 짧고 굵은 팔걸이 둘 · 넓고 낮은 계단식 돌 받침
- **위보다 바닥에서 훨씬 넓다** — 감시자(가로 넓음 · 최하단 폭비 0.19)와 엘리트(좁고 가늚)를 동시에 피하는 실루엣
- 좌석은 **비어 있다** · 녹슨 붉음 + 어두운 쇠 · **발광 없음**
- 강조점은 **하나** — 좌석 위 오래 기대어 닳은 매끈한 자리

### 🔑 세션 14 교훈을 문구에 적용

- `standing` · `legs` · `feet` · `head` **0회** — 전제를 만드는 어휘는 부정문으로도 안 쓴다
- 지표 2 를 **배수가 아니라 형태어**로 박는다(`far wider at the ground than at the top`)
- 화풍 문구의 `an oversized head` → `an oversized upper body` 로 축소

## ④ 미결

- `spinRadius 2.5`(80px)에 폭이 더 못 미친다(폭 ≤63px = 반경 1.0유닛).
  감시자에서 「예고 링이 메운다」가 플레이로 확인됐으므로 **같은 판단으로 넘긴다.**

## ⑤ R1 판정 지표 (기존 8종 실측을 먼저 걸었다)

```
종             폭/높이   최하단15%폭비
근접 병사        1.20      0.48
중장 강적        1.34      0.32
원거리 사수 ⚠️   0.80      0.60     (같은 방 1파)
뼈 궁수         1.16      0.57
공허 술사        0.83      0.49
화염 박격포      1.45      0.80
엘리트 사냥꾼 ⚠️ 0.58      0.25     (같은 방 1파)
감시자 거인 ⚠️   1.16      0.19     (같은 동작)
```

| # | 지표 | 합격 | 비고 |
|---|---|---|---|
| 1 | **폭/높이** | **≤ 0.70** | 사수 0.80 · 공허 술사 0.83 을 피한다 |
| 2 | 🔑 **최하단 15% 평균폭 / 최대폭** | **≥ 0.85** | 8종 최고가 0.80 · 엘리트(0.25)와 3.4배 갈린다 |
| 3 | 높이 | **90px 이상** | |
| 4 | 실픽셀 | **2800 이상** | |
| 5 | 밝은 면 `>=99A6AC` | **3~12%** | |
| 6 | 연결 덩어리 | **1** | |
| 7 | 칸 148 안 | ≤148 | |

**1·2·6·7 중 하나라도 떨어지면 R2.**
🔑 **1·2 는 조합으로만 성립한다** — 단독으로는 둘 다 기존 표본과 겹친다.
🔴 지표는 네 번 틀린 적이 있다(감시자) — **통과해도 눈으로 본다.**

## 생성 기록

### R1 (pro · style `2fbf625f-f755-4d1d-a113-adadfdf97ce0` · size 128 · view side · 40 gen)

```
chunky stylized pixel art with an oversized upper body and short limbs, clean cel shading with 3 to 4 flat
tones per material, strong light and dark contrast, smooth surfaces without scratches or speckles; a heavy
empty iron throne risen upright on its own, its tall narrow high back forming the body, two short thick
armrests reaching out to the left and to the right from the middle of it, and a wide low stepped stone base
spreading out along the ground beneath it so that it is far wider at the ground than at the top, the seat
empty, the armour plating a dull matte oxidised rust red and dark iron with no glow, one small worn smooth
patch on the seat where something leaned for a very long time as the only bright thing on it, all one single
connected body with nothing floating free of it
```

- 2026-09-22 세션 14: 요청이 연결 오류로 끊겼다(유럽 AWS 리전 경로 불통).
- 🔴 2026-09-23 **문구 한 곳 정정**: 인수인계본 문구에 `standing`("high back standing as the body")이 남아 있었다 —
  ③의 「`standing` 0회」와 어긋난다. `forming` 으로 바꿨다. 나머지는 확정 문구 그대로.
- 2026-09-23: 잔여 103 · 대기열 비어 있음 확인 → **큐에 안 들어갔다.** 경로 복구(HTTP 307) 후 재요청 → **id `4124117f-ac80-4e3e-990c-70a10d2ecfee`** (40 gen · 잔여 103 → 63).

### R1 실측 (2026-09-23) — 형태는 문구대로 나왔다. 🔴 **그런데 「적」이 아니라 「가구」로 읽힌다**

| 방향 | 크기 | 폭/높이 (≤0.70) | 실픽셀 (≥2800) | 밝은 면 (3~12%) | 덩어리 (1) |
|---|---|---|---|---|---|
| east (게임 측면) | 88x109 | ❌ **0.81** | ✅ 3627 | ❌ 0.0% | ✅ 1 |
| south-east | 110x112 | ❌ 0.98 | ✅ 6418 | ❌ 0.0% | ✅ 1 |

- 🔴 **지표 2(최하단 폭비)는 판정에서 뺐다** — 같은 계산을 감시자 R4 · 엘리트 R4 에 대 보니
  폭/높이는 기준값(1.16 · 0.58)과 맞는데 **최하단 폭비는 0.19 · 0.25 를 재현하지 못했다**(행 폭 0.58 · 0.42 / 행 픽셀수 0.29 · 0.27).
  기준 8종을 어느 이미지·어떤 식으로 쟀는지 기록이 없다. 👉 **기준 표를 다시 재기 전에는 이 지표를 쓰지 않는다**
- ✅ 문구가 요구한 것은 전부 나왔다 — 높고 좁은 등받이 · 짧은 팔걸이 · 계단식 넓은 받침 · 빈 좌석 · 닳은 자리 하나 · 한 덩어리
- 🔴 **닳은 자리가 주황으로 빛난다** — `no glow` 를 어겼다(밝은 면 0% 는 그 점이 무채 기준 `>=99A6AC` 밖이라서다)
- 🔴 **east(측면)은 「ㄴ」자 의자 옆모습** — 등받이 기둥 하나 + 좌석. 몸통이 아니라 막대로 읽힌다
- 🔴 **가장 큰 문제: 살아 있다는 신호가 하나도 없다.** 방 한가운데 놓여 있으면 배경 소품이다.
  문구의 `risen upright on its own` 은 형태가 아니라 사연이라 그림에 안 남았다
- ⚠️ 이 몸에 회전베기 애니메이션(`animate_character` · 사람형 뼈대)을 입히기 어려울 수 있다 — 팔도 날도 없다

📌 **판정은 사용자 결정 대기.** R2 로 가면 「살아 있다는 신호」(예: 좌석의 빈 자리를 메운 것 · 등받이에서 뻗은 날 · 기울어짐)를 무엇으로 줄지 먼저 정한다.
📌 판정 이미지: `Art_Source/characters/midboss_throne_warden/_r1_compare.png`(왼쪽부터 R1 east · R1 south-east · 감시자 R4 · 엘리트 R4, 3배)

### R2 (2026-09-23 · 사용자 지시 「앉은 보스형 / 의자 자체가 심연의 괴물」 중 택1 → **의자 자체가 괴물**) · id `a0bb1075-7b3b-4187-97e6-522e323b5b14` · 40 gen(잔여 23)

🔑 **왜 「앉은 것」이 아닌가** — 앉았던 것은 보스(왕좌의 영혼)다. 파수관의 정체는 **빈 자리**라 좌석은 비어야 한다.
회전베기를 하려면 휘두를 것이 있어야 해서 **팔걸이를 날 달린 팔로 키웠다.**

```
chunky stylized pixel art with an oversized upper body and short limbs, clean cel shading with 3 to 4 flat
tones per material, strong light and dark contrast, smooth surfaces without scratches or speckles; a monstrous
living iron throne from the abyss, its tall narrow high back bent forward over the seat like a hunched spine
with a row of jagged iron ridges along the top edge, the empty seat split open into a deep dark hollow maw
lined with uneven iron teeth, its two armrests grown into two long thick iron arms ending in heavy flat blades
held low close to its sides with the blade tips pointing down, a wide low stepped stone base spreading out
along the ground beneath it so that it is far wider at the ground than at the top, dull matte oxidised rust
red and dark iron with no glow and no light inside the maw, one pale grey worn stripe along the edge of each
blade as the only bright thing on it, all one single connected body with nothing floating free of it
```

| 방향 | 크기 | 폭/높이 (≤0.70) | 실픽셀 | 밝은 면 (3~12%) | 덩어리 |
|---|---|---|---|---|---|
| east | 82x113 | ⚠️ **0.73** (R1 0.81) | ✅ 4805 | ❌ 1.5% | ✅ 1 |
| south-east | 104x115 | 0.90 | ✅ 6179 | ❌ 0.0% | ✅ 1 |

- ✅ **살아 있다** — 굽은 등뼈 · 톱니 볏 · 좌석이 이빨 아가리 · 날 팔 둘. R1 의 「가구」 문제는 풀렸다
- ✅ 아가리 안이 빛나지 않는다 · 한 덩어리 · 계단 받침이 아래를 깐다
- 🔴 **재질이 쇠가 아니라 살이다** — 근육 팔 · 선명한 빨강. `dull matte oxidised rust` 를 안 따랐다.
  S3 재료 규약(쇠 · 녹 붉음)과 어긋나고, 🔴 **감시자 R1 이 「플레이어 축 색과 겹침」으로 떨어진 전례**가 있다 — 채도가 그쪽이다
- ⚠️ 날 팔이 감시자와 같은 「아래로 기운 긴 날 둘」이다 — 같은 회전베기라 동작이 겹치는 것은 정해진 것이고, 몸 형태(세로 척추 + 넓은 받침)로는 갈린다
- 📌 색은 **후처리(gen 0)로 내릴 수 있다**(공허 술사 desaturate 전례 · `sheet_color`). 형태를 살리고 색만 고치는 길이 있다

📌 판정 이미지: `_r2_compare.png`(왼쪽부터 R2 east · R2 south-east · R1 south-east · 감시자 R4, 3배). **사용자 판정 대기.**
