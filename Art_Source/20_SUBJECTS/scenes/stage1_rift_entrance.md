# `stage1_rift_entrance`

> UI 이름 **균열의 입구** · 보스 `AbyssKeeper` · 5~7방 + 보스 (`08-content-roadmap §6-1`)
> **첫 씬이고 앵커를 여기서 먼저 뽑는다.** 작성 2026-09-06

---

## ① 정체

> **아직 아무것도 안 없어졌다. 위와 같은데 잘못 놓여 있다.**

없는 것: **없음.** 8개 씬 중 유일하다 — 그래서 다음 씬부터의 결여가 읽힌다.

## ② 소설 근거

**잰 것** — 하란이 처음 내려간 층은 **자기 세계의 밑바닥**이다. 다른 세계가 아니라 **익숙한 것이 잘못 놓인 곳**이다
(집필계획 §3 1권 8장: *"1층: 아직 자기 세계다. 익숙한 것들이 잘못 놓여 있다"*).

그 앞의 경계는 이렇게 생겼다:

> 등불을 내밀어도 빛이 아래로 가지 않았다. **빛이 선을 넘는 순간 없어졌다.** — `1권/04-그날 밤.md:146`
> 빛이 닿지 않는 것이 아니라, **빛이 그 선을 넘으면 없어졌다.** — `1권/08-목록.md:199`

## ③ 시각 브리프

| | 배정 | 근거 |
|---|---|---|
| **빛의 거동** | **선을 넘으면 없어진다** — 그라데이션이 아니라 뚜렷한 선에서 끊긴다 | `1권/04:146`·`08:199` |
| **램프 위치** | 중간 | `01-world-identity §5` |
| **잔여 색상** | 차가운 회청 | P2 |
| **층 강조** | **없음** | 첫 화면 |
| **재질** | 돌 · **익숙한 살림이 잘못 놓인 것** | W2 |
| **레이어** | sky · far · mid · near (기본형) | `06 §2` |

### 🔴 gen3에서 바꾸는 것 — 셋 다 새 규약 위반이다

| gen3 | 위반 | 새 브리프 |
|---|---|---|
| `collapsed temple ruins at the mouth of a rift` | **W2** (폐허 아님) · **W4** (`temple`·`rift` 0회 어휘) | 무너진 유적이 아니라 **어제까지 멀쩡했던 곳.** 살림이 그대로인데 자리가 어긋나 있다 |
| `one pale shaft of light from far overhead` | 🔴 **L1** (광원 금지) · **L2** (방향 그림자) | 광원 없음. 빛은 **선에서 끊길** 뿐 어디서도 안 온다 |
| `dark moss` (팔레트 `3A4A38`·`55663F`) | 🔴 **P5** (어비스에 초록 없음) | 제거. 초록은 시엔과 지상 전용이다 |

> 💡 **「유적」을 버리는 것이 이 씬에서 가장 큰 변화다.**
> 유적은 오래된 것이고 이 씬은 **어제까지 사람이 쓰던 곳**이다.
> 익숙한 것이 조금씩 어긋나 있는 쪽이 무너진 신전보다 무섭고, **소설 1층이 정확히 그렇다.**

### 살아남는 것 — gen3의 `[STORY]` 둘

| gen3 소재 | 판정 |
|---|---|
| `a stairway that ends in open air` | ✅ **유지.** 계단이 하강의 유일한 형식이고(C2), *"위를 향하다 만 것"*은 소설의 삼천 단과 이어진다 |
| `a wall of tally marks left by someone counting days` | ✅ **유지 · 강화.** 🔑 소설 전체가 **세는 이야기**다. `3권/21`의 **벽의 글자가 줄어드는** 것으로 확장할 수 있다 |
| `a shattered seal slab` | ❌ **버린다.** 「봉인」은 소설 어휘가 아니다. 대신 **경계 그 자체** — 빛이 끊기는 선 |

## ④ 팔레트 1차안

**무채 회청 램프 9단 + 가장 밝은 1 = 10색. 층 강조 없음.**

```
0B0E11   141A20   212A33   2F3B45   435059
5E6B72   7C8A8C   99A6AC   BAC4C9   D2D9DB
```

| 검사 | 결과 |
|---|---|
| **P4** 순검정·순백 아님 | ✅ `0B0E11` / `D2D9DB` |
| **P1** 잔여 채도 있음 | ✅ 전 단계가 청색 쪽으로 치우쳐 있다 |
| **P5** 초록 없음 | ✅ gen3의 이끼 2색 제거 |
| **S3** 축 6색과 명도·채도에서 갈림 | ✅ 채도가 전부 매우 낮다. 축 6색은 전부 고채도 |
| **P2** 씬 잔여가 회청으로 일관 | ⚠️ gen3의 `A6B0AC`(초록 쪽)·`C9CBBF`(누런 쪽)를 청색 쪽으로 되돌렸다 — **stage3의 누렁과 겹치는 것을 막기 위해서** |

