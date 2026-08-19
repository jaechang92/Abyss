# -*- coding: utf-8 -*-
"""
form_shading.py — 픽셀 아트 셰이딩 엔진.

🔑 이 파일이 "왜 지금 스프라이트가 탁해 보이는가"에 대한 답이다.

기존 팔레트는 한 색의 **명도만** 낮춰 3단계를 만들었다(#C65636 → #8C3426 → #541E18).
그러면 그림자가 회색으로 죽고 전체가 탁해진다. 프로 픽셀 아트가 다른 이유는 기법 하나다:

  🔴 **색조 이동(hue shift)** — 그림자로 갈수록 색상을 차갑게(보라·파랑) 돌리고
     채도를 올린다. 하이라이트로 갈수록 따뜻하게(노랑) 돌리고 채도를 낮춘다.

여기에 둘을 더한다:
  · **림라이트** — 광원 반대쪽 가장자리에 한 줄 밝은 선. 배경에서 형체를 떼어낸다.
  · **셀아웃(selective outline)** — 외곽선을 검정 하나로 두르지 않고, 빛 받는 쪽은
    밝은 색조의 어두운 색으로 둔다. 검정 테두리는 도트를 스티커처럼 보이게 만든다.

광원은 **왼쪽 위 고정**이다. 게임 내 조명과 무관하게 스프라이트끼리 광원이 어긋나면
그게 가장 먼저 아마추어로 읽힌다.
"""
import colorsys

# 광원 방향(정규화된 화면 좌표). 왼쪽 위.
LIGHT = (-0.55, -0.83)

# 색조 이동량(도). 양수 = 따뜻한 쪽, 음수 = 차가운 쪽.
# 그림자를 -18도 돌리는 것이 이 엔진의 핵심 숫자다.
HUE_SHIFT = {
    "hi":     +14.0,   # 하이라이트: 노랑 쪽
    "light":  +6.0,
    "base":    0.0,
    "shade":  -12.0,   # 그림자: 보라 쪽
    "dark":   -18.0,
    "line":   -22.0,   # 외곽선까지 색조를 돌린다(검정으로 두지 않는다)
}

# 명도 배율
VALUE = {"hi": 1.34, "light": 1.16, "base": 1.0, "shade": 0.72, "dark": 0.50, "line": 0.30}

# 채도 배율 — 어두울수록 채도를 **올린다**(명도만 낮추면 회색으로 죽는다)
SAT = {"hi": 0.62, "light": 0.84, "base": 1.0, "shade": 1.18, "dark": 1.30, "line": 1.24}

TONES = ("hi", "light", "base", "shade", "dark", "line")


def ramp(base_rgb):
    """
    기본색 하나에서 6단계 램프를 만든다. 반환 dict: tone -> (R,G,B,A).

    HSV로 옮겨 색조·채도·명도를 각각 조정한다. RGB에서 곱셈으로 밝기만 바꾸면
    색조가 그대로 남아 탁해진다 — 그게 지금 스프라이트의 문제였다.
    """
    r, g, b = [c / 255.0 for c in base_rgb[:3]]
    h, s, v = colorsys.rgb_to_hsv(r, g, b)

    out = {}
    for tone in TONES:
        hh = (h + HUE_SHIFT[tone] / 360.0) % 1.0
        ss = min(1.0, s * SAT[tone])
        vv = max(0.0, min(1.0, v * VALUE[tone]))
        rr, gg, bb = colorsys.hsv_to_rgb(hh, ss, vv)
        out[tone] = (int(rr * 255 + 0.5), int(gg * 255 + 0.5), int(bb * 255 + 0.5), 255)
    return out


def shade_tone(nx, ny):
    """
    표면 법선 근사(nx, ny)에서 톤 이름을 고른다.

    실루엣 안에서 각 픽셀이 '어느 쪽을 향하는가'를 이웃 픽셀 유무로 근사한다.
    정교한 3D 법선이 아니라 **도트에서 통하는 근사**다 — 단계가 6개뿐이라
    정밀도를 올려도 결과가 안 바뀐다.
    """
    d = nx * LIGHT[0] + ny * LIGHT[1]     # 램버트 근사
    if d > 0.80:
        return "hi"
    if d > 0.30:
        return "light"
    if d > -0.25:
        return "base"
    if d > -0.70:
        return "shade"
    return "dark"


def normal_at(mask, x, y, w, h):
    """
    이웃 8칸의 빈 자리 방향을 모아 법선을 근사한다.
    가장자리일수록 강한 방향이 나오고, 내부는 (0,0)에 가까워 base가 된다.
    """
    nx = ny = 0.0
    for dy in (-1, 0, 1):
        for dx in (-1, 0, 1):
            if dx == 0 and dy == 0:
                continue
            px, py = x + dx, y + dy
            empty = px < 0 or py < 0 or px >= w or py >= h or not mask[py][px]
            if empty:
                nx += dx
                ny += dy
    mag = (nx * nx + ny * ny) ** 0.5
    if mag < 0.001:
        return 0.0, 0.0
    return nx / mag, ny / mag


def is_rim(mask, x, y, w, h):
    """
    림라이트 자리인가 — 광원 **반대쪽** 가장자리 한 줄.
    배경이 어두운 게임이라 이 한 줄이 형체를 배경에서 떼어낸다.
    """
    nx, ny = normal_at(mask, x, y, w, h)
    if nx == 0.0 and ny == 0.0:
        return False
    d = nx * LIGHT[0] + ny * LIGHT[1]
    return d < -0.72
