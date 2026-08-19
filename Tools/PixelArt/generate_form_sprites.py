# -*- coding: utf-8 -*-
"""
generate_form_sprites.py — 폼(플레이어) 정지 스프라이트 4종 생성기. 4-1 아트.

규격은 form_sprite_spec.py가 SoT다(16x32, PPU 16, 피벗 발밑 중앙).
기존 적 생성기와 같은 ASCII 매트릭스 방식 — 한 글자가 한 픽셀이라 손으로 고칠 수 있다.

출력: Assets/Art/Sprites/Forms/{formId}.png
실행: python Tools/PixelArt/generate_form_sprites.py

Pillow 필요: pip install Pillow

🔑 이 넷은 **같은 사람이 형태를 바꾼 것**이다(00-concept USP-2).
   그래서 머리 위치·키·살색을 공유하고 어깨너비·장비·망토로만 가른다.
   전부 다르게 그리면 폼체인지가 '캐릭터 교체'로 읽힌다.
"""
import os
import sys
from PIL import Image

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import form_sprite_spec as spec

OUT_DIR = os.path.normpath(os.path.join(
    os.path.dirname(__file__), "..", "..", "Assets", "Art", "Sprites", "Forms"))

# 매트릭스 글자 규약 (전 폼 공통)
#   .  투명        o  외곽선
#   s  살          S  살 그림자
#   l  밝은색      b  기본색      d  그림자색      a  강조색
LEGEND = "  . 투명 / o 외곽선 / s 살 / S 살그림자 / l 밝음 / b 기본 / d 그림자 / a 강조"


def build(matrix, palette):
    """ASCII 매트릭스 → RGBA 이미지. 규격 크기와 안 맞으면 즉시 실패한다."""
    assert len(matrix) == spec.HEIGHT, f"행 {len(matrix)} != {spec.HEIGHT}"
    for i, row in enumerate(matrix):
        assert len(row) == spec.WIDTH, f"{i}행 폭 {len(row)} != {spec.WIDTH}"

    lut = {
        ".": spec.TRANSPARENT, "o": spec.OUTLINE,
        "s": spec.SKIN, "S": spec.SKIN_SHADE,
        "l": palette["light"], "b": palette["base"],
        "d": palette["shade"], "a": palette["accent"],
    }
    img = Image.new("RGBA", (spec.WIDTH, spec.HEIGHT), spec.TRANSPARENT)
    for y, row in enumerate(matrix):
        for x, ch in enumerate(row):
            img.putpixel((x, y), lut[ch])
    return img


# ─────────────────────────────────────────────────────────────
# 암흑 검사 — 어깨가 넓고 오른쪽에 긴 검. 세로로 뻗는 선 하나.
# ─────────────────────────────────────────────────────────────
DARK_BLADE = [
    "................",
    ".....oooo.......",
    "....odddo.......",
    "....odbdo...o...",
    "....ossso...oao.",
    "....ossso...oao.",
    ".....oso....oao.",
    "...oooooo...oao.",
    "..oldbbldo..oao.",
    ".oldbbbbldo.oao.",
    ".obdbbbbdbo.oao.",
    ".obdbbbbdbosoao.",
    ".obdbbbbdboSoao.",
    ".oobbbbbboooao..",
    "..obbbbbbo.oao..",
    "..odbbbbdo.oo...",
    "..odbbbbdo......",
    "..oodbbdoo......",
    "...obbbbo.......",
    "...obbbbo.......",
    "...od..do.......",
    "...od..do.......",
    "...ob..bo.......",
    "...ob..bo.......",
    "...ob..bo.......",
    "...od..do.......",
    "...od..do.......",
    "..ood..doo......",
    "..oddoodoo......",
    "..oddoodoo......",
    "..oooooooo......",
    "................",
]

