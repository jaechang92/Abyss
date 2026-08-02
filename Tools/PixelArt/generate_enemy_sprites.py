"""
generate_enemy_sprites.py
Abyss 프로젝트 적 도트(픽셀 아트) 스프라이트 절차적 생성기.
Pillow(PIL) 필요: pip install Pillow

출력 폴더: Assets/Art/Sprites/Enemies/{enemyId}.png
실행 위치: D:/JaeChang/Abyss (프로젝트 루트)
"""

import os
from PIL import Image

OUTPUT_DIR = os.path.join(os.path.dirname(__file__), "..", "..", "Assets", "Art", "Sprites", "Enemies")
OUTPUT_DIR = os.path.normpath(OUTPUT_DIR)


def ensure_dir(path):
    os.makedirs(path, exist_ok=True)


def draw_pixels(size, matrix, palette):
    """
    matrix: 문자열 리스트, 각 문자 1픽셀.
    palette: 문자 -> (R, G, B, A) 딕셔너리.
    크기 불일치 시 matrix 기준으로 잘라냄.
    """
    w, h = size
    img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    for row_idx, row in enumerate(matrix):
        if row_idx >= h:
            break
        for col_idx, ch in enumerate(row):
            if col_idx >= w:
                break
            color = palette.get(ch, (0, 0, 0, 0))
            img.putpixel((col_idx, row_idx), color)
    return img


# -----------------------------------------------------------------------
# 공통 색상 상수
# -----------------------------------------------------------------------
TRANSPARENT = (0, 0, 0, 0)
OUTLINE     = (26, 26, 26, 255)   # #1A1A1A
WHITE_EYE   = (240, 240, 240, 255) # #F0F0F0


# -----------------------------------------------------------------------
# melee_grunt (24x24) - 좀비형 근접 병사
# -----------------------------------------------------------------------
def make_melee_grunt():
    BODY_GREEN  = (107, 142, 90, 255)  # #6B8E5A
    CLOTH_BROWN = (139, 111, 71, 255)  # #8B6F47
    EYE_BLACK   = (26,  26,  26, 255)  # #1A1A1A

    P = {
        ".": TRANSPARENT,
        "O": OUTLINE,
        "G": BODY_GREEN,
        "B": CLOTH_BROWN,
        "E": EYE_BLACK,
        "W": WHITE_EYE,
    }

    # 24 cols x 24 rows
    matrix = [
        "........................",  # 0
        "........OOOOOOOO........",  # 1
        ".......OGGGGGGGO.......",   # 2  <- 23 chars → pad
        "......OGGGGGGGGGO......",   # 3
        "......OGEGGGGEGGO......",   # 4  눈
        "......OGGGGGGGGGO......",   # 5
        "......OGGGGGGGGGO......",   # 6
        ".......OOOOOOOO........",   # 7
        "......OBBBBBBBBO.......",   # 8  몸통 시작
        ".....OBBBBBBBBBO.......",   # 9  (좌팔)
        "....OBBBBBBBBBBBO......",   # 10
        "......OBBBBBBBBO.......",   # 11
        "......OBBBBBBBBO.......",   # 12
        ".......OBBBBBBO........",   # 13
        "......OBBOOBBBO........",   # 14  다리 분기
        ".....OBBOO.OBBOO.......",   # 15
        "....OBBOO...OBBOO......",   # 16
        "...OBBOO.....OBBOO.....",   # 17
        "..OBBOO.......OBBOO....",   # 18
        "............................",  # 19
        "............................",  # 20
        "............................",  # 21
        "............................",  # 22
        "............................",  # 23
    ]

    # 각 행을 정확히 24글자로 맞춤 (짧으면 '.' 패딩)
    matrix = [row.ljust(24, ".")[:24] for row in matrix]
    return draw_pixels((24, 24), matrix, P)


