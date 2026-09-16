using UnityEngine;
using RuneCast.Core;
using RuneCast.Meta;

namespace RuneCast.UI
{
    /// <summary>
    /// 주력이 오르는 걸 눈에 보이게 한다.
    ///
    /// **숫자가 그냥 바뀌면 오른 걸 못 본다.** 1840에서 1860이 되는 건 화면에서
    /// 아무 일도 아니다 — 강화를 누른 사람은 파편이 줄어든 것만 느끼고
    /// 뭘 얻었는지는 못 느낀다. 성장형 게임에서 이 순간이 전부인데도 그렇다.
    ///
    /// 그래서 세 가지를 한다.
    ///  1. 숫자가 **차오른다** (한 번에 안 바뀐다)
    ///  2. 오르는 동안 **밝게 빛난다**
    ///  3. 오른 만큼을 **+20으로 옆에 띄운다**
    ///
    /// 표시만 하는 물건이라 실제 값(CombatPower.Total)은 건드리지 않는다.
    /// </summary>
    public class PowerTicker
    {
        private const float TickTime = 0.55f;

        private int _from;
        private int _to = -1;
        private float _t = -1f;
        private int _gain;
        private int _lastFrame = -1;

        /// <summary>지금 화면에 보여줄 값. 차오르는 중이면 중간값.</summary>
        public int Display { get; private set; }

        /// <summary>0~1. 오르는 중일수록 크다. 색·크기를 흔드는 데 쓴다.</summary>
        public float Heat
        {
            get
            {
                if (_t < 0f) return 0f;
                float k = Mathf.Clamp01(_t / TickTime);
                return 1f - k;
            }
        }

        public int Gain { get { return _gain; } }

        /// <summary>
        /// 매 프레임 부른다. 값이 바뀌었으면 차오르기 시작한다.
        ///
        /// 처음 볼 때는 애니메이션하지 않는다 — 화면에 들어오자마자 0에서
        /// 차오르면 방금 뭔가 얻은 것처럼 보인다.
        /// </summary>
        public void Update(int current)
        {
            // **OnGUI는 한 프레임에 여러 번 돈다** (Layout·Repaint·입력 이벤트마다).
            // 버튼이 있는 화면은 Repaint만 걸러낼 수도 없어서, 그냥 두면 시간이
            // 프레임당 두세 번 더해져 숫자가 그만큼 빨리 차오른다.
            if (_lastFrame == Time.frameCount) return;
            _lastFrame = Time.frameCount;

            if (_to < 0)
            {
                _to = _from = Display = current;
                return;
            }

            if (current != _to)
            {
                _from = Display;
                _to = current;
                _gain = _to - _from;
                _t = 0f;

                if (_gain > 0) AudioManager.Play(Sfx.RuneLearned, 0.5f, 1.25f);
            }

            if (_t < 0f) { Display = _to; return; }

            _t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(_t / TickTime);

            // 빨리 오르다 끝에서 느려진다. 선형이면 카운터가 도는 것처럼 보이고
            // 감속이 붙어야 "도달했다"가 읽힌다.
            float e = 1f - (1f - k) * (1f - k) * (1f - k);
            Display = Mathf.RoundToInt(Mathf.Lerp(_from, _to, e));

            if (k >= 1f) { _t = -1f; Display = _to; }
        }

        /// <summary>
        /// 주력 한 줄을 그린다. 오르는 중에는 밝아지고 살짝 커지며,
        /// 오른 양이 옆에 뜬다.
        /// </summary>
        public void Draw(Rect r, GUIStyle style, GUIStyle smallStyle)
        {
            float heat = Heat;

            // 밝은 판·배경 위라 옅은 주황은 안 보인다. 평소엔 진한 청동,
            // 차오르는 동안 밝은 호박색으로 — 어두워지는 게 아니라 달아오른다.
            Color cold = new Color(0.55f, 0.36f, 0.08f);
            Color hot = new Color(0.9f, 0.55f, 0.1f);

            // 교차한 검. 옆의 파편과 같은 줄에 뜨는 숫자라, 그림이 없으면
            // 둘 중 어느 쪽이 "쓰는 것"이고 어느 쪽이 "쌓이는 것"인지 매번 읽어야 한다.
            UiSkin.LabelWithIcon(r, UiSkin.Icon.Power, Loc.F("common.power", Display),
                style, Color.Lerp(cold, hot, heat), 20f);

            if (heat > 0.01f && _gain > 0)
            {
                GUI.color = new Color(0.1f, 0.5f, 0.2f, heat);
                GUI.Label(new Rect(r.x, r.y - 16f * heat, r.width, r.height),
                    "+" + _gain, smallStyle);
            }

            GUI.color = Color.white;
        }
    }
}
