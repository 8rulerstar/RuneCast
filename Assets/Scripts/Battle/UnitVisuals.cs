namespace RuneCast.Battle
{
    /// <summary>유닛 종류. 스탯이 아니라 "무엇으로 보이는가"와 기본 성향을 정한다.</summary>
    public enum UnitKind
    {
        Soldier,   // 아군 — 옛 Tiny RPG 병사. Warrior로 대체됐고 폴백으로만 남는다
        Orc,       // 기본 적 — 평범
        Skeleton,  // 다수·저체력
        Bonelord,  // 소수·고체력
        Vampire,   // 빠름

        /// <summary>
        /// 망령 — **평타가 20%만 들어간다.**
        ///
        /// 이 게임의 전제는 "자동전투는 샌드백이고 개입이 본체"인데, 아군이
        /// 알아서 다 잡을 수 있으면 그리는 게 선택사항이 된다. 이 적은 개입의
        /// 값어치를 크게 만든다 — 룬 한 방이면 넘어가는데 아군에게 맡기면
        /// 넷이 붙어 십여 초다.
        ///
        /// **처음엔 완전 면역이었다.** "개입 안 하면 절대 안 죽는다"는 분명했지만
        /// 대가가 컸다. 아군이 때리는데 숫자가 하나도 안 뜨면 게임이 고장 난 것처럼
        /// 보이고, 마나가 마른 순간에는 아무것도 못 하는 벽이 된다.
        ///
        /// 세지 않게 잡은 건 그대로다. 위협의 근거가 "강해서"가 아니라
        /// "아군 손으로는 오래 걸려서"여야 한다.
        /// </summary>
        Wraith,

        /// <summary>
        /// 주술사 — **뒤에 서서 다른 적을 회복시킨다. 아군은 손이 안 닿는다.**
        ///
        /// 적들이 능력치만 다르고 행동은 다 같았다. 가까운 걸 향해 걸어가서
        /// 때린다 — 그러면 전장에 읽을 게 없고, 개입은 "마나가 차면 제일 센 걸
        /// 쓴다"로 굳는다.
        ///
        /// 이 적은 **표적을 고르게 만든다.** 놔두면 깎아 놓은 체력이 계속
        /// 되돌아오니 앞줄만 치던 손이 뒤를 보게 된다.
        ///
        /// 망령이 "평타가 안 통해서" 플레이어 몫이라면 주술사는 "손이 안 닿아서"
        /// 플레이어 몫이다. 사거리가 길어 한참 뒤에 서므로 앞줄이 다 죽기 전에는
        /// 아군이 거기까지 안 간다. 새 규칙을 배울 게 없다.
        /// </summary>
        Shaman,

        /// <summary>
        /// 놀 — **멀리서 뼈를 던진다.** 아군보다 사거리가 길다.
        ///
        /// 지금까지의 적은 전부 붙어서 때렸다. 그래서 전선이 한 줄로 정리되고,
        /// 그 줄만 보면 됐다. 놀은 **줄 밖에서 때린다** — 아군이 앞줄과 엉켜
        /// 있는 동안 뒤에서 계속 깎는다.
        ///
        /// 아군이 못 잡는 건 아니다. 앞줄을 정리하면 다가가서 잡는다.
        /// 다만 그때까지 맞은 만큼은 이미 잃은 뒤다 — 그 시간을 줄이는 게 개입이다.
        /// </summary>
        Gnoll,

        /// <summary>
        /// 거미 — **죽으면 둘로 쪼개진다.**
        ///
        /// 망령·주술사·놀은 전부 **어디를** 칠지를 묻는다. 그 셋만 있으면 적을
        /// 늘려도 전장이 묻는 질문은 하나뿐이다.
        ///
        /// 거미는 **언제** 칠지를 묻는다. 별똥별은 "여럿을 한 번에 지운다"였는데,
        /// 거미가 섞이면 같은 한 방이 오히려 수를 늘린다 — 셋을 쓸어서 여섯을
        /// 만든다. 아군이 앞줄을 정리한 뒤에 터뜨릴지, 지금 터뜨리고 새끼를
        /// 아군에게 맡길지를 고르게 된다.
        /// </summary>
        Spider,

        /// <summary>
        /// 새끼 거미 — 거미가 죽으면 나온다. **다시 쪼개지지는 않는다.**
        ///
        /// 끝없이 늘어나면 정리할 방법이 없고, 그건 어려운 게 아니라 못 이기는 것이다.
        /// 같은 그림의 축소판이라 "저게 아까 그것에서 나왔다"가 설명 없이 읽힌다.
        /// </summary>
        Spiderling,

        /// <summary>
        /// 트롤 — **보스.** 12판 마지막에 혼자 나온다.
        ///
        /// 지금까지 장의 끝이 "적이 좀 더 많은 물결"이었다. 그러면 다 깨고도
        /// 끝났다는 느낌이 없다. 보스는 그 자리에 마침표를 찍는 역할이다.
        ///
        /// **크게 휘두르기 전에 멈춰 선다.** 1.2초 동안 몸이 붉어지고 커지다가
        /// 주변 아군을 한꺼번에 후려친다. 그 사이에 보호막을 씌우거나 소용돌이로
        /// 끌어내면 막을 수 있다 — 못 막으면 아군 여럿이 한 번에 반피가 된다.
        ///
        /// 예고 없이 때리는 보스는 만들지 않는다. 맞고 나서야 배우는 건
        /// 어려운 게 아니라 불친절한 것이다.
        /// </summary>
        Troll,

        // ── 아군 (Tiny Swords) ──────────────────────────────────
        //
        // **아군을 Tiny RPG 병사에서 이쪽으로 옮겼다.** 아군은 판마다 넷다섯이
        // 한 줄로 같이 서 있어서, 화풍이 섞이면 화면에서 제일 크게 드러나는
        // 자리가 거기다. 후반 적(주술사·놀·거미·트롤)이 이미 Tiny Swords라
        // 옮기는 쪽이 오히려 이질적인 유닛을 줄인다.
        //
        // 옛 병사는 프레임의 22%밖에 안 차서 화면 높이가 0.53이었다 —
        // 해골(0.80)보다 작았다. 셋 다 0.95로 맞춰서 주인공이 제일 작은
        // 상태를 끝냈다.

        /// <summary>전사 — 방패를 든 근접. 기본 아군이자 앞에 서는 쪽.</summary>
        Warrior,

        /// <summary>
        /// 궁수 — **아군 최초의 원거리.**
        ///
        /// 배치가 의미를 가지려면 뒤에 남는 유닛이 있어야 한다. 지금까지 아군은
        /// 전부 근접이라 어디에 세워도 결국 같은 자리에서 싸웠다. 사거리가 길면
        /// 스스로 뒤에 서므로 UnitAI를 고칠 것이 없다 — 놀이 이미 그렇게 돈다.
        /// </summary>
        Archer,

        /// <summary>
        /// 수도사 — 아군을 조금씩 회복시킨다.
        ///
        /// **힐 룬을 덜 쓰게 만드는 것이 목적이다.** 위력이 아니라 마나 씀씀이를
        /// 바꾸는 쪽이라, 키워도 "룬이 필요 없어지는" 방향으로 가지 않는다.
        /// 적 쪽 주술사와 정확히 같은 역할이라 무슨 일이 벌어지는지 이미 배운
        /// 규칙으로 읽힌다.
        /// </summary>
        Monk,
    }

    /// <summary>
    /// 종류별 스프라이트 경로와 크기.
    ///
    /// 팩마다 프레임 크기와 파일명 규칙이 달라서(Tiny RPG는 100px `-Idle`,
    /// Enemy Animations Set은 32px `_idle`) 한 곳에 모아 둔다.
    /// 이름은 반입할 때 통일했지만 **프레임 크기는 원본 그대로**라 여기서 알려줘야 한다.
    ///
    /// bodyScale은 픽셀 밀도를 맞춘 값이 아니라 **화면에서 읽히는 크기**로 맞췄다.
    /// 32px 스프라이트를 픽셀 밀도에 맞춰 축소하면 너무 작아서 뭔지 안 보인다.
    /// </summary>
    public struct UnitVisualDef
    {
        public string Prefix;
        public string AttackClip;  // 팩마다 공격 시트 이름이 다르다
        public int FrameSize;
        public float BodyScale;
        public float FrameRate;
    }

    public static class UnitVisuals
    {
        public static UnitVisualDef Of(UnitKind kind)
        {
            switch (kind)
            {
                case UnitKind.Orc:
                    return new UnitVisualDef { Prefix = "Orc", AttackClip = "Attack01", FrameSize = 100, BodyScale = 2.4f, FrameRate = 10f };

                case UnitKind.Skeleton:
                    return new UnitVisualDef { Prefix = "Skeleton", AttackClip = "Attack", FrameSize = 32, BodyScale = 1.6f, FrameRate = 12f };

                case UnitKind.Bonelord:
                    return new UnitVisualDef { Prefix = "Bonelord", AttackClip = "Attack", FrameSize = 32, BodyScale = 2.1f, FrameRate = 12f };

                case UnitKind.Vampire:
                    return new UnitVisualDef { Prefix = "Vampire", AttackClip = "Attack", FrameSize = 32, BodyScale = 1.8f, FrameRate = 14f };

                // 전용 스프라이트가 없어서 뱀파이어 시트를 쓰되, 조금 크고
                // Unit이 푸른 유령빛으로 물들인다 — 겉모습이 다르지 않으면
                // "얘는 평타가 안 통한다"를 배울 방법이 없다.
                case UnitKind.Wraith:
                    return new UnitVisualDef { Prefix = "Vampire", AttackClip = "Attack", FrameSize = 32, BodyScale = 2.0f, FrameRate = 9f };

                // 로브를 걸친 형상이라 주술사에 맞는다. 본로드보다 작게 잡아
                // 뒤에 선 것이 크기로도 읽히게 했다. 색은 Unit이 녹빛으로 물들인다.
                // Tiny Swords의 Hex Shaman. **주술사 전용 아트다** — 본로드 시트를
                // 녹색으로 물들여 흉내내던 걸 대체한다. 지팡이를 든 형상이라
                // "저건 때리는 놈이 아니다"가 실루엣에서 읽힌다.
                //
                // 프레임 192px 중 캐릭터가 43%를 차지한다(여백이 넓은 팩이다).
                // 화면 높이를 해골(0.75)과 본로드(0.98) 사이에 두려고 2.0으로 잡았다.
                case UnitKind.Shaman:
                    return new UnitVisualDef { Prefix = "Shaman", AttackClip = "Attack", FrameSize = 192, BodyScale = 2.0f, FrameRate = 10f };

                // 공격 클립 이름이 Throw다 — 던지는 동작이라 원본이 그렇게 부른다.
                case UnitKind.Gnoll:
                    return new UnitVisualDef { Prefix = "Gnoll", AttackClip = "Throw", FrameSize = 192, BodyScale = 2.05f, FrameRate = 11f };

                // 프레임 안 캐릭터 비율 44%. 어미는 화면 높이 0.62, 새끼는 그 절반이
                // 안 되게 잡았다 — 크기 차이가 확실해야 "쪼개졌다"가 읽힌다.
                case UnitKind.Spider:
                    return new UnitVisualDef { Prefix = "Spider", AttackClip = "Attack", FrameSize = 192, BodyScale = 1.42f, FrameRate = 12f };

                case UnitKind.Spiderling:
                    return new UnitVisualDef { Prefix = "Spider", AttackClip = "Attack", FrameSize = 192, BodyScale = 0.69f, FrameRate = 15f };

                // 프레임 안 캐릭터 비율 55%. 화면 높이 1.6이면 본로드(0.98)의
                // 1.6배쯤이라 "저건 다르다"가 크기만으로 읽힌다.
                // 원본은 384px인데 Unity 임포터 한도(2048)에 걸려 프레임이
                // 어긋나므로 미리 정확히 절반으로 줄여 넣었다.
                case UnitKind.Troll:
                    return new UnitVisualDef { Prefix = "Troll", AttackClip = "Attack", FrameSize = 192, BodyScale = 2.90f, FrameRate = 10f };

                // ── 아군 (Tiny Swords, 192px) ───────────────────
                //
                // **BodyScale은 눈으로 잡지 않았다.** 프레임 안에서 캐릭터가
                // 차지하는 비율을 재서 셋 다 화면 높이 0.95가 되게 역산했다
                // (전사·궁수 47%, 수도사 37% — 수도사만 값이 큰 건 여백이
                // 넓어서지 실제로 더 커서가 아니다).
                //
                // 0.95를 고른 이유: 해골 0.80보다 확실히 크고 본로드 0.98과는
                // 비슷하다. 주인공이 잡졸보다 작으면 전선에서 눈이 아군을
                // 먼저 찾지 못한다.
                case UnitKind.Warrior:
                    return new UnitVisualDef { Prefix = "Warrior", AttackClip = "Attack", FrameSize = 192, BodyScale = 2.00f, FrameRate = 10f };

                // 공격 클립 이름이 Shoot이다 — 원본이 그렇게 부른다(놀의 Throw와 같은 경우).
                case UnitKind.Archer:
                    return new UnitVisualDef { Prefix = "Archer", AttackClip = "Shoot", FrameSize = 192, BodyScale = 2.03f, FrameRate = 10f };

                // 회복 동작이 공격 자리에 들어간다. 수도사는 때리지 않으므로
                // "공격 클립"이 곧 회복 클립이다 — 주술사가 쓰는 방식과 같다.
                case UnitKind.Monk:
                    return new UnitVisualDef { Prefix = "Monk", AttackClip = "Heal", FrameSize = 192, BodyScale = 2.57f, FrameRate = 10f };

                default:
                    return new UnitVisualDef { Prefix = "Soldier", AttackClip = "Attack01", FrameSize = 100, BodyScale = 2.4f, FrameRate = 10f };
            }
        }

        public static string DisplayName(UnitKind kind)
        {
            switch (kind)
            {
                case UnitKind.Orc: return "오크";
                case UnitKind.Skeleton: return "해골";
                case UnitKind.Bonelord: return "본로드";
                case UnitKind.Vampire: return "뱀파이어";
                case UnitKind.Wraith: return "망령";
                case UnitKind.Shaman: return "주술사";
                case UnitKind.Gnoll: return "놀";
                case UnitKind.Spider: return "거미";
                case UnitKind.Spiderling: return "새끼 거미";
                case UnitKind.Troll: return "트롤";
                // "궁수"가 아니라 "사수"인 건 잘라낸 글꼴에 '궁'이 없어서다.
                // make_font.py를 돌리면 되돌릴 수 있다.
                case UnitKind.Warrior: return "전사";
                case UnitKind.Archer: return "사수";
                case UnitKind.Monk: return "수도사";
                default: return "용사";
            }
        }
    }
}
