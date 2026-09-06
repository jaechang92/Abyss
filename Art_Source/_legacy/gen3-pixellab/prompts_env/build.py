# -*- coding: utf-8 -*-
"""
build.py — 세계 + 파라미터 + 씬 + 파트를 합쳐 PixelLab 에 넣을 생성 요청을 만든다.

    python Art_Source/prompts_env/build.py                  # 전 씬
    python Art_Source/prompts_env/build.py stage1_rift_entrance lobby

산출물은 assembled/ 아래에 나온다(git 비추적 — 언제든 다시 만들어진다).
  <scene>__<part>.json   API/MCP 로 바로 보낼 수 있는 요청 본문
  <scene>.md             웹 앱에 손으로 옮겨 담을 때 보는 표
  00_ALL.md              전체 카탈로그

🔴 한글 유출 차단이 이 스크립트의 두 번째 역할이다.
   주석은 한글로 쓰되 프롬프트 본문에는 한 글자도 섞이면 안 된다.
   캐릭터 쪽 prompts/build.py 에서 실제로 겪은 사고다 — 주석이 조립본에
   딸려 들어가면 모델이 그걸 무시하지 않고 "한국어가 적힌 그림"을 그리려 든다.

⚠️ 문구 길이도 검사한다. NovelAI 는 태그를 쌓을수록 세졌지만
   PixelLab 은 짧은 설명을 전제로 한다 — 길면 서로 희석되어 아무것도 안 걸린다.
"""
import json
import os
import re
import sys

ROOT = os.path.dirname(os.path.abspath(__file__))
OUT_DIR = os.path.join(ROOT, "assembled")
SCENE_DIR = os.path.join(ROOT, "scenes")
PART_DIR = os.path.join(ROOT, "parts")

HANGUL = re.compile(r"[가-힣ᄀ-ᇿ㄰-㆏]")
# 파트가 [WORD-BUDGET] 로 덮어쓸 수 있다. 기본값은 프롭·타일 기준이고,
# 장면 전체를 담는 배경(bg_mid 등)은 재질·광원·모티프를 다 실어야 해서 더 길다.
# ⚠️ 실측값이 아니라 판단이다 — PixelLab 은 상한을 문서화하지 않았다.
#    다만 짧은 설명을 전제로 한 모델이라, 길어지면 앞 문구부터 흐려진다.
WORD_BUDGET = 40

# 인물 배제 항목 — [ALLOW-FIGURE] 가 있는 씬에서는 이것들을 부정 프롬프트에서 뺀다.
FIGURE_TERMS = ("people", "character", "creature", "monster")


def parse(path, keep_lines=()):
    """[BLOCK] 로 갈린 섹션을 읽는다. # 로 시작하는 줄은 주석이라 버린다.

    keep_lines 에 든 블록만 줄 목록을 따로 남긴다('<NAME>@lines' 키).
    🔑 [STORY] 가 이것을 쓴다 — 소재 셋을 한 프롬프트에 다 넣으면 뭉개지므로
       줄마다 한 장씩 뽑는다. 그래야 스테이지당 서사 프롭이 서로 다른 물건이 된다.
    """
    sections, current = {}, None
    with open(path, encoding="utf-8") as f:
        for line in f:
            stripped = line.strip()
            if not stripped or stripped.startswith("#"):
                continue
            header = re.fullmatch(r"\[([A-Z0-9\- ]+)\](.*)", stripped)
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
            out[f"{name}@lines"] = [ln.rstrip(",") for ln in sections[name]]
    return out


def parse_kv(text):
    """'key = value' 줄들을 dict 로. 값은 문자열/불리언/숫자로 정리한다."""
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
    """'name = phrase' 여러 개가 한 블록에 있다. 이름으로 골라 쓴다."""
    out = {}
    for m in re.finditer(r"(\w+)\s*=\s*([^=]+?)(?=\s+\w+\s*=|$)", text):
        out[m.group(1)] = m.group(2).strip().rstrip(",")
    return out


