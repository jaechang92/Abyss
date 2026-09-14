# -*- coding: utf-8 -*-
"""무기 스프라이트의 **자루 기준점(그립)** 을 뽑는다 — 손 앵커와 만나는 지점.

    python Tools/ArtPipeline/weapon_grips.py Art_Source/weapons/sword_64_grid.png --kind blade
    python Tools/ArtPipeline/weapon_grips.py Art_Source/weapons/*_64_grid.png --json-out grips.json

🔑 **왜 필요한가**

  몸과 무기를 분리하면 둘이 만나는 약속이 필요하다. 몸 쪽은 `hand_anchors.py` 가 프레임마다
  손 위치를 주고, 무기 쪽은 이 도구가 **「스프라이트의 어느 픽셀이 손에 오는가」** 를 준다.
  둘이 같은 규약을 쓰면 **무기를 바꿔도 앵커를 다시 안 잡는다** — 그게 장비 시스템의 전제다.

🔑 **쥐는 방식이 다르면 규약도 다르다** (2026-09-14 · 후보 64개씩 실측)

  처음에 「축을 따라 가드~폼멜 중점」 하나로 넷을 다 덮으려 했는데 **틀렸다.**
  활은 그 규칙이 활대 *끝* 을 짚었고(활은 대 중앙을 쥔다), 방패는 손잡이가 그림 뒤에 있어
  y 편차가 5.1px 까지 벌어졌다. **칼날-자루 구조가 있는 것과 없는 것을 갈라야 한다.**

      blade   검 · 단검     가드~폼멜 중점    검 (0.788,0.789) ±0.7px · 단검 (0.680,0.589) ±1.3px
      center  활 · 방패     bbox 중심         활 (0.469,0.516) ±0.5px · 방패 (0.484,0.484) ±1.3px

  ⚠️ 편차가 ±1.3px 안이라는 것이 이 규약을 구한다 — **한 무기에서 잡은 앵커가 같은 종류의
  다른 63개에도 맞는다.** 폼 전용이라 종류를 넘을 일이 없으므로 이걸로 충분하다.

⚠️ **출력 y 는 위에서부터다.** Unity 스프라이트 피벗은 **아래에서부터**라 `pivot_unity` 를 따로 낸다.
"""

import argparse
import glob
import json
import os

import numpy as np
from PIL import Image

ALPHA_CUT = 8
GUARD_MIN_WIDTH = 8      # 이 폭 이상이면 가드로 본다. 손잡이는 3~5px, 칼날은 5~8px
FALLBACK_HANDLE = 12     # 가드를 못 찾았을 때 쓸 손잡이 길이


def cells_of(path, columns=8):
    """정사각 격자 시트를 칸 배열로. 빈 칸은 건너뛴다."""
    im = np.array(Image.open(path).convert("RGBA"))
    cell = im.shape[1] // columns
    rows = im.shape[0] // cell
    out = []
    for r in range(rows):
        for c in range(columns):
            sub = im[r * cell:(r + 1) * cell, c * cell:(c + 1) * cell]
            if (sub[:, :, 3] > ALPHA_CUT).any():
                out.append((r * columns + c, sub))
    return out, cell


def grip_blade(sub, bias=0.5):
    """칼날-자루 구조: 주축을 잡고 가드~폼멜 사이를 쥔다.

    `bias` 0.0 = 가드 바로 아래 · 0.5 = 손잡이 중간(기본) · 1.0 = 폼멜 끝.
    한 손으로 쥐면 가드 쪽이 자연스럽다 — 소켓을 붙여 보고 검이 미끄러져 보이면 낮춘다.
    """
    a = sub[:, :, 3] > ALPHA_CUT
    ys, xs = np.where(a)
    pts = np.stack([xs, ys]).astype(float)
    mean = pts.mean(axis=1, keepdims=True)
    p = pts - mean
    w, v = np.linalg.eigh(p @ p.T)
    axis = v[:, np.argmax(w)]
    if axis[0] < 0:                       # +축이 손잡이 쪽(오른쪽 아래)을 보게 한다
        axis = -axis
    perp = np.array([-axis[1], axis[0]])
    t = axis @ p
    s = perp @ p

    ts = np.arange(int(np.floor(t.min())), int(np.ceil(t.max())) + 1)
    prof = []
    for k in ts:
        m = (t >= k) & (t < k + 1)
        prof.append(0.0 if not m.any() else s[m].max() - s[m].min() + 1)
    prof = np.array(prof)

    pommel = float(ts[-1])
    guard = None
    for i in range(len(ts) - 1, -1, -1):  # 폼멜에서 칼날 쪽으로 훑는다
        if prof[i] >= GUARD_MIN_WIDTH:
            guard = float(ts[i])
            break
    if guard is None or guard >= pommel:
        guard = pommel - FALLBACK_HANDLE

    gp = mean.ravel() + axis * (guard + (pommel - guard) * bias)
    return gp, {"axisDeg": float(np.degrees(np.arctan2(axis[1], axis[0]))),
                "handlePx": round(pommel - guard, 1),
                "lengthPx": int(ts[-1] - ts[0] + 1)}


