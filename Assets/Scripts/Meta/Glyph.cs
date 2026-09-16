using System.Collections.Generic;
using UnityEngine;
using RuneCast.Gesture;

namespace RuneCast.Meta
{
    public enum GlyphRarity
    {
        Common = 0,
        Rare = 1,
        Epic = 2,
        Legendary = 3,
    }

    public enum GlyphEffect
    {
        Power = 0,    // 위력 +%
        Size = 1,     // 범위 +%
        ManaCost = 2, // 마나 소모 -%
        InkCost = 3,  // 잉크 소모 -% (= 더 크게 그릴 수 있음)
        Grade = 4,    // 판정 점수 보정 (+절대값)
    }

    /// <summary>
    /// 문양 뽑기 표와 값.
    ///
    /// 효과 다섯 가지는 서로 다른 축을 건드린다 — 위력/범위/마나/잉크/판정.
    /// 전부 "데미지 +%"류였으면 뽑기 결과가 숫자만 다른 같은 것이 되고,
    /// 그러면 뽑는 재미가 첫 판에 끝난다.
    ///
    /// **잉크 효과는 항상 전체 적용이다.** 잉크 한도는 그리기 시작할 때 정해지는데
    /// 그 시점엔 무슨 룬을 그릴지 알 수 없다. 룬별 잉크 할인은 구현할 수가 없다.
    /// </summary>
    public static class GlyphTable
    {
        /// <summary>한 번 뽑는 데 드는 파편.</summary>
        public const int PullCost = 100;

        /// <summary>10연차. 개당 가격은 단차와 같다.</summary>
        public const int MultiCount = 10;
        public const int MultiCost = PullCost * MultiCount;

        /// <summary>
        /// 10연차 확정 등급.
        ///
        /// **가격을 할인하지 않은 이유:** 개당 가격을 깎으면 실측으로 맞춘 파편 경제
        /// (문양 하나 = 100)가 통째로 틀어진다. 확정 자체가 값어치여야 한다.
        ///
        /// 영웅 이상으로 잡은 근거: 희귀 확정은 의미가 없다 —
        /// 10번 뽑으면 어차피 99.9% 나온다. 영웅 이상은 그냥 뽑으면 13.7%의 확률로
        /// 하나도 안 나오므로, **그 13.7%에서만 작동하는** 보증이 된다.
        /// 있으나 마나도 아니고 압도적이지도 않다.
        /// </summary>
        public const GlyphRarity MultiGuarantee = GlyphRarity.Epic;

        /// <summary>
        /// 일반 / 희귀 / 영웅 / 전설.
        ///
        /// 전설 2%로 잡았다가 4%로 올렸다. 스테이지가 12개뿐이라 총 수급이 한정적인데,
        /// 2%면 전설 하나를 기대하는 데 파편 5000 — 전 스테이지 3별 클리어 두 번 분량이라
        /// **시스템이 있는 줄도 모르고 끝난다.** 콘텐츠가 늘면 다시 조일 것.
        /// </summary>
        private static readonly float[] RarityRate = { 0.52f, 0.30f, 0.14f, 0.04f };

        public static string RarityName(GlyphRarity r)
        {
            switch (r)
            {
                case GlyphRarity.Legendary: return Loc.T("glyph.legendary");
                case GlyphRarity.Epic: return Loc.T("glyph.epic");
                case GlyphRarity.Rare: return Loc.T("glyph.rare");
                default: return Loc.T("glyph.common");
            }
        }

        public static Color RarityColor(GlyphRarity r)
        {
            switch (r)
            {
                case GlyphRarity.Legendary: return new Color(1f, 0.78f, 0.3f);
                case GlyphRarity.Epic: return new Color(0.82f, 0.6f, 1f);
                case GlyphRarity.Rare: return new Color(0.55f, 0.82f, 1f);
                default: return new Color(0.85f, 0.85f, 0.85f);
            }
        }

        public static int DismantleValue(GlyphRarity r)
        {
            switch (r)
            {
                case GlyphRarity.Legendary: return 220;
                case GlyphRarity.Epic: return 110;
                case GlyphRarity.Rare: return 55;
                default: return 25;
            }
        }

        public static string EffectName(GlyphEffect e)
        {
            switch (e)
            {
                case GlyphEffect.Power: return Loc.T("glyph.power");
                case GlyphEffect.Size: return Loc.T("glyph.size");
                case GlyphEffect.ManaCost: return Loc.T("glyph.mana");
                case GlyphEffect.InkCost: return Loc.T("glyph.ink");
                default: return Loc.T("glyph.grade");
            }
        }

