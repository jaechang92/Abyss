# -*- coding: utf-8 -*-
"""
make_palettes.py — palettes/_palettes.txt 의 hex 목록을 PNG 로 굽고, 바이블 규약을 검사한다.

    python Art_Source/50_BUILD/make_palettes.py
    python Art_Source/50_BUILD/make_palettes.py stage1_rift_entrance

산출물이 이 파이프라인의 색 기준이다.

🔴 2026-09-06 — **집행 지점이 바뀌었다.** 예전에는 이 PNG 를 PixelLab 의 color_image 에
   넣는 것이 강제였다. Pro(v2) 에는 그 칸이 없다. 이제 강제는 **생성 뒤**에 일어난다:

       python Tools/ArtPipeline/enforce_palette.py <뽑은것>.png --scene <scene>

   그래서 이 PNG 의 쓰임이 둘로 갈렸다:
     ① 후처리 양자화의 **목표 색** (여기가 강제다)
     ② 웹 도구 Create Tileset 의 Target Palette 칸 (**보조** — 적중률만 올린다)

🔴 그래도 **팔레트가 앵커보다 먼저다.** 이유는 바뀌었다 —
   강제라서가 아니라, 팔레트가 **앵커를 고르는 판정 기준**이기 때문이다.
   기준 없이 뽑은 앵커는 무엇과 비교해 고를지가 없다.

🔑 PIL 을 안 쓴다. 색 몇 개짜리 격자 PNG 는 표준 라이브러리로 충분하고,
   아트 담당자가 의존성 설치 없이 바로 돌릴 수 있는 쪽이 낫다.

검사는 10_BIBLE/04-palette.md 의 규약을 그대로 잰다:
   P1  회색이 순수 무채가 아닐 것 (잔여 채도)
   P4  순검정·순백을 램프 양끝에 쓰지 않을 것
   S3  시너지 축 6색과 명도 또는 채도에서 갈릴 것
"""
import os
import re
import struct
import sys
import zlib

BUILD_DIR = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(BUILD_DIR)                 # Art_Source/
SRC = os.path.join(ROOT, "palettes", "_palettes.txt")
OUT_DIR = os.path.join(ROOT, "palettes")

CELL = 16                    # 색 하나가 차지하는 정사각 픽셀 크기
HEX = re.compile(r"\b([0-9A-Fa-f]{6})\b")

# 🔴 시너지 축 6색 — SoT 는 Assets/Scripts/Runtime/Draft/SynergyAxis.cs 다.
#    아트가 이 값을 정하지 않는다. 코드가 바뀌면 여기도 맞춘다.
#    (10_BIBLE/04-palette.md §2 ② — 색은 폼이 아니라 축을 가리킨다)
AXIS_COLORS = {
    "fire": "FF752E", "abyss": "A36BF5", "guard": "FFCC52",
    "blood_pact": "E6384D", "frost": "73CCFF", "soul": "8CF2B3",
}

SAT_LIMIT = 0.35             # 월드 색의 채도 상한 (지어낸 것 — 앵커 보고 조정)
SAT_FLOOR = 64               # 이 밝기 아래로는 채도를 재지 않는다 (아래 주석)
AXIS_DISTANCE = 60           # 축 색과의 RGB 거리 하한 (지어낸 것)
EDGE_MARGIN = 8              # 순검정·순백으로 치는 여유 (P4)

# P1·P4 를 면제받는 씬. 무채색이 연출이 아니라 설정인 유일한 자리다 —
# 이름을 잃고 아래로 간 자라서 색이 없다. (10_BIBLE/04 §2 ①)
NEUTRAL_EXEMPT = {"hidden_first_fallen"}


def rgb(h):
    return tuple(int(h[i:i + 2], 16) for i in (0, 2, 4))


def saturation(h):
    r, g, b = rgb(h)
    hi, lo = max(r, g, b), min(r, g, b)
    return 0.0 if hi == 0 else (hi - lo) / float(hi)


def distance(a, b):
    return sum((x - y) ** 2 for x, y in zip(rgb(a), rgb(b))) ** 0.5


def parse_palettes(path):
    """[scene] 아래의 hex 를 모은다. # 뒤는 주석이라 잘라 버린다."""
    out, current = {}, None
    with open(path, encoding="utf-8") as f:
        for line in f:
            line = line.split("#", 1)[0].strip()
            if not line:
                continue
            header = re.fullmatch(r"\[([a-z0-9_]+)\]", line)
            if header:
                current = header.group(1)
                out[current] = []
                continue
            if current:
                out[current].extend(HEX.findall(line))
    return {k: v for k, v in out.items() if v}


