# -*- coding: utf-8 -*-
"""낱장 프레임을 **캔버스 그대로** 가로 한 줄 시트로 깐다.

    python Tools/ArtPipeline/pack_frames.py Art_Source/characters/knight_red/frames/idle_east \\
        --out Art_Source/characters/knight_red/idle_east_sheet.png

🔴 **트리밍하지 않는 것이 요점이다.**

  `repack_grid.py` 는 반대 일을 한다 — 통짜 시트를 빈 줄로 갈라 후보를 **떼어내** 다시 깐다.
  그건 무기처럼 **서로 무관한 낱개**를 고를 때 맞는 공정이다.

  애니메이션 프레임은 정반대다. 프레임끼리 **같은 좌표계에 있어야** 한다 —
  PixelLab 이 준 92x92 캔버스 안에서 캐릭터가 움직이는 것이 애니메이션의 내용이고,
  프레임마다 bbox 로 잘라 붙이면 **그 움직임이 지워지고 대신 재생 중에 덜덜 떨린다.**
  잘린 뒤에는 어느 프레임이 얼마나 잘렸는지 알 수 없어 되돌릴 수도 없다.

🔑 **그래서 이 도구는 아무것도 판정하지 않는다.** 순서대로 옆에 붙이기만 한다.
   Unity 는 `Grid By Cell Size` 에 캔버스 크기를 그대로 걸면 된다 — 출력이 그 값을 알려 준다.

⚠️ 캔버스 크기가 프레임마다 다르면 **거부한다.** 그 경우 시트의 격자가 성립하지 않고,
   조용히 늘려 맞추면 위 「덜덜 떨림」이 다른 경로로 돌아온다.
"""

import argparse
import os

from PIL import Image


def frameFiles(srcDir):
    """숫자 파일명 순서대로. `10.png` 가 `2.png` 앞에 오는 사전식 정렬을 피한다."""
    names = [f for f in os.listdir(srcDir) if f.lower().endswith(".png")]
    numbered = []
    for name in names:
        stem = os.path.splitext(name)[0]
        if not stem.isdigit():
            raise SystemExit(f"프레임 파일명이 숫자가 아니다: {name} — 0.png, 1.png ... 로 둘 것")
        numbered.append((int(stem), name))
    numbered.sort()
    return [os.path.join(srcDir, name) for _, name in numbered]


def main():
    ap = argparse.ArgumentParser(description="낱장 프레임을 캔버스 그대로 가로 시트로 깐다")
    ap.add_argument("srcDir", help="0.png, 1.png ... 이 든 폴더")
    ap.add_argument("--out", required=True, help="출력 시트 PNG")
    args = ap.parse_args()

    paths = frameFiles(args.srcDir)
    if not paths:
        raise SystemExit(f"프레임이 없다: {args.srcDir}")

    images = [Image.open(p).convert("RGBA") for p in paths]
    sizes = {im.size for im in images}
    if len(sizes) != 1:
        raise SystemExit(f"캔버스 크기가 섞여 있다 {sorted(sizes)} — 격자가 성립하지 않는다")

    cellWidth, cellHeight = images[0].size
    sheet = Image.new("RGBA", (cellWidth * len(images), cellHeight), (0, 0, 0, 0))
    for index, image in enumerate(images):
        sheet.paste(image, (index * cellWidth, 0))

    os.makedirs(os.path.dirname(os.path.abspath(args.out)), exist_ok=True)
    sheet.save(args.out)

    print(f"{args.out}")
    print(f"  프레임 {len(images)}장 · 시트 {sheet.size[0]}x{sheet.size[1]}")
    print(f"  Unity: Grid By Cell Size {cellWidth}x{cellHeight}")


if __name__ == "__main__":
    main()