        /// <summary>효과 × 등급의 기본 크기. 전체 적용 문양은 여기에 0.5를 곱한다.</summary>
        private static float BaseValue(GlyphEffect e, GlyphRarity r)
        {
            int t = (int)r;
            switch (e)
            {
                case GlyphEffect.Power: return new[] { 0.06f, 0.12f, 0.20f, 0.32f }[t];
                case GlyphEffect.Size: return new[] { 0.05f, 0.10f, 0.16f, 0.26f }[t];
                case GlyphEffect.ManaCost: return new[] { 0.05f, 0.10f, 0.16f, 0.25f }[t];
                case GlyphEffect.InkCost: return new[] { 0.04f, 0.08f, 0.13f, 0.20f }[t];
                default: return new[] { 0.03f, 0.06f, 0.10f, 0.16f }[t];
            }
        }

        /// <summary>
        /// 현재 활성화된 룬만 대상으로 뽑는다 (가르기는 비활성이라 제외).
        /// 잉크가 싼 순서 = 초반에 실제로 쓰게 되는 순서로 둔다. HUD 목록도 이 순서를 따른다.
        /// </summary>
        private static readonly RuneType[] TargetPool =
        {
            RuneType.Arrow, RuneType.Pyre, RuneType.Vortex, RuneType.Empower,
            RuneType.Heal, RuneType.Chain, RuneType.Shield, RuneType.Revive,
            RuneType.Meteor,
        };

        /// <summary>
        /// 뽑기 한 번마다 각인권이 딸려 나올 확률.
        ///
        /// **문양 카드와 따로 준다.** 카드 격자는 GlyphData 전용이라 거기에 다른
        /// 종류를 끼우면 뽑기 연출을 통째로 뜯어야 하고, 무엇보다 각인권은
        /// 등급이 없어서 카드로 만들면 "꽝"처럼 보인다.
        ///
        /// 8%면 10연차에 기대 0.8장이다. 한 번 뽑을 때마다 나오면 귀한 줄 모르고,
        /// 스무 번에 한 장이면 있는 줄도 모른다. 그 사이를 잡았다.
        /// </summary>
        public const float InscribeTicketChance = 0.08f;

        /// <summary>이번 뽑기에서 각인권이 몇 장 나왔나. 뽑은 수만큼 굴린다.</summary>
        public static int RollInscribeTickets(int pulls)
        {
            int n = 0;
            for (int i = 0; i < pulls; i++)
                if (UnityEngine.Random.value < InscribeTicketChance) n++;
            return n;
        }

        public static GlyphData Roll()
        {
            GlyphRarity rarity = RollRarity();
            GlyphEffect effect = (GlyphEffect)Random.Range(0, 5);

            // 잉크는 룬을 가릴 수 없으므로 무조건 전체.
            // 나머지는 30% 확률로 전체(대신 값이 절반).
            bool global = effect == GlyphEffect.InkCost || Random.value < 0.3f;

            float value = BaseValue(effect, rarity) * (global ? 0.5f : 1f);

            // 같은 등급 안에서도 ±12% 흔들어 준다. 결과가 표에서 바로 읽히면
            // 두 번째 뽑기부터 확인 작업이 된다.
            value *= Random.Range(0.88f, 1.12f);

            return new GlyphData
            {
                rarity = (int)rarity,
                effect = (int)effect,
                targetRune = global ? (int)RuneType.None : (int)TargetPool[Random.Range(0, TargetPool.Length)],
                value = value,
            };
        }

        /// <summary>
        /// 10연차. 확정 등급이 하나도 안 나왔으면 **마지막 칸을** 확정으로 바꾼다.
        ///
        /// 무작위 칸을 고르지 않는 이유: 연출에서 카드를 순서대로 뒤집는데,
        /// 확정이 중간에 박히면 그 뒤 카드들은 이미 결과를 아는 상태로 넘어간다.
        /// 마지막에 두면 끝까지 볼 이유가 남는다.
        /// </summary>
        public static List<GlyphData> RollMulti()
        {
            var list = new List<GlyphData>(MultiCount);
            bool hasGuarantee = false;

            for (int i = 0; i < MultiCount; i++)
            {
                GlyphData g = Roll();
                if ((GlyphRarity)g.rarity >= MultiGuarantee) hasGuarantee = true;
                list.Add(g);
            }

            if (!hasGuarantee)
            {
                GlyphData fixedUp;
                do { fixedUp = Roll(); }
                while ((GlyphRarity)fixedUp.rarity < MultiGuarantee);

                list[MultiCount - 1] = fixedUp;
            }

            return list;
        }

