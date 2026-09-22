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

🔴 **후처리는 네 모듈에 있다** (2026-09-22 분할 · 이 파일이 1050줄이 되어 컨벤션 500줄을 두 배 넘겼다)

    sheet_common.py      공용 헬퍼 — ALPHA_CUT · rgbToHsv · hsvToRgb · outlineMask
    sheet_color.py       시트 전체의 색을 옮긴다 — recolor · desaturate · snapGray · collapse
    sheet_highlight.py   밝은 면(강조점) — bladeWear -> bridgeHighlight -> bladeSmooth (순서가 있다)
    sheet_cleanup.py     지우고 꿰맨다 — eraseRect · despeckle · dropLoose · removeStrays · outline(맨 뒤)

  🔴 **레시피 키는 한 글자도 안 바뀌었다.** 이 파일은 조립만 한다 — 번들 · 칸 떼기 · 발밑 정렬 · 시트 쓰기.

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
   순서: (recolor · desaturate) → 떼기 → 그림자 → 발밑 → freezeBelow → overlay → removeStrays → lowerRegion.
📌 `recolor {hueBelow,hueAbove,keepSatBelow,keepValAbove,keepValBelow,hue,satScale,valScale}` — **레시피 최상위**.
   번들 시트 전체에 한 번 적용해 모든 프레임이 같은 값을 받는다(2026-09-18 적 근접 병사 색 B 건메탈).
   생성 캐릭터의 재료색이 플레이어 축 색과 겹칠 때 생성을 다시 돌리지 않고 색만 옮긴다.
📌 `desaturate {satScale, hue, valScale}` — **레시피 최상위**. `recolor` 와 **고르는 방식이 반대다** —
   그쪽은 색상으로 골라 일부만 옮기고, 이쪽은 **알파가 있는 픽셀 전부**의 채도를 줄인다.
   🔴 무채색에 가까운 그림에는 `recolor` 를 못 쓴다: 그 식(`h < hueBelow | h > hueAbove`)은 hue 로 고르는데
   채도가 낮으면 hue 가 불안정해 픽셀을 제멋대로 집는다. 공허 술사는 *"낡은 옷. 색이 없었다"* 가 정체라
   **몸 전체의 채도**를 낮춰야 했다 — `20_SUBJECTS/enemies/void_caster.md` R1 색 판정(후보 C).
📌 적 시트는 `cell` 을 번들 칸(124) 그대로 쓴다 — 무기 앵커가 없어 92 규약을 따를 이유가 없고,
   기어 가는 몸이 92 밖으로 나간다(근접 병사 공격 f2 22px). 피벗은 `(cell - footY) / cell`.
