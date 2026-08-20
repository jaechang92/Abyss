# HOWTO — 이미지 생성툴에 지시하는 순서

**"그래서 지금 뭘 하면 되나"** 만 적은 문서다. 위에서부터 그대로 따라 하면 된다.

| 문서 | 답하는 질문 |
|---|---|
| `README.md` | 무엇이 **어디** 있나 (폴더·명명 규약) |
| `GUIDELINE.md` | **왜** 그렇게 하나 (근거·실측값·실패 이력) |
| **`HOWTO.md`** ← 여기 | **지금 뭘** 하나 (절차) |

---

## 폴더 순번 = 작업 순번

```
0_base/  ->  1_prompts/  ->  2_raw/  ->  3_cut/  ->  4_frames/  ->  5_curated/  ->  Assets/
첨부할 그림    넣을 글       받은 그림    배경 제거    조각 내기      골라 담기       게임
```

`1_prompts/` 안에도 같은 규칙이 있다.

```
1_blocks/  ->  2_forms/  ->  build.py  ->  3_assembled/
공용 블록      폼별 블록      이어 붙임      <- 모델에 넣는 건 여기뿐
(수정 금지)    (여기를 고친다)                (손대지 말 것)
```

> 💡 **매일 쓰는 폴더는 `3_assembled/` 하나뿐이다.** 프롬프트를 안 고치는 날에는
> 1·2와 `build.py`를 열 일이 없다.

---

## 0단계 — 시작 전 확인 (세션마다 한 번)

```bash
# ① 기준 이미지가 있는가
ls Art_Source/0_base/dark_blade.png

# ② 프롬프트를 최신 상태로 만든다 (블록을 안 고쳤어도 한 번 돌려서 손해 없다)
python Art_Source/1_prompts/build.py
```

`build.py`가 17개 파일 + 카탈로그 한 장을 찍어 내면 준비 끝이다. 에러가 나면 어느 파일
몇 번째 줄인지 알려 준다 — 고치고 다시 돌린다.

> 📋 **프롬프트를 한 장에서 다 보고 싶으면 `1_prompts/3_assembled/00_ALL_PROMPTS.md`** 를 연다.
> 17개 조립본이 목차·저장경로·판정 기준과 함께 코드블록으로 들어 있어, **절마다 복사 버튼 한 번**이면 된다.
> 아래 1단계 표의 번호가 그 문서의 절 번호와 같다.
>
> 🔴 단, **절마다 따로 복사한다.** 문서 전체나 `1_blocks/` 를 통째로 붙여넣으면 안 된다 —
> 프레이밍 블록 셋이 *three-quarter* / *not a three-quarter* / *no character* 로 **서로를 부정한다.**

---

## 1단계 — 이번에 뽑을 것을 정한다

리깅용으로 남은 작업은 **9장**이다. 몸 4장 + 무기 5장.

| # | 폼 | 무엇 | 넣을 프롬프트 (`1_prompts/3_assembled/`) | 받아서 저장할 곳 (`2_raw/`) |
|:--:|---|---|---|---|
| 1 | dark_blade | 몸 | `dark_blade_rig.txt` | `dark_blade/dark_blade_rig_1.png` |
| 2 | dark_blade | 대검 | `dark_blade_weapon_greatsword.txt` | `dark_blade/dark_blade_weapon_greatsword_1.png` |
| 3 | void_archer | 몸 | `void_archer_rig.txt` | `void_archer/void_archer_rig_1.png` |
| 4 | void_archer | 활 | `void_archer_weapon_longbow.txt` | `void_archer/void_archer_weapon_longbow_1.png` |
| 5 | ancient_shield | 몸 | `ancient_shield_rig.txt` | `ancient_shield/ancient_shield_rig_1.png` |
| 6 | ancient_shield | 탑실드 | `ancient_shield_weapon_towershield.txt` | `ancient_shield/ancient_shield_weapon_towershield_1.png` |
| 7 | ancient_shield | 단검 | `ancient_shield_weapon_shortsword.txt` | `ancient_shield/ancient_shield_weapon_shortsword_1.png` |
| 8 | void_thrower | 몸 | `void_thrower_rig.txt` | `void_thrower/void_thrower_rig_1.png` |
| 9 | void_thrower | 투창 | `void_thrower_weapon_javelin.txt` | `void_thrower/void_thrower_weapon_javelin_1.png` |

