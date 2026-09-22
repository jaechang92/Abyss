# -*- coding: utf-8 -*-
"""**밝은 면(강조점)을 다룬다** — 종당 하나뿐인 강조점이 끊기거나 비거나 지글거리는 것을 고친다.

🔴 **왜 나뉘어 있나** (2026-09-22)

  `build_form_sheets.py` 가 **1050줄**이 되어 컨벤션(500줄)을 두 배 넘겼다. 후처리 함수가
  종을 더할 때마다 하나씩 늘어난 것이다(감시자 거인 하나에 네 개가 붙었다).
  **후처리만 떼도 694줄**이라 한 파일로는 또 넘어서, **하는 일로** 넷으로 나눴다.

  🔑 경계를 「시트 단위 vs 프레임 단위」로 잡지 않았다 — 그것은 `buildState` 가 정하는 것이지
     함수의 성격이 아니다(`collapseBand` 는 시트 단위지만 색이고, `normalizeOutline` 은
     프레임 단위지만 정리다).

  🔴 **레시피 키는 한 글자도 안 바뀌었다.** 이 분할은 파일 경계만 옮긴다.

  🔑 **판정 지표는 「하이라이트 연결 성분 수 = 날 개수」다.** 「색 전환율」 같은 지표는
     **뭉개도 낮아져서** 더 나쁜 그림이 더 좋은 값을 낸다(감시자 거인 R2 사용자 수정본 대조).

  🔴 **셋의 순서가 있다** — `wearBladeEdge`(없는 줄을 낸다) → `bridgeHighlight`(있는 줄을 잇는다)
     → `smoothAlongAxis`(이은 줄을 축 방향으로 고른다). 씨앗이 없으면 이을 것도 없다.
"""

from collections import Counter
import numpy as np
from PIL import Image

from sheet_common import ALPHA_CUT, outlineMask, rgbToHsv


