# -*- coding: utf-8 -*-
"""PixelLab 번들 시트에서 **Unity 가 읽을 폼 시트 9장**을 조립한다 — 레시피 한 파일이 선택을 전부 갖는다.

    python Tools/ArtPipeline/build_form_sheets.py Art_Source/characters/ancient_shield/sheet_recipe.json
    python Tools/ArtPipeline/build_form_sheets.py <레시피> --dry-run

🔴 **왜 이 도구가 생겼나** (2026-09-17)

  `knight_red` 9상태는 손으로 조립했다 — 번들에서 떼고, 프레임을 고르고, 발밑을 맞추고, 가로로 붙였다.
  폼 3벌이면 27장이고, **어느 버전의 어느 프레임을 썼는지**가 사람 기억에만 남는다.
  그래서 선택은 레시피(`sheet_recipe.json`)에 적고 이 도구는 그대로 실행만 한다.

🔑 **칸 크기가 캐릭터마다 다르다** (2026-09-17 실측)

    번들 칸     92x104 · 104x96 · 100x104       ← 캐릭터마다 다르다
    낱장 프레임  92x92 · 투척사만 100x100         ← v3 가 넓은 망토에 캔버스를 키웠다

  번들 칸은 **pivot-centred** 라 가운데에서 92x92 를 떼면 낱장 URL 과 픽셀 단위로 같다(교차검증함).
  🔴 캔버스가 100 인 폼도 **가운데 92 로 뗀다** — 격자·피벗(0.163)·손 앵커가 전부 92 규약이다.
  대신 **잘려 나가는 픽셀이 있으면 거부한다.** 조용히 자르면 망토 끝이 사라진다.

⚠️ 투척사 발밑이 「82 라 5px 낮다」고 적었던 것은 **100 칸에서 잰 값**이었다. 92 로 떼면 77 근처다.

📌 공정: 번들 받기(`--download`) → 칸 떼기 → (그림자 제거) → 발밑 정렬 → 가로 시트.
   발밑 정렬 방식은 상태마다 다르다:

    each   프레임마다 아래끝을 footY 로      지상·공중·대시 (knight_red 규약 — 착지 때 몸이 안 떨어진다)
    first  첫 프레임의 이동량을 전 프레임에   사망 — 쓰러지며 아래끝이 움직이는 것이 내용이다
    none   건드리지 않는다

📌 `freezeBelow {row, frame}` — 그 줄부터 아래를 기준 프레임으로 고정한다. 서 있는 상태에서 발이 움직이면 안 될 때
   (2026-09-17 투척사 Idle: 생성으로는 다리가 계속 움직였다).
📌 `overlay "<png>"` — 손으로 그린 92x92 조각을 모든 프레임에 얹는다(레시피 기준 상대경로).
📌 `removeStrays true` — 1px 어두운 잔여선(그림자 테두리 호)을 걷는다.
📌 `lowerRegion {x0,x1,top,bottom,by,fillFromRow,fillFromFrame}` — 한쪽 발(부츠 줄)만 내려 바닥에 붙인다.
   틈은 fillFromFrame(기본 0) 프레임의 정강이 줄로 잇는다 — 프레임마다 자기 줄을 쓰면 밑단 외곽선이 섞인다.
   순서: 떼기 → 그림자 → 발밑 → freezeBelow → overlay → removeStrays → lowerRegion.
"""

import argparse
import json
import os
import subprocess
import sys
import zipfile

import numpy as np
from PIL import Image

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from strip_ground_shadow import cleanFrame, DEFAULT_COLOR, DEFAULT_TOL, DEFAULT_MIN_ISLAND  # noqa: E402

ALPHA_CUT = 8
BUNDLE_URL = "https://api.pixellab.ai/mcp/characters/{id}/spritesheet"
MAX_SHIFT = 8


def downloadBundle(characterId, folder):
    """번들을 받아 푼다. 🔴 urllib 은 403 을 받는다 — curl 로 받는다(메모리 참조)."""
    os.makedirs(folder, exist_ok=True)
    zipPath = os.path.join(folder, "bundle.zip")
    result = subprocess.run(["curl", "-s", "--fail", "-A", "Mozilla/5.0", "-o", zipPath,
                             BUNDLE_URL.format(id=characterId)])
    if result.returncode != 0:
        raise SystemExit("번들을 못 받았다 — 생성 중이면 423 이다. 큐가 빈 뒤 다시.")
    # 🔴 이전 번들을 먼저 지운다 — 파일명에 캐릭터 ID 가 들어가 있어, 몸을 바꾸면 두 벌이 섞여
    #    loadBundle 이 「하나씩」 규칙으로 멈춘다(2026-09-17 투척사 몸 교체에서 겪음).
    for name in os.listdir(folder):
        if name.endswith((".json", ".png")):
            os.remove(os.path.join(folder, name))
    with zipfile.ZipFile(zipPath) as z:
        z.extractall(folder)


