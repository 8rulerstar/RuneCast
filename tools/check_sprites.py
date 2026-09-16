# -*- coding: utf-8 -*-
"""
유닛 스프라이트가 코드가 기대하는 대로 들어와 있는지 본다.

**틀려도 컴파일은 통과한다.** 시트를 못 찾으면 런타임에 경고 한 줄이 뜨고
유닛이 투명해질 뿐이라, 에디터를 켜서 그 판까지 가 봐야 안다.
프레임 크기가 어긋나면 더 나쁘다 — 잘리긴 잘려서 캐릭터 반쪽이 걸어다닌다.

세 가지를 본다:
  1. UnitVisuals.Of가 부르는 파일이 실제로 있는가
  2. 시트 가로가 프레임 크기로 나누어떨어지는가
  3. 시트 세로가 프레임 크기와 같은가 (가로 스트립이라 그래야 한다)

  python tools/check_sprites.py
"""
import io
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
VIS = os.path.join(ROOT, "RuneCast", "Assets", "Scripts", "Battle", "UnitVisuals.cs")
UNITS = os.path.join(ROOT, "RuneCast", "Assets", "Resources", "Sprites", "Units")

# UnitAnimator가 찾는 클립들. Hurt와 Death는 없어도 되지만 알려는 준다.
REQUIRED = ["Idle", "Walk"]
OPTIONAL = ["Hurt", "Death"]


def defs():
    """UnitVisuals.Of의 표를 읽는다."""
    text = io.open(VIS, encoding="utf-8").read()
    out = []
    pat = re.compile(
        r"case UnitKind\.(\w+):\s*\n\s*return new UnitVisualDef\s*\{([^}]*)\}", re.S)
    for m in pat.finditer(text):
        kind = m.group(1)
        body = m.group(2)

        def field(name):
            f = re.search(name + r"\s*=\s*\"?([\w.]+)\"?", body)
            return f.group(1) if f else None

        out.append({
            "kind": kind,
            "prefix": field("Prefix"),
            "attack": field("AttackClip"),
            "frame": int(float(field("FrameSize"))),
        })
    return out


def main():
    try:
        from PIL import Image
    except ImportError:
        print("Pillow가 필요합니다:  pip install pillow")
        return 1

    W = sys.stdout.buffer.write

    def say(s):
        W((s + u"\n").encode("utf-8"))

    rows = defs()
    if not rows:
        say(u"UnitVisuals.Of에서 표를 못 읽었습니다 — 형식이 바뀌었나?")
        return 1

    bad = 0
    say(u"유닛 스프라이트 %d종" % len(rows))
    say(u"")

    for d in rows:
        clips = REQUIRED + [d["attack"]] + OPTIONAL
        missing = []
        notes = []

        for clip in clips:
            path = os.path.join(UNITS, "%s-%s.png" % (d["prefix"], clip))
            if not os.path.exists(path):
                if clip in OPTIONAL:
                    notes.append(clip)
                else:
                    missing.append(clip)
                continue

            im = Image.open(path)
            fs = d["frame"]
            if im.height != fs:
                say(u"  ! %s-%s.png 세로가 %d인데 FrameSize는 %d다" % (d["prefix"], clip, im.height, fs))
                bad += 1
            elif im.width % fs != 0:
                say(u"  ! %s-%s.png 가로 %d가 %d로 안 나누어떨어진다 (프레임이 잘린다)"
                    % (d["prefix"], clip, im.width, fs))
                bad += 1

        mark = u"OK " if not missing else u"!! "
        line = u"  %s %-9s %-9s %3dpx" % (mark, d["kind"], d["prefix"], d["frame"])
        if missing:
            line += u"   없음: " + u", ".join(missing)
            bad += len(missing)
        if notes:
            line += u"   (선택 클립 없음: %s)" % u", ".join(notes)
        say(line)

    say(u"")
    say(u"문제 없음" if bad == 0 else u"%d건" % bad)
    return 1 if bad else 0


if __name__ == "__main__":
    sys.exit(main())