def wearBladeEdge(image, r):
    """마모 자국(회백 한 줄)이 **한쪽 날에만** 난 것을 반대쪽 날에도 낸다.

    🔴 감시자 거인 R4 에서 나온 기능이다(2026-09-22). 몸 생성물의 **오른쪽 날에 밝은 픽셀이 0개**(최대 L 30 ·
       왼쪽은 78)라 32프레임 중 27개에서 하이라이트 성분이 **1개**였다(목표 2 = 날 개수).
       🔑 `bridgeHighlight` 는 **씨앗이 있어야 잇는** 도구라 무에서는 못 만든다 — 그래서 이것이 따로 있다.

    🔑 **왜 후처리가 정당한가** — `10_BIBLE/03-light.md` **L1(광원을 그리지 않는다) · L2(방향 그림자를
       그리지 않는다)** 가 이 세계에 **빛의 방향이 없다**고 정한다. 「한쪽 날만 빛을 받는다」는 성립하지 않는다.
       게다가 이 줄은 반사광이 아니라 **마모 자국**이다(`midboss_sentinel.md` ③ *"백 년 땅을 쓸어 거기만 갈렸다"*)
       — **마모는 양쪽 날에 똑같이 난다.** 한쪽만 있는 것은 결함이지 빛이 아니다.

    공정: ① **열림 연산**(침식 후 팽창)으로 두꺼운 부위 = **몸통**을 떼고, 나머지를 얇은 부위 = **날**로 본다
          ② 날 성분마다 이미 밝은 픽셀이 `minSeed` 이상이면 **건너뛴다** — 있는 줄은 안 건드린다
          ③ 없으면 그 날의 **주축(PCA)** 을 구하고, 주축에 수직인 두 가장자리 중
             **몸통 반대쪽** 경계만 기존 하이라이트 색으로 칠한다

    🔴 **경계 한 줄만 칠한다.** 면을 칠하면 날이 납작해진다 — `collapseBand` 의 `to:"down"` 이 저지른 실패와 같다.
    🔴 **자세가 비대칭이라 「거울상」으로는 못 찾는다.** 공격 프레임에서 날이 머리 위로 서므로 좌우 대칭이 깨진다.

    레시피 예: "bladeWear": {"value": 0.60, "erode": 3, "minBlade": 60, "minSeed": 8}
    """
    a = np.array(image)
    alpha = a[..., 3] > ALPHA_CUT
    if not alpha.any():
        return image
    height, width = alpha.shape
    value = rgbToHsv(a[..., :3].astype(np.float64) / 255.0)[..., 2]
    threshold = r.get("value", 0.60)
    erode = r.get("erode", 3)
    minBlade = r.get("minBlade", 60)
    minSeed = r.get("minSeed", 8)

    bright = alpha & (value >= threshold)
    if not bright.any():
        print("  날 마모 — 기준이 될 하이라이트가 한 줄도 없다, 건너뛴다")
        return image
    ink = Counter(tuple(int(q) for q in a[y, x, :3])
                  for y, x in zip(*np.where(bright))).most_common(1)[0][0]

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

    thick = alpha
    for _ in range(erode):
        thick = step(thick, True)
    for _ in range(erode):
        thick = step(thick, False)
    thick &= alpha
    thin = alpha & ~thick
    if not thick.any() or not thin.any():
        print("  날 마모 — 몸통과 날이 안 갈린다(erode 를 다시 볼 것), 건너뛴다")
        return image

    neighbours = [(-1, -1), (-1, 0), (-1, 1), (0, -1), (0, 1), (1, -1), (1, 0), (1, 1)]
    seen = np.zeros_like(thin)
    blades = []
    for sy, sx in zip(*np.where(thin)):
        if seen[sy, sx]:
            continue
        stack, group = [(sy, sx)], []
        seen[sy, sx] = True
        while stack:
            y, x = stack.pop()
            group.append((y, x))
            for dy, dx in neighbours:
                ny, nx = y + dy, x + dx
                if 0 <= ny < height and 0 <= nx < width and thin[ny, nx] and not seen[ny, nx]:
                    seen[ny, nx] = True
                    stack.append((ny, nx))
        if len(group) >= minBlade:
            blades.append(group)

    edge = outlineMask(alpha)
    body = np.array(np.where(thick), dtype=float).mean(axis=1)
    painted = skipped = 0
    for group in blades:
        if sum(1 for y, x in group if bright[y, x]) >= minSeed:
            skipped += 1
            continue                                  # 이미 줄이 있는 날 — 안 건드린다
        pts = np.array(group, dtype=float)
        centre = pts.mean(axis=0)
        spread = pts - centre
        if len(pts) < 3:
            continue
        _, vectors = np.linalg.eigh(np.cov(spread.T))
        axis = vectors[:, -1]                          # 가장 긴 축 = 날이 뻗은 방향
        perp = np.array([-axis[1], axis[0]])           # 그에 수직 = 날의 두께 방향
        toBody = float((body - centre) @ perp)
        side = 1.0 if toBody >= 0 else -1.0            # 몸통이 있는 쪽
        for y, x in group:
            if not edge[y, x]:
                continue
            if (np.array([y, x], dtype=float) - centre) @ perp * side >= 0:
                continue                               # 몸통 쪽 가장자리 — 바깥이 아니다
            if tuple(int(q) for q in a[y, x, :3]) != ink:
                a[y, x, :3] = ink
                painted += 1
    print(f"  날 마모 — 날 {len(blades)}개 중 {skipped}개는 이미 줄이 있다 · 칠한 {painted}px")
    return Image.fromarray(a, "RGBA")


