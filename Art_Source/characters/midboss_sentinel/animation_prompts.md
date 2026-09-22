# midboss_sentinel — 애니메이션 생성 문구 기록

> 형식 · 문구 규칙은 `Art_Source/characters/knight_red/animation_prompts.md` 를 따른다.
> 기획 · 몸 판정은 `Art_Source/20_SUBJECTS/enemies/midboss_sentinel.md`. 조립·재생 규약은 `melee_grunt/animation_prompts.md` 끝 절과 같다.
> 🔴 **생성하면 여기에 먼저 적는다** — 문구가 휘발되면 재현이 안 된다.

---

# 🔵 현행 — R4 「제 아래를 갈아 없앤 것」 (2026-09-22 · 새 컨셉)

> 🔴 **R2 컨셉(땅에 박힌 축)은 폐기됐다.** 다리를 못 없애 네 번 실패했고(문구 3회 · 몸 R3 · `freezeBelow` ·
> 지표 신설), 사용자 결정으로 **컨셉 자체를 바꿨다.** 아래 「이전 컨셉」 절은 근거 보존용이다.

## 공통 (R4)

```
character_id   ff42f0d6-80f3-4ecd-8061-6a766783a9a8   (Sentinel R4 upper-half · pro · style 2fbf625f-f755-4d1d-a113-adadfdf97ce0)
캔버스          128x128 (pro 상한) · south-east 한 방향 + flipX
mode           v3 · 실비용 이동 4 · 공격 5 · 피격 2 · 사망 4 = 15 gen
보간            end_frame_url = R4 south-east 회전 (사망만 제외 — 돌아올 자세가 없다)
정체성 어휘      only the top half of a heavy suit of armour hovering in mid air with clear empty space
                beneath it, its body ending at the waist in a single rough uneven broken edge, two very
                long straight blades reaching far out to the left and to the right and angled a little
                downward, an empty open helmet
```

### 🔴 정체성 어휘에서 무엇을 지웠고 왜 지웠나 — 이 절이 이 파일에서 가장 중요하다

| 지운 말 | 왜 |
|---|---|
| `rooted in one place` · `buried to a point in the ground` · `planted` | **이것들이 다리를 만든 장본인이다.** 세션 13 교훈 5 — 부정문 하나로는 다른 어휘가 만든 전제를 못 이긴다 |
| `with no legs and no feet` | 🔴 **부정문조차 안 쓴다.** `legs`·`feet` 라는 낱말이 앵커를 자극한다. 하반신은 **말하지 않음으로써** 없앤다 |

🔑 **몸에서 다리를 없애도 애니메이션 문구에 `buried` 가 남아 있으면 프레임에 하반신이 다시 생긴다.**
`create_character` 앵커와 `animate_character` 앵커가 같은 전제를 공유하기 때문이다.

🔴 **`slash`·`spin`·`rotate`·`arc` 를 쓰지 않는다**(문구 규칙 — 멈춘 자세로 쓴다). 회전은 **날이 어디를 가리키는가**로 그린다.
🔴 **원(바닥의 빈 자리)을 그리게 하지 않는다** — 배경이 가질 것이다.

## 상태 (R4) — FSM Patrol · Chase → 이동 / Attack / Stagger → 피격 / Dead

| 상태 | frames | animation_name | 문구(정체성 어휘 뒤) | group | 보간 | 판정 |
|---|---|---|---|---|---|---|
| 이동 | 9 (1+8) | `move_southeast_v1` | drifting forward through the air with the whole body leaning over in the direction of travel and **rising and sinking a little as it goes**, the blades staying level, everything staying one connected body | `ddd9c927-0ca6-48bf-8246-8185617724fc` | end=R4 | ✅ **채택** · 다리 없음 전 프레임 유지 · 폭 103→125px 흔들림 |
| 공격 | 11 (1+10) | `attack_southeast_v1` | the whole body first **drawing in tight and small** with both blades folded back and upward behind it, then **opening out wide all at once** so the blades rise to point **straight up overhead** and come down pointing **far forward and low** in front, the shoulders and the helmet turning all the way around with them | `2f840eb9-175c-457f-9040-39db696ca148` | end=R4 | ✅ **채택** · 「뒤위→**똑바로 위 f5~f7**→앞아래」 · 웅크림↔펼침 실픽셀 3493→4105 · 진폭 34px |
| 피격 | 5 (1+4) | `hit_southeast_v1` | the shoulders and helmet rocking hard backward as it takes a blow and **the whole body drifting back a little through the air**, then settling forward into the same shape | `4eb0c54d-8297-4a5f-bb52-525d251a8ebe` | end=R4 | ✅ **채택** · 🔑 **R2 의 「축이 몸을 붙잡아 진폭 없음」이 해결됐다** — f2 에서 키 +12 · 폭 −19 |
| 사망 | 9 (1+8) | `dead_southeast_v1` | **sinking down out of the air for the first time**, tilting over as it falls until the blades strike the ground and it comes to rest leaning on them, no longer held up by anything | `ebad4b16-5644-4f88-84cc-484709d45d51` | 없음 | ❌ f5 에 **날 한 자루 소실** · f6~f7 날이 위로 섬 · 하이라이트 0% 프레임 둘 |

