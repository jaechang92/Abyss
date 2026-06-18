"""
generate_skill_icons.py
Abyss 프로젝트 Active 스킬 3종 아이콘(픽셀 아트) 절차적 생성기.
Pillow(PIL) 필요: pip install Pillow

출력 폴더: Assets/Art/Sprites/SkillIcons/{key}.png
실행 위치: D:/JaeChang/Abyss (프로젝트 루트) 또는 어디서나 (경로는 __file__ 기준)

키 매핑(ContentBuilder.WireSkillIcons와 일치):
  fireball   - 주황 화염구 (Projectile)
  flame_roar - 적색 화염 폭발/포효 (MeleeArea 광역)
  swift_slash- 청록 베기 궤적 (MeleeArea 전방)
"""

import os
from PIL import Image

OUTPUT_DIR = os.path.join(os.path.dirname(__file__), "..", "..", "Assets", "Art", "Sprites", "SkillIcons")
OUTPUT_DIR = os.path.normpath(OUTPUT_DIR)

SIZE = 32  # 모든 아이콘 32x32


def ensure_dir(path):
    os.makedirs(path, exist_ok=True)


def draw_pixels(size, matrix, palette):
    """matrix: 문자열 리스트(각 문자 1픽셀). palette: 문자 -> (R,G,B,A)."""
    w, h = size
    img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    matrix = [row.ljust(w, ".")[:w] for row in matrix]
    for row_idx, row in enumerate(matrix):
        if row_idx >= h:
            break
        for col_idx, ch in enumerate(row):
            color = palette.get(ch, (0, 0, 0, 0))
            img.putpixel((col_idx, row_idx), color)
    return img


# -----------------------------------------------------------------------
# 공통 색상
# -----------------------------------------------------------------------
TRANSPARENT = (0, 0, 0, 0)
OUTLINE     = (26, 26, 26, 255)   # #1A1A1A


# -----------------------------------------------------------------------
# fireball (32x32) - 주황 화염구 (꼬리가 왼쪽, 진행 오른쪽)
# -----------------------------------------------------------------------
def make_fireball():
    CORE   = (255, 244, 180, 255)  # 밝은 노랑 코어
    MID    = (255, 165, 40,  255)  # 주황
    DEEP   = (220, 90,  20,  255)  # 진한 주황
    TAIL   = (255, 120, 30,  180)  # 반투명 꼬리

    P = {".": TRANSPARENT, "O": OUTLINE, "C": CORE, "M": MID, "D": DEEP, "T": TAIL}

    matrix = [
        "................................",  # 0
        "................................",  # 1
        "...........OOOOOO...............",  # 2
        ".........OODDDDDDOO.............",  # 3
        "........ODDDMMMMMDDO............",  # 4
        ".T.....ODDMMMMMMMMMDO...........",  # 5
        "..T...ODMMMMCCCMMMMMDO..........",  # 6
        "...TT.ODMMMCCCCCMMMMDO..........",  # 7
        "..TTToDMMMCCCCCCCMMMMDO.........",  # 8
        ".TTT.ODMMMCCCCCCCMMMMDO.........",  # 9
        "TTTT.ODMMMMCCCCCMMMMMDO.........",  # 10
        ".TTT.ODMMMMMCCCMMMMMMDO.........",  # 11
        "..TTToDMMMMMMMMMMMMMDO..........",  # 12
        "...TT.ODDMMMMMMMMMDDO...........",  # 13
        ".T.....ODDDMMMMMDDDO............",  # 14
        "..T.....OODDDDDDDOO.............",  # 15
        "...........OOOOOO...............",  # 16
        "................................",  # 17
    ]
    return draw_pixels((SIZE, SIZE), matrix, P)


