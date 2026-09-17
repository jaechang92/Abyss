# `melee_grunt` — 근접 병사 · 빈 갑옷

> 표시 이름 **근접 병사**(그대로) · 일반 · 배치 S1·S2·S3 **12방**(가장 많이 나온다)
> 작성 2026-09-17 · 목록: `_ROSTER.md`

---

## ① 정체

**빈 갑옷.** 끝나지 않는 전쟁의 세계에서 내려온, 사람이 들어 있지 않은 갑옷.
반쯤 묻힌 채 한 팔로 빈 땅을 내리치기를 반복하다가, **플레이어가 오면 비로소 상대가 생겨** 그쪽으로 기어 온다.

## ② 소설 근거 (1권 10장 「두 번째 세계」)

> *사람이 들어 있지 않은 갑옷들이 서 있거나 엎어져 있거나 반쯤 묻혀 있었다.*
> *투구의 눈구멍이 그를 보고 있었다. 안에는 아무것도 없었다.*
> *어깨 이음매에 붉은 가루가 쌓여 있었다.* — *이 벌판 전체가 부스러진 사람들이었다.*
> *가슴판이 열려 있고 한쪽 팔이 없는 갑옷이었다. 그것이 땅에 반쯤 묻힌 채, 남은 한 팔로 검을 들었다가 내리치고 있었다. 앞에는 아무도 없었다.*
> *검날이 다 닳아 있었다.* — 하란의 기록: ***싸울 상대가 없는데도 싸우고 있다.***

## ③ 시각 브리프

| | 배정 | 근거 |
|---|---|---|
| **자세** | **무릎을 꿇고 앞으로 크게 기운 낮은 자세** — 높이보다 너비가 크다 | 「적은 서 있지 않다」 · 반쯤 묻힌 갑옷 |
| **도형** | 가로로 긴 덩어리 (플레이어의 세로선 · 곡선 · 사각 · 삼각이 아님) | `08-silhouette §5` |
| **결손** | 가슴판이 열림 · **한쪽 팔 없음** · 투구 눈구멍 안이 빔 | 원문 그대로 |
| **무기** | 남은 팔에 **다 닳은 검** — 날이 짧고 무딤 | *검날이 다 닳아 있었다* |
| **색** | 무딘 쇠 회색 + **산화된 붉은 갈색 녹** · 이음매에 붉은 가루 · 발광 없음 | 녹 붉음 재료색 · 팔레트 발광 금지 |
| **크기** | 몸 높이 **44~56px**(웅크려서 일반 적 48~64 의 아래쪽) · 캔버스 92 | `08-silhouette §1` |

## ④ 변형 · 애니메이션

| 상태 | 동작 (멈춘 자세로 쓴다) |
|---|---|
| 이동 | 남은 팔과 무릎으로 **끌듯이 기어 온다** |
| 공격 | 남은 팔로 **닳은 검을 들어 내리친다**(원문의 반복 동작 그대로) |
| 피격 | 투구가 뒤로 젖혀지고 녹가루가 떨어진다 |
| 사망 | **부스러져 붉은 가루로 주저앉는다** — *이 벌판 전체가 부스러진 사람들이었다* |

## ⑤ 미결

| | 무엇 | 언제 |
|---|---|---|
| ~~ⓐ~~ | ~~웅크린 몸이 콜라이더(1x1)와 맞는가~~ → ✅ **1.8x1.5** (그림 73x61px ≈ 2.3x1.9 유닛 · `EnemyAnimationBuilder.Entries`) · 플레이 조정 여지 | 2026-09-18 |
| ~~ⓑ~~ | ~~적 재생 방식~~ → ✅ **Animator 재사용**(플레이어와 같은 층 · 사용자 결정) | 2026-09-18 |
| ⓒ | 가슴판 안 「빈 속」이 64px 급에서 읽히는가 — 안 읽히면 눈구멍 · 열린 가슴 중 하나만 강조 | 몸 판정 |

---

## 생성 기록

| 회차 | 도구 · 설정 | 문구 | id | 판정 |
|---|---|---|---|---|
| R1 몸 | `create_character` v3 · size 92 · view side · single color black outline · medium detail · **3 gen** | 아래 | `88900cf8-a87a-4727-8034-1e85cdd72cdf` | ⚠️ 사용자 판정 대기 |

### R1 실측 (SE · `characters/melee_grunt/_r1_compare.png`)

```
            폭   높이   채움
플레이어 기사  36    60    65.6%
빈 갑옷 R1    61    89    69.3%     <- 목표 높이 44~56 인데 캔버스를 꽉 채웠다
```

