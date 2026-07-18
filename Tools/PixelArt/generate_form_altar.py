"""
generate_form_altar.py
Abyss 프로젝트 폼 보상 제단(FormAltar) 월드 스프라이트 절차적 생성기.
Pillow(PIL) 필요: pip install Pillow

출력: Assets/Art/Sprites/Props/form_altar.png (32x48, PPU 32 기준 = 1x1.5 월드 유닛)
실행: python Tools/PixelArt/generate_form_altar.py (경로는 __file__ 기준)

디자인: 돌 제단(계단식 받침 + 기둥 + 상단 슬래브) 위에 떠 있는 호박색 발광 오브.
색은 FormAltarBuilder의 호박색 테마(보상)와 맞춘다.
"""

import os
from PIL import Image, ImageDraw

OUTPUT_DIR = os.path.join(os.path.dirname(__file__), "..", "..", "Assets", "Art", "Sprites", "Props")
OUTPUT_DIR = os.path.normpath(OUTPUT_DIR)

W, H = 32, 48

OUTLINE = (26, 26, 26, 255)   # #1A1A1A
STONE_L = (156, 156, 172, 255)  # 밝은 돌
STONE_M = (112, 112, 130, 255)  # 중간 돌
STONE_D = (74, 74, 92, 255)     # 진한 돌(그림자)

GOLD = (245, 190, 70, 255)    # 오브 본체(호박)
CORE = (255, 244, 190, 255)   # 밝은 코어
DEEP = (208, 132, 38, 255)    # 하단 그림자


def ensure_dir(path):
    os.makedirs(path, exist_ok=True)


def make_altar():
    img = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)

    def slab(box, fill):
        d.rectangle(box, fill=fill, outline=OUTLINE)

    # ── 돌 제단 (아래에서 위로) ─────────────────────────────
    # 받침 슬래브(가장 넓음)
    slab([4, 40, 27, 46], STONE_M)
    d.rectangle([5, 40, 26, 41], fill=STONE_L)   # 윗면 하이라이트
    d.rectangle([5, 45, 26, 45], fill=STONE_D)   # 밑면 그림자

    # 기둥
    slab([9, 27, 22, 40], STONE_M)
    d.rectangle([10, 28, 12, 39], fill=STONE_L)  # 좌측 하이라이트
    d.rectangle([19, 28, 21, 39], fill=STONE_D)  # 우측 그림자

    # 상단 슬래브(제물 안치면)
    slab([6, 23, 25, 28], STONE_M)
    d.rectangle([7, 23, 24, 24], fill=STONE_L)

    # ── 발광 오브 ──────────────────────────────────────────
    cx, cy = 16, 13
    # 외곽 글로우(반투명 2겹)
    for r, a in ((11, 55), (9, 95)):
        d.ellipse([cx - r, cy - r, cx + r, cy + r], fill=(255, 200, 90, a))
    # 오브 본체
    d.ellipse([cx - 7, cy - 7, cx + 7, cy + 7], fill=GOLD, outline=OUTLINE)
    # 코어 하이라이트(좌상 치우침)
    d.ellipse([cx - 5, cy - 5, cx + 2, cy + 2], fill=CORE)
    # 하단 그림자
    d.ellipse([cx - 1, cy + 2, cx + 6, cy + 6], fill=DEEP)
    # 반짝임
    d.point([(cx - 4, cy - 4)], fill=(255, 255, 255, 255))

    return img


def main():
    ensure_dir(OUTPUT_DIR)
    out_path = os.path.join(OUTPUT_DIR, "form_altar.png")
    make_altar().save(out_path, "PNG")
    print(f"  [form_altar] {W}x{H}px -> {out_path}")


if __name__ == "__main__":
    main()
