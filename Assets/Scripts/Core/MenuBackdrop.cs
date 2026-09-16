using UnityEngine;

namespace RuneCast.Core
{
    /// <summary>
    /// 메뉴 화면 전용 배경 — **구름이 흘러가는 청록 바다.**
    ///
    /// 다섯 번째 판이다: 어두운 그라디언트("꺼진 화면") → 밤하늘(검고 심심) →
    /// 크림빛(너무 하얗다) → 색 그라디언트(탁하다) → 전투와 같은 초원(밋밋하다).
    /// 초원의 교훈은 "전투와 같으면 배경이 아니라 복사"라는 것 — 메뉴는 전투와
    /// **다른 곳**이어야 화면이 바뀌었다는 감각이 생긴다.
    ///
    /// 바다를 고른 이유: 같은 팩(Tiny Swords)에 바다 재료가 통째로 있었다.
    /// 청록 물 + 뭉게구름(그림자까지 그려져 있다) + **일렁이는 물바위**(16프레임
    /// 애니메이션). 전투의 초원과 화풍은 같고 색·움직임은 완전히 달라서,
    /// "같은 세계의 다른 장소"로 읽힌다. 스테이지 선택이 바다 위라는 건
    /// 지도를 내려다보는 감각과도 맞는다.
    ///
    /// 재료는 tools/slice_ui.py가 ~/Assets에서 복사해 넣는다.
    /// </summary>
    public class MenuBackdrop : MonoBehaviour
    {
        private struct Mote
        {
            public Transform T;
            public SpriteRenderer Sr;
            public float Speed;
            public float Drift;
            public float Phase;
            public float BaseAlpha;
        }

        private struct Twinkle
        {
            public SpriteRenderer Sr;
            public Color Tint;
            public float Phase;
            public float Rate;
            public float BaseAlpha;
        }

        private struct Cloud
        {
            public Transform T;
            public float Speed;
            public float HalfWidth;
        }

        private struct BobRock
        {
            public SpriteRenderer Sr;
            public Sprite[] Frames;
            public float Fps;
            public float Phase;
        }

        private Mote[] _motes;
        private Twinkle[] _glints;
        private Cloud[] _clouds;
        private BobRock[] _rocks;

        private SpriteRenderer _shoot;
        private float _shootT = -1f;     // 진행 시간. 음수면 대기 중
        private float _nextShoot;
        private Vector3 _shootFrom, _shootTo;

        private float _halfW, _halfH;

        public static MenuBackdrop Create(float worldWidth, float worldHeight, int moteCount = 20)
        {
            var go = new GameObject("MenuBackdrop");
            var bd = go.AddComponent<MenuBackdrop>();
            bd.Build(worldWidth, worldHeight, moteCount);
            return bd;
        }

        private void Build(float w, float h, int count)
        {
            _halfW = w * 0.5f;
            _halfH = h * 0.5f;

            BuildWater(w, h);
            BuildRocks();
            BuildClouds(w);
            BuildGlints();
            BuildMotes(count);

            // 비네트 — 가장자리를 눌러 시선을 가운데(글씨·룬)로.
            // 바다 위라 그늘색을 푸른 쪽으로: 따뜻한 갈색 그늘은 물과 안 섞인다.
            var vig = PrimitiveSprites.Spawn("Vignette", PrimitiveSprites.Vignette(256, 0.34f),
                new Color(0.01f, 0.05f, 0.07f, 0.3f), -910, transform);
            vig.transform.localPosition = new Vector3(0f, 0f, 0.85f);
            Fit(vig, w, h);

            // 금빛 유성 한 줄기. 미리 만들어 두고 숨긴다 — 몇 초에 한 번 나오는 것
            // 때문에 생성·파괴를 반복할 이유가 없다.
            _shoot = PrimitiveSprites.Spawn("Shoot", PrimitiveSprites.Glow(128),
                Color.clear, -915, transform);
            _nextShoot = Random.Range(2f, 5f);
        }

        private void BuildWater(float w, float h)
        {
            // **쨍한 남빛 → 터키옥 그라디언트.** 팩의 물색 타일(차분한 청록)을
            // 먼저 썼는데 눈에 띄게 물이 죽어 보였다 — 구름·바위의 흰 테두리는
            // 채도 높은 물에서도 그대로 서고, 배경의 주인공은 물색 자체다.
            var sr = PrimitiveSprites.Spawn("Water",
                PrimitiveSprites.VerticalGradient(new Color(0.0f, 0.42f, 0.92f),
                    new Color(0.05f, 0.78f, 0.8f), 256), Color.white, -960, transform);
            sr.transform.localPosition = new Vector3(0f, 0f, 1f);
            Fit(sr, w, h);
        }

