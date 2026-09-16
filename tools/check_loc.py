# -*- coding: utf-8 -*-
"""
문구 표(Loc)를 검사한다.

찾는 것:
  1. **한국어/영어의 자리표시자 개수가 다른 항목** — 한국어에서는 멀쩡한데
     영어로 바꾸면 글자가 깨지거나 예외가 난다. 눈으로는 거의 안 잡힌다.
  2. **부르는 쪽 인자 수와 안 맞는 항목** — string.Format이 던지거나 엉뚱하게 찍는다.
  3. 쓰이지 않는 항목 / 표에 없는 키를 부르는 곳
  4. 자리표시자 번호가 건너뛰는 것 ({0}과 {2}만 있는 등)

  python tools/check_loc.py
"""
import io
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
SCRIPTS = os.path.join(HERE, "..", "RuneCast", "Assets", "Scripts")
LOC = os.path.abspath(os.path.join(SCRIPTS, "Meta", "Loc.cs"))

W = sys.stdout.buffer.write


def p(s):
    W((s + u"\n").encode("utf-8"))


def placeholders(s):
    """{0} {1:F2} 같은 자리표시자의 번호 집합."""
    return set(int(m) for m in re.findall(r"\{(\d+)[^}]*\}", s))


def load_table():
    text = io.open(LOC, encoding="utf-8").read()
    entries = {}
    # { "key", new[] { "ko", "en" } },   — 줄바꿈이 섞여 있을 수 있다
    for m in re.finditer(
            r'\{\s*"([\w.]+)"\s*,\s*new\[\]\s*\{\s*"((?:[^"\\]|\\.)*)"\s*,\s*'
            r'"((?:[^"\\]|\\.)*)"\s*\}\s*\}', text, re.S):
        entries[m.group(1)] = (m.group(2), m.group(3))
    return entries


def find_calls():
    """
    Loc.T / Loc.F 호출을 모은다. (키, 인자 수, 파일:줄)

    **파일 전체를 한 덩어리로 읽는다.** 한 줄씩 보면 여러 줄에 걸친 호출의
    인자를 못 세서 멀쩡한 곳을 문제라고 찍는다 — 실제로 그렇게 오탐이 났다.
    """
    calls = []
    pat = re.compile(r'Loc\.([TF])\(\s*"([\w.]+)"')

    for base, _, files in os.walk(SCRIPTS):
        for f in files:
            if not f.endswith(".cs"):
                continue
            path = os.path.join(base, f)
            text = io.open(path, encoding="utf-8").read()

            for m in pat.finditer(text):
                line = text.count(chr(10), 0, m.start()) + 1
                where = "%s:%d" % (f, line)

                # "stage." + Id 처럼 문자열을 이어 붙이는 건 동적 키다. 표에 그 이름이
                # 그대로 있을 리 없으므로 "표에 없는 키"로 찍으면 안 된다.
                tail = text[m.end():m.end() + 40].lstrip()
                if tail.startswith("+"):
                    continue

                if m.group(1) == "T":
                    calls.append((m.group(2), 0, where))
                    continue

                # 괄호가 닫힐 때까지 최상위 쉼표를 센다 (줄바꿈·중첩 호출 포함).
                # 쉼표 개수 = 키 뒤에 오는 인자 개수다 — 키 자체를 세면 전부 +1이 된다.
                depth = 0
                n = 0
                i = m.end()
                while i < len(text):
                    ch = text[i]
                    if ch in "([":
                        depth += 1
                    elif ch in ")]":
                        if depth == 0:
                            break
                        depth -= 1
                    elif ch == '"':
                        i += 1
                        while i < len(text) and text[i] != '"':
                            i += 2 if text[i] == chr(92) else 1
                    elif ch == "," and depth == 0:
                        n += 1
                    i += 1
                calls.append((m.group(2), n, where))
    return calls


def dynamic_keys():
    """
    코드가 문자열을 붙여 만드는 키. 안 쓰는 것으로 오해하면 안 된다.
    예: "stage." + Id,  "tut." + 단계이름,  "credits." + 분류
    """
    text = ""
    for base, _, files in os.walk(SCRIPTS):
        for f in files:
            if f.endswith(".cs"):
                text += io.open(os.path.join(base, f), encoding="utf-8").read()

    prefixes = set(re.findall(r'"([\w.]+\.)"\s*\+', text))
    # Entries 표처럼 키를 배열에 담아 두는 경우
    prefixes |= set(re.findall(r'\{\s*"(credits)\.', text))
    return prefixes


def main():
    table = load_table()
    p(u"문구 %d개 검사\n" % len(table))
    bad = 0

    # 1) 한/영 자리표시자 불일치
    for k, (ko, en) in sorted(table.items()):
        a, b = placeholders(ko), placeholders(en)
        if a != b:
            bad += 1
            p(u"  [불일치] %s" % k)
            p(u"      한국어 %s : %s" % (sorted(a) or u"없음", ko))
            p(u"      영어   %s : %s" % (sorted(b) or u"없음", en))

    # 2) 번호 건너뜀
    for k, (ko, en) in sorted(table.items()):
        a = placeholders(ko)
        if a and sorted(a) != list(range(len(a))):
            bad += 1
            p(u"  [번호 건너뜀] %s — %s" % (k, sorted(a)))

    # 3) 호출부 인자 수
    calls = find_calls()
    used = set()
    for key, argc, where in calls:
        used.add(key)
        if key not in table:
            bad += 1
            p(u"  [표에 없는 키] %s  (%s)" % (key, where))
            continue
        need = len(placeholders(table[key][0]))
        if argc == 0 and need > 0:
            bad += 1
            p(u"  [T로 부름/서식 필요] %s — 자리표시자 %d개인데 Loc.T (%s)" % (key, need, where))
        elif argc > 0 and argc != need:
            bad += 1
            p(u"  [인자 수 불일치] %s — 필요 %d, 전달 %d  (%s)" % (key, need, argc, where))

    # 4) 안 쓰는 항목
    # Loc.T(cond ? "a" : "b") 처럼 호출 형태를 안 타는 것도 있다.
    # 소스 어디든 그 키가 문자열로 등장하면 쓰인 것으로 본다.
    #  **Loc.cs 자신은 빼야 한다.** 표에 있는 키는 당연히 Loc.cs에 적혀 있으므로,
    #  포함해서 세면 어떤 문구도 "안 쓰임"으로 안 잡힌다 — 실제로 그래서
    #  화면에서 사라진 문구 8개를 놓치고 있었다.
    all_text = ""
    for base, _, files in os.walk(SCRIPTS):
        for f in files:
            if f.endswith(".cs") and os.path.abspath(os.path.join(base, f)) != LOC:
                all_text += io.open(os.path.join(base, f), encoding="utf-8").read()
    for k in list(table):
        if '"%s"' % k in all_text:
            used.add(k)

    dyn = dynamic_keys()
    unused = sorted(k for k in set(table) - used
                    if not any(k.startswith(d) for d in dyn))
    if unused:
        p(u"\n  안 쓰는 문구 %d개: %s" % (len(unused), u", ".join(unused)))

    p(u"\n  문제 %d건" % bad)
    return 1 if bad else 0


if __name__ == "__main__":
    sys.exit(main())
