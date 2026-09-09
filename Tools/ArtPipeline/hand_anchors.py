# -*- coding: utf-8 -*-
"""애니메이션 프레임마다 손 앵커를 뽑는다 — **믿을 수 있는 프레임만 쓰고 나머지는 보간한다.**

    python Tools/ArtPipeline/hand_anchors.py armed_sheet/ unarmed_sheet/ --anim difftest_walk

🔴 **왜 「믿을 수 있는 프레임만」인가** (2026-09-10 실측)

  무기 있는 몸과 없는 몸을 둘 다 애니메이션해서 프레임별로 diff 를 냈더니 **절반이 틀렸다:**

      f0  266px  bbox y 23~74   <- 세로 전체. 검이 아니라 자세 차이다
      f1  136px  bbox y 56~67   <- 정지본(133px)과 일치. 검이다
      f2  123px  bbox y 57~67   <- 검
      f3  236px  bbox y 23~69   <- 자세 차이
      f4  305px  bbox y 22~72   <- 자세 차이
      f5  130px  bbox y 57~67   <- 검
      f6  128px  bbox y 51~62   <- 검
      f7  151px  bbox y 56~67   <- 검

  `create_character_state` 가 *「스켈레톤을 물려받는다」*고 적어 두었고 template 애니메이션도
  스켈레톤 기반인데, **두 벌이 픽셀 단위로 같지는 않았다.** 망토·팔이 프레임마다 조금씩 어긋난다.

🔑 **그런데 8개 중 5개는 정확했다.** 그러니 diff 를 버릴 게 아니라 **거르면 된다** —
   맞은 프레임을 키로 쓰고 사이를 보간한다. 사람이 잡아야 할 키가 54~81개에서 0 에 가까워진다.

⚠️ **보간값은 추정이다.** 틀린 프레임 자리는 앞뒤 키의 사이값일 뿐 실제 손 위치가 아니다.
   이 도구는 **Unity 에서 다듬을 출발점**을 주는 것이지 최종값을 주지 않는다.
   출력에 `키`/`보간`을 표시하는 이유가 그것이다.
"""

import argparse
import glob
import json
import os

import numpy as np
from PIL import Image

ALPHA_CUT = 8

# ── 신뢰 판정 ────────────────────────────────────────────────────────────────
# 무기가 제대로 잡혔는지 가르는 두 잣대. 위 실측에서 뽑았다.
#   ① 픽셀 수가 기준(정지 프레임의 무기 크기)에서 크게 안 벗어난다
#   ② bbox 세로가 짧다 — 자세 차이는 몸 전체(세로 50px+)로 번지고 무기는 한 뭉치다
PIXEL_TOLERANCE = 0.45      # 기준 대비 ±45%
MAX_BBOX_HEIGHT_RATIO = 0.35  # 캔버스 세로의 35% 를 넘으면 무기가 아니다


def largest_blob(mask):
    h, w = mask.shape
    seen = np.zeros_like(mask, bool)
    best = []
    for sy in range(h):
        for sx in range(w):
            if not mask[sy, sx] or seen[sy, sx]:
                continue
            stack, blob = [(sy, sx)], []
            seen[sy, sx] = True
            while stack:
                y, x = stack.pop()
                blob.append((y, x))
                for ny, nx in ((y-1, x), (y+1, x), (y, x-1), (y, x+1)):
                    if 0 <= ny < h and 0 <= nx < w and mask[ny, nx] and not seen[ny, nx]:
                        seen[ny, nx] = True
                        stack.append((ny, nx))
            if len(blob) > len(best):
                best = blob
    out = np.zeros_like(mask, bool)
    for y, x in best:
        out[y, x] = True
    return out


def load_sheet(folder, anim=None):
    """스프라이트시트 zip 을 푼 폴더에서 프레임을 잘라 낸다."""
    meta_path = glob.glob(os.path.join(folder, "*.json"))
    png_path = glob.glob(os.path.join(folder, "*.png"))
    if not meta_path or not png_path:
        raise SystemExit("🔴 %s 에 시트(json+png)가 없다" % folder)
    meta = json.load(open(meta_path[0], encoding="utf-8"))["spritesheet"]
    sheet = Image.open(png_path[0]).convert("RGBA")
    cw = meta["cell_size"]["width"]
    ch = meta["cell_size"]["height"]

    rows = [r for r in meta["rows"] if r.get("type") == "animation"]
    if anim:
        rows = [r for r in rows if anim in str(r.get("animation", ""))]
    if not rows:
        names = [str(r.get("animation"))[:40] for r in meta["rows"] if r.get("type") == "animation"]
        raise SystemExit("🔴 애니메이션을 못 찾았다. 있는 것: %s" % names)
    row = rows[0]
    frames = [sheet.crop((i * cw, row["row"] * ch, (i + 1) * cw, (row["row"] + 1) * ch))
              for i in range(row["frame_count"])]
    return frames, row, (cw, ch)


def frame_weapon(armed, unarmed):
    a = np.array(armed, dtype=int)
    b = np.array(unarmed, dtype=int)
    am, bm = a[:, :, 3] > ALPHA_CUT, b[:, :, 3] > ALPHA_CUT
    raw = am & ~bm
    if raw.sum() == 0:
        return None, None, bm
    return largest_blob(raw), raw, bm


