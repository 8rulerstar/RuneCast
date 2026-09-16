# -*- coding: utf-8 -*-
"""
마나를 쓰는 플레이어를 흉내 내는 시뮬레이터.

`sim_battle.py`는 "초당 몇 피해"라는 눈금만 잰다. 그건 1장(마나 무한)에는 맞지만
**2장에서는 틀린 질문**이다 — 거기서 개입량을 정하는 건 손이 아니라 마나다.
마나 회복을 `Time.deltaTime`으로 바꾼 뒤로는 더 그렇다.

여기서는 실제 규칙을 그대로 돌린다:
  · 마나는 최대치까지 초당 ManaRegen 만큼 찬다 (게임 시간 기준)
  · 룬마다 마나 비용과 피해가 있다
  · 그리는 데 시간이 걸리고, 그동안 시간이 0.2배로 흐른다
  · 망령은 룬 피해만 받는다

플레이어 정책은 단순하다 — **살 수 있는 것 중 마나당 피해가 제일 큰 룬을 쓴다.**
어디에 쓸지는 고르지 않고 늘 앞줄부터 때린다.

**하한선이라고 부르지 않는다.** 예전 주석에는 그렇게 적혀 있었는데 사실이 아니었다.
이 흉내내기는 도형을 틀리지 않고, 손이 미끄러지지 않고, 마나가 차자마자 그린다.
사람은 그러지 못한다. 그러니 여기서 이긴다고 사람도 이긴다는 보장은 없다 —
**여기서 지면 사람은 확실히 진다**는 쪽만 믿을 수 있다.

  python tools/sim_player.py
"""
import math
import random
import sys
import os

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from sim_battle import STATS, RUNE_ONLY, U, spacing_for, nearest, DT, \
    HERO_LINE_X, ENEMY_LINE_X, WAVE_DELAY, RETARGET, \
    tick_heals, tick_splits, tick_slams, reform_line  # noqa: E402

MAX_SECONDS = 180.0

# 시전 슬로우. TraceCapture.castTimeScale.
CAST_SCALE = 0.2

# 한 번 그리는 데 걸리는 실제 시간(초). 도형이 복잡할수록 길다.
# 슬로우 때문에 게임 시간으로는 이것의 0.2배만 흐른다.
DRAW_REAL = {"arrow": 0.7, "chain": 1.1, "meteor": 1.3, "pyre": 0.8}

# 결정하고 손을 대기까지 걸리는 시간(게임 시간). 이 동안은 원래 속도로 흐른다.
THINK = 0.55

# (마나, 한 대당 피해, 때리는 횟수, 그리는 시간 키)
#   화살  26 × 4발 (등급 GOOD 기준 배율 0.9)
#   연쇄  34 × 5홉
#   별똥별 70 × 평균 3마리
#   봉화  11 × 11틱
#
# **횟수를 따로 두는 이유:** 예전에는 "기대 피해" 하나로 뭉쳐 두고 그 값을
# 적들에게 남김없이 나눠 넣었다. 그러면 초과 피해가 한 방울도 안 버려진다 —
# 체력 5 남은 적을 26짜리 화살로 때려도 21이 뒷줄로 흘러가 그대로 쓰였다.
# 광역도 아니고 사거리도 없는 "완벽 분배 삭제기"였고, 그래서 어떤 판이든
# 어떤 조건이든 100% 3별이 나왔다. 도구가 눈금 구실을 못 하고 있었다.
#
# 이제 한 대씩 따로 넣고 넘치는 건 버린다. 사람이 실제로 겪는 낭비다.
RUNES = {
    "arrow":  (15, 26 * 0.9, 4, "arrow"),
    "chain":  (28, 34 * 0.9, 5, "chain"),
    "meteor": (45, 70 * 0.9, 3, "meteor"),
    "pyre":   (38, 11 * 0.9, 11, "pyre"),
}


# ── 장 규칙 (Meta/ChapterRules.cs 와 같은 값으로 유지할 것) ──────────
#
# 4장 안개는 여기 없다. 유닛이 흐려질 뿐 수치가 안 바뀌므로 시뮬레이터가
# 잴 것이 없다 — **그게 이 규칙의 성격이기도 하다.** 사람에게는 압박이지만
# 도구에게는 아무 일도 아니다. 그래서 4장 숫자는 실제보다 쉽게 나온다.

FROST_INTERVAL = 1.30   # 3장 — 아군 공격 주기 배수
PLAGUE_DAMAGE  = 6.0    # 5장 — 적이 죽을 때 반경 안 아군이 받는 피해
PLAGUE_RADIUS  = 1.6
ABYSS_REGEN    = 0.50   # 7장 — 시간 회복 배수 (0.35는 강화 없이 전패였다)
ABYSS_PER_KILL = 7.0    # 7장 — 처치당 회복


