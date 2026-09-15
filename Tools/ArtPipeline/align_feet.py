# -*- coding: utf-8 -*-
"""프레임마다 흔들리는 **발밑을 한 줄에 못박는다**.

    python Tools/ArtPipeline/align_feet.py <프레임폴더> --target 77
    python Tools/ArtPipeline/align_feet.py <프레임폴더> --target 77 --dry-run

🔴 **왜 이 도구가 생겼나** (2026-09-15)

  `south-east` 로 방향을 바꾸면서 다시 뽑은 Walk 의 발밑이 프레임마다 75~78 로 흔들렸다.
  기존 `east` 는 지상 상태가 전부 **0~1px** 이었다. 3px 이면 화면 배율 3배에서 **9픽셀**이
  출렁이고, 걸을 때 캐릭터가 **바닥에서 떴다 가라앉았다** 한다.

  같은 종류의 어긋남을 2026-09-13 에 이미 만났다 — `difftest_walk` 만 발중심이 9px
  **왼쪽**이었다. 그때는 손으로 고치고 **도구를 안 남겼다.** 그래서 또 만났다.

🔑 **사람이 걸으면 지지발은 지면에 고정되고 몸통이 오르내린다.**
   그러니 알파 bbox 의 **아래끝이 상수**인 것이 옳고, 이 도구는 그걸 정수 평행이동으로 맞춘다.
   프레임 안의 상하 운동(머리 까딱임)은 **그대로 남는다** — 통째로 옮기기 때문이다.

⚠️ **아무 상태에나 쓰면 안 된다.**

  | 쓴다 | 안 쓴다 |
  |---|---|
  | 지상 동작 (Idle · Walk · Dash · Hit · Attack) | **공중** (Jump · Fall) — 떠 있는 것이 내용이다 |
  | | **쓰러짐** (Dead) — 발밑이 내려가는 것이 내용이다 |

  판단은 사람이 한다. 도구는 시키는 것만 하고, **이동량이 크면 거부한다** —
  큰 이동은 정렬 문제가 아니라 그림이 다른 문제일 가능성이 높다.

📌 가로(x)는 **안 건드린다.** 걸음에 따라 좌우로 움직이는 것이 정상이라 일괄 보정하면
   걸음이 지워진다. 시트 전체가 한쪽으로 치우친 경우(2026-09-13)는 별개의 판단이 필요하다.
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


def footY(image):
    """알파 bbox 의 아래끝. 완전히 투명하면 None."""
    box = image.getchannel("A").getbbox()
    return None if box is None else box[3]


def shiftFrame(image, dy):
    """캔버스를 유지한 채 통째로 세로 이동. 잘려 나가면 알려 준다."""
    moved = Image.new("RGBA", image.size, (0, 0, 0, 0))
    moved.paste(image, (0, dy))
    return moved


def main():
    ap = argparse.ArgumentParser(description="프레임의 발밑을 한 줄에 맞춘다")
    ap.add_argument("srcDir", help="0.png, 1.png ... 이 든 폴더")
    ap.add_argument("--target", type=int, default=None,
                    help="맞출 발밑 y. 생략하면 첫 프레임의 발밑을 쓴다")
    ap.add_argument("--max-shift", type=int, default=6,
                    help="허용 이동량(px). 넘으면 거부한다 (기본 6)")
    ap.add_argument("--dry-run", action="store_true", help="쓰지 않고 계산만 보여 준다")
    args = ap.parse_args()

    paths = frameFiles(args.srcDir)
    if not paths:
        raise SystemExit(f"PNG 가 없다: {args.srcDir}")

    images = [Image.open(p).convert("RGBA") for p in paths]
    feet = [footY(im) for im in images]
    if any(f is None for f in feet):
        raise SystemExit("완전히 투명한 프레임이 있다 — 발밑을 못 잡는다")

    target = args.target if args.target is not None else feet[0]
    shifts = [target - f for f in feet]
    biggest = max(abs(s) for s in shifts)

    print(f"목표 발밑 y = {target}")
    for path, before, dy in zip(paths, feet, shifts):
        mark = "" if dy == 0 else f"  ->  {dy:+d}px"
        print(f"  {os.path.basename(path):<8} 발밑 {before}{mark}")
    print(f"최대 이동량 {biggest}px · 변동 {max(feet) - min(feet)}px -> 0px")

    if biggest > args.max_shift:
        raise SystemExit(
            f"🔴 이동량 {biggest}px 가 한도 {args.max_shift}px 를 넘는다.\n"
            "   정렬 문제가 아니라 그림이 다른 문제일 수 있다 — 눈으로 먼저 볼 것.\n"
            "   의도한 것이면 --max-shift 로 한도를 올린다.")

    if biggest == 0:
        print("이미 맞아 있다. 쓰지 않는다.")
        return
    if args.dry_run:
        print("--dry-run 이므로 쓰지 않았다.")
        return

    written = 0
    for path, image, dy in zip(paths, images, shifts):
        if dy == 0:
            continue
        moved = shiftFrame(image, dy)
        if footY(moved) != target:
            raise SystemExit(f"🔴 {path} 를 옮겼는데 발밑이 {footY(moved)} 다 — 캔버스 밖으로 잘렸다")
        moved.save(path)
        written += 1
    print(f"{written}장 갱신했다.")


main()
