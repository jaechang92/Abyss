"""
generate_title_bg.py
Abyss 타이틀 화면 협곡 배경 레이어 절차적 생성기 (절벽 회랑판).
Pillow(PIL) 필요: pip install Pillow

출력: Assets/Art/Backgrounds/Title/*.png
실행: python Tools/PixelArt/generate_title_bg.py (경로는 __file__ 기준)

구도: **양쪽에 절벽을 끼고 가운데 길을 걸어 들어가는** 협곡 회랑.
좌우 절벽을 깊이가 다른 조각(slab) 여러 장으로 나눠, 각자 소실점에서 생겨나 커지며
바깥으로 흘러 카메라를 지나가게 한다(런타임: TitleBackdrop).

  title_sky           (2160x1320, 정적)    심연 그라데이션 하늘 + 소실점의 빛
  title_road          (2160x1320, 정적)    소실점에서 벌어지는 길 바닥
  title_horizon       (1920x1080, 거의정적) 길 끝에 보이는 원경 두 산
  title_cliff_a~d     (560x?, 다가옴)       좌우 절벽 조각 4종 — 오른쪽 것만 굽는다
  title_road_mark_a/b (2432x900, 다가옴)    길바닥 균열·자갈 — 지면 흐름을 만든다
  title_fog           (1920x1080, 느림)     낮게 깔린 안개
  title_vignette      (1920x1080, 정적)     UI 가독성용 어둠
  title_mote          (32x32)               소실점에서 흘러나오는 재 입자

절벽 텍스처는 **밝은 실루엣**으로 굽는다 — 런타임에서 깊이에 따라 색을 곱해
먼 것은 하늘색에 가깝게, 가까운 것은 검정에 가깝게 만든다(대기 원근).
그래서 이 파일의 색은 최종 화면 색이 아니라 '곱해지기 전의 밝기'다.

> 📌 **관문(gate)판은 폐기했다.** 협곡 단면을 통째로 그린 판을 깊이마다 겹치는 방식이었다.
> 판은 한 깊이에 놓인 평면이라 확대돼도 표면이 균일하게 커질 뿐 <b>표면 자체가 물러나지
> 않는다</b> — 결과가 "아치 터널"로 읽히고 도프 줌과 구별되지 않았다(2026-08-15 피드백).
> 절벽은 면이 z축으로 물러나야 하고, 그건 깊이에 흩어진 조각으로만 만들어진다.

<b>생성기가 '걷는 느낌'을 위해 지는 책임 셋</b> — 나머지는 런타임이 한다:
  ① 조각의 위·아래 경계를 소실점 광선에 맞춘다. 가로로 자르면 조각마다 계단이 생긴다.
  ② 벽면에 결(세로 균열·층리·선반)을 넣는다. 결이 없으면 실루엣만 벌어져 확대로 읽힌다.
  ③ 길바닥 균열·자갈을 따로 굽는다. 발밑이 정지해 있으면 '저것이 온다'가 된다.

구성:
  title_bg_config.py — 공용 상수·팔레트·헬퍼 (런타임/빌더와 짝이 맞아야 하는 수치)
  title_bg_cliff.py  — 깊이 순환 레이어(절벽 조각·길바닥 무늬)
  이 파일             — 그 밖의 레이어(하늘·원경·길·안개·비네트·입자) + 진입점
"""

import math
import os
import random

from PIL import Image, ImageDraw, ImageFilter

from title_bg_config import (
    DRIFT_MARGIN, FOG, GLOW, HORIZON_FAR, HORIZON_NEAR, HORIZON_RIM,
    OUTPUT_DIR, ROAD_SPREAD, SCREEN_H, SCREEN_W, SEED, SKY_LOW, SKY_MID, SKY_TOP,
    VANISH_RATIO, apply_vertical_ramp, ease, ensure_dir, lerp3,
)
from title_bg_cliff import make_cliff, make_road_mark


