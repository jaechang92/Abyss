# -*- coding: utf-8 -*-
"""실루엣만 남겨 서로 갈리는지 잰다 — 색·명암을 빼고 형태만 본다.

    python Tools/ArtPipeline/measure_silhouette.py Assets/Art/Sprites/Forms/*.png --height 64 --sheet out.png

🔴 **왜 이 도구가 생겼나** (2026-09-10)

  `10_BIBLE/08-silhouette.md` 가 폼 4종을 **도형으로 가른다**고 정해 두었다
  (세로 선 / 곡선 / 사각 / 삼각). 그 규약이 그림에 실현됐는지 처음으로 쟀더니
  **여섯 쌍이 전부 IoU 0.49~0.58 에 몰려 있었다.**

      검사 vs 궁수 0.50   검사 vs 방패 0.52   검사 vs 투척 0.51
      궁수 vs 방패 0.49   궁수 vs 투척 0.51   방패 vs 투척 0.58

  도형으로 갈린다면 쌍마다 값이 벌어져야 하는데 안 벌어진다. 넷이 균일하게 절반씩
  겹친다는 뜻이고, **구분이 문서에만 있었다**(W7). 128px 로 키워도 같은 값이라
  해상도 문제도 아니었다.

🔑 **눈으로는 안 잡힌다.** 색·명암·장식이 얹혀 있으면 넷은 확실히 달라 보인다.
   전투 중에 필요한 것은 그것이 아니라 **한눈에 드는 형태**이고, 그건 실루엣만
   남겨야 보인다. 이 도구는 그 벗겨내기를 한다.

🔴 **IoU 가 못 재는 것 — 「도형의 종류」** (2026-09-10 방패 폼에서 드러났다)

  방패 폼에 실제로 탑실드를 붙였더니 **활과의 IoU 가 0.37 -> 0.59 로 나빠졌다.**
  지표만 보면 후퇴인데 **눈으로는 넷이 더 잘 갈린다.** 사각 방패가 커지면서 활의 곡선과
  겹치는 **면적**이 늘었을 뿐이고, 사각과 곡선은 겹쳐도 다른 도형이다.

  🔑 IoU 는 **면적 겹침**만 잰다. 「이 둘이 다른 형태인가」가 아니라 「얼마나 포개지는가」다.
     채움 면적도 마찬가지다 — 빽빽할수록 남과 겹치므로 **「밀도 = 무게」와 IoU 는 상충한다.**
     방패병은 무거워야 하고(hp 1.5) 무거우면 빽빽하고 빽빽하면 겹친다.

  📌 **그래서 이 도구의 값을 「낮을수록 좋다」로 읽지 말 것.** 두 폼이 같은 도형인데
     위치만 다른 경우(둘 다 세로선)를 잡는 데는 강하고, 다른 도형인데 면적이 겹치는
     경우(사각 vs 곡선)는 못 가른다. **눈 판정을 대신하지 못한다.**

⚠️ **IoU 합격선을 안 둔다.** ΔE 때와 같은 이유다(`04-palette §5-A`) —
   표본이 4장뿐이고 그마저 전부 「안 갈리는 상태」다. 지금 아는 것은
   **0.49~0.58 이 안 갈리는 값**이라는 것 하나뿐이라, 그것을 기준점으로 적어만 둔다.
   다시 뽑은 그림이 그보다 내려가는지를 보는 것이 이 도구의 쓸모다.
"""

import argparse
import os
from itertools import combinations

import numpy as np
from PIL import Image

# 「안 갈리는 상태」의 실측 기준점 — 합격선이 아니다. 위 주석 참조.
KNOWN_INDISTINCT = (0.49, 0.58)
ALPHA_CUT = 8          # 이 아래는 투명으로 본다
SOLID_CUT = 127        # 축소 뒤 이 위를 실루엣으로 본다


def silhouette(path, height):
    """알파 바운딩으로 자르고 높이를 맞춘 뒤 이진 실루엣을 준다.

    🔑 축소는 BOX(면적 평균)로 한다. NEAREST 로 줄이면 가는 획이 통째로 사라져
       **실제보다 잘 갈리는 것처럼 보인다** — 활 시위가 그런 획이다.
    """
    im = Image.open(path).convert("RGBA")
    a = np.array(im)[:, :, 3]
    ys, xs = np.where(a > ALPHA_CUT)
    if len(ys) == 0:
        return None
    im = im.crop((xs.min(), ys.min(), xs.max() + 1, ys.max() + 1))
    w = max(1, round(im.width * height / im.height))
    small = im.resize((w, height), Image.BOX)
    return np.array(small)[:, :, 3] > SOLID_CUT


