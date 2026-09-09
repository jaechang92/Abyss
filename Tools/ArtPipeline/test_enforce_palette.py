# -*- coding: utf-8 -*-
"""
test_enforce_palette.py — enforce_palette.py 픽스처. 외부 테스트 프레임워크를 안 쓴다.

    python Tools/ArtPipeline/test_enforce_palette.py

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

🔴 이 파일이 존재하는 이유 — 2026-09-09

  2026-09-07 커밋 메시지에 "픽스처 5종 통과"라고 적혀 있었다. 그런데 **그 픽스처는
  저장소에 없었다.** 그 세션에서 손으로 만들어 돌려 보고 지운 것이다. 그래서 오늘
  "기존 픽스처가 안 깨지는지 확인"하려니 확인할 대상이 없었다.

  통과했다는 기록은 남았는데 다시 돌릴 방법이 안 남은 것 — 다음에 이 파일을 고치는
  사람에게는 아무 보호막이 아니다. 그래서 픽스처를 파일로 고정한다.

  ⚠️ 아래 5종(F1~F5)은 그 세션의 서술에서 복원한 것이다. 그때 돌린 그림과 픽셀 단위로
     같지는 않다. **재는 성질**이 같도록 만들었다 (SESSION_HISTORY 2026-09-07 —
     "ΔE 0.0인데 램프 10단 중 2단만 쓴 그림").
"""
import io
import os
import shutil
import sys
import tempfile
from contextlib import redirect_stdout

import numpy as np
from PIL import Image

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import enforce_palette as ep


# ── 픽스처용 램프 ──────────────────────────────────────────────────────────────
# 실제 씬 팔레트를 안 쓴다 — 씬 팔레트는 앞으로 고를 것이 7개 남았고, 그때마다 이 테스트가
# 깨지면 테스트가 아니라 잡음이 된다. 씬 팔레트를 읽는 일 자체는 T0 에서 따로 잰다.
RAMP = ["0B0E11", "141A20", "212A33", "2F3B45", "435059",
        "5E6B72", "7C8A8C", "99A6AC", "BAC4C9", "D2D9DB"]

WHITE = (254, 254, 253)      # PixelLab 컨택트 시트의 거터 색 (실측 #FEFEFD)

TMP = None
FAILED = []
PASSED = 0


def rgb(hex6):
    return tuple(int(hex6[i:i + 2], 16) for i in (0, 2, 4))


def canvas(w, h, fill=(0, 0, 0, 0)):
    a = np.zeros((h, w, 4), dtype=np.uint8)
    a[:, :] = fill
    return a


def save(arr, name):
    path = os.path.join(TMP, name)
    Image.fromarray(arr, "RGBA").save(path)
    return path


def run(path, crop_gutter=False, check_only=True, colors=None):
    """enforce 를 돌리고 (반환값, 찍힌 글, 출력경로) 를 준다. 화면 출력은 삼킨다."""
    buf = io.StringIO()
    out_path = os.path.join(TMP, "out.png")
    with redirect_stdout(buf):
        result = ep.enforce(path, out_path, colors or RAMP,
                            check_only=check_only, crop_gutter=crop_gutter)
    return result, buf.getvalue(), out_path


def check(label, cond, detail=""):
    global PASSED
    if cond:
        PASSED += 1
        print("  ok   %s" % label)
    else:
        FAILED.append(label)
        print("  FAIL %s   %s" % (label, detail))


# ══════════════════════════════════════════════ T0 — 팔레트 읽기 (실데이터)

def t0_palette_source():
    print("T0  _palettes.txt 를 읽는다")
    colors = ep.load_from_txt("stage1_rift_entrance")
    check("stage1 팔레트 10색", len(colors) == 10, "받은 것: %d색" % len(colors))
    check("hex 는 대문자로 정규화", all(c == c.upper() for c in colors))
    # 💡 P4(순검정·순백을 램프 양끝에 안 쓴다)가 색 규약으로 정해 둔 것이
    #    거터 탐지의 **근거**가 된다. 팔레트가 순백을 쓰기 시작하면 이 줄이 먼저 깨진다.
    check("순백이 팔레트에 없다 (P4 — 거터 탐지의 근거)",
          all(min(rgb(c)) < ep.GUTTER_MIN_RGB for c in colors))


# ══════════════════════════════════════════════ F1~F5 — 팔레트 집행 (복원)

def f1_exact_colors():
    print("F1  팔레트 색만 쓴 그림 — 옮길 거리가 없다")
    a = canvas(20, 10)
    for i, c in enumerate(RAMP):
        a[:, i * 2:(i + 1) * 2] = rgb(c) + (255,)
    (de, gut), log, _ = run(save(a, "f1.png"))
    check("평균 ΔE 0.0", abs(de) < 1e-6, "ΔE=%.4f" % de)
    check("램프 10단 전부 사용", "쓰인 색    10 / 10 단" in log)
    check("경고 없음", "🔴" not in log and "⚠️" not in log)
    check("거터 0", gut == 0)