def make_sky():
    """세로 그라데이션 + 소실점에서 새어 나오는 빛 + 흐린 별.

    길과 마찬가지로 여백째 굽는다 — 소실점의 빛이 관문의 소실점과 같은 절대 위치에
    있어야 한다.
    """
    m = DRIFT_MARGIN
    w, h = SCREEN_W + m * 2, SCREEN_H + m * 2
    img = Image.new("RGBA", (w, h), SKY_TOP + (255,))
    d = ImageDraw.Draw(img)
    for y in range(h):
        t = min(1.0, max(0.0, (y - m) / (SCREEN_H - 1.0)))
        if t < 0.58:
            c = lerp3(SKY_TOP, SKY_MID, ease(t / 0.58))
        else:
            c = lerp3(SKY_MID, SKY_LOW, ease((t - 0.58) / 0.42))
        d.line([(0, y), (w, y)], fill=c + (255,))

    rng = random.Random(SEED + 7)
    for _ in range(280):
        x = rng.randrange(w)
        y = int(abs(rng.gauss(0, 1)) * SCREEN_H * 0.26)
        if y >= SCREEN_H * 0.62:
            continue
        img.putpixel((x, y), (196, 188, 230, rng.randint(40, 150)))

    # 소실점(화면 중앙 약간 아래)의 빛 — 길 끝이 밝아야 '들어가는' 방향이 읽힌다.
    small_w = 192
    small_h = max(2, int(round(small_w * h / float(w))))
    small = Image.new("RGBA", (small_w, small_h), (0, 0, 0, 0))
    sd = ImageDraw.Draw(small)
    cx = small_w * 0.5
    cy = small_h * (m + SCREEN_H * VANISH_RATIO) / float(h)
    for r, a in ((84, 34), (60, 50), (42, 70), (26, 96), (13, 132)):
        sd.ellipse([cx - r, cy - r * 0.62, cx + r, cy + r * 0.62], fill=GLOW + (a,))
    small = small.filter(ImageFilter.GaussianBlur(6))
    img.alpha_composite(small.resize((w, h), Image.BICUBIC))
    return img


