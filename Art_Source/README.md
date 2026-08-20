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

```
prompts/   ─①─>  raw/  ─②─>  cut/  ─③─>  frames/  ─④─>  curated/  ─⑤─>  Assets/Art/Sprites/Forms/
                                                                              (여기부터 Unity)
```

| 폴더 | 단계 | 내용 | git |
|------|------|------|-----|
| `prompts/` | ① | 프롬프트 **SoT**. 정체성·프레이밍·규칙·폼별·조립본 | ✅ 추적 |
| `base/` | ① | 폼별 **기준 이미지**(정체성 블록이 가리키는 것) | ✅ 추적 |
| `raw/<form>/` | ② | AI 생성 원본. 마젠타 배경, 후보 여러 장 | ❌ 제외 |
| `cut/` | ③ | 크로마키 추출본(알파) | ❌ 제외 |
| `frames/<form>/` | ④ | 시트에서 분리한 상태별 프레임 | ❌ 제외 |
| `curated/<form>/` | ⑤ | 큐레이션 통과본. **게임에 들어갈 것** | ✅ 추적 |
| `qa/` | — | 측정 리포트·대조 이미지 | ❌ 제외 |

> 📌 **추적 기준은 "재생성 가능한가"다.** `raw`·`cut`·`frames`·`qa`는 원본과 스크립트만
> 있으면 다시 만들어지므로 저장소에 넣지 않는다(용량만 먹는다).
> `prompts`·`base`·`curated`는 **다시 만들 수 없다** — 프롬프트는 손으로 쓴 것이고,
> base와 curated는 여러 후보 중 **사람이 고른 결과**다.

---

## 명명 규약

| 대상 | 형식 | 예 |
|------|------|-----|
| 기준 이미지 | `base/<formId>.png` | `base/dark_blade.png` |
| 생성 원본(단일) | `raw/<formId>/<formId>_<n>.png` | `raw/void_archer/void_archer_1.png` |
| 생성 원본(행) | `raw/<formId>/sheet_<상태들>.png` | `raw/dark_blade/sheet_idle-walk-attack.png` |
| 분리 프레임 | `frames/<formId>/<state>.png` | `frames/dark_blade/attack.png` |
| 큐레이션 통과 | `curated/<formId>/<state>.png` | `curated/dark_blade/idle.png` |
| **리깅 base(몸)** | `raw/<formId>/<formId>_rig_<n>.png` → `curated/<formId>/rig_base.png` | `curated/void_archer/rig_base.png` |
| **무기 단독** | `raw/<formId>/<formId>_weapon_<무기id>_<n>.png` → `curated/<formId>/weapon_<무기id>.png` | `curated/ancient_shield/weapon_towershield.png` |

리깅 base의 `<state>`는 **`rig_base`로 고정**한다. `idle`과 헷갈리기 쉬운데 다른 것이다 —
`idle`은 **그려진 정지 포즈**이고 `rig_base`는 **뼈가 변형할 원본**이라 요구 조건이 다르다
(정측면·팔다리 분리·관절 가시·천 정지·**빈손**). 규약은 `prompts/_rig_rules.txt`, 근거는 GUIDELINE §7.

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
python Tools/ArtPipeline/chroma_cutout.py Art_Source/raw/dark_blade/sheet_idle-walk-attack.png --out-dir Art_Source/cut
python Tools/ArtPipeline/measure_consistency.py Art_Source/cut/sheet_idle-walk-attack-cut.png --expect 3 --save-frames Art_Source/frames/dark_blade
```

---

## 프롬프트 조립

프롬프트는 **블록을 이어 붙여** 쓴다. 공용 블록이나 폼 블록을 고치면 조립본을 다시 만들어야 한다.

```bash
python Art_Source/prompts/build.py              # 전부
python Art_Source/prompts/build.py void_archer  # 하나만
```

공용 블록은 **정체성 / 프레이밍 / 규칙** 셋으로 갈려 있다. 정체성은 모든 조립본에 그대로 들어가고,
프레이밍과 규칙이 용도마다 바뀐다.

| 조립본 | 블록 구성 | 쓰임 |
|---|---|---|
| `_assembled_<form>.txt` | 정체성 + 쇼케이스 프레이밍 + 폼(몸·무기·실루엣) | 전신 단일 이미지 — 정체성이 붙는지 먼저 확인 |
| `_assembled_<form>_sheet.txt` | 위 + `_sheet_rules` | 행 이미지(3포즈 한 장) |
| `_assembled_<form>_rig.txt` | 정체성 + 리깅 프레이밍 + 폼(몸) + `_rig_rules` | **리깅 base**(정측면·빈손) — GUIDELINE §7 |
| `_assembled_<form>_weapon_<id>.txt` | 정체성 + 무기 프레이밍 + `_weapon_rules` + 폼(무기) | **무기·방패 단독** — GUIDELINE §8 |

> 🔴 **`cat`으로 직접 붙이지 말 것.** 블록 파일에는 사람용 한글 주석이 섞여 있고,
> 그게 모델에 넘어가면 **지시로 읽힌다.** `build.py`가 걷어낸다.
> 2026-08-20까지 이 문서가 `cat`을 안내했고, 실제로 `_sheet_rules.txt`의 한글 3줄이
> **모든 행 프롬프트에 들어가고 있었다** — 안내가 도구보다 오래 살아남은 경우다.

⚠️ **`_identity.txt`는 한 글자도 바꾸지 않는다.** 폼마다 정체성이 미세하게 다르면
같은 사람으로 안 읽힌다 — 2026-08-20 측정에서 팔레트 일치 89.7%가 나온 근거가
"모든 생성이 같은 정체성 블록 + 같은 기준 이미지를 참조했다"는 것뿐이다.

> 📌 2026-08-20 이전에는 `_anchor.txt` 하나가 정체성과 프레이밍을 같이 들고 있었다.
> 바꾸면 안 되는 것은 **정체성**인데 프레이밍이 묶여 있어, 리깅 프롬프트가
> *"three-quarter"* 라고 한 뒤 *"NOT three-quarter"* 로 자기 말을 뒤집었다.
> `_shared.txt`도 같은 내용을 중복해 들고 있어 함께 제거됐다.

### 조립에 안 들어가는 파일

아래는 `build.py`가 **읽지 않는다.** 손으로 참고할 때만 쓴다.

| 파일 | 무엇 |
|---|---|
| `idle.txt` · `walk.txt` · `attack.txt` | 행 이미지용 포즈 문구 **예시**. `dark_blade` 기준으로 쓰여 있어 다른 폼에는 그대로 안 맞는다 |
| `void_archer_prompt.txt` · `void_archer_v2_prompt.txt` | 블록 구조가 생기기 전의 **1회성 초안**. 이력으로만 남긴다 |

---

## 참조

- `Docs/adr/008-art-direction-pixel.md` — 아트 방향(🔴 **전제 재검토 필요**: Skul은 픽셀 아트가 아니다)
- `Tools/PixelArt/form_sprite_spec.py` — 절차적 도트 파이프라인의 규격(기준선으로 보존)