def f2_flat_two_steps():
    print("F2  2단만 쓴 그림 — ΔE 는 만점인데 계조가 죽었다")
    # 📌 2026-09-07 에 실제로 나온 모양. **합격 지표 하나만 보면 이 실패가 통과한다.**
    a = canvas(20, 10)
    a[:, :10] = rgb(RAMP[1]) + (255,)
    a[:, 10:] = rgb(RAMP[8]) + (255,)
    (de, _), log, _ = run(save(a, "f2.png"))
    check("평균 ΔE 0.0", abs(de) < 1e-6, "ΔE=%.4f" % de)
    check("얇은 램프를 잡아낸다", "단만 썼다" in log, log)
    check("안 쓴 색을 적어 준다", "안 쓴 색:" in log)


def f3_far_off_palette():
    print("F3  팔레트 밖으로 한참 나간 그림 — 다시 뽑으라고 해야 한다")
    a = canvas(20, 10, (200, 40, 160, 255))     # 채도 높은 자홍. 무채 램프에 없는 색이다
    (de, _), log, _ = run(save(a, "f3.png"))
    check("평균 ΔE 가 재추첨선을 넘는다", de > ep.REROLL_MEAN_DE, "ΔE=%.1f" % de)
    check("재추첨을 권한다", "다시 뽑을 것" in log)


def f4_all_transparent():
    print("F4  전부 투명 — 재지 않고 건너뛴다")
    (de, gut), log, _ = run(save(canvas(20, 10), "f4.png"))
    check("점수를 안 낸다", de is None)
    check("건너뛴다고 적는다", "전부 투명" in log)
    check("거터로 오인하지 않는다", gut == 0)


def f5_alpha_preserved():
    print("F5  반투명이 섞인 그림 — 알파를 보존하고 불투명만 잰다")
    a = canvas(20, 10, (200, 40, 160, 255))
    a[:, :10, 3] = 0                            # 왼쪽 절반 투명 (색은 팔레트 밖)
    a[:, 10:] = rgb(RAMP[4]) + (255,)           # 오른쪽만 불투명 · 팔레트 색
    (de, _), log, out_path = run(save(a, "f5.png"), check_only=False)
    check("투명 영역은 안 잰다 (ΔE 0.0)", abs(de) < 1e-6, "ΔE=%.4f" % de)
    check("불투명 픽셀 수를 100 으로 센다", "불투명 100 px" in log, log)
    saved = np.asarray(Image.open(out_path).convert("RGBA"))
    check("알파가 그대로 남는다",
          saved[:, :10, 3].max() == 0 and saved[:, 10:, 3].min() == 255)
    check("출력 크기 유지", saved.shape[:2] == (10, 20))


# ══════════════════════════════════════════════ G1~G8 — 흰 거터

def picture(w, h):
    """램프를 세로 줄무늬로 깐 그림. 어느 변도 희지 않다."""
    a = canvas(w, h)
    for x in range(w):
        a[:, x] = rgb(RAMP[x % len(RAMP)]) + (255,)
    return a


def paint_gutter(a, top=0, bottom=0, left=0, right=0):
    h, w = a.shape[:2]
    if top:
        a[:top, :] = WHITE + (255,)
    if bottom:
        a[h - bottom:, :] = WHITE + (255,)
    if left:
        a[:, :left] = WHITE + (255,)
    if right:
        a[:, w - right:] = WHITE + (255,)
    return a


def g1_no_gutter():
    print("G1  거터 없는 그림 — 한 줄도 안 뗀다")
    peel, cap = ep.find_gutter(picture(40, 20).astype(np.float64))
    check("네 변 모두 0", sum(peel.values()) == 0, str(peel))
    check("상한에 안 걸린다", not cap)


def g2_two_sides():
    print("G2  위 3 · 오 2 — 붙는 변이 후보마다 다르다")
    a = paint_gutter(picture(40, 20), top=3, right=2)
    peel, _ = ep.find_gutter(a.astype(np.float64))
    check("정확히 위 3 · 오 2",
          peel == {"top": 3, "bottom": 0, "left": 0, "right": 2}, str(peel))


def g3_uneven_thickness():
    print("G3  두께가 변마다 다르다 — 고정 픽셀로는 못 자른다")
    a = paint_gutter(picture(40, 20), top=5, bottom=2, right=1)
    peel, _ = ep.find_gutter(a.astype(np.float64))
    check("위5 아래2 왼0 오1",
          peel == {"top": 5, "bottom": 2, "left": 0, "right": 1}, str(peel))
    check("사람이 읽는 문구", ep.describe_peel(peel) == "위 5 · 아래 2 · 오 1",
          ep.describe_peel(peel))


