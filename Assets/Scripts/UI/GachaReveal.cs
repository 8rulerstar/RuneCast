using System.Collections.Generic;
using UnityEngine;
using RuneCast.Core;
using RuneCast.Meta;

namespace RuneCast.UI
{
    /// <summary>
    /// 문양 소환 연출. 단차와 10연차를 같은 흐름으로 처리한다.
    ///
    /// **뽑기의 재미는 결과가 아니라 결과가 나오기 직전의 몇 초에 있다.**
    /// 결과를 즉시 글자로 띄우면 확률 표를 확인하는 작업이 되고, 두 번째부터는 아무 감흥이 없다.
    /// 그래서 세 박자로 나눈다: 모으고(Charge) → 터뜨리고(Flash) → 보여준다(Card).
    ///
    /// 등급은 **터지기 직전에** 색으로 새어 나온다. 처음부터 등급색을 쓰면 기대가 사라지고,
    /// 끝까지 숨기면 터지는 순간이 그냥 놀라움일 뿐 쌓인 게 없다.
    /// 높은 등급일수록 모으는 시간이 길다 — 기다림 자체가 신호가 된다.
    ///
    /// 10연차에서는 **묶음 안 최고 등급**으로 모으기를 정한다. 최고가 전설이면 오래 끌고,
    /// 그 긴장이 카드 열 장 전체에 걸린다. 평균으로 잡으면 전설이 섞여도 밋밋해진다.
    /// </summary>
    public class GachaReveal : MonoBehaviour
    {
        private enum Phase { Idle, Charge, Flash, Card }

        private Phase _phase = Phase.Idle;
        private float _t;
        private bool _soundedTell;

        private readonly List<GlyphData> _results = new List<GlyphData>();
        private GlyphRarity _best;
        private int _shownCards;   // 10연차에서 지금까지 뒤집힌 장수

        private const float FlashTime = 0.18f;
        private const float CardTime = 0.45f;

        /// <summary>10연차에서 카드 한 장씩 뒤집히는 간격.</summary>
        private const float CardStagger = 0.13f;

        public bool Busy { get { return _phase != Phase.Idle; } }
        public bool IsMulti { get { return _results.Count > 1; } }
        public List<GlyphData> Results { get { return _results; } }

        /// <summary>결과가 전부 드러나 조작을 받을 수 있는 상태.</summary>
        public bool ShowingCard
        {
            get
            {
                if (_phase != Phase.Card) return false;
                return IsMulti ? _t >= CardStagger * _results.Count + 0.25f : _t >= CardTime;
            }
        }

        private float ChargeTime
        {
            get
            {
                // 전설은 2.0초까지 끈다. 이 기다림이 "뭔가 온다"는 신호다.
                switch (_best)
                {
                    case GlyphRarity.Legendary: return 2.0f;
                    case GlyphRarity.Epic: return 1.5f;
                    case GlyphRarity.Rare: return 1.1f;
                    default: return 0.85f;
                }
            }
        }

        /// <summary>등급이 새어 나오기 시작하는 지점 (모으는 시간 대비 비율).</summary>
        private const float TellAt = 0.65f;

        public void Begin(GlyphData result)
        {
            _results.Clear();
            _results.Add(result);
            BeginSequence();
        }

        public void BeginMulti(List<GlyphData> results)
        {
            _results.Clear();
            _results.AddRange(results);
            BeginSequence();
        }

        /// <summary>
        /// 이 메서드 이름을 Start로 두면 안 된다 — Unity가 컴포넌트 시작 시 자동으로 불러서
        /// 게임을 켜자마자 빈 연출이 터진다.
        /// </summary>
        private void BeginSequence()
        {
            _best = GlyphRarity.Common;
            for (int i = 0; i < _results.Count; i++)
                if ((GlyphRarity)_results[i].rarity > _best) _best = (GlyphRarity)_results[i].rarity;

            _phase = Phase.Charge;
            _t = 0f;
            _shownCards = 0;
            _soundedTell = false;

            AudioManager.Play(Sfx.CastMeteor, 0.55f, 0.85f);
        }

        public void Dismiss()
        {
            _phase = Phase.Idle;
            _results.Clear();
        }

