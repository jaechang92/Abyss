# melee_brute — 애니메이션 생성 문구 기록

> 형식 · 문구 규칙은 `Art_Source/characters/knight_red/animation_prompts.md` 를 따른다.
> 기획 · 몸 판정은 `Art_Source/20_SUBJECTS/enemies/melee_brute.md`. 조립·재생 규약은 `melee_grunt/animation_prompts.md` 끝 절과 같다.
> 🔴 **생성하면 여기에 먼저 적는다** — 문구가 휘발되면 재현이 안 된다.

## 공통

```
character_id   49cea6a0-5d3a-4df2-9991-a436743fecd9   (Melee Brute R2 · pro · style 2fbf625f-f755-4d1d-a113-adadfdf97ce0)
캔버스          92x92 · south-east 한 방향 + flipX
mode           v3 · 8프레임 2 gen · 4프레임 1 gen
후처리          없음 (몸이 이미 건메탈 — 근접 병사 색 B 불필요)
정체성 어휘      two empty dark gunmetal armors fused into one lump by rust, a long spear through both, two leaning bucket helms
```

## 상태 (FSM Patrol · Chase → 이동 / Attack / Stagger → 피격 / Dead)

| 상태 | frames | animation_name | 문구(정체성 어휘 뒤) | group | 판정 |
|---|---|---|---|---|---|
| 이동 | 9 (1+8) | `move_southeast_v1` | shuffling forward slowly as one heavy lump, the two armors dragging their knees one after the other so the lump rocks from side to side, the spear staying through both bodies | `6a6dbcff-0436-4598-aa15-c392b1c453f8` | ⚠️ 2 gen · **f4 부터 일어서서 걷는다**(웅크린 덩어리가 아님) · 루프 f8→f0 에서 선 자세 → 웅크림으로 튐 · 발밑 92~97 |
| 공격 | 9 (1+8) | `attack_southeast_v1` | rearing up and leaning back as one lump, then toppling forward heavily and slamming the whole lump down onto the ground in front of it with the spear point driving forward, then pushing back up into its low crouch | `8c5bc5ad-ff75-4afb-831f-94a63d7bf054` | ✅ 2 gen · f3~4 들어 올림 → **f5~6 앞으로 엎어지며 창끝이 땅으로**(덮치는 프레임 있음) · 다시 일어나지는 않음 · 발밑 93~96 |
| 이동 v2 | 9 (1+8) | `move_southeast_v2` | staying low in a hunched crouch the whole time and never standing up, dragging itself forward on its knees in small heavy lurches, the lump tilting forward and settling back, the spear staying through both bodies | `5806d46f-17bd-47d5-ab26-7693db75f880` | ✅ 2 gen · **9장 내내 웅크림**(발밑 93 고정 · 키 58→54→58 덜컹임) · f8 이 f0 으로 돌아와 루프가 안 튄다 |
| 피격 | 5 (1+4) | `hit_southeast_v1` | jolted backward as one lump with both helms tipping back and rust flakes falling from the joints, then settling low again | `8c7d7737-f3f1-4829-a1b2-fc5288652bc8` | ⚠️ 1 gen · 투구가 뒤로 젖혀짐 · **f3 왼쪽 투구에 검은 덩어리 · 오른쪽 아래 떨어진 1px** |
| 사망 | 9 (1+8) | `dead_southeast_v1` | the rust joints cracking apart so the two armors fall away from each other, collapsing into two separate empty heaps on the ground with the spear lying between them | `26929856-c1b0-4de3-9da9-40f077ff97aa` | ⚠️ 2 gen · 창이 바닥에 떨어지고 **투구 둘이 빠져 굴러떨어진다** · 갑옷 둘로 갈라지기보다 「하나가 주저앉고 투구가 떨어짐」 · f7 검은 조각 |

📌 공격은 근접 병사에서 「내리치는 프레임이 없다」가 나왔다 — 여기서는 **덮치는 순간(slamming down onto the ground)** 을 명시했다.

미리보기: `_anim_v1_preview.png` — 상태마다 원본(raw) / 발밑 정렬 후(built) 두 줄 · 초록선 = footY 92

## 사용자 판정 (2026-09-18)

- 공격 · 피격 · 사망 v1 **채택** — 피격 f3 · 사망 f7 검은 조각은 Unity 에서 보이면 그때
- 이동 v1 ❌ **일어서서 걷는다** → v2 재생성(「never standing up」을 동작 문구 맨 앞에) → ✅ v2 채택

📌 **「never standing up」 류의 부정 표현이 먹었다** — 문구 규칙은 긍정문이지만 `staying low in a hunched crouch the whole time` 과 같이 붙이면 됐다.

## 시트 · 재생 (2026-09-18)

```
레시피   sheet_recipe.json — cell 124 · footY 92 · recolor 없음 · 이동 v2
클립     Move 10fps 루프 · Attack 1.2초(windup 0.8 + recovery 0.4) · Hit 0.3초 · Dead 1.0초(사라지기 1.2 − 0.2)
```

- 공격 9장을 1.2초에 → 7.5fps. **f6(창끝이 땅에 닿음)이 0.8초** — 피해 시각이 그림의 덮침과 맞는다
- 🔴 공격은 `align first` — f6~8 의 가장 아래 픽셀은 **땅에 박힌 창끝**(96)이라 `each` 로 맞추면 덮치는 순간 몸이 4px 떠오른다

## ✅ Unity 재생 (2026-09-18 · 사용자 「검증완료」)

- 시트 4장 `Assets/Art/Sprites/Enemies/melee_brute/southeast/` · 클립 4 · `MeleeBrute.overrideController` · 프리팹 Visual 배선
- 📌 피격 f3 · 사망 f7 검은 조각은 검증에서 문제로 지적되지 않아 그대로 둠