| 사망 v2 | 9 (1+8) | `dead_southeast_v2` | first sinking down out of the air, then **toppling over sideways and coming to rest flat on the ground** with the blades lying flat beside it | `24252e66-7458-4f8d-b7a5-7a6ec3273546` | 없음 | ❌ f3~f5 에 **투구가 몸에서 떨어져 위로 떠오른다** · 🔴 f5 150px · f8 **153px 로 칸 148 초과** |
| 사망 v3 | 9 (1+8) | `dead_southeast_v3` | the armour body **quietly thinning and wearing away into nothing where it is**, while the empty helmet and the two blades **drop straight down and come to rest close together** | `2e3ecb1d-e8f0-4167-8078-67a54bfff177` | 없음 | ❌ f0~f4 거의 변화 없음 · **끝 두 프레임이 갈색 무더기로 뭉개져** 칼·투구가 구별 안 됨 |
| **사망 (채택)** | 9 | **손 조립** `dead_composed/` | — `Tools/ArtPipeline/compose_shed_death.py` | — | 없음 | ✅ **채택** · 몸이 갈려 없어지고 **칼 둘 + 투구가 바닥에** · bbox 103x27 |

### 🔴 사망을 **세 번 생성해 세 번 다 실패했다** — 12 gen · 방식이 틀렸다

| 회차 | 무엇이 틀렸나 |
|---|---|
| v1 | 날 한 자루가 사라지고, 날이 위로 섰다. 「날에 기대어 멈춘다」가 안 나왔다 |
| v2 | 🔴 **투구가 몸에서 떨어져 위로 떠올랐다.** 게다가 누우면서 **폭 153px** 로 칸 148 을 넘었다 |
| v3 | 사용자 요청(「몸은 사라지고 칼·투구만」)대로 문구를 썼으나 **끝 두 프레임이 무더기로 뭉갰다** |

🔑 **PixelLab v3 는 「몸이 사라지고 부품만 남는 것」을 못 그린다.** 캐릭터를 유지하도록 학습된 모델이라
   사라짐을 요구하면 뭉갠다. **방식이 틀렸으므로 프레임을 늘려도 안 된다**(메모리 `feedback_reference_before_motion`).
👉 **조립으로 바꿨다**(gen 0) — `compose_shed_death.py` 가 ① 얇은 부위 = 날, 상자+씨앗 = 투구를 떼고
   ② 나머지 몸을 프레임마다 한 겹씩 **침식**해 없애고 ③ 날을 **끝을 축으로 세로 압축**해 눕히고
   ④ **투구를 마지막에 얹는다**(엘리트 사냥꾼 사망과 같은 차례).

🔴 **「연기처럼 사라진다」로 하지 않았다** — `10_BIBLE/03-light.md` **L6(공중 파티클 금지)**.
   대신 **「제자리에서 갈려 없어진다」**로 했고, 그것이 이 적의 정체 그 자체다
   (**제 아래를 갈아 없앤 것**이 마지막에 제 전부를 갈아 없앤다. 갈려도 안 없어지는 것이 쇠다).

**앞 컨셉의 교훈을 어디서 쓰고 있나**

