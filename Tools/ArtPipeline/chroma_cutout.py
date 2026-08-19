# -*- coding: utf-8 -*-
"""
chroma_cutout.py — 크로마키 배경 제거 + 소프트 알파 언믹싱.

AI 생성 이미지를 게임 스프라이트로 쓰려면 배경을 투명으로 빼야 하는데,
단순 "그 색이면 지운다"로는 **가장자리가 지저분해진다** — 안티에일리어싱된 픽셀이
캐릭터색과 배경색의 혼합이라, 그걸 그냥 남기면 마젠타 테두리가 남고
그냥 지우면 실루엣이 갉힌다.

그래서 언믹싱을 한다:
    관측색 C = a·F + (1-a)·B      (F=전경 실제색, B=배경색, a=알파)
배경색 B를 알고 있으므로 a를 추정하고 F를 역산한다. 이러면 반투명 가장자리가
**원래 색 그대로 반투명**으로 남아 어떤 배경 위에 올려도 테두리가 안 뜬다.

실행:
  python Tools/PixelArt/chroma_cutout.py <입력.png> [--out 출력.png] [--tol 0.28]
"""
import argparse
import os
import sys

import numpy as np
from PIL import Image


def estimate_bg(arr):
    """모서리 표본의 중앙값을 배경색으로 본다(단색 배경 가정)."""
    h, w = arr.shape[:2]
    k = max(2, min(h, w) // 40)
    patches = [arr[:k, :k], arr[:k, -k:], arr[-k:, :k], arr[-k:, -k:]]
    return np.median(np.concatenate([p.reshape(-1, 3) for p in patches]), axis=0)


def cutout(path, out_path, tol):
    im = Image.open(path).convert("RGB")
    arr = np.asarray(im).astype(np.float32)
    h, w = arr.shape[:2]

    bg = estimate_bg(arr)

    # 배경색과의 거리를 '배경 방향' 축으로 재는 대신 단순 유클리드로 간다 —
    # 마젠타처럼 채도가 극단인 배경에서는 이것으로 충분하고, 축 투영은
    # 캐릭터에 배경과 같은 색조(붉은 갑주 vs 분홍 배경)가 있을 때 오히려 갉는다.
    dist = np.linalg.norm(arr - bg, axis=2) / 441.67  # 0~1 정규화

    # 🔴 알파 램프는 **좁아야 한다.** 캐릭터 대부분은 완전 불투명이고,
    # 반투명은 안티에일리어싱된 가장자리 한두 픽셀뿐이다. 램프를 넓게 잡으면
    # 캐릭터 내부가 반투명으로 계산되고, 그 상태로 배경색을 빼면 채널이 음수가 되어
    # **붉은 갑주가 초록으로 뒤집힌다**(2026-08-19에 실제로 그랬다).
    t0 = tol * 0.35          # 이 이하 = 배경
    t1 = tol                 # 이 이상 = 완전 불투명
    alpha = np.clip((dist - t0) / max(1e-6, t1 - t0), 0.0, 1.0)

    # 언믹싱은 **가장자리(0<a<1)에만** 적용한다. 내부는 원본 색 그대로 둔다 —
    # 불투명 픽셀에 굳이 역산을 걸면 반올림 오차만 쌓인다.
    fg = arr.copy()
    edge = (alpha > 0.02) & (alpha < 0.98)
    if edge.any():
        a_e = np.maximum(alpha[edge], 0.25)[..., None]
        fg[edge] = np.clip((arr[edge] - (1.0 - a_e) * bg) / a_e, 0, 255)

    rgba = np.dstack([fg, alpha * 255.0]).astype(np.uint8)
    out = Image.fromarray(rgba, "RGBA")

    # 완전 투명 영역을 잘라낸다(스프라이트는 여백이 곧 낭비다)
    bbox = out.getbbox()
    if bbox:
        out = out.crop(bbox)

    out.save(out_path)

    opaque = int((alpha > 0.95).sum())
    soft = int(((alpha > 0.02) & (alpha <= 0.95)).sum())
    # 남은 마젠타 오염: 알파가 있는데 여전히 배경색에 가까운 픽셀
    resid = int(((alpha > 0.3) & (dist < tol * 1.3)).sum())

    print("  %-22s %dx%d -> %dx%d" % (os.path.basename(path), w, h, out.width, out.height))
    print("     배경색 추정 RGB%s · 허용치 %.2f" % (tuple(int(v) for v in bg), tol))
    print("     불투명 %6d px · 반투명(가장자리) %5d px · 잔류오염 %d px" % (opaque, soft, resid))
    if resid > opaque * 0.01:
        print("     ⚠️ 잔류 오염이 1%% 이상 — 허용치를 올리거나 배경색이 균일한지 볼 것")
    return out


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("inputs", nargs="+")
    ap.add_argument("--out-dir", default=None)
    ap.add_argument("--tol", type=float, default=0.28)
    args = ap.parse_args()

    for p in args.inputs:
        d = args.out_dir or os.path.dirname(p)
        os.makedirs(d, exist_ok=True)
        stem = os.path.splitext(os.path.basename(p))[0]
        cutout(p, os.path.join(d, stem + "-cut.png"), args.tol)


if __name__ == "__main__":
    main()
