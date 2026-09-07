# -*- coding: utf-8 -*-
"""
enforce_palette.py — 생성된 이미지를 씬 팔레트로 강제 양자화하고, **강제의 대가를 잰다.**

    python Tools/ArtPipeline/enforce_palette.py raw/*.png --scene stage1_rift_entrance
    python Tools/ArtPipeline/enforce_palette.py raw/bg_mid.png --palette Art_Source/palettes/stage1_rift_entrance.png
    python Tools/ArtPipeline/enforce_palette.py raw/*.png --scene stage1_rift_entrance --check

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

🔴 이 파일이 존재하는 이유 — 2026-09-06 설계 수정

  넉 달에 도구가 세 번 바뀌었고 그때마다 정체성이 같이 죽었다. 그래서 Art_Source/ 를
  내구도 순으로 나누고 40_TOOLS/ 를 **버리는 층**으로 만들었다.

  그래 놓고 바이블의 핵심 장치를 그 버리는 층의 기능에 걸어 뒀다:

      "프롬프트는 부탁이고 color_image 는 명령이다" — 10_BIBLE/04-palette.md

  color_image 는 PixelLab 의 **legacy 엔드포인트에만** 있는 칸이다. 도구가 네 번째로
  바뀌면 P1~P5 가 같이 죽는다 — 이 재작성이 고치려던 바로 그 병을 다시 만든 것이다.

  그래서 집행을 **생성에서 후처리로** 옮겼다:

      생성 (도구가 무엇이든)  →  enforce_palette.py  →  P1~P5 보장

  픽셀 아트에서 표준 공정이고, 도구가 또 바뀌어도 안 죽는다.
  color_image 같은 칸이 있는 모델이면 그쪽도 받는다 — 다만 **집행의 권한은 여기 있다.**

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

🔑 그런데 강제만 하면 gen1~3 의 실패를 다른 방식으로 반복한다.

  팔레트에서 한참 벗어난 그림을 억지로 끌어다 붙이면 색은 규약을 지키지만 그림이 뭉개진다
  (계조가 무너지고 면이 뭉텅이로 뭉친다). 그리고 **그렇게 나온 결과물은 조용하다** —
  색 검사를 통과하므로 아무도 안 멈춘다.

  그래서 이 스크립트는 재는 일을 같이 한다:

      옮긴 거리    얼마나 끌어왔나 (ΔE)     크면 생성이 팔레트를 안 따랐다는 뜻
      쓰인 색      램프 몇 단을 실제로 썼나  적으면 그림이 납작하다
      뭉침         몇 색이 한 칸으로 뭉쳤나  크면 계조가 죽는다

  🔴 **막는 것은 강제가 아니라 판정이다.** 거리가 크면 고쳐 붙이지 말고 다시 뽑는다.
     (40_TOOLS/pixellab/profile.md · 10_BIBLE/03-light.md §5 와 같은 태도)
"""
import argparse
import os
import re

import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(os.path.dirname(HERE))
PALETTE_SRC = os.path.join(REPO, "Art_Source", "palettes", "_palettes.txt")

HEX = re.compile(r"\b([0-9A-Fa-f]{6})\b")

# ── 판정 기준선 ────────────────────────────────────────────────────────────────
# ⚠️ 전부 **지어낸 것**이다. PixelLab 산출물로 아직 안 쟀다.
#    stage1 앵커를 뽑은 뒤 실측으로 조정한다 — 지금 숫자를 확정으로 옮겨 적으면
#    안 재고 정하는 것이 된다 (3권 19장 · 10_BIBLE/04 §5).
#
#    ΔE76 기준: 2.3 이 사람이 겨우 구분하는 차이다. 10색 램프에 스냅하면
#    평균 5~10 은 정상이고, 그 위는 "생성이 팔레트를 안 따랐다"에 가깝다.
REROLL_MEAN_DE = 12.0        # 평균이 이보다 크면 다시 뽑는 쪽이 낫다
REROLL_P95_DE = 28.0         # 상위 5% 가 이보다 크면 특정 영역이 통째로 벗어나 있다
THIN_RAMP_RATIO = 0.5        # 팔레트의 절반도 안 썼으면 그림이 납작하다
ALPHA_CUT = 8                # 이 아래는 투명으로 보고 색을 재지 않는다


