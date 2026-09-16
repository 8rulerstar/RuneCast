using System;
using System.Collections.Generic;

namespace RuneCast.Gesture
{
    /// <summary>
    /// 궤적 전처리 — 리샘플링 / 정규화 / 기하 특징 추출.
    /// 전부 순수 C#. UnityEngine 참조 없음 → 에디터 없이 테스트 가능.
    /// </summary>
    public static class StrokeMath
    {
        /// <summary>
        /// 리샘플 점 개수. 매칭 비용이 O(n^2 * sqrt(n))이라 올릴수록 급격히 무거워진다.
        /// 32면 별 모양(꼭짓점 10개)까지 형태가 살아남는다.
        /// </summary>
        public const int ResampleCount = 32;

        public static float Distance(GPoint a, GPoint b)
        {
            float dx = a.X - b.X;
            float dy = a.Y - b.Y;
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }

        public static float PathLength(IList<GPoint> pts)
        {
            float len = 0f;
            for (int i = 1; i < pts.Count; i++)
            {
                if (pts[i].StrokeId != pts[i - 1].StrokeId) continue;
                len += Distance(pts[i - 1], pts[i]);
            }
            return len;
        }

        public static GPoint Centroid(IList<GPoint> pts)
        {
            float sx = 0f, sy = 0f;
            for (int i = 0; i < pts.Count; i++)
            {
                sx += pts[i].X;
                sy += pts[i].Y;
            }
            float inv = pts.Count > 0 ? 1f / pts.Count : 0f;
            return new GPoint(sx * inv, sy * inv);
        }

        /// <summary>궤적을 등간격 n개 점으로 다시 뽑는다. 그리기 속도의 영향을 제거하는 단계.</summary>
        public static List<GPoint> Resample(IList<GPoint> pts, int n)
        {
            var dst = new List<GPoint>(n);
            if (pts == null || pts.Count == 0) return dst;

            float total = PathLength(pts);
            if (total <= 1e-6f)
            {
                // 한 점에 머무른 입력. 나눗셈 폭주를 막고 같은 점을 채운다.
                for (int i = 0; i < n; i++) dst.Add(pts[0]);
                return dst;
            }

            float interval = total / (n - 1);
            float accumulated = 0f;

            var src = new List<GPoint>(pts);
            dst.Add(src[0]);

            for (int i = 1; i < src.Count; i++)
            {
                if (src[i].StrokeId != src[i - 1].StrokeId)
                {
                    accumulated = 0f;
                    continue;
                }

                float d = Distance(src[i - 1], src[i]);
                if (accumulated + d >= interval)
                {
                    float t = (interval - accumulated) / d;
                    if (float.IsNaN(t) || float.IsInfinity(t)) t = 0.5f;

                    var q = new GPoint(
                        src[i - 1].X + t * (src[i].X - src[i - 1].X),
                        src[i - 1].Y + t * (src[i].Y - src[i - 1].Y),
                        src[i].StrokeId);

                    dst.Add(q);
                    src.Insert(i, q); // 남은 구간을 q부터 다시 재도록 되돌린다
                    accumulated = 0f;
                }
                else
                {
                    accumulated += d;
                }
            }

            // 부동소수 누적 오차로 마지막 한두 점이 모자랄 수 있다
            while (dst.Count < n) dst.Add(src[src.Count - 1]);
            if (dst.Count > n) dst.RemoveRange(n, dst.Count - n);

            return dst;
        }

        /// <summary>
        /// 긴 변 기준 균일 스케일로 0~1 상자에 맞춘다.
        /// 축별 개별 스케일이 아닌 이유: 그러면 납작한 타원과 원이 같아져 버린다.
        /// </summary>
        public static List<GPoint> ScaleToUnit(IList<GPoint> pts)
        {
            var dst = new List<GPoint>(pts.Count);
            if (pts.Count == 0) return dst;

            float minX = float.MaxValue, minY = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue;
            for (int i = 0; i < pts.Count; i++)
            {
                if (pts[i].X < minX) minX = pts[i].X;
                if (pts[i].Y < minY) minY = pts[i].Y;
                if (pts[i].X > maxX) maxX = pts[i].X;
                if (pts[i].Y > maxY) maxY = pts[i].Y;
            }

            float size = Math.Max(maxX - minX, maxY - minY);
            if (size <= 1e-6f) size = 1f;

            for (int i = 0; i < pts.Count; i++)
                dst.Add(new GPoint((pts[i].X - minX) / size, (pts[i].Y - minY) / size, pts[i].StrokeId));

            return dst;
        }

