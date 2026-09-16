using UnityEngine;

namespace RuneCast.Core
{
    /// <summary>
    /// 배경 — 초원 타일 + 바위 장식 + 떠다니는 꽃가루 + 비네트.
    ///
    /// 처음엔 보라색 그라디언트에 빛무리를 띄웠는데 몽환적이라 픽셀 기사들과 안 맞았다.
    /// 유닛이 세로로 늘어서서 부딪히는 배치라 탑다운 초원이 자연스럽다.
    ///
    /// 반복 타일을 쓰되 **격자가 눈에 띄지 않는 것**을 골랐다. 예전에 넣었던 체커 패턴은
    /// 규칙적인 직선이 손으로 그린 궤적과 섞여 보였다. 풀 텍스처는 무늬가 불규칙해서
    /// 반복돼도 선으로 읽히지 않는다.
    /// </summary>
    public class Backdrop : MonoBehaviour
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

        /// <summary>풀 타일 한 칸의 월드 크기. 캐릭터 키(약 1유닛)와의 비례로 정한 값.</summary>
        private const float TileWorldSize = 1.2f;

        /// <summary>유닛이 서는 세로 구간. 장식을 이 밖에만 놓아 전투를 가리지 않게 한다.</summary>
        private const float BattleLaneHalfHeight = 2.7f;

        private Mote[] _motes;
        private float _halfW;
        private float _halfH;

        public static Backdrop Create(float worldWidth, float worldHeight, int moteCount = 34)
        {
            var go = new GameObject("Backdrop");
            var bd = go.AddComponent<Backdrop>();
            bd.Build(worldWidth, worldHeight, moteCount);
            return bd;
        }

        private void Build(float w, float h, int count)
        {
            _halfW = w * 0.5f;
            _halfH = h * 0.5f;

            BuildGround(w, h);
            BuildRocks();
            BuildMotes(count);

            // 비네트를 따뜻한 어두운색으로. 완전한 검정은 초원 위에서 그을린 것처럼 보인다.
            var vig = PrimitiveSprites.Spawn("Vignette", PrimitiveSprites.Vignette(256, 0.34f),
                new Color(0.06f, 0.05f, 0.02f, 0.42f), -900, transform);
            vig.transform.localPosition = new Vector3(0f, 0f, 0.85f);
            Fit(vig, w, h);
        }

        private SpriteRenderer _ground;

        /// <summary>
        /// 장이 바뀌면 바닥도 바뀐다.
        ///
        /// **2장이 "밤의 군세"인데 배경이 1장과 같은 초원이었다.** 장이 넘어간 게
        /// 화면에서 안 읽히면 새 장을 만든 뜻이 절반은 사라진다.
        ///
        /// 같은 타일셋의 색 변주라(Tiny Swords의 Tilemap_color1/color5) 화풍이
        /// 어긋나지 않으면서 분위기만 달라진다. 2장은 차가운 청록이다.
        /// </summary>
        public void SetChapter(int chapter)
        {
            if (_ground == null) return;

            Sprite s = SpriteSheet.Tileable(chapter >= 2
                ? "Sprites/Terrain/grass_tile_ch2"
                : "Sprites/Terrain/grass_tile");
            if (s != null) _ground.sprite = s;

            _ground.color = TintOf(chapter);
        }

