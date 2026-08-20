# -*- coding: utf-8 -*-
"""
build.py — 프롬프트 블록을 이어 붙여 **모델에 그대로 넣을 수 있는** 조립본을 만든다.

🔴 단순히 cat으로 붙이면 안 된다. 블록 파일에는 사람이 읽을 한글 주석이 섞여 있는데,
그것까지 모델에 넘기면 **지시로 읽힌다** — "바꾸지 않는다"를 그림 내용으로 해석하거나,
한글이 섞여 스타일 해석이 흔들린다. 여기서 걷어내고 영문 지시만 남긴다.

실행:
  python Art_Source/1_prompts/build.py              # 전부 재생성
  python Art_Source/1_prompts/build.py void_archer  # 하나만


폴더
----
    1_blocks/     공용 블록. ❌ 전부 수정 금지 — 하나하나가 실측 결함에 대응한다
    2_forms/      폼별 블록. ✅ 수정 가능
    poses/        행 이미지용 포즈 문구 예시. 조립에 안 들어간다(dark_blade 기준)
    legacy/       블록 구조 이전의 1회성 초안. 이력으로만 남긴다
    3_assembled/  ← 여기로 나온다. 손으로 고치지 말 것(다음 실행에 덮어쓴다)


공용 블록 (2026-08-20 3차 재편)
-------------------------------
쓰이는 **범위**가 다른 것들을 계속 갈라 왔다. 지금 축은 셋이다.

    reference_showcase / _rig / _weapon   기준 이미지에서 **무엇을 베끼는가**
    style                                  그림체 — 넷 다 공통
    identity_figure                        인물 — **사람이 나올 때만**
    framing_showcase / _rig / _weapon      화면에 무엇이 어떻게 들어가는가
    sheet_rules / rig_rules / weapon_rules 그 용도의 제약

📌 갈라 온 이력이 그대로 결함 목록이다.
   ① `_anchor.txt` 가 정체성 + 프레이밍을 같이 들고 있었다
      → 리깅 프롬프트가 "three-quarter" 뒤에 "NOT three-quarter"로 자기 말을 뒤집었다
   ② `_identity.txt` 가 BASE IMAGE + STYLE + IDENTITY 를 같이 들고 있었다
      → 무기 단독 프롬프트가 "물체 하나뿐"이라 해 놓고 후드 쓴 사람을 서술했다
   ③ BASE IMAGE 문단이 셋 공용이라 "exactly 맞춰라"가 리깅에도 들어갔다
      → **첨부 이미지의 대검이 "hands empty" 한 줄과 매번 싸웠다.**
        이미지 조건은 텍스트 부정문보다 세다 — 부정문을 더 쌓는 대신
        `reference_rig.txt` 가 **베낄 범위에서 검을 빼낸다.**

🔑 셋 다 같은 모양의 결함이다 — **한 블록이 서로 다른 범위의 것을 같이 들고 있으면,
   그 블록을 쓰는 조립본 중 하나는 반드시 자기 말을 뒤집는다.**

폼 블록(`2_forms/form_<id>.txt`)은 대괄호 절 표시로 나뉜다.

    [BODY]                    몸 — 방어구·색·체격
    [RIG-EXCLUDE]             그 폼의 무기가 **이 그림에 없다**는 서술 (리깅 전용)
    [SILHOUETTE]              실루엣 — 전신 기준
    [WEAPON: <id>]            그 무기의 **생김새**
    [WEAPON-CARRY: <id>]      캐릭터가 **드는 방식** (전신 그림 전용)
    [WEAPON-IMAGE: <id>]      **단독으로 그릴 때의 방향** (무기 그림 전용)

🔑 무기가 셋으로 갈린 이유는 절마다 쓰는 곳이 다르기 때문이다.
   생김새는 둘 다 쓰지만, "오른손에 들고 옆으로 늘어뜨린다"는 전신 그림에만,
   "똑바로 세워 손잡이를 아래로"는 무기 그림에만 해당한다.
   합쳐 두면 무기 단독 프롬프트가 "물체 혼자"라고 해 놓고 "왼팔에 끼고 몸 옆에"라고
   덧붙이게 된다 — 실제로 재작성 전 조립본이 그랬다.


조립본 (3_assembled/)
-------------------
    <form>.txt                reference_showcase + style + identity + framing_showcase
                              + BODY + (WEAPON + CARRY)들 + SILHOUETTE
    <form>_sheet.txt          위 + sheet_rules
    <form>_rig.txt            reference_rig + style + identity + framing_rig
                              + BODY + RIG-EXCLUDE + rig_rules       ← 무기 없음

🔑 [RIG-EXCLUDE] 가 BODY **바로 뒤**에 오는 것은 순서가 중요해서다.
   [BODY]는 폼을 "shieldbearer" · "archer" · "thrower"라고 부른다 — 역할 명사 자체가
   무기를 소환한다. 그 문장 바로 다음에서 취소해야 한다.
   맨 뒤로 밀면 긴 규칙 블록에 묻힌다.
    <form>_weapon_<id>.txt    reference_weapon + style + framing_weapon
                              + weapon_rules + WEAPON + WEAPON-IMAGE  ← 인물 없음

🔴 리깅 조립본에서 SILHOUETTE 을 뺀 것은 실수가 아니다 — 실루엣 문장이 전부 무기를 가리킨다
   ("the great arc of the bow beside the body"). 무기 없는 몸을 뽑는 프롬프트에 넣으면
   모델이 활을 그린다.

🔴 무기 조립본에 identity_figure 가 없는 것도 실수가 아니다 — 거울상 결함이다.
   들어가면 "그 마른 성인 남자, 얼굴을 덮은 후드…"를 서술한 뒤
   프레이밍이 "no character, no person"으로 뒤집는다.

📌 3_assembled/ 는 git 추적 대상이 아니다. 1_blocks/ · 2_forms/ · build.py 만 있으면
   언제든 다시 만들어지므로, README 의 추적 기준("재생성 가능한가")에 그대로 걸린다.
"""
import io
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
BLOCKS = os.path.join(HERE, "1_blocks")
FORMS_DIR = os.path.join(HERE, "2_forms")
OUT = os.path.join(HERE, "3_assembled")

