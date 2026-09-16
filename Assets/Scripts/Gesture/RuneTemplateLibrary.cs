using System;
using System.Collections.Generic;

namespace RuneCast.Gesture
{
    /// <summary>
    /// 기본 룬 템플릿을 코드로 생성한다.
    ///
    /// .asset 파일 대신 절차 생성인 이유:
    ///  - 에셋 파일 없이 즉시 돌아간다 (임포트/참조 연결 불필요)
    ///  - 이상적인 형태라 사람이 그린 샘플보다 편향이 없다
    ///  - 도형을 바꾸고 싶으면 숫자 몇 개만 고치면 된다
    ///
    /// 커스텀 룬(Phase 5)은 여기에 런타임으로 Add하면 같은 엔진을 그대로 탄다.
    /// </summary>
    public static class RuneTemplateLibrary
    {
        private static List<RuneTemplate> _templates;

        public static List<RuneTemplate> Templates
        {
            get
            {
                if (_templates == null) _templates = BuildDefaults();
                return _templates;
            }
        }

        public static void Add(RuneTemplate t)
        {
            Templates.Add(t);
        }

        /// <summary>커스텀 룬을 모두 버리고 기본 템플릿만 남긴다.</summary>
        public static void Reset()
        {
            _templates = BuildDefaults();
        }

        /// <summary>이름으로 템플릿 찾기. 인식 결과를 화면에 그려 보여줄 때 쓴다.</summary>
        public static RuneTemplate Find(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;

            var list = Templates;
            for (int i = 0; i < list.Count; i++)
                if (list[i].Name == name) return list[i];
            return null;
        }

        public static int CountOf(RuneType type)
        {
            int n = 0;
            var list = Templates;
            for (int i = 0; i < list.Count; i++)
                if (list[i].Type == type) n++;
            return n;
        }