> 🔴 **1번(dark_blade 몸) 한 장만 뽑고 거기서 멈춰 판정한다.** 기준 폼이라 여기서 안 되면
> 나머지 8장도 똑같이 안 된다. 한 장으로 확인하는 비용이 가장 싸다 —
> 9장 다 뽑고 나서 발견하면 9장을 버린다.

전신 쇼케이스·행 이미지가 필요하면 `<form>.txt` · `<form>_sheet.txt`를 같은 방식으로 쓴다.

---

## 2단계 — 한 장 뽑는 절차

### S1. 새 대화를 연다

> 🔴 **한 대화에서 여러 장을 이어 뽑지 말 것.** 앞에서 생성한 그림이 문맥에 남아
> 다음 장에 섞인다. 궁수를 뽑던 대화에서 방패병을 뽑으면 방패병이 홀쭉해지고,
> **무기를 뽑던 대화에서 몸을 뽑으면 그 무기가 손에 들려 나온다.**
> **한 장 = 한 대화.** 같은 그림을 다시 뽑는 재시도만 같은 대화에서 한다.

### S2. 기준 이미지를 첨부한다

`Art_Source/0_base/dark_blade.png` 를 첨부한다. **네 종류 프롬프트가 모두 이 그림 하나를 쓴다.**

> 💡 같은 그림인데 **베끼는 범위가 프롬프트마다 다르다.** 몸 프롬프트는 *"저 검은 네가
> 베낄 대상이 아니다"* 라고 말하고, 무기 프롬프트는 *"저 검이 그림체 본보기다"* 라고 말한다.
> 그 문장이 프롬프트 안에 이미 들어 있으니 **첨부만 하고 아무 설명도 덧붙이지 않는다.**

### S3. 프롬프트 전문을 붙여넣는다

둘 중 편한 쪽에서 복사한다. 내용은 같다.

- `3_assembled/<파일명>.txt` 를 열어 **처음부터 끝까지 전부**
- `3_assembled/00_ALL_PROMPTS.md` 의 해당 절 **코드블록** (미리보기 복사 버튼)

> 🔴 **아무 말도 덧붙이지 말 것.** *"좀 더 멋있게"*, *"배경은 투명으로"* 같은 한 문장이
> 블록의 지시와 충돌하면 모델은 둘 중 하나를 버린다 — 어느 쪽을 버릴지는 알 수 없다.
> 고치고 싶은 게 있으면 `2_forms/form_<id>.txt`를 고치고 `build.py`를 다시 돌린다.

### S4. 후보를 3~4장 받는다

한 장만 받고 끝내지 않는다. **큐레이션이 이 파이프라인의 본체다** — 한 장뿐이면 고를 게 없다.
같은 대화에서 이어서:

```
Generate 3 more variations of the same image, same prompt, same reference.
```

### S5. 3단계 판정을 통과한 것만 저장한다

파일 이름은 1단계 표를 그대로 쓴다. **`<formId>`는 `FormData.formId`와 같은 값**이라
오타가 나면 나중에 조용히 안 붙는다 — 새 이름을 만들지 말고 표에서 복사한다.

---

## 3단계 — 저장 전에 눈으로 보는 판정

자동 측정으로는 안 잡히는 것들이다. **실루엣 안쪽 문제라 팔레트도 알파도 정상으로 나온다.**

### 몸(`_rig`) — 6가지

| ☐ | 확인 | 아니면 |
|---|---|---|
| ☐ | **양손이 비었는가** — 쥔 것·팔에 매단 것·등에 멘 것 전부 | 재시도 **A** |
| ☐ | 근접 팔과 몸통 사이로 **배경이 비치는가** (팔 전체 길이에 걸쳐) | 재시도 **B** |
| ☐ | 두 다리 사이로 **배경이 비치는가** | 재시도 **B** |
| ☐ | **정측면**인가 — 얼굴 방향이 아니라 **어깨선**으로 본다 | 재시도 **C** |
| ☐ | 어깨·팔꿈치·골반·무릎이 **점으로 짚이는가** | 재시도 **D** |
| ☐ | 천이 **정지**해 있는가 (펄럭이면 뼈로 흔들 때 두 번 흔들린다) | 재시도 **E** |

