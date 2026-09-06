# -*- coding: utf-8 -*-
"""
make_palettes.py — palettes/_palettes.txt 의 hex 목록을 PNG 로 굽는다.

산출물을 PixelLab 의 color_image 에 넣으면 생성이 그 색 밖으로 나갈 수 없다.
프롬프트로 "어둡게 그려 달라"고 부탁하는 것과 달리 이건 강제다.

    python Art_Source/prompts_env/make_palettes.py

🔑 PIL 을 안 쓴다. 색 몇 개짜리 격자 PNG 는 표준 라이브러리만으로 충분하고,
   아트 담당자가 의존성 설치 없이 바로 돌릴 수 있는 쪽이 낫다.
"""
import os
import re
import struct
import sys
import zlib

ROOT = os.path.dirname(os.path.abspath(__file__))
SRC = os.path.join(ROOT, "palettes", "_palettes.txt")
OUT_DIR = os.path.join(ROOT, "palettes")

CELL = 16  # 색 하나가 차지하는 정사각 픽셀 크기
HEX = re.compile(r"\b([0-9A-Fa-f]{6})\b")


def parse_palettes(path):
    """[scene] 아래의 hex 를 모은다. # 뒤는 주석이라 잘라 버린다."""
    out, current = {}, None
    with open(path, encoding="utf-8") as f:
        for line in f:
            line = line.split("#", 1)[0].strip()
            if not line:
                continue
            header = re.fullmatch(r"\[([a-z0-9_]+)\]", line)
            if header:
                current = header.group(1)
                out[current] = []
                continue
            if current:
                out[current].extend(HEX.findall(line))
    return {k: v for k, v in out.items() if v}


def write_png(path, width, height, rows):
    """rows = [bytes(RGB * width)] * height. 8bit RGB, 무압축 필터."""
    raw = b"".join(b"\x00" + r for r in rows)

    def chunk(tag, data):
        body = tag + data
        return struct.pack(">I", len(data)) + body + struct.pack(">I", zlib.crc32(body))

    header = struct.pack(">IIBBBBB", width, height, 8, 2, 0, 0, 0)
    with open(path, "wb") as f:
        f.write(b"\x89PNG\r\n\x1a\n")
        f.write(chunk(b"IHDR", header))
        f.write(chunk(b"IDAT", zlib.compress(raw, 9)))
        f.write(chunk(b"IEND", b""))


def build_one(name, colors):
    width, height = CELL * len(colors), CELL
    rgb = [bytes.fromhex(c) for c in colors]
    row = b"".join(px * CELL for px in rgb)
    write_png(os.path.join(OUT_DIR, f"{name}.png"), width, height, [row] * height)
    return width, height


def main():
    if not os.path.exists(SRC):
        raise SystemExit(f"🔴 팔레트 원본이 없다: {SRC}")

    palettes = parse_palettes(SRC)
    wanted = sys.argv[1:] or sorted(palettes)

    for name in wanted:
        if name not in palettes:
            print(f"  건너뜀 (팔레트 없음): {name}")
            continue
        colors = palettes[name]
        # ⚠️ 색이 너무 적으면 모델이 그라데이션을 못 만들어 그림이 납작해진다.
        if len(colors) < 6:
            print(f"  ⚠️ {name}: 색이 {len(colors)}개뿐이다. 8~12개를 권한다")
        w, h = build_one(name, colors)
        print(f"  {name}.png  ({len(colors)}색, {w}x{h})")

    print(f"\n완료 → {os.path.relpath(OUT_DIR, os.path.dirname(ROOT))}")
    print("   PixelLab 의 color_image / Target Palette 칸에 넣는다.")


if __name__ == "__main__":
    main()
