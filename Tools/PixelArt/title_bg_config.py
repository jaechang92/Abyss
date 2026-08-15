"""
title_bg_config.py
타이틀 협곡 배경 생성기의 공용 상수와 색 헬퍼. 진입점은 generate_title_bg.py.

<b>여기 있는 수치는 대부분 런타임(TitleBackdrop)·빌더(TitleSceneBuilder.Backdrop)와
짝이 맞아야 한다.</b> 어느 한쪽만 고치면 소실점이 어긋나거나 액자가 드러난다.
문서: Docs/technical/title-backdrop.md
"""

import os

from PIL import Image, ImageChops

OUTPUT_DIR = os.path.join(os.path.dirname(__file__), "..", "..", "Assets", "Art", "Backgrounds", "Title")
OUTPUT_DIR = os.path.normpath(OUTPUT_DIR)

SEED = 20260815

SCREEN_W, SCREEN_H = 1920, 1080

# 길바닥 무늬는 바닥선 아래만 쓴다. 바닥선 비율만 맞추면 런타임이 절벽과 같은 배율
# 스케줄로 돌릴 수 있다(피벗이 곧 소실점이라 거기서부터 커진다).
MARK_W, MARK_H = 2432, 900
MARK_GROUND_RATIO = 0.12

# 하늘·길은 카메라가 흔들려도 가장자리가 비면 안 되므로 화면보다 넓게 굽는다.
# ⚠️ TitleSceneBuilder.DRIFT_MARGIN과 같은 값이어야 한다. 여기서 여백째로 구워야
#    소실점이 텍스처 안 절대 위치에 고정된다 — Unity 쪽에서 RectTransform만 넓히면
#    소실점이 여백의 절반만큼 밀려 관문의 소실점과 어긋난다.
DRIFT_MARGIN = 120

# 화면에서 소실점이 놓이는 세로 비율. title_sky의 광원·title_horizon의 능선과 맞춘다.
VANISH_RATIO = 0.545

# 길이 소실점 아래로 벌어지는 기울기(세로 1px당 반폭). 길·길바닥 무늬·절벽 밑동이
# 모두 이 하나의 원뿔 위에 있어야 하므로 상수로 뽑아 공유한다.
ROAD_SPREAD = (SCREEN_W * 0.34) / (SCREEN_H * (1.0 - VANISH_RATIO))


# ── 절벽 기하 ──────────────────────────────────────────────
#
# <b>절벽은 '한 깊이의 판'이 아니라 깊이에 흩어진 조각(slab)이다.</b>
# 판 하나로 협곡 단면을 그리면 확대될 때 표면이 균일하게 커질 뿐이라 도프 줌과 구별되지
# 않는다. 벽면이 <b>스스로 안쪽으로 물러나야</b> 협곡을 걷는 것으로 읽히고, 그건 깊이가
# 다른 조각을 여러 장 겹쳐야 나온다(Out Run 계열 스케일러가 노변 물체를 그리던 방식).
#
# 조각 하나는 소실점 기준으로 이렇게 놓인다(배율 s):
#   밑동 안쪽 모서리 = ( ±WALL_SIDE·s , WALL_DROP·s 아래 )
#   즉 <b>피벗이 길 가장자리 광선 위</b>에 있다. WALL_SIDE = ROAD_SPREAD·WALL_DROP 이
#   그 조건이고, 이래야 조각마다 밑동이 어긋나 계단이 생기지 않는다.
#
# 위·아래 경계도 소실점에서 뻗는 광선을 따라야 한다 — 가로로 자르면 조각마다
# 계단이 생긴다(관문판에서 겪은 그 문제다).
#   밑동 기울기(로컬 dy/dx) =  1/ROAD_SPREAD
#   능선 기울기(로컬 dy/dx) = (WALL_DROP - WALL_HEIGHT)/WALL_SIDE   ← 바깥으로 갈수록 위
WALL_DROP = 240.0                        # 배율 1에서 밑동이 소실점 아래로 내려간 거리
WALL_SIDE = ROAD_SPREAD * WALL_DROP      # 배율 1에서의 좌우 벽 위치(= 길 가장자리)
WALL_HEIGHT = 780.0                      # 안쪽 모서리에서 잰 벽 높이

BASE_SLOPE = 1.0 / ROAD_SPREAD
RIM_SLOPE = (WALL_DROP - WALL_HEIGHT) / WALL_SIDE

# 피벗에서 바깥으로 뻗는 거리. <b>굽이 진폭의 상한이 여기서 나온다.</b>
# 이웃한 두 조각의 밑동이 이어지려면
#   CLIFF_REACH >= WALL_SIDE·(r-1) + (굽이로 인한 이웃 간 어긋남)
# 이어야 한다. r = (SCALE_MAX/SCALE_MIN)^(1/조각수) 이고, 굽이 항은
# TitleBackdrop.Path.cs의 진폭이 정한다(현재 실측 최대 ≒ 225px).
CLIFF_REACH = 520
CLIFF_MARGIN = 40                        # 안쪽 실루엣이 안으로 삐죽 나올 여유
CLIFF_W = CLIFF_REACH + CLIFF_MARGIN
CLIFF_PIVOT_X = CLIFF_MARGIN
CLIFF_PIVOT_Y = int(WALL_HEIGHT - RIM_SLOPE * CLIFF_REACH) + 24
CLIFF_H = CLIFF_PIVOT_Y + int(BASE_SLOPE * CLIFF_REACH) + 24

# ── 심연 팔레트 ────────────────────────────────────────────
SKY_TOP = (6, 6, 13)
SKY_MID = (16, 14, 30)
SKY_LOW = (32, 27, 55)
GLOW = (96, 74, 142)

# 절벽은 밝은 실루엣 — 런타임 tint가 이 값을 어둡게 곱한다.
CLIFF_FACE = (96, 91, 122)         # 벽면 중턱
CLIFF_RIM = (196, 188, 236)        # 능선 하이라이트
CLIFF_BASE = (22, 20, 31)          # 밑동 그늘
CLIFF_GRAIN_LIT = (172, 162, 212)  # 벽면 결 — 지나가는 것이 보이게 하는 유일한 요소
CLIFF_GRAIN_DARK = (16, 14, 23)

ROAD_MARK_LIT = (132, 122, 172)
ROAD_MARK_DARK = (12, 11, 18)

HORIZON_FAR = (58, 53, 92)
HORIZON_NEAR = (40, 36, 66)
HORIZON_RIM = (104, 92, 152)

FOG = (86, 76, 126)


# ── 헬퍼 ──────────────────────────────────────────────────

def ensure_dir(path):
    os.makedirs(path, exist_ok=True)


def lerp3(a, b, t):
    return tuple(int(round(a[i] + (b[i] - a[i]) * t)) for i in range(3))


def ease(t):
    return t * t * (3.0 - 2.0 * t)


def apply_vertical_ramp(img, value_at):
    """세로 위치별 계수(0~1)를 이미지 알파에 곱한다.

    경계를 없애는 데 반복해서 쓰인다 — 관문 밑동, 벽면 결, 길의 윗변 모두 <b>뚝 끊기면
    가로선이 남는</b> 자리다. 한 줄짜리 램프를 만들어 늘리는 편이 픽셀 루프보다 빠르다.
    """
    w, h = img.size
    ramp = Image.new("L", (1, h), 255)
    for y in range(h):
        ramp.putpixel((0, y), int(255 * max(0.0, min(1.0, value_at(y)))))
    img.putalpha(ImageChops.multiply(img.getchannel("A"), ramp.resize((w, h))))
    return img
