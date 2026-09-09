# -*- coding: utf-8 -*-
"""경로별 ΔE 관측 범위 + 측정 누적 (enforce_palette.py 가 쓴다).

🔴 **왜 갈라져 있나 — 한 선으로 못 자른다는 것이 실측이다** (2026-09-10, stage1 27장):

      파생 (style_image 있음)     평균 0.3 ~  6.2    상위5% 3.7 ~ 24.4
      앵커 (style_image 없음)     평균 4.4 ~ 10.1
      타일셋 (style 칸 자체 없음)  평균 9.2 ~  9.7    상위5% 11.7 ~ 13.1

    **파생의 최악(6.2)이 타일셋의 최선(9.2)보다 낮다. 두 분포가 겹치지 않는다.**
    파생에 맞춘 선은 타일셋을 전부 죽이고, 타일셋에 맞춘 선은 파생에서 안 걸린다.
    붙들 수단이 다르면(style_image / 없음 / 칸 자체가 없음) 결과 분포가 다르다 —
    같은 잣대를 대는 것이 애초에 틀렸다.

🔴 **그런데 여기서 새 선을 긋지 않는다. 표본이 채택본뿐이기 때문이다.**
    후보 143장은 규약상 안 남긴다(`curated/_manifest.md`). 위 숫자는 **사람이 이미 고른 것**만
    모은 것이라, 이걸로 선을 그으면 **합격한 것만 보고 합격선을 정하는 셈**이다.
    게다가 실제로 위험하다 — ΔE 9.2 짜리 타일셋은 **눈으로 보니 멀쩡했다.**
    타일셋 경로에 9.0 을 그으면 그 장을 버렸을 것이다.

🔑 그래서 이 파일이 하는 일은 셋이다:
      ① 경로를 가른다              — 지금은 한 선뿐이라 구조적으로 불가능하던 것
      ② 관측 범위 안/밖을 **말한다** — 버릴지는 사람이 정한다. 숫자가 판정을 대신하지 않는다
      ③ 측정을 파일로 쌓는다        — 그림은 안 남겨도 숫자는 남길 수 있다.
                                     다음 씬 7개를 뽑는 동안 **진짜 표본**이 모인다

    ③ 이 본론이다. 선을 못 긋는 이유가 표본이 없어서이므로, 표본을 쌓는 장치가 먼저다.
"""

import datetime
import json
import os

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(os.path.dirname(HERE))
ASSEMBLED = os.path.join(REPO, "Art_Source", "assembled")
LOG_PATH = os.path.join(REPO, "Art_Source", "_measurements.tsv")

# ── 경로 ──────────────────────────────────────────────────────────────────────
# 조립본이 이미 이 구분을 갖고 있다 — `_style_image_path` 유무 + `tool`.
# 여기서 이름을 붙일 뿐 새로 판단하지 않는다.
DERIVED = "derived"      # create_image_pro + style_image  (배경 파생 · 프롭 · 발판)
ANCHOR = "anchor"        # create_image_pro, style_image 없음  (씬당 한 장)
TILESET = "tileset"      # create_sidescroller_tileset — style_image 칸이 아예 없다
PATHS = (DERIVED, ANCHOR, TILESET)

# ── 관측 범위 (기준선이 아니다) ────────────────────────────────────────────────
# ⚠️ **이것은 합격선이 아니라 「지금까지 본 것」이다.** 밖에 있다고 불합격이 아니라
#    「처음 보는 값이니 눈으로 볼 것」이라는 뜻이다. 표본이 채택본뿐이라 그 이상은 못 말한다.
#    표본 수를 같이 적는 이유: 2장으로 만든 범위와 25장으로 만든 범위는 무게가 다르다.
OBSERVED = {
    DERIVED: {"mean": (0.3, 6.2), "p95": (3.7, 24.4), "n": 25, "since": "2026-09-08"},
    ANCHOR:  {"mean": (4.4, 10.1), "p95": None, "n": 20, "since": "2026-09-08"},
    TILESET: {"mean": (3.9, 9.7), "p95": (6.6, 13.1), "n": 4, "since": "2026-09-09"},
}


