# -*- coding: utf-8 -*-
"""격자 시트의 무기 후보를 **몸의 손 앵커에 얹어 본다** — 화풍이 붙는지는 합쳐 봐야 안다.

    python Tools/ArtPipeline/mount_weapon.py 몸.png Art_Source/weapons/sword_64_grid.png \\
        --indices 0,5,12 --hand 53.8,61.6 --rotate -90 --grip left --out 결과.png

🔴 **왜 이 도구가 생겼나** (2026-09-10)

  무기를 몸과 분리해 만들기로 했는데(`10_BIBLE/08-silhouette §1-C`), 분리한 순간
  **「따로 보면 멀쩡한 두 그림이 합치면 안 붙는다」**는 위험이 생긴다. 실제로 무기 후보는
  갈색·금색 아이콘 톤이고 앵커 캐릭터는 어둡고 거친 픽셀이다. **얹어 봐야 판정된다.**

🔑 **회전은 90도 배수만 쓴다.** 픽셀아트를 임의 각도로 돌리면 계단이 뭉개져서, 안 붙는
   원인이 화풍인지 회전 손상인지 못 가린다. 판정용 자세는 90도 배수로 만들고, 실제
   휘두르는 각도는 Unity Transform 이 런타임에 돌린다(§1-C 미결).

🔑 **손잡이 끝(grip)을 손 앵커에 맞춘다.** `extract_weapon.py` 가 앵커에서 뽑는 손 좌표가
   「무기의 몸 쪽 끝」이므로 같은 규약으로 맞물린다. `--grip` 은 **회전 뒤** 기준이다.

⚠️ **앞뒤 관계는 「무기가 위」로 가정한다.** east 를 볼 때 드는 손이 앞이라 대개 맞지만,
   방패를 등에 지거나 양손 무기를 반대 손으로 잡는 자세는 이 가정이 깨진다 —
   §1-C 가 미결로 남겨 둔 자리다.
"""

import argparse

import numpy as np
from PIL import Image

ALPHA_CUT = 128
EDGE_BAND = 1.5  # 끝점 판정에 함께 묶을 투영 거리

# grip 이름 -> 「그 방향으로 가장 먼 픽셀」을 찾는 벡터 (x, y). y 는 아래가 +.
GRIP_DIRS = {
    "left": (-1, 0), "right": (1, 0), "top": (0, -1), "bottom": (0, 1),
    "top-left": (-1, -1), "top-right": (1, -1),
    "bottom-left": (-1, 1), "bottom-right": (1, 1),
}


def parseIndices(spec):
    out = []
    for part in spec.split(","):
        part = part.strip()
        if not part:
            continue
        if "-" in part.lstrip("-") and not part.startswith("-"):
            lo, hi = part.split("-", 1)
            out.extend(range(int(lo), int(hi) + 1))
        else:
            out.append(int(part))
    return out


def cellOf(sheet, index, cellSize):
    cols = sheet.width // cellSize
    row, col = divmod(index, cols)
    return sheet.crop((col * cellSize, row * cellSize, (col + 1) * cellSize, (row + 1) * cellSize))


def orient(cell, rotate, flip):
    """90도 배수 회전 + 좌우 뒤집기. 순서는 뒤집기 -> 회전."""
    if rotate % 90:
        raise SystemExit("🔴 회전은 90도 배수만 된다 (받은 값 %d). 임의 각도는 픽셀을 뭉갠다." % rotate)
    if flip:
        cell = cell.transpose(Image.FLIP_LEFT_RIGHT)
    return cell.rotate(-rotate, expand=True) if rotate else cell


def gripPoint(cell, direction):
    """무기의 손잡이 끝. 지정한 방향으로 가장 먼 픽셀들의 무게중심.

    `center` 는 예외다 — 활·방패는 손이 **끝이 아니라 한가운데**를 잡는다.
    """
    alpha = np.array(cell)[..., 3] > ALPHA_CUT
    if not alpha.any():
        raise SystemExit("🔴 빈 칸이다")
    ys, xs = np.where(alpha)
    if direction == "center":
        return (xs.min() + xs.max()) / 2.0, (ys.min() + ys.max()) / 2.0
    dx, dy = GRIP_DIRS[direction]
    proj = xs * dx + ys * dy
    edge = proj >= proj.max() - EDGE_BAND
    return xs[edge].mean(), ys[edge].mean()


