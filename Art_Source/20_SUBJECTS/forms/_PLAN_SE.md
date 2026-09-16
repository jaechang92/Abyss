# 폼 3벌 — south-east 제작 계획 (2026-09-16)

> 대상: `ancient_shield` · `void_archer` · `void_thrower`. `dark_blade`(붉은 기사)는 SE 9상태가 이미 있다.
> 규약: `10_BIBLE/08-silhouette.md` §1 · §1-C · §4 · 도구: `40_TOOLS/pixellab/character.md` §7
> 문구 기록: 폼마다 `Art_Source/characters/<formId>/animation_prompts.md` (knight_red 형식)

---

## 0. 착수 전에 확인한 것 (gen 0)

### 계정 자산은 셋 다 그대로 못 쓴다

| 자산 | SE 에서 | 판정 |
|---|---|---|
| `2fbf625f` 붉은 No Weapon | 후드 그림자 · 무기 없음 | ✅ **파생 소스** |
| `150cff52` · `aae5ce55` 금색 방패 | 🔴 **SE 에서 맨얼굴이 보인다** · 방패가 구워져 있다 | ❌ |
| `f30ebb51` 검정 투척사 | 🔴 **SE 회전이 뒷모습**이다 · 단검이 구워져 있다 | ❌ |
| `93262325` 보라 궁수 | 다른 그룹 · `view=high top-down` · 활이 구워져 있다 | ❌ 같은 인물 규약 밖 |

🔑 셋 다 **무기가 몸에 구워진 시절**(2026-09-10) 것이다. 무기 분리(09-14) 뒤로 몸은 **무기 없는 state** 여야 한다.
→ **`2fbf625f` 에서 state 로 새로 파생한다.** 옛 state 는 지우지 않는다(비교용·되돌릴 때).

### 🔴 SE 기준선 — 이미 east 보다 나쁘다

`measure_silhouette.py --height 64`, 기존 SE 회전 이미지 4장(무기 구워진 상태):

```
            채움     vs 붉은몸  vs 방패  vs 투척  vs 궁수
붉은 몸     66.0%      —        0.63     0.71     0.45
방패(금)    70.0%     0.63       —       0.74     0.55
투척(검정)  55.4%     0.71      0.74      —       0.53
궁수(보라)  55.6%     0.45      0.55     0.53      —

범위 0.45 ~ 0.74     (east 옛 기준 0.49 ~ 0.58)
```

- 3/4 은 몸이 정면에 가까워 **무기를 뺀 몸끼리는 더 닮는다** — 붉은 몸이 무기 없이 채움 66%
- **방패 vs 투척 0.74 가 최악**이다. east 에서도 최악의 쌍(0.58)이었다 — 둘을 **같이** 판정한다
- 🔑 그래서 **몸이 스스로 형태를 가져야 한다.** 도형은 무기가 만들지만(소켓이 값으로 강제),
  §1-C 「무기가 바뀌어도 폼이 읽혀야 한다」를 지키려면 **옷·부피·밑단**이 몸에서 갈려야 한다

---

## 1. 몸의 차이 — 무기 말고 무엇으로 가르나

`08-silhouette §4` 처방(밑단 · 밀도=무게)을 SE 몸에 옮긴다. **무기는 안 적는다 — 두 손은 비어 있다.**

| 폼 | 색 | 몸이 만드는 형태 | 채움 목표(무게 순) |
|---|---|---|---|
| 방패 | 금색 | **넓은 어깨 · 두꺼운 판금 · 엉덩이에서 고르게 끊긴 갑주 치마** — 덩어리 | 가장 높다 |
| 검사 | 붉음 | (현행 `2fbf625f`) | 둘째 |
| 투척 | 검정 | **좁은 어깨 → 발목에서 넓게 퍼지는 망토** — 아래만 넓은 삼각 | 셋째 |
| 궁수 | 보라 | **몸에 붙는 가벼운 가죽 · 짧은 밑단 · 등의 화살통** — 가늘고 세로로 긴 몸 | 가장 낮다 |

⚠️ **투척의 삼각은 아래만 넓어야 한다** — 위까지 넓으면 방패 덩어리에 붙는다(0.74 의 원인).
⚠️ **궁수의 화살통은 몸의 일부다**(무기 아님) — 활을 바꿔도 남으므로 §1-C 에 맞는다.

### 문구 초안 (`create_character_state` · 소스 `2fbf625f`)

규칙(`animation_prompts.md`): **긍정문 · 정체성 어휘 앞에 · 「하지 마라」 대신 「무엇이 보이나」.**
🔴 `use_color_palette_from_reference=false` — 새 색을 넣으므로 켜면 붉음으로 스냅된다.