| 문구 | 결과 |
|---|---|
| empty · hollow dark inside | ✅ 열린 가슴판 안이 검게 비어 보인다 |
| rust · red rust powder | ✅ 녹 얼룩 · 화풍(검은 외곽선 · 음영)도 플레이어와 같다 |
| kneeling low | ✅ 무릎을 꿇었다 |
| 🔴 hunched far forward · wider than tall | ❌ **상체가 곧게 섰다** — 폭 61 < 높이 89. 「무릎 꿇은 기사」로 읽힌다 |
| 🔴 left arm missing | ❌ **두 팔이 다 있다** |
| 🔴 dull grey | ⚠️ 밝은 은색 하이라이트 — 플레이어보다 눈에 띈다 |
| 🔴 크기 | ❌ **v3 는 캔버스를 채운다.** 폼은 기준 캐릭터의 state 라 60px 이 유지됐지만, 새 캐릭터는 size 가 곧 몸 크기다 |

```
R1 문구
an empty suit of rusted iron armor with nobody inside, kneeling low and hunched far forward so its body is wider than it is tall,
the chest plate hanging open showing a hollow dark inside, the left arm missing at the shoulder, the right hand gripping a short worn-down dull sword resting on the ground,
a helm with empty dark eye slits, dull grey iron with oxidized red-brown rust and red rust powder in the joints
```

### R1 사용자 판정 (2026-09-17) — ❌ 「플레이어 아트풍과 너무 동떨어져 있다」

화풍 차이: 플레이어는 **머리(후드)가 키의 1/3 · 짧은 팔다리 · 적은 색 · 주황 테두리선 · 반사광 없음**,
R1 은 **사실적 비례 · 은빛 반사광 · 명암 단계 많음**. 원인 — 플레이어는 사용자가 웹에서 화풍 키워드로 만든 캐릭터이고
폼 3벌은 그 state 라 화풍을 물려받았다. **새 캐릭터(v3)는 참조가 없어 모델 기본 화풍**이 나온다.
→ 사용자 선택: **pro + `style_character_id = 2fbf625f`**(종당 20~40 gen).

| 회차 | 도구 · 설정 | id | 판정 |
|---|---|---|---|
| R2 몸 | `create_character` **pro** · style `2fbf625f` · size 92 · view side · **25 gen** | `0b367e3c-c36f-489b-a5b9-c7d719a28f7d` | ⚠️ 사용자 판정 대기 |

### R2 실측 (SE · `characters/melee_grunt/_r2_compare.png`)

```
            폭   높이   채움
플레이어 기사  36    60    65.6%
R1 (v3)      61    89    69.3%
R2 (pro)     78    66    46.1%    <- 폭 > 높이 · 서 있지 않다
```

| 문구 | R2 |
|---|---|
| crawling low on its belly and right arm | ✅ 배와 한 팔로 기는 자세 · 가로로 긴 몸 |
| empty left shoulder socket | ✅ 왼쪽 어깨 자리가 검게 뚫렸다 |
| dragging a short worn-down sword | ✅ |
| helm with empty dark eye slits | ✅ |
| dull dark iron with oxidized red rust | ✅ 반사광이 사라지고 탁한 녹 갈색 — 화풍이 플레이어 쪽으로 왔다 |
| chest plate hanging open, hollow inside | ⚠️ SE 에서는 가슴이 바닥을 향해 안 보인다(어깨 구멍이 「빈 속」을 대신 말한다) |
| 크기 | ⚠️ 높이 66 은 목표(44~56)보다 조금 크고, 폭 78 은 플레이어의 두 배 — 콜라이더(1x1 유닛)와 맞춰야 한다 |

```
R2 문구 (R1 의 안 먹은 셋을 긍정문으로)
an empty suit of rusted armor with nobody inside, crawling low along the ground on its belly and its right arm, dragging a short worn-down sword,
the chest plate hanging open with a hollow dark inside, an empty left shoulder socket with torn metal edges,
a helm with empty dark eye slits, dull dark iron with oxidized red rust
```

🔴 **예산 영향**: pro 가 12종 모두에 필요하면 몸만 240~480 gen(잔여 약 770). R2 결과를 보고 종마다 pro 를 쓸지 정한다.

### R2 사용자 판정 → 화풍 레퍼런스(스컬) 분석 (2026-09-17)

