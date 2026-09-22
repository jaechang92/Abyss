# -*- coding: utf-8 -*-
"""**시트 전체의 색을 옮긴다** — 생성물의 재료색이 규약과 어긋날 때 생성을 다시 돌리지 않고 색만 옮긴다.

🔴 **왜 나뉘어 있나** (2026-09-22)

  `build_form_sheets.py` 가 **1050줄**이 되어 컨벤션(500줄)을 두 배 넘겼다. 후처리 함수가
  종을 더할 때마다 하나씩 늘어난 것이다(감시자 거인 하나에 네 개가 붙었다).
  **후처리만 떼도 694줄**이라 한 파일로는 또 넘어서, **하는 일로** 넷으로 나눴다.

  🔑 경계를 「시트 단위 vs 프레임 단위」로 잡지 않았다 — 그것은 `buildState` 가 정하는 것이지
     함수의 성격이 아니다(`collapseBand` 는 시트 단위지만 색이고, `normalizeOutline` 은
     프레임 단위지만 정리다).

  🔴 **레시피 키는 한 글자도 안 바뀌었다.** 이 분할은 파일 경계만 옮긴다.

  🔴 **여기 있는 것은 전부 「번들 시트 한 장」에 건다**(프레임마다 걸면 안 된다).
     프레임마다 팔레트를 새로 뽑으면 **같은 색이 프레임마다 다른 색으로 가서 깜빡인다**
     (원거리 사수 v2 계열). `main()` 이 `buildState` 보다 **먼저** 부르는 이유다.
"""

from collections import Counter
import numpy as np
from PIL import Image

from sheet_common import ALPHA_CUT, hsvToRgb, rgbToHsv


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