def short(path):
    """저장소 기준 상대경로. 단 다른 드라이브면 relpath 가 예외를 던진다(Windows).

    ⚠️ 실제로 밟았다 — 아트 산출물은 저장소 밖(다른 드라이브)에 둘 수 있다.
       보기 좋으라고 넣은 줄 하나가 도구 전체를 멈추게 하면 안 된다.
    """
    try:
        return os.path.relpath(path, REPO)
    except ValueError:
        return path


# ────────────────────────────────────────────────────────────── 색 공간
#
# 🔑 RGB 유클리드로 최근접을 찾지 않는다.
#    우리 팔레트는 **잔여 채도만 남긴 무채 램프**(P1)라 색끼리 RGB 거리가 촘촘하다.
#    거기서 RGB 로 재면 명도가 아니라 색조 쪽으로 스냅해 램프 순서가 뒤집힌다.
#    Lab 은 명도(L)를 축으로 분리해서 재므로 램프의 단이 유지된다.

def srgb_to_lab(rgb):
    """sRGB(0~255) → CIE Lab (D65). shape (..., 3) 를 그대로 받는다."""
    v = np.asarray(rgb, dtype=np.float64) / 255.0
    lin = np.where(v <= 0.04045, v / 12.92, ((v + 0.055) / 1.055) ** 2.4)

    m = np.array([[0.4124564, 0.3575761, 0.1804375],
                  [0.2126729, 0.7151522, 0.0721750],
                  [0.0193339, 0.1191920, 0.9503041]])
    xyz = lin @ m.T
    white = np.array([0.95047, 1.00000, 1.08883])
    t = xyz / white

    eps = 216.0 / 24389.0
    kappa = 24389.0 / 27.0
    f = np.where(t > eps, np.cbrt(t), (kappa * t + 16.0) / 116.0)

    out = np.empty_like(f)
    out[..., 0] = 116.0 * f[..., 1] - 16.0
    out[..., 1] = 500.0 * (f[..., 0] - f[..., 1])
    out[..., 2] = 200.0 * (f[..., 1] - f[..., 2])
    return out


# ────────────────────────────────────────────────────────────── 팔레트 읽기

def load_from_txt(scene):
    """_palettes.txt 의 [scene] 블록에서 hex 를 모은다. make_palettes.py 와 같은 규칙."""
    if not os.path.exists(PALETTE_SRC):
        raise SystemExit("🔴 팔레트 원본이 없다: %s" % PALETTE_SRC)
    colors, current = [], None
    with open(PALETTE_SRC, encoding="utf-8") as f:
        for line in f:
            line = line.split("#", 1)[0].strip()
            if not line:
                continue
            header = re.fullmatch(r"\[([a-z0-9_]+)\]", line)
            if header:
                current = header.group(1)
                continue
            if current == scene:
                colors.extend(HEX.findall(line))
    if not colors:
        raise SystemExit(
            "🔴 '%s' 팔레트가 _palettes.txt 에 없다.\n"
            "   아직 안 고른 씬이다 — 값을 지어내 채우지 말고 먼저 고를 것.\n"
            "   근거: Art_Source/palettes/_palettes.txt 아래쪽 주석" % scene)
    return [c.upper() for c in colors]


def load_from_png(path):
    """구운 팔레트 PNG 에서 고유색을 뽑는다(순서는 왼쪽부터)."""
    im = Image.open(path).convert("RGB")
    arr = np.asarray(im).reshape(-1, 3)
    seen, out = set(), []
    for r, g, b in arr:
        key = (int(r), int(g), int(b))
        if key not in seen:
            seen.add(key)
            out.append("%02X%02X%02X" % key)
    return out


# ────────────────────────────────────────────────────────────── 집행

