using System.Collections.Generic;
using UnityEngine;
using RuneCast.Battle;

namespace RuneCast.Meta
{
    /// <summary>
    /// 전투 중에 내려놓을 수 있는 용사 카드.
    ///
    /// **클래시 로얄의 카드 배치를 이 게임에 얹는다.** 지금까지 아군은 판이
    /// 시작될 때 한 번 서고 그걸로 끝이었다 — 편성은 전투 전에 정하는 것이고,
    /// 전투 중 플레이어가 할 수 있는 건 룬뿐이었다.
    ///
    /// 카드가 붙으면 전투 중 손이 둘로 갈린다:
    ///  · **룬** — 지금 벌어지는 일에 개입한다 (즉시, 한 번)
    ///  · **배치** — 앞으로 벌어질 일을 바꾼다 (남아서 계속 싸운다)
    ///
    /// **마나를 나눠 쓴다.** 엘릭서를 따로 만들지 않은 이유는 이 프로젝트가
    /// 재화를 하나로 두는 것과 같다(파편). 자원이 갈리면 "룬을 쓸까 유닛을
    /// 낼까"가 사라지고 각자 알아서 차오르는 두 개의 시계가 된다.
    ///
    /// 비용은 룬(15~55) 사이에 넣었다. 유닛은 남아서 계속 싸우므로 한 번 쓰고
    /// 끝나는 룬보다 비싸야 하지만, 별똥별(45)보다 비싸면 아무도 안 낸다.
    /// </summary>
    public struct DeckCard
    {
        public UnitKind Kind;
        public int Cost;
        public string NameKey;

        public DeckCard(UnitKind kind, int cost, string nameKey)
        {
            Kind = kind; Cost = cost; NameKey = nameKey;
        }
    }

    public static class Deck
    {
        /// <summary>
        /// 손에 드는 카드.
        ///
        /// **편성(Party)과 같은 목록을 쓴다.** 대장장이에서 해금한 용사가 그대로
        /// 카드가 되므로 배울 것이 늘지 않는다 — 편성은 "판을 시작할 때 서는 넷",
        /// 카드는 "싸우는 중에 더 낼 수 있는 것"이고 둘 다 같은 용사들이다.
        /// </summary>
        private static readonly DeckCard[] All =
        {
            new DeckCard(UnitKind.Warrior, 24, "hero.warrior"),
            new DeckCard(UnitKind.Archer,  30, "hero.archer"),
            new DeckCard(UnitKind.Monk,    34, "hero.monk"),
        };

        /// <summary>지금 낼 수 있는 카드들. 잠긴 용사는 카드로도 안 나온다.</summary>
        public static List<DeckCard> Hand()
        {
            var list = new List<DeckCard>();
            for (int i = 0; i < All.Length; i++)
                if (HeroRoster.IsUnlocked(All[i].Kind)) list.Add(All[i]);
            return list;
        }

        /// <summary>
        /// 이 자리에 내려놓을 수 있는가.
        ///
        /// **자기 쪽에만 놓을 수 있다.** 클래시 로얄이 다리 건너에 못 놓게 하는
        /// 것과 같은 이유다 — 적 뒷줄에 바로 떨어뜨릴 수 있으면 주술사·놀처럼
        /// "뒤에 서서 괴롭히는" 적이 내는 질문이 통째로 사라진다.
        ///
        /// 전장 한가운데(x=0)를 경계로 본다. 아군 시작선이 -3.5, 적이 +3.5라
        /// 딱 절반이다.
        /// </summary>
        public static bool CanDeployAt(Vector3 world)
        {
            return world.x < 0f;
        }
    }
}
