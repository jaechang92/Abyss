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
from collections import Counter
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


def rgbToHsv(rgb):
    """0~1 RGB 배열 → 0~1 HSV 배열 (colorsys 와 같은 정의 · 픽셀 단위 벡터화)."""
    r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    maxc = rgb.max(axis=-1)
    minc = rgb.min(axis=-1)
    delta = maxc - minc
    safe = np.where(delta == 0, 1, delta)
    rc, gc, bc = (maxc - r) / safe, (maxc - g) / safe, (maxc - b) / safe
    h = np.where(maxc == r, bc - gc, np.where(maxc == g, 2.0 + rc - bc, 4.0 + gc - rc))
    h = np.where(delta == 0, 0.0, (h / 6.0) % 1.0)
    s = np.where(maxc == 0, 0.0, delta / np.where(maxc == 0, 1, maxc))
    return np.stack([h, s, maxc], axis=-1)


def hsvToRgb(hsv):
    h, s, v = hsv[..., 0], hsv[..., 1], hsv[..., 2]
    i = np.floor(h * 6.0).astype(int) % 6
    f = h * 6.0 - np.floor(h * 6.0)
    p, q, t = v * (1 - s), v * (1 - s * f), v * (1 - s * (1 - f))
    choices = [(v, t, p), (q, v, p), (p, v, t), (p, q, v), (t, p, v), (v, p, q)]
    out = np.zeros(hsv.shape)
    for k, (rr, gg, bb) in enumerate(choices):
        mask = i == k
        out[..., 0] = np.where(mask, rr, out[..., 0])
        out[..., 1] = np.where(mask, gg, out[..., 1])
        out[..., 2] = np.where(mask, bb, out[..., 2])
    return out


def recolor(image, r):
    """붉음~주황 픽셀만 한 색상으로 옮긴다. 강조점(흐린 밝은색)과 외곽선(아주 어두움)은 보존한다.

    🔑 **식은 `Art_Source/20_SUBJECTS/enemies/melee_grunt.md` 「R3 채택」 절에 있던 손으로 적은 값이다.**
    도구로 옮긴 이유: 프레임마다 같은 값이 들어가야 하고, 애니메이션을 다시 뽑아도 같은 색이 나와야 한다.
    """
    a = np.array(image).astype(np.float64)
    hsv = rgbToHsv(a[..., :3] / 255.0)
    h, s, v = hsv[..., 0], hsv[..., 1], hsv[..., 2]

    isTarget = (h < r["hueBelow"]) | (h > r["hueAbove"])
    isKept = ((s < r["keepSatBelow"]) & (v > r["keepValAbove"])) | (v < r["keepValBelow"])
    change = isTarget & ~isKept & (a[..., 3] > ALPHA_CUT)

    moved = np.stack([np.full_like(h, r["hue"] / 360.0),
                      np.clip(s * r["satScale"], 0, 1),
                      np.clip(v * r["valScale"], 0, 1)], axis=-1)
    rgb = np.round(hsvToRgb(moved) * 255.0)
    a[..., :3] = np.where(change[..., None], rgb, a[..., :3])
    return Image.fromarray(a.astype(np.uint8), "RGBA")


def snapAchromatic(image, r):
    """밝은 **무채** 픽셀을 팔레트에서 명도가 가장 가까운 **유채** 색으로 옮긴다.

    🔴 감시자 거인 R2 에서 사용자가 지적한 결함이다. 몸이 검붉은데 날 위에 **무채 회색 42px**
       (`#AFAFB1` 23 · `#747676` 19)이 섞여 손잡이와 날 끝이 푸르게 떴다.

    🔑 **생성기가 같은 명도 단을 만들면서 채도만 잃은 것**이라 짝이 정확히 있다:
       `#AFAFB1`(v 0.69 · sat 0.01) ↔ `#B18F7F`(v 0.69 · sat 0.28) ·
       `#747676`(v 0.46 · sat 0.02) ↔ `#765343`(v 0.46 · sat 0.43).
       그래서 **명도로 짝을 찾으면 색이 안 바뀌고 채도만 돌아온다.**

    🔴 **시트 전체(번들)에 한 번 건다** — `recolor`·`desaturate` 와 같은 자리다.
       프레임마다 팔레트를 새로 뽑으면 같은 회색이 프레임마다 다른 색으로 가서 깜빡인다.

    레시피 예: "snapGray": {"valueMin": 0.25, "satMax": 0.10}
    """
    a = np.array(image)
    alpha = a[..., 3] > ALPHA_CUT
    if not alpha.any():
        return image

    hsv = rgbToHsv(a[..., :3].astype(np.float64) / 255.0)
    s, v = hsv[..., 1], hsv[..., 2]
    valueMin = r.get("valueMin", 0.25)
    satMax = r.get("satMax", 0.10)
    satMin = r.get("targetSatMin", 0.15)

    gray = alpha & (v >= valueMin) & (s < satMax)
    if not gray.any():
        return image

    # 짝 후보 = 같은 시트의 유채색 팔레트
    colourful = alpha & (s >= satMin)
    if not colourful.any():
        raise SystemExit("🔴 snapGray: 짝지을 유채색이 시트에 없다 — valueMin/satMax 를 다시 볼 것")
    palette = {}
    for pixel, value in zip(a[colourful][:, :3], v[colourful]):
        palette.setdefault(tuple(int(q) for q in pixel), float(value))

    moved = 0
    for y, x in zip(*np.where(gray)):
        target = min(palette.items(), key=lambda kv: abs(kv[1] - v[y, x]))[0]
        a[y, x, :3] = target
        moved += 1
    print(f"  무채 스냅 — {moved}px (명도 {valueMin} 이상 · 채도 {satMax} 미만)")
    return Image.fromarray(a, "RGBA")


