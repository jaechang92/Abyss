# -*- coding: utf-8 -*-
"""무기 한 장을 **각도별로 미리 돌려** 가로 스트립으로 굽는다.

    python Tools/ArtPipeline/bake_weapon_angles.py Assets/Art/Sprites/Weapons/sword_00.png \
        --grip 0.214,0.2067 --angles 24 \
        --out Assets/Art/Sprites/Weapons/sword_angles.png

🔴 **왜 이 도구가 생겼나** (2026-09-15)

  무기 각도를 런타임 `Transform` 회전으로 주니 **칼날이 뭉갰다.** 픽셀아트를 임의 각도로
  돌리면 계단이 리샘플되고, 무기가 45° 대각선으로 그려져 있어 하필 **-45° 부근이 최악**이다
  (대각선이 수평으로 눌린다). 그래서 90° 배수로 스냅했더니 방향이 **네 개**뿐이라
  회전이 툭툭 끊겼다.

🔑 **각도를 미리 구우면 둘 다 없다.** 그리고 **gen 이 안 든다** —
   2026-09-14 에 「각도별 프레임은 돌린 그림이 아니라 새로 그린 그림」으로 정했는데,
   그 판단의 전제는 *「돌리면 naive 회전뿐」* 이었다. 픽셀아트 전용 회전은 결과가 다르다.

## 회전 방식 — RotSprite 계열

  ① 크게 키운다(기본 8배, 최근접)   ② 매끄럽게 돌린다(bicubic)
  ③ 블록마다 **최빈색**으로 되돌린다  ④ 원본 팔레트로 스냅한다

  ③이 요점이다. 평균을 내면 새 색이 생기고 픽셀아트가 뭉개진 사진이 된다.
  **가장 많이 차지한 색이 그 칸의 주인**이라고 보면 계단이 살아 있다.
  ④는 bicubic 이 만든 중간색을 원본 색으로 되돌린다.

🔑 **자루를 캔버스 중앙에 두고 돌린다.** 그래서 **모든 각도가 피벗을 공유한다**(0.5, 0.5) —
   각도마다 피벗을 따로 들 필요가 없고, Unity 에서 격자 슬라이스 한 번이면 끝난다.

⚠️ **캔버스는 「자루에서 가장 먼 픽셀」로 정해진다.** 무기가 길수록 칸이 커진다.
   칸 크기는 출력이 알려 주므로 그 값으로 Unity 격자를 잡을 것.

📌 각도 0 은 **그려진 그대로**다. 인덱스 i 는 시계방향으로 `-360*i/N` 도 돌린 그림이다.
   런타임은 목표 각도에서 가장 가까운 칸을 고른다.
"""

import argparse
import collections
import math
import os

from PIL import Image

ALPHA_CUT = 128


def gripPixel(image, gripX, gripY):
    """피벗 비율(아래가 +y) → 이미지 픽셀 좌표(위가 +y)."""
    return gripX * image.width, (1.0 - gripY) * image.height


def canvasRadius(image, gx, gy):
    """자루에서 가장 먼 불투명 픽셀까지의 거리. 캔버스 반지름이 된다."""
    px = image.load()
    far = 0.0
    for y in range(image.height):
        for x in range(image.width):
            if px[x, y][3] > ALPHA_CUT:
                far = max(far, math.hypot(x + 0.5 - gx, y + 0.5 - gy))
    return int(math.ceil(far)) + 2


def paletteOf(image):
    px = image.load()
    return {px[x, y][:3]
            for y in range(image.height) for x in range(image.width)
            if px[x, y][3] > ALPHA_CUT}


def snapToPalette(image, palette):
    """bicubic 이 만든 중간색을 원본 색으로 되돌린다."""
    px = image.load()
    cache = {}
    for y in range(image.height):
        for x in range(image.width):
            r, g, b, a = px[x, y]
            if a < ALPHA_CUT:
                px[x, y] = (0, 0, 0, 0)
                continue
            key = (r, g, b)
            if key not in cache:
                cache[key] = min(palette, key=lambda c: (c[0] - r) ** 2 + (c[1] - g) ** 2 + (c[2] - b) ** 2)
            px[x, y] = (*cache[key], 255)
    return image


def bakeAngle(image, gx, gy, radius, degrees, upscale, palette):
    """자루를 중앙에 둔 캔버스에 올려 RotSprite 계열로 돌린다."""
    size = 2 * radius
    base = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    base.alpha_composite(image, (int(round(radius - gx)), int(round(radius - gy))))
    if degrees % 90 == 0:
        # 90도 배수는 무손실이다 — 굳이 키웠다 줄이지 않는다
        return base.rotate(degrees, resample=Image.NEAREST, expand=False)

    big = base.resize((size * upscale, size * upscale), Image.NEAREST)
    big = big.rotate(degrees, resample=Image.BICUBIC, expand=False)
    out = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    src, dst = big.load(), out.load()
    for y in range(size):
        for x in range(size):
            counter = collections.Counter()
            for dy in range(upscale):
                for dx in range(upscale):
                    p = src[x * upscale + dx, y * upscale + dy]
                    counter[p if p[3] >= ALPHA_CUT else None] += 1
            best, _ = counter.most_common(1)[0]
            if best is not None:
                dst[x, y] = best
    return snapToPalette(out, palette)


def main():
    ap = argparse.ArgumentParser(description="무기를 각도별로 미리 돌려 스트립으로 굽는다")
    ap.add_argument("source", help="무기 PNG 한 장")
    ap.add_argument("--grip", required=True, help="자루 피벗 비율 'x,y' (Unity 규약: 아래가 +y)")
    ap.add_argument("--angles", type=int, default=24, help="각도 수 (기본 24 = 15도 간격)")
    ap.add_argument("--upscale", type=int, default=8, help="회전 전 확대 배율 (기본 8)")
    ap.add_argument("--out", required=True, help="출력 스트립 PNG")
    args = ap.parse_args()

    if args.angles < 4 or 360 % args.angles:
        raise SystemExit(f"🔴 각도 수 {args.angles} 는 360 의 약수여야 한다 (8·12·16·24·36 ...)")

    image = Image.open(args.source).convert("RGBA")
    gripX, gripY = (float(v) for v in args.grip.split(","))
    gx, gy = gripPixel(image, gripX, gripY)
    radius = canvasRadius(image, gx, gy)
    palette = paletteOf(image)

    step = 360.0 / args.angles
    frames = [bakeAngle(image, gx, gy, radius, -step * i, args.upscale, palette)
              for i in range(args.angles)]

    size = 2 * radius
    strip = Image.new("RGBA", (size * args.angles, size), (0, 0, 0, 0))
    for i, f in enumerate(frames):
        strip.paste(f, (i * size, 0))
    os.makedirs(os.path.dirname(os.path.abspath(args.out)), exist_ok=True)
    strip.save(args.out)

    print(args.out)
    print(f"  각도 {args.angles}개 · {step:g}도 간격 · 칸 {size}x{size} · 팔레트 {len(palette)}색")
    print(f"  자루가 칸 중앙이므로 **피벗은 전부 (0.5, 0.5)** 다")
    print(f"  Unity: Grid By Cell Size {size}x{size} · Pivot Center · PPU 32 · Point · 압축 없음")


main()
