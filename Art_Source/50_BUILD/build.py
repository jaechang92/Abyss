# -*- coding: utf-8 -*-
"""
build.py — 30_BLOCKS + 40_TOOLS + 20_SUBJECTS 를 합쳐 PixelLab 생성 요청을 만든다.

    python Art_Source/50_BUILD/build.py                       # 전 씬
    python Art_Source/50_BUILD/build.py stage1_rift_entrance

산출물은 Art_Source/assembled/ 아래에 나온다 (git 비추적 — 언제든 다시 만들어진다).
  <scene>__<part>.json   API/MCP 로 바로 보낼 수 있는 요청 본문
  <scene>.md             웹 앱에 손으로 옮겨 담을 때 보는 표
  00_ALL.md              전체 카탈로그

이 스크립트의 역할은 조립만이 아니다. 검사가 셋 있다:

  ① 한글 유출 차단
     주석은 한글로 쓰되 프롬프트 본문에는 한 글자도 섞이면 안 된다.
     gen2 에서 실제로 겪은 사고다 — 주석이 조립본에 딸려 들어가면
     모델이 그걸 무시하지 않고 "한국어가 적힌 그림"을 그리려 든다.

  ② 문구 길이 검사
     NovelAI 는 태그를 쌓을수록 세졌지만 PixelLab 은 짧은 설명을 전제로 한다 —
     길면 서로 희석되어 아무것도 안 걸린다.

  ③ 🔴 금지 어휘 검사 (이번에 새로 붙는 것)
     gen1~3 이 전부 샜던 자리다. `dark fantasy underground ruins` 가
     소설에서 온 것인지 장르 관습인지 파일만 보고는 알 수 없었다.
     이제 30_BLOCKS/00_core.txt [FORBIDDEN] 에 걸리면 빌드가 멈춘다.
     부정 프롬프트는 부탁이고 이건 검사다.
"""
import json
import os
import re
import sys

BUILD_DIR = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(BUILD_DIR)                       # Art_Source/
CORE_FILE = os.path.join(ROOT, "30_BLOCKS", "00_core.txt")
PART_DIR = os.path.join(ROOT, "30_BLOCKS", "parts")
PARAMS_FILE = os.path.join(ROOT, "40_TOOLS", "pixellab", "params.txt")
SCENE_DIR = os.path.join(ROOT, "20_SUBJECTS", "scenes")
OUT_DIR = os.path.join(ROOT, "assembled")

# 🔴 씬 파일은 사람이 읽는 .md 이고 빌드 입력이 그 안에 같이 있다.
#    한 대상에 두 파일을 두면 반드시 갈린다 — 규약이 두 곳에 파편화되는 것을 막는다.
#    이 표시 줄 **아래**만 블록으로 읽는다. 위쪽 산문은 통째로 무시된다.
SCENE_MARK = "<!-- BUILD-INPUT -->"

HANGUL = re.compile(r"[가-힣ᄀ-ᇿ㄰-㆏]")
BLOCK = re.compile(r"\[([A-Z0-9\- ]+)\](.*)")

# 파트가 [WORD-BUDGET] 로 덮어쓸 수 있다. 기본값은 프롭·타일 기준이고,
# 장면 전체를 담는 배경(bg_mid)은 재질·빛·모티프를 다 실어야 해서 더 길다.
# ⚠️ 실측값이 아니라 판단이다 — PixelLab 은 상한을 문서화하지 않았다.
#    다만 짧은 설명을 전제로 한 모델이라, 길어지면 앞 문구부터 흐려진다.
WORD_BUDGET = 40

# 🆕 2026-09-06 Pro(v2) 전환의 청구서.
#
#    [WORD-BUDGET] 은 **내용에 쓸 단어 수**로 잡은 값이다. legacy 에서는 outline/shading/
#    detail/view 가 API 필드라 문구를 한 글자도 안 먹었다. v2 에는 그 칸이 없어서
#    같은 지정이 이제 문구로 들어간다 — 배경 파트 기준 **7 단어**
#    (`side view` + `lineless` + `basic shading` + `low detail`).
#
#    그대로 두면 전환만으로 내용 예산이 40 → 33 으로 조용히 줄어든다. 실제로 첫 빌드에서
#    멀쩡하던 파트 4개가 초과 경고를 냈다 — 내용은 한 글자도 안 늘었는데.
#
# 🔑 그래서 스타일 어구는 예산 밖으로 센다. **내용 예산을 원래 값으로 지키는 것**이
#    이 상수의 뜻이다.
# ⚠️ 총 문구는 실제로 길어졌다. PixelLab 은 문구가 길면 희석되는 모델이므로 공짜가 아니다.
#    다만 붙는 자리가 맨 끝이고 전부 짧은 관용어라, 정체성 문구를 깎는 것보다 낫다고 봤다.
#    **판단이지 실측이 아니다** — 앵커를 뽑아 보고 스타일 지정이 안 걸리면 여기를 의심할 것.
STYLE_WORD_ALLOWANCE = True