def mount(body, cell, hand, grip):
    """무기를 손 앵커에 맞춰 몸 위에 올린 새 이미지."""
    gx, gy = gripPoint(cell, grip)
    out = body.copy()
    # 무기 캔버스의 (gx,gy) 가 몸의 hand 에 오도록 붙인다
    offset = (int(round(hand[0] - gx)), int(round(hand[1] - gy)))
    layer = Image.new("RGBA", body.size, (0, 0, 0, 0))
    layer.paste(cell, offset)
    out.alpha_composite(layer)
    return out


def contactSheet(images, labels, scale, pad=15):
    cw, ch = images[0].size
    W = len(images) * cw * scale
    H = ch * scale + pad
    from PIL import ImageDraw
    cv = Image.new("RGBA", (W, H), (34, 34, 40, 255))
    d = ImageDraw.Draw(cv)
    for i, (im, label) in enumerate(zip(images, labels)):
        big = im.resize((cw * scale, ch * scale), Image.NEAREST)
        bg = Image.new("RGBA", big.size, (250, 250, 250, 255))
        bg.alpha_composite(big)
        x = i * cw * scale
        cv.paste(bg, (x, pad))
        d.rectangle([x, pad, x + cw * scale - 1, pad + ch * scale - 1], outline=(95, 95, 105, 255))
        d.text((x + 4, 3), label, fill=(255, 215, 110, 255))
    return cv


def main():
    ap = argparse.ArgumentParser(description="무기 후보를 몸의 손 앵커에 얹어 본다")
    ap.add_argument("body", help="무기 없는 몸 PNG")
    ap.add_argument("sheet", help="무기 격자 시트 PNG")
    ap.add_argument("--indices", required=True, help='얹을 칸. 예: "0,5,12" 또는 "0-7"')
    ap.add_argument("--hand", required=True, help='손 앵커 "x,y" (extract_weapon.py 가 준다)')
    ap.add_argument("--cell", type=int, default=32, help="시트 셀 한 변 (기본 32)")
    ap.add_argument("--rotate", type=int, default=0, help="무기 회전, 90도 배수. 양수=시계방향")
    ap.add_argument("--flip", action="store_true", help="회전 전에 좌우 뒤집기")
    ap.add_argument("--grip", default="bottom", choices=sorted(GRIP_DIRS) + ["center"],
                    help="회전 뒤 손잡이가 있는 쪽. 활·방패는 center")
    ap.add_argument("--scale", type=int, default=4, help="결과 확대 배율")
    ap.add_argument("--reference", default=None, help="맨 앞에 나란히 둘 원본(무기 있는 몸) PNG")
    ap.add_argument("--out", required=True, help="결과 PNG")
    args = ap.parse_args()

    body = Image.open(args.body).convert("RGBA")
    sheet = Image.open(args.sheet).convert("RGBA")
    hand = tuple(float(v) for v in args.hand.split(","))

    images, labels = [], []
    if args.reference:
        images.append(Image.open(args.reference).convert("RGBA"))
        labels.append("원본")
    images.append(body)
    labels.append("맨몸")

    for i in parseIndices(args.indices):
        cell = orient(cellOf(sheet, i, args.cell), args.rotate, args.flip)
        images.append(mount(body, cell, hand, args.grip))
        labels.append("#%d" % i)

    contactSheet(images, labels, args.scale).save(args.out)
    print("손 앵커 (%.1f, %.1f) · 회전 %d도%s · 손잡이 %s" % (
        hand[0], hand[1], args.rotate, " · 좌우뒤집기" if args.flip else "", args.grip))
    print("%d개 얹음 → %s" % (len(images) - (2 if args.reference else 1), args.out))


if __name__ == "__main__":
    main()
