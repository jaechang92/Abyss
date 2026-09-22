# -*- coding: utf-8 -*-
"""시트 조립 후처리의 **공용 헬퍼** — 색 공간 변환과 외곽선 마스크.

🔴 **왜 나뉘어 있나** (2026-09-22)

  `build_form_sheets.py` 가 **1050줄**이 되어 컨벤션(500줄)을 두 배 넘겼다. 후처리 함수가
  종을 더할 때마다 하나씩 늘어난 것이다(감시자 거인 하나에 네 개가 붙었다).
  **후처리만 떼도 694줄**이라 한 파일로는 또 넘어서, **하는 일로** 넷으로 나눴다.

  🔑 경계를 「시트 단위 vs 프레임 단위」로 잡지 않았다 — 그것은 `buildState` 가 정하는 것이지
     함수의 성격이 아니다(`collapseBand` 는 시트 단위지만 색이고, `normalizeOutline` 은
     프레임 단위지만 정리다).

  🔴 **레시피 키는 한 글자도 안 바뀌었다.** 이 분할은 파일 경계만 옮긴다.

  여기 있는 것은 **셋 이상이 쓰는 것**만이다. 하나만 쓰는 헬퍼는 그 모듈에 둔다.
"""

import numpy as np

ALPHA_CUT = 8


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