        /// <summary>
        /// 물바위 — 가장자리에 흩는다. **이게 이 배경의 생명이다**: 16프레임짜리
        /// 일렁임이 있어서 화면이 정지화면으로 안 보인다. 가운데는 글씨 자리라 비운다.
        /// </summary>
        private void BuildRocks()
        {
            var frames = new Sprite[4][];
            for (int i = 0; i < 4; i++)
                frames[i] = SpriteSheet.LoadGrid("Sprites/Terrain/water_rock_" + (i + 1), 16, 1);

            _rocks = new BobRock[7];
            int made = 0;

            for (int i = 0; i < _rocks.Length; i++)
            {
                Sprite[] f = frames[i % 4];
                if (f == null || f.Length == 0) continue;

                var sr = PrimitiveSprites.Spawn("WaterRock", f[0], Color.white, -955, transform);

                // 가운데 55×65%를 비운다
                float x, y;
                do
                {
                    x = Random.Range(-_halfW * 0.95f, _halfW * 0.95f);
                    y = Random.Range(-_halfH * 0.95f, _halfH * 0.95f);
                } while (Mathf.Abs(x) < _halfW * 0.55f && Mathf.Abs(y) < _halfH * 0.65f);

                sr.transform.localPosition = new Vector3(x, y, 0.95f);
                sr.transform.localScale = Vector3.one * Random.Range(0.9f, 1.5f);
                sr.flipX = Random.value < 0.5f;

                _rocks[made++] = new BobRock
                {
                    Sr = sr,
                    Frames = f,
                    // 프레임 위상을 흩는다 — 일곱 개가 같은 박자로 출렁이면 기계다
                    Fps = Random.Range(6f, 9f),
                    Phase = Random.Range(0f, 16f),
                };
            }

            System.Array.Resize(ref _rocks, made);
        }

        /// <summary>
        /// 구름 — 바다 위를 천천히 흘러간다. 그림자가 스프라이트에 같이 그려져 있어서
        /// 따로 만들 게 없다. 큰 것 둘은 위아래 가장자리, 작은 것 하나는 지나가는 손님.
        /// </summary>
        private void BuildClouds(float w)
        {
            string[] names = { "cloud_1", "cloud_2", "cloud_3", "cloud_2" };
            _clouds = new Cloud[4];
            int made = 0;

            for (int i = 0; i < _clouds.Length; i++)
            {
                Sprite s = SpriteSheet.Single("Sprites/Terrain/" + names[i], 64f);
                if (s == null) continue;

                var sr = PrimitiveSprites.Spawn("Cloud", s, new Color(1f, 1f, 1f, 0.96f),
                    -940, transform);

                float scale = Random.Range(0.55f, 0.95f);
                sr.transform.localScale = Vector3.one * scale;

                // 세로는 가장자리 쪽, 가로는 아무 데나 — 어차피 흘러간다
                float y = Random.Range(_halfH * 0.35f, _halfH * 0.85f);
                if (i % 2 == 1) y = -y;
                sr.transform.localPosition = new Vector3(
                    Random.Range(-_halfW, _halfW), y, 0.9f);

                _clouds[made++] = new Cloud
                {
                    T = sr.transform,
                    // 왼쪽으로 흐른다. 구름마다 속도가 달라야 층이 생긴다.
                    Speed = Random.Range(0.12f, 0.3f),
                    HalfWidth = s.bounds.size.x * scale * 0.5f,
                };
            }

            System.Array.Resize(ref _clouds, made);
        }

        /// <summary>물비늘 — 수면에서 반짝이는 흰 점. 해가 물에 부서지는 것.</summary>
        private void BuildGlints()
        {
            const int n = 40;
            _glints = new Twinkle[n];

            for (int i = 0; i < n; i++)
            {
                var sr = PrimitiveSprites.Spawn("Glint", PrimitiveSprites.Circle(24),
                    Color.white, -948, transform);
                sr.transform.localScale = new Vector3(
                    Random.Range(0.05f, 0.12f), Random.Range(0.015f, 0.03f), 1f); // 납작하게 — 물비늘
                sr.transform.localPosition = new Vector3(
                    Random.Range(-_halfW, _halfW), Random.Range(-_halfH, _halfH), 0.95f);

                // 흰 비늘, 다섯에 하나는 옅은 금 — 해가 있는 쪽
                Color tint = (i % 5 == 0)
                    ? new Color(1f, 0.95f, 0.7f)
                    : new Color(0.92f, 1f, 1f);

                _glints[i] = new Twinkle
                {
                    Sr = sr,
                    Tint = tint,
                    Phase = Random.Range(0f, Mathf.PI * 2f),
                    Rate = Random.Range(0.6f, 1.8f),
                    BaseAlpha = Random.Range(0.25f, 0.55f),
                };
            }
        }

        private void BuildMotes(int count)
        {
            _motes = new Mote[count];
            for (int i = 0; i < count; i++)
            {
                var sr = PrimitiveSprites.Spawn("Spray", PrimitiveSprites.Circle(64),
                    Color.white, -930, transform);

                float size = Random.Range(0.03f, 0.09f);
                sr.transform.localScale = Vector3.one * size;
                sr.transform.localPosition = new Vector3(
                    Random.Range(-_halfW, _halfW), Random.Range(-_halfH, _halfH), 0.8f);

                // 바닷바람에 날리는 물보라 — 꽃가루의 바다판. 흰빛으로.
                float depth = Mathf.InverseLerp(0.03f, 0.09f, size);
                float alpha = Mathf.Lerp(0.1f, 0.28f, depth);
                sr.color = new Color(0.95f, 1f, 1f, alpha);

                _motes[i] = new Mote
                {
                    T = sr.transform,
                    Sr = sr,
                    Speed = Mathf.Lerp(0.05f, 0.18f, depth),
                    Drift = Random.Range(0.08f, 0.28f),
                    Phase = Random.Range(0f, Mathf.PI * 2f),
                    BaseAlpha = alpha,
                };
            }
        }

