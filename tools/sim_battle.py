# -*- coding: utf-8 -*-
"""
자동전투 시뮬레이터 — **플레이어가 아무것도 안 했을 때** 각 스테이지가 어떻게 끝나는지 잰다.

이 게임의 설계는 "자동전투는 샌드백이고 개입이 본체"다. 그런데 그게 실제로 그런지
확인한 적이 없었다. 자동으로 이기는 판은 그리는 게 선택사항이고, 자동으로 처참하게
지는 판은 몇 번 그려도 못 뒤집는다. **둘 다 재봐야 안다.**

UnitAI.Update와 BattleManager의 규칙을 그대로 옮겼다:
  - 0.2초마다 가장 가까운 살아있는 적으로 재조준
  - 사거리 밖이면 대상 쪽으로 moveSpeed 만큼 이동
  - 사거리 안이면 attackInterval 마다 attackDamage
  - 첫 공격 쿨다운은 0~attackInterval 사이 무작위 (동시 타격 방지)
  - 웨이브는 적이 전멸하고 waveDelay 뒤에 다음이 나온다

수치가 바뀌면(UnitStats, StageDef) 이 파일도 같이 고쳐야 한다.
게임 코드가 MonoBehaviour라 그대로 못 부르는 대신, 규칙이 짧아서 옮기는 편이 쌌다.

  python tools/sim_battle.py
"""
import math
import random
import sys

DT = 1.0 / 60.0
MAX_SECONDS = 180.0

HERO_LINE_X = -3.5
ENEMY_LINE_X = 3.5
LINE_SPACING = 1.5
MAX_COLUMN_SPREAD = 6.6
WAVE_DELAY = 3.4   # BattleManager.waveDelay 와 같이 유지할 것
RETARGET = 0.2

# UnitStats.Of — (MaxHp, AttackDamage, AttackRange, AttackInterval, MoveSpeed)
STATS = {
    "Soldier":  (100.0, 8.0, 1.15, 0.80, 1.50),
    "Orc":      (55.0, 7.0, 1.10, 0.90, 1.20),
    "Skeleton": (30.0, 5.0, 0.95, 0.70, 1.55),
    "Bonelord": (135.0, 14.0, 1.20, 1.15, 0.85),
    "Vampire":  (58.0, 9.0, 1.00, 0.75, 2.10),
    # 망령 — 평타가 통하지 않는다. 룬 피해만 받는다.
    "Wraith":   (90.0, 6.0, 1.10, 1.00, 0.95),
    # 주술사 — 때리지 않는다(공격력 0). 사거리가 길어 한참 뒤에 선다.
    "Shaman":   (45.0, 0.0, 3.80, 2.00, 0.80),
    # 놀 — 멀리서 던진다. 사거리가 아군(1.15)의 두 배가 넘는다.
    "Gnoll":    (40.0, 7.0, 2.60, 1.40, 1.05),
    # 거미 — 죽으면 새끼 둘로 쪼개진다
    "Spider":     (52.0, 6.0, 1.00, 0.85, 1.35),
    "Spiderling": (22.0, 4.0, 0.90, 0.70, 1.90),
    # 트롤 — 보스. 평타는 평범하고 위협은 예고된 강타에서 나온다.
    "Troll":      (420.0, 11.0, 1.40, 1.50, 0.70),
}
# 평타가 잘 안 통하는 적. 값은 Unit.NormalDamageScale과 맞춘다.
# **완전 면역이 아니다** — 예전에는 0이었고, 그때는 마나가 마르면 벽이 됐다.
RUNE_ONLY = {"Wraith"}
NORMAL_SCALE = 0.2

# 죽으면 쪼개지는 적. {어미: (새끼 종류, 마리 수)}
SPLITTERS = {"Spider": ("Spiderling", 2)}

# 주술사가 주변 적을 되살리는 양. ShamanAura의 값과 맞춘다.
HEALERS = {"Shaman"}
HEAL_CYCLE = 2.2
HEAL_RADIUS = 3.6
HEAL_AMOUNT = 22.0