# -----------------------------------------------------------------------
# melee_brute (32x32) - 거인형 강타자
# -----------------------------------------------------------------------
def make_melee_brute():
    RUST       = (139, 69,  19, 255)  # #8B4513
    DARK_BROWN = (93,  46,  12, 255)  # #5D2E0C
    EYE_YELLOW = (244, 208, 63, 255)  # #F4D03F

    P = {
        ".": TRANSPARENT,
        "O": OUTLINE,
        "R": RUST,
        "D": DARK_BROWN,
        "Y": EYE_YELLOW,
    }

    # 32 cols x 32 rows — 어깨 넓고 몸통 굵음
    matrix = [
        "................................",  # 0
        "........OOOOOOOOOOOO............",  # 1
        ".......ORRRRRRRRRRRO............",  # 2
        "......ORRRRRRRRRRRRO............",  # 3
        "......ORRYRRRRRYRRO.............",  # 4  눈
        "......ORRRRRRRRRRRRO............",  # 5
        "......ORRRRRRRRRRRRRO...........",  # 6
        ".......OOOOOOOOOOOOO............",  # 7
        "...OOOORRDDDDDDDDRROOOO.........",  # 8  어깨
        "..ORRRRRRDDDDDDDDRRRRRO.........",  # 9
        ".ORRRRRRRRDDDDDDDDRRRRRO........",  # 10
        "..ORRRRRRDDDDDDDDRRRRRO.........",  # 11
        "...OOOOORRRRRRRRRROOOOO.........",  # 12
        ".......ORRRRRRRRRRO.............",  # 13
        ".......ORRRRRRRRRRO.............",  # 14
        ".......ORRRRRRRRRRO.............",  # 15
        ".......ORRRRRRRRRRO.............",  # 16
        "........OOOOOOOOOOO.............",  # 17
        ".......ORROO.ORROO..............",  # 18  다리
        "......ORROO...ORROO.............",  # 19
        ".....ORROO.....ORROO............",  # 20
        "....ORROO.......ORROO...........",  # 21
        "...ORROO.........ORROO..........",  # 22
        "................................",  # 23~31
        "................................",
        "................................",
        "................................",
        "................................",
        "................................",
        "................................",
        "................................",
        "................................",
    ]

    matrix = [row.ljust(32, ".")[:32] for row in matrix]
    return draw_pixels((32, 32), matrix, P)


# -----------------------------------------------------------------------
# ranged_archer (24x24) - 후드 원거리 사수
# -----------------------------------------------------------------------
def make_ranged_archer():
    PURPLE      = (106, 61,  154, 255)  # #6A3D9A
    DARK_PURPLE = (61,  31,  92,  255)  # #3D1F5C
    EYE_RED     = (231, 76,  60,  255)  # #E74C3C

    P = {
        ".": TRANSPARENT,
        "O": OUTLINE,
        "P": PURPLE,
        "D": DARK_PURPLE,
        "R": EYE_RED,
        "W": WHITE_EYE,
    }

    # 24x24 — 후드 전체 덮음, 얼굴 좁게 노출
    matrix = [
        "........................",
        ".......OOOOOOOO.........",
        "......ODDDDDDDDO........",
        ".....ODDDDDDDDDDO.......",
        "....ODDDDWWRWWDDDO......",  # 눈: W = 흰자, R = 빨간 눈
        "....ODDDDWWRWWDDDO......",
        "....ODDDDDDDDDDDO.......",
        ".....ODDDDDDDDDO........",
        "....ODDDDDDDDDDDO.......",
        "...ODDPPPPPPPPPPDO......",  # 망토 몸통
        "..ODDPPPPPPPPPPPPDO.....",
        ".ODDPPPPPPPPPPPPPPDO....",
        "..ODDPPPPPPPPPPPPDO.....",
        "...ODDPPPPPPPPPPDO......",
        "....ODDPPPPPPPDO........",
        "....ODDPPPPPPPDO........",
        ".....ODDPOOPPPDO........",  # 다리
        "....ODDPOO.OPPPDO.......",
        "...ODDPOO...OPPPDO......",
        "..ODDPOO.....OPPPDO.....",
        "........................",
        "........................",
        "........................",
        "........................",
    ]

    matrix = [row.ljust(24, ".")[:24] for row in matrix]
    return draw_pixels((24, 24), matrix, P)