        private void Update()
        {
            if (_phase == Phase.Idle) return;

            _t += Time.unscaledDeltaTime;

            switch (_phase)
            {
                case Phase.Charge:
                {
                    float k = _t / ChargeTime;

                    // 등급이 새어 나오는 순간에 한 번 더 소리를 얹는다.
                    // 화면만 바뀌면 눈을 딴 데 두고 있을 때 놓친다.
                    if (!_soundedTell && k >= TellAt)
                    {
                        _soundedTell = true;
                        if (_best >= GlyphRarity.Rare)
                            AudioManager.Play(Sfx.GradeGreat, 0.5f + 0.15f * (int)_best, 1.1f);
                    }

                    if (_t >= ChargeTime) EnterFlash();
                    break;
                }

                case Phase.Flash:
                    if (_t >= FlashTime)
                    {
                        _phase = Phase.Card;
                        _t = 0f;
                    }
                    break;

                case Phase.Card:
                    if (IsMulti) TickCardSounds();
                    break;
            }
        }

        /// <summary>카드가 한 장 뒤집힐 때마다 소리를 낸다. 등급이 높으면 더 크게.</summary>
        private void TickCardSounds()
        {
            int want = Mathf.Min(_results.Count, Mathf.FloorToInt(_t / CardStagger) + 1);
            while (_shownCards < want)
            {
                var r = (GlyphRarity)_results[_shownCards].rarity;
                _shownCards++;

                if (r >= GlyphRarity.Epic)
                {
                    AudioManager.Play(Sfx.GradePerfect, 0.75f);
                    CameraShake.Shake(0.06f + 0.05f * (int)r, 0.2f);

                    // 좋은 카드가 뒤집히는 순간 화면 가운데서 마법 연기가 터진다.
                    // 전설이면 폭죽까지 — 소리·흔들림·그림이 같은 순간을 가리켜야
                    // "방금 그 카드"라는 게 몸으로 읽힌다.
                    ParticleFx.SpawnAtGui(Pfx.Poof,
                        new Vector2(UiScale.W * 0.5f, UiScale.H * 0.42f), 0.9f);
                    if (r >= GlyphRarity.Legendary)
                        ParticleFx.SpawnAtGui(Pfx.FireworkBig,
                            new Vector2(UiScale.W * 0.5f, UiScale.H * 0.3f), 0.8f);
                }
                else
                {
                    AudioManager.Play(Sfx.DrawBegin, 0.35f, r == GlyphRarity.Rare ? 1.15f : 0.95f);
                }
            }
        }

        private void EnterFlash()
        {
            _phase = Phase.Flash;
            _t = 0f;

            AudioManager.Play(_best >= GlyphRarity.Epic ? Sfx.GradePerfect : Sfx.GradeGreat,
                0.7f + 0.1f * (int)_best);

            CameraShake.Shake(0.06f + 0.06f * (int)_best, 0.3f);
            if (_best >= GlyphRarity.Epic) TimeControl.HitStop(0.05f, 0.06f);

            // 터지는 박자에 마법 연기. 단차는 카드가 한 장이라 카드별 연출
            // (TickCardSounds)이 안 돌므로 여기가 유일한 자리다.
            // 10연차도 여기서 한 번 — "열렸다"의 신호는 공통이다.
            ParticleFx.SpawnAtGui(Pfx.Poof, new Vector2(UiScale.W * 0.5f, UiScale.H * 0.45f),
                0.8f + 0.15f * (int)_best);
        }

        // ── 그리기 ──────────────────────────────────────────────

        public void Draw(GUIStyle bigCenter, GUIStyle midCenter, GUIStyle smallCenter)
        {
            if (_phase == Phase.Idle) return;

            float cx = UiScale.W * 0.5f;
            float cy = UiScale.H * 0.5f - 20f;

            // 연출 동안은 뒤를 더 어둡게. 시선이 가운데로 모여야 한다.
            GUI.color = new Color(0.02f, 0.02f, 0.03f, _phase == Phase.Charge ? 0.75f : 0.9f);
            GUI.DrawTexture(new Rect(0f, 0f, UiScale.W, UiScale.H), Texture2D.whiteTexture);
            GUI.color = Color.white;

            EnsureGlyphStyle(midCenter);

            Color rc = GlyphTable.RarityColor(_best);

            if (_phase == Phase.Charge) DrawCharge(cx, cy, rc, midCenter);
            else if (_phase == Phase.Flash) DrawFlash();
            else if (IsMulti) DrawCardGrid(cx, cy, midCenter, smallCenter);
            else DrawSingleCard(cx, cy, rc, bigCenter, midCenter, smallCenter);
        }