def check_hangul(text, label):
    found = HANGUL.findall(text)
    if found:
        sample = "".join(sorted(set(found))[:12])
        raise SystemExit(
            f"🔴 한글 유출: {label}\n"
            f"   섞인 글자: {sample}\n"
            f"   프롬프트 본문에 한글이 들어갔다. 해당 줄을 '#' 주석으로 옮길 것."
        )


def joined(*parts):
    return ", ".join(p.strip().rstrip(",") for p in parts if p and p.strip())


def scene_pick(scene, names, fallback=""):
    """[USE-SCENE] 에 적힌 순서로 **먼저 채워져 있는 블록**을 고른다(폴백 사슬).

    🔑 타일이 이것을 쓴다 — 타일 재질은 [GROUND-MATERIAL] 이고, 없으면 배경용
       [MATERIAL] 로 떨어진다. 둘을 가른 이유는 parts/tileset_ground.txt 에 있다:
       배경 재질에는 "toppled pillars" 같은 **물건**이 섞여 있어서
       32px 타일에 들어가면 바닥마다 같은 물건이 반복된다.
    """
    for name in [s.strip() for s in names.split(",") if s.strip()]:
        if scene.get(name):
            return scene[name]
    return fallback


def build_negative(world_negative, allow_figure):
    if not allow_figure:
        return world_negative
    kept = [t.strip() for t in world_negative.split(",")
            if t.strip() and t.strip() not in FIGURE_TERMS]
    return ", ".join(kept)