def desaturate(image, r):
    """알파가 있는 픽셀 **전부**의 채도를 줄이고 색상을 한 값으로 모은다.

    🔴 **`recolor` 와 반대로 고른다.** 그쪽은 `h < hueBelow | h > hueAbove` 로 붉음~주황만 집는데,
    무채색에 가까운 그림은 hue 가 불안정해 그 식이 못 쓴다. 여기서 바꿀 것은 「어느 색을 옮기나」가
    아니라 **「색이 있는가」** 자체다.

    🔑 공허 술사 R1 은 명도 분리(머리 80.4% ↔ 옷 8.8%)가 이미 돼 있고 **색상만 따뜻했다.**
    그래서 채도를 죽이는 것만으로 원문의 「색이 없는 낡은 옷 + 종이색 빈 얼굴」이 된다 — 재생성 25 gen 을 아낀다.
    (`20_SUBJECTS/enemies/void_caster.md` 「색은 후처리로 해결된다」)

    채도가 0 에 가까운 픽셀(외곽선 · 검정)은 hue 를 줘도 그대로 어둡게 남는다.
    """
    a = np.array(image).astype(np.float64)
    hsv = rgbToHsv(a[..., :3] / 255.0)
    h, s, v = hsv[..., 0], hsv[..., 1], hsv[..., 2]

    change = a[..., 3] > ALPHA_CUT
    moved = np.stack([np.full_like(h, r.get("hue", 0.0) / 360.0),
                      np.clip(s * r["satScale"], 0, 1),
                      np.clip(v * r.get("valScale", 1.0), 0, 1)], axis=-1)
    rgb = np.round(hsvToRgb(moved) * 255.0)
    a[..., :3] = np.where(change[..., None], rgb, a[..., :3])
    return Image.fromarray(a.astype(np.uint8), "RGBA")


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


def outlineMask(alpha):
    """불투명 픽셀 중 4-이웃에 투명(또는 캔버스 밖)이 있는 것 = 외곽선 한 겹."""
    edge = np.zeros_like(alpha)
    for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)):
        shifted = np.roll(alpha, (dy, dx), (0, 1))
        if dy == 1:
            shifted[0, :] = False
        if dy == -1:
            shifted[-1, :] = False
        if dx == 1:
            shifted[:, 0] = False
        if dx == -1:
            shifted[:, -1] = False
        edge |= alpha & ~shifted
    return edge