"""

import argparse
import glob
import json
import os
import subprocess
import sys
import zipfile

import numpy as np
from PIL import Image

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from strip_ground_shadow import cleanFrame, DEFAULT_COLOR, DEFAULT_TOL, DEFAULT_MIN_ISLAND  # noqa: E402

# 🔴 후처리는 네 모듈로 나뉘어 있다(각 파일 맨 위 「왜 나뉘어 있나」) — 레시피 키는 그대로다.
from sheet_common import ALPHA_CUT  # noqa: E402
from sheet_color import collapseBand, desaturate, recolor, snapAchromatic  # noqa: E402
from sheet_highlight import bridgeHighlight, smoothAlongAxis, wearBladeEdge  # noqa: E402
from sheet_cleanup import (despeckle, dropLooseParts, eraseRect,  # noqa: E402
                           normalizeOutline, removeStrayLines)

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


def loadFrameDir(folder, cell):
    """**손으로 조립한 프레임**을 폴더에서 읽는다 — 생성기가 끝내 못 그리는 상태를 끼워 넣는 자리.

    🔴 감시자 거인 R4 사망에서 생겼다(2026-09-22). 「몸은 갈려 없어지고 **칼과 투구만 바닥에 남는다**」를
       **세 번 생성해 세 번 다 실패했고**(12 gen), 방식이 틀렸다고 판단해 `compose_shed_death.py` 로 조립했다.
       그 결과를 시트에 넣으려면 조립기가 **번들 말고 폴더**에서도 읽을 수 있어야 한다.

    🔑 **칸 떼기 규약은 `cutFrames` 와 같다** — 가운데에서 `cell` 을 떼고, **잘리면 거부한다.**
       조용히 자르면 날 끝이 사라진다.

    레시피 예: "dead": { "framesDir": "dead_composed", "align": "first", "skipPost": ["dropLoose"] }
    """
    paths = sorted(glob.glob(os.path.join(folder, "*.png")),
                   key=lambda q: int(os.path.splitext(os.path.basename(q))[0]))
    if not paths:
        raise SystemExit(f"🔴 {folder} 에 프레임 PNG 가 없다")
    frames = []
    for path in paths:
        full = Image.open(path).convert("RGBA")
        ox, oy = (full.width - cell) // 2, (full.height - cell) // 2
        crop = full.crop((ox, oy, ox + cell, oy + cell))
        lost = countAlpha(full) - countAlpha(crop)
        if lost > 0:
            raise SystemExit(f"🔴 {os.path.basename(path)}: {cell} 칸으로 떼면 {lost}px 이 잘린다")
        frames.append(crop)
    print(f"  손 조립 프레임 {len(frames)}장 — {folder}")
    return frames


def buildState(meta, sheet, state, spec, recipe):
    cell = recipe["cell"]
    # 🔑 `framesDir` 이 있으면 번들 대신 **폴더의 손 조립 프레임**을 쓴다.
    source = (loadFrameDir(os.path.join(recipe["_dir"], spec["framesDir"]), cell)
              if "framesDir" in spec else cutFrames(meta, sheet, spec["animation"], cell))
    frames = pickFrames(source, spec.get("frames", "all"))

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

    # 🔑 잡으려는 것은 **프레임마다 발밑이 다른 것**이지 전체가 한쪽으로 치우친 것이 아니다.
    #    캔버스에서 낮게 앉은 그림(적 원거리 사수 — 밑여백 4)은 전 프레임이 나란히 12~13px 이동한다.
    #    그래서 절대 이동량이 아니라 **산포**를 본다. 치우침은 레시피의 footY 가 흡수한다.
    spread = max(shifts) - min(shifts)
    limit = recipe.get("maxShift", MAX_SHIFT)
    if spread > limit:
        raise SystemExit(f"🔴 {state}: 이동량 {shifts} 의 산포 {spread}px 가 {limit}px 를 넘는다 — 그림이 다른 문제일 수 있다")

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

    # 🔴 **프레임 단위 정리는 순서가 중요하다** — 사용자가 직접 고친 것과 대조해 정한 차례다(2026-09-21).
    #    ① 지우고 → ② 튀는 점을 걷고 → ③ 하이라이트를 잇고 → ④ 축으로 고르고 → ⑤ **마지막에 외곽선**.
    #    🔑 외곽선이 맨 뒤인 이유: ①~④ 가 실루엣을 바꾸므로 **그 뒤에 외곽을 다시 꿰매야** 한다.
    #    (사용자가 지운 185px 자리에 **외곽선 20px 을 다시 그었다** — 내 1차 처리엔 그 단계가 없었다.)
    # 🔑 <b>상태별로 후처리를 끌 수 있다</b> — `"skipPost": ["dropLoose"]` 를 그 상태에 적는다.
    #    🔴 `dropLooseParts` 의 주석이 이미 경고한 자리다: **「조각이 흩어지는 것이 의도인 상태에는 걸지 말 것」**
    #       — 그런데 레시피가 최상위라 **4상태 전부에 걸렸다.** 감시자 거인 R4 사망이
    #       「몸이 옅어져 없어지고 **투구와 날만 바닥에 떨어진다**」라 그 조각들이 지워질 판이었다(2026-09-22).
    #    📌 뼈 궁수의 「재가 부스러지는」 사망도 같은 자리다 — 그때는 레시피에서 통째로 뺐다.
    skip = set(spec.get("skipPost", []))

    def apply(key, fn, frames):
        if key in skip:
            print(f"  {key} — 이 상태에서는 건너뛴다(skipPost)")
            return frames
        return [fn(f, recipe[key]) for f in frames]

    if "eraseRect" in recipe:
        frames = apply("eraseRect", eraseRect, frames)

    if "despeckle" in recipe:
        frames = apply("despeckle", despeckle, frames)

    # 🔴 알파 연결은 **밝은 픽셀 연결과 다른 검사**다 — despeckle 이 못 잡는 「떠 있는 조각」을 본다.
    if "dropLoose" in recipe:
        frames = apply("dropLoose", dropLooseParts, frames)

    # 🔴 <b>씨앗을 먼저 심는다</b> — `bridgeHighlight` 는 있는 줄을 잇고, `bladeWear` 는 없는 줄을 낸다.
    if "bladeWear" in recipe:
        frames = apply("bladeWear", wearBladeEdge, frames)

    if "bridgeHighlight" in recipe:
        frames = apply("bridgeHighlight", bridgeHighlight, frames)

    # 🔴 축은 **프레임마다** 다시 구한다 — 회전베기는 프레임마다 날 각도가 바뀐다.
    if "bladeSmooth" in recipe:
        frames = apply("bladeSmooth", smoothAlongAxis, frames)

    if "outline" in recipe:
        frames = apply("outline", normalizeOutline, frames)

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
    if "recolor" in recipe:
        sheet = recolor(sheet, recipe["recolor"])
        print(f"  색 변환 — hue {recipe['recolor']['hue']}° · 채도 ×{recipe['recolor']['satScale']} · 명도 ×{recipe['recolor']['valScale']}")
    if "desaturate" in recipe:
        d = recipe["desaturate"]
        sheet = desaturate(sheet, d)
        print(f"  채도 변환 — 채도 ×{d['satScale']} · hue {d.get('hue', 0.0)}° · 명도 ×{d.get('valScale', 1.0)}")
    # 🔴 색 변환 **뒤**에 건다 — recolor/desaturate 가 채도를 건드리므로 그 결과에서 무채를 판정해야 한다.
    if "snapGray" in recipe:
        sheet = snapAchromatic(sheet, recipe["snapGray"])
    # 🔴 하이라이트 단 통일도 시트 단위다 — 프레임마다 단을 새로 고르면 깜빡인다.
    if "collapse" in recipe:
        sheet = collapseBand(sheet, recipe["collapse"])

    outDir = recipe["outDir"]
    heightsByState = {}
    for state, spec in recipe["states"].items():
        frames, feet, shifts, heights = buildState(meta, sheet, state, spec, recipe)
        heightsByState[state] = heights
        outPath = f"{outDir}/{recipe['prefix']}_{state}_{recipe['direction']}.png"
        source = spec.get("animation") or f"dir:{spec['framesDir']}"
        print(f"  {state:<12} {source:<26} {len(frames)}장 · 발밑 {feet} -> {recipe['footY']} "
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
