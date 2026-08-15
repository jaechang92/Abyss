"""
title_bg_cliff.py
깊이 순환 레이어 — 절벽 조각(cliff slab)과 길바닥 무늬(road mark).
진입점은 generate_title_bg.py.

<b>절벽은 오른쪽 것만 굽는다.</b> 왼쪽은 런타임이 x 배율을 음수로 줘 뒤집는다 —
텍스처를 두 벌 굽지 않고, 좌우가 대칭으로 어긋나는 일도 없다.

기하(피벗·기울기·크기)는 전부 title_bg_config.py에 있다. 그림은 그 뼈대 위에 얹는 살이다.
문서: Docs/technical/title-backdrop.md
"""

import math
import random

from PIL import Image, ImageDraw, ImageFilter

from title_bg_config import (
    BASE_SLOPE, CLIFF_BASE, CLIFF_FACE, CLIFF_GRAIN_DARK, CLIFF_GRAIN_LIT, CLIFF_H,
    CLIFF_MARGIN, CLIFF_PIVOT_Y, CLIFF_REACH, CLIFF_RIM, CLIFF_W, MARK_GROUND_RATIO,
    MARK_H, MARK_W, RIM_SLOPE, ROAD_MARK_DARK, ROAD_MARK_LIT, ROAD_SPREAD, SEED,
    WALL_HEIGHT, lerp3,
)


def cliff_face(rng):
    """절벽 표면의 색판. 마스크로 잘라 쓰기 전의 '무한한 바위 벽'이다.

    <b>가로 그라데이션이 조각 사이의 색 계단을 지운다.</b> 조각마다 대기 원근 tint가
    한 단계씩 다른데, 실제로 보이는 것은 각 조각의 <b>안쪽 띠</b>뿐이다(그 바깥은 더
    가까운 조각이 덮는다). 그래서 안쪽을 밝게, 바깥으로 갈수록 어둡게 두면 한 조각의
    끝 밝기가 다음 조각의 시작 밝기와 이어져 띠가 연속으로 읽힌다.
    바깥 = 카메라에 더 가까운 쪽이므로 물리적으로도 맞다.
    """
    img = Image.new("RGBA", (CLIFF_W, CLIFF_H), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)

    for x in range(CLIFF_W):
        u = max(0.0, (x - CLIFF_MARGIN) / float(CLIFF_REACH))
        # 안쪽 150px 남짓에서 빠르게, 그 바깥은 완만하게 어두워진다.
        shade = 1.0 - 0.24 * min(1.0, u * CLIFF_REACH / 150.0) - 0.14 * u
        top = CLIFF_PIVOT_Y - WALL_HEIGHT + RIM_SLOPE * (x - CLIFF_MARGIN)
        bot = CLIFF_PIVOT_Y + BASE_SLOPE * (x - CLIFF_MARGIN)
        height = max(1.0, bot - top)
        for seg in range(24):
            t0 = seg / 24.0
            t1 = (seg + 1) / 24.0
            # 세로: 밑동은 그늘, 중턱이 밝고, 능선으로 갈수록 조금 죽는다.
            v = 1.0 - t0
            if v < 0.30:
                c = lerp3(CLIFF_BASE, CLIFF_FACE, (v / 0.30) ** 0.75)
            else:
                c = lerp3(CLIFF_FACE, lerp3(CLIFF_FACE, CLIFF_BASE, 0.35),
                          (v - 0.30) / 0.70)
            c = tuple(int(ch * shade) for ch in c)
            d.rectangle([x, top + height * t0, x + 1, top + height * t1 + 1],
                        fill=c + (255,))
    return img


