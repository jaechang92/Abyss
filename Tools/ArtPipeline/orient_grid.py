# -*- coding: utf-8 -*-
"""통짜 격자 시트에서 **방향이 뒤집힌 칸만 180도 돌린다.**

    # 판정만 (파일 안 건드림)
    python Tools/ArtPipeline/orient_grid.py Art_Source/weapons/dagger_64_grid.png --check

    # 지정한 칸을 돌려서 덮어쓴다
    python Tools/ArtPipeline/orient_grid.py Art_Source/weapons/dagger_64_grid.png --rotate 24-63

🔴 **왜 180도 회전인가 — 반대각 대칭은 각도를 세운다** (2026-09-10 실측)

  단검은 대각선으로 놓인 물건이라 「끝을 맞바꾸는」 변환이 둘 있다:

      A 180도 회전      각도 유지, 끝만 맞바꿈           25도 -> 25도
      B 반대각(↗↙) 대칭  각도가 90도에서 접힘             25도 -> 65도

  뒤집힌 후보(24~63)는 원래 **얕은 각도(약 25도)**로 그려졌고 정상 후보(0~23)는 약 40도다.
  B 를 쓰면 65도로 서 버려서 정상본보다 더 어긋난다. **A 가 맞다.**

  💡 B 가 이론상 유리한 경우가 있긴 하다 — 빛이 위에서 올 때 A 는 하이라이트를 아래로
     옮긴다. 그런데 32px 단검에서는 그 차이가 안 보이고, **각도가 틀어지는 쪽이 훨씬 크게
     보인다.** 그래서 각도를 지키는 A 를 고른다.

🔑 **판정(`--check`)은 참고지 판결이 아니다.** 손잡이(갈색 계열)와 날(무채색)의 무게중심을
   대각 축 `x+y` 에 투영해 비교하는데, **날까지 갈색인 후보**(뼈·나무·돌)와 **손잡이가 없는
   후보**(수정 파편)에서는 한쪽 표본이 비어 「판정불가」가 난다. 실제로 64칸 중 20칸이
   판정불가였고, 그중 #4 #8 은 판정이 났다면 오답이었을 것이다(날이 갈색이라 손잡이로 셌다).
   **눈으로 본 결과를 `--rotate` 로 직접 주는 것이 정답이고, `--check` 는 그 눈을 돕는다.**
"""

import argparse

import numpy as np
from PIL import Image

ALPHA_CUT = 128

# ── 판정 잣대 ────────────────────────────────────────────────────────────────
# 손잡이 = 나무/가죽의 따뜻한 색, 날 = 무채색 금속. 둘 다 최소 표본이 있어야 비교가 성립한다.
WOOD_HUE = (10.0, 50.0)
WOOD_SAT_MIN = 0.30
WOOD_VAL_MIN = 0.12
METAL_SAT_MAX = 0.22
MIN_SAMPLE = 6


def parseIndices(spec, total):
    """"24-63,7,10" 같은 표기를 인덱스 집합으로."""
    out = set()
    for part in spec.split(","):
        part = part.strip()
        if not part:
            continue
        if "-" in part:
            lo, hi = part.split("-", 1)
            out.update(range(int(lo), int(hi) + 1))
        else:
            out.add(int(part))
    bad = sorted(i for i in out if not 0 <= i < total)
    if bad:
        raise SystemExit("🔴 칸 범위를 벗어났다(0~%d): %s" % (total - 1, bad))
    return sorted(out)


def toHsv(cell):
    """RGB 배열 -> (h[0~360], s, v). PIL 변환은 팔레트를 뭉개서 직접 계산한다."""
    rgb = cell[..., :3] / 255.0
    r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    mx = rgb.max(axis=-1)
    mn = rgb.min(axis=-1)
    d = mx - mn
    s = np.where(mx > 0, d / np.maximum(mx, 1e-6), 0.0)
    h = np.zeros_like(mx)
    lit = d > 1e-6
    idx = (mx == r) & lit
    h[idx] = ((g - b)[idx] / d[idx]) % 6
    idx = (mx == g) & lit
    h[idx] = ((b - r)[idx] / d[idx]) + 2
    idx = (mx == b) & lit
    h[idx] = ((r - g)[idx] / d[idx]) + 4
    return h * 60.0, s, mx


