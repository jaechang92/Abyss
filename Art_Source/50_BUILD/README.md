# 50_BUILD — 조립기

> `20_SUBJECTS` × `30_BLOCKS` × `40_TOOLS/<도구>` → `assembled/`
> **손으로 이어 붙이지 않는다.** 한 글자만 달라져도 다른 그림이 나온다.

## build.py 가 해야 할 일 (gen2·gen3에서 물려받은 요구사항)

| | 하는 일 | 왜 |
|---|---|---|
| 1 | 블록을 대상별로 **골라** 붙인다 (전부 붙이지 않는다) | PixelLab은 문구가 길면 희석된다 |
| 2 | **한글 주석(`#`·`[…]`)을 걷어낸다** | 안 걷으면 모델이 지시로 읽는다 |
| 3 | **문구 길이를 검사**한다 (상한 초과 시 경고) | gen3가 이미 넣어 뒀던 검사 |
| 4 | 대상마다 `assembled/<대상>__<파트>.json` 을 낸다 | API·MCP에 그대로 보낼 수 있게 |
| 5 | `assembled/00_ALL.md` 에 표로 모은다 | 파일을 하나씩 열지 않아도 되게 |
| 6 | 🔴 **금지 어휘 검사** — `00_SOURCE/forbidden-vocabulary.md §1` 의 말이 섞이면 실패시킨다 | gen1~3이 샜던 자리를 자동으로 막는다 |
| 7 | 🆕 **스타일 지정을 문구로 번역**한다 (`40_TOOLS/…/params.txt [STYLE-WORDS]`) | v2에 `outline`/`shading`/`detail`/`view` 칸이 없다. 매핑에 없는 값은 **경고** — 조용히 빠지면 "안 실었다"와 "모델이 무시했다"를 구분 못 한다 |
| 8 | 🆕 요청 본문과 **파이프라인 메타를 가른다** (`_`로 시작하면 메타) | `_palette`·`_reject_if`는 API에 보내는 것이 아니다. 섞어 보여 주면 손으로 옮겨 담을 때 없는 칸을 찾게 된다 |

### 🆕 2026-09-06 Pro(v2) 전환이 이 파일에 남긴 것

| 없어진 칸 | 어디로 갔나 |
|---|---|
| `color_image` | 후처리 `Tools/ArtPipeline/enforce_palette.py` (`_palette`가 그 입력) |
| `negative_description` | 조립본 `.md`의 **「다시 뽑는 조건」** 체크리스트 (`_reject_if`) |
| `outline`/`shading`/`detail`/`view` | `[STYLE-WORDS]`를 거쳐 **문구 끝**에 붙는다 |
| `image_size` 문자열 | `{"width":…, "height":…}` **객체** |

> 🔑 **문구 예산의 청구서** — 스타일 지정이 필드(공짜)에서 문구로 내려오며 배경 파트마다 **7단어**를 먹는다.
> 그대로 두면 내용 예산이 40 → 33으로 조용히 줄어, 첫 빌드에서 멀쩡하던 파트 4개가 초과 경고를 냈다
> (내용은 한 글자도 안 늘었는데). 그래서 `STYLE_WORD_ALLOWANCE`로 **예산 밖에서 센다** —
> `[WORD-BUDGET]`은 여전히 *내용에 쓸 단어 수*라는 뜻이다.
> ⚠️ 총 문구는 실제로 길어졌으니 공짜는 아니다. 앵커에서 스타일 지정이 안 걸리면 여기를 의심할 것.

## 산출물은 비추적이다

`assembled/`는 `.gitignore` 대상이다 — 원본 블록과 `build.py`만 있으면 언제든 다시 만들어진다.
**원본과 산출물이 한 폴더에 섞이면 어느 것이 손으로 쓴 것인지 눈으로 구분이 안 된다.**

## 쓰는 법