def cliff_facets(rng):
    """바위 사면 — 큰 다각형 몇 장을 밝기를 달리해 겹친다.

    ⚠️ 부드러운 세로 결만 있으면 벽이 커튼처럼 보인다. 바위는 평면들의 집합이라
    <b>각진 명암 경계</b>가 있어야 한다. 블러를 거의 주지 않는 이유도 그것이다.
    """
    f = Image.new("RGBA", (CLIFF_W, CLIFF_H), (0, 0, 0, 0))
    fd = ImageDraw.Draw(f)
    for _ in range(80):
        cx = rng.uniform(-80, CLIFF_W + 80)
        cy = rng.uniform(-120, CLIFF_H + 120)
        rw = rng.uniform(50, 250)
        rh = rng.uniform(120, 640)
        n = rng.randint(4, 6)
        pts = []
        for k in range(n):
            ang = math.tau * k / n + rng.uniform(-0.28, 0.28)
            pts.append((cx + math.cos(ang) * rw * rng.uniform(0.6, 1.25),
                        cy + math.sin(ang) * rh * rng.uniform(0.6, 1.25)))
        col = CLIFF_GRAIN_LIT if rng.random() < 0.44 else CLIFF_GRAIN_DARK
        fd.polygon(pts, fill=col + (rng.randint(16, 46),))
    return f.filter(ImageFilter.GaussianBlur(1.0))


def cliff_grain(rng):
    """벽면 결 — 세로 균열과 층리, 그리고 가로 선반.

    관문판에서 얻은 교훈 그대로다. 결이 없으면 조각이 커질 때 실루엣만 벌어져
    확대(zoom)로 읽힌다. 다만 여기서는 <b>선반(ledge)</b>을 넣어도 된다 —
    조각마다 소실점에서 뻗는 광선을 따라 기울어 있어 가로 막대로 보이지 않는다.
    """
    grain = Image.new("RGBA", (CLIFF_W, CLIFF_H), (0, 0, 0, 0))
    gd = ImageDraw.Draw(grain)
    taper = (0.2, 0.6, 0.95, 1.0, 0.95, 0.6, 0.2)

    for _ in range(150):
        x = rng.uniform(-20, CLIFF_W + 20)
        w = rng.uniform(4, 30)
        y0 = rng.uniform(-CLIFF_H * 0.1, CLIFF_H * 0.9)
        span = rng.uniform(CLIFF_H * 0.10, CLIFF_H * 0.42)
        skew = rng.uniform(-16, 16)
        col = CLIFF_GRAIN_LIT if rng.random() < 0.38 else CLIFF_GRAIN_DARK
        base = rng.randint(20, 62)
        for k, weight in enumerate(taper):
            t0 = k / float(len(taper))
            t1 = (k + 1) / float(len(taper))
            gd.polygon([(x + skew * t0, y0 + span * t0), (x + w + skew * t0, y0 + span * t0),
                        (x + w + skew * t1, y0 + span * t1), (x + skew * t1, y0 + span * t1)],
                       fill=col + (int(base * weight),))

    # 선반 — 소실점 광선과 나란한 기울기 언저리로 흔든다.
    for _ in range(16):
        y = rng.uniform(0, CLIFF_H)
        slope = RIM_SLOPE * rng.uniform(0.25, 0.75)
        th = rng.uniform(3.0, 14.0)
        col = CLIFF_GRAIN_LIT if rng.random() < 0.45 else CLIFF_GRAIN_DARK
        x = rng.uniform(-120, 0)
        while x < CLIFF_W:
            seg = rng.uniform(CLIFF_W * 0.08, CLIFF_W * 0.30)
            if rng.random() < 0.7:
                gd.polygon([(x, y + slope * x), (x + seg, y + slope * (x + seg)),
                            (x + seg, y + slope * (x + seg) + th), (x, y + slope * x + th)],
                           fill=col + (rng.randint(14, 34),))
            x += seg + rng.uniform(CLIFF_W * 0.02, CLIFF_W * 0.12)

    return grain.filter(ImageFilter.GaussianBlur(2.6))