def loadBundle(folder):
    jsons = [f for f in os.listdir(folder) if f.endswith(".json")]
    pngs = [f for f in os.listdir(folder) if f.endswith(".png")]
    if len(jsons) != 1 or len(pngs) != 1:
        raise SystemExit(f"번들 폴더에 json·png 가 하나씩 있어야 한다: {folder}")
    meta = json.load(open(os.path.join(folder, jsons[0]), encoding="utf-8"))["spritesheet"]
    sheet = Image.open(os.path.join(folder, pngs[0])).convert("RGBA")
    return meta, sheet


def cutFrames(meta, sheet, animation, cell):
    rows = [r for r in meta["rows"] if r.get("animation") == animation]
    if not rows:
        names = [r.get("animation") for r in meta["rows"] if r.get("type") == "animation"]
        raise SystemExit(f"🔴 번들에 '{animation}' 이 없다. 있는 것: {names}")
    row = rows[0]
    cw, ch = meta["cell_size"]["width"], meta["cell_size"]["height"]
    ox, oy = (cw - cell) // 2, (ch - cell) // 2

    frames = []
    for i in range(row["frame_count"]):
        full = sheet.crop((i * cw, row["row"] * ch, (i + 1) * cw, (row["row"] + 1) * ch))
        crop = full.crop((ox, oy, ox + cell, oy + cell))
        lost = countAlpha(full) - countAlpha(crop)
        if lost > 0:
            raise SystemExit(f"🔴 {animation} f{i}: {cell} 칸으로 떼면 {lost}px 이 잘린다 — 칸을 키울지 판단할 것")
        frames.append(crop)
    return frames


def countAlpha(image):
    # getdata 는 Pillow 14 에서 빠진다 — numpy 로 센다.
    return int((np.asarray(image.getchannel("A")) > ALPHA_CUT).sum())


def footY(image):
    box = image.getchannel("A").getbbox()
    return None if box is None else box[3]


def shift(image, dy):
    moved = Image.new("RGBA", image.size, (0, 0, 0, 0))
    moved.paste(image, (0, dy))
    return moved


def freezeLowerBody(frames, row, baseIndex):
    """`row` 줄부터 아래를 기준 프레임으로 덮는다. 위(몸통)는 각 프레임 그대로."""
    base = frames[baseIndex]
    lower = base.crop((0, row, base.width, base.height))
    frozen = []
    for f in frames:
        g = f.copy()
        g.paste(Image.new("RGBA", lower.size, (0, 0, 0, 0)), (0, row))
        g.paste(lower, (0, row))
        frozen.append(g)
    return frozen


def removeStrayLines(image, fromRow=60, darkMax=12):
    """1px 짜리 어두운 잔여선을 걷는다 — 불투명 4방향 이웃이 1개 이하인 어두운 픽셀을 반복해서 지운다.

    📌 그림자 테두리가 호(弧)로 남는 경우용이다(투척사 Idle v1). 몸 외곽선은 안쪽 이웃이 있어 안 지워진다.
    """
    a = np.array(image)
    for _ in range(6):
        opaque = a[:, :, 3] > ALPHA_CUT
        dark = opaque & (a[:, :, :3].max(axis=2) < darkMax)
        kill = []
        h, w = opaque.shape
        for y, x in zip(*np.nonzero(dark)):
            if y < fromRow:
                continue
            neighbours = sum(1 for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1))
                             if 0 <= y + dy < h and 0 <= x + dx < w and opaque[y + dy, x + dx])
            if neighbours <= 1:
                kill.append((y, x))
        if not kill:
            break
        for y, x in kill:
            a[y, x, 3] = 0
    return Image.fromarray(a)


def lowerRegion(image, r, fillSource):
    """열 x0..x1 에서 top..bottom 줄(부츠)을 by 만큼 내리고, 비는 줄은 fillSource 의 fillFromRow(정강이) 줄로 채운다."""
    a = np.array(image)
    x0, x1, top, bottom, by, fill = r["x0"], r["x1"] + 1, r["top"], r["bottom"], r["by"], r["fillFromRow"]
    block = a[top:bottom + 1, x0:x1].copy()
    shin = np.array(fillSource)[fill, x0:x1].copy()
    a[top:bottom + 1 + by, x0:x1] = 0
    for y in range(top, top + by):
        a[y, x0:x1] = shin
    a[top + by:bottom + 1 + by, x0:x1] = block
    return Image.fromarray(a)


def pickFrames(frames, spec):
    if spec == "all":
        return frames
    picked = []
    for index in spec:
        if index >= len(frames):
            raise SystemExit(f"🔴 프레임 {index} 가 없다 (총 {len(frames)})")
        picked.append(frames[index])
    return picked