        private static void Fit(SpriteRenderer sr, float w, float h)
        {
            Vector2 size = sr.sprite.bounds.size;
            sr.transform.localScale = new Vector3(
                w / Mathf.Max(size.x, 1e-4f),
                h / Mathf.Max(size.y, 1e-4f), 1f);
        }

        public void SetVisible(bool on)
        {
            if (gameObject.activeSelf != on) gameObject.SetActive(on);
        }

        private void Update()
        {
            // 메뉴는 timeScale과 무관해야 한다 — 일시정지 중에 설정을 열어도 멈추면 안 된다
            float dt = Time.unscaledDeltaTime;
            float t = Time.unscaledTime;

            for (int i = 0; i < _motes.Length; i++)
            {
                Mote m = _motes[i];
                Vector3 p = m.T.localPosition;

                p.y += m.Speed * dt;
                p.x += Mathf.Sin(t * 0.35f + m.Phase) * m.Drift * dt;

                if (p.y > _halfH) { p.y = -_halfH; p.x = Random.Range(-_halfW, _halfW); }
                m.T.localPosition = p;

                var c = m.Sr.color;
                c.a = m.BaseAlpha * (0.7f + 0.3f * Mathf.Sin(t * 0.8f + m.Phase));
                m.Sr.color = c;
            }

            for (int i = 0; i < _glints.Length; i++)
            {
                Twinkle s = _glints[i];
                // sin을 제곱해 "대부분 어둡고 가끔 반짝"으로 만든다.
                float k = Mathf.Sin(t * s.Rate + s.Phase);
                float a = s.BaseAlpha * (0.15f + 0.85f * k * k);
                s.Sr.color = new Color(s.Tint.r, s.Tint.g, s.Tint.b, a);
            }

            for (int i = 0; i < _clouds.Length; i++)
            {
                Cloud c = _clouds[i];
                Vector3 p = c.T.localPosition;
                p.x -= c.Speed * dt;

                // 왼쪽으로 다 빠지면 오른쪽에서 다시 들어온다. 높이도 다시 뽑는다 —
                // 같은 자리로 돌아오면 몇 바퀴 만에 "도는 배경"이 들킨다.
                if (p.x < -_halfW - c.HalfWidth)
                {
                    p.x = _halfW + c.HalfWidth;
                    float y = Random.Range(_halfH * 0.3f, _halfH * 0.85f);
                    p.y = Random.value < 0.5f ? y : -y;
                }
                c.T.localPosition = p;
            }

            for (int i = 0; i < _rocks.Length; i++)
            {
                BobRock r = _rocks[i];
                int frame = (int)(t * r.Fps + r.Phase) % r.Frames.Length;
                if (r.Sr.sprite != r.Frames[frame]) r.Sr.sprite = r.Frames[frame];
            }

            UpdateShootingStar(dt);
        }

        /// <summary>
        /// 금빛 유성. 몇 초에 한 번, 위쪽을 사선으로 가로지른다.
        /// 화면을 계속 보게 만드는 건 평균 밝기가 아니라 "방금 뭐가 지나갔다"다.
        /// </summary>
        private void UpdateShootingStar(float dt)
        {
            const float dur = 0.9f;

            if (_shootT < 0f)
            {
                _nextShoot -= dt;
                if (_nextShoot > 0f) return;

                bool ltr = Random.value < 0.5f;
                float y = Random.Range(_halfH * 0.25f, _halfH * 0.85f);
                float x = _halfW * 1.1f;
                _shootFrom = new Vector3(ltr ? -x : x, y, 0.9f);
                _shootTo = new Vector3(ltr ? x : -x, y - _halfH * 0.45f, 0.9f);

                var d = _shootTo - _shootFrom;
                _shoot.transform.localRotation =
                    Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);

                float unit = _shoot.sprite.bounds.size.x;
                _shoot.transform.localScale = new Vector3(2.6f / unit, 0.14f / unit, 1f);

                _shootT = 0f;
                return;
            }

            _shootT += dt;
            float k = _shootT / dur;

            if (k >= 1f)
            {
                _shootT = -1f;
                _nextShoot = Random.Range(5f, 11f);
                _shoot.color = Color.clear;
                return;
            }

            _shoot.transform.localPosition = Vector3.Lerp(_shootFrom, _shootTo, k);

            float a = Mathf.Sin(k * Mathf.PI);
            _shoot.color = new Color(1f, 0.75f, 0.2f, 0.8f * a);
        }
    }
}
