# -*- coding: utf-8 -*-
"""
generate_form_sprites_hd.py — 폼 스프라이트 HD판(32x64, PPU 32). 4-1 아트.

기존 16x32판(generate_form_sprites.py)과의 차이는 둘이다:

  1. **픽셀 예산 4배** — 512px -> 2048px. 갑주 이음매·얼굴·장비 디테일이 들어갈 자리가 생긴다.
  2. **재질 맵 + 절차적 조명** — ASCII는 '무엇으로 만들어졌는가'만 적고,
     색은 form_shading이 광원에서 계산한다. 손으로 톤을 찍지 않으므로
     네 폼의 광원이 절대 어긋나지 않는다.

재질 글자
  .  빈칸        K  살(피부)      A  주 갑주/의복    B  보조(가죽·천)
  M  금속(무기·방패)              C  망토             E  발광(강조·마법)

실행: python Tools/PixelArt/generate_form_sprites_hd.py
Pillow 필요.
"""
import os
import sys
from PIL import Image

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import form_sprite_spec as spec
import form_shading as sh

W, H = 32, 64
PPU = 32
OUT_DIR = os.path.normpath(os.path.join(
    os.path.dirname(__file__), "..", "..", "Assets", "Art", "Sprites", "Forms"))

# 재질별 기본색. 폼 팔레트의 base를 주 갑주로 쓰고 나머지는 재질 성격으로 고정한다.
SKIN_BASE = (222, 178, 138)
LEATHER = (92, 66, 52)
STEEL = (172, 180, 196)


def materials(form_id):
    p = spec.PALETTES[form_id]
    return {
        "K": sh.ramp(SKIN_BASE),
        "A": sh.ramp(p["base"][:3]),
        "B": sh.ramp(LEATHER),
        "M": sh.ramp(STEEL),
        "C": sh.ramp(p["shade"][:3]),
        "E": sh.ramp(p["accent"][:3]),
    }


def render(matrix, form_id):
    """재질 맵 -> 조명된 스프라이트."""
    assert len(matrix) == H, "행 %d != %d" % (len(matrix), H)
    for i, row in enumerate(matrix):
        assert len(row) == W, "%d행 폭 %d != %d" % (i, len(row), W)

    mats = materials(form_id)
    mask = [[ch != "." for ch in row] for row in matrix]
    img = Image.new("RGBA", (W, H), (0, 0, 0, 0))

    for y in range(H):
        for x in range(W):
            ch = matrix[y][x]
            if ch == ".":
                continue
            ramp = mats[ch]

            # 발광 재질은 조명을 안 받는다 — 스스로 빛나는 것이라 광원과 무관해야 한다.
            if ch == "E":
                img.putpixel((x, y), ramp["hi"])
                continue

            nx, ny = sh.normal_at(mask, x, y, W, H)
            edge = (nx != 0.0 or ny != 0.0)

            if edge and sh.is_rim(mask, x, y, W, H):
                tone = "light"          # 림라이트 — 광원 반대쪽 가장자리
            elif edge and (nx * sh.LIGHT[0] + ny * sh.LIGHT[1]) < -0.35:
                tone = "line"           # 셀아웃 — 검정이 아니라 색조가 도는 어두운 색
            else:
                tone = sh.shade_tone(nx, ny)

            img.putpixel((x, y), ramp[tone])
    return img