        /// <summary>
        /// 장별 바닥 색.
        ///
        /// **타일을 장마다 새로 구할 수는 없다.** Tiny Swords 타일셋에 색 변주가
        /// 다섯 벌 있지만 지금 반입한 건 두 장뿐이고, 나머지는 저장소 밖 라이브러리에
        /// 있다. 색을 곱하는 것만으로 일곱 장을 갈라 놓을 수 있으면 그게 싸다.
        ///
        /// **어둡게만 하지 않는다.** 장이 오를수록 한 단씩 어두워지게 하면
        /// 7장이 거의 검은 화면이 되어 유닛이 안 읽힌다. 대신 **색상을 돌린다** —
        /// 초원(중립) → 밤(청록) → 서리(푸름) → 안개(채도 없음) → 부패(누런 초록)
        /// → 봉인(보랏빛) → 심연(검붉음).
        ///
        /// 어두운 건 마지막 장 하나뿐이다(7장 0.60/0.42/0.46). 거기만 예외로 둔
        /// 이유는 "제일 깊은 곳"이 화면에서도 제일 어두워야 해서다. 실제로 찍어
        /// 확인했고 아군 갑옷과 체력바는 그 위에서도 읽힌다 — 더 내리면 묻힌다.
        ///
        /// **값은 눈으로 짐작하지 않았다.** 일곱 장을 실제 전투 화면에 적용해
        /// 나란히 놓고 골랐다. 처음 잡은 값은 4장이 1장과 구분이 안 됐고
        /// 7장은 가을빛으로 보였다 — 하나씩 보면 둘 다 그럴듯했다.
        /// </summary>
        public static Color TintOf(int chapter)
        {
            switch (chapter)
            {
                case 1:  return Color.white;
                case 2:  return new Color(0.86f, 0.92f, 1.00f);   // 밤
                case 3:  return new Color(0.70f, 0.86f, 1.00f);   // 서리
                // **안개는 초록을 죽여야 한다.** 회색으로 곱하기만 하면 바닥이
                // 어두워질 뿐 여전히 초원이다 — 일곱 장을 나란히 놓고 보니
                // 1장과 구분이 안 됐다. G를 R·B보다 더 깎아서 채도를 뺀다.
                case 4:  return new Color(0.92f, 0.80f, 0.90f);   // 안개
                case 5:  return new Color(0.88f, 0.95f, 0.72f);   // 부패
                case 6:  return new Color(0.86f, 0.80f, 0.98f);   // 봉인
                // 처음엔 0.82/0.74/0.76이었는데 화면에서 **가을빛**으로 읽혔다.
                // 마지막 장이 따뜻해 보이면 안 된다. 확실히 어둡게 내렸다.
                case 7:  return new Color(0.60f, 0.42f, 0.46f);   // 심연
                default: return Color.white;
            }
        }

        /// <summary>
        /// 장 카드(목록)에 쓸 강조색.
        ///
        /// **바닥 색을 그대로 못 쓴다.** `TintOf`는 초록 풀밭에 *곱하는* 값이라
        /// 크림색 카드 위에 곱하면 채도가 안 올라온다 — 실제로 6장(보랏빛)이
        /// 1장과 구분이 안 됐다. 곱하기는 어둡게만 할 수 있기 때문이다.
        ///
        /// 그래서 카드는 크림색에서 이 색으로 **섞는다**(lerp). 방향은 바닥 색과
        /// 같게 잡되 채도만 올렸다 — 목록에서 본 인상이 판에 들어가서도 이어진다.
        ///
        /// 여기 두는 이유는 `TintOf` 바로 옆이어야 **둘이 어긋나지 않기** 때문이다.
        /// 하나를 고치면 다른 하나도 보게 된다.
        /// </summary>
        public static Color CardAccentOf(int chapter)
        {
            switch (chapter)
            {
                case 1:  return new Color(0.86f, 0.93f, 0.70f);   // 초원
                case 2:  return new Color(0.62f, 0.72f, 0.95f);   // 밤
                case 3:  return new Color(0.66f, 0.88f, 0.98f);   // 서리
                case 4:  return new Color(0.80f, 0.80f, 0.84f);   // 안개
                case 5:  return new Color(0.78f, 0.90f, 0.45f);   // 부패
                case 6:  return new Color(0.72f, 0.60f, 0.92f);   // 봉인
                case 7:  return new Color(0.74f, 0.42f, 0.42f);   // 심연
                default: return Color.white;
            }
        }

        private void BuildGround(float w, float h)
        {
            Sprite grass = SpriteSheet.Tileable("Sprites/Terrain/grass_tile");
            if (grass != null)
            {
                var sr = PrimitiveSprites.Spawn("Ground", grass, Color.white, -1000, transform);
                _ground = sr;
                sr.drawMode = SpriteDrawMode.Tiled;

                // Tileable이 ppu = 텍스처 폭으로 만들어 스프라이트 한 장 = 1유닛이므로,
                // size를 타일 개수로 환산해서 넣는다.
                sr.size = new Vector2(w / TileWorldSize, h / TileWorldSize);
                sr.transform.localScale = Vector3.one * TileWorldSize;
                sr.transform.localPosition = new Vector3(0f, 0f, 1f);
            }
            else
            {
                // 타일을 못 읽어도 초록 바탕은 깔린다
                var fallback = PrimitiveSprites.Spawn("Ground", PrimitiveSprites.Square(8),
                    new Color(0.42f, 0.63f, 0.28f), -1000, transform);
                fallback.transform.localPosition = new Vector3(0f, 0f, 1f);
                Fit(fallback, w, h);
            }

            // 가운데를 살짝 밝혀 전투가 벌어지는 띠로 시선을 모은다
            var glow = PrimitiveSprites.Spawn("LaneLight", PrimitiveSprites.Circle(256),
                new Color(1f, 0.98f, 0.8f, 0.10f), -999, transform);
            glow.transform.localPosition = new Vector3(0f, 0f, 0.96f);
            Fit(glow, w * 1.1f, h * 0.9f);
        }