```
방패  golden yellow heavy plate armor, hood up with the face hidden in deep black shadow,
      broad rounded pauldrons, thick plated chest, an armored skirt ending evenly at the hips,
      a wide solid stance, both hands empty and open

궁수  purple light leather armor fitted close to the body, hood up with the face hidden in deep
      black shadow, a quiver of arrows strapped across the back, a short hem ending above the knees,
      slim and upright, both hands empty and open

투척  charcoal black hooded cloak, hood up with the face hidden in deep black shadow, narrow at the
      shoulders and flaring out wide into a broad hem at the ankles, light armor underneath,
      both hands empty and open
```

📌 방패 문구의 `face hidden in deep black shadow` 는 **금색 state 에서 맨얼굴이 나온 것**에 대한 처방이다.

### R1 호출 (2026-09-16 · 위 문구 그대로 · 팔레트 스냅 끔)

| 폼 | state_name | id |
|---|---|---|
| 방패 | `Shield Body` | `cb29cabb-ff10-4d30-9e17-e7d1cf375550` |
| 궁수 | `Archer Body` | `38f6e24b-6f1b-4eff-be72-bf8c998c9060` |
| 투척 | `Thrower Body` | `1b0ce390-693c-4c31-bb00-a265abb4041e` |

### R1 결과 (2026-09-16 · 60 gen — 셋 다 하한 20)

```
           몸만(SE)                    몸+무기(Idle f0 앵커 · 각도 0 · WeaponSocket 규약 재현)
           채움    vs검  vs방패 vs투척   채움    vs검  vs방패 vs투척
검(붉음)   66.0%    —    0.83   0.84    46.3%    —    0.56   0.63
방패(금)   68.7%  0.83    —     0.77    66.6%  0.56    —     0.76
투척(검정) 65.7%  0.84  0.77     —      52.3%  0.63  0.76     —
궁수(보라) 57.5%  0.72  0.77   0.64     49.3%  0.52  0.65   0.64

범위       몸만 0.64 ~ 0.84            몸+무기 0.52 ~ 0.76     (기존 SE 자산 0.45 ~ 0.74)
```

| 폼 | 눈 판정 | 문구가 먹었나 |
|---|---|---|
| 공통 | ✅ **얼굴 셋 다 안 보인다** · 같은 체격 · SE 정상 회전 | 금색 state 의 맨얼굴 문제 해소 |
| 궁수 | ✅ 가는 몸 · 등의 화살통 · 짧은 밑단 | ✅ 전부 |
| 방패 | ⚠️ 금색 판금·둥근 견갑은 나왔다. 🔴 **후드와 망토가 붉은색 그대로** · 부피가 검사와 거의 같다(채움 68.7 vs 66.0) | 색 절반 · 부피 ❌ |
| 투척 | 🔴 **망토가 곧게 떨어진다** — 발목에서 안 퍼졌다. 09-10 과 같은 자리(`flaring wide` 가 안 먹음) | 색 ✅ · 삼각 ❌ |

- ✅ **몸만 채움이 「밀도 = 무게」 순서와 맞는다** — 방패 68.7 > 검 66.0 > 투척 65.7 > 궁수 57.5
- 🔴 **몸만 IoU 가 0.64~0.84 — 예상대로 SE 몸은 크게 닮는다.** 가장 붙는 것은 검vs투척 0.84(망토가 안 퍼져서)
- 🔴 **몸+무기에서도 방패vs투척 0.76 이 최악**으로 남았다 — east(0.58) · 기존 SE(0.74) 와 같은 쌍
- 💡 무기를 얹으면 전 쌍이 내려간다(평균 0.77 → 0.63). 도형은 여전히 무기가 만든다

**사용자 판정**: 궁수 채택 · 방패 재추첨 · 투척 문구 바꿔 재추첨.

### R1' 재추첨 (2026-09-17) — 🔑 R1 몸을 소스로 **한 곳만** 바꾼다(W6)

```
방패 v2  소스 cb29cabb · 팔레트 스냅 끔(새 색)   -> 2de47574-d60c-4f47-bd0d-0204065ed52a
  hood up with the face hidden in deep black shadow, the hood and the cape recolored to the same
  golden yellow as the armor, a bulkier heavier build with thick layered golden plates and massive
  broad pauldrons reaching wider than the hood, both hands empty and open

투척 v2  소스 1b0ce390 · 팔레트 스냅 켬(색 유지) -> 0f43556f-6e97-4406-929d-eb79928070eb
  hood up with the face hidden in deep black shadow, the charcoal black cloak shaped like a long wide
  bell, narrow at the shoulders and its hem resting on the ground in a broad circle around the feet,
  twice as wide as the shoulders at the bottom, both hands empty and open
```

📌 투척 문구는 **동작어(`flaring out`)를 버리고 형태어(`bell` · `resting on the ground` · `twice as wide`)**로 바꿨다.
`flaring wide` 가 09-10 · R1 두 번 안 먹었다 — 「멈춘 자세로 쓴다」(`animation_prompts.md` ②)의 옷 판본이다.