def align(sils, height):
    """발밑 기준 · 가로 중앙으로 맞춘다. 캐릭터는 서 있는 것이라 발이 기준이다."""
    width = max(s.shape[1] for s in sils)
    out = []
    for s in sils:
        canvas = np.zeros((height, width), bool)
        off = (width - s.shape[1]) // 2
        canvas[:, off:off + s.shape[1]] = s
        out.append(canvas)
    return out


def sheet(sils, names, height, zoom, dest):
    """실루엣만 나란히 그린다. 밝은 단색 — 캐릭터 대역(`08-silhouette §2`)을 흉내낸다."""
    from PIL import ImageDraw
    cell = max(s.shape[1] for s in sils) + 8
    img = Image.new("RGBA", (cell * len(sils) * zoom + 20, height * zoom + 44), (24, 28, 34, 255))
    d = ImageDraw.Draw(img)
    d.text((8, 6), "SILHOUETTE ONLY  -  height %dpx" % height, fill=(210, 217, 219, 255))
    for i, (s, n) in enumerate(zip(sils, names)):
        rgba = np.zeros((s.shape[0], s.shape[1], 4), np.uint8)
        rgba[:, :, :3] = 210
        rgba[:, :, 3] = s * 255
        tile = Image.fromarray(rgba, "RGBA")
        tile = tile.resize((tile.width * zoom, tile.height * zoom), Image.NEAREST)
        x = 10 + i * cell * zoom + (cell * zoom - tile.width) // 2
        img.paste(tile, (x, 26), tile)
        d.text((10 + i * cell * zoom, height * zoom + 30), n[:16], fill=(150, 166, 172, 255))
    img.save(dest)
    return dest


def main():
    ap = argparse.ArgumentParser(description="실루엣만 남겨 서로 갈리는지 잰다")
    ap.add_argument("inputs", nargs="+")
    ap.add_argument("--height", type=int, default=64,
                    help="맞출 세로 아트px (기본 64 — 폼 기본 크기, 08-silhouette §1)")
    ap.add_argument("--zoom", type=int, default=5, help="시트 확대 배율")
    ap.add_argument("--sheet", default=None, help="비교 시트 PNG 경로 (안 주면 안 그린다)")
    args = ap.parse_args()

    names, sils = [], []
    for p in args.inputs:
        if not os.path.exists(p):
            print("  건너뜀 (파일 없음): %s" % p)
            continue
        s = silhouette(p, args.height)
        if s is None:
            print("  건너뜀 (불투명 픽셀 없음): %s" % p)
            continue
        names.append(os.path.splitext(os.path.basename(p))[0])
        sils.append(s)

    if len(sils) < 2:
        raise SystemExit("🔴 두 장 이상 필요하다 — 서로 갈리는지 재는 도구다")

    print("세로 %dpx 로 맞춰 잰다 (%d장)\n" % (args.height, len(sils)))
    print("  %-22s %6s %8s" % ("이름", "폭", "채움"))
    for n, s in zip(names, sils):
        print("  %-22s %5dpx %7.1f%%" % (n, s.shape[1], 100 * s.sum() / s.size))

    aligned = align(sils, args.height)
    print("\n  실루엣 겹침 (IoU) — 1.00 이면 형태가 같다")
    pairs = []
    for i, j in combinations(range(len(aligned)), 2):
        inter = (aligned[i] & aligned[j]).sum()
        union = (aligned[i] | aligned[j]).sum()
        iou = inter / union if union else 0.0
        pairs.append((iou, names[i], names[j]))
        print("  %-22s vs %-22s %.2f" % (names[i], names[j], iou))

    lo, hi = min(p[0] for p in pairs), max(p[0] for p in pairs)
    print("\n  범위 %.2f ~ %.2f" % (lo, hi))
    klo, khi = KNOWN_INDISTINCT
    if lo >= klo and hi <= khi + 0.02:
        print("  🔴 옛 폼 4종이 「안 갈리던 때」와 같은 구간이다 —")
        print("     도형 배정이 그림에 반영되지 않았을 때의 값이 %.2f~%.2f 였다." % (klo, khi))
    print("\n  ⚠️ 합격선은 없다. 이 값들은 판정이 아니라 **다시 뽑은 것이 내려갔는지** 보는 눈금이다.")
    print("     실측 기준점: 2026-09-10 옛 폼 4종이 %.2f~%.2f (전부 안 갈리는 상태)" % (klo, khi))

    if args.sheet:
        print("\n  시트  %s" % sheet(sils, names, args.height, args.zoom, args.sheet))


if __name__ == "__main__":
    main()