사용자가 스컬 추출 이미지를 **화풍(텍스처) 레퍼런스**로 줬다. ⚠️ 상업 게임 추출물이라 **분석만** 하고
프로젝트에 넣거나 생성 참조 이미지로 올리지 않는다(화풍 참조는 계속 플레이어 `2fbf625f`).

| | 스컬 | 플레이어 기사 | R2 |
|---|---|---|---|
| 비례 | 머리 · 상체 과장, 짧은 다리 | 후드가 커서 가까움 | 거의 사실적 |
| 명암 | **평평한 면 3~4단 · 단계 간 명도차 큼** | 깔끔 + 주황 테두리선 | 중간톤 많음 · **녹 잡티** |
| 색 | 1~2 계열 + **밝은 강조점 하나**(눈 등) · 25색 | 26색 | 갈색 한 계열 · 강조점 없음 · 36색 |

📌 색 수만 6~8 로 줄인 시험(gen 0)은 잡티만 줄고 **대비 · 강조점 부재**는 그대로였다 → 화풍은 **생성 단계에서** 넣는다.

| 회차 | 도구 · 설정 | id | 판정 |
|---|---|---|---|
| R3 몸 | pro · style `2fbf625f` · size 92 · **25 gen** | `bbdbcfd4-d587-40f4-8eaf-3c9b701f3ba3` | ✅ **채택**(사용자) — 화풍 만족 · 적 전체 화풍 기준 · 색은 후처리 **B 건메탈** |

R3 실측 (`characters/melee_grunt/_r3_compare.png`): 폭 73 · 높이 61 · 채움 52.3% · 37색

| 화풍 문구 | R3 |
|---|---|
| oversized helm · short limbs | ✅ **양동이 투구가 몸의 절반** — 스컬식 과장 비례 |
| cel shading 3~4 tones · strong contrast · no speckles | ✅ 면이 깔끔하고 잡티가 사라졌다 |
| bone-white only inside the eye slits | ✅ 눈구멍 속 뼈색 강조점이 또렷하다 |
| crawling low | ✅ 손을 짚고 엎드린 낮은 자세 |
| 🔴 empty left shoulder socket | ❌ 두 팔이 다 있다(R2 에서 먹었던 것이 화풍 문구에 밀렸다) |
| 🔴 「빈 갑옷」 · 섬뜩함 | ⚠️ 둥글고 귀여운 꼬마 기사로 읽힌다 — 속이 빈 느낌이 없다 |
| 🔴 색 | ⚠️ **붉은 갈색이 플레이어 암흑 검사(붉음)와 가깝다** — 축 색 충돌 |

```
R3 문구 (화풍 어휘를 맨 앞에)
chunky stylized pixel art with an oversized helm and upper body and short limbs, clean cel shading with 3 to 4 flat tones per material,
strong light and dark contrast, smooth surfaces without scratches or speckles; an empty suit of rusted armor with nobody inside,
crawling low along the ground on its belly and its right arm, dragging a short worn-down sword, an empty left shoulder socket with torn edges,
a helm with empty eye slits, dark rust red-brown iron, pale bone-white only inside the eye slits and around the torn shoulder edge
```
강조점 = **흐린 뼈색** — 플레이어 축 색(붉음 · 보라 · 금 · 주황)과 안 겹치고 「종이색 빈 면」 재료색과 맞는다. 발광 없음.

### ✅ R3 채택 + 색 후처리 B (2026-09-17 사용자)

R3 원본의 붉은 갈색이 플레이어 암흑 검사(붉음)와 가까워 **색만 후처리**로 돌렸다(gen 0). 시안 셋(`characters/melee_grunt/_r3_color_test.png`):
A 회청색 쇠 · **B 건메탈(채택)** · C 탁한 황갈 녹. 🔑 B 는 「재 검정」 재료색에 가깝고 **뼈색 강조점이 가장 또렷하다**.

```
색 변환 B (모든 프레임에 같은 값으로 — 애니메이션은 R3 원본으로 뽑고 받은 뒤 적용)
대상     hue < 0.12 또는 > 0.92 (붉음~주황)인 픽셀만
보존     채도 < 0.25 이면서 명도 > 0.6 (뼈색 강조점) · 명도 < 0.12 (외곽선)
변환     hue = 200° · 채도 × 0.18 · 명도 × 0.85
```

⚠️ 손으로 적은 식이다 — 시트 조립 도구의 레시피 옵션으로 옮길 것(프레임마다 같은 값이 보장되게).
📌 어깨 결손 · 「빈 속」은 R3 에서 사라진 채로 **감수**했다(사용자 채택). 사망 애니메이션의 부스러짐이 「빈 갑옷」을 말한다.