def normalizeOutline(image, spec):
    """외곽선을 한 색으로 통일한다 — 생성물의 외곽에 몸 색이 섞여 나오는 것을 걷는다.

    🔴 감시자 거인 R2 에서 사용자가 지적한 결함이다. 외곽 461px 중 검정 계열이 79.7% 이고
       나머지 20.3%(94px)가 갈색·회색이라 **실루엣이 흐려졌다.**
       튄 픽셀은 **투구 꼭대기(세로 1%)와 밑동(세로 90~100%)에 몰린다.**

    🔑 **밝은 강조점이 외곽에 노출된 것은 남긴다.** 이 적은 날 가장자리의 회백 한 줄이
       종당 하나의 강조점이라 같이 지우면 정체가 사라진다 — `keepBright` 의 세로 구간
       안에 있고 `brightMin` 이상으로 밝은 외곽 픽셀만 통과시킨다.

    레시피 예: "outline": {"ink": "#060405", "keepBright": [0.06, 0.88], "brightMin": 116}
    """
    a = np.array(image)
    alpha = a[:, :, 3] > ALPHA_CUT
    if not alpha.any():
        return image

    edge = outlineMask(alpha)
    ink = spec.get("ink", "#060405").lstrip("#")
    ink = (int(ink[0:2], 16), int(ink[2:4], 16), int(ink[4:6], 16))
    lo, hi = spec.get("keepBright", [0.0, 0.0])
    brightMin = spec.get("brightMin", 116)

    ys = np.where(alpha.any(axis=1))[0]
    top, bottom = int(ys.min()), int(ys.max())
    span = max(bottom - top, 1)

    for y, x in zip(*np.where(edge)):
        pixel = tuple(int(v) for v in a[y, x, :3])
        if pixel == ink:
            continue
        rel = (y - top) / span
        if max(pixel) >= brightMin and lo <= rel <= hi:
            continue            # 강조점이 외곽에 드러난 자리 — 남긴다
        a[y, x, :3] = ink
    return Image.fromarray(a, "RGBA")


def collapseBand(image, r):
    """명도 대역 하나를 통째로 위 또는 **아래**로 흡수한다 — 얼룩처럼 흩어진 중간 단을 없앤다.

    🔴 **처음에 「밝은 단을 위로 모으는」 것만 만들었다가 그림을 더 나쁘게 했다**(감시자 거인 R2).
       임계 0.40 이 하이라이트가 아니라 **날의 중간 면(C단 0.46)**까지 삼켰고, 그것을 최고 밝기로
       올리자 **얼룩이 훨씬 두드러졌다** — 사용자가 「가로줄이 매우 어색하다」고 지적한 자리다:

           y82  원본 68 45 45 45 45 45 45 45   ->  잘못된 결과 83 83 83 83 83 83 83 83

    🔑 **대역마다 방향이 다르다.** 하이라이트(B)는 위로 모아 줄을 잇고,
       얼룩진 중간 단(C)은 **아래로** 내려 몸통에 흡수시킨다.

    레시피 예: "collapse": [{"from": [0.60, 0.78], "to": "#D8C3BF"},
                            {"from": [0.40, 0.60], "to": "down"}]
      to = "#RRGGBB" 그 색으로 · "down" 대역 아래에서 가장 흔한 색으로 · "up" 대역 위에서 가장 흔한 색으로
    """
    a = np.array(image)
    alpha = a[..., 3] > ALPHA_CUT
    if not alpha.any():
        return image

    for band in r:
        hsv = rgbToHsv(a[..., :3].astype(np.float64) / 255.0)
        value = hsv[..., 2]
        lo, hi = band["from"]
        target = alpha & (value >= lo) & (value < hi)
        if not target.any():
            continue
        spec = band["to"]
        if spec in ("down", "up"):
            side = alpha & (value < lo) if spec == "down" else alpha & (value >= hi)
            if not side.any():
                continue
            ink = Counter(tuple(int(q) for q in px) for px in a[side][:, :3]).most_common(1)[0][0]
        else:
            t = spec.lstrip("#")
            ink = (int(t[0:2], 16), int(t[2:4], 16), int(t[4:6], 16))
        moved = int(target.sum())
        a[..., :3] = np.where(target[..., None], np.array(ink, dtype=a.dtype), a[..., :3])
        print(f"  대역 흡수 — {lo}~{hi} {moved}px -> #{ink[0]:02X}{ink[1]:02X}{ink[2]:02X} ({spec})")
    return Image.fromarray(a, "RGBA")


def eraseRect(image, r):
    """bbox 기준 **사각 영역 안의 픽셀을 지운다** — 생성물이 발밑에 깔아 놓은 바닥 그림자를 걷는다.

    🔴 감시자 거인 R2 에서 사용자가 직접 지운 **149px**(`x17~49 · y92~99`)이 그것이다.
       몸통 축보다 넓게 퍼진 납작한 판이라 **고립도 작은 덩어리도 아니라** 자동 처리가 못 잡았다.

    🔴 **두 번 틀렸다.**
      ① 「행 폭이 국소 최소를 지나 다시 넓어지는 지점 아래」라는 **자동 규칙**은
         기존 7종에 걸어 보니 **엘리트 사냥꾼의 벌어진 다리를 오탐**했다(배율 1.86 대 1.70 — 못 가른다).
      ② 그래서 **행 단위**(`eraseBelow row`)로 바꿨더니 **261px** 를 지웠다 — 사용자의 149px 보다 훨씬 많고,
         같은 행에 걸친 **오른쪽 날 끝까지 날아갔다.**
      👉 **가로 범위까지 받아야 한다.** 그림자는 몸통 아래에만 있고 날은 옆으로 뻗는다.

    레시피 예: "eraseRect": [{"x": [17, 49], "y": [92, 99]}]
    """
    a = np.array(image)
    alpha = a[..., 3] > ALPHA_CUT
    if not alpha.any():
        return image
    ys = np.where(alpha.any(axis=1))[0]
    xs = np.where(alpha.any(axis=0))[0]
    top, left = int(ys.min()), int(xs.min())
    wiped = 0
    for box in r:
        x0, x1 = box["x"]
        y0, y1 = box["y"]
        region = a[top + y0:top + y1 + 1, left + x0:left + x1 + 1]
        wiped += int((region[..., 3] > ALPHA_CUT).sum())
        region[..., 3] = 0
    print(f"  영역 지우기 — {len(r)}개 사각 · {wiped}px")
    return Image.fromarray(a, "RGBA")