def silhouette_jitter(rng, jag):
    """안쪽 실루엣의 행별 흔들림.

    ⚠️ 사인 합으로 흔들면 <b>커튼</b>이 된다. 바위는 평면들이 만나 이룬 것이라 실루엣이
    직선 구간과 <b>수직 단층</b>으로 끊겨야 한다. 그래서 평균 회귀 랜덤워크를 꺾은선으로
    잇고, 가끔 값을 뚝 끊는다.
    """
    knots = []
    y = -CLIFF_H * 0.05
    v = 0.0
    while y < CLIFF_H * 1.05:
        knots.append((y, v))
        seg = rng.uniform(CLIFF_H * 0.04, CLIFF_H * 0.15)
        v += rng.uniform(-1.0, 1.0) * 40.0 * jag - v * 0.35   # 평균 회귀
        y += seg
        if rng.random() < 0.28:
            knots.append((y, v))                              # 수직 단층
            v += rng.uniform(-1.0, 1.0) * 55.0 * jag - v * 0.25
    knots.append((CLIFF_H * 1.2, v))

    out = [0.0] * CLIFF_H
    k = 0
    for y in range(CLIFF_H):
        while k + 1 < len(knots) - 1 and knots[k + 1][0] <= y:
            k += 1
        y0, v0 = knots[k]
        y1, v1 = knots[k + 1]
        t = 0.0 if y1 <= y0 else (y - y0) / (y1 - y0)
        out[y] = v0 + (v1 - v0) * max(0.0, min(1.0, t))
    return out


def cliff_mask(rng, jag, notch):
    """조각의 모양. 행마다 '벽이 시작되는 x'를 정한다.

    벽이 있는 영역은 <b>능선 광선 위쪽·밑동 광선 아래쪽을 뺀 쐐기</b>다. 두 경계를
    광선 기울기로 두는 것이 핵심 — 가로로 자르면 조각마다 계단이 생긴다.

    ⚠️ 밑동 광선보다 <b>안쪽으로 넘어오면 안 된다.</b> 그 아래는 길이라, 벽이 길을
       덮으면 협곡 바닥이 잘려 보인다. 잔결은 바깥 방향으로만 허용한다.
    """
    mask = Image.new("L", (CLIFF_W, CLIFF_H), 0)
    md = ImageDraw.Draw(mask)

    jitter = silhouette_jitter(rng, jag)
    # 큰 굴곡 하나 — 이게 없으면 조각이 전부 같은 모양의 쐐기로 보인다.
    # 삼각 쐐기로 둔다(부드러운 종 모양이면 다시 커튼이 된다).
    notch_y = rng.uniform(CLIFF_H * 0.15, CLIFF_H * 0.75)
    notch_h = rng.uniform(CLIFF_H * 0.10, CLIFF_H * 0.28)
    notch_peak = rng.uniform(0.25, 0.75)

    for y in range(CLIFF_H):
        x_rim = (CLIFF_PIVOT_Y - WALL_HEIGHT - y) / (-RIM_SLOPE)
        x_base = (y - CLIFF_PIVOT_Y) / BASE_SLOPE
        edge = max(0.0, x_rim, x_base)

        d = (y - notch_y) / notch_h
        if -1.0 < d < 1.0:
            u = (d + 1.0) * 0.5
            wedge = u / notch_peak if u < notch_peak else (1.0 - u) / (1.0 - notch_peak)
            edge += notch * max(0.0, wedge)

        x_in = edge + jitter[y] + CLIFF_MARGIN
        # 밑동 광선 안쪽(=길 위)으로는 절대 넘어오지 않게 한다.
        if y > CLIFF_PIVOT_Y:
            x_in = max(x_in, x_base + CLIFF_MARGIN)
        x_in = max(0.0, x_in)
        if x_in >= CLIFF_W:
            continue
        md.line([(x_in, y), (CLIFF_W, y)], fill=255)

    return mask.filter(ImageFilter.GaussianBlur(0.6))