def bridgeHighlight(image, r):
    """끊긴 하이라이트 줄을 **이어 붙인다** — 성분 수가 목표보다 많으면 가장 가까운 쌍을 잇는다.

    🔴 사용자가 직접 고친 것과 대조해서 나온 기능이다(감시자 거인 R2 · 2026-09-21).
       사용자가 바꾼 110px 중 **94px 가 「최고 밝기로 올린 것」**이었고, 그중에는
       **어두운 몸통색 25px 과 외곽선 6px** 까지 있었다 — **줄을 잇기 위해서였다.**

    🔑 **나는 반대로 했다.** 중간톤을 몸통색으로 내려(`collapse down`) 줄을 **얇게** 만들었고,
       그 결과 하이라이트 성분이 **3개로 끊긴 채**였다(사용자 2개 = 날마다 하나).
       👉 **하이라이트는 내려서 없애는 것이 아니라 올려서 잇는다.**

    📌 **판정 지표도 여기서 나온다** — 「날 영역 색 전환율」은 **뭉개도 낮아져서** 내 쪽이 더 낮았는데
       그림은 내 쪽이 더 나빴다. **성분 수(= 날 개수)**가 옳은 지표다.

    레시피 예: "bridgeHighlight": {"value": 0.60, "expect": 2, "minSeed": 8, "maxGap": 10}
    """
    a = np.array(image)
    alpha = a[..., 3] > ALPHA_CUT
    if not alpha.any():
        return image
    height, width = alpha.shape
    value = rgbToHsv(a[..., :3].astype(np.float64) / 255.0)[..., 2]
    threshold = r.get("value", 0.60)
    expect = r.get("expect", 2)
    minSeed = r.get("minSeed", 8)
    maxGap = r.get("maxGap", 10)
    neighbours = [(-1, -1), (-1, 0), (-1, 1), (0, -1), (0, 1), (1, -1), (1, 0), (1, 1)]

    def components():
        mask = alpha & (value >= threshold)
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
                for dy, dx in neighbours:
                    ny, nx = y + dy, x + dx
                    if 0 <= ny < height and 0 <= nx < width and mask[ny, nx] and not seen[ny, nx]:
                        seen[ny, nx] = True
                        stack.append((ny, nx))
            if len(group) >= minSeed:
                found.append(group)
        return sorted(found, key=len, reverse=True)

    groups = components()
    ink = Counter(tuple(int(q) for q in a[y, x, :3])
                  for g in groups for y, x in g).most_common(1)[0][0] if groups else None
    joined = 0
    # 🔴 <b>루프 가드</b> — 잇고 나서도 성분 수가 안 줄면 무한히 돈다.
    #    감시자 거인에서 `freezeBelow` 가 하단을 덮어 하이라이트가 끊기자 실제로 20분 넘게 멈췄다.
    #    성분 수가 줄지 않으면 즉시 포기한다(잇는 것이 목적이지 반드시 이뤄야 하는 것은 아니다).
    rounds = 0
    while ink is not None and len(groups) > expect and rounds < 16:
        rounds += 1
        before = len(groups)
        best = None
        for i in range(len(groups)):
            for j in range(i + 1, len(groups)):
                for p in groups[i]:
                    for q in groups[j]:
                        d = abs(p[0] - q[0]) + abs(p[1] - q[1])
                        if best is None or d < best[0]:
                            best = (d, p, q)
        if best is None or best[0] > maxGap:
            break
        _, (ay, ax), (by, bx) = best
        steps = max(abs(by - ay), abs(bx - ax))
        for t in range(steps + 1):                      # 두 성분 사이를 직선으로 채운다
            y = int(round(ay + (by - ay) * t / max(steps, 1)))
            x = int(round(ax + (bx - ax) * t / max(steps, 1)))
            if alpha[y, x] and tuple(int(q) for q in a[y, x, :3]) != ink:
                a[y, x, :3] = ink
                joined += 1
        value = rgbToHsv(a[..., :3].astype(np.float64) / 255.0)[..., 2]
        groups = components()
        if len(groups) >= before:
            break                      # 이었는데도 안 줄었다 — 더 돌아도 소용없다
    print(f"  하이라이트 잇기 — 성분 {len(groups)}개(목표 {expect}) · 채운 {joined}px")
    return Image.fromarray(a, "RGBA")