# -----------------------------------------------------------------------
# elite_hunter (32x32) - 엘리트 갑옷 헌터
# -----------------------------------------------------------------------
def make_elite_hunter():
    METAL  = (93,  109, 126, 255)  # #5D6D7E
    GOLD   = (241, 196, 15,  255)  # #F1C40F
    EYE_R  = (231, 76,  60,  255)  # #E74C3C

    P = {
        ".": TRANSPARENT,
        "O": OUTLINE,
        "M": METAL,
        "G": GOLD,
        "R": EYE_R,
    }

    # 32x32 — 헬멧 + 갑옷 몸통 + 금색 어깨·가슴 장식
    matrix = [
        "................................",
        ".........OOOOOOOOOO.............",
        "........OMMMMMMMMMMO............",
        "........OMMMMMMMMMMO............",
        "........OMMRMMMRMMO.............",  # 눈
        "........OMMMMMMMMMMO............",
        "........OGGGGGGGGGGGO...........",  # 금색 어깨 장식
        ".........OMMMMMMMMO.............",
        "......OGGGGMMMMMMMGGGGGO........",  # 금색 어깨 넓게
        ".....OGGGMMMMMMMMMMGGGGO........",
        "......OGGGGMMMMMMMGGGGGO........",
        ".......OGGMMMMMMMMMGGO..........",
        ".......OMMMMMMMMMMMMO...........",  # 갑옷 몸통
        ".......OMGMMMMMMMGMO............",  # 금색 가슴 장식
        ".......OMMMMMMMMMMO.............",
        ".......OMMMMMMMMMMO.............",
        ".......OMMMMMMMMMMO.............",
        "........OOOOOOOOOOO.............",
        ".......OMMOO.OMMOO..............",  # 강철 다리
        "......OMMOO...OMMOO.............",
        ".....OMMOO.....OMMOO............",
        "....OMMOO.......OMMOO...........",
        "...OMMOO.........OMMOO..........",
        "................................",
        "................................",
        "................................",
        "................................",
        "................................",
        "................................",
        "................................",
        "................................",
        "................................",
    ]

    matrix = [row.ljust(32, ".")[:32] for row in matrix]
    return draw_pixels((32, 32), matrix, P)


# -----------------------------------------------------------------------
# boss_abyss_keeper (48x48) - 심연의 수문장 보스
# -----------------------------------------------------------------------
def make_boss_abyss_keeper():
    DARK_CLOAK  = (26,  26,  46,  255)  # #1A1A2E
    PURPLE_CORE = (142, 68,  173, 255)  # #8E44AD
    GLOW        = (187, 143, 206, 255)  # #BB8FCE
    EYE_RED     = (231, 76,  60,  255)  # #E74C3C

    P = {
        ".": TRANSPARENT,
        "O": OUTLINE,
        "C": DARK_CLOAK,
        "P": PURPLE_CORE,
        "G": GLOW,
        "R": EYE_RED,
    }

    # 48x48 — 거대 망토 전체, 중앙 보라 코어, 가시 어깨
    matrix = [
        "................................................",  # 0
        "................................................",  # 1
        ".................OOOOOOOOOOOO...................",  # 2
        "................OCCCCCCCCCCCCO..................",  # 3
        "...............OCCCCCCCCCCCCCCO.................",  # 4
        "...............OCCCRRCCCCRRCCCO.................",  # 5  눈
        "...............OCCCCCCCCCCCCCCO.................",  # 6
        "...............OCCCCCCCCCCCCCCO.................",  # 7
        "................OOOOOOOOOOOOOO..................",  # 8
        "...OOOO.........OCCCCCCCCCCCCO........OOOO...",   # 9  가시 어깨
        "..OCCCO.........OCCCCCCCCCCCCO.........OCCCO.",   # 10
        ".OCCCCO........OCCCCCCCCCCCCCCO........OCCCCO",  # 11
        "..OCCCO.......OCCCCCCCCCCCCCCCCO......OCCCO..",  # 12
        "...OOOO......OCCCCCPPPPPPPCCCCCCO....OOOO...",   # 13  보라 코어 등장
        "............OCCCCCPGGGGGGGPCCCCCCO..........",   # 14
        "...........OCCCCCPGGGGGGGGGPCCCCCCO.........",   # 15
        "..........OCCCCCPGGGGGGGGGGGPCCCCCC O.......",   # 16
        "...........OCCCCCPGGGGGGGGGPCCCCCCO.........",   # 17
        "............OCCCCCPPPPPPPPPCCCCCCO..........",   # 18
        "..............OCCCCCCCCCCCCCCCCO............",   # 19
        "...............OCCCCCCCCCCCCCCO.............",   # 20
        "................OOOOOOOOOOOOOO..............",   # 21
        "...............OCCCCCCCCCCCCO...............",   # 22
        "...............OCCCCCCCCCCCCO...............",   # 23
        "..............OCCCCCCCCCCCCCCO..............",   # 24
        "..............OCCCCCCCCCCCCCCO..............",   # 25
        ".............OCCCCCCCCCCCCCCCCO.............",   # 26
        ".............OCCCCCCCCCCCCCCCCO.............",   # 27
        "............OCCCCCCCCCCCCCCCCCCO............",   # 28
        "...........OCCCCCCCCCCCCCCCCCCCCO...........",   # 29
        "..........OCCCCCCCCCCCCCCCCCCCCCCO..........",   # 30
        ".........OCCCCCCOOOOOCCCCCCOOOOCCCCO........",   # 31  다리 분기
        "........OCCCCCOO.....OCCCCO.....OOCCO.......",   # 32
        ".......OCCCCCOO.......OCCCCO.....OOCCO......",   # 33
        "......OCCCCCOO.........OCCCCO.....OOCCO.....",   # 34
        ".....OCCCCCOO...........OCCCCO.....OOCCO....",   # 35
        "................................................",
        "................................................",
        "................................................",
        "................................................",
        "................................................",
        "................................................",
        "................................................",
        "................................................",
        "................................................",
        "................................................",
        "................................................",
        "................................................",
    ]

    matrix = [row.ljust(48, ".")[:48] for row in matrix]
    return draw_pixels((48, 48), matrix, P)