        /// <summary>무게중심을 원점으로. 화면 어디에 그렸는지를 무시하게 만드는 단계.</summary>
        public static List<GPoint> TranslateToOrigin(IList<GPoint> pts)
        {
            var c = Centroid(pts);
            var dst = new List<GPoint>(pts.Count);
            for (int i = 0; i < pts.Count; i++)
                dst.Add(new GPoint(pts[i].X - c.X, pts[i].Y - c.Y, pts[i].StrokeId));
            return dst;
        }

        /// <summary>리샘플 → 균일 스케일 → 중심 이동. 인식기에 넣기 직전의 표준형.</summary>
        public static List<GPoint> Normalize(IList<GPoint> pts, int n = ResampleCount)
        {
            return TranslateToOrigin(ScaleToUnit(Resample(pts, n)));
        }

        public static List<GPoint> Rotate(IList<GPoint> pts, float radians)
        {
            float cos = (float)Math.Cos(radians);
            float sin = (float)Math.Sin(radians);
            var dst = new List<GPoint>(pts.Count);
            for (int i = 0; i < pts.Count; i++)
                dst.Add(new GPoint(
                    pts[i].X * cos - pts[i].Y * sin,
                    pts[i].X * sin + pts[i].Y * cos,
                    pts[i].StrokeId));
            return dst;
        }

        /// <summary>
        /// 꺾쇠의 뾰족한 끝이 가리키는 방향. 화살 발사 방향으로 쓴다.
        /// 시작점–끝점을 잇는 선에서 가장 멀리 떨어진 점을 꼭짓점으로 보고,
        /// 그 선의 중점에서 꼭짓점으로 향하는 벡터를 반환한다.
        ///
        /// "중심에서 가장 먼 점"을 쓰지 않는 이유: &gt; 모양에서는 꼬리 끝 두 개가
        /// 꼭짓점보다 중심에서 더 멀어서 방향이 뒤집힌다.
        /// </summary>
        public static void ApexDirection(IList<GPoint> pts, out float dx, out float dy)
        {
            dx = 1f;
            dy = 0f;
            if (pts == null || pts.Count < 3) return;

            GPoint first = pts[0];
            GPoint last = pts[pts.Count - 1];

            float ax = last.X - first.X;
            float ay = last.Y - first.Y;
            float aLen = (float)Math.Sqrt(ax * ax + ay * ay);

            int apex = -1;
            float best = -1f;

            if (aLen <= 1e-6f)
            {
                // 시작과 끝이 겹친 경우 — 중점 대신 무게중심에서 가장 먼 점
                var c = Centroid(pts);
                for (int i = 0; i < pts.Count; i++)
                {
                    float d = Distance(c, pts[i]);
                    if (d > best) { best = d; apex = i; }
                }
                if (apex >= 0)
                {
                    dx = pts[apex].X - c.X;
                    dy = pts[apex].Y - c.Y;
                }
            }
            else
            {
                for (int i = 0; i < pts.Count; i++)
                {
                    // 선분 (first→last)에서의 수직 거리
                    float cross = Math.Abs(ax * (pts[i].Y - first.Y) - ay * (pts[i].X - first.X));
                    float d = cross / aLen;
                    if (d > best) { best = d; apex = i; }
                }

                float mx = (first.X + last.X) * 0.5f;
                float my = (first.Y + last.Y) * 0.5f;
                dx = pts[apex].X - mx;
                dy = pts[apex].Y - my;
            }

            float len = (float)Math.Sqrt(dx * dx + dy * dy);
            if (len <= 1e-6f) { dx = 1f; dy = 0f; return; }
            dx /= len;
            dy /= len;
        }
    }
}
