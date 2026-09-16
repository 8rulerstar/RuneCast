using System.Collections.Generic;
using UnityEngine;
using RuneCast.Core;

namespace RuneCast.Gesture
{
    /// <summary>
    /// 그리는 중인 궤적을 화면에 표시한다.
    /// 손이 지금 어디까지 그렸는지 안 보이면 도형을 맞출 수가 없으므로,
    /// 이건 디버그 기능이 아니라 게임플레이 필수 요소다.
    ///
    /// 선을 두 겹으로 그린다 — 뒤에 굵고 옅은 글로우, 앞에 가늘고 밝은 심.
    /// 한 겹이면 어두운 배경에서 선이 납작해 보이고, 무엇보다 이 게임에서
    /// 플레이어가 가장 오래 쳐다보는 물건이라 여기에 품을 들이는 값이 크다.
    /// </summary>
    [RequireComponent(typeof(LineRenderer))]
    public class TraceRenderer : MonoBehaviour
    {
        public Color drawColor = new Color(0.6f, 0.88f, 1f, 0.95f);
        public Color glowColor = new Color(0.35f, 0.55f, 1f, 0.4f);

        public float width = 0.085f;
        public float glowWidthScale = 3.2f;

        [Tooltip("판정 후 궤적이 사라지기까지의 시간(초). 실시간 기준.")]
        public float fadeDuration = 0.35f;

        [Tooltip("잉크가 바닥날 때의 색. 남은 양에 따라 drawColor에서 이쪽으로 옮겨간다.")]
        public Color emptyColor = new Color(1f, 0.45f, 0.35f, 0.95f);

        private LineRenderer _line;
        private LineRenderer _glow;
        private Camera _cam;
        private TraceCapture _capture;
        private float _fadeTimer = -1f;

        private void Awake()
        {
            _line = GetComponent<LineRenderer>();
            Setup(_line, width, 100);

            var glowGo = new GameObject("Glow");
            glowGo.transform.SetParent(transform, false);
            _glow = glowGo.AddComponent<LineRenderer>();
            Setup(_glow, width * glowWidthScale, 99);

            // 시작점은 가늘고 붓끝으로 갈수록 굵어지게 — 어디를 그리는 중인지가 또렷해진다
            var curve = new AnimationCurve(
                new Keyframe(0f, 0.35f),
                new Keyframe(0.5f, 0.8f),
                new Keyframe(1f, 1f));
            _line.widthCurve = curve;
            _glow.widthCurve = curve;

            SetAlpha(1f);
        }

        private static void Setup(LineRenderer lr, float w, int order)
        {
            lr.material = PrimitiveSprites.SpriteMaterial;
            lr.useWorldSpace = true;
            lr.textureMode = LineTextureMode.Stretch;
            lr.numCapVertices = 6;
            lr.numCornerVertices = 6;
            lr.sortingOrder = order;
            lr.widthMultiplier = w;
            lr.positionCount = 0;
            lr.alignment = LineAlignment.View;
        }

        private void Start()
        {
            _cam = Camera.main;
        }

        public void Bind(TraceCapture capture)
        {
            _capture = capture;
            capture.StrokeBegan += OnBegan;
            capture.StrokeUpdated += OnUpdated;
            capture.StrokeCompleted += OnCompleted;
            capture.StrokeDiscarded += Clear;
        }

        private void OnBegan()
        {
            _morphT = -1f;
            _fadeTimer = -1f;
            _line.positionCount = 0;
            _glow.positionCount = 0;
            SetAlpha(1f);
        }

        /// <summary>
        /// 새로 들어온 점만 이어 붙인다.
        ///
        /// 예전에는 점이 하나 늘 때마다 전체를 다시 썼다. 잉크 한도가 커진 후반에는
        /// 한 획이 270점까지 가는데, 그러면 획 하나를 긋는 동안 SetPosition이
        /// 270×270×2번 불린다. positionCount를 늘려도 기존 값은 그대로 남으므로
        /// 꼬리만 채우면 된다.
        /// </summary>
        private void OnUpdated(List<Vector2> screenPoints)
        {
            if (_cam == null) _cam = Camera.main;
            if (_cam == null) return;

            int have = _line.positionCount;
            int want = screenPoints.Count;

            // 잉크가 떨어지는 순간 마지막 점이 잘린 위치로 교체될 수 있다.
            // 개수가 그대로여도 꼬리 한 점은 다시 써야 한다.
            int from = want > have ? have : Mathf.Max(0, want - 1);

            if (want != have)
            {
                _line.positionCount = want;
                _glow.positionCount = want;
            }

            float depth = Mathf.Abs(_cam.transform.position.z);
            for (int i = from; i < want; i++)
            {
                Vector3 w = _cam.ScreenToWorldPoint(new Vector3(screenPoints[i].x, screenPoints[i].y, depth));
                w.z = 0f;
                _line.SetPosition(i, w);
                _glow.SetPosition(i, w);
            }
        }