def dropLooseParts(image, r):
    """몸에서 **떨어져 나온 조각**을 지운다 — 알파 연결 덩어리 중 가장 큰 것만 남긴다.

    🔴 감시자 거인 이동 v1 f4~f6 에 **몸과 안 닿은 작은 흰 조각**이 떠 있었다.
       `despeckle` 은 **밝은 픽셀의** 덩어리만 보므로 이런 것을 못 잡는다 —
       **알파(실루엣) 자체의 연결**을 봐야 한다.

    🔑 화염 박격포 R1 이 같은 결함으로 기각됐고(공중에 뜬 기와 한 장) 그때 얻은 규칙이
       **「연결 덩어리 수(flood fill)는 싼 검사다」**였다. 그 검사를 조립 단계에 넣은 것이다.

    ⚠️ **조각이 흩어지는 것이 의도인 상태에는 걸지 말 것**(재가 부스러지는 뼈 궁수 등).
       레시피에 넣는 순간 4상태 전부에 걸린다.

    🔴 **절대 픽셀 수로는 못 가른다** — 감시자 거인 이동 f4~f6 의 조각이 **45~85px** 이라
       `minPart 12` 에 안 걸렸다. 몸이 3000px 이므로 그 조각은 **2~3%** 다.
       👉 **가장 큰 덩어리 대비 비율**로 본다.

    레시피 예: "dropLoose": {"maxRatio": 0.05}    # 본체의 이 비율 미만인 별도 덩어리를 지운다
    """
    a = np.array(image)
    alpha = a[..., 3] > ALPHA_CUT
    if not alpha.any():
        return image
    height, width = alpha.shape
    maxRatio = r.get("maxRatio", 0.05)
    minPart = r.get("minPart", 0)
    neighbours = [(-1, -1), (-1, 0), (-1, 1), (0, -1), (0, 1), (1, -1), (1, 0), (1, 1)]

    seen = np.zeros_like(alpha)
    parts = []
    for sy, sx in zip(*np.where(alpha)):
        if seen[sy, sx]:
            continue
        stack, group = [(sy, sx)], []
        seen[sy, sx] = True
        while stack:
            y, x = stack.pop()
            group.append((y, x))
            for dy, dx in neighbours:
                ny, nx = y + dy, x + dx
                if 0 <= ny < height and 0 <= nx < width and alpha[ny, nx] and not seen[ny, nx]:
                    seen[ny, nx] = True
                    stack.append((ny, nx))
        parts.append(group)
    if len(parts) <= 1:
        return image
    parts.sort(key=len, reverse=True)
    body = len(parts[0])
    dropped = 0
    for group in parts[1:]:
        if len(group) >= body * maxRatio and len(group) >= minPart:
            continue                      # 본체에 견줄 만큼 크면 의도된 것일 수 있어 남긴다
        for y, x in group:
            a[y, x, 3] = 0
            dropped += 1
    print(f"  떨어진 조각 지우기 — 덩어리 {len(parts)}개 · {dropped}px")
    return Image.fromarray(a, "RGBA")


