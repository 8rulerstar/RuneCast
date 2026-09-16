using System.Collections.Generic;
using UnityEngine;
using RuneCast.Core;
using RuneCast.Gesture;
using RuneCast.Meta;

namespace RuneCast.UI
{
    /// <summary>
    /// 룬 도형이 저절로 그려지는 연출. 두 군데서 쓴다 —
    /// **타이틀 화면**(무작위로 계속 바뀜)과 **튜토리얼**(따라 그릴 도형을 시연).
    ///
    /// **왜 만드나:** 이 게임의 정체는 캐릭터도 전투도 아니고
    /// "손으로 도형을 그려서 마법을 건다"는 것인데, 그건 정지 화면에 안 담긴다.
    /// 글로 "꺾쇠를 그리세요"라고 쓰는 것보다 한 번 그려 보이는 게 훨씬 빠르다.
    ///
    /// 실제 템플릿을 실제 궤적 렌더러와 같은 방식(굵고 옅은 글로우 + 가늘고 밝은 심)으로
    /// 그린다. 따로 만든 애니메이션이 아니라 **게임이 실제로 하는 일**이라,
    /// 룬을 추가하면 여기에도 저절로 나온다.
    /// </summary>
    public class RuneTracer : MonoBehaviour
    {
        /// <summary>타이틀 모드일 때만 지정한다. null이면 항상 그린다(튜토리얼 쪽).</summary>
        public GameFlow flow;

        /// <summary>고정할 도형. None이면 활성 룬 중에서 무작위로 돌린다.</summary>
        public RuneType fixedType = RuneType.None;

        /// <summary>true면 화면비를 보고 스스로 자리를 잡는다 (타이틀 배치).</summary>
        public bool autoPlace = true;

        /// <summary>autoPlace가 false일 때 밖에서 켜고 끈다.</summary>
        [System.NonSerialized] public bool visible = true;

        [Tooltip("한 획을 그리는 데 걸리는 시간")]
        public float drawTime = 1.1f;

        [Tooltip("다 그리고 머무는 시간")]
        public float holdTime = 1.0f;

        public float fadeTime = 0.5f;

        private LineRenderer _line;
        private LineRenderer _glow;

        private List<GPoint> _points;
        private Color _tint;
        private float _t;
        private Vector3 _center;
        private float _size;

        // 배경이 파란 바다가 되면서 **찬 색을 전부 뺐다** — 파랑·보라 선은
        // 물에 먹혀서 타이틀의 주인공이 안 보였다. 남는 건 뜨거운 색과 흰빛뿐이고,
        // 그게 바다 위에서는 정답이다: 노을·등대·불꽃은 다 바다의 보색이다.
        private static readonly Color[] Palette =
        {
            new Color(1f, 0.85f, 0.15f),
            new Color(1f, 0.45f, 0.1f),
            new Color(1f, 1f, 0.95f),
            new Color(1f, 0.35f, 0.55f),
        };

        private void Awake()
        {
            _line = Build("Core", 0.075f, 61);
            _glow = Build("Glow", 0.075f * 3.4f, 60);
            Next();
        }

        private LineRenderer Build(string name, float width, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);

            var lr = go.AddComponent<LineRenderer>();
            lr.material = PrimitiveSprites.SpriteMaterial;
            lr.useWorldSpace = true;
            lr.textureMode = LineTextureMode.Stretch;
            lr.numCapVertices = 6;
            lr.numCornerVertices = 6;
            lr.sortingOrder = order;
            lr.widthMultiplier = width;
            lr.positionCount = 0;
            lr.alignment = LineAlignment.View;

            // 붓끝으로 갈수록 굵어진다 — 궤적 렌더러와 같은 인상
            lr.widthCurve = new AnimationCurve(
                new Keyframe(0f, 0.4f), new Keyframe(0.6f, 0.85f), new Keyframe(1f, 1f));
            return lr;
        }

        /// <summary>다음 룬을 고른다. 활성 룬 중에서만 — 꺼둔 룬이 타이틀에 나오면 안 된다.</summary>
        private void Next()
        {
            var runes = GlyphTable.Runes;
            RuneType want = fixedType != RuneType.None ? fixedType : runes[Random.Range(0, runes.Length)];

            var pool = new List<RuneTemplate>();
            var all = RuneTemplateLibrary.Templates;
            for (int i = 0; i < all.Count; i++)
                if (all[i].Type == want) pool.Add(all[i]);

            if (pool.Count == 0) return;

            _points = pool[Random.Range(0, pool.Count)].Points;
            _tint = fixedType != RuneType.None ? Palette[0] : Palette[Random.Range(0, Palette.Length)];
            _t = 0f;

            if (autoPlace) Place();
        }