# ══════════════════════════════════════════════════════════════
# 암흑 검사 — 넓은 어깨, 오른손 대검. 세로로 뻗는 선 하나.
# ══════════════════════════════════════════════════════════════
DARK_BLADE = [
    "................................",
    "..............AAAA..............",
    ".............AAAAAA.............",
    "............AAAAAAAA............",
    "............AAAAAAAA............",
    "............AKKKKKKA............",
    "............AKKKKKKA......MM....",
    "............AKKKKKKA......ME....",
    ".............KKKKKK.......ME....",
    "..............KKKK........ME....",
    "..............AAAA........ME....",
    "...........AAAAAAAAAA.....ME....",
    ".........AAAAAAAAAAAAAA...ME....",
    "........AAAAAAAAAAAAAAAA..ME....",
    ".......AAAAAAAAAAAAAAAAA..ME....",
    ".......AAAAAABBAAAAAAAAA..ME....",
    ".......AAAAABBBBAAAAAAAKKKME....",
    ".......AAAAABBBBAAAAAAAKKMME....",
    ".......AAAAABBBBAAAAAAAKMMEE....",
    ".......AAAAAABBAAAAAAAMMMEE.....",
    "........AAAAAAAAAAAAAMME........",
    "........AAAAAAAAAAAAMM..........",
    ".........AAAAAAAAAAM............",
    ".........AAAAAAAAAA.............",
    "..........AAAAAAAA..............",
    "..........AAAAAAAA..............",
    "..........BBBBBBBB..............",
    "..........BBBBBBBB..............",
    "..........BBB..BBB..............",
    ".........BBBB..BBBB.............",
    ".........AAAA..AAAA.............",
    ".........AAAA..AAAA.............",
    ".........AAAA..AAAA.............",
    ".........AAAA..AAAA.............",
    ".........AAAA..AAAA.............",
    ".........AAAA..AAAA.............",
    ".........AAAA..AAAA.............",
    "..........AAA..AAA..............",
    "..........AAA..AAA..............",
    "..........AAA..AAA..............",
    "..........AAA..AAA..............",
    "..........BBB..BBB..............",
    "..........BBB..BBB..............",
    "..........BBB..BBB..............",
    "..........BBB..BBB..............",
    "..........BBB..BBB..............",
    "..........BBB..BBB..............",
    ".........BBBB..BBBB.............",
    ".........BBBB..BBBB.............",
    ".........BBBB..BBBB.............",
    ".........BBBB..BBBB.............",
    "........BBBBB..BBBBB............",
    "........BBBBB..BBBBB............",
    "........MMMMM..MMMMM............",
    "........MMMMM..MMMMM............",
    ".......MMMMMM..MMMMMM...........",
    ".......MMMMMM..MMMMMM...........",
    ".......MMMMMM..MMMMMM...........",
    "......MMMMMMM..MMMMMMM..........",
    "......MMMMMMM..MMMMMMM..........",
    "......MMMMMMMMMMMMMMMM..........",
    "......MMMMMMMMMMMMMMMM..........",
    "................................",
    "................................",
]

# ══════════════════════════════════════════════════════════════
# 공허 궁수 — 가장 마르다. 큰 곡선 활. 유일한 곡선 실루엣.
# ══════════════════════════════════════════════════════════════
VOID_ARCHER = [
    "................................",
    "..............AAAA..............",
    ".............AAAAAA.............",
    "............AAAAAAAA......MM....",
    "............AAAAAAAA.....MM.....",
    "............AKKKKKKA....MM......",
    "............AKKKKKKA...MM.......",
    "............AKKKKKKA..MM........",
    ".............KKKKKK...ME........",
    "..............KKKK....ME........",
    "..............BBBB....ME........",
    "............BBBBBBBB..ME........",
    "...........BBBBBBBBBB.ME........",
    "..........BBBBAAAABBB.ME........",
    "..........BBBAAAAAABB.ME........",
    "..........BBAAAAAAABB.ME........",
    "..........BBAAAAAAABBKME........",
    "..........BBAAAAAAABBKME........",
    "..........BBAAAAAAABBKME........",
    "..........BBBAAAAAABB.ME........",
    "..........BBBBAAAABBB.ME........",
    "...........BBBBBBBBB..ME........",
    "...........BBBBBBBBB..ME........",
    "............BBBBBBB...ME........",
    "............AAAAAAA...ME........",
    "............AAAAAAA..MM.........",
    "............AAAAAAA.MM..........",
    "............AAAAAAAMM...........",
    "............AAAAAAMM............",
    "............AAAAAMM.............",
    "............AAAAAA..............",
    "............BBBBBB..............",
    "...........BBBB.BBB.............",
    "...........BBBB.BBB.............",
    "...........AAAA.AAA.............",
    "...........AAAA.AAA.............",
    "...........AAAA.AAA.............",
    "...........AAAA.AAA.............",
    "...........AAAA.AAA.............",
    "...........AAAA.AAA.............",
    "...........AAAA.AAA.............",
    "...........AAAA.AAA.............",
    "...........AAAA.AAA.............",
    "...........BBBB.BBB.............",
    "...........BBBB.BBB.............",
    "...........BBBB.BBB.............",
    "...........BBBB.BBB.............",
    "...........BBBB.BBB.............",
    "...........BBBB.BBB.............",
    "...........BBBB.BBB.............",
    "..........BBBBB.BBBB............",
    "..........BBBBB.BBBB............",
    "..........BBBBB.BBBB............",
    "..........BBBBB.BBBB............",
    "..........BBBBB.BBBB............",
    "..........BBBBB.BBBB............",
    ".........BBBBBB.BBBBB...........",
    ".........BBBBBB.BBBBB...........",
    ".........BBBBBB.BBBBB...........",
    ".........BBBBBBBBBBBB...........",
    ".........BBBBBBBBBBBB...........",
    ".........BBBBBBBBBBBB...........",
    "................................",
    "................................",
]

