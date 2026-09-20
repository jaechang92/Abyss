# flame_mortar — 애니메이션 생성 문구 기록

> 형식 · 문구 규칙은 `Art_Source/characters/knight_red/animation_prompts.md` 를 따른다.
> 기획 · 몸 판정은 `Art_Source/20_SUBJECTS/enemies/flame_mortar.md`. 조립·재생 규약은 `melee_grunt/animation_prompts.md` 끝 절과 같다.
> 🔴 **생성하면 여기에 먼저 적는다** — 문구가 휘발되면 재현이 안 된다.

## 공통

```
character_id   b5c6be41-9701-4ebf-b458-9db464d5f6d5   (Flame Mortar R2 · pro · style 2fbf625f-f755-4d1d-a113-adadfdf97ce0)
캔버스          92x92 · south-east 한 방향 + flipX
mode           v3 · 8프레임 2 gen · 4프레임 1 gen
후처리          ❌ **없다** — 🔴 채도를 내리면 원거리 사수(실게임 hue 48 흙빛)로 수렴한다.
                사수는 S1·2·3 전부에 나오므로 S3 에서 같은 방에 난다(`flame_mortar.md` 「지표 5 를 폐기한다」).
                R2 원본(hue 33 · 채도 0.66 테라코타)이 앞 5종과 가장 잘 갈린다
정체성 어휘      a low wide heap of broken town pieces — clay roof tiles, a chimney, an upright pale plastered wall slab,
                an upturned bowl — all leaning together in one connected pile with no head and no face
```

🔑 **「한 덩어리」를 매 문구 앞에 다시 쓴다.** R1 의 유일한 구조적 결함이 **공중에 뜬 기와 한 장**이었고,
그것을 이긴 것이 `all leaning together in **one connected pile**` 이다. 애니메이션은 **매 프레임 새로 그리므로**
분리가 다시 생기면 프레임마다 자리가 바뀌어 깜빡인다(원거리 사수 v2 의 「땅에 박힌 화살」과 같은 계열).

🔑 **「머리도 얼굴도 없다」도 매번 쓴다.** v3 는 부자연스러운 형태를 못 버티고 **사람으로 회귀한다**
(중장 강적의 「일어서서 걷는다」 · 공허 술사의 「걷는 다리」). 이 적은 **사람이 아닌 것**이 정체다.

🔴 **발사체를 그리게 하지 않는다.** 공격은 「조각을 위로 올린다」인데, 그 조각을 몸에서 떼어 그리면
**발사체 프리팹과 겹쳐 화면에 둘**이 된다. `clear of the heap` 같은 **분리 어휘를 쓰지 않고**
(R1 몸이 바로 그 어휘로 분리됐다) **`pushed up along the top edge`** 로 표면을 따라 밀어 올린다.
그리고 **`the space above the heap staying clear and empty`** 로 위쪽이 비어 있음을 **긍정형**으로 못박는다
(공허 술사 공격 v2 에서 얻은 규칙).

## 상태 (FSM Patrol · Chase → 이동 / Attack / Stagger → 피격 / Dead)

| 상태 | frames | animation_name | 문구(정체성 어휘 뒤) | group | 보간 | 판정 |
|---|---|---|---|---|---|---|
| 이동 | 9 (1+8) | `move_southeast_v1` | dragging itself slowly forward along the ground with no legs showing, the stacked pieces shifting and rattling slightly against each other, the whole heap staying low and wide the whole time | `c8454ac8-3b74-4bd6-acd8-bceee3f1f665` | end=R2 | (사용자 판정 대기) |
| 공격 | 9 (1+8) | `attack_southeast_v1` | the top of the heap heaving upward as one clay tile is pushed up along the top edge and over it, the rest of the pile staying pressed together, the space above the heap staying clear and empty the whole time, then settling back into the same low wide shape | `f7d0ba8e-b7fe-4675-b167-5cb8701feb69` | end=R2 | (사용자 판정 대기) |
| 피격 | 5 (1+4) | `hit_southeast_v1` | a few pieces sliding loose down the side of the heap and settling against it again, the body barely moving at all, everything staying in one connected pile, then settling back into the same low wide shape | `c38ef780-cd85-4ac7-9bee-8a75133f7b66` | end=R2 | (사용자 판정 대기) |
| 사망 | 9 (1+8) | `dead_southeast_v1` | the whole heap sagging and then collapsing outward, the chimney toppling and the wall slab falling flat, the pieces spreading wider and flatter across the ground until nothing stands above the rubble | `8e689331-5e51-437d-8c7f-6f3a10bd4392` | 없음 | (사용자 판정 대기) |