        private static List<RuneTemplate> BuildDefaults()
        {
            var list = new List<RuneTemplate>();

            // ── 원 = 힐 ──────────────────────────────────────────
            // 시계/반시계 둘 다 등록. 그리는 방향까지 맞추라고 요구할 이유가 없다.
            list.Add(new RuneTemplate("circle_ccw", RuneType.Heal, Circle(false)));
            list.Add(new RuneTemplate("circle_cw", RuneType.Heal, Circle(true)));

            // ── 삼각형 = 쉴드 ────────────────────────────────────
            // 원래 네모였는데 삼각형으로 바꿨다. 네모는 점 대부분이 변 위에 있어
            // 원(힐)과 가깝고, 다른 곳은 모서리 4곳뿐이다. 조금만 둥글게 그리면
            // 원 템플릿이 이겨서, 쉴드를 그렸는데 힐이 나갔다.
            //
            // 실측(합성 손그림, 보통 정도로 흔든 입력):
            //   네모   84% / 53% / 27.5%   ← 둥근 모서리 변형까지 넣고도 이 수준
            //   삼각형 100% / 99.5% / 77%
            //
            // 삼각형은 변이 3개뿐이라 원과의 편차가 훨씬 커서 구조적으로 안 섞인다.
            list.Add(new RuneTemplate("tri_up_cw", RuneType.Shield, Polygon(3, Math.PI / 2, true)));
            list.Add(new RuneTemplate("tri_up_ccw", RuneType.Shield, Polygon(3, Math.PI / 2, false)));

            // 사람이 삼각형을 정확히 세워 그리지 않으므로 기울어진 변형도 등록
            list.Add(new RuneTemplate("tri_tilt_a", RuneType.Shield, Polygon(3, Math.PI / 2 + 0.3, true)));
            list.Add(new RuneTemplate("tri_tilt_b", RuneType.Shield, Polygon(3, Math.PI / 2 - 0.3, false)));

            // 아래로 뾰족한 방패 모양도 자연스러운 그리기 방식이다
            list.Add(new RuneTemplate("tri_down_cw", RuneType.Shield, Polygon(3, -Math.PI / 2, true)));
            list.Add(new RuneTemplate("tri_down_ccw", RuneType.Shield, Polygon(3, -Math.PI / 2, false)));

            // ── 꺾쇠 > = 화살 ────────────────────────────────────
            // 회전 정규화를 켜는 대신 8방향 변형을 등록한다.
            // 지표각(indicative angle) 정규화는 꺾쇠처럼 좌우 비대칭이 약한 도형에서
            // 불안정해서, 변형을 늘리는 쪽이 예측 가능하다. 매칭 비용은 무시할 수준.
            for (int k = 0; k < 8; k++)
            {
                float rad = (float)(k * Math.PI / 4.0);
                list.Add(new RuneTemplate("chevron_" + (k * 45), RuneType.Arrow,
                    StrokeMath.Rotate(Chevron(), rad)));
            }

            // ── 별 = 별똥별 ──────────────────────────────────────
            // 한 붓 그리기 오각별(펜타그램). 이게 $P를 쓰는 이유를 보여주는 케이스 —
            // 규칙 기반이었으면 이 도형 하나 때문에 판별 코드를 새로 짜야 했다.
            list.Add(new RuneTemplate("star", RuneType.Meteor, Star(false)));
            list.Add(new RuneTemplate("star_rev", RuneType.Meteor, Star(true)));

            // ── 직선 = 가르기 : 현재 비활성 ──────────────────────
            //
            // 성능이 너무 좋아서 뺐다. 제일 싸고(12), 제일 빨리 그려지고,
            // 그은 선 위의 적을 전부 베어서 다른 룬을 쓸 이유가 없어졌다.
            // 도형이 단순할수록 등급도 잘 나오니 이중으로 유리했다.
            //
            // 다시 켤 때는 비용을 올리는 것만으로는 부족하다 —
            // "그리기 쉬움 = 강함"이 구조적 문제라, 사거리나 대상 수에 상한을 둬야 한다.
            // SlashWave.cs와 RuneType.Slash는 그대로 두었으니 이 블록만 되살리면 된다.
            //
            // for (int k = 0; k < 6; k++)
            // {
            //     float rad = (float)(k * Math.PI / 6.0);
            //     list.Add(new RuneTemplate("line_" + (k * 30), RuneType.Slash,
            //         StrokeMath.Rotate(Line(), rad)));
            // }

            // ── 지그재그 = 연쇄 번개 ─────────────────────────────
            // 급반전이 두 번 들어가는 건 이것뿐이다.
            //
            // 변형을 넉넉히 두는 이유: 꺾임이 많은 도형이라 같은 Z를 그려도 편차가 크고,
            // 템플릿 2개만 두면 조금만 흔들려도 거부 임계값을 넘어버린다.
            // 실측에서 보통 정도로 그린 Z의 28%가 "인식 실패"로 튕겼다.
            foreach (int deg in new[] { 0, 20, -20, 90, -90 })
            {
                float rad = (float)(deg * Math.PI / 180.0);
                list.Add(new RuneTemplate("zigzag_" + deg, RuneType.Chain,
                    StrokeMath.Rotate(Zigzag(false), rad)));
                list.Add(new RuneTemplate("zigzag_" + deg + "_rev", RuneType.Chain,
                    StrokeMath.Rotate(Zigzag(true), rad)));
            }

            // ── 나선 = 소용돌이 ──────────────────────────────────
            // 원과 헷갈릴 것 같지만 실제로는 잘 갈린다 —
            // 원은 점이 전부 테두리에 있고, 나선은 안쪽까지 채운다.
            list.Add(new RuneTemplate("spiral_ccw", RuneType.Vortex, Spiral(false)));
            list.Add(new RuneTemplate("spiral_cw", RuneType.Vortex, Spiral(true)));

            // ── 무한대 ∞ = 고양 ─────────────────────────────────
            // 원(힐)과 나선(소용돌이) 사이에 빈 자리가 있었다. 고리가 둘이라
            // 점이 좌우 두 덩어리로 갈리는데, 기존 도형 중엔 그런 게 없다.
            //
            // 실측(합성 손그림, 보통 정도로 흔든 입력):
            //   ±20° 기울여 그린 입력에서 자기 인식률 100% / 9종 전체 96.3%
            //
            // **가로 방향만, 대신 기울기 변형을 둔다.** 90°씩 도는 변형을 넣었다가 뺐다 —
            // ∞를 세로로 그리는 사람까지 받으면 낙서 오발동이 8.3% → 11.2%로 뛰는데,
            // HUD에 ∞로 표기하는 이상 세로로 그릴 이유가 없어서 얻는 게 없었다.
            //
            // 반대로 기울기 변형(±20°)은 반드시 필요하다. 이것 없이 방향만 4개 두면
            // 조금 기울여 그린 ∞의 15%가 통째로 인식 실패한다 —
            // 회전 변형은 **자세**를 늘릴 뿐 **손떨림 여유**를 만들지 못한다.
            foreach (int deg in new[] { 0, 20, -20 })
            {
                list.Add(new RuneTemplate("infinity_" + deg, RuneType.Empower,
                    StrokeMath.Rotate(Infinity(), (float)(deg * Math.PI / 180.0))));
            }

            // ── 하트 ♡ = 소생 ───────────────────────────────────
            // 닫힌 곡선이라 원(힐)과 섞일 줄 알았는데 실측은 반대였다 —
            // 위쪽 홈과 아래쪽 꼭짓점 두 군데가 원에는 절대 없는 특징이라
            // 오히려 삼각형(쉴드)과 조금 겹친다(4%). 그쪽이 더 가깝다.
            //
            // 실측: 자기 인식률 99.2% / 전체 96.3%
            //
            // 회전 변형을 안 두는 이유: 하트를 뒤집어 그리는 사람은 없다.
            // 방향까지 등록하면 템플릿만 늘고 다른 룬과의 여유만 깎인다.
            list.Add(new RuneTemplate("heart", RuneType.Revive, Heart()));
            list.Add(new RuneTemplate("heart_rev", RuneType.Revive, Heart(true)));

            // ── 깃발 ᛝ = 봉화 ───────────────────────────────────
            // 고리 + 자루. 자루가 붙어 있어서 원·나선 어느 쪽과도 안 겹친다.
            // 자루 끝이 곧 불이 붙는 자리라, 어디에 깔리는지도 손으로 정해진다.
            //
            // 실측: 세워 그린 입력 ±20° 기울기에서 자기 인식률 100% / 9종 전체 96.6%.
            //
            // **회전 변형을 안 둔다.** 처음엔 다른 룬처럼 90°씩 4방향을 넣었는데,
            // 그게 낙서 오발동의 주범이었다 — 무작위로 그은 획 4000개 중 8.4%가 봉화로 갔다.
            // 고리 + 꼬리는 손이 아무렇게나 지나간 궤적과 제일 닮은 모양이라,
            // 방향을 늘릴수록 그물만 넓어지고 얻는 건 없었다.
            // 세운 것만 남기니 오발동이 13.9% → 8.2%로 떨어졌고 인식률은 100% 그대로였다.
            // (깃대를 눕혀 그리는 사람은 없으므로 잃는 것도 없다.)
            list.Add(new RuneTemplate("banner_ccw", RuneType.Pyre, Banner(false)));
            list.Add(new RuneTemplate("banner_cw", RuneType.Pyre, Banner(true)));

            // ── 탈락한 후보 ─────────────────────────────────────
            // 넣었다가 실측하고 버린 도형들. 다시 시도하지 않기 위해 남겨 둔다.
            //
            //   아치 ⌒   자기 90.8% / 전체 94.3%  — 꺾쇠(화살)와 서로 6~8% 오염.
            //                                      둥글게 그린 꺾쇠가 곧 아치라 손으로 구분이 안 된다.
            //   나비 ⋈   자기 73.0% / 전체 92.5%  — 23%가 아예 인식 실패.
            //                                      교차선이 리샘플되면 지그재그 되돌기와 같아진다.
            //   물결 ∿   자기 87.2% / 전체 95.7%  — 나선(소용돌이)으로 5% 샌다.
            //
            // 공통점: **셋 다 기존 도형을 "부드럽게 하거나 뒤집은" 것**이다.
            // 새 룬은 기존 것의 변형이 아니라 없던 특징을 가져와야 한다.
            //
            // 하트를 아래가 뾰족한 형태로 바꿔보기도 했다. 결과는 정반대였다 —
            // 뾰족한 꼭짓점이 삼각형(보호막)과 겹쳐서 둘 사이 거리가 0.129 → 0.077로
            // 무너지고 인식률도 96.7% → 87.0%가 됐다. 둥근 쪽을 유지한다.

            return list;
        }


