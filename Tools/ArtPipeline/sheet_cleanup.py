# -*- coding: utf-8 -*-
"""**지우고 꿰맨다** — 튀는 점·떨어진 조각·잔여선을 걷고, 마지막에 외곽선을 다시 긋는다.

🔴 **왜 나뉘어 있나** (2026-09-22)

  `build_form_sheets.py` 가 **1050줄**이 되어 컨벤션(500줄)을 두 배 넘겼다. 후처리 함수가
  종을 더할 때마다 하나씩 늘어난 것이다(감시자 거인 하나에 네 개가 붙었다).
  **후처리만 떼도 694줄**이라 한 파일로는 또 넘어서, **하는 일로** 넷으로 나눴다.

  🔑 경계를 「시트 단위 vs 프레임 단위」로 잡지 않았다 — 그것은 `buildState` 가 정하는 것이지
     함수의 성격이 아니다(`collapseBand` 는 시트 단위지만 색이고, `normalizeOutline` 은
     프레임 단위지만 정리다).

  🔴 **레시피 키는 한 글자도 안 바뀌었다.** 이 분할은 파일 경계만 옮긴다.

  🔴 **`normalizeOutline` 은 언제나 맨 뒤다.** 앞의 것들이 실루엣을 바꾸므로 **그 뒤에 외곽을
     다시 꿰매야** 한다 — 사용자가 지운 185px 자리에 **외곽선 20px 을 다시 그었다**(2026-09-21).
"""

from collections import Counter
import numpy as np
from PIL import Image

from sheet_common import ALPHA_CUT, outlineMask, rgbToHsv


def eraseRect(image, r):
    """bbox 기준 **사각 영역 안의 픽셀을 지운다** — 생성물이 발밑에 깔아 놓은 바닥 그림자를 걷는다.

    🔴 감시자 거인 R2 에서 사용자가 직접 지운 **149px**(`x17~49 · y92~99`)이 그것이다.
       몸통 축보다 넓게 퍼진 납작한 판이라 **고립도 작은 덩어리도 아니라** 자동 처리가 못 잡았다.

    🔴 **두 번 틀렸다.**
      ① 「행 폭이 국소 최소를 지나 다시 넓어지는 지점 아래」라는 **자동 규칙**은
         기존 7종에 걸어 보니 **엘리트 사냥꾼의 벌어진 다리를 오탐**했다(배율 1.86 대 1.70 — 못 가른다).
      ② 그래서 **행 단위**(`eraseBelow row`)로 바꿨더니 **261px** 를 지웠다 — 사용자의 149px 보다 훨씬 많고,
         같은 행에 걸친 **오른쪽 날 끝까지 날아갔다.**
      👉 **가로 범위까지 받아야 한다.** 그림자는 몸통 아래에만 있고 날은 옆으로 뻗는다.

    레시피 예: "eraseRect": [{"x": [17, 49], "y": [92, 99]}]
    """
    a = np.array(image)
    alpha = a[..., 3] > ALPHA_CUT
    if not alpha.any():
        return image
    ys = np.where(alpha.any(axis=1))[0]
    xs = np.where(alpha.any(axis=0))[0]
    top, left = int(ys.min()), int(xs.min())
    wiped = 0
    for box in r:
        x0, x1 = box["x"]
        y0, y1 = box["y"]
        region = a[top + y0:top + y1 + 1, left + x0:left + x1 + 1]
        wiped += int((region[..., 3] > ALPHA_CUT).sum())
        region[..., 3] = 0
    print(f"  영역 지우기 — {len(r)}개 사각 · {wiped}px")
    return Image.fromarray(a, "RGBA")