# 🆕 2026-09-07 — 호출당 비용(generations). 40_TOOLS/pixellab/profile.md §1·§2 의 실측이다.
#
# 🔴 이 숫자를 여기에 두는 이유는 **빌드가 청구서를 같이 내게 하려는 것**이다.
#    조립본만 보면 "요청 14개"로 보이고 그건 싸 보인다. 실제로는 씬 하나가
#    244~486 generations 이고, 8씬이면 한 사이클 배정량을 넘는다.
#    뽑기 시작한 뒤에 알면 이미 늦다 — 되돌릴 방법이 없다.
#
# ⚠️ 도구를 바꾸면 이 두 줄도 바꾼다. 여기 있는 것 자체가 도구 어휘라 원래는
#    40_TOOLS 에 있어야 맞지만, 그러면 계산하는 곳과 값이 갈린다. 옮긴다면 같이 옮길 것.
COST_IMAGE = (20, 40)     # create_image_pro — 후보 수와 무관하게 호출당
COST_TILESET = (2, 3)     # create_sidescroller_tileset — 1 이 아니다

# 인물 배제 항목 — [ALLOW-FIGURE] 가 있는 씬에서는 판정 목록에서 뺀다.
FIGURE_TERMS = ("people", "character", "creature", "monster")

# 🆕 2026-09-06 Pro(v2) 전환 —
#    v2 에는 outline/shading/detail/view 칸이 **없다.** 그 지정을 문구로 내린다.
#    params.txt [STYLE-WORDS] 에서 `<필드>_<값>` 으로 찾는다 (예: outline_lineless).
STYLE_FIELDS = ("view", "outline", "shading", "detail")

# 요청 본문이 아닌 파이프라인 메타는 밑줄로 시작한다.
#   _palette    후처리(enforce_palette.py)가 쓸 팔레트 — 생성 요청에는 안 들어간다
#   _reject_if  판정 목록 — v2 에 negative_description 이 없어서 사람이 보고 거른다
#   _count      몇 장 뽑을 것인가
META_PREFIX = "_"


def parse_size(text):
    """'160x96' → (160, 96).

    🔴 2026-09-07 정정 — 여기서 `{"width":…, "height":…}` **객체**를 내고 있었다.
       실제 인자는 `width` / `height` **정수 두 칸**이다 (profile.md §6 ④).
       ⚠️ 타입만 어긋난 것이라 **조용히 실패했을 것**이고, 그랬다면 원인을
          문구나 팔레트에서 찾았을 것이다. 첫 실호출 전에 걸린 게 다행이다.

    💡 `image_size = 160x96` 이라는 **입력 표기는 그대로 둔다.** 그건 우리 DSL 이고,
       도구 인자로 옮기는 것은 이 함수의 일이다. 파트 파일은 도구를 몰라도 된다.
    """
    m = re.fullmatch(r"\s*(\d+)\s*[xX]\s*(\d+)\s*", str(text))
    if not m:
        raise SystemExit("🔴 image_size 를 못 읽었다: %r (형식은 '160x96')" % text)
    return int(m.group(1)), int(m.group(2))


def candidates_for(width, height):
    """create_image_pro 가 **한 호출에** 주는 후보 수. 캔버스가 작을수록 많다.

    🔴 비용은 후보 수가 아니라 **호출당**이다. 둘을 섞으면 "여러 장 뽑는다"를
       여러 번 호출로 읽어 값을 네 배로 낸다 (profile.md §7).
    """
    longest = max(width, height)
    if longest <= 42:
        return 64
    if longest <= 85:
        return 16
    if longest <= 170:
        return 4
    return 1


def scene_seed(scene_id):
    """씬 이름에서 정해지는 고정 seed. 한 씬의 레이어들이 같은 값을 쓴다.

    🔴 seed 는 `40_TOOLS` 가 "한 씬 안에서 고정한다"고 정한 규약인데, 사람이 매번
       같은 숫자를 적어 넣는 방식이면 반드시 한 번은 어긋난다. 씬 이름에서 **파생**시킨다.
    ⚠️ 이것이 색온도 일치를 보장하지는 않는다 — seed 는 *같은 인자*에 같은 그림을 주는
       장치이고, 한 씬의 레이어들은 애초에 문구가 서로 다르다. 세션 규약의 대체가 아니다.
    💡 난수를 안 쓰는 이유: 다시 빌드하면 값이 바뀌어 조립본 diff 가 매번 더러워진다.
    """
    digest = 0
    for ch in scene_id:
        digest = (digest * 131 + ord(ch)) & 0x7FFFFFFF
    return digest


