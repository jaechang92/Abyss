# -*- coding: utf-8 -*-
"""프레임에 **구워진 바닥 그림자**를 걷어 낸다.

    python Tools/ArtPipeline/strip_ground_shadow.py <프레임폴더>
    python Tools/ArtPipeline/strip_ground_shadow.py <프레임폴더> --dry-run
    python Tools/ArtPipeline/strip_ground_shadow.py <프레임폴더> --color 20,20,18 --tol 6

🔴 **왜 이 도구가 생겼나** (2026-09-17)

  투척사 몸(`0f43556f`)은 「밑단이 바닥에 닿는 종 모양 망토」로 뽑았다. 그 문구가
  **바닥 그림자를 같이 불렀다** — 밑단 아래에 단색 `#141412` 타원이 구워져 나왔다.

    - 그림자가 발밑(y=77) 아래 **y=84 까지** 내려가 발밑 피벗을 못 잡는다
    - **프레임마다 생겼다 없어진다**(Idle 9장 중 5장 · Walk 8장 중 6장) — 재생하면 깜박인다
    - 캐릭터 문구 규약 `transparent background, no ground, no shadow`(`20_SUBJECTS/forms/_ASSETS.md`)에 어긋난다

  몸을 다시 뽑는 대신 **gen 0 후처리**로 가기로 했다(사용자 결정).

🔑 **색만 보고 지우면 안 된다 — 망토 안쪽 그늘이 같은 색이다.**
   그래서 **바깥 투명 영역에서 출발해 그 색으로만 이어진 픽셀**을 지운다(4방향 채움).
   망토 윤곽선(`#020202`)이 안쪽 그늘을 둘러싸고 있어 안쪽까지는 못 들어간다.

🔑 **그 다음 본체에서 떨어진 작은 조각을 지운다.** 그림자 타원의 테두리 픽셀이
   점처럼 남는다(1~6px). 남기면 `align_feet.py` 가 bbox 아래끝으로 발밑을 재므로
   **점 하나가 발밑이 된다.** → 이 도구를 **정렬보다 먼저** 돌린다.

⚠️ **작은 조각 판정은 본체와의 크기 비로 한다.** 공격·대시 프레임에서 망토 자락이
   본체와 떨어져 보일 수 있으므로 `--min-island` 는 작게 둔다(기본 8px).
   지운 조각은 전부 좌표와 함께 출력한다 — **눈으로 확인할 것.**

📌 공정 순서: `strip_ground_shadow.py` → `align_feet.py` → 시트 조립.
"""

import argparse
import os
from collections import deque

import numpy as np
from PIL import Image

ALPHA_CUT = 8
DEFAULT_COLOR = (20, 20, 18)   # 투척사 v2 에서 잰 그림자 색
DEFAULT_TOL = 6                # 채널별 허용 오차
DEFAULT_MIN_ISLAND = 8         # 이보다 작은 떨어진 조각은 지운다(px)


def frameFiles(srcDir):
    """숫자 파일명 순서대로 (`align_feet.py` 와 같은 규약)."""
    names = [f for f in os.listdir(srcDir) if f.lower().endswith(".png")]
    numbered = []
    for name in names:
        stem = os.path.splitext(name)[0]
        if not stem.isdigit():
            raise SystemExit(f"프레임 파일명이 숫자가 아니다: {name} — 0.png, 1.png ... 로 둘 것")
        numbered.append((int(stem), name))
    numbered.sort()
    return [os.path.join(srcDir, name) for _, name in numbered]


def stripExteriorShadow(rgba, color, tol):
    """바깥 투명 영역과 그림자 색으로만 이어진 픽셀의 마스크."""
    h, w = rgba.shape[:2]
    opaque = rgba[:, :, 3] > ALPHA_CUT
    diff = np.abs(rgba[:, :, :3].astype(int) - np.array(color)).max(axis=2)
    shade = opaque & (diff <= tol)

    seen = ~opaque          # 투명 칸은 출발점이다
    queue = deque(zip(*np.nonzero(~opaque)))
    removed = np.zeros((h, w), bool)
    while queue:
        y, x = queue.popleft()
        for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            ny, nx = y + dy, x + dx
            if 0 <= ny < h and 0 <= nx < w and not seen[ny, nx] and shade[ny, nx]:
                seen[ny, nx] = True
                removed[ny, nx] = True
                queue.append((ny, nx))
    return removed