# ─────────────────────────────────────────────────────────────
# 공허 궁수 — 가장 마르다. 활이 몸 옆 곡선(유일한 곡선 실루엣).
# ─────────────────────────────────────────────────────────────
VOID_ARCHER = [
    "................",
    ".....oooo.......",
    "....odddo..oo...",
    "....odbdo.oaao..",
    "....ossso.oa.o..",
    "....ossso.oa.o..",
    ".....oso..oa.o..",
    "....oooo..oa.o..",
    "...oldblo.oa.o..",
    "...obbbbo.oa.o..",
    "...obdbbosoa.o..",
    "...obbbboSoa.o..",
    "...obdbbo.oa.o..",
    "...obbbbo.oa.o..",
    "...oobboo.oa.o..",
    "....obbo..oa.o..",
    "....obbo..oaao..",
    "....obbo...oo...",
    "....obbo........",
    "...od..do.......",
    "...od..do.......",
    "...ob..bo.......",
    "...ob..bo.......",
    "...ob..bo.......",
    "...ob..bo.......",
    "...od..do.......",
    "...od..do.......",
    "...od..do.......",
    "..ood..doo......",
    "..oddoodoo......",
    "..oooooooo......",
    "................",
]

# ─────────────────────────────────────────────────────────────
# 고대 방패병 — 가장 넓다. 방패가 몸 절반을 덮는 사각 덩어리.
# ─────────────────────────────────────────────────────────────
ANCIENT_SHIELD = [
    "................",
    "....oooo........",
    "...odddo........",
    "...odbdo........",
    "...ossso........",
    "...ossso........",
    "....oso.........",
    ".oooooooo.......",
    "oldbbbbldo......",
    "obdbbbbdbooooo..",
    "obdbbbbdbolaal..",
    "obdbbbbdboladal.",
    "obbbbbbbbolaaal.",
    "oobbbbbboolaaal.",
    ".obbbbbbo.ladal.",
    ".obdbbdbo.laal..",
    ".obbbbbbo.oooo..",
    ".oobbbboo.......",
    "..obbbbo........",
    "..od..do........",
    "..od..do........",
    "..ob..bo........",
    "..ob..bo........",
    "..ob..bo........",
    "..ob..bo........",
    "..od..do........",
    "..od..do........",
    ".ood..doo.......",
    ".oddoodddo......",
    ".oddoodddo......",
    ".ooooooooo......",
    "................",
]

# ─────────────────────────────────────────────────────────────
# 심연 투척사 — 망토가 아래로 퍼진다. 삼각형(위가 좁고 아래가 넓다).
# ─────────────────────────────────────────────────────────────
VOID_THROWER = [
    "................",
    ".....oooo.......",
    "....odddo.......",
    "....odbdo.......",
    "....ossso.......",
    "....ossso....o..",
    ".....oso....oa..",
    "....oooo...oa...",
    "...oldblo.oa....",
    "...obbbboso.....",
    "...obdbboS......",
    "..oobbbboo......",
    "..obbbbbbo......",
    "..obdbbdbo......",
    "..obbbbbbo......",
    ".odbbbbbbdo.....",
    ".odbbbbbbdo.....",
    ".odbbbbbbdo.....",
    "odbbbbbbbbdo....",
    "odbbbbbbbbdo....",
    "oddbbbbbbbbddo..",
    ".oddbbbbbbddo...",
    "..oddbbbbddo....",
    "...oodbbdoo.....",
    "...ob..bo.......",
    "...ob..bo.......",
    "...od..do.......",
    "...od..do.......",
    "..ood..doo......",
    "..oddoodoo......",
    "..oooooooo......",
    "................",
]

FORMS = [
    ("dark_blade",     DARK_BLADE),
    ("void_archer",    VOID_ARCHER),
    ("ancient_shield", ANCIENT_SHIELD),
    ("void_thrower",   VOID_THROWER),
]


def main():
    os.makedirs(OUT_DIR, exist_ok=True)
    print(f"폼 스프라이트 생성 → {OUT_DIR}")
    print(f"규격 {spec.WIDTH}x{spec.HEIGHT} · PPU {spec.PPU} · 피벗 {spec.PIVOT}")
    print(LEGEND)
    for form_id, matrix in FORMS:
        img = build(matrix, spec.PALETTES[form_id])
        path = os.path.join(OUT_DIR, f"{form_id}.png")
        img.save(path)
        opaque = sum(1 for p in img.getdata() if p[3] > 0)
        print(f"  생성: {form_id:<16} 불투명 {opaque:3d}px  ({spec.SILHOUETTE_RULES[form_id]})")
    print("완료 — Unity 임포트 후 Tools > Abyss > Generate > Content 재실행으로 연결")


if __name__ == "__main__":
    main()
