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
| `prompts/` | ① | 프롬프트 **SoT**. 앵커·폼별·조립본 | ✅ 추적 |
| `base/` | ① | 폼별 **기준 이미지**(앵커가 가리키는 것) | ✅ 추적 |
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
| **리깅 base 포즈** | `<단계>/<formId>/rig_base.png` | `curated/void_archer/rig_base.png` |

리깅 base 포즈의 `<state>`는 **`rig_base`로 고정**한다. `idle`과 헷갈리기 쉬운데 다른 것이다 —
`idle`은 **그려진 정지 포즈**이고 `rig_base`는 **뼈가 변형할 원본**이라 요구 조건이 다르다
(정측면·팔다리 분리·천 정지). 규약은 `prompts/_rig_rules.txt`, 근거는 GUIDELINE §7.

`<formId>`는 **`FormData.formId`와 같은 값**을 쓴다(`dark_blade`·`void_archer`·
`ancient_shield`·`void_thrower`). 파일 이름이 곧 데이터 키라 오타가 나면
조용히 안 붙는다 — 새 이름을 만들지 말고 에셋에서 복사할 것.

---

## 도구

| 스크립트 | 하는 일 |
|---|---|
| `Tools/PixelArt/chroma_cutout.py` | 크로마키 제거 + 소프트 알파 언믹싱 (② → ③) |
| `Tools/PixelArt/measure_consistency.py` | 행 이미지 프레임 분리 + 일관성 정량 측정 (③ → ④) |

```bash
python Tools/PixelArt/chroma_cutout.py Art_Source/raw/dark_blade/sheet_idle-walk-attack.png --out-dir Art_Source/cut
python Tools/PixelArt/measure_consistency.py Art_Source/cut/sheet_idle-walk-attack-cut.png --expect 3 --save-frames Art_Source/frames/dark_blade
```

---

## 프롬프트 조립

프롬프트는 **블록을 이어 붙여** 쓴다. 앵커나 폼 블록을 고치면 조립본을 다시 만들어야 한다.

```bash
python Art_Source/prompts/build.py              # 전부
python Art_Source/prompts/build.py void_archer  # 하나만
```

조립본은 셋이 나온다.

| 조립본 | 블록 구성 | 쓰임 |
|---|---|---|
| `_assembled_<form>.txt` | 앵커 + 폼 | 단일 이미지 — 앵커가 붙는지 먼저 확인 |
| `_assembled_<form>_sheet.txt` | 앵커 + 폼 + `_sheet_rules` | 행 이미지(3포즈 한 장) |
| `_assembled_<form>_rig.txt` | 앵커 + 폼 + `_rig_rules` | **리깅용 base 포즈**(정측면) — GUIDELINE §7 |

> 🔴 **`cat`으로 직접 붙이지 말 것.** 블록 파일에는 사람용 한글 주석이 섞여 있고,
> 그게 모델에 넘어가면 **지시로 읽힌다.** `build.py`가 걷어낸다.
> 2026-08-20까지 이 문서가 `cat`을 안내했고, 실제로 `_sheet_rules.txt`의 한글 3줄이
> **모든 행 프롬프트에 들어가고 있었다** — 안내가 도구보다 오래 살아남은 경우다.

⚠️ **`_anchor.txt`는 한 글자도 바꾸지 않는다.** 폼마다 앵커가 미세하게 다르면
같은 사람으로 안 읽힌다 — 2026-08-20 측정에서 팔레트 일치 89.7%가 나온 근거가
"모든 생성이 같은 앵커 + 같은 기준 이미지를 참조했다"는 것뿐이다.

---

## 참조

- `Docs/adr/008-art-direction-pixel.md` — 아트 방향(🔴 **전제 재검토 필요**: Skul은 픽셀 아트가 아니다)
- `Tools/PixelArt/form_sprite_spec.py` — 절차적 도트 파이프라인의 규격(기준선으로 보존)