def smoothAlongAxis(image, r):
    """긴 날 같은 **띠 모양 부위**를 제 축 방향으로 고른다 — 「지글거림」을 없앤다.

    🔴 감시자 거인 R2 에서 **점을 지우는 것만으로는 끝나지 않았다.** 날의 명암이
       **띠(band)가 아니라 점묘(dither)로 찍혀** 있어, 고립 픽셀을 지우고 대역을 흡수해도
       남은 것들이 계속 지글거렸다(사용자 6회 지적).

    🔑 픽셀 아트에서 대각선 칼날은 **축을 따라 평행한 밴드**여야 한다.
       그래서 **축 방향으로 최빈색을 고르면** 축을 따라 색이 이어지고 축에 수직인 노이즈가 사라진다.

    공정: ① 하이라이트(`seedValue` 이상)의 연결 성분을 날의 **씨앗**으로 잡고
          ② 성분의 주축을 PCA 로 구한 뒤 ③ 축 수직으로 `span` 만큼 넓혀 **날 영역**을 만들고
          ④ 그 안의 픽셀을 **축 방향 ±`reach` 이웃의 최빈색**으로 바꾼다.

    🔴 **프레임 단위다** — 이 적은 회전베기라 **프레임마다 날 각도가 바뀐다.** 축을 프레임마다 다시 구해야 한다.
    ✅ **몸통은 안 걸린다** — 몸통에는 20px 넘는 긴 하이라이트 줄이 없다.
    ✅ **외곽선은 건드리지 않는다**(`outlineBelow` 아래는 표본에서도 빼고 대상에서도 뺀다).

    레시피 예: "bladeSmooth": {"seedValue": 0.60, "minSeed": 20, "span": 6, "reach": 2}
    """
    a = np.array(image)
    alpha = a[..., 3] > ALPHA_CUT
    if not alpha.any():
        return image
    height, width = alpha.shape
    value = rgbToHsv(a[..., :3].astype(np.float64) / 255.0)[..., 2]
    seedValue = r.get("seedValue", 0.60)
    minSeed = r.get("minSeed", 20)
    span = r.get("span", 6)
    reach = r.get("reach", 2)
    outlineBelow = r.get("outlineBelow", 0.10)
    neighbours = [(-1, -1), (-1, 0), (-1, 1), (0, -1), (0, 1), (1, -1), (1, 0), (1, 1)]

    seedMask = alpha & (value >= seedValue)
    seen = np.zeros_like(seedMask)
    seeds = []
    for sy, sx in zip(*np.where(seedMask)):
        if seen[sy, sx]:
            continue
        stack, group = [(sy, sx)], []
        seen[sy, sx] = True
        while stack:
            y, x = stack.pop()
            group.append((y, x))
            for dy, dx in neighbours:
                ny, nx = y + dy, x + dx
                if 0 <= ny < height and 0 <= nx < width and seedMask[ny, nx] and not seen[ny, nx]:
                    seen[ny, nx] = True
                    stack.append((ny, nx))
        if len(group) >= minSeed:
            seeds.append(group)

    touched = 0
    for group in seeds:
        points = np.array(group, dtype=float)
        centred = points - points.mean(axis=0)
        weights, vectors = np.linalg.eigh(np.cov(centred.T))
        axis = vectors[:, int(np.argmax(weights))]
        axis = axis / np.linalg.norm(axis)
        perp = np.array([-axis[1], axis[0]])

        band = np.zeros_like(alpha)
        for py, px in group:
            for t in range(-span, span + 1):
                y = int(round(py + perp[0] * t))
                x = int(round(px + perp[1] * t))
                if 0 <= y < height and 0 <= x < width and alpha[y, x] and value[y, x] >= outlineBelow:
                    band[y, x] = True

        for y, x in zip(*np.where(band)):
            samples = []
            for t in range(-reach, reach + 1):
                sy = int(round(y + axis[0] * t))
                sx = int(round(x + axis[1] * t))
                if 0 <= sy < height and 0 <= sx < width and alpha[sy, sx] and value[sy, sx] >= outlineBelow:
                    samples.append(tuple(int(q) for q in a[sy, sx, :3]))
            if len(samples) < 3:
                continue
            best = Counter(samples).most_common(1)[0][0]
            if best != tuple(int(q) for q in a[y, x, :3]):
                a[y, x, :3] = best
                touched += 1
    print(f"  축 방향 고르기 — 띠 {len(seeds)}개 · {touched}px")
    return Image.fromarray(a, "RGBA")