def islands(mask):
    """8방향 연결 덩어리 목록(큰 순)."""
    h, w = mask.shape
    label = np.zeros((h, w), bool)
    found = []
    for sy, sx in zip(*np.nonzero(mask)):
        if label[sy, sx]:
            continue
        queue = deque([(sy, sx)])
        label[sy, sx] = True
        points = []
        while queue:
            y, x = queue.popleft()
            points.append((y, x))
            for dy in (-1, 0, 1):
                for dx in (-1, 0, 1):
                    ny, nx = y + dy, x + dx
                    if 0 <= ny < h and 0 <= nx < w and mask[ny, nx] and not label[ny, nx]:
                        label[ny, nx] = True
                        queue.append((ny, nx))
        found.append(points)
    return sorted(found, key=len, reverse=True)


def cleanFrame(image, color, tol, minIsland):
    rgba = np.array(image.convert("RGBA"))
    shadow = stripExteriorShadow(rgba, color, tol)
    rgba[shadow, 3] = 0

    pieces = islands(rgba[:, :, 3] > ALPHA_CUT)
    dropped = []
    for piece in pieces[1:]:
        if len(piece) < minIsland:
            ys = [p[0] for p in piece]
            xs = [p[1] for p in piece]
            dropped.append((len(piece), min(xs), min(ys), max(xs), max(ys)))
            for p in piece:
                rgba[p][3] = 0
    return Image.fromarray(rgba), int(shadow.sum()), dropped


def main():
    ap = argparse.ArgumentParser(description="구워진 바닥 그림자와 떨어진 점을 걷어 낸다")
    ap.add_argument("srcDir", help="0.png, 1.png ... 이 든 폴더")
    ap.add_argument("--color", default=",".join(map(str, DEFAULT_COLOR)),
                    help='그림자 색 "r,g,b" (기본 20,20,18)')
    ap.add_argument("--tol", type=int, default=DEFAULT_TOL, help="채널별 허용 오차 (기본 6)")
    ap.add_argument("--min-island", type=int, default=DEFAULT_MIN_ISLAND,
                    help="이보다 작은 떨어진 조각은 지운다 (기본 8px)")
    ap.add_argument("--dry-run", action="store_true", help="쓰지 않고 계산만 보여 준다")
    args = ap.parse_args()

    color = tuple(int(v) for v in args.color.split(","))
    paths = frameFiles(args.srcDir)
    if not paths:
        raise SystemExit(f"PNG 가 없다: {args.srcDir}")

    print(f"그림자 색 {color} ±{args.tol} · 떨어진 조각 < {args.min_island}px 제거")
    results = []
    for path in paths:
        before = Image.open(path).convert("RGBA")
        after, shadowCount, dropped = cleanFrame(before, color, args.tol, args.min_island)
        boxBefore = before.getchannel("A").getbbox()
        boxAfter = after.getchannel("A").getbbox()
        print(f"  {os.path.basename(path):<8} 그림자 {shadowCount:>4}px · 조각 {len(dropped)}개 · "
              f"발밑 {boxBefore[3] if boxBefore else '-'} -> {boxAfter[3] if boxAfter else '-'}")
        for size, x0, y0, x1, y1 in dropped:
            print(f"             ↳ {size}px  ({x0},{y0})~({x1},{y1})")
        results.append((path, after, shadowCount + len(dropped)))

    if args.dry_run:
        print("--dry-run 이므로 쓰지 않았다.")
        return

    written = 0
    for path, after, changed in results:
        if changed:
            after.save(path)
            written += 1
    print(f"{written}장 썼다.")


if __name__ == "__main__":
    main()