### 무기(`_weapon_*`) — 4가지

| ☐ | 확인 | 아니면 |
|---|---|---|
| ☐ | **손·손가락·팔이 안 나왔는가** (모델이 가장 자주 어긴다) | 재시도 **F** |
| ☐ | 그립(감개·띠·보스)이 **가려지지 않고 보이는가** | 재시도 **G** |
| ☐ | 프레임에 **잘린 데가 없는가** (활 양끝·투창 양끝) | 재시도 **H** |
| ☐ | **하나만** 그려졌는가 (세트·거치대·작은 복사본 없이) | 재시도 **I** |

### 공통 — 4가지

| ☐ | 확인 | 아니면 |
|---|---|---|
| ☐ | 배경이 **균일한 마젠타**인가 (그라데이션·비네트 없이) | 재시도 **J** |
| ☐ | 발밑에 **바닥·그림자**가 안 붙었는가 | 재시도 **J** |
| ☐ | 턴어라운드가 아니라 **한 포즈 한 장**인가 | 재시도 **K** |
| ☐ | 얼굴이 **안 보이는가** | 다시 뽑는다 (전제가 깨진다) |

---

## 4단계 — 재시도할 때 하는 말

같은 대화에서 아래 문구를 **그대로** 붙여넣는다. 짧게, **한 번에 한 가지만** 말하는 것이 요령이다 —
여러 개를 한꺼번에 말하면 모델이 하나만 고치고 나머지를 놓친다.

| | 증상 | 그대로 붙여넣을 문구 |
|:--:|---|---|
| **A** | 무기를 들고 나왔다 | `The character is holding something. Redraw with both hands completely empty and open. The weapon belongs to a separate image and must not appear here.` |
| **B** | 팔·다리가 몸에 붙었다 | `Open a clear gap of background between the near arm and the torso, and between the two legs, along their whole length. Swing the near arm forward and the far arm back at the shoulder.` |
| **C** | 3/4 뷰로 나왔다 | `This is still a three-quarter view. Redraw as a pure flat side profile, the camera exactly perpendicular, no part of the chest turned toward the viewer.` |
| **D** | 어깨·골반이 묻혔다 | `The shoulder and hip joints are buried under armour and cloth. Make each joint readable as a distinct point.` |
| **E** | 천이 날린다 | `The cloth is blowing. Let every cloak, tabard and hem hang straight down at rest, settled and still.` |
| **F** | 무기에 손이 딸려 왔다 | `Remove the hand. Draw the object completely alone: no hand, no fingers, no arm, nothing holding it and nothing touching it.` |
| **G** | 그립이 안 보인다 | `The grip is not visible. Draw the wrapped grip clearly as a distinct band so its centre can be located.` |
| **H** | 끝이 잘렸다 | `The object is cropped by the frame. Fit the whole object inside the frame with an even margin on all four sides.` |
| **I** | 여러 개가 나왔다 | `Draw one single object only: no set, no rack, no variants side by side, no smaller inset copies.` |
| **J** | 배경·발밑이 지저분하다 | `The background is not uniform. Use one flat solid magenta #FF00FF with no gradient, no vignette, no ground, no cast shadow and nothing underneath.` |
| **K** | 턴어라운드가 나왔다 | `Draw a single character in a single pose. Do not draw a turnaround, multiple views, or front and back views.` |

> ⚠️ **같은 증상으로 3번 이상 재시도하면 대화를 버리고 새로 연다.** 문맥에 실패한 그림이
> 쌓이면 모델이 그것을 기준으로 삼는다. 새 대화에서도 같으면 프롬프트 쪽 결함이니
> `GUIDELINE.md` §7·§8을 볼 것.

---

## 5단계 — 후처리 (배경 제거 → 측정 → 선택)

### 배경 제거 (`2_raw` → `3_cut`)

