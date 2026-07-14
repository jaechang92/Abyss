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
  void_volley- 보랏빛 부채꼴 3화살 (Projectile 다발)
"""

import os
import math
from PIL import Image, ImageDraw

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
# battle_cry (32x32) - 전장의 함성(공격력 버프): 금색 상승 더블 셰브론(강화 화살표)
# -----------------------------------------------------------------------
def make_battle_cry():
    EDGE = (255, 240, 170, 255)  # 밝은 금빛 테두리
    GOLD = (245, 195, 60,  255)  # 금색
    DEEP = (200, 140, 30,  255)  # 진한 금색
    GLOW = (255, 220, 110, 150)  # 반투명 광채

    P = {".": TRANSPARENT, "O": OUTLINE, "E": EDGE, "G": GOLD, "D": DEEP, "g": GLOW}

    # 위로 향하는 셰브론 2개(강화 상승). 위가 밝고 아래가 진함.
    matrix = [
        "................................",  # 0
        "..............OO................",  # 1
        ".............OEEO...............",  # 2
        "............OEGGEO..............",  # 3
        "...........OEGGGGEO.............",  # 4
        "..........OEGGDDGGEO............",  # 5
        ".........OEGGDOODGGEO...........",  # 6
        "........OEGGDO..ODGGEO..........",  # 7
        ".......OEGGDO....ODGGEO.........",  # 8
        "......OEGGDO......ODGGEO........",  # 9
        ".....OOGGDO........ODGGOO.......",  # 10
        "......OODO..........ODOO........",  # 11
        "................................",  # 12  (셰브론 사이 간격)
        "..............OO................",  # 13
        ".............OEEO...............",  # 14
        "............OEGGEO..............",  # 15
        "...........OEGGGGEO.............",  # 16
        "..........OEGGDDGGEO............",  # 17
        ".........OEGGDOODGGEO...........",  # 18
        "........OEGGDO..ODGGEO..........",  # 19
        ".......OEGGDO....ODGGEO.........",  # 20
        "......OEGGDO......ODGGEO........",  # 21
        ".....OOGGDO........ODGGOO.......",  # 22
        "......OODO..........ODOO........",  # 23
    ]
    return draw_pixels((SIZE, SIZE), matrix, P)


# -----------------------------------------------------------------------
# void_volley (32x32) - 보랏빛 부채꼴 3화살 (Projectile 다발)
#   ImageDraw로 좌측 원점에서 -27/0/+27도 방향 화살 3발을 그린다.
# -----------------------------------------------------------------------
def make_void_volley():
    EDGE = (224, 208, 255, 255)  # 밝은 보라-흰 하이라이트
    MID  = (150, 110, 240, 255)  # 보라
    DEEP = (92,  52,  172, 255)  # 진보라(미사용 예비)

    img = Image.new("RGBA", (SIZE, SIZE), TRANSPARENT)
    d = ImageDraw.Draw(img)

    origin = (5, 16)
    length = 21
    head = 5
    for ang in (-27, 0, 27):
        r = math.radians(ang)
        dx, dy = math.cos(r), math.sin(r)
        ex, ey = origin[0] + dx * length, origin[1] + dy * length
        # 자루: 외곽선(굵게) 위에 보라 심
        d.line([origin, (ex, ey)], fill=OUTLINE, width=4)
        d.line([origin, (ex, ey)], fill=MID, width=2)
        # 화살촉: 진행 방향 삼각형
        tip = (ex + dx * head, ey + dy * head)
        px, py = -dy, dx
        left = (ex + px * head * 0.9, ey + py * head * 0.9)
        right = (ex - px * head * 0.9, ey - py * head * 0.9)
        d.polygon([tip, left, right], fill=MID, outline=OUTLINE)
        # 촉 끝 하이라이트
        d.point([(int(ex + dx * 2), int(ey + dy * 2))], fill=EDGE)
    return img


# -----------------------------------------------------------------------
# 메인
# -----------------------------------------------------------------------
# -----------------------------------------------------------------------
# 방패 실루엣 공통 스펙 (heater shield). (left_pad, body) → draw_pixels가 우측·높이 패딩.
# 상단이 가장 넓고 하단으로 뾰족하게 좁아진다. 미스카운트 방지를 위해 좌패딩+본문만 지정.
# -----------------------------------------------------------------------
def _shield_rows():
    return [
        (8, "OOOOOOOOOOOOOOOO"),  # 3 상단 테두리
        (8, "OLLLLLLLLLLLLLLO"),  # 4 상단 하이라이트
        (8, "OLMMMMMMMMMMMMDO"),  # 5
        (8, "OLMMMMMMMMMMMMDO"),  # 6
        (8, "OLMMMMMCCMMMMMDO"),  # 7 중앙 스터드
        (8, "OLMMMMMCCMMMMMDO"),  # 8 중앙 스터드
        (8, "OLMMMMMMMMMMMMDO"),  # 9
        (8, "OLMMMMMMMMMMMMDO"),  # 10
        (9, "OLMMMMMMMMMMDO"),    # 11 테이퍼
        (10, "OLMMMMMMMMDO"),     # 12
        (11, "OLMMMMMMDO"),       # 13
        (12, "OLMMMMDO"),         # 14
        (13, "OLMMDO"),           # 15
        (14, "OMMO"),             # 16
        (15, "OO"),               # 17 하단 꼭짓점
    ]


def _make_shield(steel_l, steel_m, steel_d, stud):
    P = {".": TRANSPARENT, "O": OUTLINE, "L": steel_l, "M": steel_m, "D": steel_d, "C": stud}
    matrix = ["", "", ""] + ["." * left + body for left, body in _shield_rows()]
    return draw_pixels((SIZE, SIZE), matrix, P)


# shield_bash (32x32) - 강철 방패 + 황동 스터드 (근접 광역 강타)
def make_shield_bash():
    return _make_shield(
        steel_l=(205, 215, 230, 255),
        steel_m=(150, 165, 195, 255),
        steel_d=(95, 110, 145, 255),
        stud=(235, 205, 120, 255),  # 황동 중앙
    )


# iron_guard (32x32) - 청강 방패 + 청록 젬 (방어 버프)
def make_iron_guard():
    return _make_shield(
        steel_l=(190, 220, 240, 255),
        steel_m=(120, 160, 205, 255),
        steel_d=(70, 100, 150, 255),
        stud=(120, 235, 220, 255),  # 청록 젬
    )


def main():
    ensure_dir(OUTPUT_DIR)

    icons = [
        ("fireball",   make_fireball),
        ("flame_roar", make_flame_roar),
        ("swift_slash", make_swift_slash),
        ("battle_cry", make_battle_cry),
        ("void_volley", make_void_volley),
        ("shield_bash", make_shield_bash),
        ("iron_guard", make_iron_guard),
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
