using UnityEngine;
using RuneCast.Core;

namespace RuneCast.UI
{
    /// <summary>
    /// "여기에 손을 대고 이렇게 그으세요" — 화면을 어둡게 깔고 손 모양이 궤적을 따라간다.
    ///
    /// **도형만 저절로 그려지는 것으로는 부족했다.** 프롤로그와 첫 판에서
    /// `RuneTracer`가 꺾쇠를 그려 보이지만, 그건 "저런 게 뜨는구나"로 읽힐 뿐
    /// **내가 무언가 해야 한다**는 신호가 아니다. 처음 켠 사람은 화면을 만질
    /// 생각을 못 하고 전투가 저절로 흘러가는 걸 지켜본다.
    ///
    /// 세 가지를 같이 건다:
    ///  · **어둡게 깐다** — 전장이 흐려지면 밝게 남은 것(궤적과 손)만 남는다.
    ///    무엇을 보라는 건지 고를 필요가 없어진다.
    ///  · **손이 붓끝을 따라간다** — 정지한 그림은 "보라"지만 움직이는 손은 "따라 하라"다.
    ///  · **손끝에서 고리가 퍼진다** — 누르는 곳이 어디인지 점으로 못 박는다.
    ///
    /// 월드 스프라이트로 그린다(GUI가 아니라). 궤적이 LineRenderer라 같은 층에
    /// 있어야 앞뒤가 맞고, GUI로 덮으면 궤적까지 같이 어두워진다.
    /// </summary>
    public class TouchHint : MonoBehaviour
    {
        /// <summary>따라갈 시연 궤적.</summary>
        public RuneTracer tracer;

        /// <summary>밖에서 켜고 끈다.</summary>
        [System.NonSerialized] public bool visible;

        // 궤적(60/61)보다 아래에 막을, 위에 손을 둔다.
        private const int ScrimOrder = 55;
        private const int RingOrder = 65;
        private const int HandOrder = 66;

        /// <summary>막의 짙기. 전장이 보이되 눈이 안 가는 정도.</summary>
        private const float ScrimAlpha = 0.55f;

        private SpriteRenderer _scrim, _hand, _ring;
        private float _fade;

        private void Start()
        {
            _scrim = PrimitiveSprites.Spawn("Scrim", PrimitiveSprites.Square(8),
                new Color(0.02f, 0.02f, 0.05f, 0f), ScrimOrder, transform);

            _ring = PrimitiveSprites.Spawn("Ring", PrimitiveSprites.Ring(64, 0.16f),
                new Color(1f, 0.92f, 0.6f, 0f), RingOrder, transform);

            Sprite hand = SpriteSheet.Single("Sprites/UI/hand", 128f);
            if (hand != null)
                _hand = PrimitiveSprites.Spawn("Hand", hand, new Color(1f, 1f, 1f, 0f),
                    HandOrder, transform);

            FitScrim();
        }

        /// <summary>막이 화면을 다 덮게. 카메라가 바뀔 수 있으니 매번 다시 잰다.</summary>
        private void FitScrim()
        {
            Camera cam = Camera.main;
            if (cam == null || _scrim == null) return;

            float h = cam.orthographicSize * 2f;
            float w = h * cam.aspect;

            // 1.4배로 넉넉히 — 확대(프롤로그)가 풀릴 때 가장자리가 드러나면 안 된다.
            _scrim.transform.localScale = new Vector3(w * 1.4f, h * 1.4f, 1f);
            _scrim.transform.position = new Vector3(cam.transform.position.x,
                                                    cam.transform.position.y, 0.6f);
        }

        private void LateUpdate()
        {
            // 켜고 끌 때 부드럽게. 툭 어두워지면 화면이 깜빡인 것처럼 보인다.
            _fade = Mathf.MoveTowards(_fade, visible ? 1f : 0f, Time.unscaledDeltaTime * 2.4f);

            if (_scrim != null)
            {
                FitScrim();
                var c = _scrim.color;
                _scrim.color = new Color(c.r, c.g, c.b, ScrimAlpha * _fade);
            }

            if (_fade <= 0.001f)
            {
                Hide(_hand); Hide(_ring);
                return;
            }

            // 붓끝을 따라간다. 다 그린 뒤에는 마지막 점에 머무는데, 그때는
            // 손도 같이 멈춰야 "여기서 끝난다"가 읽힌다.
            Vector3 tip = tracer != null ? tracer.TipWorld : Vector3.zero;

            if (_hand != null)
            {
                // **손끝이 궤적 위에 오게 밀어 준다.** 그림에서 검지 끝은
                // 왼쪽 위 모서리 근처라, 스프라이트 중심을 그대로 두면
                // 손바닥이 선 위에 얹힌다.
                _hand.transform.position = tip + new Vector3(0.42f, -0.46f, -0.1f);
                _hand.transform.localScale = Vector3.one * 1.15f;
                _hand.color = new Color(1f, 1f, 1f, _fade);
            }

            if (_ring != null)
            {
                // 손끝에서 고리가 퍼졌다 사라진다. 0.9초 주기.
                float k = Mathf.Repeat(Time.unscaledTime, 0.9f) / 0.9f;
                _ring.transform.position = tip + new Vector3(0f, 0f, -0.05f);
                _ring.transform.localScale = Vector3.one * Mathf.Lerp(0.35f, 1.5f, k);
                _ring.color = new Color(1f, 0.92f, 0.6f, (1f - k) * 0.85f * _fade);
            }
        }

        private static void Hide(SpriteRenderer sr)
        {
            if (sr == null) return;
            var c = sr.color;
            if (c.a != 0f) sr.color = new Color(c.r, c.g, c.b, 0f);
        }
    }
}
