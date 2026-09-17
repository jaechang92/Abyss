# `melee_brute` — 중장 강적 · 녹으로 붙은 갑옷 덩어리

> 표시 이름 **중장 강적**(그대로) · 일반 · 배치 S1·S2·S3 **11방** · 데이터 HP 60 · 피해 18 · 속도 2.5 · 사거리 1.5 · 쿨다운 2
> 작성 2026-09-18 · 목록: `_ROSTER.md` · 화풍 기준: `melee_grunt.md` R3

---

## ① 정체

**찌른 자세 그대로 굳어 녹으로 이어 붙은 갑옷 두 벌.** 쇠의 벌판에서 서로를 찌르다 멈춘 둘이 하나의 덩어리가 됐다.
혼자서는 못 움직여 **둘이 엇박으로 몸을 끌며** 느리게 온다. 공격은 **덩어리째 앞으로 무너져 덮친다.**

## ② 소설 근거 (1권 10장 「두 번째 세계」)

> *어떤 것은 창을 들고 있었고 어떤 것은 방패를 들고 있었다.*
> *어떤 것은 서로 붙어 있었다 — 하나가 다른 하나를 찌른 자세 그대로 굳어서.*
> ***녹이 그들을 이어 붙여 놓았다.***

## ③ 시각 브리프

| | 배정 | 근거 |
|---|---|---|
| **도형** | **둥근 덩어리** — 근접 병사(가로로 낮게 긴 몸)와 갈린다. 폭 ≥ 높이 | `08-silhouette §5` · 「적은 서 있지 않다」 |
| **자세** | 앞의 갑옷은 무릎이 꺾여 주저앉고, 뒤의 갑옷이 **창으로 앞 갑옷의 가슴을 꿰뚫은 채** 등에 엎어져 있다 | *찌른 자세 그대로* |
| **정체 표지** | ① 몸을 관통한 **긴 창 한 자루**(양쪽으로 튀어나옴) ② **투구 둘** ③ 이음매마다 **두꺼운 녹 딱지** | 원문 셋 그대로 |
| **색** | 근접 병사와 같은 **어두운 건메탈 쇠**(계통 ① 공유) + **녹은 붙은 이음매에만** 탁한 붉은 갈색 — 녹이 곧 「이어 붙인 것」 | 녹 붉음 재료색 · 플레이어 암흑 검사(선명한 붉음)와는 채도로 갈림 |
| **강조점** | 투구 눈구멍 속 **흐린 뼈색**(근접 병사와 같은 계통 표지) | `_ROSTER` 강조점 규칙 |
| **크기** | 몸 높이 **56~64px**(일반 상한 · 근접 병사 61보다 크게 읽히도록 폭으로) · 캔버스 92 | `08-silhouette §1` |

## ④ 애니메이션 (멈춘 자세로 쓴다)

| 상태 | 동작 |
|---|---|
| 이동 | 두 몸이 **엇갈려 한 쪽씩 끌며** 덩어리가 앞으로 기울었다 돌아온다 — 느림 |
| 공격 | 뒤의 갑옷이 **몸을 들어 올렸다가 덩어리째 앞으로 무너지며 덮친다**(창끝이 앞으로) |
| 피격 | 덩어리가 뒤로 흔들리고 이음매에서 녹가루가 떨어진다 |
| 사망 | **녹 이음매가 부서져 두 갑옷이 떨어지며** 따로따로 무너진다 — *녹이 이어 붙여 놓았다*의 역 |

## ⑤ 미결

| | 무엇 | 언제 |
|---|---|---|
| ~~ⓐ~~ | ~~투구 둘 · 창이 92 캔버스에서 읽히는가~~ → ✅ R2 에서 둘 다 읽힘 | 2026-09-18 |
| ~~ⓑ~~ | ~~공격 동작 시간~~ → ✅ windup 0.8 + recovery 0.4 (f6 창끝이 땅에 닿는 순간 = 0.8초) | 2026-09-18 |
| ~~ⓒ~~ | ~~콜라이더~~ → ✅ 1.5x1.6 (창 제외 · 몸 약 43x58px) | 2026-09-18 |

---

## 생성 기록

| 회차 | 도구 · 설정 | id | 판정 |
|---|---|---|---|
| R1 몸 | `create_character` **pro** · style `2fbf625f-f755-4d1d-a113-adadfdf97ce0` · size 92 · view side · 25 gen | `574e2acc-25ed-437f-8329-2999419a975b` | ❌ 사용자 「R2 다시 생성」 — 창 없음 · 서 있음 |

> ⚠️ 첫 요청 `31b68674` 는 **style ID 뒷부분을 잘못 적어** `Style character not found` 로 실패했다(gen 차감 없음).
> 문서에는 앞 8자리만 적혀 있는 곳이 많다 — **전체 ID 는 `2fbf625f-f755-4d1d-a113-adadfdf97ce0`**.

