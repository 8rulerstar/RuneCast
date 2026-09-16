using UnityEngine;
using RuneCast.Gesture;

namespace RuneCast.Meta
{
    /// <summary>
    /// 룬 강화 곡선.
    ///
    /// 레벨은 **위력만** 올린다. 범위까지 같이 올리면 잉크 제한이 무의미해지고,
    /// 마나까지 내리면 강화가 다른 모든 축을 덮어버린다.
    /// 범위·마나·잉크는 문양(뽑기)의 몫으로 남겨 둬야 두 시스템이 각자 할 일이 생긴다.
    /// </summary>
    public static class RuneUpgrades
    {
        /// <summary>레벨 1은 배율 1.0, 레벨당 +8%.</summary>
        public static float PowerMultiplier(RuneType rune)
        {
            return 1f + 0.08f * (PlayerData.LevelOf(rune) - 1);
        }

        /// <summary>
        /// 다음 레벨로 올리는 데 드는 파편. 최대 레벨이면 -1.
        ///
        /// 룬 하나 만렙에 약 1020. 전 스테이지 3별 첫 클리어 수급이 2448이므로
        /// 한 번 훑으면 룬 두 개를 올리거나 뽑기 24회를 할 수 있다 —
        /// **둘 다는 못 한다는 게 핵심**이다. 다 되면 선택이 사라진다.
        /// </summary>
        public static int UpgradeCost(RuneType rune)
        {
            int level = PlayerData.LevelOf(rune);
            if (level >= PlayerData.MaxRuneLevel) return -1;
            return 10 + 8 * level + 2 * level * level;
        }

        public static bool TryUpgrade(RuneType rune)
        {
            int cost = UpgradeCost(rune);
            if (cost < 0) return false;
            if (!PlayerData.TrySpend(cost)) return false;

            PlayerData.SetLevel(rune, PlayerData.LevelOf(rune) + 1);
            return true;
        }
    }

    /// <summary>
    /// 장착한 문양의 효과를 합쳐서 알려준다.
    ///
    /// 같은 효과가 여러 개면 **더한다**(곱하지 않는다). 곱셈이면 문양을 모을수록
    /// 기하급수적으로 세져서 후반 밸런스가 통째로 무너진다.
    /// 마나·잉크 할인은 60%에서 자른다 — 공짜에 가까워지면 자원 설계가 사라진다.
    /// </summary>
    public static class Loadout
    {
        private const float MaxDiscount = 0.6f;

        private const int EffectCount = 5;
        private const int RuneSlots = 16; // RuneType 최대값보다 넉넉히

        // [효과, 룬] 합계와 [효과] 전체적용 합계. 장착이 바뀔 때만 다시 만든다.
        private static float[,] _perRune;
        private static float[] _global;
        private static bool _valid;

        /// <summary>
        /// 장착이 바뀌었음을 알린다. PlayerData가 문양·장착을 건드릴 때마다 부른다.
        ///
        /// 캐시를 넣은 이유: Sum이 장착 슬롯마다 **전체 문양 목록을 선형 탐색**했다
        /// (FindGlyph). HUD는 룬 9종의 마나 비용을 매 프레임 두 번씩 묻고, OnGUI는
        /// 한 프레임에 두 번 도니까 프레임당 36번. 10연차를 몇 번 돌려 문양이 300개면
        /// 아무 일도 안 일어나는 화면에서 프레임마다 수만 번을 순회하고 있었다.
        /// </summary>
        public static void Invalidate()
        {
            _valid = false;

            // Rebuild가 아니라 여기서 올린다. Rebuild에서 올리면 UI가 "버전이 그대로니
            // 다시 계산할 필요 없다"고 판단해 Rebuild를 부르지 않고, 그래서 버전도 안 오르는
            // 순환에 빠져 낡은 값이 영영 남는다.
            Version++;
        }

        /// <summary>
        /// 장착 구성이 바뀔 때마다 오른다. UI가 자기 캐시를 언제 버릴지 판단하는 데 쓴다
        /// (예: HUD의 마나 비용 문자열).
        /// </summary>
        public static int Version { get; private set; }

        private static void Rebuild()
        {
            if (_perRune == null)
            {
                _perRune = new float[EffectCount, RuneSlots];
                _global = new float[EffectCount];
            }

            System.Array.Clear(_perRune, 0, _perRune.Length);
            System.Array.Clear(_global, 0, _global.Length);

            // **문양은 끼워진 룬에만 걸린다.** 예전에는 대상이 없는 문양이 모든
            // 룬에 걸렸는데, 룬마다 칸이 생긴 지금은 "어디에 끼웠나"가 곧 대상이다.
            // 대상 없는 문양의 값어치는 이제 "아무 룬에나 낄 수 있다"는 쪽이다.
            var runes = GlyphTable.Runes;
            for (int r = 0; r < runes.Length; r++)
            {
                int ri = (int)runes[r];
                if (ri <= 0 || ri >= RuneSlots) continue;

                var slots = PlayerData.EquippedOn(runes[r]);
                for (int i = 0; i < slots.Count; i++)
                {
                    GlyphData g = PlayerData.FindGlyph(slots[i]);
                    if (g == null) continue;

                    int e = g.effect;
                    if (e < 0 || e >= EffectCount) continue;

                    _perRune[e, ri] += g.value;
                }
            }

            _valid = true;
        }

        private static float Sum(GlyphEffect effect, RuneType rune)
        {
            if (!_valid) Rebuild();

            int e = (int)effect;
            int r = (int)rune;
            float total = _global[e];
            if (r > 0 && r < RuneSlots) total += _perRune[e, r];
            return total;
        }

        /// <summary>위력 배율. 룬 레벨 강화까지 포함한 최종값.</summary>
        public static float PowerMultiplier(RuneType rune)
        {
            return RuneUpgrades.PowerMultiplier(rune) * (1f + Sum(GlyphEffect.Power, rune));
        }

        /// <summary>그린 크기에 곱할 배율.</summary>
        public static float SizeMultiplier(RuneType rune)
        {
            return 1f + Sum(GlyphEffect.Size, rune);
        }

        /// <summary>마나 비용 배율 (1보다 작으면 할인).</summary>
        public static float ManaMultiplier(RuneType rune)
        {
            return 1f - Mathf.Min(Sum(GlyphEffect.ManaCost, rune), MaxDiscount);
        }

        /// <summary>
        /// 잉크 한도 배율. 룬을 가리지 않는다 —
        /// 잉크 한도는 그리기 시작할 때 정해지는데 그 시점엔 무슨 룬인지 알 수 없다.
        /// </summary>
        public static float InkMultiplier()
        {
            return 1f + Mathf.Min(Sum(GlyphEffect.InkCost, RuneType.None), MaxDiscount);
        }

        /// <summary>판정 점수에 더할 보정.</summary>
        public static float GradeBonus(RuneType rune)
        {
            return Sum(GlyphEffect.Grade, rune);
        }
    }
}