# -----------------------------------------------------------------------
# midboss_throne_warden (32x32) - 왕좌의 파수관 (Stage 3 중간보스)
# -----------------------------------------------------------------------
def make_midboss_throne_warden():
    STEEL  = (108, 122, 137, 255)  # #6C7A89
    ROYAL  = (52,  73,  126, 255)  # #34497E  왕실 청색 망토
    GOLD   = (212, 175, 55,  255)  # #D4AF37
    EYE_B  = (120, 200, 255, 255)  # #78C8FF  투구 틈의 푸른 빛

    P = {
        ".": TRANSPARENT,
        "O": OUTLINE,
        "S": STEEL,
        "B": ROYAL,
        "G": GOLD,
        "E": EYE_B,
    }

    # 32x32 - 긴 투구 + 왕실 망토 + 금장 견갑. 얼굴은 보이지 않는다.
    matrix = [
        "................................",
        "..............OGO...............",
        ".............OGGGO..............",
        "............OSSSSSO.............",
        "...........OSSSSSSSO............",
        "...........OSEESSEESO...........",
        "...........OSSSSSSSSO...........",
        "............OSSSSSSO............",
        "...........OGGGGGGGGO...........",
        "........OGGGBBBBBBBBGGGO........",
        ".......OGGGBBBBBBBBBBGGGO.......",
        "........OGGBBBBBBBBBBGGO........",
        ".........OBBBBSSSSBBBBO.........",
        ".........OBBBSGGGGSBBBO.........",
        ".........OBBBSSSSSSBBBO.........",
        ".........OBBBBSSSSBBBBO.........",
        "..........OBBBBBBBBBBO..........",
        "..........OBBBBBBBBBBO..........",
        "...........OBBBBBBBBO...........",
        "...........OOOOOOOOOO...........",
        "..........OSSOO..OOSSO..........",
        ".........OSSOO....OOSSO.........",
        "........OSSOO......OOSSO........",
        ".......OSSOO........OOSSO.......",
        "................................",
        "................................",
        "................................",
        "................................",
        "................................",
        "................................",
        "................................",
        "................................",
    ]

    matrix = [row.ljust(32, ".")[:32] for row in matrix]
    return draw_pixels((32, 32), matrix, P)