        private void DrawCharge(float cx, float cy, Color rc, GUIStyle midCenter)
        {
            float k = Mathf.Clamp01(_t / ChargeTime);

            // 등급색은 후반에만 섞인다 — 이게 "뭔가 다르다"를 알리는 유일한 단서다
            float tell = Mathf.Clamp01((k - TellAt) / (1f - TellAt));
            Color glow = Color.Lerp(new Color(0.75f, 0.8f, 0.9f), rc, tell);

            Texture2D circle = PrimitiveSprites.Circle(128).texture;

            // 바깥에서 안으로 조여드는 고리
            float ringR = Mathf.Lerp(260f, 70f, k * k);
            DrawRing(cx, cy, ringR, new Color(glow.r, glow.g, glow.b, 0.35f + 0.4f * k));

            // 가운데 빛덩이 — 커지면서 밝아진다
            float coreR = Mathf.Lerp(10f, 70f, k * k) * (1f + 0.06f * Mathf.Sin(_t * 30f));
            GUI.color = new Color(glow.r, glow.g, glow.b, 0.5f + 0.5f * k);
            GUI.DrawTexture(new Rect(cx - coreR, cy - coreR, coreR * 2f, coreR * 2f), circle);

            // 등급이 높을수록 광선이 많고 빨리 돈다
            int rays = 4 + (int)_best * 4;
            DrawRays(cx, cy, rays, 60f + 240f * k, _t * (90f + 120f * (int)_best),
                new Color(glow.r, glow.g, glow.b, 0.10f + 0.28f * tell));

            GUI.color = new Color(1f, 1f, 1f, 0.35f + 0.3f * Mathf.Sin(_t * 6f));
            GUI.Label(new Rect(0f, cy + 150f, UiScale.W, 26f), Loc.T("shop.summoning"), midCenter);
            GUI.color = Color.white;
        }