        private void OnCompleted(List<Vector2> screenPoints)
        {
            _fadeTimer = fadeDuration;
        }

        // ── 시전 성공 시 "이상적인 도형으로 정돈되는" 연출 ────────────────
        //
        // 이 게임에서 플레이어가 가장 오래 쳐다보는 물건은 자기가 그은 선이다.
        // 그런데 판정이 끝나면 그 선은 그냥 흐려질 뿐이라, **무엇으로 인식됐는지**가
        // 화면 가운데 글자로만 전달됐다. 손끝을 보고 있던 시선에서 먼 자리다.
        //
        // 그은 선을 인식된 템플릿 모양으로 잠깐 정돈해서 보여주면
        // "내 낙서가 룬이 되었다"가 그 자리에서 읽히고, 동시에 다음에 어떻게 그려야
        // 더 잘 나오는지도 같이 가르친다 — 등급 글자보다 훨씬 구체적인 피드백이다.
        private const float MorphDuration = 0.22f;

        private Vector3[] _morphFrom;
        private Vector3[] _morphTo;
        private int _morphCount;
        private float _morphT = -1f;
        private Color _gradeTint;

        public void BindCaster(RuneCast.Runes.RuneCaster caster)
        {
            caster.Cast += OnCast;
        }

        private void OnCast(RecognitionResult result, bool success, RuneCast.Runes.CastFail fail)
        {
            if (!success || !result.Recognized) return;

            RuneTemplate tpl = RuneTemplateLibrary.Find(result.TemplateName);
            if (tpl == null || tpl.Points.Count < 2) return;

            int n = _line.positionCount;
            if (n < 2) return;

            // 그은 선의 월드 바운딩박스. 정돈된 도형을 같은 자리·같은 크기로 겹쳐야
            // "내가 그린 것이 변한 것"으로 보인다. 다른 데서 튀어나오면 남의 도형이다.
            Vector3 lo = _line.GetPosition(0), hi = lo;
            for (int i = 1; i < n; i++)
            {
                Vector3 p = _line.GetPosition(i);
                lo = Vector3.Min(lo, p);
                hi = Vector3.Max(hi, p);
            }
            Vector3 center = (lo + hi) * 0.5f;
            float side = Mathf.Max(hi.x - lo.x, hi.y - lo.y);
            if (side <= 1e-3f) return;

            int m = tpl.Points.Count;

            // 템플릿은 무게중심 기준으로 정규화돼 있다. 무게중심이 아니라 **바운딩박스**를
            // 맞춰야 그은 선 위에 정확히 겹친다 — 나선처럼 한쪽으로 쏠린 도형에서 차이가 크다.
            float tlx = float.MaxValue, tly = float.MaxValue, thx = float.MinValue, thy = float.MinValue;
            for (int i = 0; i < m; i++)
            {
                GPoint p = tpl.Points[i];
                if (p.X < tlx) tlx = p.X;
                if (p.Y < tly) tly = p.Y;
                if (p.X > thx) thx = p.X;
                if (p.Y > thy) thy = p.Y;
            }
            float tSide = Mathf.Max(thx - tlx, thy - tly);
            if (tSide <= 1e-6f) return;

            float scale = side / tSide;
            float tcx = (tlx + thx) * 0.5f, tcy = (tly + thy) * 0.5f;

            // **점 개수는 그은 선 쪽에 맞춘다.** 반대로 템플릿(32점)에 맞추면
            // 시전하는 순간 선이 성기게 바뀌면서 툭 각져 보이고, 나선처럼 촘촘한 도형은
            // 정돈된 결과가 원본보다 오히려 조잡해진다. 부족한 쪽을 보간해야 한다.
            if (_morphTo == null || _morphTo.Length < n)
            {
                _morphTo = new Vector3[n];
                _morphFrom = new Vector3[n];
            }
            _morphCount = n;

            for (int i = 0; i < n; i++) _morphFrom[i] = _line.GetPosition(i);

            // 닫힌 도형은 어디서부터 그리기 시작했는지가 사람마다 다르다.
            // 진행도를 그냥 맞추면 원을 아래에서부터 그린 사람의 선이 정돈될 때
            // **고리를 따라 한 바퀴 돌아버린다.** 실제로 발동한 것과 무관한 움직임이라
            // 눈에는 그냥 오작동으로 보인다.
            //
            // 그래서 템플릿을 여러 시작 위치로 돌려보고 제일 덜 움직이는 것을 고른다.
            // 시전할 때 한 번 도는 계산이라 비용은 무시할 수준이다.
            int bestOffset = 0;
            float bestCost = float.MaxValue;

            for (int off = 0; off < m; off++)
            {
                float cost = 0f;

                // 전부 재지 않고 8점만 표본으로. 최적점만 고르면 되는 일이라 이걸로 충분하다.
                for (int s = 0; s < 8; s++)
                {
                    int i = s * (n - 1) / 7;
                    Vector3 t = TemplatePoint(tpl, i, n, m, off, center, tcx, tcy, scale);
                    cost += (t - _morphFrom[i]).sqrMagnitude;
                }

                if (cost < bestCost) { bestCost = cost; bestOffset = off; }
            }

            for (int i = 0; i < n; i++)
                _morphTo[i] = TemplatePoint(tpl, i, n, m, bestOffset, center, tcx, tcy, scale);

            _gradeTint = RuneCast.Core.GradeVisuals.ColorOf(RuneGrading.Of(result.Score));
            _morphT = 0f;
        }

