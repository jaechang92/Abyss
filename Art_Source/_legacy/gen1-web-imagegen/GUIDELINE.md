# AI 아트 생성 기본 지침

> 🔴 **폐기됨 — 2026-08-26.** 아래 수치·체크리스트는 **웹 image_gen 수동 생성** 기준이다.
> **NovelAI Diffusion V5**로 전환하면서 참조 대상(`_assembled_*.txt`, 앵커 블록)이 전부 삭제됐다 —
> 체크리스트의 항목들이 **가리키는 파일이 이제 없다.**
> 팔레트 일치율 같은 *측정 방법*(`Tools/ArtPipeline/measure_consistency.py`)은 여전히 쓸 수 있지만,
> *기준값과 절차*는 NovelAI 기준으로 다시 실측해야 한다.

---

`dark_blade`를 기준으로 나머지 아트를 만들 때의 작업 규약.
**여기 적힌 수치는 전부 2026-08-20에 실측한 것**이지 추정이 아니다.

---

## 0. 매 생성 전 체크리스트

| | 확인 | 왜 |
|---|---|---|
| ☐ | **base 이미지를 첨부**했는가 (`Art_Source/base/dark_blade.png`) | 앵커 첫 줄이 "Match that reference image"다. 첨부가 없으면 **가리킬 대상이 없어 앵커가 죽는다** |
| ☐ | `_assembled_*.txt`를 **그대로** 붙여넣었는가 | 블록을 손으로 재조립하면 앵커가 미세하게 갈라진다 |
| ☐ | 앵커 블록을 **안 건드렸는가** | 폼마다 앵커가 다르면 같은 사람으로 안 읽힌다 |

> 🔑 **팔레트 일치 89.7%가 나온 근거는 하나뿐이다** — 모든 생성이 *같은 앵커 + 같은 base 이미지*를
> 참조했다는 것. 둘 중 하나라도 흔들리면 그 수치는 재현되지 않는다.

---

## 1. 바꿔도 되는 것 / 안 되는 것

```
_anchor.txt        ❌ 절대 수정 금지 — 스타일·정체성·프레이밍의 SoT
form_<id>.txt      ✅ 수정 가능    — 그 폼의 장비·색·실루엣
_sheet_rules.txt   ❌ 수정 금지    — 실측 결함 2건을 막는 줄이 들어 있다
```

`_sheet_rules.txt`의 두 지시는 취향이 아니라 **버그 대응**이다:

| 실측된 결함 | 막는 지시 |
|---|---|
| 칼끝이 옆 프레임에 걸쳐 **프레임 분리가 3개 대신 1개**로 나왔다 | `wide clear empty magenta gap ... must not cross into a neighbouring pose` |
| 자세마다 **키가 12.7% 흔들려** 애니메이션에서 캐릭터가 위아래로 떤다 | `feet at the same height, identical character scale in all three` |

앵커나 폼 파일을 고쳤으면 **반드시 조립본을 다시 만든다**:

```bash
python Art_Source/prompts/build.py            # 전부
python Art_Source/prompts/build.py void_archer  # 하나만
```

> ⚠️ `cat`으로 직접 붙이지 말 것. 블록 파일에는 사람용 한글 주석(`[...]`)이 있고,
> 그게 모델에 넘어가면 **지시로 읽힌다.** `build.py`가 그것을 걷어낸다.

---

## 2. 뽑은 뒤 — 저장

```
Art_Source/raw/<formId>/<formId>_<n>.png        단일
Art_Source/raw/<formId>/sheet_<상태들>.png      행 이미지
```

- **후보를 여러 장 넣는다.** 큐레이션이 이 파이프라인의 본체다 — 한 장만 있으면 고를 게 없다
- `<formId>`는 `FormData.formId`와 **같은 값**(`dark_blade`·`void_archer`·`ancient_shield`·`void_thrower`).
  파일 이름이 곧 데이터 키라 오타는 조용히 안 붙는다
- **PNG로 받는다.** JPG는 압축 노이즈가 알파 추출 가장자리를 갉는다(실측 확인)

---

## 3. 판정 — 눈이 아니라 숫자로

```bash
python Tools/PixelArt/chroma_cutout.py Art_Source/raw/<form>/<file>.png --out-dir Art_Source/cut
python Tools/PixelArt/measure_consistency.py Art_Source/cut/<file>-cut.png --expect 3 \
       --save-frames Art_Source/frames/<form>
```