        private void DrawFlash()
        {
            float k = 1f - Mathf.Clamp01(_t / FlashTime);
            GUI.color = new Color(1f, 1f, 1f, k);
            GUI.DrawTexture(new Rect(0f, 0f, UiScale.W, UiScale.H), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        // ── 단차 카드 ───────────────────────────────────────────

        private void DrawSingleCard(float cx, float cy, Color rc,
            GUIStyle bigCenter, GUIStyle midCenter, GUIStyle smallCenter)
        {
            float k = Mathf.Clamp01(_t / CardTime);
            float scale = Pop(k);

            if (_best >= GlyphRarity.Epic)
                DrawRays(cx, cy, 8 + (int)_best * 4, 300f, _t * 40f, new Color(rc.r, rc.g, rc.b, 0.16f));

            // 단차도 세로 카드. 10연과 모양이 다르면 같은 뽑기라는 게 안 읽힌다.
            const float cw = 260f, ch = 320f;
            var card = new Rect(cx - cw * 0.5f * scale, cy - ch * 0.5f * scale, cw * scale, ch * scale);

            UiSkin.DrawPanel(card, Color.white);

            // 등급 띠를 위쪽에 굵게. 색만으로 등급이 읽혀야 글자를 안 읽어도 안다.
            GUI.color = rc;
            GUI.DrawTexture(new Rect(card.x + 8f, card.y + 8f, card.width - 16f, 6f * scale), Texture2D.whiteTexture);
            GUI.color = Color.white;

            if (k < 0.55f) return; // 카드가 자리를 잡은 뒤에 글자를 얹는다

            GUI.color = rc;
            GUI.Label(new Rect(card.x, card.y + 26f, card.width, 38f), GlyphTable.RarityName(_best), bigCenter);

            var target = (RuneCast.Gesture.RuneType)_results[0].targetRune;
            string mark = target == RuneCast.Gesture.RuneType.None ? "\u25c8" : GlyphTable.Glyph(target);

            GUI.color = new Color(0.20f, 0.18f, 0.16f);
            GUI.Label(new Rect(card.x, card.y + 78f, card.width, 90f), mark, _bigGlyphStyle);

            GUI.color = new Color(0.30f, 0.28f, 0.25f);
            GUI.Label(new Rect(card.x, card.y + 182f, card.width, 24f),
                target == RuneCast.Gesture.RuneType.None
                    ? Loc.T("rune.all") : GlyphTable.RuneName(target), midCenter);

            GUI.color = new Color(0.30f, 0.28f, 0.25f);
            GUI.Label(new Rect(card.x, card.y + 216f, card.width, 24f),
                GlyphTable.EffectName((GlyphEffect)_results[0].effect), midCenter);

            GUI.color = new Color(0.16f, 0.14f, 0.12f);
            GUI.Label(new Rect(card.x, card.y + 248f, card.width, 34f),
                GlyphTable.ValueText(_results[0]), bigCenter);

            GUI.color = Color.white;
        }

        // ── 10연차 카드 격자 ────────────────────────────────────

        /// <summary>
        /// 10연 결과.
        ///
        /// 예전엔 가로로 넓은 칸에 "화살  위력 +32%" 같은 문장을 채웠다. 그러니
        /// **카드가 아니라 표처럼 보였다** — 결과를 읽는 화면이지 뽑는 화면이 아니었다.
        ///
        /// 세로로 긴 카드로 바꾸고, 가운데에 **그 룬의 도형 글자를 크게** 넣는다.
        /// 뽑기 결과에서 제일 먼저 보고 싶은 건 등급과 "어디에 붙는 건지"고,
        /// 수치는 그 다음이다. 글로 읽는 것보다 도형이 훨씬 빨리 들어온다.
        ///
        /// 등장도 카드를 뒤집는 것처럼 가로로 펴지게 했다. 크기만 커지면
        /// 열 장이 동시에 부풀어 오르는 걸로 보인다.
        /// </summary>
        private void DrawCardGrid(float cx, float cy, GUIStyle midCenter, GUIStyle smallCenter)
        {
            const int cols = 5;
            const float cw = 118f, ch = 158f, gap = 12f;

            int rows = Mathf.CeilToInt(_results.Count / (float)cols);
            float totalW = cols * cw + (cols - 1) * gap;
            float totalH = rows * ch + (rows - 1) * gap;

            float x0 = cx - totalW * 0.5f;
            float y0 = cy - totalH * 0.5f - 10f;

            // 전설이 섞였으면 격자 뒤에서도 광선이 돈다
            if (_best >= GlyphRarity.Legendary)
                DrawRays(cx, cy, 16, 420f, _t * 25f,
                    new Color(GlyphTable.RarityColor(_best).r, GlyphTable.RarityColor(_best).g,
                        GlyphTable.RarityColor(_best).b, 0.10f));

            for (int i = 0; i < _results.Count; i++)
            {
                float appear = _t - i * CardStagger;
                if (appear <= 0f) continue;

                float k = Mathf.Clamp01(appear / 0.28f);

                // 뒤집히듯 가로로 펴진다. 세로는 바로 제 크기.
                float flip = Pop(k);

                var rarity = (GlyphRarity)_results[i].rarity;
                Color rc = GlyphTable.RarityColor(rarity);

                float bx = x0 + (i % cols) * (cw + gap);
                float by = y0 + (i / cols) * (ch + gap);

                var card = new Rect(bx + cw * (1f - flip) * 0.5f, by, cw * flip, ch);

                // 등급이 높으면 카드 뒤에 빛이 깔린다. 열 장 중 어느 게 좋은지
                // 글자를 읽기 전에 보인다.
                if (rarity >= GlyphRarity.Epic && k > 0.5f)
                {
                    // Circle이 아니라 Glow다. 꽉 찬 원을 반투명으로 깔면 가장자리가
                    // 딱 끊겨서 빛이 아니라 색종이를 붙여놓은 것처럼 보인다.
                    float gr = ch * 0.72f;
                    GUI.color = new Color(rc.r, rc.g, rc.b,
                        rarity >= GlyphRarity.Legendary ? 0.55f : 0.30f);
                    GUI.DrawTexture(new Rect(bx + cw * 0.5f - gr, by + ch * 0.5f - gr, gr * 2f, gr * 2f),
                        PrimitiveSprites.Glow(128).texture);
                    GUI.color = Color.white;
                }

                UiSkin.DrawPanel(card, Color.white);

                // 등급 띠
                GUI.color = rc;
                GUI.DrawTexture(new Rect(card.x + 6f, card.y + 6f, card.width - 12f, 5f), Texture2D.whiteTexture);
                GUI.color = Color.white;

                if (k < 0.6f) continue;

                GUI.color = rc;
                GUI.Label(new Rect(card.x, card.y + 14f, card.width, 22f),
                    GlyphTable.RarityName(rarity), smallCenter);

                // 가운데에 대상 룬의 도형 글자를 크게. 전체 적용이면 별표.
                var target = (RuneCast.Gesture.RuneType)_results[i].targetRune;
                string mark = target == RuneCast.Gesture.RuneType.None ? "\u25c8" : GlyphTable.Glyph(target);

                GUI.color = new Color(0.20f, 0.18f, 0.16f);
                GUI.Label(new Rect(card.x, card.y + 40f, card.width, 52f), mark, _glyphStyle);

                // 무슨 효과인지 + 수치. 대상은 위 도형이 이미 말했으므로 반복하지 않는다.
                GUI.color = new Color(0.30f, 0.28f, 0.25f);
                GUI.Label(new Rect(card.x + 3f, card.y + 96f, card.width - 6f, 20f),
                    GlyphTable.EffectName((GlyphEffect)_results[i].effect), smallCenter);

                GUI.color = new Color(0.16f, 0.14f, 0.12f);
                GUI.Label(new Rect(card.x + 3f, card.y + 118f, card.width - 6f, 24f),
                    GlyphTable.ValueText(_results[i]), midCenter);
                GUI.color = Color.white;
            }
        }

        private GUIStyle _glyphStyle;
        private GUIStyle _bigGlyphStyle;

        /// <summary>카드 가운데 도형 글자용. 크기가 고정이라 한 번만 만든다.</summary>
        private void EnsureGlyphStyle(GUIStyle basis)
        {
            if (_glyphStyle != null) return;
            _glyphStyle = new GUIStyle(basis) { fontSize = UiSkin.Text.Huge, alignment = TextAnchor.MiddleCenter };
            _bigGlyphStyle = new GUIStyle(basis) { fontSize = UiSkin.Text.Glyph, alignment = TextAnchor.MiddleCenter };
        }

        /// <summary>살짝 넘쳤다가 돌아오는 등장. 선형으로 커지면 튀어나온 느낌이 안 난다.</summary>
        private static float Pop(float k)
        {
            if (k >= 1f) return 1f;
            return Mathf.Max(0.05f, 1f + 0.18f * Mathf.Sin(k * Mathf.PI) - 0.35f * (1f - k));
        }

        // ── 도형 헬퍼 ───────────────────────────────────────────

        /// <summary>
        /// 고리 하나. 두께를 인자로 받지 않는 이유: PrimitiveSprites.Ring은 두께마다
        /// 텍스처를 새로 만들어 캐시한다. 매 프레임 바뀌는 값을 넘기면 캐시가 무한히 늘어난다.
        /// 고정 두께 하나를 크기만 바꿔 쓴다 — 커질수록 테두리도 같이 두꺼워지는데,
        /// 조여드는 연출에서는 오히려 그쪽이 자연스럽다.
        /// </summary>
        private static Texture2D _ringTex;

        private static void DrawRing(float cx, float cy, float radius, Color color)
        {
            // Ring(128, 0.1f)은 "ri128_" + 두께로 키를 만들어 찾는다 — 소환 연출이
            // 도는 동안 매 프레임 그 문자열이 새로 생긴다. 한 번만 잡아 둔다.
            if (_ringTex == null) _ringTex = PrimitiveSprites.Ring(128, 0.1f).texture;
            Texture2D ring = _ringTex;
            GUI.color = color;
            GUI.DrawTexture(new Rect(cx - radius, cy - radius, radius * 2f, radius * 2f), ring);
            GUI.color = Color.white;
        }

        /// <summary>중심에서 뻗는 광선. IMGUI에는 회전이 없어 행렬을 직접 돌린다.</summary>
        private static void DrawRays(float cx, float cy, int count, float length, float spin, Color color)
        {
            Matrix4x4 saved = GUI.matrix;
            GUI.color = color;

            for (int i = 0; i < count; i++)
            {
                float angle = spin + i * (360f / count);
                GUIUtility.RotateAroundPivot(angle, new Vector2(cx, cy));
                GUI.DrawTexture(new Rect(cx - 5f, cy, 10f, length), Texture2D.whiteTexture);
                GUI.matrix = saved;
            }

            GUI.color = Color.white;
        }
    }
}