# -----------------------------------------------------------------------
# boss_thronebound (48x48) - 왕좌의 영혼 (Stage 3 최종보스)
# -----------------------------------------------------------------------
def make_boss_thronebound():
    CROWN    = (212, 175, 55,  255)  # #D4AF37  금관
    CROWN_L  = (245, 220, 130, 255)  # #F5DC82  금관 하이라이트
    SPIRIT   = (86,  70,  130, 255)  # #564682  영체(짙은 보라)
    SPIRIT_L = (140, 120, 190, 255)  # #8C78BE  영체(옅은 보라)
    WISP     = (170, 150, 220, 160)  # 반투명 - 바닥에 닿지 않는 유령 자락
    EYE_G    = (255, 236, 150, 255)  # #FFEC96  금빛 눈

    P = {
        ".": TRANSPARENT,
        "O": OUTLINE,
        "C": CROWN,
        "H": CROWN_L,
        "S": SPIRIT,
        "L": SPIRIT_L,
        "W": WISP,
        "E": EYE_G,
    }

    # 48x48 - 금관 + 영체 상반신 + 아래로 갈수록 흩어지는 자락. 다리가 없다.
    matrix = [
        "................................................",
        "................................................",
        "..............OC....OC....OC....................",
        ".............OCHO..OCHO..OCHO...................",
        ".............OCHCOOCHCOOCHCO....................",
        "............OCCCCCCCCCCCCCCCO...................",
        "............OCHHHHHHHHHHHHHCO...................",
        "............OCCCCCCCCCCCCCCCO...................",
        ".............OSSSSSSSSSSSSSO....................",
        "............OSSSSSSSSSSSSSSSO...................",
        "............OSSEEESSSSEEESSSO...................",
        "............OSSEEESSSSEEESSSO...................",
        "............OSSSSSSSSSSSSSSSO...................",
        ".............OSSSSSSSSSSSSSO....................",
        "..............OSSSSSSSSSSSO.....................",
        "...............OSSSSSSSSSO......................",
        "........OCCO...OSSSSSSSSSO...OCCO...............",
        ".......OCHHCO.OSSSSSSSSSSSO.OCHHCO..............",
        "......OCHHHHCOSSSSLLLLLSSSSOCHHHHCO.............",
        ".......OCHHCOSSSSLLLLLLLSSSSOCHHCO..............",
        "........OCCOSSSSLLLLLLLLLSSSSOCCO...............",
        "..........OSSSSLLLLLLLLLLLSSSSO.................",
        "..........OSSSSLLLLLLLLLLLSSSSO.................",
        "..........OSSSSSLLLLLLLLLSSSSSO.................",
        "...........OSSSSSLLLLLLLSSSSSO..................",
        "...........OSSSSSSLLLLLSSSSSSO..................",
        "............OSSSSSSSSSSSSSSSO...................",
        "............OSSSSSSSSSSSSSSSO...................",
        ".............OSSSSSSSSSSSSSO....................",
        ".............OSSSSSSSSSSSSSO....................",
        "..............OSSSSSSSSSSSO.....................",
        "..............OWSSSSSSSSSWO.....................",
        "...............OWSSSSSSSWO......................",
        "...............OWWSSSSSWWO......................",
        "................OWWSSSWWO.......................",
        "................OWWWSWWWO.......................",
        ".................OWWWWWO........................",
        ".................OW.W.WO........................",
        "..................W...W.........................",
        "................................................",
        "................................................",
        "................................................",
        "................................................",
        "................................................",
        "................................................",
        "................................................",
        "................................................",
        "................................................",
    ]

    matrix = [row.ljust(48, ".")[:48] for row in matrix]
    return draw_pixels((48, 48), matrix, P)


# -----------------------------------------------------------------------
# 메인
# -----------------------------------------------------------------------
def main():
    ensure_dir(OUTPUT_DIR)

    enemies = [
        ("melee_grunt",       (24, 24), make_melee_grunt),
        ("melee_brute",       (32, 32), make_melee_brute),
        ("ranged_archer",     (24, 24), make_ranged_archer),
        ("elite_hunter",      (32, 32), make_elite_hunter),
        ("boss_abyss_keeper", (48, 48), make_boss_abyss_keeper),
        ("midboss_throne_warden", (32, 32), make_midboss_throne_warden),
        ("boss_thronebound", (48, 48), make_boss_thronebound),
    ]

    generated = []
    for enemy_id, size, make_fn in enemies:
        img = make_fn()
        out_path = os.path.join(OUTPUT_DIR, f"{enemy_id}.png")
        img.save(out_path, "PNG")
        generated.append(out_path)
        print(f"  [{enemy_id}] {size[0]}x{size[1]}px -> {out_path}")

    print(f"\nGenerated {len(generated)} enemy sprites:")
    for p in generated:
        print(f"  {p}")


if __name__ == "__main__":
    main()