def make_horizon():
    """길 끝에 보이는 원경 두 산. 소실점 주변에 작게 자리한다."""
    img = Image.new("RGBA", (SCREEN_W, SCREEN_H), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    rng = random.Random(SEED + 41)
    horizon_y = SCREEN_H * VANISH_RATIO

    # 뒷줄 → 앞줄 순서로 두 겹. 두 산 사이가 비어 길이 그리로 이어져 보인다.
    # ⚠️ 작게 그려야 한다 — 소실점 근처는 좌우 절벽이 거의 맞닿는 자리라, 크게 그리면
    #    절벽 밖으로 삐져나와 원근이 깨진다.
    for layer, (color, scale, spread) in enumerate((
            (HORIZON_FAR, 1.35, 0.075),
            (HORIZON_NEAR, 0.95, 0.048))):
        for side in (-1, 1):
            cx = SCREEN_W * (0.5 + side * spread)
            pw = SCREEN_W * 0.055 * scale
            ph = SCREEN_H * 0.052 * scale
            jag = [(rng.uniform(0.4, 1.1), rng.uniform(0, math.tau), rng.uniform(2.0, 6.0))
                   for _ in range(3)]
            for x in range(int(cx - pw), int(cx + pw) + 1):
                if x < 0 or x >= SCREEN_W:
                    continue
                # t가 1을 넘으면 음수의 실수 거듭제곱이라 complex가 나온다(정수 변환 오차).
                t = min(1.0, abs(x - cx) / pw)
                h = ph * (1.0 - t) ** 1.35
                for freq, phase, amp in jag:
                    h += amp * math.sin(freq * x * 0.03 + phase)
                if h <= 0:
                    continue
                top = horizon_y - h
                d.line([(x, top), (x, horizon_y)], fill=color + (255,))
                if layer == 1:
                    d.line([(x, top), (x, top + 2)], fill=HORIZON_RIM + (255,))

    return img.filter(ImageFilter.GaussianBlur(1.0))


def make_road():
    """소실점에서 화면 아래로 벌어지는 길. 관문 뒤에 깔리는 정적 평면.

    원근에서 가로 폭은 소실점으로부터의 세로 거리에 비례하므로 사다리꼴(선형)이다.

    ⚠️ 가장자리 두 줄은 <b>일부러 어둡게</b> 둔다. 이 줄은 소실점에서 뻗는 완벽한 직선이라
    길이 굽는 연출과 정면으로 싸운다 — 밝게 두면 "길은 곧은데 벽만 흔들린다"로 읽힌다.
    길이 어디까지인지는 흐르는 균열(title_road_mark)이 대신 알려준다.
    """
    m = DRIFT_MARGIN
    w, h = SCREEN_W + m * 2, SCREEN_H + m * 2
    img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    # 소실점은 여백을 포함한 절대 위치에 둔다(title_bg_config.DRIFT_MARGIN 주석 참고).
    vanish_y = m + SCREEN_H * VANISH_RATIO
    span = SCREEN_H * (1.0 - VANISH_RATIO)
    cx = w * 0.5

    for y in range(int(vanish_y), h):
        t = min(1.0, (y - vanish_y) / span)
        half = ROAD_SPREAD * (y - vanish_y)
        # 길 밖(협곡 바닥 그늘)
        d.line([(0, y), (w, y)], fill=lerp3((34, 31, 48), (16, 15, 22), ease(t)) + (255,))
        if half < 1.0:
            continue
        # 길 안 — 멀수록 밝다(소실점의 빛을 받는다)
        c = lerp3((38, 35, 52), (19, 18, 26), ease(t ** 0.85))
        d.line([(cx - half, y), (cx + half, y)], fill=c + (255,))
        # 가장자리 두 줄 — 존재는 하되 눈에 띄지 않을 만큼만
        edge = lerp3((62, 57, 84), (30, 28, 40), ease(t ** 0.5))
        ew = max(1.0, 1.5 + 2.0 * t)
        d.line([(cx - half, y), (cx - half + ew, y)], fill=edge + (255,))
        d.line([(cx + half - ew, y), (cx + half, y)], fill=edge + (255,))

    # ⚠️ 소실선에서 불투명하게 시작하면 하늘과 만나는 <b>완벽한 수평선</b>이 생긴다.
    #    관문 벽이 대부분 가려 주지만 벽 사이로 짧은 '선반' 조각이 드러난다.
    #    바닥이 하늘로 스며들도록 알파를 태워 없앤다.
    fade = 110.0
    apply_vertical_ramp(img, lambda y: (y - vanish_y) / fade)
    return img.filter(ImageFilter.GaussianBlur(1.6))


def make_fog():
    """길 위에 낮게 깔린 안개. 관문 사이의 깊이를 뭉개 거리감을 만든다."""
    # 안개는 화면에 고정된 레이어라(드리프트 밖) 화면 크기로 굽는다.
    small_w, small_h = SCREEN_W // 8, SCREEN_H // 8
    small = Image.new("RGBA", (small_w, small_h), (0, 0, 0, 0))
    d = ImageDraw.Draw(small)
    rng = random.Random(SEED + 21)
    for _ in range(52):
        cx = rng.uniform(0, small_w)
        cy = rng.uniform(small_h * 0.50, small_h * 0.78)
        rw = rng.uniform(small_w * 0.12, small_w * 0.30)
        rh = rng.uniform(small_h * 0.018, small_h * 0.048)
        a = rng.randint(26, 54)
        for off in (-small_w, 0, small_w):
            d.ellipse([cx + off - rw, cy - rh, cx + off + rw, cy + rh], fill=FOG + (a,))

    # 블러·확대는 가장자리에서 바깥 픽셀을 복제한다(wrap 하지 않는다).
    # 가로 3벌 이어 붙여 처리하고 가운데만 잘라 쓴다.
    wide = Image.new("RGBA", (small_w * 3, small_h), (0, 0, 0, 0))
    for i in range(3):
        wide.paste(small, (small_w * i, 0))
    wide = wide.filter(ImageFilter.GaussianBlur(4))
    wide = wide.resize((SCREEN_W * 3, SCREEN_H), Image.BICUBIC)
    return wide.crop((SCREEN_W, 0, SCREEN_W * 2, SCREEN_H))


def make_vignette():
    """가장자리 어둠 + 하단 중앙 UI 영역 어둠.

    작은 캔버스에 픽셀 단위로 알파를 계산한 뒤 확대한다 — 타원 링을 겹치면 경계가
    테두리로 남아 배경이 '구멍 뚫린 판'처럼 보인다.
    """
    small_w, small_h = 256, 144
    small = Image.new("RGBA", (small_w, small_h), (0, 0, 0, 0))
    px = small.load()
    cx, cy = small_w * 0.5, small_h * 0.46
    bx, by = small_w * 0.5, small_h * 0.60

    for y in range(small_h):
        for x in range(small_w):
            dx = (x - cx) / (small_w * 0.62)
            dy = (y - cy) / (small_h * 0.72)
            edge = min(1.0, (dx * dx + dy * dy) ** 0.5)
            a = 124.0 * edge ** 2.4

            ux = (x - bx) / (small_w * 0.34)
            uy = (y - by) / (small_h * 0.34)
            u = ux * ux + uy * uy
            if u < 1.0:
                a += 76.0 * (1.0 - u) ** 1.6

            px[x, y] = (2, 2, 6, min(230, int(a)))

    small = small.filter(ImageFilter.GaussianBlur(4))
    return small.resize((SCREEN_W, SCREEN_H), Image.BICUBIC)


def make_mote():
    """재 입자 — 가운데가 밝은 소프트 원."""
    size = 32
    img = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    c = size * 0.5
    for r, a in ((13, 26), (10, 52), (7, 96), (4, 168), (2, 235)):
        d.ellipse([c - r, c - r, c + r, c + r], fill=(214, 200, 255, a))
    return img.filter(ImageFilter.GaussianBlur(1.4))


# ── 진입점 ────────────────────────────────────────────────

def main():
    ensure_dir(OUTPUT_DIR)

    layers = [
        ("title_sky", make_sky()),
        ("title_horizon", make_horizon()),
        ("title_road", make_road()),
        # 절벽 조각 4종 — 지나가는 순서가 반복돼도 눈에 띄지 않도록 실루엣을 서로 다르게.
        # 인자는 (시드, 잔결 세기, 큰 굴곡 깊이). 굴곡을 음수로 주면 안쪽으로 튀어나온
        # 바위가 되고, 양수면 움푹 팬 자리가 된다.
        ("title_cliff_a", make_cliff(101, 1.0, -70)),
        ("title_cliff_b", make_cliff(202, 1.5, 90)),
        ("title_cliff_c", make_cliff(303, 0.7, -130)),
        ("title_cliff_d", make_cliff(404, 1.2, 40)),
        # 길바닥 무늬 — 캔버스는 작지만 바닥선 비율이 같아 같은 깊이 순환에 그대로 태운다.
        ("title_road_mark_a", make_road_mark(511, 11)),
        ("title_road_mark_b", make_road_mark(622, 9)),
        ("title_fog", make_fog()),
        ("title_vignette", make_vignette()),
        ("title_mote", make_mote()),
    ]

    for name, img in layers:
        path = os.path.join(OUTPUT_DIR, f"{name}.png")
        img.save(path, "PNG")
        print(f"  [{name}] {img.width}x{img.height}px -> {path}")

    print(f"완료 — {len(layers)}장. Unity에서 Tools/Abyss/Build/Title Scene 재실행 필요.")


if __name__ == "__main__":
    main()
