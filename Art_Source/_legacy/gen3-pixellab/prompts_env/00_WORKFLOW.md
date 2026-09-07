# 환경 아트 제작 절차 — PixelLab

> 배경·타일·프롭을 끝까지 만드는 순서.
> 프롬프트는 `build.py` 가 조립한다. 손으로 이어 붙이지 않는다.

## 파일 구성

| 파일 | 역할 |
|---|---|
| `01_world.txt` | **세계 정체성 SoT.** 스테이지가 달라도 안 변하는 것. 고치려면 여기서만 |
| `02_params.txt` | 앵커·팔레트 규약 + 종류별 생성 파라미터 |
| `scenes/*.txt` | 씬 8종 — 스테이지 5 + 히든 + 로비 + 타이틀 |
| `parts/*.txt` | 에셋 종류 12종 — 배경 5 · 타일 3 · 프롭 4 |
| `palettes/_palettes.txt` | 씬별 강제 팔레트 (hex) |
| `make_palettes.py` | 위 목록을 `color_image` 용 PNG 로 굽는다 |
| `build.py` | 전부 합쳐 `assembled/` 에 낸다. **한글 유출·문구 길이 검사 포함** |
| `assembled/` | 산출물. git 비추적 — 언제든 다시 만들어진다 |

```bash
python Art_Source/prompts_env/make_palettes.py     # 팔레트 PNG (한 번만)
python Art_Source/prompts_env/build.py             # 전 씬 (94개 요청)
python Art_Source/prompts_env/build.py lobby       # 지정 씬만
```

`assembled/00_ALL.md` 에서 씬별 표로 들어간다. 웹 앱을 쓰면 그 표를 보고 칸을 채우고,
API·MCP 를 쓰면 같은 폴더의 `<scene>__<part>.json` 을 그대로 보낸다.

## 산출물 규모

| 씬 | 파트 | 뽑을 장수(변형 포함) |
|---|---:|---:|
| 스테이지 1~5 (각) | 14 | 약 20 |
| 히든 · 로비 | 11 / 9 | 약 15 |
| 타이틀 | 2 | 2 |
| **합계** | **94 요청** | **약 130장** |

## 순서

### 1. 팔레트를 먼저 굽는다

```bash
python Art_Source/prompts_env/make_palettes.py
```

🔴 **이게 첫 단계인 이유** — 팔레트가 이 파이프라인에서 유일하게 **강제력이 있는** 장치다.
프롬프트는 부탁이지만 `color_image` 는 명령이다. 폼 액센트(주황·보라·금·청록)와
배경이 부딪히는 문제를 여기서 끝낸다.

### 2. 씬마다 앵커 한 장 (pixflux)

`assembled/<scene>__bg_mid.json` 으로 **여러 장 뽑아 사람이 한 장을 고른다.**
고른 것을 `Art_Source/env/anchor/<scene>.png` 로 저장한다.

확인할 것:
- 팔레트가 지켜졌는가 (특히 스테이지 2·3·5)
- **화면 중앙이 가장 어두운가** — 캐릭터가 그 앞에 설 자리다
- 가로 양끝이 이어지는가
- 인물·생물이 없는가

🔴 **여기서 타협하면 그 씬의 나머지 20장이 그 타협을 물려받는다.**
앵커 다시 뽑는 비용 < 씬 하나 다시 뽑는 비용.

### 3. 나머지 전부 (bitforge + style_image)

앵커를 `style_image` 로 물리고 `style_strength = 50` 으로 돌린다.
화풍이 갈리면 65까지만 올린다 — 100 에 가까우면 앵커의 구조까지 베껴
타일의 심리스가 깨진다.

### 4. 타일셋은 웹 도구로 따로

`tileset_*` 파트는 API 가 아니라 **Create Tileset** 도구다.
`inner / transition / outer` 셋을 넣고 Wang 타일셋을 받은 뒤,
**dual-grid 15-tileset 또는 3x3 형식으로 export** 하면 Unity Tilemap 에 그대로 들어간다.

🔴 **타일 재질은 배경 재질과 다른 블록에서 온다.** 지면은 씬의 `[GROUND-MATERIAL]` 을 쓰고,
없으면 `[MATERIAL]` 로 떨어진다. 배경 재질에는 `toppled pillars`·`rotted banners` 같은
**물건**이 섞여 있는데, 160x96 배경에서는 풍경이지만 32px 타일에서는 그 물건 하나가
타일을 다 차지해 **바닥을 깔 때마다 반복된다.** 자세한 이유는 `parts/tileset_ground.txt`.

⚠️ **벽(`tileset_wall`)은 아직 `[MATERIAL]` 을 그대로 쓴다** — 같은 문제가 남아 있다.
지면 어휘(`floor slabs`)를 벽에 그대로 쓸 수는 없으므로 `[WALL-MATERIAL]` 을 따로 써야 한다.

📌 지면의 `transition` 은 **플레이어가 서는 면**이라 `broken edge` 가 아니라 평평한 한 줄을
요구한다. Unity Tilemap 콜라이더가 격자 칸 경계라, 윗면이 울퉁불퉁하면 보이는 바닥과
밟히는 높이가 어긋난다. 벽은 반대로 `broken edge` 가 맞다.

### 5. 스테이지 1 을 끝까지 관통한다

🔴 **8씬을 다 뽑기 전에 스테이지 1 하나를 Unity 재생까지 끝낸다.**
임포트·슬라이스·패럴랙스 배치에서 나올 문제를 130장 뽑은 뒤에 발견하면 전부 다시 한다.

### 6. Unity 임포트

| 대상 | 설정 |
|---|---|
| 배경 4층 | Sprite(Single). 패럴랙스 계수는 sky < far < mid < near |
| 타일셋 | Sprite(Multiple) → 격자 슬라이스 → Tile Palette |
| 발판·프롭 | Sprite(Single), 피벗 **Bottom** |

전부 **Filter Mode: Point (no filter)** · **Compression: None**.
픽셀 아트에 Bilinear 가 걸리면 확대할 때 뭉개진다.

## 주의

- 🔴 **한 씬은 한 세션에서 끝낸다.** 패럴랙스는 네 장이 겹쳐 보이는 구조라
  색온도 차이가 그대로 층으로 드러난다
- 🔴 **다른 씬의 앵커를 style_image 로 쓰지 않는다.** 예외는 히든 하나뿐이고,
  그건 거울상 영역이라 의도적으로 스테이지 1 앵커를 쓴다(`[STYLE-FROM]`)
- ⚠️ `no_background` 는 200x200 이 넘는 면적에서 투명이 아니라 빈 배경이 될 수 있다.
  프롭·발판은 전부 그 아래라 문제되지 않는다
- ⚠️ `negative_description` 은 **pixflux 에서 deprecated** 다. 앵커 생성에는 안 먹으므로
  앵커에서 인물이 나오면 부정 프롬프트를 손보지 말고 다시 뽑는다

## 캐릭터 쪽과의 관계

캐릭터는 `Art_Source/prompts/` 가 담당한다(폼 4종, 상태 9종).
두 폴더는 **팔레트에서만 만난다** — 환경이 폼 액센트 4색을 피하는 것이
`palettes/_palettes.txt` 의 존재 이유다. 그 외에는 서로 독립이다.
