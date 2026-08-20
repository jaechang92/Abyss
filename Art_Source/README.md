# Art_Source — 아트 원본·중간 산출물

AI 생성 아트 파이프라인의 작업 공간이다. **`Assets/` 바깥에 있는 것이 의도다.**

> 🔴 **왜 `Assets/` 밖인가**
> Unity는 `Assets/` 아래 모든 파일을 임포트하고 `.meta`를 만든다. 원본은 1.5MB짜리
> 고해상도 PNG이고 후보를 여러 장 뽑으므로, 안에 두면 **임포트 시간과 프로젝트 용량만
> 먹고 게임은 그중 하나도 안 쓴다**(최종본만 `Assets/Art/Sprites/`로 간다).
> 밑줄 접두어(`_source`)로는 안 걸러진다 — Unity가 무시하는 것은 `~`로 끝나거나
> `.`으로 시작하는 폴더뿐이다.

---

## 파이프라인과 폴더의 대응

**폴더 앞의 숫자가 사용 순번이다.** 파일 탐색기에서 정렬만 해도 작업 순서가 그대로 보인다.

```
0_base/  ─>  1_prompts/  ─>  2_raw/  ─>  3_cut/  ─>  4_frames/  ─>  5_curated/  ─>  Assets/Art/Sprites/Forms/
 기준그림     프롬프트       생성원본     알파추출     프레임분리     최종 선택본        (여기부터 Unity)
```

| 폴더 | 순번 | 내용 | git |
|------|:---:|------|-----|
| `0_base/` | 0 | 폼별 **기준 이미지**. 프롬프트에 **첨부**하는 그림이라 0번이다 — 없으면 시작을 못 한다 | ✅ 추적 |
| `1_prompts/` | 1 | 프롬프트 **SoT**. 아래 하위 폴더로 다시 갈린다 | ✅ 추적 |
| `2_raw/<form>/` | 2 | AI 생성 원본. 마젠타 배경, 후보 여러 장 | ❌ 제외 |
| `3_cut/` | 3 | 크로마키 추출본(알파) | ❌ 제외 |
| `4_frames/<form>/` | 4 | 행 이미지에서 분리한 상태별 프레임 | ❌ 제외 |
| `5_curated/<form>/` | 5 | 큐레이션 통과본. **게임에 들어갈 것** | ✅ 추적 |
| `qa/` | 곁 | 측정 리포트·대조 이미지 (단계가 아니라 3·4의 부산물) | ❌ 제외 |

> 📌 **추적 기준은 "재생성 가능한가"다.** `2_raw`·`3_cut`·`4_frames`·`qa`는 원본과 스크립트만
> 있으면 다시 만들어지므로 저장소에 넣지 않는다(용량만 먹는다).
> `1_prompts`·`0_base`·`5_curated`는 **다시 만들 수 없다** — 프롬프트는 손으로 쓴 것이고,
> base와 curated는 여러 후보 중 **사람이 고른 결과**다.
> 같은 기준으로 `1_prompts/3_assembled/`는 제외한다 — 블록과 `build.py`만 있으면 다시 나온다.

### `1_prompts/` 하위 구조 — 여기도 숫자가 순번이다

```
1_prompts/
  1_blocks/      공용 블록 — 참조범위·스타일·정체성·프레이밍·규칙   ❌ 수정 금지
  2_forms/       폼별 블록 — 몸·무기·실루엣·리깅 배제               ✅ 여기를 고친다
  build.py       1·2를 이어 붙여 3을 만든다                        ▶ 1·2를 고쳤으면 반드시 실행
  3_assembled/   ← 모델에 넣는 건 여기뿐. 손으로 고치지 말 것
  poses/         행 이미지용 포즈 문구 예시 (조립 안 됨, 참고용)
  legacy/        블록 구조 이전의 1회성 초안 (안 씀, 이력으로만)
```

읽는 순서는 **1 → 2 → build.py → 3**이고, **매일 쓰는 순서는 3 하나뿐**이다.
프롬프트를 안 고치는 날에는 `3_assembled/`만 열면 된다.

> 🔴 **왜 갈랐나 (2026-08-20).** 예전에는 서른 개 넘는 파일이 `prompts/` 바닥에
> 한 줄로 늘어서 있었다. `_identity.txt`(원본) · `_assembled_dark_blade_rig.txt`(산출물) ·
> `void_archer_v2_prompt.txt`(폐기된 초안)가 **같은 높이에 이름만 다르게** 있었고,
> 파일명 접두어(`_`)만으로는 "고쳐야 할 것 / 덮어써질 것 / 안 쓰는 것"이 구분되지 않았다.
> 이제 **폴더가 역할과 순서를 같이 말한다.**

> ▶ **실제 작업 절차는 [HOWTO.md](./HOWTO.md)에 있다.** 이 문서는 "무엇이 어디 있는가",
> `GUIDELINE.md`는 "왜 그렇게 하는가", `HOWTO.md`는 "그래서 지금 뭘 하는가"다.