def sealed_rune(stage_id):
    """6장 — 판 번호로 정해지는 잠긴 룬. 소생은 제외(게임과 같은 규칙).

    게임은 GlyphTable.Runes 아홉 개에서 고르지만 이 시뮬레이터는 넷만 쓴다.
    이름이 맞아떨어질 때만 실제로 잠기므로, **여기서 잠기는 빈도는 게임보다 낮다** —
    그만큼 6장 숫자는 실제보다 쉽게 나온다."""
    order = ["arrow", "vortex", "heal", "chain", "shield", "meteor", "empower", "pyre"]
    return order[stage_id % len(order)]


def best_rune(mana, power, banned=None):
    """살 수 있는 것 중 마나당 피해가 큰 것. banned 는 6장에서 잠긴 룬."""
    best = None
    for name, (cost, hit, times, draw) in RUNES.items():
        if cost > mana or name == banned:
            continue
        eff = hit * times * power / cost
        if best is None or eff > best[0]:
            best = (eff, name, cost, hit * power, times, draw)
    return best


def simulate(stage, rnd, power=1.0, skill=1.0):
    """
    power: 강화·문양 배율 (1.0 = 아무것도 안 키움, 2.0 = 만렙 근처)
    skill: 그리는 속도 배율 (1.0 = 기준, 0.7 = 빠름)
    """
    chapter = stage.get("chapter", 1)
    frost   = chapter == 3
    plague  = chapter == 5
    sealing = chapter == 6
    abyss   = chapter == 7
    banned  = sealed_rune(stage["id"]) if sealing else None

    units = []
    n = stage["heroes"]
    sp = spacing_for(n)
    for i in range(n):
        u = U(True, "Soldier", HERO_LINE_X, (i - (n - 1) * 0.5) * sp, rnd=rnd)
        u.hp = stage.get("heroHp", 100.0)
        u.max_hp = u.hp
        u.dmg = stage.get("heroDmg", 8.0)
        if frost:
            u.iv *= FROST_INTERVAL     # 서리 — 아군만 느려진다
        units.append(u)

    mana_max, regen = stage["mana"] if stage["mana"] else (999999.0, 999999.0)
    mana = mana_max

    wave_i = 0
    wait = 0.0
    t = 0.0
    spawned = False

    cast_cd = THINK          # 다음 시전까지 남은 게임 시간
    drawing = 0.0            # 그리는 중 남은 실제 시간
    pending = None
    casts = 0

    while t < MAX_SECONDS:
        heroes = [u for u in units if u.hero and u.alive]
        foes = [u for u in units if not u.hero and u.alive]

        if not heroes:
            return ("패배", 0, n, t, casts)

        if not foes:
            if wave_i >= len(stage["waves"]):
                return ("승리", len(heroes), n, t, casts)
            if spawned:
                wait -= DT
                if wait > 0:
                    # **물결 사이에도 마나는 찬다.** 게임의 ManaPool은 전투가
                    # 있든 없든 매 프레임 돈다. 여기서는 `continue`로 아래
                    # "시간 흐름" 블록을 통째로 건너뛰고 있어서, 물결마다
                    # waveDelay×regen 만큼을 덜 세고 있었다 —
                    # 간격을 30초로 바꿔도 결과가 한 자리도 안 변해서 드러났다.
                    mana = min(mana_max, mana + regen * (ABYSS_REGEN if abyss else 1.0) * DT)
                    t += DT
                    continue
            reform_line(units)
            wave = stage["waves"][wave_i]
            wave_i += 1
            total = sum(c for _, c, _, _ in wave)
            wsp = spacing_for(total)
            slot = 0
            for kind, cnt, hs, ds in wave:
                for _ in range(cnt):
                    y = (slot - (total - 1) * 0.5) * wsp
                    x = ENEMY_LINE_X + (slot % 2) * 0.8
                    slot += 1
                    units.append(U(False, kind, x, y, hs, ds, rnd))
            wait = WAVE_DELAY
            spawned = True
            t += DT
            continue

        # ── 시간 흐름 ──
        # 그리는 동안 게임 시간은 0.2배. 마나도 그 속도로 찬다(고친 규칙).
        scale = CAST_SCALE if drawing > 0 else 1.0
        gdt = DT * scale

        mana = min(mana_max, mana + regen * (ABYSS_REGEN if abyss else 1.0) * gdt)

        if drawing > 0:
            drawing -= DT          # 그리는 시간은 실제 시간으로 흐른다
            if drawing <= 0 and pending:
                hit, times = pending
                pending = None
                casts += 1

                # 앞줄부터 한 대씩. **넘치는 피해는 버린다.**
                foes.sort(key=lambda u: u.x)
                i = 0
                for _ in range(times):
                    while i < len(foes) and foes[i].hp <= 0:
                        i += 1
                    if i >= len(foes):
                        break          # 더 때릴 게 없으면 나머지는 허공에
                    foes[i].hp -= hit
        else:
            cast_cd -= gdt
            if cast_cd <= 0:
                pick = best_rune(mana, power, banned)
                if pick:
                    _, name, cost, hit, times, draw = pick
                    mana -= cost
                    pending = (hit, times)
                    drawing = DRAW_REAL[draw] * skill
                    cast_cd = THINK

        # 이번 틱을 시작할 때 살아 있던 적. 아래에서 새로 죽은 것을 가려낸다 —
        # 부패(5장)와 심연(7장)이 죽는 순간에 걸려 있다.
        was_alive = [u for u in units if not u.hero and u.alive] if (plague or abyss) else None

        # ── 전투 ──
        tick_splits(units, rnd)
        tick_heals(units, gdt)
        tick_slams(units, gdt)

        for u in units:
            if not u.alive:
                continue
            u.cd -= gdt
            u.rt -= gdt
            if u.tgt is None or not u.tgt.alive or u.rt <= 0:
                u.tgt = nearest(u, units)
                u.rt = RETARGET
            if u.tgt is None:
                # 아군은 그 자리에 선다. 물결이 끝나면 대열이 통째로
                # 시작선으로 되돌아간다(reform_line) — UnitAI와 같은 규칙.
                if not u.hero:
                    u.x -= u.spd * gdt
                continue
            dx, dy = u.tgt.x - u.x, u.tgt.y - u.y
            d = math.hypot(dx, dy)
            if d > u.rng:
                u.x += dx / max(d, 1e-4) * u.spd * gdt
                u.y += dy / max(d, 1e-4) * u.spd * gdt
            elif u.cd <= 0:
                from sim_battle import NORMAL_SCALE
                scale = NORMAL_SCALE if (u.tgt.kind in RUNE_ONLY and u.hero) else 1.0
                u.tgt.hp -= u.dmg * scale
                u.cd = u.iv

        if was_alive:
            for e in was_alive:
                if e.alive:
                    continue

                # 심연 — 잡으면 마나가 찬다
                if abyss:
                    mana = min(mana_max, mana + ABYSS_PER_KILL)

                # 부패 — 죽은 자리 가까이 있던 아군이 다친다
                if plague:
                    for h in units:
                        if not h.hero or not h.alive:
                            continue
                        if math.hypot(h.x - e.x, h.y - e.y) <= PLAGUE_RADIUS:
                            h.hp -= PLAGUE_DAMAGE

        t += gdt

    return ("시간초과", len([u for u in units if u.hero and u.alive]), n, t, casts)