**기준선 (dark_blade 실측값)**

| 축 | 값 | 판정 |
|---|---|---|
| 팔레트 일치 | **89.7%** | ✅ 이 근처면 통과 |
| 키 편차 | 12.7% | ⚠️ 자세 차이라 허용. **단 발밑 정렬 필요** |
| 밝기 편차 | 16.9% | ⚠️ 발광 차이라 허용 |
| 알파 잔류 오염 | 0 px | ✅ 마젠타 테두리 없음 |

> 📌 **팔레트가 판정의 중심축이다.** 키·밝기는 자세와 연출에서 자연히 갈리지만,
> 팔레트가 흔들리면 **같은 캐릭터가 아니게 된다.** 80% 아래면 다시 뽑는다.

---

## 4. 실패 유형과 대처 (전부 실제로 겪은 것)

| 증상 | 원인 | 대처 |
|---|---|---|
| 무기가 손에서 **떨어져 있다** | 프롬프트에 파지 지시가 없었다 | 앵커에 이미 `gripping ... firmly` 추가됨. 재생성 |
| 발밑에 **돌바닥·그림자**가 붙었다 | 지형이 스프라이트에 구워진다 | 앵커의 `no ground, no platform, no cast shadow` 확인 |
| 배경 마젠타가 **균일하지 않다** | 그라데이션·비네트 | 앵커의 `perfectly uniform, no gradient no vignette` 확인 |
| 프레임이 **1개로 붙어 나온다** | 무기가 옆 프레임에 걸쳤다 | `_sheet_rules.txt` 포함했는지 확인. 그래도 붙으면 `--expect`로 강제 분할됨 |
| 추출 후 **색이 뒤집힌다**(붉은색→초록) | 알파 램프가 넓어 내부가 반투명 계산됨 | `chroma_cutout.py`에서 해결됨. `--tol`을 낮추면 더 좁아진다 |
| 얼굴이 **보인다** | 정체성 조항이 안 먹혔다 | 다시 뽑는다. 얼굴이 나오면 "같은 사람이 형태를 바꾼 것"이라는 전제가 깨진다 |

---

## 5. 순서 — 한 번에 다 뽑지 않는다

```
① 폼 1종 단일 이미지    앵커가 붙는지 확인      _assembled_<form>.txt
② 같은 폼 행 이미지     3포즈 일관성 확인       _assembled_<form>_sheet.txt
③ 나머지 폼 반복
④ 큐레이션 → curated/  게임에 넣을 것 고르기
```

**①을 건너뛰지 말 것.** 앵커가 안 먹히는 상태로 12장을 뽑으면 12장을 버린다.
한 장으로 확인하는 비용이 가장 싸다.

---

## 6. 다른 폼을 만들 때 지켜야 할 것

넷은 **다른 캐릭터가 아니라 같은 사람이 형태를 바꾼 것**이다(`00-concept.md` USP-2).
그래서 앵커의 `CHARACTER IDENTITY` 블록이 공유된다 — 같은 체격·같은 키·**얼굴은 언제나 안 보임**.

구분은 **색이 아니라 실루엣**으로 낸다. 전투 중에는 피격 플래시와 폼 틴트로 색이 순간 뭉개지고,
색약 대응이기도 하다(`00-concept.md` 디자인 절).

| 폼 | 실루엣 도형 | 강조색 |
|---|---|---|
| 암흑 검사 | 세로 선 (대검) | 불꽃 주황 |
| 공허 궁수 | **곡선** (활) | 심연 보라 `#A36BF5` |
| 고대 방패병 | **사각** (탑실드) | 수호 금색 `#FFCC52` |
| 심연 투척사 | **삼각** (퍼지는 망토) | 청록 `#7AC8BE` |

> 투척사는 설정상 심연 축이지만 궁수와 같은 보라를 쓰면 구분이 안 되어 청록으로 갈랐다.

---

## 참조

- `Art_Source/README.md` — 폴더 구조·명명 규약
- `Docs/adr/008-art-direction-pixel.md` — 🔴 **전제 재검토 필요**(Skul은 픽셀 아트가 아니다)
- `Tools/PixelArt/form_sprite_spec.py` — 절차적 도트 파이프라인(기준선으로 보존)