---

## 명명 규약

| 대상 | 형식 | 예 |
|------|------|-----|
| 기준 이미지 | `0_base/<formId>.png` | `0_base/dark_blade.png` |
| 생성 원본(단일) | `2_raw/<formId>/<formId>_<n>.png` | `2_raw/void_archer/void_archer_1.png` |
| 생성 원본(행) | `2_raw/<formId>/sheet_<상태들>.png` | `2_raw/dark_blade/sheet_idle-walk-attack.png` |
| 분리 프레임 | `4_frames/<formId>/<state>.png` | `4_frames/dark_blade/attack.png` |
| 큐레이션 통과 | `5_curated/<formId>/<state>.png` | `5_curated/dark_blade/idle.png` |
| **리깅 base(몸)** | `2_raw/<formId>/<formId>_rig_<n>.png` → `5_curated/<formId>/rig_base.png` | `5_curated/void_archer/rig_base.png` |
| **무기 단독** | `2_raw/<formId>/<formId>_weapon_<무기id>_<n>.png` → `5_curated/<formId>/weapon_<무기id>.png` | `5_curated/ancient_shield/weapon_towershield.png` |

리깅 base의 `<state>`는 **`rig_base`로 고정**한다. `idle`과 헷갈리기 쉬운데 다른 것이다 —
`idle`은 **그려진 정지 포즈**이고 `rig_base`는 **뼈가 변형할 원본**이라 요구 조건이 다르다
(정측면·팔다리 분리·관절 가시·천 정지·**빈손**). 규약은 `1_prompts/1_blocks/rig_rules.txt`, 근거는 GUIDELINE §7.

무기 id는 폼 블록의 `[WEAPON: <id>]` 표시와 **같은 값**을 쓴다.

| 폼 | 무기 id |
|---|---|
| `dark_blade` | `greatsword` |
| `void_archer` | `longbow` |
| `ancient_shield` | `towershield` · `shortsword` |
| `void_thrower` | `javelin` |

`<formId>`는 **`FormData.formId`와 같은 값**을 쓴다(`dark_blade`·`void_archer`·
`ancient_shield`·`void_thrower`). 파일 이름이 곧 데이터 키라 오타가 나면
조용히 안 붙는다 — 새 이름을 만들지 말고 에셋에서 복사할 것.

---

## 도구

| 스크립트 | 하는 일 |
|---|---|
| `Tools/ArtPipeline/chroma_cutout.py` | 크로마키 제거 + 소프트 알파 언믹싱 (② → ③) |
| `Tools/ArtPipeline/measure_consistency.py` | 행 이미지 프레임 분리 + 일관성 정량 측정 (③ → ④) |

```bash
python Tools/ArtPipeline/chroma_cutout.py Art_Source/2_raw/dark_blade/sheet_idle-walk-attack.png --out-dir Art_Source/3_cut
python Tools/ArtPipeline/measure_consistency.py Art_Source/3_cut/sheet_idle-walk-attack-cut.png --expect 3 --save-frames Art_Source/4_frames/dark_blade
```

---

## 프롬프트 조립

프롬프트는 **블록을 이어 붙여** 쓴다. 공용 블록이나 폼 블록을 고치면 조립본을 다시 만들어야 한다.

```bash
python Art_Source/1_prompts/build.py              # 전부
python Art_Source/1_prompts/build.py void_archer  # 하나만
```

공용 블록은 **참조범위 / 스타일 / 정체성 / 프레이밍 / 규칙** 다섯으로 갈려 있다.
스타일만 넷 다 공통이고, 나머지는 용도마다 바뀐다.

| `1_blocks/` 파일 | 무엇 | 어디에 들어가나 |
|---|---|---|
| `reference_showcase` · `reference_rig` · `reference_weapon` | **기준 이미지에서 무엇을 베끼는가** | 용도별로 하나씩 |
| `style` | 그림체 | 전부 |
| `identity_figure` | 인물 정체성 | 사람이 나올 때만 (무기 단독 제외) |
| `framing_showcase` · `framing_rig` · `framing_weapon` | 화면 구성 | 용도별로 하나씩 |
| `sheet_rules` · `rig_rules` · `weapon_rules` | 그 용도의 제약 | 해당 조립본만 |

| 조립본 (`3_assembled/`) | 블록 구성 | 쓰임 |
|---|---|---|
| `<form>.txt` | reference_showcase + style + identity + framing_showcase + 폼(몸·무기·실루엣) | 전신 단일 이미지 — 정체성이 붙는지 먼저 확인 |
| `<form>_sheet.txt` | 위 + `sheet_rules` | 행 이미지(3포즈 한 장) |
| `<form>_rig.txt` | reference_**rig** + style + identity + framing_rig + 몸 + **RIG-EXCLUDE** + `rig_rules` | **리깅 base**(정측면·빈손) — GUIDELINE §7 |
| `<form>_weapon_<id>.txt` | reference_**weapon** + style + framing_weapon + `weapon_rules` + 폼(무기) | **무기·방패 단독** — GUIDELINE §8 |