        private void BuildRocks()
        {
            // 전투 구간을 피해 위아래 가장자리에만. 장식이 유닛과 겹치면
            // 뭐가 적이고 뭐가 배경인지 순간적으로 헷갈린다.
            for (int i = 0; i < 9; i++)
            {
                Sprite rock = SpriteSheet.Single("Sprites/Terrain/rock_" + Random.Range(1, 5), 64f);
                if (rock == null) break;

                var sr = PrimitiveSprites.Spawn("Rock", rock, new Color(1f, 1f, 1f, 0.9f), -998, transform);

                float y = Random.Range(BattleLaneHalfHeight, _halfH);
                if (Random.value < 0.5f) y = -y;

                sr.transform.localPosition = new Vector3(Random.Range(-_halfW, _halfW), y, 0.95f);
                sr.transform.localScale = Vector3.one * Random.Range(0.7f, 1.2f);
                sr.flipX = Random.value < 0.5f;
            }
        }

        private void BuildMotes(int count)
        {
            _motes = new Mote[count];
            for (int i = 0; i < count; i++)
            {
                var sr = PrimitiveSprites.Spawn("Pollen", PrimitiveSprites.Circle(64), Color.white, -997, transform);

                float size = Random.Range(0.04f, 0.13f);
                sr.transform.localScale = Vector3.one * size;
                sr.transform.localPosition = new Vector3(
                    Random.Range(-_halfW, _halfW), Random.Range(-_halfH, _halfH), 0.9f);

                // 작을수록 흐리고 느리게 — 깊이감이 생긴다
                float depth = Mathf.InverseLerp(0.04f, 0.13f, size);
                float alpha = Mathf.Lerp(0.10f, 0.32f, depth);

                sr.color = new Color(1f, 0.98f, 0.75f, alpha); // 햇빛 속 꽃가루

                _motes[i] = new Mote
                {
                    T = sr.transform,
                    Sr = sr,
                    Speed = Mathf.Lerp(0.06f, 0.24f, depth),
                    Drift = Random.Range(0.08f, 0.3f),
                    Phase = Random.Range(0f, Mathf.PI * 2f),
                    BaseAlpha = alpha,
                };
            }
        }

        /// <summary>전투 밖에서는 끈다. 메뉴 뒤로 초원이 비치면 글씨가 안 읽힌다.</summary>
        public void SetVisible(bool on)
        {
            if (gameObject.activeSelf != on) gameObject.SetActive(on);
        }

        private static void Fit(SpriteRenderer sr, float width, float height)
        {
            Vector2 size = sr.sprite.bounds.size;
            sr.transform.localScale = new Vector3(
                width / Mathf.Max(size.x, 1e-4f),
                height / Mathf.Max(size.y, 1e-4f),
                1f);
        }

        private void Update()
        {
            // 시전 슬로우에 배경까지 느려지면 화면이 멎은 것처럼 보인다.
            // 배경은 항상 같은 속도로 흐르게 unscaled를 쓴다.
            float dt = Time.unscaledDeltaTime;
            float t = Time.unscaledTime;

            for (int i = 0; i < _motes.Length; i++)
            {
                Transform tr = _motes[i].T;
                if (tr == null) continue;

                Vector3 p = tr.localPosition;
                p.y += _motes[i].Speed * dt;
                p.x += Mathf.Sin(t * 0.4f + _motes[i].Phase) * _motes[i].Drift * dt;

                if (p.y > _halfH)
                {
                    p.y = -_halfH;
                    p.x = Random.Range(-_halfW, _halfW);
                }

                tr.localPosition = p;

                float pulse = 0.75f + 0.25f * Mathf.Sin(t * 0.9f + _motes[i].Phase);
                var c = _motes[i].Sr.color;
                c.a = _motes[i].BaseAlpha * pulse;
                _motes[i].Sr.color = c;
            }
        }
    }
}
