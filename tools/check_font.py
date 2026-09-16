# -*- coding: utf-8 -*-
"""
글꼴이 어긋날 수 있는 두 가지를 확인한다.

**1. 폰트에 없는 글자를 쓰는가.** 잘라낸 글꼴에 없는 글자는 화면에 네모로 뜬다.
   make_font.py를 안 돌리고 문장만 고치면 바로 이렇게 된다.

**2. 사다리 밖의 글자 크기를 쓰는가.** 이게 더 고약하다. FontWarmup이 시작할 때
   아틀라스를 다 채워두는데, 목록에 없는 크기를 쓰면 그 크기의 글자가 처음
   뜨는 순간 **아틀라스가 통째로 다시 짜인다.** 다시 짜이면 이미 그려둔 글자들이
   엉뚱한 글자로 보인다 — "이어하기"가 "이어호기"로 보이던 그 문제다.

   조용히 되살아나는 종류의 버그라 사람이 기억으로 막을 수 없다.

  python tools/check_font.py
"""
import io
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SCRIPTS = os.path.join(ROOT, "RuneCast", "Assets", "Scripts")
FONT = os.path.join(ROOT, "RuneCast", "Assets", "Resources", "Fonts", "Galmuri11.ttf")

# FontWarmup이 미리 굽는 크기. 여기 없는 걸 쓰면 아틀라스가 다시 짜인다.
ALLOWED = {
    "UiSkin.Text.Small", "UiSkin.Text.Mid", "UiSkin.Text.Large",
    "UiSkin.Text.Title", "UiSkin.Text.Huge", "UiSkin.Text.Glyph",
    "Text.Small", "Text.Mid", "Text.Large", "Text.Title", "Text.Huge", "Text.Glyph",
    # 타이틀 로고 전용(64). 별·룬 도형과 같은 예외 — 굽는 글자가 대문자 27자뿐이다.
    # FontWarmup이 LogoChars를 이 크기로 미리 굽는다.
    "UiSkin.Text.Logo", "Text.Logo",
}

LIT = re.compile(r'"((?:[^"\\]|\\.)*)"')
ESC = re.compile(r'\\u([0-9a-fA-F]{4})')
SIZE = re.compile(r'fontSize\s*=\s*([^,;}\)\n]+)')


def cs_files():
    for base, _, files in os.walk(SCRIPTS):
        for f in sorted(files):
            if f.endswith(".cs"):
                yield os.path.join(base, f)


def rel(p):
    return os.path.relpath(p, ROOT).replace("\\", "/")


def check_sizes(say):
    bad = 0
    for path in cs_files():
        if os.path.basename(path) in ("UiSkin.cs", "FontWarmup.cs"):
            continue  # 사다리를 정의하는 곳
        for i, line in enumerate(io.open(path, encoding="utf-8"), 1):
            for m in SIZE.finditer(line):
                expr = m.group(1).strip()
                if expr in ALLOWED:
                    continue
                # Snap()은 사다리 안으로 되돌려주므로 괜찮다
                if "Text.Snap(" in expr:
                    continue
                say(u"  %s:%d  %s" % (rel(path), i, expr))
                bad += 1
    return bad


def check_glyphs(say):
    try:
        from fontTools.ttLib import TTFont
    except ImportError:
        say(u"  fonttools가 없어 글자 검사를 건너뜁니다:  pip install fonttools")
        return 0

    if not os.path.exists(FONT):
        say(u"  폰트가 없습니다: " + rel(FONT))
        return 1

    have = set()
    for t in TTFont(FONT)["cmap"].tables:
        have.update(t.cmap.keys())

    missing = {}
    for path in cs_files():
        text = io.open(path, encoding="utf-8").read()
        for m in LIT.finditer(text):
            body = ESC.sub(lambda e: chr(int(e.group(1), 16)), m.group(1))
            for c in body:
                if ord(c) < 0x20 or ord(c) in have:
                    continue
                missing.setdefault(c, rel(path))

    for c in sorted(missing):
        say(u"  '%s' (U+%04X)  %s" % (c, ord(c), missing[c]))
    return len(missing)


def main():
    W = sys.stdout.buffer.write

    def say(s):
        W((s + u"\n").encode("utf-8"))

    say(u"글자 크기 — FontWarmup이 굽는 사다리 안인가")
    n1 = check_sizes(say)
    say(u"  문제 없음" if n1 == 0 else u"  %d곳. 아틀라스가 다시 짜여서 글자가 뒤바뀝니다." % n1)

    say(u"")
    say(u"글자 — 잘라낸 폰트에 다 들어 있는가")
    n2 = check_glyphs(say)
    say(u"  문제 없음" if n2 == 0 else u"  %d자 빠짐. python tools/make_font.py 를 돌리세요." % n2)

    return 1 if (n1 or n2) else 0


if __name__ == "__main__":
    sys.exit(main())