| 교훈 | 어디에 |
|---|---|
| 🔑 **공격 v3 의 웅크림↔펼침**(진폭 19 → 51px) — 날로 원을 그리려던 것이 잘못이었다 | 공격 `drawing in tight and small` → `opening out wide all at once` |
| 🔑 **v2 의 방향 단계 못박기**(뒤위 → 똑바로 위 → 앞아래) | 공격 문구 뒷단 그대로 |
| 🔑 **떠 있는 적은 정렬이 `first`**(세션 13 교훈 4) — `each` 는 위아래 흔들림을 지운다 | 이동 `rising and sinking a little` 을 **일부러 넣었다** |
| 🔑 **피격은 축이 없어져 진폭을 얻는다** — R2 는 `the buried lower point stays completely still` 이 몸을 붙잡았다 | 피격 `drifting back a little through the air` |
| 🔑 **기획 ④ 가 더 강해졌다** — 「백 년 만에 처음 자리를 뜬다」 = **「처음으로 땅에 닿는다」** | 사망 `sinking down out of the air for the first time` |

---

# ⬛ 이전 컨셉 — R2 「땅에 박힌 축」 (폐기 · 근거 보존)

## 공통

```
character_id   39f5e125-f5fd-4ca0-968a-4559e0b2637f   (Midboss Sentinel R2 · pro · style 2fbf625f-f755-4d1d-a113-adadfdf97ce0)
캔버스          🔴 128x128 (pro 상한) · south-east 한 방향 + flipX
mode           v3 · 8프레임 4 gen · 4프레임 2 gen   ← 🔴 92 캔버스(2/1)의 두 배다
후처리          (시트 조립 때 결정 — 아래 「후처리 파이프라인」)
정체성 어휘      a heavy empty suit of armour rooted in one place with no legs and no feet,
                its lower end buried to a point in the ground, two very long straight blades
                reaching far out to the left and to the right held low just above the ground,
                an empty open helmet
```

🔴 **비용이 캔버스에 비례한다.** 128 캔버스는 8프레임에 **4 gen**(92 캔버스는 2)이다 —
4상태 합이 **14 gen**(엘리트 사냥꾼 120 캔버스는 7, 92 캔버스 종들은 7~9).

🔑 **「박혀 있다」를 매 문구에 다시 쓴다.** 이 적의 정체 표지 둘 중 하나가 **아래 끝이 한 점으로 땅에 박힌 것**이고,
몸 R1 에서 형태어(`tapering … to a narrow point … buried straight down`)로 한 번에 얻었다.
🔴 R2 에서 `the torso very short` 를 덧붙였다가 **다리가 생길 뻔했다**(실제로는 날이었지만 내가 오판했다) —
**이긴 문구에 다른 요구를 덧붙이면 진다.**

🔴 **`slash`·`spin`·`rotate` 를 쓰지 않는다**(문구 규칙 — 멈춘 자세로 쓴다).
회전은 **날이 어디를 가리키는가**로 그린다: `start drawn far back behind it and then come all the way around to point far forward`.

🔴 **원(바닥의 빈 자리)을 그리게 하지 않는다.** 그것은 배경이 가질 것이고,
캐릭터 그림에 넣으면 프레임마다 자리가 바뀌어 깜빡인다(원거리 사수 v2 「땅에 박힌 화살」 계열).

## 상태 (FSM Patrol · Chase → 이동 / Attack / Stagger → 피격 / Dead)

| 상태 | frames | animation_name | 문구(정체성 어휘 뒤) | group | 보간 | 판정 |
|---|---|---|---|---|---|---|
| 이동 | 9 (1+8) | `move_southeast_v1` | dragging itself forward with the whole body leaning over in the direction of travel while the buried point stays planted, the blades staying level and never leaving the ground, everything staying one connected body | `bb1749f3-68a2-4996-91ac-e32bcbfa3068` | end=R2 | ⏳ |
| ~~공격 v1~~ | 9 (1+8) | `attack_southeast_v1` | two very long straight blades that start drawn far back behind it and then come all the way around to point far forward, sweeping level just above the ground the whole way, the buried lower point never moving at all, the upper body turning with the blades | `cd2d287f-bae0-46d2-9d54-9a1dd0533344` | end=R2 | ❌ **사용자 「어색하다」** — 날이 땅에 붙은 채 진폭 19px |
| 피격 | 5 (1+4) | `hit_southeast_v1` | the shoulders and helmet rocking backward as it takes a blow while the buried lower point stays completely still, then settling back upright into the same shape | `e8fb4c3e-d023-4ec4-b411-bc4c85500c67` | end=R2 | ⏳ |
| **공격 v2** | **11 (1+10)** | `attack_southeast_v2` | the whole upper body wound far around so that both blades point **back and upward** behind it, then turning hard the other way so the blades first rise to point **straight up overhead** and then come down pointing **far forward and low** in front, the shoulders and the helmet twisting all the way around with them, **only** the buried lower point staying planted | `3251d2da-dd89-4d6e-929e-b098aacd8bb6` | end=R2 | ✅ **채택** · 진폭 19→**35px** · f7 이 예비동작 끝 |
| 사망 | 9 (1+8) | `dead_southeast_v1` | its narrow lower end **snapping off at the ground so that for the first time it is no longer rooted**, the whole body toppling over sideways and coming to rest flat on the ground with the blades lying flat beside it | `8bc0f887-63b4-444c-93d0-77352a3d96fe` | 없음 | ⏳ |

