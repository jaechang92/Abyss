# -*- coding: utf-8 -*-
"""
build.py — 프롬프트 블록을 이어 붙여 **모델에 그대로 넣을 수 있는** 조립본을 만든다.

🔴 단순히 cat으로 붙이면 안 된다. 블록 파일에는 사람이 읽을 한글 주석이 섞여 있는데,
그것까지 모델에 넘기면 **지시로 읽힌다** — "바꾸지 않는다"를 그림 내용으로 해석하거나,
한글이 섞여 스타일 해석이 흔들린다. 여기서 걷어내고 영문 지시만 남긴다.

실행:
  python Art_Source/prompts/build.py              # 전부 재생성
  python Art_Source/prompts/build.py void_archer  # 하나만


구조 (2026-08-20 재작성 — 무기 분리)
------------------------------------
공용 블록은 **정체성 / 프레이밍 / 규칙** 셋으로 갈라져 있다.

    _identity.txt          ❌ 수정 금지 — 스타일·인물의 SoT. 모든 조립본에 들어간다
    _framing_showcase.txt  ❌ 수정 금지 — 전신 3/4 (게임에 들어간 폼 4종이 이걸로 나왔다)
    _framing_rig.txt       ❌ 수정 금지 — 정측면 (리깅 base)
    _framing_weapon.txt    ❌ 수정 금지 — 물체 단독 (무기·방패)
    _sheet_rules.txt       ❌ 수정 금지 — 행 이미지 실측 결함 2건을 막는다
    _rig_rules.txt         ❌ 수정 금지 — SpriteSkin 단일 메시의 제약
    _weapon_rules.txt      ❌ 수정 금지 — 강체 파트로 쓰이기 위한 조건

📌 예전에는 `_anchor.txt` 하나가 정체성과 프레이밍을 같이 들고 있었다. 바꾸면 안 되는 것은
   정체성 쪽인데 프레이밍이 묶여 있어서, 리깅 프롬프트가 "three-quarter"라고 한 뒤
   "NOT three-quarter"로 자기 말을 뒤집었다. 용도마다 프레이밍이 달라야 하므로 갈랐다.

폼 블록(`form_<id>.txt`)은 ✅ 수정 가능하고, 대괄호 절 표시로 나뉜다.

    [BODY]                    몸 — 방어구·색·체격
    [SILHOUETTE]              실루엣 — 전신 기준
    [WEAPON: <id>]            그 무기의 **생김새**
    [WEAPON-CARRY: <id>]      캐릭터가 **드는 방식** (전신 그림 전용)
    [WEAPON-IMAGE: <id>]      **단독으로 그릴 때의 방향** (무기 그림 전용)

🔑 무기가 셋으로 갈린 이유는 절마다 쓰는 곳이 다르기 때문이다.
   생김새는 둘 다 쓰지만, "오른손에 들고 옆으로 늘어뜨린다"는 전신 그림에만,
   "똑바로 세워 손잡이를 아래로"는 무기 그림에만 해당한다.
   합쳐 두면 무기 단독 프롬프트가 "물체 혼자"라고 해 놓고 "왼팔에 끼고 몸 옆에"라고
   덧붙이게 된다 — 실제로 재작성 전 조립본이 그랬다.


조립본
------
    _assembled_<form>.txt                정체성 + 쇼케이스 프레이밍 + BODY + WEAPON들 + SILHOUETTE
    _assembled_<form>_sheet.txt          위 + 행 규칙
    _assembled_<form>_rig.txt            정체성 + 리깅 프레이밍 + BODY + 리깅 규칙   ← 무기 없음
    _assembled_<form>_weapon_<id>.txt    정체성 + 무기 프레이밍 + 무기 규칙 + WEAPON + WEAPON-IMAGE

리깅 조립본에서 SILHOUETTE 을 뺀 것은 실수가 아니다 — 실루엣 문장이 전부 무기를 가리킨다
("the great arc of the bow beside the body"). 무기 없는 몸을 뽑는 프롬프트에 넣으면
모델이 활을 그린다.
"""
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
FORMS = ["dark_blade", "void_archer", "ancient_shield", "void_thrower"]

# 사람용 메타 줄 — 대괄호로 시작하는 줄은 전부 주석으로 본다(절 표시도 여기 걸려 사라진다).
META = re.compile(r"^\s*\[")

# 🔴 한글이 한 글자라도 있으면 사람용 주석이다.
#
# 프롬프트 본문은 **설계상 전부 영문**이다(정체성·프레이밍·폼 블록 어디에도 한글 문장이 없다).
# 그래서 "한글 유무"가 주석 판정의 정확한 기준이 된다.
#
# 2026-08-20 발견: 원래는 대괄호 줄만 걷어냈는데, `_sheet_rules.txt`의 한글 주석은
# `🔴`·`·`로 시작해서 걸리지 않았다. 그 결과 **행 프롬프트마다 한글 3줄이 모델에 넘어갔다** —
# 이 파일 docstring이 막겠다고 적어 둔 바로 그 일이 조용히 일어나고 있었다.
# 접두어를 하나씩 추가하는 방식은 새 기호를 쓸 때마다 같은 구멍이 다시 난다.
HANGUL = re.compile(r"[가-힣]")