def g4_not_enough_white():
    print("G4  변의 80%만 흰 줄 — 거터가 아니라 그림이다")
    a = picture(40, 20)
    a[0, :32] = WHITE + (255,)                  # 40칸 중 32칸 = 80% < 90%
    peel, _ = ep.find_gutter(a.astype(np.float64))
    check("안 뗀다", sum(peel.values()) == 0, str(peel))


def g5_transparent_margin():
    print("G5  투명 여백 — 거터가 아니다 (프롭 컷아웃의 정렬을 지킨다)")
    a = picture(40, 20)
    a[:2, :] = (255, 255, 255, 0)               # 희지만 투명하다
    peel, _ = ep.find_gutter(a.astype(np.float64))
    check("투명은 안 센다", sum(peel.values()) == 0, str(peel))


def g6_crop_changes_measurement():
    print("G6  자르면 크기가 줄고 ΔE 에서 순백이 빠진다")
    a = paint_gutter(picture(40, 20), top=3, bottom=1, left=2)
    path = save(a, "g6.png")

    (de_off, gut_off), log_off, _ = run(path, crop_gutter=False)
    (de_on, gut_on), log_on, out_on = run(path, crop_gutter=True, check_only=False)

    check("안 자르면 거터를 알린다", "흰 거터가 붙어 있다" in log_off, log_off)
    check("안 자르면 ΔE 가 순백에 오염된다", de_off > 1.0, "ΔE=%.1f" % de_off)
    check("자르면 ΔE 0.0", abs(de_on) < 1e-6, "ΔE=%.4f" % de_on)
    check("자른 사실과 전후 크기를 적는다",
          "거터 제거" in log_on and "40x20 → 38x16" in log_on, log_on)
    check("뗀 줄 수를 돌려준다", gut_off == 6 and gut_on == 6,
          "%s / %s" % (gut_off, gut_on))
    saved = np.asarray(Image.open(out_on).convert("RGBA"))
    check("출력이 실제로 작아졌다", saved.shape[:2] == (16, 38), str(saved.shape))
    check("순백이 한 픽셀도 안 남았다",
          int((saved[..., :3] >= ep.GUTTER_MIN_RGB).all(axis=2).sum()) == 0)


def g7_all_white():
    print("G7  전부 흰 이미지 — 판정을 포기하고 사람에게 넘긴다")
    a = canvas(20, 10, WHITE + (255,))
    peel, _ = ep.find_gutter(a.astype(np.float64))
    check("None 을 준다", peel is None, str(peel))
    (de, gut), log, _ = run(save(a, "g7.png"), crop_gutter=True)
    check("멈추지 않고 알린다", "네 변이 다 희다" in log, log)
    check("안 자른다", gut == 0)


def g8_peel_cap():
    print("G8  상한까지 뗐는데 아직 희다 — 거터가 아니라 그림일 수 있다")
    a = paint_gutter(picture(40, 40), top=ep.GUTTER_MAX_PEEL + 4)
    peel, cap = ep.find_gutter(a.astype(np.float64))
    check("상한에서 멈춘다", peel["top"] == ep.GUTTER_MAX_PEEL, str(peel))
    check("멈춘 변을 알린다", cap == {"top"}, str(cap))
    _, log, _ = run(save(a, "g8.png"), crop_gutter=True)
    check("사람에게 넘긴다", "떼고도 아직 희다" in log, log)


# ══════════════════════════════════════════════ R1 — 실데이터

def r1_real_anchor():
    print("R1  확정된 stage1 앵커 — 손으로 이미 자른 것이라 뗄 것이 없어야 한다")
    path = os.path.join(ep.REPO, "Art_Source", "anchors", "stage1_rift_entrance.png")
    if not os.path.exists(path):
        print("  skip 앵커가 없다: %s" % ep.short(path))
        return
    arr = np.asarray(Image.open(path).convert("RGBA")).astype(np.float64)
    peel, cap = ep.find_gutter(arr)
    check("거터 0 (오탐이 없다)", peel is not None and sum(peel.values()) == 0, str(peel))
    check("상한 경고 없음", not cap)


CASES = (t0_palette_source,
         f1_exact_colors, f2_flat_two_steps, f3_far_off_palette,
         f4_all_transparent, f5_alpha_preserved,
         g1_no_gutter, g2_two_sides, g3_uneven_thickness,
         g4_not_enough_white, g5_transparent_margin,
         g6_crop_changes_measurement, g7_all_white, g8_peel_cap,
         r1_real_anchor)


def main():
    global TMP
    TMP = tempfile.mkdtemp(prefix="enforce_pal_")
    print("픽스처 임시 폴더: %s" % TMP)
    print()
    try:
        for fn in CASES:
            fn()
            print()
    finally:
        shutil.rmtree(TMP, ignore_errors=True)

    if FAILED:
        print("🔴 실패 %d건 / 통과 %d건" % (len(FAILED), PASSED))
        for f in FAILED:
            print("   - %s" % f)
        return 1
    print("🟢 전부 통과 — %d건" % PASSED)
    return 0


if __name__ == "__main__":
    sys.exit(main())