def buildState(meta, sheet, state, spec, recipe):
    cell = recipe["cell"]
    frames = pickFrames(cutFrames(meta, sheet, spec["animation"], cell), spec.get("frames", "all"))

    if recipe.get("stripShadow"):
        frames = [cleanFrame(f, tuple(DEFAULT_COLOR), DEFAULT_TOL, DEFAULT_MIN_ISLAND)[0] for f in frames]

    target = recipe["footY"]
    mode = spec.get("align", "each")
    feet = [footY(f) for f in frames]
    if mode == "each":
        shifts = [target - y for y in feet]
    elif mode == "first":
        shifts = [target - feet[0]] * len(frames)
    else:
        shifts = [0] * len(frames)

    if max(abs(s) for s in shifts) > MAX_SHIFT:
        raise SystemExit(f"🔴 {state}: 이동량 {shifts} 가 {MAX_SHIFT}px 를 넘는다 — 그림이 다른 문제일 수 있다")

    frames = [shift(f, s) for f, s in zip(frames, shifts)]

    # 🔑 하체 고정 — 기준 프레임의 아래쪽 줄을 모든 프레임에 그대로 쓴다(발밑 정렬 뒤라 줄이 맞는다).
    #    v3 는 시작·끝 프레임을 같게 줘도 중간에서 다리를 옮긴다(투척사 Idle v4 실측) — 생성으로는 보장이 안 된다.
    if "freezeBelow" in spec:
        frames = freezeLowerBody(frames, spec["freezeBelow"]["row"], spec["freezeBelow"]["frame"])

    # 🔑 손으로 그린 조각을 얹는다 — 생성이 끝내 못 그린 것(투척사 Idle 의 나란히 선 두 다리).
    #    경로는 레시피 기준 상대경로. 조각은 칸과 같은 크기(92x92)의 투명 PNG 다.
    if "overlay" in spec:
        patch = Image.open(os.path.join(recipe["_dir"], spec["overlay"])).convert("RGBA")
        frames = [Image.alpha_composite(f, patch) for f in frames]

    if spec.get("removeStrays"):
        frames = [removeStrayLines(f) for f in frames]

    # 🔑 한쪽 발만 떠 있을 때 — 그 발(부츠 줄)을 내리고 생긴 틈을 정강이 줄로 잇는다.
    #    전체 발밑 정렬(each)은 가장 아래 픽셀 하나만 보므로 **다른 발이 떠 있는 것**을 못 잡는다(투척사 Idle 앞발 3px).
    if "lowerRegion" in spec:
        region = spec["lowerRegion"]
        # 🔴 정강이 줄은 **한 프레임에서** 가져온다(fillFromFrame, 기본 0). 프레임마다 자기 줄을 쓰면
        #    그 줄을 망토 밑단 외곽선이 지나가는 프레임에서 검정이 세로로 늘어난다(투척사 Idle f4~f8 · 사용자 지적).
        source = frames[region.get("fillFromFrame", 0)]
        frames = [lowerRegion(f, region, source) for f in frames]

    heights = []
    for f in frames:
        box = f.getchannel("A").getbbox()
        heights.append(box[3] - box[1])
    return frames, feet, shifts, heights


def main():
    ap = argparse.ArgumentParser(description="번들에서 폼 시트 9장을 조립한다")
    ap.add_argument("recipe", help="sheet_recipe.json")
    ap.add_argument("--bundle", default=None, help="번들을 푼 폴더 (안 주면 레시피 옆 _bundle/)")
    ap.add_argument("--download", action="store_true", help="번들을 새로 받는다")
    ap.add_argument("--dry-run", action="store_true", help="시트를 쓰지 않는다")
    args = ap.parse_args()

    recipe = json.load(open(args.recipe, encoding="utf-8"))
    recipe["_dir"] = os.path.dirname(os.path.abspath(args.recipe))
    bundleDir = args.bundle or os.path.join(os.path.dirname(os.path.abspath(args.recipe)), "_bundle")
    if args.download or not os.path.isdir(bundleDir):
        downloadBundle(recipe["characterId"], bundleDir)
    meta, sheet = loadBundle(bundleDir)

    outDir = recipe["outDir"]
    heightsByState = {}
    for state, spec in recipe["states"].items():
        frames, feet, shifts, heights = buildState(meta, sheet, state, spec, recipe)
        heightsByState[state] = heights
        outPath = f"{outDir}/{recipe['prefix']}_{state}_{recipe['direction']}.png"
        print(f"  {state:<12} {spec['animation']:<26} {len(frames)}장 · 발밑 {feet} -> {recipe['footY']} "
              f"({spec.get('align', 'each')}) · 키 {min(heights)}~{max(heights)}")
        if args.dry_run:
            continue
        out = Image.new("RGBA", (recipe["cell"] * len(frames), recipe["cell"]), (0, 0, 0, 0))
        for i, f in enumerate(frames):
            out.paste(f, (i * recipe["cell"], 0))
        os.makedirs(outDir, exist_ok=True)
        out.save(outPath)

    # 🔑 공중 자세는 짝으로 판정한다 — 키가 다르면 정점에서 실루엣이 튄다(knight_red 2026-09-15).
    if "jump" in heightsByState and "fall" in heightsByState:
        jump, fall = heightsByState["jump"], heightsByState["fall"]
        gap = abs(sum(jump) / len(jump) - sum(fall) / len(fall))
        mark = "✅" if gap <= 3 else "🔴"
        print(f"{mark} Jump·Fall 평균 키 차이 {gap:.1f}px (knight_red 0~1px · 웅크림 실패작 15px)")

    print("--dry-run 이므로 쓰지 않았다." if args.dry_run else f"→ {outDir}")


if __name__ == "__main__":
    main()