def bridgeHighlight(image, r):
    """끊긴 하이라이트 줄을 **이어 붙인다** — 성분 수가 목표보다 많으면 가장 가까운 쌍을 잇는다.

    🔴 사용자가 직접 고친 것과 대조해서 나온 기능이다(감시자 거인 R2 · 2026-09-21).
       사용자가 바꾼 110px 중 **94px 가 「최고 밝기로 올린 것」**이었고, 그중에는
       **어두운 몸통색 25px 과 외곽선 6px** 까지 있었다 — **줄을 잇기 위해서였다.**

    🔑 **나는 반대로 했다.** 중간톤을 몸통색으로 내려(`collapse down`) 줄을 **얇게** 만들었고,
       그 결과 하이라이트 성분이 **3개로 끊긴 채**였다(사용자 2개 = 날마다 하나).
       👉 **하이라이트는 내려서 없애는 것이 아니라 올려서 잇는다.**

    📌 **판정 지표도 여기서 나온다** — 「날 영역 색 전환율」은 **뭉개도 낮아져서** 내 쪽이 더 낮았는데
       그림은 내 쪽이 더 나빴다. **성분 수(= 날 개수)**가 옳은 지표다.

    레시피 예: "bridgeHighlight": {"value": 0.60, "expect": 2, "minSeed": 8, "maxGap": 10}
    """
    a = np.array(image)
    alpha = a[..., 3] > ALPHA_CUT
    if not alpha.any():
        return image
    height, width = alpha.shape
    value = rgbToHsv(a[..., :3].astype(np.float64) / 255.0)[..., 2]
    threshold = r.get("value", 0.60)
    expect = r.get("expect", 2)
    minSeed = r.get("minSeed", 8)
    maxGap = r.get("maxGap", 10)
    neighbours = [(-1, -1), (-1, 0), (-1, 1), (0, -1), (0, 1), (1, -1), (1, 0), (1, 1)]

    def components():
        mask = alpha & (value >= threshold)
        seen = np.zeros_like(mask)
        found = []
        for sy, sx in zip(*np.where(mask)):
            if seen[sy, sx]:
                continue
            stack, group = [(sy, sx)], []
            seen[sy, sx] = True
            while stack:
                y, x = stack.pop()
                group.append((y, x))
                for dy, dx in neighbours:
                    ny, nx = y + dy, x + dx
                    if 0 <= ny < height and 0 <= nx < width and mask[ny, nx] and not seen[ny, nx]:
                        seen[ny, nx] = True
                        stack.append((ny, nx))
            if len(group) >= minSeed:
                found.append(group)
        return sorted(found, key=len, reverse=True)

    groups = components()
    ink = Counter(tuple(int(q) for q in a[y, x, :3])
                  for g in groups for y, x in g).most_common(1)[0][0] if groups else None
    joined = 0
    # 🔴 <b>루프 가드</b> — 잇고 나서도 성분 수가 안 줄면 무한히 돈다.
    #    감시자 거인에서 `freezeBelow` 가 하단을 덮어 하이라이트가 끊기자 실제로 20분 넘게 멈췄다.
    #    성분 수가 줄지 않으면 즉시 포기한다(잇는 것이 목적이지 반드시 이뤄야 하는 것은 아니다).
    rounds = 0
    while ink is not None and len(groups) > expect and rounds < 16:
        rounds += 1
        before = len(groups)
        best = None
        for i in range(len(groups)):
            for j in range(i + 1, len(groups)):
                for p in groups[i]:
                    for q in groups[j]:
                        d = abs(p[0] - q[0]) + abs(p[1] - q[1])
                        if best is None or d < best[0]:
                            best = (d, p, q)
        if best is None or best[0] > maxGap:
            break
        _, (ay, ax), (by, bx) = best
        steps = max(abs(by - ay), abs(bx - ax))
        for t in range(steps + 1):                      # 두 성분 사이를 직선으로 채운다
            y = int(round(ay + (by - ay) * t / max(steps, 1)))
            x = int(round(ax + (bx - ax) * t / max(steps, 1)))
            if alpha[y, x] and tuple(int(q) for q in a[y, x, :3]) != ink:
                a[y, x, :3] = ink
                joined += 1
        value = rgbToHsv(a[..., :3].astype(np.float64) / 255.0)[..., 2]
        groups = components()
        if len(groups) >= before:
            break                      # 이었는데도 안 줄었다 — 더 돌아도 소용없다
    print(f"  하이라이트 잇기 — 성분 {len(groups)}개(목표 {expect}) · 채운 {joined}px")
    return Image.fromarray(a, "RGBA")