📌 **다리는 몸에서 사라졌다**(R1 엔 있었고 R2 엔 없다). 이동을 **`dragging … with no legs showing`** 으로 쓴 것은
그 사실에 맞춘 것이고, 공허 술사가 「걷지 않고 미끄러진다」로 푼 것과 같은 수다.

📌 **④ 의 「등이 다시 차오른다」(⑤-ⓖ)는 이번 문구에 안 넣었다.** 9프레임에 「올린다 + 다시 찬다」 둘을 요구하면
공허 술사의 「세 번」처럼 뭉개진다. 먼저 **올리는 동작 하나**를 확실히 얻고, 필요하면 그때 더한다.

## v1 판정 (2026-09-20) — 🟢 **넷 다 결함 없음** (`_anim_v1_preview.png` · 7 gen)

### ✅ 「한 덩어리」가 완벽히 작동했다 — 32프레임 전수 검사

```
이동  분리된 프레임 0/9
공격  분리된 프레임 1/9   <- f5 에 120px 조각 하나 (의도된 것 — 아래)
피격  분리된 프레임 0/5
사망  분리된 프레임 0/9
```

🔑 **R1 몸의 유일한 구조적 결함이 애니메이션에서 한 번도 재발하지 않았다.**
`all leaning together in **one connected pile**` 을 매 문구 앞에 다시 쓴 것이 값을 했다 —
원거리 사수 v2 가 「땅에 박힌 화살」로 7 gen 을 다시 쓴 것과 정반대 결과다.

| 상태 | 결과 |
|---|---|
| 이동 | ✅ **채택.** 한 덩어리 유지 · **다리가 안 생겼다**(v3 의 사람 회귀 없음) · 낮고 넓은 형태 유지. ⚠️ 이동감이 약하지만 속도 1.4 로 12종 중 가장 느린 적이라 어긋나지 않는다 |
| 공격 | ✅ **채택.** **f5 에서 조각 하나가 꼭대기 위로 떠오른다** — 🔑 **이 분리는 의도된 것**이고, 바로 그 프레임이 발사 시점이 된다(아래 ⓑ). 캔버스도 116x96 으로 이동(116x92)과 4px 차이뿐 — 공허 술사 공격 v1 이 긴 선으로 92→132 로 부푼 것과 대비된다 |
| 피격 | ✅ **채택.** 조각이 흔들리고 한 덩어리 유지 · 몸이 거의 안 밀린다(④ 의도대로 — 무겁고 느리다). ⚠️ 피격감은 플레이에서 볼 것 |
| 사망 | ✅ **채택 — 넷 중 가장 잘 나왔다.** 더미가 무너지며 **굴뚝이 쓰러지고 회벽이 눕는다**(문구 그대로). 낮고 넓게 퍼져 「도시로 돌아간다」가 읽힌다 |

### 🔑 ⓑ 동작 시간이 이 그림에서 바로 나온다 — 뼈 궁수와 **같은 식**

공격 **f5 가 조각이 떠나는 프레임**이므로 `5/9 x (w+r) = w` → **`w = 1.25r`** (뼈 궁수와 같다).
다만 이 적은 **12종 중 가장 느리고** 예비동작이 길어야 「피할 수 있는 적」이 되므로(⑤-ⓑ) 합을 크게 잡는다:

```
w + r = 1.8   ->   windup 1.0 + recovery 0.8     f5 = 5/9 x 1.8 = 1.0초 ✅
```

- 예비동작 **1.0초**는 앞 5종(0.5~0.8) 중 **가장 길다** — 사거리 9 · 곡사 비행 1.15초와 합쳐 회피 창이 넉넉하다
- 쿨다운 3.2 안에 1.8 이 들어가고 1.4 가 남는다
- 🔑 **그림의 조각이 떠나는 순간에 발사체 프리팹이 난다** — 겹치지 않고 이어진다
