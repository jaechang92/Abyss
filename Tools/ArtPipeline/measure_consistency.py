# -*- coding: utf-8 -*-
"""
measure_consistency.py — AI 생성 포즈들이 "같은 캐릭터"로 읽히는지 정량 측정.

AI 이미지 생성으로 게임 스프라이트를 만들 때 유일하게 어려운 문제는 **일관성**이다.
눈으로 "비슷해 보인다"는 판단은 표본이 늘면 흔들리므로, 세 축을 숫자로 잰다:

  1. 비율   — 프레임 높이 대비 폭. 같은 캐릭터면 체격이 안 변한다.
  2. 팔레트 — 상위 색의 히스토그램 교집합. 갑주 색이 프레임마다 바뀌면 여기서 잡힌다.
  3. 밝기   — 평균 명도. 조명이 프레임마다 달라지면 애니메이션에서 깜빡인다.

행 이미지(row strip)를 세로 투영으로 갈라 프레임을 분리한다 — 연결요소 라벨링 대신
투영을 쓰는 이유는 **무기가 인접 프레임과 겹쳐도 사람 단위로 갈리기 때문**이다.
(연결요소는 칼끝이 옆 프레임에 닿으면 둘을 하나로 붙여 버린다.)

실행:
  python Tools/PixelArt/measure_consistency.py <행이미지-cut.png> --expect 3
"""
import argparse
import os

import numpy as np
from PIL import Image


def split_columns(alpha, expect):
    """
    세로 투영의 **골짜기**로 프레임을 가른다.

    🔴 "빈 구간(밀도 0)"으로 가르면 안 된다 — 무기가 옆 프레임에 걸치면 밀도가 0으로
    안 떨어지고 세 프레임이 하나로 붙는다(2026-08-20에 실제로 그랬다: 기대 3, 검출 1).
    사람은 그 지점을 "여기가 경계"로 읽으므로, 기준도 절대 0이 아니라
    **주변 대비 얼마나 낮은가**여야 한다.

    expect-1개의 가장 깊은 골짜기를 고른다 — 기대 프레임 수를 아는 것이
    임계값을 추측하는 것보다 훨씬 안정적이다.
    """
    col = (alpha > 40).sum(axis=0).astype(np.float32)
    w = len(col)
    if expect <= 1:
        return [(0, w)]

    # 가장자리 여백은 후보에서 제외(그쪽은 원래 비어 있다)
    margin = int(w * 0.08)
    sep = max(1, int(w / expect * 0.45))   # 골짜기끼리 최소 간격

    cand = sorted(range(margin, w - margin), key=lambda x: col[x])
    cuts = []
    for x in cand:
        if all(abs(x - c) >= sep for c in cuts):
            cuts.append(x)
            if len(cuts) == expect - 1:
                break
    cuts.sort()

    bounds = [0] + cuts + [w]
    return [(bounds[i], bounds[i + 1]) for i in range(len(bounds) - 1)]


def analyse(img, box):
    x0, x1 = box
    sub = np.asarray(img)[:, x0:x1]
    a = sub[..., 3]
    ys, xs = np.nonzero(a > 40)
    if len(ys) == 0:
        return None
    h = ys.max() - ys.min() + 1
    w = xs.max() - xs.min() + 1
    rgb = sub[..., :3][a > 128].astype(np.float32)

    # 상위 색 히스토그램 — 5비트로 양자화해 미세 차이를 무시한다
    q = (rgb // 32).astype(np.int32)
    keys, counts = np.unique(q[:, 0] * 64 + q[:, 1] * 8 + q[:, 2], return_counts=True)
    hist = dict(zip(keys.tolist(), (counts / counts.sum()).tolist()))

    return {
        "w": int(w), "h": int(h),
        "ratio": w / h,
        "px": int((a > 128).sum()),
        "lum": float((0.299 * rgb[:, 0] + 0.587 * rgb[:, 1] + 0.114 * rgb[:, 2]).mean()),
        "hist": hist,
    }


def hist_overlap(a, b):
    """히스토그램 교집합 — 1.0이면 색 분포가 동일."""
    return sum(min(a.get(k, 0.0), b.get(k, 0.0)) for k in set(a) | set(b))


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("sheet")
    ap.add_argument("--expect", type=int, default=3)
    ap.add_argument("--names", default="idle,walk,attack")
    ap.add_argument("--save-frames", default=None)
    args = ap.parse_args()

    img = Image.open(args.sheet).convert("RGBA")
    alpha = np.asarray(img)[..., 3]
    spans = split_columns(alpha, args.expect)

    print("행 이미지: %s (%dx%d)" % (os.path.basename(args.sheet), img.width, img.height))
    print("검출된 프레임: %d개 (기대 %d)" % (len(spans), args.expect))
    if len(spans) != args.expect:
        print("  ⚠️ 프레임 수가 기대와 다르다 — 무기가 옆 프레임에 닿았거나 간격이 좁다")

    names = args.names.split(",")
    stats = []
    for i, box in enumerate(spans):
        st = analyse(img, box)
        if st is None:
            continue
        st["name"] = names[i] if i < len(names) else "frame%d" % i
        st["box"] = box
        stats.append(st)
        if args.save_frames:
            os.makedirs(args.save_frames, exist_ok=True)
            img.crop((box[0], 0, box[1], img.height)).crop(
                img.crop((box[0], 0, box[1], img.height)).getbbox()
            ).save(os.path.join(args.save_frames, "%s.png" % st["name"]))

    print()
    print("%-8s %6s %6s %7s %9s %8s" % ("프레임", "폭", "높이", "폭/높이", "불투명px", "평균밝기"))
    for s in stats:
        print("%-8s %6d %6d %7.3f %9d %8.1f" % (s["name"], s["w"], s["h"], s["ratio"], s["px"], s["lum"]))

    if len(stats) < 2:
        return 0

    hs = [s["h"] for s in stats]
    lums = [s["lum"] for s in stats]
    print()
    print("── 일관성 판정 ──")
    hv = (max(hs) - min(hs)) / max(hs)
    print("키 편차          %5.1f%%   %s" % (hv * 100, verdict(hv, 0.06, 0.12)))
    lv = (max(lums) - min(lums)) / max(lums)
    print("밝기 편차        %5.1f%%   %s" % (lv * 100, verdict(lv, 0.08, 0.15)))

    pairs = [(stats[i], stats[j]) for i in range(len(stats)) for j in range(i + 1, len(stats))]
    ov = [hist_overlap(a["hist"], b["hist"]) for a, b in pairs]
    for (a, b), o in zip(pairs, ov):
        print("팔레트 %-6s↔%-7s %5.1f%%   %s" % (a["name"], b["name"], o * 100, verdict(1 - o, 0.18, 0.32)))
    print()
    print("팔레트 평균      %5.1f%%   %s" % (np.mean(ov) * 100, verdict(1 - np.mean(ov), 0.18, 0.32)))
    return 0


def verdict(v, good, ok):
    return "✅ 양호" if v <= good else ("⚠️ 보통" if v <= ok else "❌ 불일치")


if __name__ == "__main__":
    raise SystemExit(main())