def wearBladeEdge(image, r):
    """마모 자국(회백 한 줄)이 **한쪽 날에만** 난 것을 반대쪽 날에도 낸다.

    🔴 감시자 거인 R4 에서 나온 기능이다(2026-09-22). 몸 생성물의 **오른쪽 날에 밝은 픽셀이 0개**(최대 L 30 ·
       왼쪽은 78)라 32프레임 중 27개에서 하이라이트 성분이 **1개**였다(목표 2 = 날 개수).
       🔑 `bridgeHighlight` 는 **씨앗이 있어야 잇는** 도구라 무에서는 못 만든다 — 그래서 이것이 따로 있다.

    🔑 **왜 후처리가 정당한가** — `10_BIBLE/03-light.md` **L1(광원을 그리지 않는다) · L2(방향 그림자를
       그리지 않는다)** 가 이 세계에 **빛의 방향이 없다**고 정한다. 「한쪽 날만 빛을 받는다」는 성립하지 않는다.
       게다가 이 줄은 반사광이 아니라 **마모 자국**이다(`midboss_sentinel.md` ③ *"백 년 땅을 쓸어 거기만 갈렸다"*)
       — **마모는 양쪽 날에 똑같이 난다.** 한쪽만 있는 것은 결함이지 빛이 아니다.

    공정: ① **열림 연산**(침식 후 팽창)으로 두꺼운 부위 = **몸통**을 떼고, 나머지를 얇은 부위 = **날**로 본다
          ② 날 성분마다 이미 밝은 픽셀이 `minSeed` 이상이면 **건너뛴다** — 있는 줄은 안 건드린다
          ③ 없으면 그 날의 **주축(PCA)** 을 구하고, 주축에 수직인 두 가장자리 중
             **몸통 반대쪽** 경계만 기존 하이라이트 색으로 칠한다

    🔴 **경계 한 줄만 칠한다.** 면을 칠하면 날이 납작해진다 — `collapseBand` 의 `to:"down"` 이 저지른 실패와 같다.
    🔴 **자세가 비대칭이라 「거울상」으로는 못 찾는다.** 공격 프레임에서 날이 머리 위로 서므로 좌우 대칭이 깨진다.

    레시피 예: "bladeWear": {"value": 0.60, "erode": 3, "minBlade": 60, "minSeed": 8}
    """
    a = np.array(image)
    alpha = a[..., 3] > ALPHA_CUT
    if not alpha.any():
        return image
    height, width = alpha.shape
    value = rgbToHsv(a[..., :3].astype(np.float64) / 255.0)[..., 2]
    threshold = r.get("value", 0.60)
    erode = r.get("erode", 3)
    minBlade = r.get("minBlade", 60)
    minSeed = r.get("minSeed", 8)

    bright = alpha & (value >= threshold)
    if not bright.any():
        print("  날 마모 — 기준이 될 하이라이트가 한 줄도 없다, 건너뛴다")
        return image
    ink = Counter(tuple(int(q) for q in a[y, x, :3])
                  for y, x in zip(*np.where(bright))).most_common(1)[0][0]

    def step(mask, keep):
        """4-이웃 침식(keep=True) 또는 팽창(keep=False). 캔버스 밖은 투명으로 본다."""
        out = mask.copy()
        for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            shifted = np.roll(mask, (dy, dx), (0, 1))
            if dy == 1:
                shifted[0, :] = False
            if dy == -1:
                shifted[-1, :] = False
            if dx == 1:
                shifted[:, 0] = False
            if dx == -1:
                shifted[:, -1] = False
            out = (out & shifted) if keep else (out | shifted)
        return out

    thick = alpha
    for _ in range(erode):
        thick = step(thick, True)
    for _ in range(erode):
        thick = step(thick, False)
    thick &= alpha
    thin = alpha & ~thick
    if not thick.any() or not thin.any():
        print("  날 마모 — 몸통과 날이 안 갈린다(erode 를 다시 볼 것), 건너뛴다")
        return image

    neighbours = [(-1, -1), (-1, 0), (-1, 1), (0, -1), (0, 1), (1, -1), (1, 0), (1, 1)]
    seen = np.zeros_like(thin)
    blades = []
    for sy, sx in zip(*np.where(thin)):
        if seen[sy, sx]:
            continue
        stack, group = [(sy, sx)], []
        seen[sy, sx] = True
        while stack:
            y, x = stack.pop()
            group.append((y, x))
            for dy, dx in neighbours:
                ny, nx = y + dy, x + dx
                if 0 <= ny < height and 0 <= nx < width and thin[ny, nx] and not seen[ny, nx]:
                    seen[ny, nx] = True
                    stack.append((ny, nx))
        if len(group) >= minBlade:
            blades.append(group)

    edge = outlineMask(alpha)
    body = np.array(np.where(thick), dtype=float).mean(axis=1)
    painted = skipped = 0
    for group in blades:
        if sum(1 for y, x in group if bright[y, x]) >= minSeed:
            skipped += 1
            continue                                  # 이미 줄이 있는 날 — 안 건드린다
        pts = np.array(group, dtype=float)
        centre = pts.mean(axis=0)
        spread = pts - centre
        if len(pts) < 3:
            continue
        _, vectors = np.linalg.eigh(np.cov(spread.T))
        axis = vectors[:, -1]                          # 가장 긴 축 = 날이 뻗은 방향
        perp = np.array([-axis[1], axis[0]])           # 그에 수직 = 날의 두께 방향
        toBody = float((body - centre) @ perp)
        side = 1.0 if toBody >= 0 else -1.0            # 몸통이 있는 쪽
        for y, x in group:
            if not edge[y, x]:
                continue
            if (np.array([y, x], dtype=float) - centre) @ perp * side >= 0:
                continue                               # 몸통 쪽 가장자리 — 바깥이 아니다
            if tuple(int(q) for q in a[y, x, :3]) != ink:
                a[y, x, :3] = ink
                painted += 1
    print(f"  날 마모 — 날 {len(blades)}개 중 {skipped}개는 이미 줄이 있다 · 칠한 {painted}px")
    return Image.fromarray(a, "RGBA")