        /// <summary>자리를 직접 지정한다 (튜토리얼처럼 특정 지점에 시연할 때).</summary>
        public void SetPlacement(Vector3 center, float size)
        {
            autoPlace = false;
            _center = center;
            _size = size;
        }

        /// <summary>
        /// 지금 붓끝이 있는 자리. 손 모양 안내(TouchHint)가 이걸 따라간다.
        ///
        /// **그리는 중일 때만 뜻이 있다.** 다 그리고 머무는 동안이나 사라지는
        /// 동안에는 마지막 점에 멈춰 있으므로, 부르는 쪽이 <see cref="Drawing"/>을
        /// 같이 봐야 한다.
        /// </summary>
        public Vector3 TipWorld
        {
            get
            {
                if (_line == null || _line.positionCount == 0) return _center;
                return _line.GetPosition(_line.positionCount - 1);
            }
        }

        /// <summary>선이 자라고 있는 중인가. 손을 보일지 정하는 데 쓴다.</summary>
        public bool Drawing { get { return _points != null && _t < drawTime; } }

        /// <summary>
        /// 화면 왼쪽에 놓는다. 글씨·버튼은 오른쪽이라 겹치지 않는다.
        /// 화면비가 좁으면(4:3 등) 가운데로 옮긴다 — 좁은 화면에서 왼쪽에 두면
        /// 글씨와 붙어서 둘 다 안 읽힌다.
        /// </summary>
        private void Place()
        {
            Camera cam = Camera.main;
            if (cam == null) return;

            float h = cam.orthographicSize * 2f;
            float w = h * cam.aspect;

            bool wide = cam.aspect > 1.5f;
            _center = new Vector3(wide ? -w * 0.22f : 0f, wide ? 0f : h * 0.08f, 0f);

            // 화면 높이의 3분의 1. 처음엔 0.42로 뒀는데 별(제일 큰 도형)이 화면 높이의
            // 77%를 먹어서 글씨 쪽까지 밀고 들어왔다. 주인공이되 화면을 삼키면 안 된다.
            _size = Mathf.Min(h * 0.33f, w * 0.23f);
        }

        private void Update()
        {
            bool show = flow != null ? flow.State == GameState.Title : visible;
            if (!show)
            {
                if (_line.positionCount != 0) { _line.positionCount = 0; _glow.positionCount = 0; }
                return;
            }

            if (_points == null) { Next(); return; }

            // 타이틀은 timeScale과 무관해야 한다 — 일시정지 상태로 들어와도 멈추면 안 된다
            _t += Time.unscaledDeltaTime;

            float total = drawTime + holdTime + fadeTime;
            if (_t >= total) { Next(); return; }

            // 그려지는 중: 앞에서부터 점을 늘려 간다
            float drawn = Mathf.Clamp01(_t / drawTime);

            // 끝으로 갈수록 느려지게 — 사람이 도형을 마무리할 때의 속도에 가깝다
            drawn = 1f - (1f - drawn) * (1f - drawn);

            int n = Mathf.Max(2, Mathf.RoundToInt(drawn * (_points.Count - 1)) + 1);
            if (n != _line.positionCount)
            {
                _line.positionCount = n;
                _glow.positionCount = n;
            }

            for (int i = 0; i < n; i++)
            {
                Vector3 p = _center + new Vector3(_points[i].X, _points[i].Y, 0f) * _size;
                _line.SetPosition(i, p);
                _glow.SetPosition(i, p);
            }

            float alpha = 1f;
            if (_t > drawTime + holdTime)
                alpha = 1f - (_t - drawTime - holdTime) / fadeTime;

            // 다 그린 순간 살짝 밝아진다 — 시전이 성립한 느낌
            float flash = 0f;
            if (_t > drawTime && _t < drawTime + 0.25f)
                flash = 1f - (_t - drawTime) / 0.25f;

            Color c = Color.Lerp(_tint, Color.white, flash * 0.55f);
            _line.startColor = _line.endColor = new Color(c.r, c.g, c.b, alpha * 0.95f);
            _glow.startColor = _glow.endColor =
                new Color(_tint.r, _tint.g, _tint.b, alpha * (0.28f + 0.22f * flash));
        }
    }
}
