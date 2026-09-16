using UnityEngine;
using RuneCast.Gesture;

namespace RuneCast.Core
{
    /// <summary>
    /// 등급의 표현 값. HUD와 이펙트가 **같은 색**을 써야 "내가 잘 그려서 저렇게 됐다"가 읽힌다.
    /// 색이 따로 놀면 화면만 요란하고 인과가 전달되지 않는다.
    /// </summary>
    public static class GradeVisuals
    {
        public static Color ColorOf(RuneGrade g)
        {
            switch (g)
            {
                case RuneGrade.Perfect: return new Color(1f, 0.92f, 0.45f);  // 금색
                case RuneGrade.Excellent: return new Color(0.6f, 0.95f, 1f); // 청록
                case RuneGrade.Great: return new Color(0.78f, 1f, 0.8f);     // 연두
                default: return new Color(0.78f, 0.78f, 0.84f);              // 무채색
            }
        }

        /// <summary>등급이 높을수록 화면을 세게 흔든다. GOOD·GREAT는 안 흔든다.</summary>
        public static float ShakeOf(RuneGrade g)
        {
            switch (g)
            {
                case RuneGrade.Perfect: return 0.20f;
                case RuneGrade.Excellent: return 0.10f;
                default: return 0f;
            }
        }

        /// <summary>등급이 높을수록 소리도 조금 더 크고 높게.</summary>
        public static float VolumeOf(RuneGrade g)
        {
            return 0.7f + 0.1f * (int)g;
        }
    }
}
