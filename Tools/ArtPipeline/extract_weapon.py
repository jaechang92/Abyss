# -*- coding: utf-8 -*-
"""무기 있는 몸 - 무기 없는 몸 = 무기 픽셀 + 손 앵커.

    python Tools/ArtPipeline/extract_weapon.py armed.png unarmed.png --out weapon.png

🔴 **왜 이 도구가 생겼나** (2026-09-10)

  무기가 **장비**다 — 업그레이드가 있고 다른 방패를 장착하면 구별돼야 한다.
  그러면 무기를 몸에 구워 그릴 수 없다(`10_BIBLE/08-silhouette §1-C`):

      무기 N종 x 폼 4 x 상태 9 = 조합 폭발

  분리의 유일한 난관이 **프레임마다 손이 어디인가**였다. 애니메이션은 도구가 만들고
  손 위치를 우리가 모른다. 9상태 x 6~9프레임 = 54~81개를 손으로 잡아야 하나 싶었는데 —

🔑 **무기 있는 몸과 없는 몸을 둘 다 만들면 차이가 곧 무기다.** 그 차이의 뿌리가 손이다.
   애니메이션도 두 벌 만들면 프레임마다 자동으로 나온다. **수작업 0.**

   검증 (앵커 붉은 기사 east, 92x92):
       무기 픽셀 133개가 x 53~74 · y 59~69 한 덩어리로 떨어졌다
       손 앵커 (54.0, 61.9) — 자루 끝에 정확히 찍혔다
       몸 쪽 잔여 34px (팔 자세가 미세하게 달라진 것)

⚠️ **몸 쪽 잔여를 0으로 만들 수는 없다.** state 파생은 무기만 지우는 게 아니라 자세를
   조금씩 건드린다. 그래서 이 도구는 **가장 큰 덩어리만** 무기로 본다 —
   흩어진 몇 픽셀은 팔이 움직인 자국이지 무기가 아니다.
"""

import argparse
import os

import numpy as np
from PIL import Image

ALPHA_CUT = 8
MIN_BLOB = 8          # 이보다 작은 덩어리는 자세 차이로 본다


def load(path):
    im = Image.open(path).convert("RGBA")
    return im, np.array(im, dtype=int)


def largest_blob(mask):
    """가장 큰 연결 성분만 남긴다 — 흩어진 자세 차이를 걷어낸다.

    scipy 없이 4-이웃 flood fill 로 한다(픽셀 수가 수백 개라 충분하다).
    """
    h, w = mask.shape
    seen = np.zeros_like(mask, bool)
    best = None
    for sy in range(h):
        for sx in range(w):
            if not mask[sy, sx] or seen[sy, sx]:
                continue
            stack, blob = [(sy, sx)], []
            seen[sy, sx] = True
            while stack:
                y, x = stack.pop()
                blob.append((y, x))
                for ny, nx in ((y-1, x), (y+1, x), (y, x-1), (y, x+1)):
                    if 0 <= ny < h and 0 <= nx < w and mask[ny, nx] and not seen[ny, nx]:
                        seen[ny, nx] = True
                        stack.append((ny, nx))
            if best is None or len(blob) > len(best):
                best = blob
    out = np.zeros_like(mask, bool)
    if best and len(best) >= MIN_BLOB:
        for y, x in best:
            out[y, x] = True
    return out


def hand_anchor(weapon_mask, body_mask):
    """무기의 **몸 쪽 끝**을 손으로 본다.

    🔑 무기는 손에서 바깥으로 뻗는다. 그러니 몸 중심에 가장 가까운 무기 픽셀들이 자루다.
       칼끝이 아니라 자루를 잡아야 Unity 에서 붙일 기준이 된다.
    """
    ys, xs = np.where(weapon_mask)
    if len(ys) == 0:
        return None
    _, bxs = np.where(body_mask)
    if len(bxs) == 0:
        return None
    order = np.argsort(np.abs(xs - bxs.mean()))[:max(4, len(xs) // 8)]
    return float(xs[order].mean()), float(ys[order].mean())


def extract(armed_path, unarmed_path, out_path=None, keep_all=False):
    ima, a = load(armed_path)
    imb, b = load(unarmed_path)
    if a.shape != b.shape:
        raise SystemExit("🔴 두 장의 크기가 다르다: %s vs %s" % (ima.size, imb.size))

    am, bm = a[:, :, 3] > ALPHA_CUT, b[:, :, 3] > ALPHA_CUT
    raw = am & ~bm
    weapon = raw if keep_all else largest_blob(raw)

    hand = hand_anchor(weapon, bm)
    result = {
        "armed_px": int(am.sum()),
        "unarmed_px": int(bm.sum()),
        "diff_px": int(raw.sum()),
        "weapon_px": int(weapon.sum()),
        "dropped_px": int(raw.sum() - weapon.sum()),
        "hand": hand,
    }
    if weapon.sum():
        ys, xs = np.where(weapon)
        result["bbox"] = (int(xs.min()), int(ys.min()), int(xs.max()), int(ys.max()))

    if out_path and weapon.sum():
        cut = np.zeros_like(a, dtype=np.uint8)
        cut[weapon] = a[weapon]
        Image.fromarray(cut, "RGBA").save(out_path)
        result["out"] = out_path
    return result


def main():
    ap = argparse.ArgumentParser(description="무기 있는 몸에서 무기와 손 앵커를 뽑는다")
    ap.add_argument("armed", help="무기를 든 몸")
    ap.add_argument("unarmed", help="같은 몸, 무기 없음")
    ap.add_argument("--out", default=None, help="뽑은 무기 PNG 경로")
    ap.add_argument("--keep-all", action="store_true",
                    help="가장 큰 덩어리만 남기지 않고 차이 전부를 무기로 본다")
    args = ap.parse_args()

    for p in (args.armed, args.unarmed):
        if not os.path.exists(p):
            raise SystemExit("🔴 파일이 없다: %s" % p)

    r = extract(args.armed, args.unarmed, args.out, args.keep_all)
    print("  불투명    무기있음 %d · 무기없음 %d" % (r["armed_px"], r["unarmed_px"]))
    print("  차이      %d px" % r["diff_px"])
    print("  무기      %d px%s" % (r["weapon_px"],
          "" if args.keep_all else "  (자세 차이 %d px 버림)" % r["dropped_px"]))
    if "bbox" in r:
        x0, y0, x1, y1 = r["bbox"]
        print("  범위      x %d~%d · y %d~%d" % (x0, x1, y0, y1))
    if r["hand"]:
        print("  🔑 손 앵커  (%.1f, %.1f)   <- 무기의 몸 쪽 끝 = 자루" % r["hand"])
    else:
        print("  🔴 손 앵커를 못 찾았다 — 무기 픽셀이 없거나 너무 흩어져 있다")
    if "out" in r:
        print("  → %s" % r["out"])

    if r["weapon_px"] == 0:
        print("\n  ⚠️ 무기가 안 잡혔다. 두 장이 같은 자세·같은 방향인지 확인할 것.")


if __name__ == "__main__":
    main()