def style_phrases(resolved, style_words, label, warnings):
    """outline/shading/detail/view 토큰을 [STYLE-WORDS] 의 영문 어구로 옮긴다.

    🔴 못 찾으면 **경고한다.** 조용히 빠지면 그 지정이 사라진 채로 그림이 나오고,
       나온 그림만 봐서는 '문구를 안 실었다'와 '모델이 무시했다'를 구분할 수 없다.
    """
    out = []
    for field in STYLE_FIELDS:
        value = resolved.get(field)
        if not value:
            continue
        key = "%s_%s" % (field, str(value).strip().replace(" ", "_"))
        if key in style_words:
            out.append(style_words[key])
        else:
            warnings.append("%s: [STYLE-WORDS] 에 '%s' 가 없다 — '%s = %s' 지정이 문구에서 빠진다"
                            % (label, key, field, value))
    return out


# ────────────────────────────────────────────────────────────── 파싱

def parse_blocks(lines, keep_lines=()):
    sections, current = {}, None
    for line in lines:
        stripped = line.strip()
        if not stripped or stripped.startswith("#"):
            continue
        header = BLOCK.fullmatch(stripped)
        if header:
            current = header.group(1).strip()
            rest = header.group(2).strip()
            sections[current] = [rest] if rest else []
            continue
        if current:
            sections[current].append(stripped)
    out = {k: " ".join(v).strip() for k, v in sections.items()}
    for name in keep_lines:
        if name in sections:
            # 🔑 [STORY] 가 이것을 쓴다 — 소재 셋을 한 프롬프트에 다 넣으면 뭉개지므로
            #    줄마다 한 장씩 뽑는다. 그래야 서사 프롭이 서로 다른 물건이 된다.
            out[name + "@lines"] = [ln.rstrip(",") for ln in sections[name]]
    return out


def parse_file(path, keep_lines=()):
    with open(path, encoding="utf-8") as f:
        return parse_blocks(f.readlines(), keep_lines)


def parse_scene(path, keep_lines=()):
    """씬 .md 에서 SCENE_MARK 아래만 읽는다."""
    with open(path, encoding="utf-8") as f:
        text = f.read()
    if SCENE_MARK not in text:
        raise SystemExit(
            "🔴 빌드 입력 표시가 없다: %s\n"
            "   씬 파일 맨 끝에 '%s' 한 줄을 두고 그 아래에 [BLOCK] 들을 적는다."
            % (os.path.basename(path), SCENE_MARK))
    body = text.split(SCENE_MARK, 1)[1]
    # ⚠️ 주석은 검사하지 않는다 — 이 규칙을 설명하는 주석 자체에 '|' 가 들어간다.
    #    실제로 첫 실행에서 여기 걸렸다. 검사는 모델에 넘어갈 줄만 본다.
    for n, line in enumerate(body.splitlines(), 1):
        stripped = line.strip()
        if stripped and not stripped.startswith("#") and "|" in stripped:
            raise SystemExit(
                "🔴 빌드 입력 구역에 표가 섞였다: %s\n"
                "   '%s' 아래 %d번째 줄: %s\n"
                "   여기에는 [BLOCK] 과 '#' 주석만 둔다. 산문·표는 그 위로 옮길 것."
                % (os.path.basename(path), SCENE_MARK, n, stripped[:60]))
    return parse_blocks(body.splitlines(), keep_lines)


def parse_kv(text):
    out = {}
    for piece in re.split(r"(?<=[^=])\s(?=[a-z_]+\s*=)", text):
        if "=" not in piece:
            continue
        k, v = piece.split("=", 1)
        k, v = k.strip(), v.strip()
        if v.lower() in ("true", "false"):
            out[k] = v.lower() == "true"
        elif re.fullmatch(r"-?\d+", v):
            out[k] = int(v)
        elif re.fullmatch(r"-?\d*\.\d+", v):
            out[k] = float(v)
        else:
            out[k] = v
    return out


def parse_motifs(text):
    out = {}
    for m in re.finditer(r"(\w+)\s*=\s*([^=]+?)(?=\s+\w+\s*=|$)", text):
        out[m.group(1)] = m.group(2).strip().rstrip(",")
    return out