# ══════════════════════════════════════════════════════════════
# 고대 방패병 — 가장 넓다. 큰 방패가 몸 절반을 덮는 사각 덩어리.
# ══════════════════════════════════════════════════════════════
ANCIENT_SHIELD = [
    "................................",
    "...........AAAA.................",
    "..........AAAAAA................",
    ".........AAAAAAAA...............",
    ".........AAAAAAAA...............",
    ".........AKKKKKKA...............",
    ".........AKKKKKKA...............",
    ".........AKKKKKKA...............",
    "..........KKKKKK................",
    "...........KKKK.................",
    "...........AAAA.................",
    "........AAAAAAAAAA..............",
    "......AAAAAAAAAAAAAA.MMMMMMMM...",
    ".....AAAAAAAAAAAAAAAAMMMMMMMMM..",
    ".....AAAAAAAAAAAAAAAAMMMEEEMMM..",
    ".....AAAAAABBAAAAAAAAMMEEEEEMM..",
    ".....AAAAABBBBAAAAAAKMMEEEEEMM..",
    ".....AAAAABBBBAAAAAAKMMEEEEEMM..",
    ".....AAAAABBBBAAAAAAKMMEEEEEMM..",
    ".....AAAAAABBAAAAAAAAMMEEEEEMM..",
    "......AAAAAAAAAAAAAAAMMMEEEMMM..",
    "......AAAAAAAAAAAAAAAMMMMMMMMM..",
    ".......AAAAAAAAAAAAA.MMMMMMMM...",
    ".......AAAAAAAAAAAAA............",
    "........AAAAAAAAAAA............."[:32],
    "........AAAAAAAAAAA.............",
    "........BBBBBBBBBBB.............",
    "........BBBBBBBBBBB.............",
    "........BBBB...BBBB.............",
    ".......BBBBB...BBBBB............",
    ".......AAAAA...AAAAA............",
    ".......AAAAA...AAAAA............",
    ".......AAAAA...AAAAA............",
    ".......AAAAA...AAAAA............",
    ".......AAAAA...AAAAA............",
    ".......AAAAA...AAAAA............",
    "........AAAA...AAAA.............",
    "........AAAA...AAAA.............",
    "........AAAA...AAAA.............",
    "........AAAA...AAAA.............",
    "........BBBB...BBBB.............",
    "........BBBB...BBBB.............",
    "........BBBB...BBBB.............",
    "........BBBB...BBBB.............",
    "........BBBB...BBBB.............",
    "........BBBB...BBBB.............",
    ".......BBBBB...BBBBB............",
    ".......BBBBB...BBBBB............",
    ".......BBBBB...BBBBB............",
    ".......BBBBB...BBBBB............",
    "......BBBBBB...BBBBBB...........",
    "......BBBBBB...BBBBBB...........",
    "......MMMMMM...MMMMMM...........",
    "......MMMMMM...MMMMMM...........",
    ".....MMMMMMM...MMMMMMM..........",
    ".....MMMMMMM...MMMMMMM..........",
    ".....MMMMMMM...MMMMMMM..........",
    "....MMMMMMMM...MMMMMMMM.........",
    "....MMMMMMMM...MMMMMMMM.........",
    "....MMMMMMMMMMMMMMMMMMM.........",
    "....MMMMMMMMMMMMMMMMMMM.........",
    "....MMMMMMMMMMMMMMMMMMM.........",
    "................................",
    "................................",
]

