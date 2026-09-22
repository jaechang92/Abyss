# -*- coding: utf-8 -*-
"""**몸은 갈려 없어지고 쇠붙이만 떨어지는** 사망을 조립한다 — 생성기가 못 그리는 것을 손으로 만든다.

    python Tools/ArtPipeline/compose_shed_death.py Art_Source/characters/midboss_sentinel/_r4_southeast.png \
        --out Art_Source/characters/midboss_sentinel/dead_composed

🔴 **왜 이 도구가 생겼나** (2026-09-22 · 감시자 거인 R4)

  사망을 **세 번 생성했고 세 번 다 다른 이유로 실패했다** — v1 은 날 한 자루가 사라졌고,
  v2 는 투구가 몸에서 떨어져 **위로 떠올랐으며**(게다가 폭 153px 로 칸 148 을 넘었다),
  v3 는 끝 두 프레임이 **갈색 무더기로 뭉개져** 칼과 투구가 구별되지 않았다. 12 gen 을 썼다.

  🔑 **PixelLab v3 는 「몸이 사라지고 부품만 남는 것」을 못 그린다.** 캐릭터를 유지하도록 학습된
     모델이라 사라짐을 요구하면 뭉갠다. **방식이 틀렸으므로 프레임을 늘려도 안 된다**
     (메모리 `feedback_reference_before_motion` 과 같은 자리다).

  👉 그래서 **생성이 아니라 조립**으로 만든다. gen 0 이고 프레임마다 완전히 제어된다.

🔑 **이 연출이 이 적의 정체 그 자체다.** 감시자 거인은 **제 아래를 갈아 없앤 것**이고,
   마지막에 **제 전부가 갈려 없어진다.** 갈려도 안 없어지는 것이 **쇠 날과 투구**다.
   🔴 「연기처럼 흩어진다」로 하지 않는 이유는 `10_BIBLE/03-light.md` **L6(공중 파티클 금지)** 다.

공정: ① 열림 연산으로 **얇은 부위 = 날**을 떼고 ② 상자 + 씨앗 번짐으로 **투구**를 떼고
      ③ 나머지 몸을 프레임마다 **한 겹씩 침식**해 없애고 ④ 날·투구를 **중력으로 떨어뜨린다**
      🔑 **투구가 마지막에 얹힌다** — 엘리트 사냥꾼 사망에서 얼굴판이 그랬고 사용자가 그것을 좋게 봤다
"""
import argparse
import os

import numpy as np
from PIL import Image

ALPHA_CUT = 8
NEIGHBOURS8 = [(-1, -1), (-1, 0), (-1, 1), (0, -1), (0, 1), (1, -1), (1, 0), (1, 1)]


def step(mask, keep):
    """4-이웃 침식(keep=True) 또는 팽창(keep=False). 캔버스 밖은 투명으로 본다."""
    out = mask.copy()
    for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)):
        shifted = np.roll(mask, (dy, dx), (0, 1))
        if dy == 1:
            shifted[0, :] = False
        if dy == -1:
            shifted[-1, :] = False
        if dx == 1:
            shifted[:, 0] = False
        if dx == -1:
            shifted[:, -1] = False
        out = (out & shifted) if keep else (out | shifted)
    return out


def components(mask):
    height, width = mask.shape
    seen = np.zeros_like(mask)
    found = []
    for sy, sx in zip(*np.where(mask)):
        if seen[sy, sx]:
            continue
        stack, group = [(sy, sx)], []
        seen[sy, sx] = True
        while stack:
            y, x = stack.pop()
            group.append((y, x))
            for dy, dx in NEIGHBOURS8:
                ny, nx = y + dy, x + dx
                if 0 <= ny < height and 0 <= nx < width and mask[ny, nx] and not seen[ny, nx]:
                    seen[ny, nx] = True
                    stack.append((ny, nx))
        found.append(group)
    return sorted(found, key=len, reverse=True)


def splitParts(alpha, erode, helmetBox, seed):
    """알파를 **날 · 투구 · 몸** 셋으로 가른다.

    🔴 **`erode` 를 잘못 고르면 날이 아니라 어깨가 잡힌다.** 감시자 R4 실측 —
       `erode 12` 의 둘째 성분은 `386px x23~44 **y61~88**` 로 **왼쪽 어깨 파울드론**이었고,
       진짜 왼쪽 칼은 `erode 10` 의 `239px x43~70 **y95~116**` 이다(같은 첫째 성분 `740px` = 오른쪽 칼).
       👉 **성분의 세로 위치를 보고 고른다** — 칼은 아래쪽에 있다. 크기만 보면 어깨를 고른다.

    🔴 **완벽한 분리가 아니어도 된다.** 몸에 남은 날 뿌리는 어차피 몸과 함께 갈려 없어지므로
       「부러져 떨어진 칼」로 읽힌다. 투구만은 **바이저까지** 들어가야 정체가 산다.
    """
    thick = alpha
    for _ in range(erode):
        thick = step(thick, True)
    for _ in range(erode):
        thick = step(thick, False)
    thick &= alpha

    blades = np.zeros_like(alpha)
    for group in components(alpha & ~thick)[:2]:
        for y, x in group:
            blades[y, x] = True

    box = np.zeros_like(alpha)
    box[helmetBox[0]:helmetBox[1], helmetBox[2]:helmetBox[3]] = True
    box &= alpha & ~blades
    helmet = np.zeros_like(alpha)
    helmet[seed[0], seed[1]] = True
    for _ in range(80):
        grown = step(helmet, False) & box
        if grown.sum() == helmet.sum():
            break
        helmet = grown

    return blades, helmet, alpha & ~blades & ~helmet


