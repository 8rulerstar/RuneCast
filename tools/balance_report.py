# -*- coding: utf-8 -*-
"""
스테이지 12개를 자동전투만으로 돌려 보고, 개입이 얼마나 필요한지 표로 낸다.

읽는 법:
  자동 승률이 100%   → 그리는 게 선택사항이다. 이 게임의 설계가 작동하지 않는다.
  자동 승률이 0%이고 개입해도 힘들다 → 몇 번 그려도 못 뒤집는 판이다.
  목표는 **자동으로는 지고, 개입하면 이기는** 구간.

  python tools/balance_report.py
"""
import io
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from sim_battle import run  # noqa: E402

W = sys.stdout.buffer.write


def p(s):
    W((s + "\n").encode("utf-8"))


# **표는 소스에서 직접 읽는다.** 손으로 옮겼다가 5개 판이 실제와 달랐고,
# 그 잘못된 표로 밸런스를 재고 있었다 — 시뮬레이터가 게임과 다른 걸 재면
# 그 숫자는 아무 의미가 없다.
from read_stages import load  # noqa: E402

STAGES = load()

NAMES = {1: u"첫 교전", 2: u"해골 떼", 3: u"단단한 놈", 4: u"빠른 습격",
         5: u"혼성 부대", 6: u"본로드의 진군", 7: u"아껴 쓰기", 8: u"밀려드는 무리",
         9: u"정예", 10: u"포위", 11: u"밤의 군세", 12: u"최후의 진"}


def verdict(auto_win, auto_alive, heroes, help_win):
    if auto_win >= 0.95:
        return u"자동으로 이김 — 그릴 이유가 없다"
    if auto_win >= 0.4:
        return u"자동으로도 반쯤 이김"
    if help_win < 0.5:
        return u"도와도 힘듦"
    return u"자동으론 짐 · 개입하면 이김"


def main():
    p(u"자동전투만 돌렸을 때 (판당 40회, 개입 없음)")
    p(u"  '개입' 열은 초당 25 피해를 계속 넣어줬을 때 — 개입 여지를 재는 눈금이다.")
    p(u"")
    p(u"  판  이름              자동승률  남는아군  평균시간   개입시 승률   판정")
    p(u"  " + u"─" * 88)

    rows = []
    for st in STAGES:
        aw, aa, at = run(st, 0.0, trials=40)
        hw, ha, ht = run(st, 25.0, trials=40)
        rows.append((st, aw, aa, at, hw))
        p(u"  %2d  %-16s %6.0f%%   %4.1f/%d   %5.1f초   %6.0f%%      %s"
          % (st["id"], NAMES[st["id"]], aw * 100, aa, st["heroes"], at, hw * 100,
             verdict(aw, aa, st["heroes"], hw)))

    p(u"")
    bad_auto = [r for r in rows if r[1] >= 0.95]
    bad_hard = [r for r in rows if r[1] < 0.4 and r[4] < 0.5]
    p(u"  자동으로 이기는 판: %s" % (
        u", ".join(str(r[0]["id"]) for r in bad_auto) if bad_auto else u"없음"))
    p(u"  도와도 힘든 판:     %s" % (
        u", ".join(str(r[0]["id"]) for r in bad_hard) if bad_hard else u"없음"))


if __name__ == "__main__":
    main()