def smoothAlongAxis(image, r):
    """긴 날 같은 **띠 모양 부위**를 제 축 방향으로 고른다 — 「지글거림」을 없앤다.

    🔴 감시자 거인 R2 에서 **점을 지우는 것만으로는 끝나지 않았다.** 날의 명암이
       **띠(band)가 아니라 점묘(dither)로 찍혀** 있어, 고립 픽셀을 지우고 대역을 흡수해도
       남은 것들이 계속 지글거렸다(사용자 6회 지적).

    🔑 픽셀 아트에서 대각선 칼날은 **축을 따라 평행한 밴드**여야 한다.
       그래서 **축 방향으로 최빈색을 고르면** 축을 따라 색이 이어지고 축에 수직인 노이즈가 사라진다.

    공정: ① 하이라이트(`seedValue` 이상)의 연결 성분을 날의 **씨앗**으로 잡고
          ② 성분의 주축을 PCA 로 구한 뒤 ③ 축 수직으로 `span` 만큼 넓혀 **날 영역**을 만들고
          ④ 그 안의 픽셀을 **축 방향 ±`reach` 이웃의 최빈색**으로 바꾼다.

    🔴 **프레임 단위다** — 이 적은 회전베기라 **프레임마다 날 각도가 바뀐다.** 축을 프레임마다 다시 구해야 한다.
    ✅ **몸통은 안 걸린다** — 몸통에는 20px 넘는 긴 하이라이트 줄이 없다.
    ✅ **외곽선은 건드리지 않는다**(`outlineBelow` 아래는 표본에서도 빼고 대상에서도 뺀다).

    레시피 예: "bladeSmooth": {"seedValue": 0.60, "minSeed": 20, "span": 6, "reach": 2}
    """
    a = np.array(image)
    alpha = a[..., 3] > ALPHA_CUT
    if not alpha.any():
        return image
    height, width = alpha.shape
    value = rgbToHsv(a[..., :3].astype(np.float64) / 255.0)[..., 2]
    seedValue = r.get("seedValue", 0.60)
    minSeed = r.get("minSeed", 20)
    span = r.get("span", 6)
    reach = r.get("reach", 2)
    outlineBelow = r.get("outlineBelow", 0.10)
    neighbours = [(-1, -1), (-1, 0), (-1, 1), (0, -1), (0, 1), (1, -1), (1, 0), (1, 1)]

    seedMask = alpha & (value >= seedValue)
    seen = np.zeros_like(seedMask)
    seeds = []
    for sy, sx in zip(*np.where(seedMask)):
        if seen[sy, sx]:
            continue
        stack, group = [(sy, sx)], []
        seen[sy, sx] = True
        while stack:
            y, x = stack.pop()
            group.append((y, x))
            for dy, dx in neighbours:
                ny, nx = y + dy, x + dx
                if 0 <= ny < height and 0 <= nx < width and seedMask[ny, nx] and not seen[ny, nx]:
                    seen[ny, nx] = True
                    stack.append((ny, nx))
        if len(group) >= minSeed:
            seeds.append(group)

    touched = 0
    for group in seeds:
        points = np.array(group, dtype=float)
        centred = points - points.mean(axis=0)
        weights, vectors = np.linalg.eigh(np.cov(centred.T))
        axis = vectors[:, int(np.argmax(weights))]
        axis = axis / np.linalg.norm(axis)
        perp = np.array([-axis[1], axis[0]])

        band = np.zeros_like(alpha)
        for py, px in group:
            for t in range(-span, span + 1):
                y = int(round(py + perp[0] * t))
                x = int(round(px + perp[1] * t))
                if 0 <= y < height and 0 <= x < width and alpha[y, x] and value[y, x] >= outlineBelow:
                    band[y, x] = True

        for y, x in zip(*np.where(band)):
            samples = []
            for t in range(-reach, reach + 1):
                sy = int(round(y + axis[0] * t))
                sx = int(round(x + axis[1] * t))
                if 0 <= sy < height and 0 <= sx < width and alpha[sy, sx] and value[sy, sx] >= outlineBelow:
                    samples.append(tuple(int(q) for q in a[sy, sx, :3]))
            if len(samples) < 3:
                continue
            best = Counter(samples).most_common(1)[0][0]
            if best != tuple(int(q) for q in a[y, x, :3]):
                a[y, x, :3] = best
                touched += 1
    print(f"  축 방향 고르기 — 띠 {len(seeds)}개 · {touched}px")
    return Image.fromarray(a, "RGBA")


