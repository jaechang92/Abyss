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
| 6 | 🔴 **금지 어휘 검사** — `00_SOURCE/forbidden-vocabulary.md §1` 의 말이 섞이면 실패시킨다 | **이번에 새로 넣는 것.** gen1~3이 샜던 자리를 자동으로 막는다 |

## 산출물은 비추적이다

`assembled/`는 `.gitignore` 대상이다 — 원본 블록과 `build.py`만 있으면 언제든 다시 만들어진다.
**원본과 산출물이 한 폴더에 섞이면 어느 것이 손으로 쓴 것인지 눈으로 구분이 안 된다.**

## 쓰는 법

```bash
python Art_Source/50_BUILD/make_palettes.py                        # ① 팔레트 먼저
python Art_Source/50_BUILD/build.py                                # ② 전 씬
python Art_Source/50_BUILD/build.py stage1_rift_entrance           # ② 한 씬만
```

🔴 **①이 먼저다.** `color_image`는 명령이라 앵커를 뽑는 시점에 이미 있어야 한다.

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