def classify(stem, explicit=None):
    """파일 이름으로 경로를 고른다. `(경로, 근거, 확신)` 을 돌려준다.

    🔴 **이름 추측은 실제로 틀렸다.** 이 장치를 처음 돌린 날(2026-09-10) 타일셋 산출물
       `wall_r5_final.png` 가 `derived` 로 분류됐고, 그 바람에 평균 3.9 를
       **파생 범위(0.3~6.2) 안**이라고 말했다. 타일셋 범위(3.9~9.7)에서는 하단이다.
       raw 산출물 이름은 임의라 앞으로도 틀린다.

    🔑 그래서 확신이 없으면 **모른다고 말하고 범위 판정을 보류한다.**
       조용히 틀린 범위를 말하는 것이 아무 말도 안 하는 것보다 나쁘다 —
       이 파일이 「숫자가 판정을 대신하지 않는다」고 적어 놓고 스스로 어길 뻔했다.
    """
    if explicit:
        return explicit, "지정됨(--path)", True

    low = stem.lower()
    if "tileset" in low:
        return TILESET, "이름에 tileset", True
    if "bg_mid" in low or "anchor" in low:
        return ANCHOR, "이름에 bg_mid/anchor", True
    return DERIVED, "이름으로 못 갈랐다", False


def from_assembled(scene, part):
    """조립본에서 경로를 읽는다. 이름 추측보다 이쪽이 사실이다."""
    path = os.path.join(ASSEMBLED, "%s__%s.json" % (scene, part))
    if not os.path.exists(path):
        return None
    with open(path, encoding="utf-8") as f:
        req = json.load(f)
    if req.get("tool") == "create_sidescroller_tileset":
        return TILESET
    return DERIVED if req.get("_style_image_path") else ANCHOR


def describe(path_kind, mean_de, p95_de, confident=True):
    """관측 범위 안인지 말한다. **판정하지 않는다.**

    확신이 없으면(`confident=False`) 범위를 아예 말하지 않는다 — 위 classify 주석 참조.
    """
    if not confident:
        return ["경로를 못 갈랐다 — 관측 범위 판정을 **보류한다**. "
                "`--path %s` 중 하나를 지정할 것" % "|".join(PATHS)]

    ref = OBSERVED.get(path_kind)
    if not ref:
        return ["경로 %s — 관측 기록이 없다" % path_kind]

    lines = []
    lo, hi = ref["mean"]
    where = "안" if lo <= mean_de <= hi else ("아래" if mean_de < lo else "위")
    lines.append("경로 %s (표본 %d장, %s~) — 평균 %.1f 은 관측 범위 %.1f~%.1f 의 **%s**"
                 % (path_kind, ref["n"], ref["since"], mean_de, lo, hi, where))

    if ref["p95"]:
        plo, phi = ref["p95"]
        pwhere = "안" if plo <= p95_de <= phi else ("아래" if p95_de < plo else "위")
        lines.append("   상위5%% %.1f 은 관측 %.1f~%.1f 의 **%s**" % (p95_de, plo, phi, pwhere))

    if where == "위":
        lines.append("   ⚠️ 이 경로에서 처음 보는 높이다 — **눈으로 볼 것.** 불합격이라는 뜻이 아니다")
    return lines


# ── 측정 누적 ─────────────────────────────────────────────────────────────────
COLUMNS = ("date", "scene", "file", "path", "w", "h", "opaque",
           "mean_de", "p95_de", "max_de", "used", "ramp", "src_colors")


def log(scene, stem, path_kind, w, h, opaque, mean_de, p95_de, max_de,
        used, ramp, src_colors, log_path=None):
    """한 줄 덧붙인다. **그림은 안 남겨도 숫자는 남길 수 있다.**

    ⚠️ 채택 여부는 안 적는다 — 재는 시점에는 아직 안 골랐다.
       고른 결과는 `curated/_manifest.md` 가 갖는다. 두 곳에 적으면 갈린다.
    """
    dest = log_path or LOG_PATH
    fresh = not os.path.exists(dest)
    row = (datetime.date.today().isoformat(), scene or "-", stem, path_kind,
           w, h, opaque, "%.2f" % mean_de, "%.2f" % p95_de, "%.2f" % max_de,
           used, ramp, src_colors)
    with open(dest, "a", encoding="utf-8", newline="\n") as f:
        if fresh:
            f.write("\t".join(COLUMNS) + "\n")
        f.write("\t".join(str(v) for v in row) + "\n")
    return dest