def despeckle(image, r):
    """튀는 픽셀을 걷는다 — ① 비슷한 명도의 이웃이 하나도 없는 것 ② 너무 작은 연결 덩어리.

    🔴 감시자 거인 R2 에서 사용자가 **네 번** 지적한 끝에 자리 잡은 기준이다. 내가 틀린 것들:
      - 「무채인가」를 봤는데 봐야 할 것은 **「주변보다 밝은가」**였다(무채 스냅은 색만 바꾸고 밝기는 뒀다)
      - 고립을 **8이웃이 다 찬 것**으로 좁혀, 가장자리의 `0/3`·`0/5`·`0/6` 을 놓쳤다 →
        🔑 **비슷한 이웃이 0 이면 이웃 수와 무관하게 고립이다**
      - 같은 행의 **가로 거리**로 묶었더니 **두 날을 서로 「튄 것」으로 오인**해 줄을 조각냈다 →
        🔑 **연결 성분으로 묶어야 각 날이 자기 덩어리로 남는다**

    레시피 예: "despeckle": {"valueMin": 0.30, "minComponent": 5, "sameBand": 0.12}
    """
    a = np.array(image)
    alpha = a[..., 3] > ALPHA_CUT
    if not alpha.any():
        return image
    hsv = rgbToHsv(a[..., :3].astype(np.float64) / 255.0)
    value = hsv[..., 2]
    valueMin = r.get("valueMin", 0.30)
    sameBand = r.get("sameBand", 0.12)
    minComponent = r.get("minComponent", 5)
    neighbours = [(-1, -1), (-1, 0), (-1, 1), (0, -1), (0, 1), (1, -1), (1, 0), (1, 1)]
    height, width = alpha.shape

    def around(y, x):
        return [(y + dy, x + dx) for dy, dx in neighbours
                if 0 <= y + dy < height and 0 <= x + dx < width and alpha[y + dy, x + dx]]

    # ① 고립 — 비슷한 명도의 이웃이 0 개
    isolated = 0
    for y, x in zip(*np.where(alpha & (value >= valueMin))):
        nb = around(y, x)
        if nb and not any(abs(value[p] - value[y, x]) < sameBand for p in nb):
            a[y, x, :3] = Counter(tuple(int(q) for q in a[p][:3]) for p in nb).most_common(1)[0][0]
            isolated += 1

    # ② 작은 연결 덩어리 — 큰 덩어리(각 날의 줄 · 몸통 명암)는 남는다
    hsv = rgbToHsv(a[..., :3].astype(np.float64) / 255.0)
    value = hsv[..., 2]
    mask = alpha & (value >= valueMin)
    seen = np.zeros_like(mask)
    small = 0
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
        if len(group) >= minComponent:
            continue
        for y, x in group:
            dark = [p for p in around(y, x) if value[p] < valueMin]
            if dark:
                a[y, x, :3] = Counter(tuple(int(q) for q in a[p][:3]) for p in dark).most_common(1)[0][0]
                small += 1
    print(f"  튀는 픽셀 정리 — 고립 {isolated}px · {minComponent}px 미만 덩어리 {small}px")
    return Image.fromarray(a, "RGBA")


def dropLooseParts(image, r):
    """몸에서 **떨어져 나온 조각**을 지운다 — 알파 연결 덩어리 중 가장 큰 것만 남긴다.

    🔴 감시자 거인 이동 v1 f4~f6 에 **몸과 안 닿은 작은 흰 조각**이 떠 있었다.
       `despeckle` 은 **밝은 픽셀의** 덩어리만 보므로 이런 것을 못 잡는다 —
       **알파(실루엣) 자체의 연결**을 봐야 한다.

    🔑 화염 박격포 R1 이 같은 결함으로 기각됐고(공중에 뜬 기와 한 장) 그때 얻은 규칙이
       **「연결 덩어리 수(flood fill)는 싼 검사다」**였다. 그 검사를 조립 단계에 넣은 것이다.

    ⚠️ **조각이 흩어지는 것이 의도인 상태에는 걸지 말 것**(재가 부스러지는 뼈 궁수 등).
       레시피에 넣는 순간 4상태 전부에 걸린다.

    🔴 **절대 픽셀 수로는 못 가른다** — 감시자 거인 이동 f4~f6 의 조각이 **45~85px** 이라
       `minPart 12` 에 안 걸렸다. 몸이 3000px 이므로 그 조각은 **2~3%** 다.
       👉 **가장 큰 덩어리 대비 비율**로 본다.

    레시피 예: "dropLoose": {"maxRatio": 0.05}    # 본체의 이 비율 미만인 별도 덩어리를 지운다
    """
    a = np.array(image)
    alpha = a[..., 3] > ALPHA_CUT
    if not alpha.any():
        return image
    height, width = alpha.shape
    maxRatio = r.get("maxRatio", 0.05)
    minPart = r.get("minPart", 0)
    neighbours = [(-1, -1), (-1, 0), (-1, 1), (0, -1), (0, 1), (1, -1), (1, 0), (1, 1)]

    seen = np.zeros_like(alpha)
    parts = []
    for sy, sx in zip(*np.where(alpha)):
        if seen[sy, sx]:
            continue
        stack, group = [(sy, sx)], []
        seen[sy, sx] = True
        while stack:
            y, x = stack.pop()
            group.append((y, x))
            for dy, dx in neighbours:
                ny, nx = y + dy, x + dx
                if 0 <= ny < height and 0 <= nx < width and alpha[ny, nx] and not seen[ny, nx]:
                    seen[ny, nx] = True
                    stack.append((ny, nx))
        parts.append(group)
    if len(parts) <= 1:
        return image
    parts.sort(key=len, reverse=True)
    body = len(parts[0])
    dropped = 0
    for group in parts[1:]:
        if len(group) >= body * maxRatio and len(group) >= minPart:
            continue                      # 본체에 견줄 만큼 크면 의도된 것일 수 있어 남긴다
        for y, x in group:
            a[y, x, 3] = 0
            dropped += 1
    print(f"  떨어진 조각 지우기 — 덩어리 {len(parts)}개 · {dropped}px")
    return Image.fromarray(a, "RGBA")


