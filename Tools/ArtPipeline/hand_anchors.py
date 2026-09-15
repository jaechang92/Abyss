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
import math
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


# ── 돌출부 모드 ────────────────────────────────────────────────────────────────
# 🔑 **무장본이 없어도 손을 찾는다** (2026-09-14 실측으로 추가).
#
#   diff 모드는 armed/unarmed 두 벌을 요구하는데, 9상태를 No Weapon 한 벌로 통일하고 나니
#   짝이 없어져 그냥은 못 돌게 됐다. armed 를 다시 뽑으면 gen 45 쯤 든다.
#
#   그런데 **순수 측면(east)이라 팔은 항상 몸보다 앞으로 나오고 망토는 뒤로 흐른다.**
#   그래서 머리·다리를 뺀 실루엣의 **최전방 돌출부**가 곧 손이다. gen 0 이다.
#
#   ⚠️ **모든 프레임에서 되지는 않는다.** 팔이 몸통 실루엣 안에 들어간 프레임(걷기 등)은
#   잡을 돌출이 없다. 그래서 diff 모드와 **같은 구조**로 간다 — 잡히는 것만 키로 쓰고 보간한다.
#   판별은 무기 픽셀 수가 아니라 **돌출량**이다(몸통 앞면 기준선에서 얼마나 튀어나왔나).
#
#   실측 (KnightRed · 2026-09-14)
#       AttackHeavy  키 7/9   돌출 4~14
#       AttackLight  키 5/9   돌출 1~22
#       Idle         키 6/9   돌출 4~8
#       Walk         키 0/8   돌출 2~4   <- 안 잡힌다. 대신 손이 몇 px 밖에 안 움직여 오차도 작다

HEAD_CUT = 0.28          # 실루엣 위 28% = 머리·후드. 든 손보다 후드가 앞설 때가 있다
LEG_CUT = 0.70           # 아래 30% = 다리·망토 밑단
FIST_RADIUS2 = 16        # 최전방점 반경 4px 안을 주먹 덩어리로 본다
TORSO_CUT = 0.62        # 몸통 무게중심을 잴 때의 아래 경계(다리 제외)
DRAWN_ANGLE = 45.0      # 무기 그림이 그려져 있는 각도. weapon_grips.py 규약과 같아야 한다
MIN_PROTRUSION = 6.0     # 이만큼 튀어나와야 키로 쓴다


def load_unity_sheet(path):
    """Unity 시트(가로로 이어 붙인 정사각 칸) 를 프레임 배열로."""
    im = np.array(Image.open(path).convert("RGBA"))
    cell = im.shape[0]
    if im.shape[1] % cell:
        raise SystemExit("🔴 %s 가로(%d)가 칸(%d)의 배수가 아니다" % (path, im.shape[1], cell))
    n = im.shape[1] // cell
    return [im[:, i * cell:(i + 1) * cell] for i in range(n)], cell


def detect_protrusion(frame):
    """한 프레임에서 (손 x, 손 y, 돌출량). 좌표는 칸 왼쪽 위 기준 px."""
    alpha = frame[:, :, 3] > ALPHA_CUT
    ys = np.where(alpha.max(axis=1))[0]
    if not len(ys):
        return None
    top, bot = ys.min(), ys.max()
    h = bot - top + 1

    m = alpha.copy()
    m[:int(top + h * HEAD_CUT)] = False
    m[int(top + h * LEG_CUT):] = False
    if not m.any():
        return None

    # 행마다 가장 앞(오른쪽) 픽셀 → 그 중앙값이 몸통 앞면 기준선
    front = np.array([np.where(m[y])[0].max() if m[y].any() else -1 for y in range(m.shape[0])])
    valid = front[front >= 0]
    x_tip = int(valid.max())
    body_line = float(np.median(valid))

    y_tip = int(np.where(m[:, x_tip])[0].mean())
    yy, xx = np.mgrid[0:m.shape[0], 0:m.shape[1]]
    fist = m & ((xx - x_tip) ** 2 + (yy - y_tip) ** 2 <= FIST_RADIUS2)
    cx = float((fist * xx).sum() / fist.sum())
    cy = float((fist * yy).sum() / fist.sum())
    return cx, cy, x_tip - body_line