```bash
python Art_Source/50_BUILD/make_palettes.py                        # ① 팔레트 먼저
python Art_Source/50_BUILD/build.py                                # ② 전 씬
python Art_Source/50_BUILD/build.py stage1_rift_entrance           # ② 한 씬만
#   ③ 뽑는다 (PixelLab — 계정이 필요해 사람이 한다)
python Tools/ArtPipeline/enforce_palette.py <뽑은것>.png --scene stage1_rift_entrance   # ④ 집행
```

🔴 **①이 먼저다.** 근거는 2026-09-06에 바뀌었다 — 예전에는 `color_image`가 명령이라서였고,
Pro(v2)에는 그 칸이 없다. 지금은 **팔레트가 앵커를 고르는 판정 기준**이기 때문이다.
기준 없이 뽑은 앵커는 무엇과 비교해 고를지가 없고, 그 한 장이 씬 전부의 화풍을 정한다.

🔴 **④를 건너뛸 수 없다.** 생성 단계에 강제 장치가 없으므로 **여기가 P1~P5가 지켜지는 유일한 자리**다.
그리고 이 단계가 ③의 판정을 숫자로 거든다 — 옮긴 거리(ΔE)가 크면 그 그림은 팔레트를 안 따른 것이라
스냅해 쓰지 말고 다시 뽑는다. ⚠️ **ΔE 0이 합격이 아니다** — 램프 10단 중 2단만 쓴 납작한 그림도
거리가 0으로 나온다. 「쓰인 색」 줄을 같이 본다.

`make_palettes.py`도 검사를 한다 — `10_BIBLE/04-palette.md`의 P1·P4·S3을 직접 잰다
(순수 무채 / 순검정·순백 / 축 6색과의 거리·채도).
`hidden_first_fallen`만 `NEUTRAL_EXEMPT`로 면제된다 — 무채색이 연출이 아니라 설정인 씬이다.

## 씬 파일의 형식

씬은 **`20_SUBJECTS/scenes/<씬>.md` 한 파일**이다. 사람이 읽는 문서와 빌드 입력이 같이 있다 —
한 대상에 두 파일을 두면 반드시 갈린다.

`<!-- BUILD-INPUT -->` 아래만 블록으로 읽고 위쪽 산문은 통째로 무시한다.
그 아래에는 `[BLOCK]`과 `#` 주석만 둔다.

| 블록 | 뜻 |
|---|---|
| `[USE-SCENE]` | 적힌 씬 블록을 **전부** 붙인다 |
| `[USE-SCENE-FIRST]` | **먼저 채워져 있는 하나**만 쓴다 (폴백 사슬) |

## 첫 실행이 잡은 것 — 검사가 값을 했다

| 걸린 것 | 무엇이었나 |
|---|---|
| `ruined` (금지 어휘) | `[TONE]`의 **`not ruined`**. 🔑 고치고 보니 검사가 옳았다 — 부정 서술은 이미지 모델에 약하다. 모델은 "ruined를 빼라"가 아니라 `ruined`를 읽는다. **`still standing`**으로 바꿨다 |
| 표(`\|`) 섞임 | 규칙을 설명하는 **주석 자체**에 `\|`가 있었다. 검사가 주석을 건너뛰게 고쳤다 |
| 문구 46단어 | `prop_hazard`가 `GROUND-MATERIAL`과 `MATERIAL`을 **둘 다** 실었다. 타일셋만 폴백이고 이미지 파트에는 그 수단이 없었다 → `[USE-SCENE-FIRST]` 신설 |
| 문구 51·49단어 | 같은 말을 세 곳이 반복 — `MATERIAL`의 먼지와 `MOTIF(left)`의 먼지. 정체는 `SETTING`, 살림은 `MOTIF`로 갈랐다 |

> 💡 **금지 어휘 검사가 처음 잡은 것이 그것을 만든 사람의 문구였다.** 그게 이 검사가 필요한 이유다 —
> gen1~3은 검사가 없어서 `dark fantasy underground ruins`가 넉 달을 살아남았다.

## 참고 구현

`_legacy/gen3-pixellab/prompts_env/build.py`에서 왔다. 조립·한글 차단·길이 검사는 거기서 물려받았고,
**금지 어휘 검사**와 `[USE-SCENE-FIRST]`가 새로 붙었다.
