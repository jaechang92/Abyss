# -*- coding: utf-8 -*-
"""
build.py — 프롬프트 블록을 이어 붙여 **모델에 그대로 넣을 수 있는** 조립본을 만든다.

🔴 단순히 cat으로 붙이면 안 된다. 블록 파일에는 사람이 읽을 한글 주석
(`[ANCHOR — 한 글자도 바꾸지 않는다]` 같은 대괄호 줄)이 섞여 있는데,
그것까지 모델에 넘기면 **지시로 읽힌다** — "바꾸지 않는다"를 그림 내용으로
해석하거나, 한글이 섞여 스타일 해석이 흔들린다.

여기서 대괄호 메타 줄과 연속 빈 줄을 걷어내 영문 지시만 남긴다.

실행:
  python Art_Source/prompts/build.py            # 전부 재생성
  python Art_Source/prompts/build.py void_archer  # 하나만
"""
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
FORMS = ["dark_blade", "void_archer", "ancient_shield", "void_thrower"]

# 사람용 메타 줄 — 대괄호로 시작하는 줄은 전부 주석으로 본다.
META = re.compile(r"^\s*\[")

# 🔴 한글이 한 글자라도 있으면 사람용 주석이다.
#
# 프롬프트 본문은 **설계상 전부 영문**이다(앵커·폼·공유 블록 어디에도 한글 문장이 없다).
# 그래서 "한글 유무"가 주석 판정의 정확한 기준이 된다.
#
# 2026-08-20 발견: 원래는 대괄호 줄만 걷어냈는데, `_sheet_rules.txt`의 한글 주석은
# `🔴`·`·`로 시작해서 걸리지 않았다. 그 결과 **행 프롬프트마다 한글 3줄이 모델에 넘어갔다** —
# 이 파일 docstring이 막겠다고 적어 둔 바로 그 일이 조용히 일어나고 있었다.
# 접두어를 하나씩 추가하는 방식은 새 기호를 쓸 때마다 같은 구멍이 다시 난다.
HANGUL = re.compile(r"[가-힣]")


def clean(path):
    if not os.path.exists(path):
        return ""
    out = []
    for line in open(path, encoding="utf-8").read().splitlines():
        if META.match(line) or HANGUL.search(line):
            continue
        out.append(line.rstrip())
    # 연속 빈 줄을 하나로
    text = "\n".join(out)
    return re.sub(r"\n{3,}", "\n\n", text).strip()


def build(form):
    anchor = clean(os.path.join(HERE, "_anchor.txt"))
    body = clean(os.path.join(HERE, "form_%s.txt" % form))
    rules = clean(os.path.join(HERE, "_sheet_rules.txt"))
    rig = clean(os.path.join(HERE, "_rig_rules.txt"))

    # dark_blade는 기준 폼이라 form_ 블록이 없다(정체성은 _shared.txt가 들고 있다).
    # 그래서 단일·행 조립본은 예전처럼 안 만든다 — 기준 폼은 이미 그려져 있다.
    # 🔴 다만 리깅 base 포즈는 네 폼 전부 새로 뽑아야 하므로 rig 변형만 몸통을 보충한다.
    rig_body = body or (clean(os.path.join(HERE, "_shared.txt")) if form == "dark_blade" else "")

    variants = []
    if body:
        variants.append(("", [anchor, body]))
        variants.append(("_sheet", [anchor, body, rules]))
    if rig_body and rig:
        # 🔴 _rig_rules는 반드시 마지막이다 — 앵커의 "three-quarter" 프레이밍을 뒤에서 덮는다.
        variants.append(("_rig", [anchor, rig_body, rig]))
    if not variants:
        return []

    written = []
    for suffix, parts in variants:
        text = "\n\n".join(p for p in parts if p) + "\n"
        path = os.path.join(HERE, "_assembled_%s%s.txt" % (form, suffix))
        open(path, "w", encoding="utf-8").write(text)
        written.append((os.path.basename(path), len(text.splitlines()), len(text)))
    return written


def main():
    targets = sys.argv[1:] or FORMS
    for form in targets:
        for name, lines, chars in build(form):
            print("  %-40s %3d줄 %5d자" % (name, lines, chars))
    print("\n모델에 넣을 때는 위 파일 내용을 그대로 붙여넣고, base 이미지를 함께 첨부할 것.")


if __name__ == "__main__":
    main()