def torso_center(frame):
    """몸통 밴드(머리·다리 제외)의 알파 무게중심. 팔 벡터의 시작점이다."""
    alpha = frame[:, :, 3] > ALPHA_CUT
    ys = np.where(alpha.max(axis=1))[0]
    if not len(ys):
        return None
    top, bot = ys.min(), ys.max()
    h = bot - top + 1
    m = alpha.copy()
    m[:int(top + h * HEAD_CUT)] = False
    m[int(top + h * TORSO_CUT):] = False
    if not m.any():
        return None
    yy, xx = np.mgrid[0:m.shape[0], 0:m.shape[1]]
    return float((m * xx).sum() / m.sum()), float((m * yy).sum() / m.sum())


def arm_angle(frame, hand_x, hand_y):
    """몸통 중심 → 손 벡터의 각도(도). **무기 각도의 출발점**이다.

    🔑 팔이 어디를 향하는지가 곧 무기가 향하는 쪽이다 — 손목의 꺾임까지는 못 잡지만,
       0(그려진 45°) 으로 두는 것보다 훨씬 낫다. 2026-09-15 실측:
       Idle 은 -83° 로 검을 내리고, AttackHeavy 는 팔을 들면 -28° 로 같이 올라간다.

    ⚠️ **반환값은 「그려진 각도로부터의 회전량」이다.** 무기 그림이 45° 대각선으로
       그려져 있으므로(`weapon_grips.py` 규약) 목표 방향에서 45 를 뺀다.
       규약이 바뀌면 이 상수도 같이 바꿔야 한다.
    """
    tc = torso_center(frame)
    if tc is None:
        return 0.0
    tx, ty = tc
    # 화면 y 는 아래가 + 이므로 뒤집어 수학 좌표로 만든다
    theta = math.degrees(math.atan2(-(hand_y - ty), hand_x - tx))
    return round(theta - DRAWN_ANGLE, 1)


def run_protrusion(args):
    sheets = args.sheets
    pivot_x = args.pivot_x
    pivot_y_px = None
    out_all = []

    for path in sheets:
        frames, cell = load_unity_sheet(path)
        if pivot_y_px is None:
            pivot_y_px = cell - args.pivot_y * cell      # 위 기준 y
        px_cx = pivot_x * cell

        raw = [detect_protrusion(f) for f in frames]
        keys = [(d[0], d[1]) if d and d[2] >= args.min_protrusion else None for d in raw]
        n_key = sum(1 for k in keys if k)

        print()
        print("%s · %d프레임 · 칸 %d" % (os.path.basename(path), len(frames), cell))
        print(" f   돌출량   판정   손 앵커(px)")
        for i, d in enumerate(raw):
            if not d:
                print("%2d      —      🔴 없음" % i)
                continue
            good = keys[i] is not None
            print("%2d   %6.1f   %s   %s" % (
                i, d[2], "✅ 키 " if good else "⚠️ 낮음",
                "(%5.1f,%5.1f)" % (d[0], d[1]) if good else "—"))

        if n_key == 0:
            # 🔑 **빈칸으로 내보내지 않는다.** 임포트가 빈칸을 만나면 무기가 원점에 박힌다.
            #    돌출이 낮다는 것은 「손이 몸에 붙어 있다」는 뜻이라, 그 위치가 이미 쓸 만한 근사다.
            #    다만 키가 아니므로 `제안` 으로 표시해 Unity 에서 다듬을 자리임을 남긴다.
            #
            #    0키가 나오는 세 가지는 알고리즘 한계가 아니라 **동작의 성질**이다:
            #      걷기   팔이 몸통 실루엣 안. 손 이동 폭이 몇 px 라 고정값으로 충분하다
            #      대시   몸이 앞으로 기울어 팔이 몸 아래로 들어간다. 돌진 자세는 무기가 몸에 붙어야 한다
            #      사망   🔴 몸이 수평으로 눕는다 — 「팔이 앞으로 나온다」는 이 검출의 전제가 깨진다
            print("🔴 키가 없다 — 손이 몸에 붙어 있는 동작이다(걷기·대시·사망).")
            print("   낮은 돌출 위치를 `제안` 으로 채운다. Unity 에서 한 점만 잡아 전체에 복사하면 된다.")
            med = sorted((d[0], d[1]) for d in raw if d)
            fallback = med[len(med) // 2] if med else (px_cx, pivot_y_px)
            out_all.append({
                "sheet": os.path.basename(path), "frames": len(frames), "keys": 0,
                "needsManual": True,
                "anchors": [{
                    "frame": i,
                    "xPx": round(fallback[0], 1), "yPx": round(fallback[1], 1),
                    "x": round((fallback[0] - px_cx) / args.ppu, 4),
                    "y": round((pivot_y_px - fallback[1]) / args.ppu, 4),
                    "source": "제안",
                    "angle": arm_angle(frames[i], fallback[0], fallback[1]),
                } for i in range(len(frames))],
            })
            continue

        final, filled = interpolate(keys)
        print("키 %d / %d · 보간 %d" % (n_key, len(frames), len(filled)))
        anchors = []
        for i, (x, y) in enumerate(final):
            anchors.append({
                "frame": i,
                "xPx": round(x, 1), "yPx": round(y, 1),
                # Unity 지역 좌표: 피벗 기준 · 위가 +y · PPU 로 나눈 유닛
                "x": round((x - px_cx) / args.ppu, 4),
                "y": round((pivot_y_px - y) / args.ppu, 4),
                "source": "보간" if i in filled else "키",
                # 그려진 각도(45°)로부터의 회전량. 임포터가 클립별로 쓸지 말지 정한다
                "angle": arm_angle(frames[i], x, y),
            })
        xs = [a["xPx"] for a in anchors]
        ys = [a["yPx"] for a in anchors]
        print("이동 폭  x %.1fpx · y %.1fpx" % (max(xs) - min(xs), max(ys) - min(ys)))
        out_all.append({"sheet": os.path.basename(path), "frames": len(frames),
                        "keys": n_key, "needsManual": False, "anchors": anchors})

    print()
    print("⚠️ 보간값은 추정이다 — Unity 에서 다듬을 **출발점**이지 최종값이 아니다.")
    if args.json_out:
        with open(args.json_out, "w", encoding="utf-8") as f:
            json.dump({"mode": "protrusion",
                       "pivot": {"x": pivot_x, "y": args.pivot_y},
                       "ppu": args.ppu,
                       "minProtrusion": args.min_protrusion,
                       "sheets": out_all}, f, ensure_ascii=False, indent=2)
        print("→ %s" % args.json_out)


def run_diff(args):
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
                "referencePx": ref,
                "frames": [{"frame": i, "x": round(x, 1), "y": round(y, 1),
                            "source": "보간" if i in filled else "키"}
                           for i, (x, y) in enumerate(final)],
            }, f, ensure_ascii=False, indent=2)
        print("→ %s" % args.json_out)


