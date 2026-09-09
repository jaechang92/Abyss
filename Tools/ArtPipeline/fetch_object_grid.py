# -*- coding: utf-8 -*-
"""PixelLab 오브젝트 후보를 받아 **원본 격자로 되붙인다.**

    python Tools/ArtPipeline/fetch_object_grid.py <object_id> --out Art_Source/weapons/shield_64_grid.png

🔴 **왜 되붙이나 — 도구가 잘못 자른다** (2026-09-10 실측)

  `create_1_direction_object` 는 후보 64장을 큰 격자에 그린 뒤 32px 칸으로 잘라 준다.
  그런데 **칸 크기와 그림 크기가 안 맞아 서로 침범한다:**

      frame 0~7    y 8~31  · 맨아랫줄 18px 참    -> 방패 아래가 잘렸다
      frame 8~13   y 0~31  · 맨윗줄 16px + 별개 덩어리 64px
                                                 -> 윗칸에서 잘려 넘어온 조각

  🔑 **잘려서 없어진 게 아니라 한 칸 아래로 밀려 있었다.** 8열 격자로 되붙이면
     경계에서 그림이 다시 이어진다 — 검증: 7/7 경계에서 위아래가 맞닿았다(70~128px).

💡 그래서 이 도구는 **자르지 않은 통짜 시트**를 만든다. Unity 스프라이트 에디터에서
   직접 슬라이스하는 것이 낫다 — 격자가 규칙적이라 Grid By Cell Size 로 한 번에 된다.

⚠️ **후보 수와 크기는 맞물려 있다** (`40_TOOLS/pixellab/profile.md §8`):
   ≤42px → 64장 · ≤85 → 16 · ≤170 → 4. 크게 뽑으면 후보가 준다.
"""

import argparse
import math
import os
import urllib.request

from PIL import Image

BASE = ("https://backblaze.pixellab.ai/file/pixellab-characters/objects"
        "/{account}/{object_id}/rotations/frame_{i}.png")
DEFAULT_ACCOUNT = "0915848d-a816-42a9-bbc1-2f9f72f2b442"

# 🔴 **User-Agent 를 줘야 한다.** 기본 urllib 헤더(`Python-urllib/3.x`)로 가면
#    저장소가 **403 Forbidden** 을 준다 — 같은 URL 을 curl 로 받으면 200 이다.
#    URL 이 틀린 것도 파일이 없는 것도 아니라서, 안 겪어 보면 「아직 생성 중」으로 오해한다.
HEADERS = {"User-Agent": "Mozilla/5.0 (compatible; AbyssArtPipeline/1.0)"}


def fetch(account, object_id, count, cache_dir):
    os.makedirs(cache_dir, exist_ok=True)
    frames = []
    missing = []
    for i in range(count):
        dest = os.path.join(cache_dir, "f%03d.png" % i)
        if not os.path.exists(dest):
            url = BASE.format(account=account, object_id=object_id, i=i)
            try:
                req = urllib.request.Request(url, headers=HEADERS)
                with urllib.request.urlopen(req, timeout=30) as r, open(dest, "wb") as f:
                    f.write(r.read())
            except Exception as e:
                missing.append((i, type(e).__name__))
                continue
        frames.append((i, dest))
    return frames, missing


def build(frames, cols, out_path):
    if not frames:
        raise SystemExit("🔴 받은 프레임이 없다")
    first = Image.open(frames[0][1])
    cw, ch = first.size
    rows = (max(i for i, _ in frames) // cols) + 1
    sheet = Image.new("RGBA", (cols * cw, rows * ch), (0, 0, 0, 0))
    for i, path in frames:
        r, c = divmod(i, cols)
        sheet.paste(Image.open(path).convert("RGBA"), (c * cw, r * ch))
    sheet.save(out_path)
    return sheet, (cw, ch), (cols, rows)


def verify(sheet, cell_w, cell_h, cols, rows):
    """행 경계에서 그림이 이어지는지 본다 — 격자가 맞으면 이어진다."""
    import numpy as np
    a = np.array(sheet)[:, :, 3] > 8
    touch_top = touch_bot = 0
    for r in range(rows):
        for c in range(cols):
            cell = a[r*cell_h:(r+1)*cell_h, c*cell_w:(c+1)*cell_w]
            if cell.shape[0] < cell_h:
                continue
            if cell[0].any():
                touch_top += 1
            if cell[-1].any():
                touch_bot += 1

    joined = 0
    lines = []
    for r in range(1, rows):
        y = r * cell_h
        if y >= a.shape[0]:
            break
        both = int((a[y - 1] & a[y]).sum())
        lines.append((y, both))
        if both:
            joined += 1
    return joined, lines, touch_top, touch_bot


def main():
    ap = argparse.ArgumentParser(description="오브젝트 후보를 원본 격자로 되붙인다")
    ap.add_argument("object_id")
    ap.add_argument("--out", required=True, help="시트 PNG 경로")
    ap.add_argument("--count", type=int, default=64, help="후보 수 (기본 64)")
    ap.add_argument("--cols", type=int, default=8, help="격자 열 수 (기본 8)")
    ap.add_argument("--account", default=DEFAULT_ACCOUNT)
    ap.add_argument("--cache", default=None, help="프레임 캐시 폴더")
    args = ap.parse_args()

    cache = args.cache or os.path.join(os.path.dirname(args.out) or ".",
                                       "_frames_" + args.object_id[:8])
    frames, missing = fetch(args.account, args.object_id, args.count, cache)
    print("받은 프레임 %d / %d" % (len(frames), args.count))
    if missing:
        print("  ⚠️ 못 받은 것: %s" % missing[:8])
        if any(n == "HTTPError" for _, n in missing):
            print("     🔴 403 이면 아직 생성 중이 아니라 **헤더 문제**다 — HEADERS 확인")

    sheet, (cw, ch), (cols, rows) = build(frames, args.cols, args.out)
    print("격자 %dx%d · 셀 %dx%d · 시트 %dx%d" % (cols, rows, cw, ch, sheet.width, sheet.height))

    joined, lines, touch_top, touch_bot = verify(sheet, cw, ch, cols, rows)
    overflow = touch_top + touch_bot
    print("칸 경계에 닿는 그림  윗줄 %d · 아랫줄 %d" % (touch_top, touch_bot))

    if overflow == 0:
        print("✅ 넘친 그림이 없다 — 도구가 안 잘랐다. 이어 붙일 것도 없다.")
    else:
        for y, both in lines:
            print("  행 경계 y=%3d  맞닿은 px %3d" % (y, both))
        if joined == len(lines):
            print("✅ %d/%d 경계에서 이어진다 = 격자가 맞다 (잘린 그림이 복원됐다)"
                  % (joined, len(lines)))
        else:
            print("🔴 넘친 그림이 있는데 %d/%d 경계만 이어진다 — --cols 를 바꿔 볼 것"
                  % (joined, len(lines)))
    print("→ %s" % args.out)
    print("\n💡 Unity 스프라이트 에디터에서 Grid By Cell Size %dx%d 로 슬라이스한다." % (cw, ch))


if __name__ == "__main__":
    main()