```bash
python Tools/ArtPipeline/chroma_cutout.py Art_Source/2_raw/dark_blade/dark_blade_rig_1.png --out-dir Art_Source/3_cut
```

여러 장을 한 번에 넘겨도 된다. 가장자리에 마젠타가 남으면 `--tol` 을 낮춘다(기본 0.28).

### 행 이미지만 — 프레임 분리·일관성 측정 (`3_cut` → `4_frames`)

```bash
python Tools/ArtPipeline/measure_consistency.py Art_Source/3_cut/sheet_idle-walk-attack-cut.png --expect 3 --save-frames Art_Source/4_frames/dark_blade
```

**팔레트 일치 80% 아래면 다시 뽑는다** (기준선 89.7%). 몸·무기 단독은 이 단계가 없다.

### 고른 것을 `5_curated`로

```bash
cp Art_Source/3_cut/dark_blade_rig_1-cut.png Art_Source/5_curated/dark_blade/rig_base.png
```

이름 규약 — 몸은 **`rig_base.png` 고정**(`idle` 아님), 무기는 `weapon_<무기id>.png`.

| 폼 | 무기 id |
|---|---|
| `dark_blade` | `greatsword` |
| `void_archer` | `longbow` |
| `ancient_shield` | `towershield` · `shortsword` |
| `void_thrower` | `javelin` |

---

## 6단계 — 결과가 계속 나쁠 때 어디로 돌아가나

| 증상 | 돌아갈 곳 |
|---|---|
| 뭘 고쳐도 결과가 그대로다 | **`build.py`를 안 돌렸을 가능성이 가장 크다.** `3_assembled/` 파일의 수정 날짜부터 볼 것 |
| 그 폼만 계속 무기가 나온다 | `2_forms/form_<id>.txt`의 `[RIG-EXCLUDE]` 절 → `build.py` (GUIDELINE §9) |
| 폼 색·장비를 바꾸고 싶다 | `2_forms/form_<id>.txt`의 `[BODY]` → `build.py` |
| 무기 방향·피벗 자리를 바꾸고 싶다 | `2_forms/form_<id>.txt`의 `[WEAPON-IMAGE]` → `build.py` |
| 폼 넷 전부에서 같은 문제가 난다 | `1_blocks/` — 단 **수정 금지 블록**이다. 손대기 전에 `GUIDELINE.md` §1을 읽을 것 |

---

## 하지 말 것 (요약)

| 🚫 | 왜 |
|---|---|
| **`1_blocks/`를 통째로 이어 붙여 쓰기** | 프레이밍 셋이 *three-quarter* / *not a three-quarter* / *no character* 로 **서로를 부정한다.** 용도마다 하나씩만 들어가야 하고 그 조합을 `build.py`가 만든다 |
| `00_ALL_PROMPTS.md`를 통째로 붙여넣기 | 그 안에 17개가 다 들어 있다 — 서로 다른 용도라 섞으면 위와 같은 일이 난다. **절 하나만** 복사한다 |
| `3_assembled/`의 파일을 손으로 고치기 | 다음 `build.py`에 덮어써진다. 원본은 `1_blocks/`·`2_forms/`다 |
| 프롬프트에 자기 말 덧붙이기 | 블록 지시와 충돌하면 모델이 둘 중 하나를 버린다 |
| 한 대화에서 여러 장 뽑기 | 앞 그림이 다음 그림에 섞인다 (특히 **무기 → 몸** 순서) |
| 기준 이미지 첨부 빼먹기 | 프롬프트가 *"저 그림을 봐라"* 라고 하는데 볼 게 없어 **정체성이 죽는다** |
| 블록을 `cat`으로 이어 붙이기 | 사람용 한글 주석이 모델에 **지시로 넘어간다.** `build.py`가 걷어낸다 |
| 1번 건너뛰고 9장 몰아 뽑기 | 안 되는 상태로 뽑으면 9장을 통째로 버린다 |

---

## 참조

- `README.md` — 폴더 구조·명명 규약
- `GUIDELINE.md` — §7 리깅 base의 근거, §8 무기 단독의 근거, §9 폼 블록 절 구분