> 🔴 **`_rig`와 `_weapon`이 쓰는 `reference_*` 블록이 다른 것이 핵심이다.**
> 기준 이미지(`0_base/dark_blade.png`)는 **거대한 대검을 쥐고 있다.** 예전에는 세 용도가
> BASE IMAGE 문단 하나를 공유해서 리깅 프롬프트에도 *"Match that reference image exactly"* 가
> 그대로 들어갔고, 그 이미지가 텍스트의 *"hands empty"* 한 줄과 매번 싸웠다.
> **이미지 조건이 텍스트 부정문보다 세다** — 그래서 결과가 뽑기마다 갈렸다.
> `reference_rig.txt`는 부정문을 더 쌓는 대신 **베낄 범위에서 검을 빼낸다.**
> 반대로 `reference_weapon.txt`는 그 대검을 *"이 스타일에서 무기를 그리는 법의 예시"* 로 적극 가리킨다.

> 🔴 **`cat`으로 직접 붙이지 말 것.** 블록 파일에는 사람용 한글 주석이 섞여 있고,
> 그게 모델에 넘어가면 **지시로 읽힌다.** `build.py`가 걷어낸다.
> 2026-08-20까지 이 문서가 `cat`을 안내했고, 실제로 `sheet_rules.txt`의 한글 3줄이
> **모든 행 프롬프트에 들어가고 있었다** — 안내가 도구보다 오래 살아남은 경우다.
>
> 📌 같은 날 한 번 더 샜다. 주석을 **줄 단위**로 걷어내다 보니, 여러 줄짜리 주석 안에서
> **삭제한 옛 영문 원문을 인용한 줄**이 대괄호로 시작하지도 한글도 없어서 그대로 통과했다.
> *"no weapon, no shield, no bow, no javelin, no staff"* 라는 **지워 놓은 문장이
> 주석 인용을 타고 리깅 프롬프트에 다시 실렸다.** 지금은 여는 `[`부터 닫는 `]`까지
> **블록 통째로** 버리고, 안 닫힌 주석은 build가 줄 번호와 함께 에러로 세운다.

⚠️ **`1_blocks/style.txt`와 `1_blocks/identity_figure.txt`는 손대지 않는다.** 폼마다 정체성이
미세하게 다르면 같은 사람으로 안 읽힌다 — 2026-08-20 측정에서 팔레트 일치 89.7%가 나온 근거가
"모든 생성이 같은 정체성 블록 + 같은 기준 이미지를 참조했다"는 것뿐이다.

> 📌 **갈라 온 이력이 그대로 결함 목록이다.**
> ① `_anchor.txt`가 정체성 + 프레이밍을 같이 들고 있었다 → 리깅 프롬프트가
>    *"three-quarter"* 뒤에 *"NOT three-quarter"* 로 자기 말을 뒤집었다.
>    (`_shared.txt`도 같은 내용을 중복해 들고 있어 함께 제거)
> ② `_identity.txt`가 BASE IMAGE + STYLE + IDENTITY 를 같이 들고 있었다 → **무기 단독 프롬프트가
>    "물체 하나뿐, 사람 없음"이라고 해 놓고 그 위에서 후드 쓴 사람을 서술**하고 있었다.
> ③ BASE IMAGE 문단이 세 용도 공용이라 *"exactly 맞춰라"* 가 리깅에도 들어갔다 →
>    **첨부 이미지의 대검이 무기 없는 몸을 요구하는 프롬프트와 싸웠다.**
>
> 🔑 셋 다 같은 모양이다 — **한 블록이 서로 다른 범위의 것을 같이 들고 있으면,
> 그 블록을 쓰는 조립본 중 하나는 반드시 자기 말을 뒤집는다.**
> `identity_figure.txt` 마지막 줄에서 *"runs through the **weapon** and armor trim"* 의
> 무기 언급을 뺀 것도 ②의 연장이다(accent 색 값은 `[BODY]`가, 무기 accent 는
> `weapon_rules`와 `[WEAPON]` 절이 이미 말한다 — 잃는 정보가 없다).

### 조립에 안 들어가는 파일

`poses/`와 `legacy/`는 `build.py`가 **읽지 않는다.** 각 폴더의 `README.md` 참조.

| 폴더 | 무엇 |
|---|---|
| `poses/` | 행 이미지용 포즈 문구 **예시**. `dark_blade` 기준으로 쓰여 있어 다른 폼에는 그대로 안 맞는다 |
| `legacy/` | 블록 구조가 생기기 전의 **1회성 초안**. 이력으로만 남긴다 |

---

## 참조

- `Docs/adr/008-art-direction-pixel.md` — 아트 방향(🔴 **전제 재검토 필요**: Skul은 픽셀 아트가 아니다)
- `Tools/PixelArt/form_sprite_spec.py` — 절차적 도트 파이프라인의 규격(기준선으로 보존)