def build_one(scene_id, scene, part_id, part, world, params, motifs):
    kind = part.get("KIND", "prop")
    is_anchor = scene.get("ANCHOR-PART") == part_id
    allow_figure = "ALLOW-FIGURE" in scene

    # ── 파라미터: 전역 → 종류별 → 파트 개별 순으로 덮어쓴다 ──────────────
    resolved = dict(parse_kv(world.get("GLOBAL", "")))
    block = part.get("PARAMS", "")
    if block and block in params:
        resolved.update(parse_kv(params[block]))
    if "OVERRIDE" in part:
        resolved.update(parse_kv(part["OVERRIDE"]))

    # ── 타일셋은 요청 모양이 아예 다르다 (웹 도구, description 이 셋) ────
    if kind == "tileset":
        material = scene_pick(scene, part.get("USE-SCENE", "MATERIAL"), scene.get("MATERIAL", ""))
        # 🔴 outer 는 씬이 덮을 수 있다. 스테이지 5 는 바닥 밖이 허공이 아니라 무(無)다.
        #    (예전에는 파트의 [OUTER] 만 봤다 — 주석은 씬에서 가져온다고 적혀 있었지만
        #     코드가 그러지 않았다. 문서가 로직보다 앞서 있던 자리다)
        outer = scene.get("TILE-OUTER") or part.get("OUTER", "")
        req = {
            "tool": resolved.get("tool", "create-tileset"),
            "inner_description": joined(part.get("INNER", ""), material),
            "transition_description": joined(part.get("TRANSITION", ""), material),
            "outer_description": outer,
            "tile_size": resolved.get("tile_size", 32),
            "transition_size": resolved.get("transition_size", 1),
            "border_jitter": resolved.get("border_jitter", "medium"),
            "color_image": f"palettes/{scene_id}.png",
        }
        for key in ("inner_description", "transition_description", "outer_description"):
            check_hangul(req[key], f"{scene_id}/{part_id} {key}")
        return [("", req, 1)]

    # ── 이미지 생성 (pixflux 앵커 / bitforge 파생) ────────────────────────
    # 🔑 [SPLIT-SCENE] 이 걸린 블록은 줄마다 한 장씩 뽑는다.
    #    소재 셋을 한 프롬프트에 다 넣으면 셋 다 흐려진 물건 하나가 나온다.
    split_name = part.get("SPLIT-SCENE", "").strip()
    variants = scene.get(f"{split_name}@lines") if split_name else None

    pieces = [part.get("BODY", "")]
    for name in [s.strip() for s in part.get("USE-SCENE", "").split(",") if s.strip()]:
        if name == split_name:
            pieces.append("\x00")  # 자리표. 아래에서 변형마다 갈아 끼운다
        elif name in scene:
            pieces.append(scene[name])
    if part.get("USE-MOTIF", "no").startswith("y"):
        key = scene.get("MOTIF", "").strip()
        if key in motifs:
            pieces.append(motifs[key])
    if allow_figure:
        pieces.append(scene["ALLOW-FIGURE"])
    voice = part.get("USE-VOICE", "")
    if voice and voice in params:
        pieces.append(params[voice])
    if part.get("USE-TONE", "no").startswith("y"):
        pieces.append(world.get("TONE", ""))
    pieces.append(world.get("CORE", ""))

    model = "pixflux" if is_anchor else "bitforge"
    count = int(part.get("COUNT", "1") or 1)
    out = []

    for index, variant in enumerate(variants or [None], start=1):
        filled = [variant if p == "\x00" else p for p in pieces]
        description = joined(*filled)
        check_hangul(description, f"{scene_id}/{part_id} description")

        req = {
            "endpoint": f"/generate-image-{model}",
            "description": description,
            "image_size": resolved.get("image_size", "64x64"),
            "view": resolved.get("view", "side"),
            "outline": resolved.get("outline"),
            "shading": resolved.get("shading"),
            "detail": resolved.get("detail"),
            "isometric": bool(resolved.get("isometric", False)),
            "no_background": bool(resolved.get("no_background", False)),
            "text_guidance_scale": resolved.get("text_guidance_scale", 8),
            "color_image": f"palettes/{scene_id}.png",
        }

        if model == "bitforge":
            # 🔑 파생 생성은 앵커를 style_image 로 물고 간다. 화풍을 붙드는 유일한 장치다.
            style_scene = scene.get("STYLE-FROM", scene_id)
            req["style_image"] = f"anchor/{style_scene}.png"
            req["style_strength"] = parse_kv(params.get("ANCHOR", "")).get("style_strength", 50)
            # ⚠️ negative_description 은 pixflux 에서 deprecated 라 bitforge 에만 싣는다.
            req["negative_description"] = build_negative(world.get("NEGATIVE", ""), allow_figure)
            check_hangul(req["negative_description"], f"{scene_id}/{part_id} negative")
        else:
            req["_note"] = "ANCHOR — pick one of several, save to anchor/<scene>.png"

        suffix = f"_{index}" if variants else ""
        out.append((suffix, req, count))

    return out


