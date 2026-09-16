using System.Collections.Generic;
using UnityEngine;
using RuneCast.Battle;

namespace RuneCast.Meta
{
    /// <summary>
    /// 쓸 수 있는 용사 종류와, 그걸 여는 값·키우는 값.
    ///
    /// **해금은 별, 강화는 파편.** 축을 나눈 근거는 이 게임에 이미 난 구멍 두 개다.
    ///
    ///  · 별은 42판이 되면서 최대 126개가 됐는데 쓸 데가 없다. 잉크 단계가
    ///    30별에서 끝나고 그 뒤로는 아무것도 안 열린다.
    ///  · 파편은 반대다. 강화 없이도 전 판 3별이라 룬을 키울 이유가 없어졌다.
    ///
    /// 용사 해금을 별에, 강화를 파편에 걸면 두 구멍이 서로를 메운다.
    ///
    /// **뽑기로 하지 않았다.** 문양은 붙였다 뗄 수 있어서 실패한 뽑기가 플레이를
    /// 막지 않지만, 용사는 부대 그 자체다. 방패병이 안 나온 사람은 "버티는 플레이"를
    /// 아예 못 하게 된다 — 룬 도형을 뽑기에 걸지 않은 것과 같은 이유다.
    ///
    /// **강화는 위력만 올린다.** 룬 강화와 같은 규칙이고, 이유도 같다 —
    /// 사거리나 이동속도까지 올리면 배치와 편성이 내는 질문이 흐려진다.
    /// </summary>
    public static class HeroRoster
    {
        public struct HeroDef
        {
            public UnitKind Kind;
            public int UnlockStars;   // 0이면 처음부터
            public string NameKey;
            public string RoleKey;
        }

        /// <summary>
        /// 고르는 순서가 곧 배우는 순서다. 전사로 시작해서 궁수(뒷줄이 생긴다),
        /// 수도사(회복이 자동으로 된다) 순으로 열린다.
        ///
        /// 해금 별 수는 1·2장에서 모을 수 있는 36개 안에 넣었다. 3장 이후는
        /// 아직 틀만 있는 판이라, 거기까지 가야 열리면 **설계된 구간에서는
        /// 새 용사를 한 번도 못 써 본다.**
        /// </summary>
        private static readonly HeroDef[] All =
        {
            new HeroDef { Kind = UnitKind.Warrior, UnlockStars = 0,
                          NameKey = "hero.warrior", RoleKey = "hero.warrior.role" },
            new HeroDef { Kind = UnitKind.Archer,  UnlockStars = 10,
                          NameKey = "hero.archer",  RoleKey = "hero.archer.role" },
            new HeroDef { Kind = UnitKind.Monk,    UnlockStars = 24,
                          NameKey = "hero.monk",    RoleKey = "hero.monk.role" },
        };

        public static int Count { get { return All.Length; } }
        public static HeroDef At(int i) { return All[Mathf.Clamp(i, 0, All.Length - 1)]; }

        public static HeroDef Of(UnitKind kind)
        {
            for (int i = 0; i < All.Length; i++)
                if (All[i].Kind == kind) return All[i];
            return All[0];
        }

        public static bool IsUnlocked(UnitKind kind)
        {
            return StageProgress.TotalStars >= Of(kind).UnlockStars;
        }

        /// <summary>지금 쓸 수 있는 용사들. 편성 화면과 전투 소환이 이걸 본다.</summary>
        public static List<UnitKind> Unlocked()
        {
            var list = new List<UnitKind>();
            for (int i = 0; i < All.Length; i++)
                if (IsUnlocked(All[i].Kind)) list.Add(All[i].Kind);
            return list;
        }

        // ── 강화 ────────────────────────────────────────────────

        public const int MaxLevel = 10;

        /// <summary>
        /// 레벨당 위력 +6%.
        ///
        /// 룬은 +8%인데 여기를 낮게 잡았다. 용사는 넷다섯이 동시에 받으므로
        /// 같은 배율이라도 전장에 들어가는 총량이 몇 배다. **룬보다 세지면
        /// 개입할 이유가 줄고, 그건 이 게임이 아니게 되는 방향이다.**
        /// </summary>
        public static float PowerAt(int level)
        {
            return 1f + 0.06f * Mathf.Clamp(level - 1, 0, MaxLevel - 1);
        }

        public static float PowerOf(UnitKind kind)
        {
            return PowerAt(PlayerData.HeroLevel(kind));
        }

        /// <summary>
        /// 다음 레벨 값. 룬 강화(총 1020)와 같은 자릿수로 맞췄다 —
        /// "룬을 올릴까 용사를 올릴까"가 성립하려면 값이 비슷해야 한다.
        /// </summary>
        public static int UpgradeCost(int currentLevel)
        {
            return 60 + currentLevel * 40;
        }

        public static int TotalCostToMax
        {
            get
            {
                int sum = 0;
                for (int lv = 1; lv < MaxLevel; lv++) sum += UpgradeCost(lv);
                return sum;
            }
        }
    }
}