📌 **사망만 「박혀 있다」를 깬다** — 기획 ④ 의 *"백 년 만에 처음으로 자리를 뜬다"* 가 그것이다.
그래서 정체성 어휘에서 `rooted in one place` 를 빼고 `no longer rooted` 로 바꿨다.

📌 **보간(`end_frame_url` = R2 의 south-east 회전 이미지)** — 사망만 뺀다(돌아올 자세가 없다).

## 🔧 후처리 파이프라인 (시트 조립 때 적용)

사용자가 몸을 직접 고친 것(`R2Custom_southeast.png`)과 대조해 정한 순서다 — `midboss_sentinel.md` 참조.

```json
"snapGray":         { "valueMin": 0.25, "satMax": 0.10 },
"eraseRect":        [ { "x": [17, 49], "y": [92, 99] } ],
"despeckle":        { "valueMin": 0.40, "minComponent": 5, "sameBand": 0.12 },
"bridgeHighlight":  { "value": 0.60, "expect": 2, "minSeed": 8, "maxGap": 10 },
"bladeSmooth":      { "seedValue": 0.60, "minSeed": 20, "span": 6, "reach": 2 },
"outline":          { "ink": "#060405", "keepBright": [0.06, 0.88], "brightMin": 116 }
```

🔴 **`collapse` 의 `"to":"down"` 을 쓰지 않는다** — 중간톤이 **날의 면**이라 몸통색으로 내리면 날이 납작해진다.
사용자는 반대로 **어두운 몸통색과 외곽선까지 밝게 올려 하이라이트 줄을 이었다.**

🔴 **`eraseRect` 값은 몸 기준이라 애니메이션에서 다시 재야 한다** — 프레임마다 바닥 그림자의 자리가 다르면
사각 하나로는 안 되고, 상태별로 나누거나 다른 수를 써야 한다. **실측 뒤 정한다.**

📌 **판정 지표는 「하이라이트 연결 성분 수 = 날 개수(2)」**다 — 「날 영역 색 전환율」은
**뭉개도 낮아져서** 내 후처리가 사용자본보다 낮게 나왔는데 그림은 더 나빴다.


## 🔴 공격 v1 이 어색했던 이유 (사용자 지적 · v2 로 해결)

| v1 문구 | 무엇이 잘못됐나 |
|---|---|
| `sweeping level just above the ground the whole way` | **날을 땅에 붙여 놓아** 큰 호를 못 그렸다 |
| `the upper body turning with the blades` | 약했다 — 몸이 거의 안 돌았다 |
| `the buried lower point never moving at all` | 🔴 **몸 전체를 붙잡았다.** 축만 고정이어야 하는데 상체까지 멈췄다 |

```
          프레임별 폭(좁을수록 날이 수직)                    진폭
v1 (9f)   99 101 98 92 82 91 94 98 99                      19px
v2 (11f)  99 102 92 68 79 92 86 67 90 101 99               35px  <- 거의 두 배
```

🔑 **v2 가 고친 것**: ① `level just above the ground` 를 빼고 **「뒤위 → 똑바로 위 → 앞아래」**로
**날이 가리키는 방향**을 단계별로 못박았다(`slash`·`spin`·`arc` 없이) ② `the shoulders and the helmet
twisting **all the way around**` 로 상체를 크게 돌렸다 ③ `**only** the buried lower point staying planted`
로 **고정 대상을 축 하나로 좁혔다.**

📌 **프레임도 9 → 11 로 늘렸다** — 큰 호는 프레임이 적으면 끊겨 보인다. 비용은 같은 4 gen 이었다.

🔴 **번들 캐시에 걸렸다** — 레시피를 v2 로 바꾸고 그냥 돌렸더니 **옛 v1(9프레임)이 조립됐다.**
새 애니메이션을 뽑았으면 `--download` 로 번들을 다시 받아야 한다.
