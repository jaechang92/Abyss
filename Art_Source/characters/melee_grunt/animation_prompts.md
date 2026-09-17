# melee_grunt — 애니메이션 생성 문구 기록

> 형식 · 문구 규칙은 `Art_Source/characters/knight_red/animation_prompts.md` 를 따른다.
> 기획 · 몸 판정은 `Art_Source/20_SUBJECTS/enemies/melee_grunt.md`.
> 🔴 **생성하면 여기에 먼저 적는다** — 문구가 휘발되면 재현이 안 된다.

## 공통

```
character_id   bbdbcfd4-d587-40f4-8eaf-3c9b701f3ba3   (Melee Grunt R3 · pro · style 2fbf625f)
캔버스          92x92 · south-east 한 방향 + flipX
mode           v3 · 비용 = ceil(92·92·frames / 65536) / 방향  →  8프레임 2 · 4프레임 1
후처리          색 변환 B(건메탈) — melee_grunt.md 「R3 채택」 절의 식을 모든 프레임에
정체성 어휘      an empty suit of rusted armor with an oversized bucket helm and bone-white eye slits, holding a short worn sword
```

## 상태 (FSM Patrol · Chase → 이동 / Attack / Stagger → 피격 / Dead)

| 상태 | frames | animation_name | 문구(정체성 어휘 뒤) | group | 판정 |
|---|---|---|---|---|---|
| 이동 | 9 (1+8) | `move_southeast_v1` | crawling forward low along the ground on its hands and knees, the heavy helm bobbing with each drag, the sword scraping along the ground | `43752e7e-6240-4cf4-a33e-3b83edda28ca` | ⚠️ 2 gen · 기는 동작이 작다(몸 앞뒤로 조금) · 발밑 91~93 |
| 공격 | 9 (1+8) | `attack_southeast_v1` | rising up on its knees with the sword lifted high overhead in one hand, then bringing the sword down hard onto the ground in front of it, then sinking low again | `70823277-0a0a-4fcb-b1f1-7ec570303ddf` | ❌ 2 gen · **일어서서 검을 들기만 하고 내리치는 프레임이 없다** · f7~8 에서 검이 사라짐 · 서는 자세라 「서 있지 않다」와 어긋남 |
| 피격 | 5 (1+4) | `hit_southeast_v1` | jolted backward with the helm tipped back and the arms pulled in close, then settling low again | `3bb62a0b-2497-4efe-bcd4-d4537b819a85` | ✅ 1 gen · 투구가 뒤로 젖혀짐 |
| 사망 | 9 (1+8) | `dead_southeast_v1` | collapsing and coming apart, the helm rolling off to the side and the armor pieces falling flat into a scattered heap on the ground, empty inside | `d1cc99a6-1d14-4d11-aae8-178c1b800ea8` | ✅ 2 gen · 주저앉고 투구가 앞으로 떨어져 더미가 됨(흩어짐은 약함) · 발밑 92~93 |

📌 `slash`/`arc` 는 안 썼다 — 공격은 「들어 올렸다 땅에 내리친다」(원문의 반복 동작).

미리보기(색 변환 B 적용): `_anim_v1_preview.png` · 번들 칸 124x124 · 피벗 칸 중심 → 92 캔버스 오프셋 (16,16)

## ✅ 사용자 판정 (2026-09-17) — 4개 전부 「괜찮아 보여」 채택

내 판정(공격 ❌ 내리치는 프레임 없음 · 이동 ⚠️ 동작 작음)과 달랐다 — **사용자는 재생성 없이 채택.**
📌 Unity 에서 실제 재생해 보고 어색하면 그때 공격부터 다시 뽑는다(2 gen).

번들: `_bundle/bundle.zip`(비추적) — 행 1 hit · 2 move · 3 attack · 4 dead, 칸 124 · 칸 중심 피벗

## ✅ 시트 조립 → Unity 재생 (2026-09-18 · gen 0 · 사용자 「검증완료」)

```
레시피   sheet_recipe.json — recolor(색 B) · cell 124 · footY 92 · 4상태 all (사망만 align first)
시트     Assets/Art/Sprites/Enemies/melee_grunt/southeast/melee_grunt_{move|attack|hit|dead}_southeast.png
클립     Move 10fps 루프 · Attack 0.8초(windup 0.6 + recovery 0.2) · Hit 0.3초 · Dead 0.8초(사라지기 1초 − 0.2)
```

- 🔴 **92 칸으로 떼면 잘린다** — 공격 f2 22px · 피격 f4 14px · 사망 3px(기어 가는 몸이 왼쪽으로 뻗는다). 적은 무기 앵커가 없어 124 그대로
- 발밑: 이동 91~93 · 공격 90~92 · 피격 90~92 → 92 로 정렬(each), 사망은 첫 프레임 기준(first)
- 공격 f6(검 최고점) → f7(내리침 · 검 사라짐) 사이가 0.6초 — **피해 시각이 그림의 내리침과 맞는다**
- 📌 공격 재생성(2 gen)은 플레이에서 어색하다고 할 때만