> ⚠️ **이 열 개는 손으로 고른 1차안이다.** 앵커를 뽑은 뒤 `measure_consistency.py`로 재서 조정한다.
> 🔴 **팔레트가 앵커보다 먼저여야 한다** — `color_image`는 명령이라 앵커를 뽑을 때 이미 있어야 한다.
>
> 💡 **층 강조가 없는 것이 이 씬의 설계다.** stage1은 나머지 씬이 벗어나는 **기준선**이고,
> 축 6색(신호)과 가장 안 부딪히는 자리다. 여기서 플레이어가 *"무엇이 내 캐릭터인가"*를 배운다.

## ⑤ 뽑을 것 — 14 요청

```
배경  bg_sky  bg_far  bg_mid  bg_near  bg_boss_arena
타일  tileset_ground  tileset_wall  tile_platform
프롭  prop_decor  prop_hazard  prop_landmark
      prop_interactive ×3
```

**앵커 파트**: `bg_mid` — 여러 장 뽑아 **사람이 한 장 고른다.** `anchors/stage1_rift_entrance.png`

> 🔴 **이 한 장이 스테이지 1의 화풍 전부를 결정하고, 나머지 스테이지 앵커도 이것을 참조한다.**
> 여기서 타협하면 130장이 그 타협을 물려받는다. 앵커 다시 뽑는 값 < 씬 하나 다시 뽑는 값.

**앵커 판정**: `10_BIBLE/03-light.md §5` 체크리스트를 그대로 쓴다. 이 씬에서 특히 볼 것 —

```
☐ 위에서 내려오는 빛줄기가 있는가        → 있으면 탈락 (gen3가 이걸 명시적으로 요청했었다)
☐ 이끼·풀·초록이 있는가                  → 있으면 탈락 (P5)
☐ 무너진 신전·기둥·유적으로 보이는가      → 그러면 탈락 (W2)
☐ 화면 중앙이 캐릭터가 설 만큼 조용한가
```

## ⑥ 미결

| 미결 | |
|---|---|
| 「잘못 놓인 살림」을 32px 타일과 48px 프롭으로 어떻게 보여 주나 | `05-material` + 프롭 카탈로그 |
| 빛이 끊기는 선을 배경 레이어 어디에 두나 | 앵커 뽑아 보고 |
| `bg_boss_arena`(AbyssKeeper)를 이 팔레트로 가나 | 보스 씬은 강조가 필요할 수 있다 |

---

<!-- BUILD-INPUT -->

# ══════════════════════════════════════════════════════════════
# 아래가 50_BUILD/build.py 가 읽는 구역이다. 위쪽 산문은 통째로 무시된다.
# 🔴 여기에는 [BLOCK] 과 '#' 주석만 둔다. 표(|)가 섞이면 빌드가 멈춘다.
# 🔴 한글은 반드시 '#' 뒤에만. 본문에 섞이면 빌드가 멈춘다.
# ══════════════════════════════════════════════════════════════

[ANCHOR-PART] bg_mid

[SETTING]
# 「폐허·고대 유적」을 버린 자리. 유적은 오래된 것이고 이 씬은 어제까지 쓰던 곳이다.
the underside of an ordinary town, everyday things set down wrong

[MATERIAL]
# 재질만 남긴다. 정체는 SETTING 이 말하고 살림은 MOTIF(left) 가 말한다 —
# 셋이 같은 말을 하면 문구 예산만 먹고 서로 흐려진다.
plain grey stone

[GROUND-MATERIAL]
# 🔴 물건 없이 면만. 32px 타일에 물건이 들어가면 바닥마다 반복된다.
flat worn stone slabs, one even surface

[WALL-MATERIAL]
# gen3 는 벽이 배경 재질을 그대로 썼다. 이제 따로 적는다.
a plain stone wall face, quiet and even

[SKY]
# 🔴 구름을 그리지 않는다(L7). 하늘은 텍스처가 아니라 면이다.
a low flat grey ceiling of nothing

[LIGHT]
# 🔴 광원을 그리지 않는다(L1). 이 씬의 빛은 선에서 끊길 뿐 어디서도 안 온다.
light stops at one hard straight edge

[MOTIF] left

[STORY]
# 인터랙티브 프롭. 줄마다 한 장씩 뽑는다([SPLIT-SCENE]).
# 셋 다 소설에서 왔다 — 삼천 단 계단 / 벽의 글자(3권 21장) / 빛이 끊기는 선(1권 4장).
a stairway that stops partway up
a wall covered in tally marks left by someone counting days
a doorway where the light ends in one straight edge

[TILE-OUTER]
dark empty air

[PARTS]
bg_sky, bg_far, bg_mid, bg_near, bg_boss_arena,
tileset_ground, tileset_wall, tile_platform,
prop_decor, prop_hazard, prop_interactive, prop_landmark