FORMS = ["dark_blade", "void_archer", "ancient_shield", "void_thrower"]

# 사람용 주석 블록의 여는 줄. 대괄호로 시작하면 주석이 열린다(절 표시도 여기 걸려 사라진다).
#
# 🔴 2026-08-20 3차 — 주석은 **줄 단위가 아니라 블록 단위**로 걷어내야 한다.
# 예전에는 "대괄호로 시작하거나 한글이 있는 줄"만 버렸다. 그런데 주석이 여러 줄에 걸치고
# 그 안에서 **옛 영문 원문을 인용하면**, 인용 줄은 대괄호로 시작하지도 않고 한글도 없어서
# 그대로 통과한다. 실제로 리깅 프롬프트에
#     "nothing held in either hand — no weapon, no shield, no bow, no javelin, no staff"
# 라는 **삭제한 문장이 주석 인용을 타고 다시 실려 나갔다.**
# 고치려던 결함을 설명하는 글이 그 결함을 재현시킨 경우다.
#
# 이제 여는 대괄호부터 닫는 `]`까지를 통째로 버린다. 한 줄에서 닫히면 그 줄만 버린다.
META = re.compile(r"^\s*\[")

# 🔴 한글이 한 글자라도 있으면 사람용 주석이다.
#
# 프롬프트 본문은 **설계상 전부 영문**이다(참조·스타일·정체성·프레이밍·폼 블록 어디에도
# 한글 문장이 없다). 그래서 "한글 유무"가 주석 판정의 정확한 기준이 된다.
#
# 2026-08-20 발견: 원래는 대괄호 줄만 걷어냈는데, `sheet_rules.txt`의 한글 주석은
# `🔴`·`·`로 시작해서 걸리지 않았다. 그 결과 **행 프롬프트마다 한글 3줄이 모델에 넘어갔다** —
# 이 파일 docstring이 막겠다고 적어 둔 바로 그 일이 조용히 일어나고 있었다.
# 접두어를 하나씩 추가하는 방식은 새 기호를 쓸 때마다 같은 구멍이 다시 난다.
HANGUL = re.compile(r"[가-힣]")

# 폼 블록의 절 표시. 대괄호로 시작하므로 출력에서는 META 가 걷어낸다.
SECTION = re.compile(
    r"^\s*\[(BODY|SILHOUETTE|RIG-EXCLUDE|WEAPON|WEAPON-CARRY|WEAPON-IMAGE)"
    r"(?:\s*:\s*([A-Za-z0-9_]+))?\]\s*$")


def strip_comments(lines, where=""):
    """대괄호 주석 블록과 한글 줄을 걷어내고 연속 빈 줄을 정리한다."""
    out = []
    open_at = None          # 열려 있는 주석 블록의 시작 줄 번호(사람이 찾을 수 있게)
    for no, raw in enumerate(lines, 1):
        line = raw.rstrip()
        if open_at is None:
            if META.match(line):
                # 같은 줄에서 닫히면 그 줄만 버린다(절 표시·한 줄 주석).
                if not line.endswith("]"):
                    open_at = no
                continue
            if HANGUL.search(line):
                continue
            out.append(line)
        elif line.endswith("]"):
            open_at = None
    if open_at is not None:
        # 🔴 안 닫힌 주석은 그 뒤 지시를 통째로 삼킨다. 조용히 넘기면 프롬프트가 반쪽이 된다.
        raise SystemExit("%s %d번째 줄에서 연 대괄호 주석이 안 닫혔다" % (where, open_at))
    return re.sub(r"\n{3,}", "\n\n", "\n".join(out)).strip()


def read_lines(path):
    if not os.path.exists(path):
        return []
    return io.open(path, encoding="utf-8").read().splitlines()


def block(name):
    """blocks/ 의 공용 블록 하나를 읽어 영문 지시만 남긴다."""
    rel = "1_blocks/%s.txt" % name
    text = strip_comments(read_lines(os.path.join(BLOCKS, name + ".txt")), rel)
    if not text:
        raise SystemExit("1_blocks/%s.txt 가 없거나 영문 본문이 비었다" % name)
    return text