# -----------------------------------------------------------------------
# flame_roar (32x32) - 적색 화염 폭발/포효 (사방 분출)
# -----------------------------------------------------------------------
def make_flame_roar():
    CORE   = (255, 230, 150, 255)  # 노랑 코어
    MID    = (255, 90,  40,  255)  # 적주황
    DEEP   = (200, 40,  20,  255)  # 적색
    SPARK  = (255, 150, 60,  220)  # 불티

    P = {".": TRANSPARENT, "O": OUTLINE, "C": CORE, "M": MID, "D": DEEP, "S": SPARK}

    matrix = [
        "................................",  # 0
        "...S......S.....S......S........",  # 1
        "....S....SDS...SDS....S.........",  # 2
        ".....S..ODDDO.ODDDO..S..........",  # 3
        "...S..OODDMMDOODMMDOO..S........",  # 4
        "....OODDMMMMMDDMMMMMDDOO........",  # 5
        "...ODDMMMMMMMMMMMMMMMMMDO.......",  # 6
        "..ODMMMMMCCCMMMMCCCMMMMMDO......",  # 7
        ".ODMMMMMCCCCCMMCCCCCMMMMMDO.....",  # 8
        ".ODMMMMMMCCCMMMMCCCMMMMMMDO.....",  # 9
        "SODMMMMMMMMMMCCMMMMMMMMMMDOS....",  # 10
        ".ODMMMMMCCCMMMMMMCCCMMMMMDO.....",  # 11
        ".ODMMMCCCCCMMMMMMCCCCCMMMDO.....",  # 12
        "..ODMMMCCCMMMMMMMMCCCMMMDO......",  # 13
        "...ODDMMMMMMMMMMMMMMMMDDO.......",  # 14
        "....OODDMMMMMMMMMMMMDDOO........",  # 15
        "...S..OODDDMMMMMMDDDOO..S.......",  # 16
        "....S...OODDDDDDDDOO...S........",  # 17
        ".....S....OOOOOOOO...S..........",  # 18
        "...S...S.....SS....S...S........",  # 19
    ]
    return draw_pixels((SIZE, SIZE), matrix, P)


# -----------------------------------------------------------------------
# swift_slash (32x32) - 청록 베기 궤적 (좌상 -> 우하 대각 슬래시)
# -----------------------------------------------------------------------
def make_swift_slash():
    EDGE   = (220, 250, 255, 255)  # 흰빛 칼날
    MID    = (90,  200, 245, 255)  # 청록
    DEEP   = (40,  120, 200, 255)  # 진한 파랑
    GLOW   = (140, 230, 255, 170)  # 반투명 잔광

    P = {".": TRANSPARENT, "O": OUTLINE, "E": EDGE, "M": MID, "D": DEEP, "G": GLOW}

    matrix = [
        "................................",  # 0
        ".....................OOO........",  # 1
        "...................OOEEEO.......",  # 2
        "..................OEEEMDO.......",  # 3
        ".................OEEMMDO........",  # 4
        "...............G.OEMMDO.........",  # 5
        "..............GG.OEMMDO.........",  # 6
        ".............GGG.OEMDO..........",  # 7
        "............GGG.OEMMDO..........",  # 8
        "...........GGG.OEMMDO...........",  # 9
        "..........GGG.OEMMDO............",  # 10
        ".........GGG.OEMMDO.............",  # 11
        "........GGG.OEMMDO..............",  # 12
        ".......GGG.OEMMDO...............",  # 13
        "......GGG.OEMMDO................",  # 14
        ".....GGG.OEMMDO.................",  # 15
        "....GGG.OEMDO...................",  # 16
        "...GGG.OEMDO....................",  # 17
        "..GGG.OEMDOO...................",  # 18
        "..GG.OEMDO.....................",  # 19
        "..G.OEMDO......................",  # 20
        "...OEMDO.......................",  # 21
        "...OMDO........................",  # 22
        "....OO.........................",  # 23
    ]
    return draw_pixels((SIZE, SIZE), matrix, P)


# -----------------------------------------------------------------------
# 메인
# -----------------------------------------------------------------------
def main():
    ensure_dir(OUTPUT_DIR)

    icons = [
        ("fireball",   make_fireball),
        ("flame_roar", make_flame_roar),
        ("swift_slash", make_swift_slash),
    ]

    generated = []
    for key, make_fn in icons:
        img = make_fn()
        out_path = os.path.join(OUTPUT_DIR, f"{key}.png")
        img.save(out_path, "PNG")
        generated.append(out_path)
        print(f"  [{key}] {SIZE}x{SIZE}px -> {out_path}")

    print(f"\nGenerated {len(generated)} skill icons:")
    for p in generated:
        print(f"  {p}")


if __name__ == "__main__":
    main()