        private static GlyphRarity RollRarity()
        {
            float roll = Random.value;
            float acc = 0f;
            for (int i = 0; i < RarityRate.Length; i++)
            {
                acc += RarityRate[i];
                if (roll < acc) return (GlyphRarity)i;
            }
            return GlyphRarity.Common;
        }

        /// <summary>
        /// 수치만. 카드처럼 대상·효과를 따로 보여주는 자리에서 쓴다 —
        /// Describe는 한 줄에 다 넣은 것이라 카드에 넣으면 같은 말이 두 번 나온다.
        /// </summary>
        public static string ValueText(GlyphData g)
        {
            var effect = (GlyphEffect)g.effect;
            if (effect == GlyphEffect.Grade) return string.Format("+{0:F2}", g.value);

            string sign = (effect == GlyphEffect.ManaCost || effect == GlyphEffect.InkCost) ? "-" : "+";
            return string.Format("{0}{1:P0}", sign, g.value);
        }

        public static string Describe(GlyphData g)
        {
            var effect = (GlyphEffect)g.effect;
            string target = g.targetRune == (int)RuneType.None ? Loc.T("rune.all") : RuneName((RuneType)g.targetRune);

            if (effect == GlyphEffect.Grade)
                return string.Format("{0}  {1} +{2:F2}", target, EffectName(effect), g.value);

            string sign = (effect == GlyphEffect.ManaCost || effect == GlyphEffect.InkCost) ? "-" : "+";
            return string.Format("{0}  {1} {2}{3:P0}", target, EffectName(effect), sign, g.value);
        }

        /// <summary>룬 이름. 화면에 뜨는 이름은 전부 여기를 거친다.</summary>
        public static string RuneName(RuneType t)
        {
            switch (t)
            {
                case RuneType.Heal: return Loc.T("rune.heal");
                case RuneType.Arrow: return Loc.T("rune.arrow");
                case RuneType.Shield: return Loc.T("rune.shield");
                case RuneType.Meteor: return Loc.T("rune.meteor");
                case RuneType.Chain: return Loc.T("rune.chain");
                case RuneType.Vortex: return Loc.T("rune.vortex");
                case RuneType.Slash: return Loc.T("rune.slash");
                case RuneType.Empower: return Loc.T("rune.empower");
                case RuneType.Revive: return Loc.T("rune.revive");
                case RuneType.Pyre: return Loc.T("rune.pyre");
                default: return Loc.T("rune.all");
            }
        }

        /// <summary>그리는 도형 이름.</summary>
        public static string ShapeName(RuneType t)
        {
            switch (t)
            {
                case RuneType.Heal: return Loc.T("shape.circle");
                case RuneType.Arrow: return Loc.T("shape.chevron");
                case RuneType.Shield: return Loc.T("shape.triangle");
                case RuneType.Meteor: return Loc.T("shape.star");
                case RuneType.Chain: return Loc.T("shape.zigzag");
                case RuneType.Vortex: return Loc.T("shape.spiral");
                case RuneType.Slash: return Loc.T("shape.line");
                case RuneType.Empower: return Loc.T("shape.infinity");
                case RuneType.Revive: return Loc.T("shape.heart");
                case RuneType.Pyre: return Loc.T("shape.banner");
                default: return "";
            }
        }

        /// <summary>화면에 쓰는 도형 글자. 언어와 무관하다.</summary>
        public static string Glyph(RuneType t)
        {
            switch (t)
            {
                case RuneType.Heal: return "○";
                case RuneType.Arrow: return "＞";
                case RuneType.Shield: return "△";
                case RuneType.Meteor: return "☆";
                case RuneType.Chain: return "Ｚ";
                case RuneType.Vortex: return "◎";
                case RuneType.Slash: return "／";
                case RuneType.Empower: return "∞";
                case RuneType.Revive: return "♡";
                // 원래 룬 문자 ᛝ(U+16DD)를 썼는데 한글 폰트에 그 글자가 없어서
                // 화면에 네모로 떴다. ¶는 고리+자루라 깃발 도형과 그림이 오히려 더 맞는다.
                case RuneType.Pyre: return "¶";
                default: return "·";
            }
        }

        public static RuneType[] Runes { get { return TargetPool; } }
    }
}
