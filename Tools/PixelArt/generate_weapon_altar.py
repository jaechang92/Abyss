"""
generate_weapon_altar.py
Abyss 프로젝트 무기 보상 제단(WeaponAltar) 월드 스프라이트 절차적 생성기.
Pillow(PIL) 필요: pip install Pillow

출력: Assets/Art/Sprites/Props/weapon_altar.png (32x48, PPU 32 기준 = 1x1.5 월드 유닛)
실행: python Tools/PixelArt/generate_weapon_altar.py (경로는 __file__ 기준)

디자인: 낮고 넓은 돌 받침에 칼날째 꽂힌 검 + 강철빛 발광.

🔴 폼 제단(generate_form_altar.py)과 **실루엣이 달라야 한다.** 둘은 같은 자리에 놓이고
(WeaponAltarBuilder 참조) 틴트만 다르면 같은 것으로 읽혔다. 그래서
  - 몸통: 폼 제단은 「가는 기둥 + 넓은 상판」 → 여기는 「기둥 없이 낮고 넓은 돌덩이」
  - 위:   폼 제단은 「둥근 오브」       → 여기는 「세로로 선 검 + 가로 코등이(十자)」
  - 빛:   폼 제단은 호박색              → 여기는 차가운 강철빛
같은 세계의 소품으로 읽히도록 **외곽선·돌 팔레트·받침 슬래브·캔버스 크기는 폼 제단과 같게** 둔다.
"""

import os
from PIL import Image, ImageDraw

OUTPUT_DIR = os.path.join(os.path.dirname(__file__), "..", "..", "Assets", "Art", "Sprites", "Props")
OUTPUT_DIR = os.path.normpath(OUTPUT_DIR)

W, H = 32, 48

# 폼 제단과 공유하는 색 — 같은 세계의 돌이어야 한다.
OUTLINE = (26, 26, 26, 255)     # #1A1A1A
STONE_L = (156, 156, 172, 255)  # 밝은 돌
STONE_M = (112, 112, 130, 255)  # 중간 돌
STONE_D = (74, 74, 92, 255)     # 진한 돌(그림자)

# 무기 제단 고유 색 — 강철(칼날)·가죽(손잡이)·차가운 발광.
STEEL_L = (226, 234, 244, 255)  # 칼날 하이라이트
STEEL_M = (168, 182, 202, 255)  # 칼날 본체
STEEL_D = (104, 118, 142, 255)  # 칼날 그림자면
GUARD = (132, 140, 156, 255)    # 코등이(칼날보다 어둡게 — 十자 윤곽을 세운다)
LEATHER = (96, 62, 44, 255)     # 손잡이 감개
LEATHER_D = (64, 40, 30, 255)   # 감개 골
GLOW = (150, 200, 255)          # 발광(알파는 겹마다 따로)


def ensure_dir(path):
    os.makedirs(path, exist_ok=True)


def make_altar():
    img = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)

    def slab(box, fill):
        d.rectangle(box, fill=fill, outline=OUTLINE)

    # ── 발광 (검 뒤, 가장 먼저) ─────────────────────────────
    # 오브처럼 둥글게 두면 폼 제단으로 읽힌다 → 검을 따라 세로로 긴 타원.
    # 바깥 겹이 캔버스 위(y<0)로 나가면 발광이 평평하게 잘린다 → cy - ry >= 0 을 지킨다.
    for (rx, ry, a) in ((8, 16, 45), (5, 13, 80)):
        cx, cy = 16, 16
        d.ellipse([cx - rx, cy - ry, cx + rx, cy + ry], fill=GLOW + (a,))

    # ── 돌 받침 (아래에서 위로) ─────────────────────────────
    # 받침 슬래브 — 폼 제단과 같은 자리·폭(두 제단의 바닥선이 맞는다)
    slab([4, 40, 27, 46], STONE_M)
    d.rectangle([5, 40, 26, 41], fill=STONE_L)
    d.rectangle([5, 45, 26, 45], fill=STONE_D)

    # 낮고 넓은 돌덩이 — 기둥이 없다(폼 제단과 갈리는 첫째 자리)
    slab([6, 30, 25, 40], STONE_M)
    d.rectangle([7, 30, 24, 31], fill=STONE_L)   # 윗면 하이라이트
    d.rectangle([7, 38, 24, 39], fill=STONE_D)   # 아랫단 그림자
    d.rectangle([22, 32, 24, 37], fill=STONE_D)  # 우측 그림자면
    # 칼이 박힌 자리에서 번진 금
    d.point([(13, 32), (12, 33), (12, 34), (19, 32), (20, 33)], fill=STONE_D)
    d.point([(11, 35), (21, 34)], fill=OUTLINE)

    # ── 검 (칼날이 돌에 박혀 있다) ─────────────────────────
    # 칼날: x 15..17 (3px), 코등이 아래부터 돌 윗면까지
    blade_top, blade_bottom = 14, 30
    d.rectangle([14, blade_top, 18, blade_bottom], fill=OUTLINE)
    d.rectangle([15, blade_top, 17, blade_bottom], fill=STEEL_M)
    d.line([(15, blade_top), (15, blade_bottom)], fill=STEEL_L)   # 좌측 날 하이라이트
    d.line([(17, blade_top), (17, blade_bottom)], fill=STEEL_D)   # 우측 그림자면
    # 박힌 자리 — 칼날 양옆 돌에 어두운 틈
    d.point([(14, 30), (18, 30)], fill=OUTLINE)

    # 코등이: 가로로 넓게(十자 실루엣의 가로획)
    slab([9, 11, 23, 13], GUARD)
    d.line([(10, 11), (22, 11)], fill=STEEL_L)
    d.point([(9, 13), (23, 13)], fill=OUTLINE)

    # 손잡이: 가죽 감개(골을 두 줄마다)
    slab([14, 4, 18, 11], LEATHER)
    for y in (6, 8, 10):
        d.line([(15, y), (17, y)], fill=LEATHER_D)

    # 폼멜
    slab([13, 1, 19, 4], GUARD)
    d.point([(15, 2)], fill=STEEL_L)

    # 반짝임 — 칼날 위쪽 하나
    d.point([(15, 16)], fill=(255, 255, 255, 255))

    return img


def main():
    ensure_dir(OUTPUT_DIR)
    out_path = os.path.join(OUTPUT_DIR, "weapon_altar.png")
    make_altar().save(out_path, "PNG")
    print(f"  [weapon_altar] {W}x{H}px -> {out_path}")


if __name__ == "__main__":
    main()