class U(object):
    __slots__ = ("hero", "kind", "x", "y", "hp", "max_hp", "dmg", "rng", "iv", "spd",
                 "cd", "tgt", "rt", "heal_cd", "split_done", "slam_cd", "home_y")

    def __init__(self, hero, kind, x, y, hp_scale=1.0, dmg_scale=1.0, rnd=None):
        hp, dmg, rng, iv, spd = STATS[kind]
        self.hero = hero
        self.kind = kind
        self.x, self.y = x, y
        self.home_y = y
        self.hp = hp * hp_scale
        self.max_hp = self.hp
        self.dmg = dmg * dmg_scale
        self.rng, self.iv, self.spd = rng, iv, spd
        self.cd = rnd.uniform(0, iv)
        self.tgt = None
        self.rt = 0.0
        # 첫 회복까지의 시간. 게임에서도 소환 직후부터 세기 시작한다.
        self.heal_cd = 1.4   # 첫 회복까지 (ShamanAura.FirstDelay)
        self.split_done = False
        self.slam_cd = SLAM_FIRST

    @property
    def alive(self):
        return self.hp > 0


def spacing_for(n):
    if n <= 1:
        return LINE_SPACING
    return min(LINE_SPACING, MAX_COLUMN_SPREAD / (n - 1))


def nearest(u, units):
    """
    가장 가까운 적. 아군은 때릴 수 있는 상대를 먼저 고른다 —
    Unit.NearestEnemyOf와 같은 규칙이다. 다른 적이 없을 때만 망령을 붙잡는다.

    **이 규칙이 밸런스를 바꾼다.** 예전에는 아군이 망령에게 달라붙어 헛손질을
    하는 동안 망령이 묶여 있었다. 이제 망령은 아무에게도 안 잡히고 계속 때린다.
    """
    best, bd = None, 1e18
    fb, fbd = None, 1e18
    for o in units:
        if o is u or not o.alive or o.hero == u.hero:
            continue
        d = (o.x - u.x) ** 2 + (o.y - u.y) ** 2
        if u.hero and o.kind in RUNE_ONLY:
            if d < fbd:
                fbd, fb = d, o
            continue
        if d < bd:
            bd, best = d, o
    return best if best is not None else fb


# 보스의 광역 강타. BossSlam과 같은 값.
SLAMMERS = {"Troll"}
SLAM_CYCLE = 7.5
SLAM_FIRST = 2.5   # 첫 강타까지 (BossSlam.FirstDelay)
SLAM_RADIUS = 2.4
SLAM_DAMAGE = 24.0


def tick_slams(units, dt):
    """
    보스가 주기마다 주변 아군을 후려친다.

    **이걸 빼면 시뮬레이터가 보스를 그냥 체력 많은 적으로 센다.** 평타가
    평범하므로 위협이 아예 안 잡히고, 보스를 넣어도 판이 안 어려워진다는
    거꾸로 된 결과가 나온다 — 주술사·거미에서 두 번 겪은 것과 같다.
    """
    r2 = SLAM_RADIUS * SLAM_RADIUS
    for b in units:
        if not b.alive or b.hero or b.kind not in SLAMMERS:
            continue

        b.slam_cd -= dt
        if b.slam_cd > 0:
            continue
        b.slam_cd = SLAM_CYCLE

        for o in units:
            if not o.alive or not o.hero:
                continue
            if (o.x - b.x) ** 2 + (o.y - b.y) ** 2 > r2:
                continue
            o.hp -= SLAM_DAMAGE


def reform_line(units):
    """
    물결이 끝나면 아군을 시작 대열로 되돌린다. BattleManager.ReformLine과 같다.

    **되돌리기와 전장 폭은 같이 정해야 한다.** 되돌리기만 넣으면 매 물결마다
    적이 전장을 가로질러 오는 시간이 통째로 공짜가 되어 판이 쉬워진다.
    폭을 11.2칸에서 7칸으로 줄여 접근을 5.8초로 맞췄다.
    """
    for u in units:
        if u.alive and u.hero:
            u.x = HERO_LINE_X
            u.y = u.home_y


def tick_splits(units, rnd):
    """
    죽은 거미를 새끼 둘로 쪼갠다. Splitter와 같은 규칙이다.

    **이걸 빼면 시뮬레이터가 거미를 그냥 약한 적으로 센다.** 체력 52짜리가
    죽으면 끝이라고 계산하니, 거미를 넣을수록 판이 쉬워진다는 거꾸로 된
    결과가 나온다 — 주술사에서 겪은 것과 같은 종류의 착각이다.

    새끼는 다시 안 쪼개진다(SPLITTERS에 Spiderling이 없다).
    """
    born = []
    for u in units:
        if u.alive or u.hero or u.split_done or u.kind not in SPLITTERS:
            continue
        u.split_done = True

        kind, count = SPLITTERS[u.kind]
        for i in range(count):
            side = -1.0 if i == 0 else 1.0
            born.append(U(False, kind, u.x + 0.1, u.y + side * 0.5, rnd=rnd))

    units.extend(born)


