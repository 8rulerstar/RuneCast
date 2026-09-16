namespace RuneCast.Gesture
{
    public enum RuneType
    {
        None = 0,

        /// <summary>원 ○ — 범위 회복</summary>
        Heal = 1,

        /// <summary>꺾쇠 &gt; — 그 방향으로 화살 발사</summary>
        Arrow = 2,

        /// <summary>네모 □ — 범위 내 아군에게 보호막</summary>
        Shield = 3,

        /// <summary>별 ☆ — 별똥별 낙하 (예고 후 광역)</summary>
        Meteor = 4,

        /// <summary>직선 ╱ — 그은 선을 따라 띠 모양 피해</summary>
        Slash = 5,

        /// <summary>지그재그 Z — 적 사이를 튕겨가는 연쇄 번개</summary>
        Chain = 6,

        /// <summary>나선 ◎ — 적을 끌어당기고 둔화 (피해 없음)</summary>
        Vortex = 7,

        /// <summary>무한대 ∞ — 범위 내 아군의 공격력·공격속도 상승</summary>
        Empower = 8,

        /// <summary>하트 ♡ — 범위 내 쓰러진 아군 부활</summary>
        Revive = 9,

        /// <summary>깃발 ᛝ — 그 자리에 남아 적을 태우는 불꽃 지대</summary>
        Pyre = 10,
    }
}
