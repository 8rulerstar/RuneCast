namespace RuneCast.Gesture
{
    /// <summary>
    /// 제스처 좌표 한 점. UnityEngine.Vector2를 쓰지 않는 이유는
    /// 인식 파이프라인 전체를 에디터 없이 검증할 수 있게 하기 위함이다.
    /// </summary>
    public struct GPoint
    {
        public readonly float X;
        public readonly float Y;

        /// <summary>멀티스트로크 확장용. 지금은 전부 0.</summary>
        public readonly int StrokeId;

        public GPoint(float x, float y, int strokeId = 0)
        {
            X = x;
            Y = y;
            StrokeId = strokeId;
        }

        public override string ToString()
        {
            return string.Format("({0:F3}, {1:F3})", X, Y);
        }
    }
}