def check(name, colors):
    """바이블 규약을 잰다. 경고 목록을 돌려준다 — 멈추지는 않는다."""
    warn = []
    exempt = name in NEUTRAL_EXEMPT

    if len(colors) < 6:
        warn.append("색이 %d개뿐이다. 9~13개를 권한다 — 적으면 그림이 납작해진다" % len(colors))

    for h in colors:
        r, g, b = rgb(h)
        up = h.upper()

        # P4 — 순검정·순백을 양끝에 쓰지 않는다 (좁은 명도 대비)
        if max(r, g, b) <= EDGE_MARGIN:
            warn.append("#%s 이 거의 순검정이다 (P4). 명도 대비를 좁게 쓴다" % up)
        if min(r, g, b) >= 255 - EDGE_MARGIN:
            warn.append("#%s 이 거의 순백이다 (P4)" % up)

        # P1 — 회색은 색이 빠진 결과다. 순수 무채가 아니어야 한다
        if not exempt and r == g == b and not (r <= EDGE_MARGIN or r >= 255 - EDGE_MARGIN):
            warn.append("#%s 이 순수 무채다 (P1). 잔여 채도를 남길 것" % up)

        # S3 — 축 6색(신호)과 갈려야 한다
        # ⚠️ 아주 어두운 색은 HSV 채도가 무의미하게 커진다(#0B0E11 이 0.35 로 나온다).
        #    밝기가 SAT_FLOOR 아래면 채도를 재지 않는다 — 어차피 신호 색과 안 헷갈린다.
        if max(r, g, b) >= SAT_FLOOR and saturation(h) > SAT_LIMIT:
            warn.append("#%s 채도가 %.2f 다 (S3, 상한 %.2f). 신호 색과 안 갈린다"
                        % (up, saturation(h), SAT_LIMIT))
        for axis, ah in AXIS_COLORS.items():
            d = distance(h, ah)
            if d < AXIS_DISTANCE:
                warn.append("#%s 이 축 색 %s(#%s) 와 너무 가깝다 (거리 %.0f < %d)"
                            % (up, axis, ah, d, AXIS_DISTANCE))
    return warn


def write_png(path, width, height, rows):
    """rows = [bytes(RGB * width)] * height. 8bit RGB, 무압축 필터."""
    raw = b"".join(b"\x00" + r for r in rows)

    def chunk(tag, data):
        body = tag + data
        return struct.pack(">I", len(data)) + body + struct.pack(">I", zlib.crc32(body))

    header = struct.pack(">IIBBBBB", width, height, 8, 2, 0, 0, 0)
    with open(path, "wb") as f:
        f.write(b"\x89PNG\r\n\x1a\n")
        f.write(chunk(b"IHDR", header))
        f.write(chunk(b"IDAT", zlib.compress(raw, 9)))
        f.write(chunk(b"IEND", b""))


def build_one(name, colors):
    width, height = CELL * len(colors), CELL
    row = b"".join(bytes.fromhex(c) * CELL for c in colors)
    write_png(os.path.join(OUT_DIR, name + ".png"), width, height, [row] * height)
    return width, height


def main():
    if not os.path.exists(SRC):
        raise SystemExit("🔴 팔레트 원본이 없다: %s" % SRC)

    palettes = parse_palettes(SRC)
    wanted = sys.argv[1:] or sorted(palettes)
    total_warn = 0

    for name in wanted:
        if name not in palettes:
            print("  건너뜀 (팔레트 없음): %s" % name)
            continue
        colors = palettes[name]
        w, h = build_one(name, colors)
        print("  %s.png  (%d색, %dx%d)" % (name, len(colors), w, h))
        for line in check(name, colors):
            print("     ⚠️ %s" % line)
            total_warn += 1

    print("\n완료 → %s" % os.path.relpath(OUT_DIR, ROOT))
    print("   ① 후처리 목표 색 — Tools/ArtPipeline/enforce_palette.py --scene <scene>")
    print("   ② 웹 도구 Create Tileset 의 Target Palette 칸 (보조 수단)")
    if total_warn:
        print("\n⚠️ 경고 %d건 — 근거는 10_BIBLE/04-palette.md" % total_warn)
        print("   경고는 실패가 아니다. 어기려면 왜 어기는지 _palettes.txt 주석에 적을 것.")


if __name__ == "__main__":
    main()
