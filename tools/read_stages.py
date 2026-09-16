# -*- coding: utf-8 -*-
"""
StageDef.cs에서 스테이지 표를 직접 읽는다.

**손으로 옮기면 틀린다.** 실제로 한 번 틀렸고, 그 잘못된 표로 밸런스를 재고 있었다 —
5판의 용사 수와 물결 구성이 실제와 달랐다. 시뮬레이터가 게임과 다른 걸 재고 있으면
그 숫자는 아무 의미가 없다.

정규식으로 읽는 게 견고하진 않지만, 이 파일은 형식이 일정하고
**틀리면 조용히 넘어가지 않고 예외로 죽는다** — 손으로 옮기는 것보다 안전하다.
"""
import io
import os
import re

HERE = os.path.dirname(os.path.abspath(__file__))
SRC = os.path.join(HERE, "..", "RuneCast", "Assets", "Scripts", "Meta", "StageDef.cs")


def strip_comments(text):
    """
    줄 주석을 걷어낸다.

    **주석이 파싱에 영향을 주면 안 된다.** 물결 사이에 설명을 한 줄 넣었더니
    9·10·12판의 물결이 하나로 합쳐져서 읽혔다. 물결을 찾는 정규식이 `),` 다음에
    공백만 두고 `new WaveDef`가 오기를 기대했는데, 그 사이에 주석이 끼면 조건이
    깨져서 다음 물결까지 한 덩어리로 삼켰다.

    조용히 틀린 값을 내놓는 종류라 더 나쁘다 — 파일은 멀쩡하고 표만 틀린다.
    설명을 적었다는 이유로 밸런스 수치가 달라 보이면 안 된다.

    따옴표 안의 // 는 남긴다. 지금 StageDef에는 그런 문자열이 없지만
    이 함수가 그걸 아는 척할 이유도 없다.
    """
    out = []
    for line in text.split(u"\n"):
        quoted = False
        cut = None
        for i in range(len(line) - 1):
            c = line[i]
            if c == u'"' and (i == 0 or line[i - 1] != u"\\"):
                quoted = not quoted
            elif not quoted and c == u"/" and line[i + 1] == u"/":
                cut = i
                break
        out.append(line if cut is None else line[:cut])
    return u"\n".join(out)


def load():
    text = strip_comments(io.open(SRC, encoding="utf-8").read())

    # BuildAll 안의 s.Add(new StageDef { ... }); 덩어리들
    body = text[text.index("private static List<StageDef> Build"):]
    blocks = re.findall(r"s\.Add\(new StageDef\s*\{(.*?)\n            \}\);", body, re.S)
    if not blocks:
        raise RuntimeError("StageDef 블록을 못 찾았다 — 파일 형식이 바뀌었나?")

    stages = []
    for b in blocks:
        def one(pat, default=None, cast=float):
            m = re.search(pat, b)
            return cast(m.group(1)) if m else default

        sid = one(r"Id\s*=\s*(\d+)", cast=int)
        heroes = one(r"HeroCount\s*=\s*(\d+)", 4, int)
        hero_hp = one(r"HeroHp\s*=\s*([\d.]+)f", 100.0)
        hero_dmg = one(r"HeroDamage\s*=\s*([\d.]+)f", 8.0)

        mana = None
        if re.search(r"UnlimitedMana\s*=\s*false", b):
            mana = (one(r"MaxMana\s*=\s*([\d.]+)f", 100.0),
                    one(r"ManaRegen\s*=\s*([\d.]+)f", 9.0))

        waves = []
        for wave_src in re.findall(r"new WaveDef\((.*?)\)\s*,\s*(?=new WaveDef|\}\s*,)", b + "\n},", re.S):
            groups = []
            for g in re.finditer(
                    r"new WaveGroup\(UnitKind\.(\w+)\s*,\s*(\d+)"
                    r"(?:\s*,\s*([\d.]+)f)?(?:\s*,\s*([\d.]+)f)?\s*\)", wave_src):
                kind = g.group(1)
                cnt = int(g.group(2))
                hs = float(g.group(3)) if g.group(3) else 1.0
                ds = float(g.group(4)) if g.group(4) else 1.0
                groups.append((kind, cnt, hs, ds))
            if groups:
                waves.append(groups)

        if not waves:
            raise RuntimeError("%d판의 물결을 못 읽었다" % sid)

        stages.append(dict(id=sid, chapter=(sid - 1) // 6 + 1,
                           heroes=heroes, heroHp=hero_hp, heroDmg=hero_dmg,
                           mana=mana, waves=waves))

    stages.sort(key=lambda s: s["id"])
    return stages


def load_all():
    """전 42판. **이제 전부 손으로 적혀 있어서 load() 하나면 된다.**

    한때 3~7장이 C# 반복문으로 찍혀 있어 여기 사본을 들고 있었다. 사본은
    원본이 바뀌면 조용히 어긋나므로, 판을 제대로 짜면서 같이 없앴다."""
    return load()


if __name__ == "__main__":
    import sys
    stages = load()

    # 한때 3~7장이 C# 반복문으로 찍혀 있어 여기 정규식에 안 걸렸고, 그래서
    # "12개가 전부"로 읽힐 위험이 있었다. 지금은 42판이 전부 손으로 적혀 있어
    # 그대로 읽힌다 — 그래도 몇 개를 읽었는지는 말해 준다. 파일 형식이 바뀌어
    # 일부를 놓치면 이 숫자가 먼저 알려 준다.
    sys.stdout.buffer.write((u"판 %d개를 읽었다.\n\n" % len(stages)).encode("utf-8"))

    for st in stages:
        line = u"%2d판  용사%d  마나%s" % (
            st["id"], st["heroes"],
            u"무한" if st["mana"] is None else u"%.0f/%.0f" % st["mana"])
        for w in st["waves"]:
            line += u"\n        " + u"  ".join(
                u"%s×%d%s" % (k, c, u"" if (h == 1 and d == 1) else u"(%.2f/%.2f)" % (h, d))
                for k, c, h, d in w)
        sys.stdout.buffer.write((line + u"\n").encode("utf-8"))
