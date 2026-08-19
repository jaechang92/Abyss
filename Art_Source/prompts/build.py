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


def clean(path):
    if not os.path.exists(path):
        return ""
    out = []
    for line in open(path, encoding="utf-8").read().splitlines():
        if META.match(line):
            continue
        out.append(line.rstrip())
    # 연속 빈 줄을 하나로
    text = "\n".join(out)
    return re.sub(r"\n{3,}", "\n\n", text).strip()


def build(form):
    anchor = clean(os.path.join(HERE, "_anchor.txt"))
    body = clean(os.path.join(HERE, "form_%s.txt" % form))
    rules = clean(os.path.join(HERE, "_sheet_rules.txt"))
    if not body:
        return []

    written = []
    for suffix, parts in (("", [anchor, body]), ("_sheet", [anchor, body, rules])):
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