def main():
    ap = argparse.ArgumentParser(description="애니메이션 프레임별 손 앵커 (믿을 수 있는 것만 키로)")
    sub = ap.add_subparsers(dest="mode", required=True)

    d = sub.add_parser("diff", help="무장/비무장 두 벌 diff (서버 시트 폴더 2개)")
    d.add_argument("armed", help="무기 든 몸의 시트 폴더")
    d.add_argument("unarmed", help="무기 없는 몸의 시트 폴더")
    d.add_argument("--anim", default=None, help="애니메이션 이름(부분 일치)")
    d.add_argument("--reference-px", type=int, default=None,
                   help="무기 픽셀 수 기준. 안 주면 프레임들의 중앙값을 쓴다")
    d.add_argument("--json-out", default=None, help="결과를 JSON 으로 저장")

    p = sub.add_parser("protrusion", help="비무장 한 벌에서 최전방 돌출부 검출 (Unity 시트)")
    p.add_argument("sheets", nargs="+", help="Unity 시트 PNG (가로로 이어 붙인 정사각 칸)")
    p.add_argument("--pivot-x", type=float, default=0.5, dest="pivot_x")
    p.add_argument("--pivot-y", type=float, default=0.163, dest="pivot_y",
                   help="칸 아래에서부터의 비율. 기본 0.163 = 92px 칸의 발밑 15px")
    p.add_argument("--ppu", type=float, default=32.0, help="Pixels Per Unit")
    p.add_argument("--min-protrusion", type=float, default=MIN_PROTRUSION, dest="min_protrusion")
    p.add_argument("--json-out", default=None, help="결과를 JSON 으로 저장")

    args = ap.parse_args()
    if args.mode == "protrusion":
        run_protrusion(args)
    else:
        run_diff(args)


if __name__ == "__main__":
    main()