def enforce(path, out_path, colors, dither=False, check_only=False):
    im = Image.open(path).convert("RGBA")
    arr = np.asarray(im).astype(np.float64)
    h, w = arr.shape[:2]

    rgb = arr[..., :3]
    alpha = arr[..., 3]
    solid = alpha >= ALPHA_CUT

    pal_rgb = np.array([[int(c[i:i + 2], 16) for i in (0, 2, 4)] for c in colors], dtype=np.float64)
    pal_lab = srgb_to_lab(pal_rgb)

    if not solid.any():
        print("  %-26s 전부 투명 — 건너뜀" % os.path.basename(path))
        return None

    px = rgb[solid]
    if dither:
        # Floyd-Steinberg. 기본값은 꺼 둔다 —
        # 배경은 lineless·flat 이 규약이라(10_BIBLE/03) 디더 노이즈가 그 면을 깬다.
        # 넓은 계조가 띠로 뭉칠 때만 켠다.
        out_rgb, idx = _dither(rgb, solid, pal_rgb, pal_lab)
    else:
        lab = srgb_to_lab(px)
        d = np.linalg.norm(lab[:, None, :] - pal_lab[None, :, :], axis=2)
        idx = np.argmin(d, axis=1)
        out_rgb = rgb.copy()
        out_rgb[solid] = pal_rgb[idx]

    # ── 대가를 잰다 ───────────────────────────────────────────────────────
    moved = np.linalg.norm(srgb_to_lab(px) - srgb_to_lab(out_rgb[solid]), axis=1)
    mean_de, p95_de, max_de = float(moved.mean()), float(np.percentile(moved, 95)), float(moved.max())

    used = np.bincount(idx, minlength=len(colors))
    used_count = int((used > 0).sum())
    src_distinct = len({tuple(v) for v in px.astype(np.uint8).reshape(-1, 3)})

    name = os.path.basename(path)
    print("  %-26s %dx%d · 불투명 %d px" % (name, w, h, int(solid.sum())))
    print("     옮긴 거리  평균 ΔE %.1f · 상위5%% %.1f · 최대 %.1f" % (mean_de, p95_de, max_de))
    print("     쓰인 색    %d / %d 단" % (used_count, len(colors)))
    print("     뭉침       원본 %d색 → %d색" % (src_distinct, used_count))

    verdict = []
    if mean_de > REROLL_MEAN_DE:
        verdict.append("🔴 평균 ΔE %.1f > %.1f — 생성이 팔레트를 안 따랐다. "
                       "고쳐 붙이지 말고 **다시 뽑을 것**" % (mean_de, REROLL_MEAN_DE))
    if p95_de > REROLL_P95_DE:
        verdict.append("🔴 상위5%% ΔE %.1f > %.1f — 특정 영역이 통째로 벗어나 있다. "
                       "어디인지 보고 판정할 것" % (p95_de, REROLL_P95_DE))
    if used_count < len(colors) * THIN_RAMP_RATIO:
        unused = [colors[i] for i in range(len(colors)) if used[i] == 0]
        verdict.append("⚠️ 램프 %d단 중 %d단만 썼다 — 그림이 납작하다. "
                       "안 쓴 색: %s" % (len(colors), used_count, " ".join("#" + c for c in unused[:6])))
    for line in verdict:
        print("     %s" % line)

    if check_only:
        return mean_de

    out = np.dstack([out_rgb, alpha]).astype(np.uint8)
    Image.fromarray(out, "RGBA").save(out_path)
    print("     → %s" % short(out_path))
    return mean_de


