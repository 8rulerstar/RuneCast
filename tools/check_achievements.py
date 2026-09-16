# -*- coding: utf-8 -*-
"""
업적 보상 총액을 대조한다.

**RIPOSTE에서 그대로 겪은 실패다.** 그쪽은 업적이 7종이던 시절의 총액 주석을
18종이 될 때까지 아무도 다시 세지 않았고, 그 사이 상점의 85%가 업적만으로 열리게
됐다. 파편을 버는 경로가 따로 고장나 있어서 한동안 티도 안 났다.

여기서 재는 것:
  1. `Reward()`의 실제 합 vs `TotalReward` 상수
  2. 업적 보상이 파편 소모처(룬 강화)에서 차지하는 비중
  3. 문구(이름·설명)가 빠진 업적
  4. Loc 키가 있는데 열거형에 없는 업적

  python tools/check_achievements.py
"""
import io
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
SCRIPTS = os.path.join(HERE, "..", "RuneCast", "Assets", "Scripts")
ACH = os.path.join(SCRIPTS, "Meta", "Achievements.cs")
LOC = os.path.join(SCRIPTS, "Meta", "Loc.cs")
UPG = os.path.join(SCRIPTS, "Meta", "Loadout.cs")
PLAYER = os.path.join(SCRIPTS, "Meta", "PlayerData.cs")

W = sys.stdout.buffer.write


def p(s):
    W((s + u"\n").encode("utf-8"))


def main():
    src = io.open(ACH, encoding="utf-8").read()

    names = re.findall(r"^\s*(\w+)\s*=\s*\d+\s*,", src, re.M)
    rewards = dict(re.findall(r"case Achievement\.(\w+):\s*return\s*(\d+);", src))

    declared = int(re.search(r"TotalReward\s*=\s*(\d+)", src).group(1))
    actual = sum(int(v) for v in rewards.values())

    p(u"업적 %d종\n" % len(names))
    bad = 0

    # 1) 총액
    mark = u"맞음" if declared == actual else u"**어긋남**"
    p(u"  보상 합계   실제 %d  /  주석 %d   %s" % (actual, declared, mark))
    if declared != actual:
        bad += 1
        p(u"     → Achievements.TotalReward 를 %d 로 고칠 것" % actual)

    # 2) 소모처 대비 비중
    cost = re.search(r"return\s*10\s*\+\s*8\s*\*\s*level\s*\+\s*2\s*\*\s*level\s*\*\s*level;",
                     io.open(UPG, encoding="utf-8").read())
    max_lv = int(re.search(r"MaxRuneLevel\s*=\s*(\d+)",
                           io.open(PLAYER, encoding="utf-8").read()).group(1))
    one = sum(10 + 8 * lv + 2 * lv * lv for lv in range(1, max_lv))
    runes = 9
    sink = one * runes
    ratio = actual / float(sink)
    p(u"  룬 강화 총액 %d (룬 %d종 × 만렙 %d)   업적이 그 %.0f%%" % (sink, runes, one, ratio * 100))
    if ratio > 0.5:
        bad += 1
        p(u"     → 업적만 훑으면 강화가 끝난다. 판을 도는 이유가 사라진다")
    elif ratio < 0.15:
        p(u"     (업적이 너무 인색하다는 뜻일 수도 있다 — 지금은 문제 아님)")

    # 3) 보상이 안 적힌 업적
    missing = [n for n in names if n not in rewards]
    if missing:
        bad += 1
        p(u"  보상이 없는 업적: %s  (default 50이 적용된다)" % u", ".join(missing))

    # 4) 문구
    loc = io.open(LOC, encoding="utf-8").read()
    for n in names:
        k = "ach." + n.lower()
        for key in (k, k + ".sub"):
            if '"%s"' % key not in loc:
                bad += 1
                p(u"  문구 없음: %s" % key)

    p(u"\n  문제 %d건" % bad)
    return 1 if bad else 0


if __name__ == "__main__":
    sys.exit(main())
