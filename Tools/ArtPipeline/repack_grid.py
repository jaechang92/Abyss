# -*- coding: utf-8 -*-
"""통짜 시트를 **빈 줄로 갈라** 후보를 하나씩 떼고, 균일 격자로 다시 깐다.

    python Tools/ArtPipeline/repack_grid.py Art_Source/weapons/shield_64_grid.png \\
        --out Art_Source/weapons/shield_64_packed.png

🔴 **왜 필요한가 — 방패는 `Grid By Cell Size 32` 로 안 잘린다** (2026-09-10 실측)

  검·활·단검은 32칸 안에 얌전히 들어가 있는데(셀 경계에 걸친 픽셀 0), **방패만 넘친다:**

      경계 7곳 전부에서 70~128 열이 위아래로 맞닿아 있다
      밴드 시작 y = 3 · 40 · 72 · 104 · 136 · 168 · 197 · 229
      첫 밴드는 36px 로 **셀(32)보다 크다**

  즉 Unity 에서 32 로 자르면 방패마다 아래가 잘리고 그 조각이 아랫칸 위에 붙는다.
  이건 `fetch_object_grid.py` 가 손대는 다운로드 문제가 아니라 **원본 배치**의 문제다.

🔑 **빈 줄이 곧 경계다.** 후보 사이에는 완전히 투명한 가로줄·세로줄이 있으므로, 32 라는
   가정을 버리고 **빈 줄로 갈라내면** 밴드 폭이 제각각이어도 정확히 64칸이 떨어진다.

💡 **다시 깔 때의 칸 크기는 가장 큰 후보가 정한다.** 그래야 아무것도 안 잘리고,
   Unity 는 그 값으로 `Grid By Cell Size` 를 걸면 된다 — 출력이 그 값을 알려 준다.
"""

import argparse
import os

import numpy as np
from PIL import Image

ALPHA_CUT = 128


def bands(mask, axis):
    """비어 있지 않은 구간의 (시작, 끝) 목록. axis=0 이면 가로 밴드(세로로 자름)."""
    filled = mask.sum(axis=1 - axis) > 0
    out, start = [], None
    for i, v in enumerate(filled):
        if v and start is None:
            start = i
        elif not v and start is not None:
            out.append((start, i - 1))
            start = None
    if start is not None:
        out.append((start, len(filled) - 1))
    return out


def main():
    ap = argparse.ArgumentParser(description="빈 줄로 갈라 후보를 떼고 균일 격자로 다시 깐다")
    ap.add_argument("sheet", help="통짜 격자 PNG")
    ap.add_argument("--out", default=None, help="다시 깐 시트 PNG (안 주면 판정만)")
    ap.add_argument("--dump-dir", default=None, help="후보를 낱장 PNG 로도 저장할 폴더")
    ap.add_argument("--cols", type=int, default=8, help="다시 깔 때의 열 수 (기본 8)")
    ap.add_argument("--margin", type=int, default=1, help="칸 가장자리 여백 px (기본 1)")
    args = ap.parse_args()

    im = Image.open(args.sheet).convert("RGBA")
    mask = np.array(im)[..., 3] > ALPHA_CUT

    rowBands = bands(mask, axis=0)
    print("가로 밴드 %d개: %s" % (len(rowBands), [(a, b, b - a + 1) for a, b in rowBands]))

    cells = []
    for top, bottom in rowBands:
        strip = mask[top:bottom + 1, :]
        for left, right in bands(strip, axis=1):
            sub = strip[:, left:right + 1]
            ys, xs = np.where(sub)
            box = (left + xs.min(), top + ys.min(), left + xs.max() + 1, top + ys.max() + 1)
            cells.append(im.crop(box))

    if not cells:
        raise SystemExit("🔴 후보를 하나도 못 찾았다")

    w = max(c.width for c in cells)
    h = max(c.height for c in cells)
    # 여백 1px 을 둔다 — 칸을 꽉 채운 후보끼리 맞닿으면 잘라 낸 뒤 이웃 픽셀이 비쳐 든다
    pitch = max(w, h) + 2 * args.margin
    print("후보 %d개 · 최대 %dx%d · 여백 %d → 칸 %dpx" % (len(cells), w, h, args.margin, pitch))
    over = [i for i, c in enumerate(cells) if max(c.size) > 32]
    if over:
        print("⚠️ 32px 를 넘는 후보 %d개: %s" % (len(over), over[:12]))

    if args.dump_dir:
        os.makedirs(args.dump_dir, exist_ok=True)
        for i, c in enumerate(cells):
            c.save(os.path.join(args.dump_dir, "%02d.png" % i))
        print("낱장 저장 → %s" % args.dump_dir)

    if not args.out:
        return

    rows = (len(cells) + args.cols - 1) // args.cols
    packed = Image.new("RGBA", (args.cols * pitch, rows * pitch), (0, 0, 0, 0))
    for i, c in enumerate(cells):
        r, col = divmod(i, args.cols)
        # 칸 안에서 가운데. 홀짝 차이는 왼쪽·위로 몰아 픽셀을 안 흘린다
        x = col * pitch + (pitch - c.width) // 2
        y = r * pitch + (pitch - c.height) // 2
        packed.paste(c, (x, y))
    packed.save(args.out)
    print("→ %s  (%dx%d)" % (args.out, packed.width, packed.height))
    print("🔑 Unity: Grid By Cell Size **%dx%d**" % (pitch, pitch))


if __name__ == "__main__":
    main()