def anchor_of(weapon, body):
    ys, xs = np.where(weapon)
    _, bxs = np.where(body)
    order = np.argsort(np.abs(xs - bxs.mean()))[:max(4, len(xs) // 8)]
    return float(xs[order].mean()), float(ys[order].mean())


def interpolate(values):
    """None 자리를 앞뒤 키의 선형 보간으로 채운다. 순환(loop) 애니메이션이라 양끝도 잇는다."""
    n = len(values)
    known = [i for i, v in enumerate(values) if v is not None]
    if not known:
        return values, []
    out = list(values)
    filled = []
    for i in range(n):
        if out[i] is not None:
            continue
        # 순환 거리로 가장 가까운 앞/뒤 키를 찾는다
        prev = max((k for k in known if k < i), default=known[-1])
        nxt = min((k for k in known if k > i), default=known[0])
        span = (nxt - prev) % n or n
        t = ((i - prev) % n) / span
        px, py = values[prev]
        nx, ny = values[nxt]
        out[i] = (px + (nx - px) * t, py + (ny - py) * t)
        filled.append(i)
    return out, filled


def main():
    ap = argparse.ArgumentParser(description="애니메이션 프레임별 손 앵커 (믿을 수 있는 것만 키로)")
    ap.add_argument("armed", help="무기 든 몸의 시트 폴더")
    ap.add_argument("unarmed", help="무기 없는 몸의 시트 폴더")
    ap.add_argument("--anim", default=None, help="애니메이션 이름(부분 일치)")
    ap.add_argument("--reference-px", type=int, default=None,
                    help="무기 픽셀 수 기준. 안 주면 프레임들의 중앙값을 쓴다")
    ap.add_argument("--json-out", default=None, help="결과를 JSON 으로 저장")
    args = ap.parse_args()

    A, rowa, (cw, ch) = load_sheet(args.armed, args.anim)
    B, rowb, _ = load_sheet(args.unarmed, args.anim)
    if len(A) != len(B):
        raise SystemExit("🔴 프레임 수가 다르다: %d vs %d" % (len(A), len(B)))

    raw = []
    for armed, unarmed in zip(A, B):
        w, r, body = frame_weapon(armed, unarmed)
        if w is None or w.sum() == 0:
            raw.append(None)
            continue
        ys, xs = np.where(w)
        raw.append({
            "px": int(w.sum()),
            "h": int(ys.max() - ys.min() + 1),
            "anchor": anchor_of(w, body),
        })

    px_list = sorted(d["px"] for d in raw if d)
    ref = args.reference_px or (px_list[len(px_list) // 2] if px_list else 0)

    print("애니메이션 %s · %d프레임 · 셀 %dx%d" % (rowa.get("animation", "?")[:30], len(A), cw, ch))
    print("무기 픽셀 기준 %d (±%d%%) · bbox 세로 상한 %d\n" % (
        ref, int(PIXEL_TOLERANCE * 100), int(ch * MAX_BBOX_HEIGHT_RATIO)))

    keys = []
    print(" f   무기px  bbox세로   판정   손 앵커")
    for i, d in enumerate(raw):
        if not d:
            print("%2d      —              🔴 없음" % i)
            keys.append(None)
            continue
        ok_px = abs(d["px"] - ref) <= ref * PIXEL_TOLERANCE
        ok_h = d["h"] <= ch * MAX_BBOX_HEIGHT_RATIO
        good = ok_px and ok_h
        why = "" if good else ("  픽셀 과다" if not ok_px else "  세로 과다")
        print("%2d   %6d   %6d    %s   %s%s" % (
            i, d["px"], d["h"], "✅ 키 " if good else "⚠️ 버림",
            "(%5.1f,%5.1f)" % d["anchor"] if good else "—", why))
        keys.append(d["anchor"] if good else None)

    n_key = sum(1 for k in keys if k)
    if n_key == 0:
        raise SystemExit("\n🔴 믿을 만한 프레임이 하나도 없다 — 두 벌의 자세가 너무 어긋난다.\n"
                         "   Unity 에서 손으로 잡아야 한다.")

    final, filled = interpolate(keys)
    print("\n키 %d / %d프레임 · 보간 %d" % (n_key, len(A), len(filled)))
    print("\n f   손 앵커(셀 기준)   출처")
    for i, (x, y) in enumerate(final):
        print("%2d   (%5.1f, %5.1f)   %s" % (i, x, y, "보간" if i in filled else "키"))

    xs = [p[0] for p in final]
    ys = [p[1] for p in final]
    print("\n이동 폭  x %.1fpx · y %.1fpx" % (max(xs) - min(xs), max(ys) - min(ys)))
    print("⚠️ 보간값은 추정이다 — Unity 에서 다듬을 **출발점**이지 최종값이 아니다.")

    if args.json_out:
        with open(args.json_out, "w", encoding="utf-8") as f:
            json.dump({
                "animation": rowa.get("animation"),
                "direction": rowa.get("direction"),
                "cell": {"width": cw, "height": ch},
                "reference_px": ref,
                "frames": [{"frame": i, "x": round(x, 1), "y": round(y, 1),
                            "source": "보간" if i in filled else "키"}
                           for i, (x, y) in enumerate(final)],
            }, f, ensure_ascii=False, indent=2)
        print("→ %s" % args.json_out)


if __name__ == "__main__":
    main()
