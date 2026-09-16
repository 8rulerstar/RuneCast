using UnityEngine;
using RuneCast.Gesture;

namespace RuneCast.Meta
{
    /// <summary>
    /// 주력(呪力) — 지금까지 키운 것 전부를 숫자 하나로 합친 값.
    ///
    /// **왜 필요한가:** 이 게임의 성장은 룬 레벨 9개 + 문양 5칸 + 잉크 단계로 흩어져 있다.
    /// 하나하나는 다 의미가 있는데, 플레이어 입장에서 "내가 지난주보다 세졌나"를
    /// 확인할 방법이 없다. 강화를 눌러도 바뀌는 건 그 룬의 배율 하나뿐이라
    /// **뭘 해도 화면에서 달라지는 게 없다.**
    ///
    /// 모바일 성장형 게임이 전투력 같은 종합 수치를 두는 이유가 이것이다.
    /// 정확한 강함을 재는 게 목적이 아니라, **모든 투자가 같은 숫자로 모여서
    /// 오르는 걸 보여주는 게** 목적이다. 그래서 여기 계산은 정밀할 필요가 없고,
    /// 대신 **어떤 투자를 해도 반드시 오르는 것**이 중요하다.
    ///
    /// 전투 결과에는 쓰이지 않는다. 실제 위력은 Loadout이 따로 계산한다 —
    /// 이 값을 전투에 먹이면 "표시용"이 아니라 밸런스 축이 하나 더 생긴다.
    /// </summary>
    public static class CombatPower
    {
        /// <summary>아무것도 안 했을 때의 시작값. 0에서 시작하면 초반 상승폭이 과장돼 보인다.</summary>
        private const int Base = 100;

        private const int PerRuneLevel = 20;   // 룬 하나를 1렙 올릴 때
        private const int PerHeroLevel = 25;   // 용사 한 종류를 1렙 (전원에게 붙는다)
        private const int PerInkTier = 60;     // 잉크 단계 하나
        private const int PerStar = 4;         // 별 하나

        /// <summary>등급별 문양 가중치. 값(value)이 %라서 그대로 쓰면 너무 작다.</summary>
        private static readonly int[] RarityWeight = { 200, 420, 800, 1500 };

        public static int RuneScore
        {
            get
            {
                int sum = 0;
                var runes = GlyphTable.Runes;
                for (int i = 0; i < runes.Length; i++)
                    sum += (PlayerData.LevelOf(runes[i]) - 1) * PerRuneLevel;
                return sum;
            }
        }

        /// <summary>
        /// 용사 강화.
        ///
        /// **한 레벨을 룬보다 무겁게 친다(25 vs 20).** 룬 강화는 그 룬 하나에만
        /// 붙지만 용사 강화는 넷다섯 전원에게 붙는다. 전장에 들어가는 총량이
        /// 몇 배인데 표시만 같으면, 파편을 어디에 쓸지 고르는 근거가 거짓이 된다.
        ///
        /// 이게 없으면 **파편을 써도 주력이 안 움직인다.** 용사 강화를 넣으면서
        /// 여기를 같이 안 고쳐 둬서 실제로 그런 상태였다.
        /// </summary>
        public static int HeroScore
        {
            get
            {
                int sum = 0;
                for (int i = 0; i < HeroRoster.Count; i++)
                {
                    var kind = HeroRoster.At(i).Kind;
                    if (!HeroRoster.IsUnlocked(kind)) continue;
                    sum += (PlayerData.HeroLevel(kind) - 1) * PerHeroLevel;
                }
                return sum;
            }
        }

        public static int GlyphScore
        {
            get
            {
                // 룬마다 낀 것을 전부 더한다. 예전엔 전체 공용 목록 하나였는데,
                // 그 필드를 그대로 읽고 있으면 룬별로 바뀐 뒤에는 늘 0이 나온다.
                int sum = 0;
                var runes = GlyphTable.Runes;
                for (int rn = 0; rn < runes.Length; rn++)
                {
                    var equipped = PlayerData.EquippedOn(runes[rn]);
                    for (int i = 0; i < equipped.Count; i++)
                    {
                        GlyphData g = PlayerData.FindGlyph(equipped[i]);
                        if (g == null) continue;

                        int r = Mathf.Clamp(g.rarity, 0, RarityWeight.Length - 1);

                        // 값 자체도 반영한다. 같은 등급이라도 굴림이 좋으면 더 세야
                        // "좋은 걸 뽑았다"가 숫자에 남는다.
                        sum += Mathf.RoundToInt(RarityWeight[r] * (0.7f + g.value * 1.5f));
                    }
                }
                return sum;
            }
        }

        public static int ProgressScore
        {
            get
            {
                int stars = StageProgress.TotalStars;
                return InkBudget.TierOf(stars) * PerInkTier + stars * PerStar;
            }
        }

        private static int _cached = -1;
        private static int _cachedPlayer = -1;
        private static int _cachedStage = -1;

        /// <summary>
        /// 합계.
        ///
        /// **캐시가 필요하다.** 이 값은 상단 재화 바·타이틀·대장장이에서 매 프레임
        /// 읽는데, GlyphScore가 장착 칸마다 전체 문양 목록을 선형 탐색한다
        /// (PlayerData.FindGlyph). 10연차를 몇 번 돌려 문양이 300개면 화면에 아무
        /// 변화가 없어도 프레임마다 수천 번을 순회한다 —
        /// Loadout에서 똑같은 실수를 하고 고쳤는데 여기서 되살렸다.
        ///
        /// 저장 데이터가 바뀔 때만 다시 계산한다.
        /// </summary>
        public static int Total
        {
            get
            {
                int pv = PlayerData.Revision;
                int sv = StageProgress.Revision;

                if (_cached < 0 || pv != _cachedPlayer || sv != _cachedStage)
                {
                    _cached = Base + RuneScore + HeroScore + GlyphScore + ProgressScore;
                    _cachedPlayer = pv;
                    _cachedStage = sv;
                }
                return _cached;
            }
        }

        /// <summary>
        /// 지금 이 룬을 한 단계 올리면 주력이 얼마나 오르나.
        /// 강화 버튼에 붙여 두면 "눌러서 확인"이 아니라 "보고 결정"이 된다.
        /// </summary>
        public static int GainFromUpgrade(RuneType rune)
        {
            return PlayerData.LevelOf(rune) >= PlayerData.MaxRuneLevel ? 0 : PerRuneLevel;
        }

        /// <summary>이 문양을 끼면 주력이 얼마나 달라지나. 음수면 지금 것보다 나쁘다.</summary>
        public static int ValueOf(GlyphData g)
        {
            if (g == null) return 0;
            int r = Mathf.Clamp(g.rarity, 0, RarityWeight.Length - 1);
            return Mathf.RoundToInt(RarityWeight[r] * (0.7f + g.value * 1.5f));
        }
    }
}