def parse_words(text):
    """쉼표로 갈린 낱말 목록을 소문자 집합으로."""
    return {w.strip().lower() for w in text.replace("\n", " ").split(",") if w.strip()}


# ────────────────────────────────────────────────────────────── 검사

def check_hangul(text, label):
    found = HANGUL.findall(text)
    if found:
        sample = "".join(sorted(set(found))[:12])
        raise SystemExit(
            "🔴 한글 유출: %s\n"
            "   섞인 글자: %s\n"
            "   프롬프트 본문에 한글이 들어갔다. 해당 줄을 '#' 주석으로 옮길 것." % (label, sample))


def find_words(text, vocabulary):
    """낱말 경계로 찾는다 — 'orb' 가 'absorb' 에 걸리지 않게."""
    seen = set(re.findall(r"[a-z]+", text.lower()))
    return sorted(seen & vocabulary)


def check_forbidden(text, label, forbidden, suspect, warnings):
    """🔴 금지 어휘가 있으면 빌드를 멈춘다. gen1~3 이 샜던 자리를 자동으로 막는다."""
    hit = find_words(text, forbidden)
    if hit:
        raise SystemExit(
            "🔴 금지 어휘: %s\n"
            "   걸린 낱말: %s\n"
            "   소설 0회 어휘이거나 10_BIBLE/03 의 빛 규약을 어기는 말이다.\n"
            "   근거: 00_SOURCE/forbidden-vocabulary.md · 목록: 30_BLOCKS/00_core.txt [FORBIDDEN]\n"
            "   정말 써야 하면 목록에서 빼되, 왜 빼는지 그 파일에 적을 것."
            % (label, ", ".join(hit)))
    soft = find_words(text, suspect)
    if soft:
        warnings.append("%s: 의심 어휘 — %s (정말 이 낱말이어야 하나)" % (label, ", ".join(soft)))


# ────────────────────────────────────────────────────────────── 조립

def joined(*parts):
    return ", ".join(p.strip().rstrip(",") for p in parts if p and p.strip())


def scene_pick(scene, names, fallback=""):
    """[USE-SCENE] 순서로 **먼저 채워져 있는 블록**을 고른다(폴백 사슬).

    🔑 타일이 이것을 쓴다 — 벽은 WALL-MATERIAL, 지면은 GROUND-MATERIAL 을 먼저 보고
       없으면 배경용 MATERIAL 로 떨어진다. 배경 재질에는 **물건**이 섞여 있어서
       32px 타일에 들어가면 바닥마다 같은 물건이 반복된다. (10_BIBLE/06 C7)
    """
    for name in [s.strip() for s in names.split(",") if s.strip()]:
        if scene.get(name):
            return scene[name]
    return fallback


def build_negative(core_negative, allow_figure):
    if not allow_figure:
        return core_negative
    kept = [t.strip() for t in core_negative.split(",")
            if t.strip() and t.strip() not in FIGURE_TERMS]
    return ", ".join(kept)