def main():
    world = parse(os.path.join(ROOT, "01_world.txt"))
    params = parse(os.path.join(ROOT, "02_params.txt"))
    motifs = parse_motifs(world.get("MOTIF", ""))

    for need, src, table in ((("CORE", "MOTIF", "TONE", "NEGATIVE", "GLOBAL"), "01_world.txt", world),
                             (("ANCHOR", "PALETTE", "PARAMS-BG", "PARAMS-TILESET", "PARAMS-PROP"),
                              "02_params.txt", params)):
        missing = [k for k in need if k not in table]
        if missing:
            raise SystemExit(f"🔴 {src} 에 블록이 없다: {', '.join(missing)}")

    check_hangul(world["NEGATIVE"], "01_world.txt [NEGATIVE]")

    all_scenes = sorted(f[:-4] for f in os.listdir(SCENE_DIR) if f.endswith(".txt"))
    wanted = sys.argv[1:] or all_scenes
    os.makedirs(OUT_DIR, exist_ok=True)

    catalog, total, warnings = [], 0, []
    for scene_id in wanted:
        path = os.path.join(SCENE_DIR, f"{scene_id}.txt")
        if not os.path.exists(path):
            print(f"  건너뜀 (씬 없음): {scene_id}")
            continue
        scene = parse(path, keep_lines=("STORY",))
        print(f"조립: {scene_id}")

        # 🔴 이 씬의 지난 산출물을 먼저 지운다.
        #    안 지우면 파트를 쪼개거나 이름을 바꿨을 때 낡은 파일이 그대로 남아,
        #    쓰는 사람은 그게 이번 빌드 결과인지 알 수 없다.
        for stale in os.listdir(OUT_DIR):
            if stale.startswith(f"{scene_id}__") or stale == f"{scene_id}.md":
                os.remove(os.path.join(OUT_DIR, stale))

        rows = []
        for part_id in [p.strip() for p in scene.get("PARTS", "").replace("\n", " ").split(",") if p.strip()]:
            part_path = os.path.join(PART_DIR, f"{part_id}.txt")
            if not os.path.exists(part_path):
                warnings.append(f"{scene_id}: 파트 파일 없음 — {part_id}")
                continue
            part = parse(part_path)
            for suffix, req, count in build_one(scene_id, scene, part_id, part,
                                                world, params, motifs):
                name = f"{part_id}{suffix}"
                text = req.get("description") or req.get("inner_description", "")
                words = len(text.split())
                budget = int(part.get("WORD-BUDGET", WORD_BUDGET))
                if words > budget:
                    warnings.append(f"{scene_id}/{name}: 문구가 {words}단어다 "
                                    f"({budget} 권장). USE-SCENE 을 줄일 것")

                req["_count"] = count
                with open(os.path.join(OUT_DIR, f"{scene_id}__{name}.json"),
                          "w", encoding="utf-8") as f:
                    json.dump(req, f, ensure_ascii=False, indent=2)
                rows.append((name, req, count))
                total += 1

        with open(os.path.join(OUT_DIR, f"{scene_id}.md"), "w", encoding="utf-8") as f:
            f.write(f"# {scene_id}\n\n")
            anchor = scene.get("ANCHOR-PART") or f"(STYLE-FROM {scene.get('STYLE-FROM', '?')})"
            f.write(f"앵커: **{anchor}** — 이것부터 여러 장 뽑아 한 장을 고른다.\n")
            f.write(f"팔레트: `palettes/{scene_id}.png` (color_image 칸)\n\n")
            for part_id, req, count in rows:
                f.write(f"## {part_id}  <sub>x{count}</sub>\n\n")
                text = req.get("description") or (
                    f"inner: {req.get('inner_description')}\n"
                    f"transition: {req.get('transition_description')}\n"
                    f"outer: {req.get('outer_description')}")
                f.write(f"```\n{text}\n```\n\n")
                skip = {"description", "inner_description", "transition_description",
                        "outer_description", "_count"}
                opts = {k: v for k, v in req.items() if k not in skip and v is not None}
                f.write("| 항목 | 값 |\n|---|---|\n")
                for k, v in opts.items():
                    f.write(f"| `{k}` | {v} |\n")
                f.write("\n")
        catalog.append((scene_id, rows))

    with open(os.path.join(OUT_DIR, "00_ALL.md"), "w", encoding="utf-8") as f:
        f.write("# Abyss 환경 아트 — 조립된 생성 요청 전체\n\n")
        f.write("씬마다 **앵커를 먼저** 뽑아 `anchor/<scene>.png` 로 저장한 뒤 나머지를 뽑는다.\n")
        f.write("앵커 없이 bitforge 를 돌리면 `style_image` 가 비어 화풍이 갈린다.\n\n")
        for scene_id, rows in catalog:
            f.write(f"- [{scene_id}](./{scene_id}.md) — {len(rows)}개 파트\n")

    print(f"\n완료 — {total}개 요청 → {os.path.relpath(OUT_DIR, os.path.dirname(ROOT))}")
    print("   카탈로그: assembled/00_ALL.md")
    if warnings:
        print(f"\n⚠️ 경고 {len(warnings)}건:")
        for w in warnings:
            print(f"   - {w}")


if __name__ == "__main__":
    main()