def parse_form(form):
    """
    forms/form_<id>.txt 를 절 단위로 가른다.

    반환: {"BODY": str, "RIG-EXCLUDE": str, "SILHOUETTE": str,
           "WEAPONS": [(weapon_id, desc, carry, image_orientation), ...]}

    무기는 **파일에 적힌 순서**를 지킨다 — 고대 방패병은 탑실드가 먼저이고 단검이 나중인데,
    그게 실루엣의 주인공 순서다.
    """
    rel = "2_forms/form_%s.txt" % form
    lines = read_lines(os.path.join(FORMS_DIR, rel.split("/")[-1]))
    if not lines:
        return None

    # 🔴 먼저 파일 전체로 한 번 훑는다. 안 닫힌 주석은 여기서만 **실제 줄 번호**로 잡힌다 —
    #    절별로 자른 뒤에는 번호가 절 기준이라 사람이 못 찾는다.
    strip_comments(lines, rel)

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
        return strip_comments(buckets.get(k, []), rel)

    weapons = []
    for wid in order:
        weapons.append((wid,
                        take(("WEAPON", wid)),
                        take(("WEAPON-CARRY", wid)),
                        take(("WEAPON-IMAGE", wid))))

    return {"BODY": take("BODY"),
            "RIG-EXCLUDE": take("RIG-EXCLUDE"),
            "SILHOUETTE": take("SILHOUETTE"),
            "WEAPONS": weapons}


def write(name, parts):
    text = "\n\n".join(p for p in parts if p) + "\n"
    if HANGUL.search(text):
        # 🔴 마지막 방어선. 여기 걸리면 블록 파일에 주석 아닌 한글이 섞인 것이다.
        raise SystemExit("%s 에 한글이 섞였다 — 블록 파일을 확인할 것" % name)
    io.open(os.path.join(OUT, name), "w", encoding="utf-8", newline="\n").write(text)
    return (name, len(text.splitlines()), len(text))


def build(form):
    sec = parse_form(form)
    if sec is None or not sec["BODY"]:
        return []

    if not sec["RIG-EXCLUDE"]:
        # 🔴 없으면 그 폼의 리깅 프롬프트가 무기를 다시 부른다. 조용히 넘기지 않는다.
        raise SystemExit("form_%s.txt 에 [RIG-EXCLUDE] 절이 없다" % form)

    ref_show = block("reference_showcase")
    ref_rig = block("reference_rig")
    ref_weap = block("reference_weapon")
    style = block("style")
    figure = block("identity_figure")
    fr_show = block("framing_showcase")
    fr_rig = block("framing_rig")
    fr_weap = block("framing_weapon")
    sheet_rules = block("sheet_rules")
    rig_rules = block("rig_rules")
    weapon_rules = block("weapon_rules")

    # 전신 그림은 무기의 생김새와 **드는 방식**을 같이 쓴다.
    # 무기 단독 그림은 드는 방식을 쓰면 안 된다 — "물체 혼자"와 정면으로 충돌한다.
    carried = []
    for _, desc, carry, _ in sec["WEAPONS"]:
        carried += [p for p in (desc, carry) if p]
    showcase = [ref_show, style, figure, fr_show, sec["BODY"]] + carried + [sec["SILHOUETTE"]]

    written = [
        write("%s.txt" % form, showcase),
        write("%s_sheet.txt" % form, showcase + [sheet_rules]),
        # 🔴 무기 없음. SILHOUETTE 도 없음 — 실루엣 문장이 전부 무기를 가리킨다.
        #    RIG-EXCLUDE 는 BODY 바로 뒤 — 역할 명사를 그 자리에서 취소한다.
        write("%s_rig.txt" % form,
              [ref_rig, style, figure, fr_rig, sec["BODY"], sec["RIG-EXCLUDE"], rig_rules]),
    ]
    for wid, desc, _carry, orientation in sec["WEAPONS"]:
        # 🔴 identity_figure 없음 — 넣으면 "물체 하나뿐"이라 해 놓고 사람을 서술하게 된다.
        #    일반 규칙(weapon_rules) 위에 그 무기만의 방향(orientation)을 얹는 순서.
        written.append(write(
            "%s_weapon_%s.txt" % (form, wid),
            [ref_weap, style, fr_weap, weapon_rules, desc, orientation]))
    return written


def main():
    if not os.path.isdir(OUT):
        os.makedirs(OUT)
    targets = sys.argv[1:] or FORMS
    for form in targets:
        rows = build(form)
        if not rows:
            print("  %-40s 건너뜀 (2_forms/form_%s.txt 없음)" % ("", form))
            continue
        for name, lines, chars in rows:
            print("  3_assembled/%-30s %3d줄 %5d자" % (name, lines, chars))
    print("\n모델에 넣을 때는 3_assembled/ 의 파일 내용을 그대로 붙여넣고, base 이미지를 함께 첨부할 것.")


if __name__ == "__main__":
    main()