def build_one(scene_id, scene, part_id, part, core, params, motifs, vocab, warnings):
    kind = part.get("KIND", "prop")
    is_anchor = scene.get("ANCHOR-PART") == part_id
    allow_figure = "ALLOW-FIGURE" in scene
    forbidden, suspect = vocab

    # ── 파라미터: 전역 → 종류별 → 파트 개별 순으로 덮어쓴다 ──────────────
    resolved = dict(parse_kv(core.get("GLOBAL", "")))
    block = part.get("PARAMS", "")
    if block and block in params:
        resolved.update(parse_kv(params[block]))
    if "OVERRIDE" in part:
        resolved.update(parse_kv(part["OVERRIDE"]))

    palette = "palettes/%s.png" % scene_id
    style_words = parse_kv(params.get("STYLE-WORDS", ""))

    # ── 타일셋은 요청 모양이 아예 다르다 ────────────────────────────────
    #
    # 🔴 2026-09-07 — 이 블록은 **거의 전부 틀렸었다.** 실제 인자를 보고 고쳤다:
    #      inner_description → lower_description   (이름이 다르다)
    #      outer_description → 없는 칸. 배경이 투명하게 나오므로 그릴 필요가 없다
    #      transition_size   → 0.0~0.5 비율. `1`·`2` 는 범위 밖이었다
    #      border_jitter     → 없는 칸. 대응물은 tileset_adherence(_freedom)
    #      color_image       → 없는 칸. 팔레트는 후처리만이 집행한다
    #
    # 🔑 반대로 outline/shading/detail 은 **여기엔 필드로 있다** — 이미지 쪽과 정반대다.
    #    그래서 타일셋만 [STYLE-WORDS] 를 문구가 아니라 **칸으로** 받는다.
    #    어휘는 같은 표를 쓴다 — 값이 두 곳으로 갈리면 반드시 어긋난다.
    if kind == "tileset":
        material = scene_pick(scene, part.get("USE-SCENE", "MATERIAL"), scene.get("MATERIAL", ""))
        label_base = "%s/%s" % (scene_id, part_id)
        req = {
            "tool": resolved.get("tool", "create_sidescroller_tileset"),
            "lower_description": joined(part.get("INNER", ""), material),
            "transition_description": joined(part.get("TRANSITION", ""), material),
            "tile_size": resolved.get("tile_size", 32),
            "transition_size": resolved.get("transition_size", 0.25),
            "tileset_adherence": resolved.get("tileset_adherence", 100),
            "tileset_adherence_freedom": resolved.get("tileset_adherence_freedom", 500),
            "tile_strength": resolved.get("tile_strength", 1),
            "seed": scene_seed(scene_id),
            "_palette": palette,
        }
        for field in ("outline", "shading", "detail"):
            value = resolved.get(field)
            if not value:
                continue
            key = "%s_%s" % (field, str(value).strip().replace(" ", "_"))
            if key in style_words:
                req[field] = style_words[key]
            else:
                warnings.append("%s: [STYLE-WORDS] 에 '%s' 가 없다 — '%s' 칸이 빠진다"
                                % (label_base, key, field))

        # 🔴 씬의 [TILE-OUTER] 는 **타일셋으로 못 넣는다** — 그 칸이 없다.
        #    땅 밖에 무엇이 있는지는 배경 레이어가 말한다. 지우지 않고 메타로 남긴다 —
        #    조용히 버리면 stage5(고인 데)의 "바닥 밖은 물"이 어디로 갔는지 알 수 없다.
        outer = scene.get("TILE-OUTER") or part.get("OUTER", "")
        if outer:
            check_hangul(outer, "%s _outer_note" % label_base)
            req["_outer_note"] = outer

        # 🔑 지면 → 벽 순서. 파트가 [FOLLOWS] 로 선행 타일셋을 선언하면
        #    그쪽 base_tile_id 를 물려 두 타일셋이 이어 붙는다. 이미지 쪽 앵커와 같은 규약이다.
        follows = part.get("FOLLOWS", "").strip()
        if follows:
            req["_base_tile_from"] = follows

        for key in ("lower_description", "transition_description"):
            label = "%s %s" % (label_base, key)
            check_hangul(req[key], label)
            check_forbidden(req[key], label, forbidden, suspect, warnings)
        return [("", req, 1)]

    # ── 이미지 생성 (Pro — 앵커/파생이 style_image 유무로만 갈린다) ────
    split_name = part.get("SPLIT-SCENE", "").strip()
    variants = scene.get(split_name + "@lines") if split_name else None

    pieces = [part.get("BODY", "")]
    for name in [s.strip() for s in part.get("USE-SCENE", "").split(",") if s.strip()]:
        if name == split_name:
            pieces.append("\x00")          # 자리표. 아래에서 변형마다 갈아 끼운다
        elif name in scene:
            pieces.append(scene[name])
    # 🔑 [USE-SCENE] 은 적힌 블록을 **전부** 붙이고, [USE-SCENE-FIRST] 는 **먼저 있는 하나**만 쓴다.
    #    타일셋은 처음부터 폴백이었는데(scene_pick) 이미지 파트에는 그 수단이 없어서,
    #    prop_hazard 가 GROUND-MATERIAL 과 MATERIAL 을 둘 다 실어 문구가 46단어가 됐다.
    #    한 규약이 두 갈래로 갈려 있던 자리다.
    first = part.get("USE-SCENE-FIRST", "")
    if first:
        picked = scene_pick(scene, first)
        if picked:
            pieces.append(picked)
    if part.get("USE-MOTIF", "no").startswith("y"):
        key = scene.get("MOTIF", "").strip()
        if key in motifs:
            pieces.append(motifs[key])
        elif key:
            warnings.append("%s: 모티프 '%s' 가 00_core.txt [MOTIF] 에 없다" % (scene_id, key))
    if allow_figure:
        pieces.append(scene["ALLOW-FIGURE"])
    voice = part.get("USE-VOICE", "")
    if voice and voice in params:
        pieces.append(params[voice])
    if part.get("USE-TONE", "no").startswith("y"):
        pieces.append(core.get("TONE", ""))
    pieces.append(core.get("CORE", ""))

    tool = parse_kv(params.get("ANCHOR", "")).get("tool", "create_image_pro")
    count = int(part.get("COUNT", "1") or 1)
    label = "%s/%s description" % (scene_id, part_id)

    # 🆕 v2 에는 outline/shading/detail/view 칸이 없다 — 문구 끝에 붙여 보낸다.
    #    [CORE] 뒤에 오므로 정체성 문구가 앞자리를 지킨다.
    styles = style_phrases(resolved, style_words, label, warnings)
    pieces.extend(styles)
    style_word_count = sum(len(s.split()) for s in styles)

    # 판정 목록. 요청에 안 실린다 — v2 에 negative_description 이 없다.
    reject_if = build_negative(core.get("NEGATIVE", ""), allow_figure)
    check_hangul(reject_if, "%s/%s reject_if" % (scene_id, part_id))

    out = []
    for index, variant in enumerate(variants or [None], start=1):
        filled = [variant if p == "\x00" else p for p in pieces]
        description = joined(*filled)
        check_hangul(description, label)
        check_forbidden(description, label, forbidden, suspect, warnings)

        width, height = parse_size(resolved.get("image_size", "64x64"))
        req = {
            "tool": tool,
            "description": description,
            # 🔴 width/height 는 **정수 두 칸**이다. 객체가 아니다 (profile.md §6 ④).
            "width": width,
            "height": height,
            # 🔴 이 칸의 기본값은 **true** 다 — 안 실으면 배경이 투명하게 나온다.
            #    그래서 값이 false 여도 **항상 싣는다.** 빠뜨리는 쪽이 위험한 칸이다.
            "no_background": bool(resolved.get("no_background", False)),
            # 밑줄로 시작하는 것은 요청 본문이 아니라 파이프라인 메타다.
            "_palette": palette,
            "_reject_if": reject_if,
            # 예산 계산에서 뺄 몫. Pro 로 오면서 문구로 내려온 스타일 지정이다.
            "_style_words": style_word_count,
            # 🔑 한 호출이 주는 후보 수. **비용은 이 수와 무관하게 호출당**이다.
            "_candidates": candidates_for(width, height),
        }

        # 🔴 프롭만 seed 를 안 건다 — 고정하면 다섯 개가 전부 닮은 물건이 된다.
        if kind != "prop":
            req["seed"] = scene_seed(scene_id)

        if is_anchor:
            # 앵커는 참조할 것이 없으므로 style_image 없이 뽑는다. 이 한 장이 화풍을 정한다.
            req["_note"] = ("ANCHOR - one call returns %d candidates; pick one, "
                            "save to anchors/<scene>.png" % req["_candidates"])
        else:
            # 🔑 파생은 앵커를 style_image 로 물고 간다. 화풍을 붙드는 유일한 장치다.
            #    legacy 는 여기서 모델이 갈렸다(pixflux → bitforge). Pro 는 같은
            #    도구에 이 칸의 유무로만 갈린다 — 규약은 그대로고 수단만 단순해졌다.
            #
            # ⚠️ 경로는 **메타**다. 도구는 base64 나 url 을 받지 파일 경로를 받지 않는다.
            #    보내는 쪽이 이 파일을 읽어 실어야 한다 — 경로를 인자 이름으로 두면
            #    "그대로 보내면 되는 줄" 알고 조용히 실패한다.
            style_scene = scene.get("STYLE-FROM", scene_id)
            req["_style_image_path"] = "anchors/%s.png" % style_scene

            # 🔴 style_copy 를 안 주면 넷 다 베낀다 — 배경 앵커의 lineless 가
            #    발판·프롭까지 따라와 우리가 명시한 selective outline 을 덮어쓴다.
            #    배경만 lineless 인 것은 취향이 아니라 조작 대상을 가르는 장치다.
            profile = "bg" if kind == "bg" else "sprite"
            copy_spec = parse_kv(params.get("STYLE-COPY", "")).get(profile)
            if copy_spec:
                req["style_copy"] = [s.strip() for s in str(copy_spec).split(",") if s.strip()]
            else:
                warnings.append("%s/%s: [STYLE-COPY] 에 '%s' 가 없다 — 기본값(넷 다)으로 떨어져 "
                                "앵커가 outline 지정을 덮어쓴다" % (scene_id, part_id, profile))

        out.append(("_%d" % index if variants else "", req, count))

    return out