```
R1 문구 (화풍 문구 맨 앞 · 정체 표지 셋을 형태어로 앞쪽에 · 색은 재료 이름에 붙여)
chunky stylized pixel art with an oversized helm and upper body and short limbs, clean cel shading with 3 to 4 flat tones per material,
strong light and dark contrast, smooth surfaces without scratches or speckles; one round hunched lump made of two empty dark gunmetal
iron armors fused together, the front armor slumped on its knees, the back armor collapsed over its shoulders driving a long spear
straight through the front armor's chest so the spear sticks out on both sides, both frozen in that stabbing pose, thick crusted dull
red-brown rust joining them at every seam, two bucket helms side by side with empty eye slits, pale bone-white only inside the eye slits
```

### R1 실측 (SE · `characters/melee_brute/_r1_compare.png`)

```
              폭   높이   채움   색
플레이어 기사    36    60   65.6%  26
근접 병사 R3    73    61   52.3%  37
중장 강적 R1    49    60   80.1%  26    <- 폭 < 높이 · 꽉 찬 사각 덩어리
```

| 문구 | R1 |
|---|---|
| chunky stylized · cel shading · strong contrast | ✅ 근접 병사 R3 와 같은 화풍 · 잡티 없음 · 26색 |
| two bucket helms side by side | ✅ **투구 둘이 나란히** — 한눈에 「둘이 붙은 것」으로 읽힌다 |
| dark gunmetal iron | ✅ 건메탈 — 후처리 없이 근접 병사 B 와 같은 계통 |
| thick crusted rust at every seam | ⚠️ 이음매에 가는 붉은 갈색 선 · 가슴에 녹 얼룩 — **딱지로 두껍게는 아님** |
| 🔴 long spear through the chest | ❌ **창이 없다** — 「찔러서 붙었다」가 안 보인다 |
| 🔴 front armor slumped on its knees · back collapsed over | ❌ **둘이 나란히 서 있다** — 「적은 서 있지 않다」와 어긋남. 머리 둘 달린 골렘처럼 읽힌다 |
| 🔴 pale bone-white only inside the eye slits | ⚠️ **투구 윗면 전체가 흐린 뼈색** — 강조점이 아니라 넓은 면 |
| 도형 | ⚠️ 둥근 덩어리가 아니라 **세로 사각 덩어리**(채움 80%) · 근접 병사(가로로 낮음)와는 확실히 갈림 |

### R2 — R1 의 빠진 것을 문구 맨 앞 형태어로 (2026-09-18)

| 회차 | 도구 · 설정 | id | 판정 |
|---|---|---|---|
| R2 몸 | pro · style `2fbf625f-f755-4d1d-a113-adadfdf97ce0` · size 92 · view side · 25 gen | `49cea6a0-5d3a-4df2-9991-a436743fecd9` | ✅ **채택**(사용자 2026-09-18) — 창 관통 · 투구 둘 · 뼈색 테두리. 상체가 곧은 것은 공격·사망 동작으로 보완 |

```
R2 문구 — 주어를 「창」으로(창이 먼저 그려지게) · 서 있지 않은 자세 둘 · 뼈색은 눈구멍 테두리로
chunky stylized pixel art with an oversized helm and upper body and short limbs, clean cel shading with 3 to 4 flat tones per material,
strong light and dark contrast, smooth surfaces without scratches or speckles; a long iron spear pierces straight through a low round lump
of two empty dark gunmetal armors, the spear shaft sticking out of both sides of the lump, one armor sagging forward on its knees with the
spear through its chest, the other armor slumped over its back still gripping the spear, both helms leaning against each other, the two
armors glued together by thick lumpy red-brown rust crust at the joints, dark empty eye slits with a thin pale bone-white rim
```

### R2 실측 (SE · `characters/melee_brute/_r2_compare.png`)

```
              폭   높이   채움   색
근접 병사 R3    73    61   52.3%  37
중장 강적 R1    49    60   80.1%  26
중장 강적 R2    78    58   47.3%  28    <- 폭 78 은 창 포함 · 몸만 약 43(열당 불투명 10px 이상인 열)
```

| 문구 | R1 | R2 |
|---|---|---|
| 🔑 long spear pierces through · sticking out of both sides | ❌ | ✅ **창이 몸을 가로로 꿰뚫고 양쪽으로 나왔다** — 주어를 창으로 둔 효과 |
| helms leaning against each other | 나란히 | ✅ 투구 둘이 기대어 붙었다 |
| bone-white rim only | ❌ 윗면 전체 | ✅ **눈구멍 테두리만** 뼈색 |
| rust crust at the joints | ⚠️ 가는 선 | ✅ 이음매 띠가 붉은 갈색으로 또렷 — 다만 **창 자루도 같은 갈색**이라 섞여 보일 수 있다 |
| sagging on its knees · slumped over | ❌ 서 있음 | ⚠️ 무릎을 굽혀 낮아졌지만(높이 58) **상체는 아직 곧다** — 「둘이 웅크려 붙은 것」 정도 |
| 도형 | 세로 사각 | ⚠️ 창이 가로선을 만들어 넓게 읽힌다 · 몸 덩어리는 세로 |

🗑 계정 정리 후보: R1 `574e2acc`(사용자 확인 후)