def cut(a, mask):
    out = np.zeros_like(a)
    out[mask] = a[mask]
    return out


def place(layer, dy, dx):
    moved = np.zeros_like(layer)
    height, width = layer.shape[:2]
    y0, y1 = max(0, dy), min(height, height + dy)
    x0, x1 = max(0, dx), min(width, width + dx)
    moved[y0:y1, x0:x1] = layer[y0 - dy:y1 - dy, x0 - dx:x1 - dx]
    return moved


def over(base, top):
    out = base.copy()
    m = top[..., 3] > ALPHA_CUT
    out[m] = top[m]
    return out


def squash(layer, factor, anchor):
    """**바닥을 축으로 세로로 눌러** 「쓰러져 눕는 것」을 만든다 — 회전의 값싼 대체다.

    🔴 **픽셀아트에서 회전은 못 쓴다** — 비직각 회전은 외곽선을 계단으로 뭉개고 1px 줄(날의 마모 자국)을 끊는다.
    🔑 날은 **끝이 이미 바닥에 닿아 있다.** 눕는다는 것은 **위쪽 끝이 끝점 쪽으로 내려오는 것**이므로,
       끝점을 축으로 세로만 누르면 각도가 눕는 것과 같은 방향으로 읽힌다.
    """
    if factor >= 0.999:
        return layer
    height, width = layer.shape[:2]
    out = np.zeros_like(layer)
    for y in range(height):
        src = anchor - (anchor - y) / factor
        sy = int(round(src))
        if 0 <= sy < height:
            out[y] = layer[sy]
    return out


def bottomOf(mask):
    ys = np.where(mask.any(axis=1))[0]
    return int(ys.max()) if len(ys) else 0


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("source")
    ap.add_argument("--out", required=True)
    ap.add_argument("--frames", type=int, default=9)
    ap.add_argument("--erode", type=int, default=10, help="날을 떼는 열림 반경")
    ap.add_argument("--helmet", default="28,45,53,79", help="투구 상자 y0,y1,x0,x1")
    ap.add_argument("--seed", default="32,63", help="투구 씨앗 y,x")
    ap.add_argument("--lie", type=float, default=0.55, help="날이 다 누웠을 때의 세로 배율")
    ap.add_argument("--minPart", type=int, default=60,
                    help="침식하다 남은 이보다 작은 몸 조각은 지운다(1px 선이 남는다)")
    ap.add_argument("--sink", type=int, default=2, help="갈려 나가는 몸이 프레임마다 가라앉는 px")
    args = ap.parse_args()

    image = Image.open(args.source).convert("RGBA")
    a = np.array(image)
    alpha = a[..., 3] > ALPHA_CUT
    box = tuple(int(v) for v in args.helmet.split(","))
    seed = tuple(int(v) for v in args.seed.split(","))
    blades, helmet, body = splitParts(alpha, args.erode, box, seed)
    print(f"가른 결과 — 날 {blades.sum()}px · 투구 {helmet.sum()}px · 몸 {body.sum()}px")

    ground = bottomOf(alpha)
    bladeLayer, helmetLayer, bodyLayer = cut(a, blades), cut(a, helmet), cut(a, body)
    bladeDrop = ground - bottomOf(blades)
    helmetDrop = ground - bottomOf(helmet) - 3          # 🔑 투구는 날 무더기 **위에** 얹힌다

    # 🔴 침식 단계는 **가속**한다 — 앞은 천천히 갈리고 뒤는 한꺼번에 무너져야 「무게」가 읽힌다.
    peel = [0, 1, 3, 5, 8, 13, 20, 30, 40]
    os.makedirs(args.out, exist_ok=True)
    for i in range(args.frames):
        remaining = body
        for _ in range(peel[min(i, len(peel) - 1)]):
            remaining = step(remaining, True)
        # 🔴 침식은 **1px 실오라기를 남긴다** — 사망에는 dropLoose 를 못 걸므로(떨어진 칼·투구가 지워진다)
        #    여기서 걷는다. 감시자 R4 f5 에 검은 1px 선이 남아 있었다.
        kept = np.zeros_like(remaining)
        for group in components(remaining):
            if len(group) >= args.minPart:
                for y, x in group:
                    kept[y, x] = True
        remaining = kept
        # 🔑 갈려 나가는 몸은 조금씩 **가라앉는다** — 침식만 하면 제자리에서 오그라들어 공중에 뜬 것처럼 보인다.
        frame = place(cut(a, remaining), args.sink * i, 0)

        # 🔑 날이 먼저 떨어지고(1~5) 투구가 나중에 얹힌다(2~7) — 엘리트 사냥꾼 사망과 같은 차례다
        bp = min(max((i - 1) / 4.0, 0.0), 1.0) ** 2
        hp = min(max((i - 2) / 4.0, 0.0), 1.0) ** 2
        # 🔑 날은 **끝이 이미 바닥이라 내려갈 자리가 없다.** 대신 끝을 축으로 눌러 눕힌다.
        fallen = squash(bladeLayer, 1.0 - (1.0 - args.lie) * bp, ground)
        frame = over(frame, place(fallen, int(round(bladeDrop * bp)), 0))
        frame = over(frame, place(helmetLayer, int(round(helmetDrop * hp)), int(round(-4 * hp))))

        out = Image.fromarray(frame, "RGBA")
        out.save(os.path.join(args.out, f"{i}.png"))
        bb = out.getbbox()
        print(f"  f{i}: 몸 남은 {int(remaining.sum()):5d}px · 침식 {peel[min(i, len(peel)-1)]:2d}겹 · "
              f"bbox {bb[2]-bb[0]}x{bb[3]-bb[1]}")


if __name__ == "__main__":
    main()
