# -*- coding: utf-8 -*-
"""
prepare_form_sprite.py — AI 생성 아트를 게임용 폼 스프라이트로 준비한다.

`Art_Source/`의 손그림 원본을 받아 `Assets/Art/Sprites/Forms/<formId>.png`로 낸다.

🔴 이 폴더(`Tools/ArtPipeline`)는 **AI 손그림 파이프라인**이다.
   `Tools/PixelArt`는 폐기된 절차적 도트 파이프라인이고 **기준선으로만 보존**한다.
   두 개를 한 폴더에 두었더니 실제로 섞여 들어갔다(2026-08-20: 픽셀아트 3종이
   게임 폴더에 남아 `WireFormBodySprites`가 그걸 연결했다). 그래서 폴더를 갈랐다.

하는 일:
  1. 필요하면 크로마키 제거(이미 투명하면 건너뜀)
  2. 여백 트림
  3. 세로 256px로 리사이즈 → PPU 128에서 정확히 2유닛(플레이어 콜라이더 높이)
  4. **발밑 피벗 실측** — 무기가 한쪽으로 뻗어 가로 중앙과 발 중심이 다르다

실행:
  python Tools/ArtPipeline/prepare_form_sprite.py <formId> <원본경로>
  python Tools/ArtPipeline/prepare_form_sprite.py --all      # SOURCES 표대로 전부
"""
import os
import sys

import numpy as np
from PIL import Image

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

ROOT = os.path.normpath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT_DIR = os.path.join(ROOT, "Assets", "Art", "Sprites", "Forms")
TARGET_H = 256          # PPU 128 → 2유닛
ALPHA_MIN = 40

# 폼별 채택 원본. **여기가 "어떤 그림을 쓰기로 했는가"의 SoT다.**
# 후보 중 무엇을 골랐는지는 파일만 봐서는 알 수 없으므로 표로 남긴다.
#
# 🔴 2026-08-26 — NovelAI Diffusion V5 전환으로 `Art_Source/cut/`을 비웠다.
#    아래 네 줄 중 **dark_blade만 살아 있고 나머지 셋은 가리키는 파일이 없다** →
#    `--all`은 3종에서 실패한다. 표를 지우지 않고 두는 이유는 이것이 *기록*이기 때문이다:
#    지금 게임에 들어가 있는 `Assets/Art/Sprites/Forms/*.png` 4장이 어디서 왔는지는
#    여기 말고는 적힌 곳이 없다. NovelAI 원본이 나오면 경로를 갈아끼운다.
#    함수 본체(트림·리사이즈·발밑 피벗 실측)는 생성기와 무관하므로 그대로 쓴다.
SOURCES = {
    # formId          경로(ROOT 기준)                                     선정 이유
    "dark_blade":     ("Art_Source/base/dark_blade.png",
                       "디테일 최상 + 이미 투명 배경"),
    "void_archer":    ("Art_Source/cut/void_archer/void_archer_1-cut.png",
                       "단일 후보 중 활 곡선이 가장 크게 읽힘"),
    "ancient_shield": ("Art_Source/cut/ancient_shield/ancient_shield_1-cut.png",
                       "측면 단일 재생성(2026-08-20). 금색 지배로 검사와 팔레트 56.7%->30.0%"),
    "void_thrower":   ("Art_Source/cut/void_thrower/void_thrower_1-cut.png",
                       "단일 후보 1장"),
}


def needs_chroma(im):
    """모서리가 불투명하면 아직 배경이 붙어 있다."""
    a = np.asarray(im)[..., 3]
    return bool(a[0, 0] > 200 and a[0, -1] > 200)


def prepare(form_id, src_rel):
    src = os.path.join(ROOT, src_rel)
    if not os.path.exists(src):
        print("  ❌ %-16s 원본 없음: %s" % (form_id, src_rel))
        return None

    im = Image.open(src).convert("RGBA")
    w0, h0 = im.size

    if needs_chroma(im):
        print("  ⚠️ %-16s 배경이 아직 붙어 있다 — chroma_cutout.py를 먼저 돌릴 것" % form_id)
        return None

    bbox = im.getbbox()
    if bbox:
        im = im.crop(bbox)

    w = max(1, round(im.width * TARGET_H / im.height))
    im = im.resize((w, TARGET_H), Image.LANCZOS)

    os.makedirs(OUT_DIR, exist_ok=True)
    out = os.path.join(OUT_DIR, "%s.png" % form_id)
    im.save(out)

    # 발밑 피벗 — 알파 최하단 6px 구간의 가로 중앙.
    # 가로 중앙(0.5)을 쓰면 무기가 뻗은 쪽으로 축이 밀려 **달릴 때 캐릭터가 좌우로 흔들린다.**
    a = np.asarray(im)[..., 3]
    ys, xs = np.nonzero(a > ALPHA_MIN)
    foot_y = ys.max()
    foot_x = xs[ys > foot_y - 6].mean()
    pivot = (foot_x / im.width, 1.0 - foot_y / im.height)

    print("  ✅ %-16s %4dx%-5d → %3dx%-4d  피벗 (%.3f, %.3f)"
          % (form_id, w0, h0, im.width, im.height, pivot[0], pivot[1]))
    return pivot


def main():
    args = sys.argv[1:]
    if args and args[0] != "--all":
        form_id, src = args[0], args[1]
        prepare(form_id, os.path.relpath(src, ROOT) if os.path.isabs(src) else src)
        return

    print("폼 스프라이트 준비 → %s" % os.path.relpath(OUT_DIR, ROOT))
    print("규격: 세로 %dpx · PPU 128(= 2유닛) · 피벗 발밑 실측\n" % TARGET_H)

    pivots = {}
    for form_id, (src, why) in SOURCES.items():
        p = prepare(form_id, src)
        if p:
            pivots[form_id] = p
        print("       원본: %s  (%s)" % (src, why))

    if pivots:
        print("\n── FormSpriteImporter.Pivots 에 넣을 값 ──")
        for k, v in pivots.items():
            print('            ["%s"] = new Vector2(%.3ff, %.3ff),' % (k, v[0], v[1]))


if __name__ == "__main__":
    main()