        /// <summary>
        /// 그은 선의 i번째 점에 대응하는 템플릿 위치를 월드 좌표로.
        /// offset만큼 템플릿 위를 돌려서 읽는다 (닫힌 도형의 시작점 차이를 흡수).
        /// </summary>
        private static Vector3 TemplatePoint(RuneTemplate tpl, int i, int n, int m, int offset,
            Vector3 center, float tcx, float tcy, float scale)
        {
            float f = (i / (float)(n - 1)) * (m - 1) + offset;

            // 되감아서 읽는다. 열린 도형이면 offset이 0으로 뽑히므로 감싸지지 않는다.
            int j0 = ((int)f) % m;
            int j1 = (j0 + 1) % m;
            float k = f - (float)System.Math.Floor(f);

            float px = Mathf.Lerp(tpl.Points[j0].X, tpl.Points[j1].X, k);
            float py = Mathf.Lerp(tpl.Points[j0].Y, tpl.Points[j1].Y, k);

            // 화면 y와 월드 y는 같은 방향이고 템플릿도 같은 좌표계를 거쳐 왔다.
            return new Vector3(
                center.x + (px - tcx) * scale,
                center.y + (py - tcy) * scale, 0f);
        }

        public void Clear()
        {
            _morphT = -1f;
            _line.positionCount = 0;
            _glow.positionCount = 0;
            _fadeTimer = -1f;
        }

        private void Update()
        {
            if (_morphT >= 0f)
            {
                _morphT += Time.unscaledDeltaTime / MorphDuration;
                float k = Mathf.Clamp01(_morphT);

                // 뒤로 갈수록 느려지게. 선형이면 도형이 "밀려 들어간" 것처럼 보이고,
                // 감속이 붙어야 제자리를 찾아 "맞물린" 느낌이 난다.
                float e = 1f - (1f - k) * (1f - k) * (1f - k);

                for (int i = 0; i < _morphCount; i++)
                {
                    Vector3 p = Vector3.LerpUnclamped(_morphFrom[i], _morphTo[i], e);
                    _line.SetPosition(i, p);
                    _glow.SetPosition(i, p);
                }

                SetAlpha(1f);
                if (_morphT >= 1f) _morphT = -1f;

                // 정돈되는 동안은 사라지지 않는다
                return;
            }

            if (_fadeTimer < 0f) return;

            // 시전 직후엔 슬로우가 풀리는 중이라 unscaled를 쓴다
            _fadeTimer -= Time.unscaledDeltaTime;
            if (_fadeTimer <= 0f)
            {
                Clear();
                return;
            }
            SetAlpha(_fadeTimer / fadeDuration);
        }

        private void SetAlpha(float a)
        {
            // 잉크가 줄수록 선이 붉어진다. 숫자 게이지를 보려면 시선을 옮겨야 하는데,
            // 그리는 중에는 손끝을 보고 있으므로 선 자체가 알려주는 게 맞다.
            // 스킨은 매 프레임 읽어도 싸다(배열 훑기 한 번). 그리는 도중에 바뀔 일은
            // 없지만, 대장장이에서 고르고 바로 전투로 들어가는 흐름이라 캐시하면 안 늦게 반영된다.
            var skin = RuneCast.Meta.TraceSkins.Current;

            float used = _capture != null ? _capture.InkRatio : 0f;
            Color baseColor = Color.Lerp(skin.Core, emptyColor, used * used);

            // 정돈되는 동안은 등급색으로 물든다. 등급 글자와 색이 같아야
            // 화면 가운데를 안 봐도 얼마나 잘 그렸는지가 손끝에서 읽힌다.
            if (_morphT >= 0f)
                baseColor = Color.Lerp(baseColor, _gradeTint, Mathf.Clamp01(_morphT) * 0.85f);

            var c = baseColor;
            c.a *= a;
            _line.startColor = c;
            _line.endColor = c;

            // 굵기도 스킨을 탄다. 색만 바뀌면 고른 티가 잘 안 난다.
            _line.widthMultiplier = width * skin.WidthScale;
            _glow.widthMultiplier = width * glowWidthScale * skin.WidthScale;

            var g = skin.Glow;
            g.a *= a;
            _glow.startColor = g;
            _glow.endColor = g;
        }

        private void LateUpdate()
        {
            // 그리는 중에도 색이 계속 변해야 한다 (SetAlpha는 시작·페이드 때만 불린다)
            if (_capture != null && _capture.IsDrawing) SetAlpha(1f);
        }
    }
}
