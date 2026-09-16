using UnityEngine;

namespace RuneCast.Meta
{
    /// <summary>
    /// 한 번에 그을 수 있는 궤적의 총 길이 = "잉크".
    ///
    /// 마나가 **얼마나 자주** 쓰느냐를 제한한다면, 잉크는 **얼마나 크게** 쓰느냐를 제한한다.
    /// 두 축이 겹치지 않아서 같이 둘 수 있다.
    ///
    /// 이 게임에 특히 잘 맞는 이유:
    ///  - 효과 범위가 이미 "그린 크기"에서 나온다. 길이를 조이면 범위가 그대로 줄어든다.
    ///  - 인식기가 크기를 정규화하므로 **작게 그려도 인식률은 그대로다.**
    ///    즉 잉크 제한은 "못 알아듣게" 만드는 게 아니라 "작게만 쓰게" 만든다.
    ///  - 도형마다 드는 길이가 달라서(원 = 지름의 3.1배, 별 = 4.8배) 잉크가 적은 초반에는
    ///    복잡한 룬을 쓸 만한 크기로 그릴 수 없다. 룬 해금이 저절로 생긴다.
    ///
    /// 단위는 픽셀이 아니라 **화면 높이 배수**다. 해상도가 바뀌어도 체감이 같아야 한다.
    /// </summary>
    public static class InkBudget
    {
        /// <summary>(필요 총 별, 화면 높이 배수). 앞에서부터 조건을 만족하는 마지막 단계가 현재 등급.</summary>
        private static readonly float[,] Tiers =
        {
            //  별,  배수
            {  0f, 0.55f },
            {  4f, 0.72f },
            {  9f, 0.90f },
            { 15f, 1.10f },
            { 22f, 1.32f },
            { 30f, 1.60f },
        };

        public static int TierCount { get { return Tiers.GetLength(0); } }

        public static int TierOf(int totalStars)
        {
            int tier = 0;
            for (int i = 0; i < Tiers.GetLength(0); i++)
                if (totalStars >= Tiers[i, 0]) tier = i;
            return tier;
        }

        public static float ScreenHeightsAt(int tier)
        {
            tier = Mathf.Clamp(tier, 0, Tiers.GetLength(0) - 1);
            return Tiers[tier, 1];
        }

        public static float CurrentScreenHeights
        {
            get { return ScreenHeightsAt(TierOf(StageProgress.TotalStars)); }
        }

        /// <summary>지금 해상도에서의 실제 픽셀 한도.</summary>
        public static float CurrentPixels
        {
            get { return CurrentScreenHeights * Screen.height; }
        }

        /// <summary>다음 단계까지 필요한 별. 최대 단계면 -1.</summary>
        public static int StarsToNextTier(int totalStars)
        {
            int tier = TierOf(totalStars);
            if (tier + 1 >= Tiers.GetLength(0)) return -1;
            return Mathf.CeilToInt(Tiers[tier + 1, 0]) - totalStars;
        }

        /// <summary>
        /// 이 잉크로 그릴 수 있는 도형의 최대 크기(화면 높이 배수).
        /// perimeterRatio는 "바운딩박스 한 변 대비 궤적 길이".
        /// UI에서 "지금 원을 얼마나 크게 그릴 수 있는가"를 보여줄 때 쓴다.
        /// </summary>
        public static float MaxShapeSize(float perimeterRatio)
        {
            return CurrentScreenHeights / Mathf.Max(perimeterRatio, 0.01f);
        }

        /// <summary>
        /// 도형별 "바운딩박스 최대 변 대비 궤적 길이".
        /// 기하로 계산한 값이다 — 이 비율이 곧 룬마다 드는 잉크의 차이다.
        ///
        ///   꺾쇠 2.0 ≈ 깃발 2.1  &lt;  무한대 3.0 ≈ 삼각형 3.0 ≈ 하트 3.1 ≈ 원 3.14
        ///   &lt;  지그재그 3.3  &lt;  나선 4.8  &lt;  별 5.1
        ///
        /// 잉크가 적은 초반에 별·나선을 쓸 만한 크기로 못 그리는 게 이 차이에서 나온다.
        /// 룬 해금을 따로 만들 필요가 없는 이유.
        /// </summary>
        public const float ChevronRatio = 1.8f;   // 두 선분
        public const float TriangleRatio = 3.0f;  // 정삼각형 둘레 / 외접 폭
        public const float CircleRatio = 3.14f;   // πd / d
        public const float ZigzagRatio = 3.4f;    // Z자 세 선분
        public const float SpiralRatio = 4.4f;    // 2.5바퀴, 평균 반지름 기준 호 길이
        public const float StarRatio = 5.0f;      // 펜타그램 다섯 현

        // 새 룬 셋. 실제 템플릿 점열에서 잰 값이다(손으로 계산한 위쪽 값들과 오차 2% 안).
        public const float BannerRatio = 2.11f;   // 자루 + 고리 — 꺾쇠 다음으로 싸다
        public const float InfinityRatio = 3.03f; // 고리 두 개
        public const float HeartRatio = 3.11f;    // 원과 거의 같다
    }
}