def _dither(rgb, solid, pal_rgb, pal_lab):
    """Floyd-Steinberg. 오차 확산은 앞 픽셀 결과에 의존해서 벡터화가 안 된다.

    **잰 것**: 160x96 에 1.1초. 최대 크기인 688x384 면 픽셀이 17배라 20초 안팎.
    한 장씩 판정하며 쓰는 도구라 견딘다 — 배치로 수십 장 돌릴 것이면 끄고 쓴다.
    """
    h, w = rgb.shape[:2]
    work = rgb.copy()
    out = rgb.copy()
    idx_map = np.zeros((h, w), dtype=np.int64)

    for y in range(h):
        rng = range(w) if y % 2 == 0 else range(w - 1, -1, -1)
        for x in rng:
            if not solid[y, x]:
                continue
            old = np.clip(work[y, x], 0, 255)
            lab = srgb_to_lab(old[None, :])
            k = int(np.argmin(np.linalg.norm(lab - pal_lab, axis=1)))
            new = pal_rgb[k]
            out[y, x] = new
            idx_map[y, x] = k
            err = old - new
            step = 1 if y % 2 == 0 else -1
            for dx, dy, f in ((step, 0, 7 / 16.0), (-step, 1, 3 / 16.0),
                              (0, 1, 5 / 16.0), (step, 1, 1 / 16.0)):
                nx, ny = x + dx, y + dy
                if 0 <= nx < w and 0 <= ny < h and solid[ny, nx]:
                    work[ny, nx] += err * f
    return out, idx_map[solid]


# ────────────────────────────────────────────────────────────── 메인

def main():
    ap = argparse.ArgumentParser(
        description="생성 이미지를 씬 팔레트로 강제하고 그 대가를 잰다 (도구 무관 후처리)")
    ap.add_argument("inputs", nargs="+", help="입력 PNG (여러 장 가능)")
    ap.add_argument("--scene", help="_palettes.txt 의 씬 id (예: stage1_rift_entrance)")
    ap.add_argument("--palette", help="구운 팔레트 PNG 경로 (--scene 대신)")
    ap.add_argument("--out-dir", default=None, help="기본값은 입력과 같은 폴더")
    ap.add_argument("--suffix", default="-pal", help="출력 파일 접미사 (기본 -pal)")
    ap.add_argument("--dither", action="store_true",
                    help="Floyd-Steinberg 디더. 기본은 끔 — 배경 flat 규약을 깬다")
    ap.add_argument("--check", action="store_true", help="재기만 하고 파일을 쓰지 않는다")
    args = ap.parse_args()

    if args.palette:
        colors = load_from_png(args.palette)
        source = short(args.palette)
    elif args.scene:
        colors = load_from_txt(args.scene)
        source = "_palettes.txt [%s]" % args.scene
    else:
        raise SystemExit("🔴 --scene 또는 --palette 중 하나가 필요하다")

    print("팔레트: %s — %d색" % (source, len(colors)))
    print("   %s" % " ".join("#" + c for c in colors))
    if args.dither:
        print("   ⚠️ 디더 켜짐 — 배경(lineless·flat)에는 권하지 않는다")
    print()

    scores, failed = [], 0
    for p in args.inputs:
        if not os.path.exists(p):
            print("  건너뜀 (파일 없음): %s" % p)
            continue
        d = args.out_dir or os.path.dirname(p) or "."
        os.makedirs(d, exist_ok=True)
        stem = os.path.splitext(os.path.basename(p))[0]
        out_path = os.path.join(d, stem + args.suffix + ".png")
        score = enforce(p, out_path, colors, dither=args.dither, check_only=args.check)
        if score is not None:
            scores.append(score)
            if score > REROLL_MEAN_DE:
                failed += 1

    if scores:
        print("\n완료 — %d장 · 평균 ΔE %.1f" % (len(scores), sum(scores) / len(scores)))
        if failed:
            print("🔴 %d장이 재생성 기준(평균 ΔE %.1f)을 넘었다." % (failed, REROLL_MEAN_DE))
            print("   강제는 됐지만 그림이 뭉갰을 가능성이 크다 — 눈으로 보고 판정할 것.")
        print("\n⚠️ 기준값(%.1f / %.1f)은 아직 **지어낸 것**이다. PixelLab 산출물로 안 쟀다."
              % (REROLL_MEAN_DE, REROLL_P95_DE))
        print("   stage1 앵커를 뽑은 뒤 실측으로 조정할 것 — 10_BIBLE/04-palette.md §5")


if __name__ == "__main__":
    main()