def judge(cell):
    """(판정, 설명). 판정은 True=정상 / False=뒤집힘 / None=판정불가."""
    alpha = cell[..., 3] > ALPHA_CUT
    if alpha.sum() < 10:
        return None, "빈 칸"

    h, s, v = toHsv(cell)
    size = cell.shape[0]
    ys, xs = np.mgrid[0:size, 0:size]
    axis = (xs + ys).astype(float)  # 작을수록 좌상, 클수록 우하

    wood = alpha & (h >= WOOD_HUE[0]) & (h <= WOOD_HUE[1]) & (s >= WOOD_SAT_MIN) & (v > WOOD_VAL_MIN)
    metal = alpha & (s < METAL_SAT_MAX)
    if wood.sum() < MIN_SAMPLE or metal.sum() < MIN_SAMPLE:
        return None, "판정불가 (손잡이 %d px · 날 %d px)" % (wood.sum(), metal.sum())

    gap = axis[wood].mean() - axis[metal].mean()  # 양수 = 손잡이가 우하 = 정상
    return gap > 0, "손잡이 %.1f · 날 %.1f · 차 %+.1f" % (axis[wood].mean(), axis[metal].mean(), gap)


def loadGrid(path, cellSize):
    im = Image.open(path).convert("RGBA")
    if im.width % cellSize or im.height % cellSize:
        raise SystemExit("🔴 %dx%d 는 셀 %d 로 안 나눠떨어진다" % (im.width, im.height, cellSize))
    return im, im.width // cellSize, im.height // cellSize


def cellArray(im, index, cols, cellSize):
    row, col = divmod(index, cols)
    box = (col * cellSize, row * cellSize, (col + 1) * cellSize, (row + 1) * cellSize)
    return np.array(im.crop(box)).astype(float), box


def runCheck(im, cols, rows, cellSize):
    total = cols * rows
    print("%d칸 (셀 %dpx · %d열)\n" % (total, cellSize, cols))
    print("%4s  %-10s  %s" % ("칸", "판정", "근거"))
    suspect, unknown = [], []
    for i in range(total):
        arr, _ = cellArray(im, i, cols, cellSize)
        ok, why = judge(arr)
        if ok is None:
            label = "판정불가"
            unknown.append(i)
        elif ok:
            label = "정상"
        else:
            label = "뒤집힘"
            suspect.append(i)
        print("%4d  %-10s  %s" % (i, label, why))
    print("\n뒤집힘 %d칸: %s" % (len(suspect), suspect))
    print("판정불가 %d칸 (눈으로 볼 것): %s" % (len(unknown), unknown))
    return suspect, unknown


def runRotate(im, indices, cols, cellSize, path):
    for i in indices:
        row, col = divmod(i, cols)
        box = (col * cellSize, row * cellSize, (col + 1) * cellSize, (row + 1) * cellSize)
        im.paste(im.crop(box).rotate(180), box)
    im.save(path)
    print("%d칸 180도 회전 후 저장: %s" % (len(indices), path))
    print("돌린 칸: %s" % indices)


def main():
    ap = argparse.ArgumentParser(description="격자 시트에서 뒤집힌 칸만 180도 회전")
    ap.add_argument("sheet", help="통짜 격자 PNG")
    ap.add_argument("--cell", type=int, default=32, help="셀 한 변 픽셀 (기본 32)")
    ap.add_argument("--check", action="store_true", help="판정만 하고 파일은 안 건드린다")
    ap.add_argument("--rotate", default=None, help='돌릴 칸. 예: "24-63" 또는 "4,26,27"')
    args = ap.parse_args()

    im, cols, rows = loadGrid(args.sheet, args.cell)

    if args.check or not args.rotate:
        runCheck(im, cols, rows, args.cell)
        if not args.rotate:
            return

    indices = parseIndices(args.rotate, cols * rows)
    runRotate(im, indices, cols, args.cell, args.sheet)


if __name__ == "__main__":
    main()
