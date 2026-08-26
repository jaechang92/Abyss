# -*- coding: utf-8 -*-
"""
build.py — 정체성 + 규칙 + 상태를 합쳐 NovelAI 에 붙여넣을 프롬프트를 만든다.

사용법:
    python Art_Source/prompts/build.py              # 전 상태
    python Art_Source/prompts/build.py idle run     # 지정 상태만

산출물은 assembled/ 아래에 나온다(git 비추적 — 언제든 다시 만들어진다).

🔴 한글 유출 차단이 이 스크립트의 두 번째 역할이다.
   주석은 한글로 쓰되 프롬프트 본문에는 한 글자도 섞이면 안 된다.
   예전에 주석이 조립본에 딸려 들어가 프롬프트에 한국어가 섞인 적이 있다.
   모델은 그걸 무시하지 않고 "한국어가 적힌 그림"을 그리려 든다.
   그래서 조립 후 한글이 남아 있으면 파일을 쓰지 않고 즉시 실패한다.
"""
import os
import re
import sys

ROOT = os.path.dirname(os.path.abspath(__file__))
OUT_DIR = os.path.join(ROOT, "assembled")
STATE_DIR = os.path.join(ROOT, "states")

# 한글 자모·음절 전체 범위
HANGUL = re.compile(r"[가-힣ᄀ-ᇿ㄰-㆏]")

# 상태 순서 — PlayerAnimationIds 의 9종과 같은 순서로 둔다.
STATES = ["idle", "run", "jump", "fall", "dash",
          "attack_light", "attack_heavy", "hit", "dead"]


def parse(path):
    """[BLOCK] 로 갈린 섹션을 읽는다. # 로 시작하는 줄은 주석이라 버린다."""
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
    return {k: "\n".join(v).strip() for k, v in sections.items()}


def joined(*parts):
    """빈 조각을 빼고 쉼표로 잇는다. 줄바꿈은 그대로 둔다 — 붙여넣기에 읽기 좋다."""
    return ",\n".join(p.strip().rstrip(",") for p in parts if p and p.strip())


def check_hangul(text, label):
    found = HANGUL.findall(text)
    if found:
        sample = "".join(sorted(set(found))[:12])
        raise SystemExit(
            f"🔴 한글 유출: {label}\n"
            f"   섞인 글자: {sample}\n"
            f"   프롬프트 본문에 한글이 들어갔다. 해당 줄을 '#' 주석으로 옮길 것."
        )


def build_state(name, char, rules):
    path = os.path.join(STATE_DIR, f"{name}.txt")
    if not os.path.exists(path):
        print(f"  건너뜀 (파일 없음): {name}")
        return []

    st = parse(path)
    use_weapon = st.get("USE-WEAPON", "").lower().startswith("y")
    weapon_block = char["WEAPON"] if use_weapon else char["NO-WEAPON"]

    # 정체성이 먼저 온다 — 앞에 놓인 태그가 더 세게 작용한다.
    head = joined(
        char["CORE"],
        char["IDENTITY-LOCK"],
        weapon_block,
        char["SILHOUETTE"],
        rules["FRAMING"],
        rules["QUALITY"],
        rules["ALPHA"],
    )

    results = []
    frame_keys = sorted(
        (k for k in st if k.startswith("FRAME ")),
        key=lambda k: int(k.split()[1]),
    )
    for key in frame_keys:
        n = int(key.split()[1])
        body = joined(head, st.get("BASE-POSE", ""), st[key])
        results.append((f"{name}_f{n:02d}", body))

    if "SHEET" in st:
        results.append((f"{name}_sheet", joined(head, st["SHEET"])))

    return results


def main():
    char = parse(os.path.join(ROOT, "01_character.txt"))
    rules = parse(os.path.join(ROOT, "02_rules.txt"))

    for need, src in ((("CORE", "IDENTITY-LOCK", "SILHOUETTE", "WEAPON", "NO-WEAPON"), "01_character.txt"),
                      (("FRAMING", "QUALITY", "ALPHA", "NEGATIVE"), "02_rules.txt")):
        missing = [k for k in need if k not in (char if src.startswith("01") else rules)]
        if missing:
            raise SystemExit(f"🔴 {src} 에 블록이 없다: {', '.join(missing)}")

    negative = rules["NEGATIVE"]
    negative_sheet = rules["NEGATIVE-SHEET"]
    check_hangul(negative, "02_rules.txt [NEGATIVE]")
    check_hangul(negative_sheet, "02_rules.txt [NEGATIVE-SHEET]")

    wanted = sys.argv[1:] or STATES
    os.makedirs(OUT_DIR, exist_ok=True)

    catalog, count = [], 0
    for name in wanted:
        print(f"조립: {name}")
        for label, body in build_state(name, char, rules):
            check_hangul(body, f"{label}")
            # 🔴 시트는 부정 프롬프트가 다르다. 낱장용을 그대로 쓰면
            #    "여러 컷을 그리지 마라"가 시트 프롬프트와 부딪힌다.
            neg = negative_sheet if label.endswith("_sheet") else negative
            with open(os.path.join(OUT_DIR, f"{label}.txt"), "w", encoding="utf-8") as f:
                f.write(body + "\n\n--- UNDESIRED CONTENT ---\n\n" + neg + "\n")
            catalog.append((label, body))
            count += 1

    # 한 장에 모은 카탈로그 — 브라우저에 옮겨 담을 때 파일을 하나씩 열지 않아도 된다.
    with open(os.path.join(OUT_DIR, "00_ALL.md"), "w", encoding="utf-8") as f:
        f.write("# dark_blade — 조립된 프롬프트 전체\n\n")
        f.write("각 항목의 코드블록이 Prompt 다. Undesired Content 는 맨 아래 두 블록에 있다 —\n")
        f.write("**낱장 프레임과 시트가 서로 다르다.** 시트에 낱장용을 쓰면\n")
        f.write("\"여러 컷을 그리지 마라\"가 시트 프롬프트와 부딪힌다.\n\n")
        for label, body in catalog:
            kind = "시트" if label.endswith("_sheet") else "낱장"
            f.write(f"## {label}  <sub>({kind})</sub>\n\n```\n{body}\n```\n\n")
        f.write(f"## UNDESIRED CONTENT — 낱장 프레임용\n\n```\n{negative}\n```\n\n")
        f.write(f"## UNDESIRED CONTENT — 시트(멀티패널)용\n\n```\n{negative_sheet}\n```\n")

    print(f"\n완료 — {count}개 → {os.path.relpath(OUT_DIR, os.path.dirname(ROOT))}")
    print("   카탈로그: assembled/00_ALL.md")


if __name__ == "__main__":
    main()