def run(stage, power=1.0, skill=1.0, trials=30):
    wins = alive = casts = 0
    for i in range(trials):
        res, a, tot, t, c = simulate(stage, random.Random(500 + i), power, skill)
        if res == "승리":
            wins += 1
        alive += a
        casts += c
    return wins / float(trials), alive / float(trials), casts / float(trials)


def main():
    from read_stages import load_all

    W = sys.stdout.buffer.write

    def p(s):
        W((s + u"\n").encode("utf-8"))

    NAMES = {1: u"첫 교전", 2: u"해골 떼", 3: u"단단한 놈", 4: u"빠른 습격",
             5: u"닿지 않는 것", 6: u"본로드의 진군", 7: u"아껴 쓰기", 8: u"밀려드는 무리",
             9: u"정예", 10: u"포위", 11: u"밤의 군세", 12: u"최후의 진"}

    RULES = {1: u"망령", 2: u"마나 제한", 3: u"서리", 4: u"안개(수치 영향 없음)",
             5: u"부패", 6: u"봉인", 7: u"심연"}

    p(u"마나를 쓰는 플레이어 (판당 30회)")
    p(u"  정책: 살 수 있는 것 중 마나당 피해가 큰 룬을 쓴다. 늘 앞줄부터 친다.")
    p(u"  도형을 틀리지 않고 손도 안 미끄러진다 — 여기서 지면 사람은 확실히 진다는 쪽만 믿을 것.")
    p(u"  강화 1.0 = 아무것도 안 키움 / 1.6 = 절반쯤 / 2.2 = 만렙 근처")
    p(u"")
    p(u"  판  이름            마나       강화1.0        강화1.6        강화2.2")
    p(u"  " + u"-" * 74)

    last_ch = None
    for st in load_all():
        if st["chapter"] != last_ch:
            last_ch = st["chapter"]
            p(u"")
            p(u"  — %d장 · %s —" % (last_ch, RULES[last_ch]))

        row = u"  %2d  %-14s %-9s" % (
            st["id"], NAMES.get(st["id"], u"%d판" % st["id"]),
            u"무한" if st["mana"] is None else u"%.0f/%.0f" % st["mana"])
        for pw in (1.0, 1.6, 2.2):
            w, a, c = run(st, pw)
            row += u"  %3.0f%% %3.1f별%s" % (
                w * 100,
                3 if a >= st["heroes"] - 0.15 else (2 if a * 2 >= st["heroes"] else (1 if a > 0 else 0)),
                u"  ")
        p(row)


if __name__ == "__main__":
    main()