# ────────────────────────────────────────────────────────────── 메인

def main():
    core = parse_file(CORE_FILE)
    params = parse_file(PARAMS_FILE)
    motifs = parse_motifs(core.get("MOTIF", ""))

    for need, src, table in (
            (("CORE", "MOTIF", "TONE", "NEGATIVE", "FORBIDDEN", "GLOBAL"), CORE_FILE, core),
            (("ANCHOR", "PALETTE", "STYLE-WORDS", "STYLE-COPY",
              "PARAMS-BG", "PARAMS-TILESET", "PARAMS-PROP"),
             PARAMS_FILE, params)):
        missing = [k for k in need if k not in table]
        if missing:
            raise SystemExit("🔴 %s 에 블록이 없다: %s"
                             % (os.path.basename(src), ", ".join(missing)))

    forbidden = parse_words(core["FORBIDDEN"])
    suspect = parse_words(core.get("SUSPECT", ""))
    vocab = (forbidden, suspect)

    check_hangul(core["NEGATIVE"], "00_core.txt [NEGATIVE]")
    # ⚠️ 부정 프롬프트는 "안 그릴 것"의 목록이라 금지 어휘가 들어 있는 게 정상이다.
    #    그래서 여기에는 check_forbidden 을 걸지 않는다.

    all_scenes = sorted(f[:-3] for f in os.listdir(SCENE_DIR) if f.endswith(".md"))
    wanted = sys.argv[1:] or all_scenes
    os.makedirs(OUT_DIR, exist_ok=True)

    catalog, total, warnings = [], 0, []
    for scene_id in wanted:
        path = os.path.join(SCENE_DIR, scene_id + ".md")
        if not os.path.exists(path):
            print("  건너뜀 (씬 없음): %s" % scene_id)
            continue
        scene = parse_scene(path, keep_lines=("STORY",))
        print("조립: %s" % scene_id)

        # 🔴 이 씬의 지난 산출물을 먼저 지운다.
        #    안 지우면 파트를 쪼개거나 이름을 바꿨을 때 낡은 파일이 그대로 남아,
        #    쓰는 사람은 그게 이번 빌드 결과인지 알 수 없다.
        for stale in os.listdir(OUT_DIR):
            if stale.startswith(scene_id + "__") or stale == scene_id + ".md":
                os.remove(os.path.join(OUT_DIR, stale))

        rows = []
        part_ids = [p.strip() for p in scene.get("PARTS", "").replace("\n", " ").split(",") if p.strip()]
        for part_id in part_ids:
            part_path = os.path.join(PART_DIR, part_id + ".txt")
            if not os.path.exists(part_path):
                warnings.append("%s: 파트 파일 없음 — %s" % (scene_id, part_id))
                continue
            part = parse_file(part_path)
            for suffix, req, count in build_one(scene_id, scene, part_id, part,
                                                core, params, motifs, vocab, warnings):
                name = part_id + suffix
                text = req.get("description") or req.get("lower_description", "")
                words = len(text.split())
                # 🔑 스타일 어구는 내용이 아니라 v2 가 필드를 없애서 문구로 내려온 관용어다.
                #    예산은 **내용에 쓸 단어 수**이므로 그 몫을 빼고 잰다 (위 STYLE_WORD_ALLOWANCE).
                content_words = words - (req.get("_style_words", 0) if STYLE_WORD_ALLOWANCE else 0)
                budget = int(part.get("WORD-BUDGET", WORD_BUDGET))
                if content_words > budget:
                    warnings.append("%s/%s: 내용이 %d단어다 (%d 권장, 총 %d). USE-SCENE 을 줄일 것"
                                    % (scene_id, name, content_words, budget, words))
                req["_count"] = count
                with open(os.path.join(OUT_DIR, "%s__%s.json" % (scene_id, name)),
                          "w", encoding="utf-8") as f:
                    json.dump(req, f, ensure_ascii=False, indent=2)
                rows.append((name, req, count))
                total += 1

        with open(os.path.join(OUT_DIR, scene_id + ".md"), "w", encoding="utf-8") as f:
            f.write("# %s\n\n" % scene_id)
            anchor = scene.get("ANCHOR-PART") or "(STYLE-FROM %s)" % scene.get("STYLE-FROM", "?")
            f.write("앵커: **%s** — 이것부터 여러 장 뽑아 한 장을 고른다.\n" % anchor)
            f.write("팔레트: `palettes/%s.png`\n" % scene_id)
            f.write("판정: `10_BIBLE/03-light.md §5` 체크리스트 + 아래 「다시 뽑는 조건」\n\n")
            # 🔴 v2 에는 팔레트 강제 칸이 없다. 뽑은 뒤 반드시 이 줄을 돌려야 P1~P5 가 선다.
            #    --crop-gutter 가 붙는 이유는 40_TOOLS/pixellab/profile.md §6-B —
            #    컨택트 시트에서 잘라 온 흰 테두리가 붙은 채로는 ΔE 를 믿을 수 없다.
            f.write("**🔴 뽑은 뒤 반드시 집행한다** — v2 에는 팔레트 강제 칸이 없다:\n\n")
            f.write("```\npython Tools/ArtPipeline/enforce_palette.py <뽑은것>.png --scene %s --crop-gutter\n```\n\n"
                    % scene_id)
            f.write("ΔE 가 크면 스냅해서 쓰지 말고 **다시 뽑는다.** "
                    "ΔE 0 도 합격이 아니다 — 「쓰인 색」 단수를 같이 본다.\n\n")
            reject = next((r.get("_reject_if") for _, r, _ in rows if r.get("_reject_if")), "")
            if reject:
                f.write("### 다시 뽑는 조건 (이게 보이면)\n\n")
                f.write("> %s\n\n" % reject)
                f.write("v2 에는 `negative_description` 칸이 없다. **막는 것은 프롬프트가 아니라 판정이다.**\n\n")
            f.write("---\n\n")
            for part_id, req, count in rows:
                f.write("## %s  <sub>x%d</sub>\n\n" % (part_id, count))
                text = req.get("description") or (
                    "lower: %s\ntransition: %s"
                    % (req.get("lower_description"), req.get("transition_description")))
                f.write("```\n%s\n```\n\n" % text)
                # 🔑 표에는 **실제로 보내는 것만** 싣는다.
                #    밑줄로 시작하는 키는 파이프라인 메타지 요청 본문이 아니다 —
                #    섞어서 보여 주면 손으로 옮겨 담을 때 없는 칸을 찾게 된다.
                skip = {"description", "lower_description", "transition_description"}
                f.write("| 항목 | 값 |\n|---|---|\n")
                for k, v in req.items():
                    if k in skip or v is None or k.startswith(META_PREFIX):
                        continue
                    f.write("| `%s` | %s |\n" % (k, json.dumps(v, ensure_ascii=False)
                                                 if isinstance(v, (dict, list)) else v))
                if req.get("_note"):
                    f.write("\n> 🔴 %s\n" % req["_note"])
                f.write("\n")
        catalog.append((scene_id, rows))

    with open(os.path.join(OUT_DIR, "00_ALL.md"), "w", encoding="utf-8") as f:
        f.write("# Abyss 환경 아트 — 조립된 생성 요청 전체\n\n")
        f.write("씬마다 **팔레트를 먼저 굽고, 앵커를 뽑아** `anchors/<scene>.png` 로 저장한 뒤 나머지를 뽑는다.\n")
        f.write("앵커 없이 파생을 돌리면 `style_image` 가 비어 화풍이 갈린다.\n\n")
        for scene_id, rows in catalog:
            f.write("- [%s](./%s.md) — %d개 요청\n" % (scene_id, scene_id, len(rows)))

    print("\n완료 — %d개 요청 → %s" % (total, os.path.relpath(OUT_DIR, ROOT)))
    print("   카탈로그: assembled/00_ALL.md")

    # ── 🆕 청구서. 요청 수가 아니라 **호출 수 × 호출당 비용**으로 센다 ──────────
    #
    # 🔑 한 호출이 후보를 여러 장 주므로 "5장 필요 = 5번 호출"이 아니다.
    #    48x48 은 한 호출에 16장을 주니 COUNT 5 도 한 번이면 된다.
    #    이 구분을 안 하면 예산을 실제의 몇 배로 잡고 겁먹거나, 반대로 셈을 놓친다.
    image_calls = tileset_calls = 0
    for _, rows in catalog:
        for _, req, count in rows:
            if req.get("tool", "").endswith("tileset"):
                tileset_calls += 1
            else:
                per_call = req.get("_candidates", 1)
                image_calls += -(-count // per_call)      # 올림 나눗셈
    low = image_calls * COST_IMAGE[0] + tileset_calls * COST_TILESET[0]
    high = image_calls * COST_IMAGE[1] + tileset_calls * COST_TILESET[1]
    print("\n💰 예상 비용 — 이미지 %d호출 + 타일셋 %d호출 = **%d~%d generations**"
          % (image_calls, tileset_calls, low, high))
    print("   (재생성·앵커 재선정은 안 센 값이다. 잔여량은 get_balance 로 본다)")

    if warnings:
        print("\n⚠️ 경고 %d건:" % len(warnings))
        for w in warnings:
            print("   - %s" % w)


if __name__ == "__main__":
    main()