        /// <summary>고리 + 자루. 자루부터 올라가서 위에서 한 바퀴 돈다.</summary>
        private static List<GPoint> Banner(bool cw)
        {
            var pts = new List<GPoint>(96);
            for (int i = 0; i < 24; i++)
                pts.Add(new GPoint(0f, -1f + 1f * i / 24f));

            const int loop = 72;
            for (int i = 0; i <= loop; i++)
            {
                double a = -Math.PI / 2.0 + (cw ? -1.0 : 1.0) * 2.0 * Math.PI * i / loop;
                pts.Add(new GPoint((float)(Math.Cos(a) * 0.55), (float)(0.55 + Math.Sin(a) * 0.55)));
            }
            return StrokeMath.Resample(pts, Samples);
        }

        /// <summary>제로노 렘니스케이트 — 가로 8자.</summary>
        private static List<GPoint> Infinity()
        {
            const int steps = 96;
            var pts = new List<GPoint>(steps);
            for (int i = 0; i < steps; i++)
            {
                double t = 2.0 * Math.PI * i / (steps - 1);
                pts.Add(new GPoint((float)Math.Cos(t), (float)(Math.Sin(2.0 * t) * 0.5)));
            }
            return StrokeMath.Resample(pts, Samples);
        }


        private static List<GPoint> Heart(bool reverse = false)
        {
            const int steps = 96;
            var pts = new List<GPoint>(steps);
            for (int i = 0; i < steps; i++)
            {
                double t = 2.0 * Math.PI * i / (steps - 1);
                double x = 16 * Math.Pow(Math.Sin(t), 3);
                double y = 13 * Math.Cos(t) - 5 * Math.Cos(2 * t) - 2 * Math.Cos(3 * t) - Math.Cos(4 * t);
                pts.Add(new GPoint((float)(x / 16.0), (float)(y / 16.0)));
            }
            if (reverse) pts.Reverse();
            return StrokeMath.Resample(pts, Samples);
        }