def removeStrayLines(image, fromRow=60, darkMax=12):
    """1px 짜리 어두운 잔여선을 걷는다 — 불투명 4방향 이웃이 1개 이하인 어두운 픽셀을 반복해서 지운다.

    📌 그림자 테두리가 호(弧)로 남는 경우용이다(투척사 Idle v1). 몸 외곽선은 안쪽 이웃이 있어 안 지워진다.
    """
    a = np.array(image)
    for _ in range(6):
        opaque = a[:, :, 3] > ALPHA_CUT
        dark = opaque & (a[:, :, :3].max(axis=2) < darkMax)
        kill = []
        h, w = opaque.shape
        for y, x in zip(*np.nonzero(dark)):
            if y < fromRow:
                continue
            neighbours = sum(1 for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1))
                             if 0 <= y + dy < h and 0 <= x + dx < w and opaque[y + dy, x + dx])
            if neighbours <= 1:
                kill.append((y, x))
        if not kill:
            break
        for y, x in kill:
            a[y, x, 3] = 0
    return Image.fromarray(a)


def normalizeOutline(image, spec):
    """외곽선을 한 색으로 통일한다 — 생성물의 외곽에 몸 색이 섞여 나오는 것을 걷는다.

    🔴 감시자 거인 R2 에서 사용자가 지적한 결함이다. 외곽 461px 중 검정 계열이 79.7% 이고
       나머지 20.3%(94px)가 갈색·회색이라 **실루엣이 흐려졌다.**
       튄 픽셀은 **투구 꼭대기(세로 1%)와 밑동(세로 90~100%)에 몰린다.**

    🔑 **밝은 강조점이 외곽에 노출된 것은 남긴다.** 이 적은 날 가장자리의 회백 한 줄이
       종당 하나의 강조점이라 같이 지우면 정체가 사라진다 — `keepBright` 의 세로 구간
       안에 있고 `brightMin` 이상으로 밝은 외곽 픽셀만 통과시킨다.

    레시피 예: "outline": {"ink": "#060405", "keepBright": [0.06, 0.88], "brightMin": 116}
    """
    a = np.array(image)
    alpha = a[:, :, 3] > ALPHA_CUT
    if not alpha.any():
        return image

    edge = outlineMask(alpha)
    ink = spec.get("ink", "#060405").lstrip("#")
    ink = (int(ink[0:2], 16), int(ink[2:4], 16), int(ink[4:6], 16))
    lo, hi = spec.get("keepBright", [0.0, 0.0])
    brightMin = spec.get("brightMin", 116)

    ys = np.where(alpha.any(axis=1))[0]
    top, bottom = int(ys.min()), int(ys.max())
    span = max(bottom - top, 1)

    for y, x in zip(*np.where(edge)):
        pixel = tuple(int(v) for v in a[y, x, :3])
        if pixel == ink:
            continue
        rel = (y - top) / span
        if max(pixel) >= brightMin and lo <= rel <= hi:
            continue            # 강조점이 외곽에 드러난 자리 — 남긴다
        a[y, x, :3] = ink
    return Image.fromarray(a, "RGBA")