### R1' 결과 (2026-09-17 · 40 gen — 둘 다 하한 20 · 누적 100 · 잔여 927)

```
           몸만(SE)                          몸+무기
           폭    채움    vs검  vs방패 vs투척   채움    vs검  vs방패 vs투척
검(붉음)   38   66.0%     —    0.66   0.73    46.3%    —    0.55   0.53
방패 v2    48   65.2%   0.66    —     0.65    69.8%  0.55    —     0.71
투척 v2    51   59.9%   0.73  0.65     —      56.0%  0.53  0.71     —
궁수       40   57.5%   0.72  0.65   0.62     49.3%  0.52  0.61   0.57

범위   몸만 0.64~0.84 -> 0.62~0.73      몸+무기 0.52~0.76 -> 0.52~0.71
```

| 폼 | 눈 판정 |
|---|---|
| 방패 v2 | ✅ 후드·망토까지 금색 · 견갑이 후드보다 넓다(폭 39 -> 48). 검사와 0.83 -> **0.66** |
| 투척 v2 | ✅ **종 모양 망토로 아래만 넓은 삼각이 섰다**(폭 42 -> 51). 검사와 0.84 -> **0.73**. ⚠️ 밑단이 바닥에 닿아 **발이 안 보이고 밑단 아래가 그림자처럼 어둡다** |

- ⚠️ **몸만 채움에서 방패(65.2)가 검(66.0) 아래로 내려갔다** — 폭이 넓어지며 빈 곳이 늘었다. 무기를 얹으면 방패 69.8 로 최고다
- 🔴 **방패 vs 투척 0.71 은 여전히 최악의 쌍** — 방패가 넓어지고 투척 밑단도 넓어져 둘 다 「넓은 덩어리」 쪽으로 갔다
- ⚠️ **투척 종 망토가 R2 에서 문제가 될 수 있다** — 발이 안 보이면 달리기·점프 다리 동작이 안 읽힌다

### ✅ R1 확정 (2026-09-17 사용자 판정) — R2 는 이 셋으로

| 폼 | 채택 몸 | 비고 |
|---|---|---|
| 방패 `ancient_shield` | **`2de47574-d60c-4f47-bd0d-0204065ed52a`** (Shield Body v2) | |
| 궁수 `void_archer` | **`38f6e24b-6f1b-4eff-be72-bf8c998c9060`** (Archer Body) | |
| 투척 `void_thrower` | **`0f43556f-6e97-4406-929d-eb79928070eb`** (Thrower Body v2) | ⚠️ 종 망토 그대로 채택 — 다리가 안 읽히면 **애니메이션을 다시 만든다**(사용자 감수) |

폐기(지우지 않음): `cb29cabb` Shield Body v1 · `1b0ce390` Thrower Body v1.

---

## 2. 라운드와 비용

| 라운드 | 무엇 | 비용(gen) | 끝나면 |
|---|---|---|---|
| **R1 몸** | state 3개 | 3 × 20~40 = **60~120** | SE 회전 이미지로 ① 몸만 IoU · 채움 순서 · 얼굴 · 방향 → **사용자 판정** |
| R1' 재추첨 | 탈락한 것만 | 20~40 / 개 | |
| R1+ 몸+무기 | 소켓 규약대로 무기를 얹어 합성(`mount_weapon.py`) | **0** | ② 몸+무기 IoU |
| **R2 애니메이션** | 폼당 9상태 v3 (2 gen/상태) + 공중·대시 2단 + 재추첨 여유 | 폼당 ~35 → **~105** | 발밑 정렬 · 공중 짝 판정 |
| R3 Unity | 시트 임포트 · 손 앵커 · 오버라이드 컨트롤러 · `FormData` 배선 | **0** | 플레이 확인 |

```
합계 약 165 ~ 225 gen     잔여 1027 (2026-09-16 · 리셋 2026-10-02)
```

🔴 **R1 이 끝나면 멈춘다.** 몸이 안 갈리면 R2 의 105 gen 이 전부 다시 든다 —
「한 장을 잘 뽑는다」 공정이라(`character.md` §7) 판정을 R2 앞에 둔다.

---

## 3. 미결

| | 어디서 |
|---|---|
| SE 에서 몸만으로 **얼마나** 갈려야 하나 — 합격선은 여전히 없다. 기준선 0.45~0.74 보다 내려가는지를 본다 | R1 판정 |
| 방패 치마·투척 망토가 **무기 소켓 자리**(손·허리)를 가리는가 | R1+ 합성 |
| 궁수 화살통이 **flip** 에서 어색한가(등 → 반대편) | R3 플레이 |
| 옛 state 6개 정리(삭제) — 지금은 남긴다 | R3 이후 |