# ══════════════════════════════════════════════════════════════
# 심연 투척사 — 망토가 아래로 퍼지는 삼각형. 오른손에 투창.
# ══════════════════════════════════════════════════════════════
VOID_THROWER = [
    "................................",
    "..............AAAA..............",
    ".............AAAAAA.............",
    "............AAAAAAAA...........E",
    "............AAAAAAAA..........EM",
    "............AKKKKKKA.........EM.",
    "............AKKKKKKA........EM..",
    "............AKKKKKKA.......EM...",
    ".............KKKKKK.......EM....",
    "..............KKKK.......EM.....",
    "..............CCCC......EM......",
    "............CCCCCCCC...EM.......",
    "...........CCCCCCCCCCKEM........",
    "..........CCCCAAAACCCKEM........",
    "..........CCCAAAAAACCKM.........",
    "..........CCAAAAAAACC...........",
    "..........CCAAAAAAACC...........",
    "..........CCAAAAAAACC...........",
    "..........CCAAAAAAACC...........",
    ".........CCCAAAAAAACCC..........",
    ".........CCCCAAAACCCCC..........",
    "........CCCCCCCCCCCCCC..........",
    "........CCCCCCCCCCCCCC..........",
    ".......CCCCCCCCCCCCCCCC.........",
    ".......CCCCCCCCCCCCCCCC.........",
    "......CCCCCCCCCCCCCCCCCC........",
    "......CCCCCCCCCCCCCCCCCC........",
    ".....CCCCCCCCCCCCCCCCCCCC.......",
    ".....CCCCCCCCCCCCCCCCCCCC.......",
    "....CCCCCCCCCCCCCCCCCCCCCC......",
    "....CCCCCCCCCCCCCCCCCCCCCC......",
    "...CCCCCCCCCCCCCCCCCCCCCCCC.....",
    "...CCCCCCCCCC....CCCCCCCCCC.....",
    "...CCCCCCCCC......CCCCCCCCC.....",
    "...CCCCCCCC........CCCCCCCC.....",
    "....CCCCCC..........CCCCCC......",
    "...........AAAA.AAA.............",
    "...........AAAA.AAA.............",
    "...........AAAA.AAA.............",
    "...........AAAA.AAA.............",
    "...........AAAA.AAA.............",
    "...........AAAA.AAA.............",
    "...........AAAA.AAA.............",
    "...........BBBB.BBB.............",
    "...........BBBB.BBB.............",
    "...........BBBB.BBB.............",
    "...........BBBB.BBB.............",
    "...........BBBB.BBB.............",
    "...........BBBB.BBB.............",
    "...........BBBB.BBB.............",
    "..........BBBBB.BBBB............",
    "..........BBBBB.BBBB............",
    "..........BBBBB.BBBB............",
    "..........BBBBB.BBBB............",
    "..........BBBBB.BBBB............",
    "..........BBBBB.BBBB............",
    ".........BBBBBB.BBBBB...........",
    ".........BBBBBB.BBBBB...........",
    ".........BBBBBB.BBBBB...........",
    ".........BBBBBBBBBBBB...........",
    ".........BBBBBBBBBBBB...........",
    ".........BBBBBBBBBBBB...........",
    "................................",
    "................................",
]

FORMS = [
    ("dark_blade", DARK_BLADE),
    ("void_archer", VOID_ARCHER),
    ("ancient_shield", ANCIENT_SHIELD),
    ("void_thrower", VOID_THROWER),
]


def main():
    os.makedirs(OUT_DIR, exist_ok=True)
    print("폼 스프라이트 HD 생성 -> %s" % OUT_DIR)
    print("규격 %dx%d · PPU %d · 광원 왼쪽 위 · 6단계 색조이동 램프" % (W, H, PPU))
    for form_id, matrix in FORMS:
        img = render(matrix, form_id)
        img.save(os.path.join(OUT_DIR, "%s.png" % form_id))
        px = sum(1 for row in matrix for ch in row if ch != ".")
        cols = len({img.getpixel((x, y)) for y in range(H) for x in range(W)
                    if img.getpixel((x, y))[3] > 0})
        print("  생성: %-16s 불투명 %4dpx · 색 %2d종  (%s)"
              % (form_id, px, cols, spec.SILHOUETTE_RULES[form_id]))
    print("완료 — 임포트 시 PPU %d · Point 필터 · 압축 없음으로 설정할 것" % PPU)


if __name__ == "__main__":
    main()