        private const int Samples = 64; // 템플릿 원본 해상도 (이후 Normalize가 32로 줄임)

        private static List<GPoint> Circle(bool clockwise)
        {
            var pts = new List<GPoint>(Samples);
            for (int i = 0; i < Samples; i++)
            {
                double t = 2.0 * Math.PI * i / (Samples - 1);
                if (clockwise) t = -t;
                // 위쪽(12시)에서 시작 — 사람이 원을 그릴 때 가장 흔한 시작점
                pts.Add(new GPoint((float)Math.Sin(t), (float)Math.Cos(t)));
            }
            return pts;
        }

        private static List<GPoint> Chevron()
        {
            // 오른쪽을 가리키는 > 모양. 위 꼬리 → 꼭짓점 → 아래 꼬리.
            var corners = new[]
            {
                new GPoint(-0.7f, 1f),
                new GPoint(0.7f, 0f),
                new GPoint(-0.7f, -1f),
            };
            return SamplePolyline(corners, Samples);
        }

        /// <summary>닫힌 정다각형. rot0는 첫 꼭짓점의 각도(라디안).</summary>
        private static List<GPoint> Polygon(int sides, double rot0, bool clockwise)
        {
            var corners = new GPoint[sides + 1];
            for (int k = 0; k <= sides; k++)
            {
                double a = rot0 + 2.0 * Math.PI * k / sides;
                if (clockwise) a = rot0 - 2.0 * Math.PI * k / sides;
                corners[k] = new GPoint((float)Math.Cos(a), (float)Math.Sin(a));
            }
            return SamplePolyline(corners, Samples);
        }

        private static List<GPoint> Line()
        {
            // 가로 직선. 정규화가 크기를 맞추므로 길이는 아무 값이나 상관없다.
            return SamplePolyline(new[] { new GPoint(-1f, 0f), new GPoint(1f, 0f) }, Samples);
        }

        private static List<GPoint> Zigzag(bool reverse)
        {
            // Z자. 좌상 → 우상 → 좌하 → 우하.
            var corners = new[]
            {
                new GPoint(-1f, 1f),
                new GPoint(1f, 1f),
                new GPoint(-1f, -1f),
                new GPoint(1f, -1f),
            };
            if (reverse) Array.Reverse(corners);
            return SamplePolyline(corners, Samples);
        }

        private static List<GPoint> Spiral(bool clockwise)
        {
            // 안에서 밖으로 2.5바퀴. 반지름이 계속 커지는 게 원과의 차이를 만든다.
            const int steps = 96;
            const double turns = 2.5;

            var pts = new List<GPoint>(steps);
            for (int i = 0; i < steps; i++)
            {
                double t = i / (double)(steps - 1);
                double a = 2.0 * Math.PI * turns * t;
                if (clockwise) a = -a;

                double r = 0.12 + 0.88 * t;
                pts.Add(new GPoint((float)(Math.Cos(a) * r), (float)(Math.Sin(a) * r)));
            }
            return StrokeMath.Resample(pts, Samples);
        }

        private static List<GPoint> Star(bool reverse)
        {
            // 펜타그램 경로: 꼭짓점을 2칸씩 건너뛰며 잇는다
            var outer = new GPoint[5];
            for (int i = 0; i < 5; i++)
            {
                double a = -Math.PI / 2.0 + 2.0 * Math.PI * i / 5.0; // 12시부터
                outer[i] = new GPoint((float)Math.Cos(a), (float)Math.Sin(a));
            }

            var path = new List<GPoint>(6);
            int idx = 0;
            for (int i = 0; i < 6; i++)
            {
                path.Add(outer[idx % 5]);
                idx += 2;
            }

            if (reverse) path.Reverse();
            return SamplePolyline(path.ToArray(), Samples);
        }

        /// <summary>꼭짓점 목록을 등간격 n개 점으로 채운다.</summary>
        private static List<GPoint> SamplePolyline(GPoint[] corners, int n)
        {
            var dense = new List<GPoint>();
            for (int i = 1; i < corners.Length; i++)
            {
                const int per = 24;
                for (int s = 0; s < per; s++)
                {
                    float t = s / (float)per;
                    dense.Add(new GPoint(
                        corners[i - 1].X + t * (corners[i].X - corners[i - 1].X),
                        corners[i - 1].Y + t * (corners[i].Y - corners[i - 1].Y)));
                }
            }
            dense.Add(corners[corners.Length - 1]);
            return StrokeMath.Resample(dense, n);
        }
    }
}
