namespace RuneCast.Gesture
{
    /// <summary>
    /// 그린 정확도 등급.
    ///
    /// **최하위 등급도 긍정어다.** 예전에는 "조잡"이었는데, 룬이 발동은 한 상황에서
    /// 플레이어를 깎아내리는 말을 띄울 이유가 없다. 인식에 실패하면 어차피 따로
    /// "인식 실패"가 뜨므로, 여기까지 온 건 전부 성공이다 — 잘한 정도만 다를 뿐.
    /// </summary>
    public enum RuneGrade
    {
        Good = 0,
        Great = 1,
        Excellent = 2,
        Perfect = 3,
    }

    /// <summary>
    /// 점수 → 등급 변환.
    ///
    /// 숫자(위력 배율)만 바꾸면 플레이어는 자기가 잘 그렸는지 알 수 없다.
    /// 등급을 따로 두는 건 **연출의 단위**가 필요해서다 —
    /// 배율 1.31배와 1.34배는 화면에서 구분되지 않지만, PERFECT와 GREAT는 구분된다.
    ///
    /// 경계값은 실측 분포에 맞췄다: 깔끔하게 그리면 점수 평균 0.79, 보통이면 0.51.
    /// 즉 PERFECT는 의식해서 천천히 그려야 나오고, 급하게 그으면 GOOD~GREAT 언저리다.
    ///
    /// 색·흔들림 같은 표현 값은 UnityEngine에 의존하므로 Core/GradeVisuals로 뺐다.
    /// 이 파일은 에디터 없이 돌리는 인식 테스트에 함께 들어가므로 순수 C#으로 유지한다.
    /// </summary>
    public static class RuneGrading
    {
        public const float PerfectThreshold = 0.85f;
        public const float ExcellentThreshold = 0.65f;
        public const float GreatThreshold = 0.40f;

        public static RuneGrade Of(float score)
        {
            if (score >= PerfectThreshold) return RuneGrade.Perfect;
            if (score >= ExcellentThreshold) return RuneGrade.Excellent;
            if (score >= GreatThreshold) return RuneGrade.Great;
            return RuneGrade.Good;
        }

        public static string DisplayName(RuneGrade g)
        {
            switch (g)
            {
                case RuneGrade.Perfect: return "PERFECT";
                case RuneGrade.Excellent: return "EXCELLENT";
                case RuneGrade.Great: return "GREAT";
                default: return "GOOD";
            }
        }

        /// <summary>연출에 곱할 화려함(0~1). 링 개수·잔상·추가 연출을 여기에 맞춘다.</summary>
        public static float Flourish(RuneGrade g)
        {
            switch (g)
            {
                case RuneGrade.Perfect: return 1f;
                case RuneGrade.Excellent: return 0.6f;
                case RuneGrade.Great: return 0.3f;
                default: return 0f;
            }
        }
    }
}