def despeckle(image, r):
    """튀는 픽셀을 걷는다 — ① 비슷한 명도의 이웃이 하나도 없는 것 ② 너무 작은 연결 덩어리.

    🔴 감시자 거인 R2 에서 사용자가 **네 번** 지적한 끝에 자리 잡은 기준이다. 내가 틀린 것들:
      - 「무채인가」를 봤는데 봐야 할 것은 **「주변보다 밝은가」**였다(무채 스냅은 색만 바꾸고 밝기는 뒀다)
      - 고립을 **8이웃이 다 찬 것**으로 좁혀, 가장자리의 `0/3`·`0/5`·`0/6` 을 놓쳤다 →
        🔑 **비슷한 이웃이 0 이면 이웃 수와 무관하게 고립이다**
      - 같은 행의 **가로 거리**로 묶었더니 **두 날을 서로 「튄 것」으로 오인**해 줄을 조각냈다 →
        🔑 **연결 성분으로 묶어야 각 날이 자기 덩어리로 남는다**

    레시피 예: "despeckle": {"valueMin": 0.30, "minComponent": 5, "sameBand": 0.12}
    """
    a = np.array(image)
    alpha = a[..., 3] > ALPHA_CUT
    if not alpha.any():
        return image
    hsv = rgbToHsv(a[..., :3].astype(np.float64) / 255.0)
    value = hsv[..., 2]
    valueMin = r.get("valueMin", 0.30)
    sameBand = r.get("sameBand", 0.12)
    minComponent = r.get("minComponent", 5)
    neighbours = [(-1, -1), (-1, 0), (-1, 1), (0, -1), (0, 1), (1, -1), (1, 0), (1, 1)]
    height, width = alpha.shape

    def around(y, x):
        return [(y + dy, x + dx) for dy, dx in neighbours
                if 0 <= y + dy < height and 0 <= x + dx < width and alpha[y + dy, x + dx]]

    # ① 고립 — 비슷한 명도의 이웃이 0 개
    isolated = 0
    for y, x in zip(*np.where(alpha & (value >= valueMin))):
        nb = around(y, x)
        if nb and not any(abs(value[p] - value[y, x]) < sameBand for p in nb):
            a[y, x, :3] = Counter(tuple(int(q) for q in a[p][:3]) for p in nb).most_common(1)[0][0]
            isolated += 1

    # ② 작은 연결 덩어리 — 큰 덩어리(각 날의 줄 · 몸통 명암)는 남는다
    hsv = rgbToHsv(a[..., :3].astype(np.float64) / 255.0)
    value = hsv[..., 2]
    mask = alpha & (value >= valueMin)
    seen = np.zeros_like(mask)
    small = 0
    for sy, sx in zip(*np.where(mask)):
        if seen[sy, sx]:
            continue
        stack, group = [(sy, sx)], []
        seen[sy, sx] = True
        while stack:
            y, x = stack.pop()
            group.append((y, x))
            for dy, dx in neighbours:
                ny, nx = y + dy, x + dx
                if 0 <= ny < height and 0 <= nx < width and mask[ny, nx] and not seen[ny, nx]:
                    seen[ny, nx] = True
                    stack.append((ny, nx))
        if len(group) >= minComponent:
            continue
        for y, x in group:
            dark = [p for p in around(y, x) if value[p] < valueMin]
            if dark:
                a[y, x, :3] = Counter(tuple(int(q) for q in a[p][:3]) for p in dark).most_common(1)[0][0]
                small += 1
    print(f"  튀는 픽셀 정리 — 고립 {isolated}px · {minComponent}px 미만 덩어리 {small}px")
    return Image.fromarray(a, "RGBA")


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
