namespace RuneCast.Battle
{
    public struct UnitStatBlock
    {
        public float MaxHp;
        public float AttackDamage;
        public float AttackRange;
        public float AttackInterval;
        public float MoveSpeed;
    }

    /// <summary>
    /// 종류별 기본 스탯.
    ///
    /// 적 종류가 서로 다른 **문제**를 내야 스테이지가 달라진다.
    /// 숫자만 키운 적은 웨이브 번호만 바뀐 것과 같아서, 종류마다 대응법이 갈리게 잡았다:
    ///  - 해골   다수·저체력·빠름   → 광역이 유리, 방치하면 순식간에 둘러싸인다
    ///  - 본로드 소수·고체력·느림   → 단일 대상 화력이 유리, 시간이 있으니 침착하게
    ///  - 뱀파이어 빠름·중간체력     → 아군에게 금방 붙는다, 막거나 붙기 전에 처리
    ///  - 오크   전부 평균          → 기준점
    ///  - 망령   평타 면역            → **반드시 룬을 써야 한다.** 세지 않다 —
    ///                                 위협의 근거는 강함이 아니라 손댈 수 없음이다
    /// </summary>
    public static class UnitStats
    {
        public static UnitStatBlock Of(UnitKind kind)
        {
            switch (kind)
            {
                case UnitKind.Skeleton:
                    return new UnitStatBlock { MaxHp = 30f, AttackDamage = 5f, AttackRange = 0.95f, AttackInterval = 0.7f, MoveSpeed = 1.55f };

                case UnitKind.Bonelord:
                    return new UnitStatBlock { MaxHp = 135f, AttackDamage = 14f, AttackRange = 1.2f, AttackInterval = 1.15f, MoveSpeed = 0.85f };

                case UnitKind.Vampire:
                    return new UnitStatBlock { MaxHp = 58f, AttackDamage = 9f, AttackRange = 1f, AttackInterval = 0.75f, MoveSpeed = 2.1f };

                case UnitKind.Orc:
                    return new UnitStatBlock { MaxHp = 55f, AttackDamage = 7f, AttackRange = 1.1f, AttackInterval = 0.9f, MoveSpeed = 1.2f };

                // 체력 90 — 화살 한 번(≈95)이면 정리된다. 룬을 써야 하지만
                // **여러 번 써야 하는 건 아니다.** 마나가 조인 후반에 망령이
                // 두세 마리 섞이면 그것만으로 자원 문제가 된다.
                case UnitKind.Wraith:
                    return new UnitStatBlock { MaxHp = 90f, AttackDamage = 6f, AttackRange = 1.1f, AttackInterval = 1f, MoveSpeed = 0.95f };

                // 때리지 않는다(공격력 0). 사거리를 길게 잡아 한참 뒤에 서므로
                // 아군이 앞줄을 정리하기 전에는 손이 닿지 않는다.
                //
                // 체력 45 — **화살 한 번이면 정리된다.** 이 적의 위협은 단단함이
                // 아니라 위치다. 질기게까지 하면 뒤로 던지는 룬을 두 번 써야 하고,
                // 그건 마나가 조인 후반에 사실상 못 잡는 적이 된다.
                //
                // **서는 거리(AttackRange)와 회복 반경은 같이 정해야 한다.**
                // 처음엔 4.6에서 멈추게 하고 반경을 3.2로 뒀는데, 앞줄은 1.1까지
                // 붙으므로 간격이 3.5가 된다 — 회복이 **아무에게도 안 닿았다.**
                // 시뮬레이터에서 회복을 껐다 켜도 결과가 한 자리도 안 달라져서 잡혔다.
                //
                // 3.8에서 멈추면 앞줄과의 간격이 2.7쯤이라 반경 4.4 안에 들어온다.
                // 그러면서도 앞줄을 노린 광역에는 안 걸린다 — 뒤를 치려면
                // 뒤를 겨냥해야 한다는 게 이 적의 전부다.
                case UnitKind.Shaman:
                    return new UnitStatBlock { MaxHp = 45f, AttackDamage = 0f, AttackRange = 3.8f, AttackInterval = 2f, MoveSpeed = 0.8f };

                // 사거리 2.6 — 아군(1.15)의 두 배가 넘는다. 붙기 전까지 일방적으로
                // 맞는 구간이 생기고, 그 구간을 짧게 만드는 게 플레이어 몫이다.
                //
                // 대신 물렁하다(체력 40). 위협의 근거는 맷집이 아니라 거리여야 한다.
                // 질기게까지 하면 그냥 강한 적이고, 그건 이미 본로드가 하고 있다.
                case UnitKind.Gnoll:
                    return new UnitStatBlock { MaxHp = 40f, AttackDamage = 7f, AttackRange = 2.6f, AttackInterval = 1.4f, MoveSpeed = 1.05f };

                // 죽으면 새끼 둘이 나온다. **체력을 낮게 잡았다** — 질기면 쪼개기
                // 전에 이미 지치고, 그러면 쪼개지는 것이 보너스가 아니라 벌이 된다.
                case UnitKind.Spider:
                    return new UnitStatBlock { MaxHp = 52f, AttackDamage = 6f, AttackRange = 1f, AttackInterval = 0.85f, MoveSpeed = 1.35f };

                // 새끼 둘을 합쳐도 어미보다 약하다(52 → 22×2 = 44).
                // 아니면 죽이는 게 손해가 되고, 손해면 아무도 안 죽인다.
                // 대신 빠르다 — 흩어져 달려드는 그림이 나와야 쪼개진 티가 난다.
                case UnitKind.Spiderling:
                    return new UnitStatBlock { MaxHp = 22f, AttackDamage = 4f, AttackRange = 0.9f, AttackInterval = 0.7f, MoveSpeed = 1.9f };

                // 보스. **체력만 크고 평타는 평범하다** — 위협은 맷집이 아니라
                // 예고된 강타에서 나온다. 평타까지 세면 다가오는 동안에만
                // 아군이 녹아서, 정작 봐야 할 강타를 볼 여유가 없다.
                //
                // 체력 420 — 강화 없이도 룬 대여섯 번이면 넘어간다. 오래 버티는
                // 게 목적이 아니라 몇 번의 강타를 넘기느냐가 목적이다.
                case UnitKind.Troll:
                    return new UnitStatBlock { MaxHp = 420f, AttackDamage = 11f, AttackRange = 1.4f, AttackInterval = 1.5f, MoveSpeed = 0.7f };

                // ── 아군 ────────────────────────────────────────
                //
                // **전사는 옛 병사와 수치가 한 자리도 다르지 않다.** 이번 변경은
                // 그림만 갈아 끼우는 것이라, 여기서 값을 하나라도 건드리면
                // 12판 밸런스가 통째로 다시 재야 하는 것이 된다
                // (AGENTS.md: 아군을 건드리면 12판 승률이 100%와 0% 사이를 오간다).
                // 체력·공격력은 어차피 StageDef가 덮어쓰고, 나머지 셋이 여기서 온다.
                case UnitKind.Warrior:
                    return new UnitStatBlock { MaxHp = 100f, AttackDamage = 8f, AttackRange = 1.15f, AttackInterval = 0.8f, MoveSpeed = 1.5f };

                // **아래 둘은 아직 어디서도 소환되지 않는다.** 용사 종류 기능이
                // 붙을 때 쓸 자리만 잡아 둔 것이고, 값은 **시뮬레이터로 확인하지
                // 않았다.** sim_player.py를 돌리기 전에는 근거 없는 숫자다.
                //
                // 궁수: 사거리를 놀(2.6)보다 짧게 잡았다. 아군이 적보다 멀리
                // 때리면 놀이라는 적이 내던 질문("줄 밖에서 맞는다")이 사라진다.
                case UnitKind.Archer:
                    return new UnitStatBlock { MaxHp = 70f, AttackDamage = 7f, AttackRange = 2.3f, AttackInterval = 1.1f, MoveSpeed = 1.5f };

                // 수도사: 평타가 없다(주술사와 같다). 회복은 별도 컴포넌트가 맡을
                // 자리이고, AttackRange는 "어디까지 다가가서 멈추는가"로만 쓰인다.
                // 주술사에서 배운 것: **서는 거리와 회복 반경은 같이 정해야 한다.**
                case UnitKind.Monk:
                    return new UnitStatBlock { MaxHp = 65f, AttackDamage = 0f, AttackRange = 2.6f, AttackInterval = 2f, MoveSpeed = 1.5f };

                default: // Soldier — 옛 Tiny RPG 병사. 폴백으로만 남는다
                    return new UnitStatBlock { MaxHp = 100f, AttackDamage = 8f, AttackRange = 1.15f, AttackInterval = 0.8f, MoveSpeed = 1.5f };
            }
        }
    }
}
