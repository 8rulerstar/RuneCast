using System.Collections.Generic;
using System.Text;

namespace RuneCast.Gesture
{
    /// <summary>
    /// 서로 다른 룬 타입의 템플릿이 점군 공간에서 얼마나 떨어져 있는지 잰다.
    ///
    /// 룬을 추가할 때 진짜 위험은 "새 룬이 안 나가는 것"이 아니라
    /// **기존 룬까지 같이 망가지는 것**이다. 비슷한 도형을 넣으면 둘 다 오락가락한다.
    /// 그걸 플레이하다가 눈치채는 대신 숫자로 먼저 본다.
    ///
    /// 읽는 법: 두 룬 타입 사이의 최소 거리가 RejectDistance보다 충분히 커야 한다.
    /// 거리가 그보다 작으면, 한쪽을 어설프게 그렸을 때 다른 쪽으로 판정될 수 있다.
    /// </summary>
    public static class TemplateSeparation
    {
        public struct Pair
        {
            public RuneType A;
            public RuneType B;
            public float MinDistance;
            public string WorstA;
            public string WorstB;
        }

        /// <summary>타입이 다른 템플릿 쌍 전부의 최소 거리를, 가까운 순으로.</summary>
        public static List<Pair> Analyze(IList<RuneTemplate> templates)
        {
            var best = new Dictionary<long, Pair>();

            for (int i = 0; i < templates.Count; i++)
            {
                for (int j = i + 1; j < templates.Count; j++)
                {
                    RuneTemplate a = templates[i];
                    RuneTemplate b = templates[j];
                    if (a.Type == b.Type) continue; // 같은 룬끼리 가까운 건 오히려 정상

                    float d = PointCloudRecognizer.Distance(a.Points, b.Points);

                    long key = Key(a.Type, b.Type);
                    Pair cur;
                    if (best.TryGetValue(key, out cur) && cur.MinDistance <= d) continue;

                    best[key] = new Pair
                    {
                        A = a.Type < b.Type ? a.Type : b.Type,
                        B = a.Type < b.Type ? b.Type : a.Type,
                        MinDistance = d,
                        WorstA = a.Type < b.Type ? a.Name : b.Name,
                        WorstB = a.Type < b.Type ? b.Name : a.Name,
                    };
                }
            }

            var list = new List<Pair>(best.Values);
            list.Sort((x, y) => x.MinDistance.CompareTo(y.MinDistance));
            return list;
        }

        private static List<Pair> _cached;
        private static IList<RuneTemplate> _cachedFor;
        private static int _cachedCount;

        /// <summary>
        /// 캐시된 분석. **화면에 띄울 때는 반드시 이쪽을 쓴다.**
        ///
        /// Analyze는 템플릿 쌍마다 점군 매칭을 돌린다 — 템플릿 37개면 쌍이 666개고,
        /// 쌍마다 시작점 4개 × 양방향 × 32×32 비교라 한 번에 수백만 연산이다.
        /// 이걸 진단 패널에서 매 프레임 부르고 있었다. OnGUI는 한 프레임에 두 번
        /// (Layout·Repaint) 도니까 실제로는 그 두 배였고, F1을 누르면 게임이 멎었다.
        ///
        /// 템플릿은 각인 모드로 룬을 등록할 때만 바뀌므로 개수로 판별하면 충분하다.
        /// </summary>
        public static List<Pair> AnalyzeCached(IList<RuneTemplate> templates)
        {
            if (_cached != null && ReferenceEquals(_cachedFor, templates) && _cachedCount == templates.Count)
                return _cached;

            _cached = Analyze(templates);
            _cachedFor = templates;
            _cachedCount = templates.Count;
            return _cached;
        }

        private static long Key(RuneType a, RuneType b)
        {
            int lo = (int)(a < b ? a : b);
            int hi = (int)(a < b ? b : a);
            return lo * 1000L + hi;
        }

        /// <summary>가장 위험한 쌍 몇 개를 사람이 읽을 형태로.</summary>
        public static string Summary(int topN = 6)
        {
            var pairs = Analyze(RuneTemplateLibrary.Templates);
            var sb = new StringBuilder();
            sb.Append("가장 헷갈리는 룬 쌍 (거부 임계값 ")
              .Append(PointCloudRecognizer.RejectDistance.ToString("F2"))
              .Append(")\n");

            for (int i = 0; i < pairs.Count && i < topN; i++)
            {
                var p = pairs[i];
                sb.Append(p.MinDistance.ToString("F3"))
                  .Append("  ").Append(p.A).Append(" ↔ ").Append(p.B)
                  .Append("   (").Append(p.WorstA).Append(" / ").Append(p.WorstB).Append(")\n");
            }
            return sb.ToString();
        }
    }
}