def grip_center(sub, bias=0.5):     # bias 는 안 쓴다 — 면·활대는 중앙 하나뿐이다
    """면·활대 구조: 쥐는 곳이 그림에 안 드러나므로 bbox 중심을 쓴다."""
    a = sub[:, :, 3] > ALPHA_CUT
    ys, xs = np.where(a)
    return (np.array([(xs.min() + xs.max()) / 2.0, (ys.min() + ys.max()) / 2.0]),
            {"bbox": [int(xs.min()), int(ys.min()), int(xs.max()), int(ys.max())]})


KINDS = {"blade": grip_blade, "center": grip_center}

# 파일명 → 규약. 새 무기 종류를 넣을 때 여기에 한 줄 추가한다.
DEFAULT_KIND = {"sword": "blade", "dagger": "blade", "bow": "center", "shield": "center"}


def kind_of(path, override=None):
    if override:
        return override
    base = os.path.basename(path).lower()
    for key, kind in DEFAULT_KIND.items():
        if base.startswith(key):
            return kind
    raise SystemExit("🔴 %s 의 규약을 모르겠다. --kind 로 blade/center 를 지정할 것" % base)


def median(vals):
    s = sorted(vals)
    n = len(s)
    return s[n // 2] if n % 2 else (s[n // 2 - 1] + s[n // 2]) / 2.0


def pstdev(vals):
    m = sum(vals) / len(vals)
    return (sum((v - m) ** 2 for v in vals) / len(vals)) ** 0.5


def main():
    ap = argparse.ArgumentParser(description="무기 스프라이트의 자루 기준점(그립) 검출")
    ap.add_argument("sheets", nargs="+", help="후보 격자 시트 PNG")
    ap.add_argument("--columns", type=int, default=8, help="격자 열 수 (기본 8)")
    ap.add_argument("--kind", choices=sorted(KINDS), default=None,
                    help="규약을 강제한다. 안 주면 파일명으로 고른다")
    ap.add_argument("--grip-bias", type=float, default=0.5, dest="grip_bias",
                    help="blade 전용. 0=가드 바로 아래 · 0.5=손잡이 중간(기본) · 1=폼멜 끝")
    ap.add_argument("--json-out", default=None)
    args = ap.parse_args()

    paths = []
    for pat in args.sheets:
        paths.extend(sorted(glob.glob(pat)) or [pat])

    out = []
    for path in paths:
        kind = kind_of(path, args.kind)
        cells, cell = cells_of(path, args.columns)
        fn = KINDS[kind]
        items = []
        for idx, sub in cells:
            gp, extra = fn(sub, args.grip_bias)
            items.append({"index": idx,
                          "xPx": round(float(gp[0]), 1), "yPx": round(float(gp[1]), 1),
                          "pivot": [round(float(gp[0]) / cell, 4), round(float(gp[1]) / cell, 4)],
                          # Unity 스프라이트 피벗은 아래에서부터라 y 를 뒤집는다
                          "pivotUnity": [round(float(gp[0]) / cell, 4),
                                          round(1.0 - float(gp[1]) / cell, 4)],
                          **extra})
        xs = [i["xPx"] for i in items]
        ys = [i["yPx"] for i in items]
        mx, my = median(xs), median(ys)
        print("%-24s %s · %d칸 · 칸 %dpx" % (os.path.basename(path), kind, len(items), cell))
        print("   그립 중앙값 (%.1f, %.1f)px  편차 ±%.1f/±%.1f" % (mx, my, pstdev(xs), pstdev(ys)))
        print("   피벗(Unity, 아래 기준) (%.3f, %.3f)" % (mx / cell, 1.0 - my / cell))
        out.append({"sheet": os.path.basename(path), "kind": kind, "cell": cell,
                    "medianPivotUnity": [round(mx / cell, 4), round(1.0 - my / cell, 4)],
                    "stdevPx": [round(pstdev(xs), 2), round(pstdev(ys), 2)],
                    "items": items})

    print()
    print("⚠️ 칸마다 값이 조금씩 다르다. 한 무기를 실제로 쓸 때는 그 칸의 pivot_unity 를 쓰고,")
    print("   중앙값은 **어느 칸을 골라도 이 근처**라는 확인용이다(편차가 작아야 규약이 성립한다).")

    if args.json_out:
        with open(args.json_out, "w", encoding="utf-8") as f:
            json.dump({"sheets": out}, f, ensure_ascii=False, indent=2)
        print("→ %s" % args.json_out)


if __name__ == "__main__":
    main()