def tick_heals(units, dt):
    """
    주술사가 주기마다 주변 적을 회복시킨다. ShamanAura와 같은 규칙이다.

    **이걸 빼면 시뮬레이터가 주술사를 그냥 약한 적으로 센다.** 체력 45에
    공격력 0이니 없는 것보다 쉬운 적이 되어, 주술사를 넣을수록 판이 쉬워진다는
    거꾸로 된 결과가 나온다.
    """
    for h in units:
        if not h.alive or h.hero or h.kind not in HEALERS:
            continue

        h.heal_cd -= dt
        if h.heal_cd > 0:
            continue
        h.heal_cd = HEAL_CYCLE

        r2 = HEAL_RADIUS * HEAL_RADIUS
        for o in units:
            if not o.alive or o.hero or o is h:
                continue
            if (o.x - h.x) ** 2 + (o.y - h.y) ** 2 > r2:
                continue
            o.hp = min(o.max_hp, o.hp + HEAL_AMOUNT)


def simulate(stage, rnd, rune_dps=0.0):
    """
    rune_dps: 플레이어 개입을 아주 거칠게 흉내낸 값(초당 적에게 들어가는 피해).
              0이면 순수 자동전투. 어디를 때리는지·범위는 모사하지 않는다 —
              "얼마나 도와야 이기나"의 눈금만 보려는 것이다.
    """
    units = []
    n = stage["heroes"]
    sp = spacing_for(n)
    for i in range(n):
        units.append(U(True, "Soldier", HERO_LINE_X,
                       (i - (n - 1) * 0.5) * sp, rnd=rnd))
        units[-1].hp = stage.get("heroHp", 100.0)
        units[-1].dmg = stage.get("heroDmg", 8.0)

    wave_i = 0
    wait = 0.0
    t = 0.0
    spawned_first = False

    while t < MAX_SECONDS:
        heroes = [u for u in units if u.hero and u.alive]
        foes = [u for u in units if not u.hero and u.alive]

        if not heroes:
            return ("패배", 0, n, t)

        if not foes:
            if wave_i >= len(stage["waves"]):
                return ("승리", len(heroes), n, t)
            if spawned_first:
                wait -= DT
                if wait > 0:
                    t += DT
                    continue
            # 웨이브 투입
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
            spawned_first = True
            t += DT
            continue

        # 룬 피해를 초당 균등하게 적에게 나눠 넣는다 (앞줄부터)
        if rune_dps > 0 and foes:
            dmg = rune_dps * DT
            foes.sort(key=lambda u: u.x)
            for f in foes:
                if dmg <= 0:
                    break
                take = min(f.hp, dmg)
                f.hp -= take
                dmg -= take

        tick_splits(units, rnd)
        tick_heals(units, DT)
        tick_slams(units, DT)

        for u in units:
            if not u.alive:
                continue

            u.cd -= DT
            u.rt -= DT
            if u.tgt is None or not u.tgt.alive or u.rt <= 0:
                u.tgt = nearest(u, units)
                u.rt = RETARGET

            if u.tgt is None:
                # 아군은 그 자리에 선다. 물결이 끝나면 대열이 통째로
                # 시작선으로 되돌아간다(reform_line) — UnitAI와 같은 규칙.
                if not u.hero:
                    u.x -= u.spd * DT
                continue

            dx, dy = u.tgt.x - u.x, u.tgt.y - u.y
            d = math.hypot(dx, dy)
            if d > u.rng:
                u.x += dx / max(d, 1e-4) * u.spd * DT
                u.y += dy / max(d, 1e-4) * u.spd * DT
            elif u.cd <= 0:
                # 룬으로만 잡히는 적은 평타를 안 받는다
                scale = NORMAL_SCALE if (u.tgt.kind in RUNE_ONLY and u.hero) else 1.0
                u.tgt.hp -= u.dmg * scale
                u.cd = u.iv

        t += DT

    return ("시간초과", len([u for u in units if u.hero and u.alive]), n, t)


def run(stage, rune_dps=0.0, trials=40):
    wins = 0
    alive = 0
    times = []
    for i in range(trials):
        rnd = random.Random(1000 + i)
        res, a, total, t = simulate(stage, rnd, rune_dps)
        if res == "승리":
            wins += 1
            times.append(t)
        alive += a
    return wins / float(trials), alive / float(trials), (sum(times) / len(times) if times else 0)