def make_cliff(seed_offset, jag, notch):
    """오른쪽 절벽 조각 한 장."""
    rng = random.Random(SEED + seed_offset)

    face = cliff_face(rng)
    face.alpha_composite(cliff_facets(rng))
    face.alpha_composite(cliff_grain(rng))

    img = Image.new("RGBA", (CLIFF_W, CLIFF_H), (0, 0, 0, 0))
    img.paste(face, (0, 0), cliff_mask(rng, jag, notch))

    # 능선 하이라이트 — 하늘과 닿는 선이 또렷해야 조각의 겹이 읽힌다.
    # 마스크의 위쪽 경계를 다시 찾는 대신, 알파의 세로 미분을 쓰면 모양이 어떻든 따라간다.
    alpha = img.getchannel("A")
    px = alpha.load()
    rim = Image.new("RGBA", (CLIFF_W, CLIFF_H), (0, 0, 0, 0))
    rd = ImageDraw.Draw(rim)
    for x in range(CLIFF_W):
        for y in range(1, CLIFF_H):
            if px[x, y] > 140 and px[x, y - 1] < 60:
                rd.line([(x, y), (x, y + 3)], fill=CLIFF_RIM + (190,))
                break
    img.alpha_composite(rim.filter(ImageFilter.GaussianBlur(0.9)))

    return img


def make_road_mark(seed_offset, cracks):
    """길바닥의 균열과 자갈. <b>절벽과 같은 방식으로 다가오는 깊이 레이어</b>다.

    발밑이 정지해 있으면 벽이 아무리 지나가도 "저것이 다가온다"로 읽힌다. 걷는 감각의
    1차 근거는 지면이 나를 향해 흘러오는 것이라, 흐를 수 있는 무늬가 바닥에 있어야 한다.

    캔버스는 절벽과 다르지만 <b>바닥선 비율</b>만 맞춰 두면 런타임이 같은 배율 스케줄과
    같은 피벗(바닥선=소실점)으로 돌릴 수 있고, 균열이 길의 원뿔 위에 정확히 얹힌다.

    ⚠️ 한 줄로 죽 이으면 화면을 가로지르는 선반이 된다. 끊어 그려야 '바닥의 결'로 보인다.
    각진 사각형이 늘어서도 '돌 타일'로 읽히므로 조각마다 두께와 기울기를 흔든다.
    """
    rng = random.Random(SEED + seed_offset)
    img = Image.new("RGBA", (MARK_W, MARK_H), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)

    ground_top = int(MARK_H * MARK_GROUND_RATIO)
    depth = MARK_H - ground_top          # 바닥선 아래로 쓸 수 있는 높이
    cx = MARK_W * 0.5

    for _ in range(cracks):
        dy = rng.uniform(depth * 0.08, depth * 0.97)
        span = ROAD_SPREAD * dy          # 그 깊이에서의 길 반폭 — 밖으로 나가면 안 된다
        thick = 1.5 + 15.0 * (dy / depth) ** 2
        lit = rng.random() < 0.4
        tilt = rng.uniform(-0.05, 0.05)
        x = cx - span * rng.uniform(0.4, 1.0)
        end = cx + span * rng.uniform(0.4, 1.0)
        while x < end:
            seg = span * rng.uniform(0.05, 0.18)
            if rng.random() < 0.66:
                y0 = ground_top + dy + (x - cx) * tilt + rng.uniform(-thick, thick) * 0.5
                th = thick * rng.uniform(0.35, 1.0)
                col = (ROAD_MARK_LIT if lit else ROAD_MARK_DARK) + (rng.randint(30, 84),)
                d.polygon([(x, y0), (x + seg, y0 + seg * tilt),
                           (x + seg, y0 + seg * tilt + th), (x, y0 + th)], fill=col)
            x += seg + span * rng.uniform(0.02, 0.09)

    # 자갈 — 균열만 있으면 결이 가로로만 흘러 좌우 움직임이 안 읽힌다.
    for _ in range(cracks * 18):
        dy = rng.uniform(depth * 0.06, depth)
        span = ROAD_SPREAD * dy
        px_ = cx + rng.uniform(-span, span) * 0.96
        py = ground_top + dy
        r = (1.0 + 5.0 * (dy / depth) ** 1.7) * rng.uniform(0.5, 1.5)
        col = ROAD_MARK_LIT if rng.random() < 0.5 else ROAD_MARK_DARK
        d.ellipse([px_ - r, py - r * 0.45, px_ + r, py + r * 0.45],
                  fill=col + (rng.randint(26, 78),))

    return img.filter(ImageFilter.GaussianBlur(1.2))