# 폼 블록의 절 표시. 대괄호로 시작하므로 출력에서는 META 가 걷어낸다.
SECTION = re.compile(
    r"^\s*\[(BODY|SILHOUETTE|WEAPON|WEAPON-CARRY|WEAPON-IMAGE)"
    r"(?:\s*:\s*([A-Za-z0-9_]+))?\]\s*$")


def strip_comments(lines):
    """한글 주석과 대괄호 메타 줄을 걷어내고 연속 빈 줄을 정리한다."""
    out = [ln.rstrip() for ln in lines if not (META.match(ln) or HANGUL.search(ln))]
    return re.sub(r"\n{3,}", "\n\n", "\n".join(out)).strip()


def read_lines(name):
    path = os.path.join(HERE, name)
    if not os.path.exists(path):
        return []
    return open(path, encoding="utf-8").read().splitlines()


def clean(name):
    """공용 블록 하나를 읽어 영문 지시만 남긴다."""
    return strip_comments(read_lines(name))


def parse_form(form):
    """
    form_<id>.txt 를 절 단위로 가른다.

    반환: {"BODY": str, "SILHOUETTE": str,
           "WEAPONS": [(weapon_id, desc, carry, image_orientation), ...]}

    무기는 **파일에 적힌 순서**를 지킨다 — 고대 방패병은 탑실드가 먼저이고 단검이 나중인데,
    그게 실루엣의 주인공 순서다.
    """
    lines = read_lines("form_%s.txt" % form)
    if not lines:
        return None

    buckets = {}          # key -> [원본 줄]
    order = []            # 무기 id 등장 순서
    key = None
    for line in lines:
        m = SECTION.match(line)
        if m:
            kind, wid = m.group(1), m.group(2)
            key = kind if wid is None else (kind, wid)
            if kind == "WEAPON" and wid not in order:
                order.append(wid)
            buckets.setdefault(key, [])
            continue
        if key is not None:
            buckets[key].append(line)

    def take(k):
        return strip_comments(buckets.get(k, []))

    weapons = []
    for wid in order:
        weapons.append((wid,
                        take(("WEAPON", wid)),
                        take(("WEAPON-CARRY", wid)),
                        take(("WEAPON-IMAGE", wid))))

    return {"BODY": take("BODY"), "SILHOUETTE": take("SILHOUETTE"), "WEAPONS": weapons}


def write(name, parts):
    text = "\n\n".join(p for p in parts if p) + "\n"
    path = os.path.join(HERE, name)
    open(path, "w", encoding="utf-8").write(text)
    return (name, len(text.splitlines()), len(text))


def build(form):
    sec = parse_form(form)
    if sec is None or not sec["BODY"]:
        return []

    identity = clean("_identity.txt")
    fr_show = clean("_framing_showcase.txt")
    fr_rig = clean("_framing_rig.txt")
    fr_weap = clean("_framing_weapon.txt")
    sheet_rules = clean("_sheet_rules.txt")
    rig_rules = clean("_rig_rules.txt")
    weapon_rules = clean("_weapon_rules.txt")

    # 전신 그림은 무기의 생김새와 **드는 방식**을 같이 쓴다.
    # 무기 단독 그림은 드는 방식을 쓰면 안 된다 — "물체 혼자"와 정면으로 충돌한다.
    carried = []
    for _, desc, carry, _ in sec["WEAPONS"]:
        carried += [p for p in (desc, carry) if p]
    showcase = [identity, fr_show, sec["BODY"]] + carried + [sec["SILHOUETTE"]]

    written = [
        write("_assembled_%s.txt" % form, showcase),
        write("_assembled_%s_sheet.txt" % form, showcase + [sheet_rules]),
        # 🔴 무기 없음. SILHOUETTE 도 없음 — 실루엣 문장이 전부 무기를 가리킨다.
        write("_assembled_%s_rig.txt" % form, [identity, fr_rig, sec["BODY"], rig_rules]),
    ]
    for wid, desc, _carry, orientation in sec["WEAPONS"]:
        # 일반 규칙(weapon_rules) 위에 그 무기만의 방향(orientation)을 얹는 순서.
        written.append(write(
            "_assembled_%s_weapon_%s.txt" % (form, wid),
            [identity, fr_weap, weapon_rules, desc, orientation]))
    return written


def main():
    targets = sys.argv[1:] or FORMS
    for form in targets:
        rows = build(form)
        if not rows:
            print("  %-44s 건너뜀 (form_%s.txt 없음)" % ("", form))
            continue
        for name, lines, chars in rows:
            print("  %-44s %3d줄 %5d자" % (name, lines, chars))
    print("\n모델에 넣을 때는 위 파일 내용을 그대로 붙여넣고, base 이미지를 함께 첨부할 것.")


if __name__ == "__main__":
    main()
