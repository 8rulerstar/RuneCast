using UnityEngine;
using RuneCast.Core;
using RuneCast.Meta;

namespace RuneCast.Battle
{
    /// <summary>
    /// 서리(3장) — 아군이 얼어붙어 있다는 표시.
    ///
    /// **이 장의 규칙만 화면에서 안 보였다.** 공격 주기가 1.3배로 느려지는데
    /// 그건 "원래 이 정도였나" 싶은 종류다 — 안개는 흐려지고 부패는 터지고
    /// 심연은 숫자가 뜨는데, 서리는 배너를 읽지 않으면 알 방법이 없었다.
    ///
    /// 둘로 알린다:
    ///  · **들어올 때 한 번** 얼음이 튄다. "지금 무슨 일이 일어났다"는 순간이 있어야
    ///    바닥이 푸른 이유가 이어진다.
    ///  · **서 있는 동안 계속** 아군이 차갑게 물든다. 순간 연출만 있으면 20초 뒤에는
    ///    잊는다. 대신 아주 옅게 — 아군을 못 알아보게 만들면 안 된다.
    /// </summary>
    public class FrostChill : MonoBehaviour
    {
        /// <summary>차갑게 물드는 정도. 1이면 원래 색.</summary>
        private static readonly Color Chill = new Color(0.72f, 0.86f, 1.00f);

        private Unit _unit;
        private SpriteRenderer _sr;
        private bool _ready;

        private void Start()
        {
            _unit = GetComponent<Unit>();
            _sr = _unit != null ? _unit.Body : null;
            if (_sr == null) { enabled = false; return; }
            _ready = true;

            // 얼음이 한 번 튄다. **작게(0.5)** — 아홉 명이 동시에 터지면
            // 판이 시작되자마자 화면이 얼음으로 덮인다.
            ParticleFx.Spawn(Pfx.Frost, transform.position, 0.5f);
        }

        private Color _wrote;
        private bool _wroteOnce;

        private void LateUpdate()
        {
            // FogVeil과 같은 이유로 LateUpdate다 — Unit이 피격 표시로 색을
            // 건드리므로 같은 프레임에 나중에 덮어써야 한다.
            if (!_ready) return;

            // **곱한 값을 다시 곱하면 안 된다.**
            //
            // 처음엔 매 프레임 `_sr.color *= Chill`로 썼는데, 그러면 프레임마다
            // 곱이 쌓여서 몇 초 만에 아군이 새까매진다. 화면에서는 "서리 장은
            // 어둡네" 정도로 보여서 버그로 안 읽히는 종류다.
            //
            // 지난 프레임에 우리가 써 둔 값과 지금 값이 다르면 Unit이 덮어썼다는
            // 뜻이다 — 그때만 원본을 새로 잡는다. 그래야 피격 표시도 살아남는다.
            if (!_wroteOnce || _sr.color != _wrote) _base = _sr.color;

            _wrote = new Color(_base.r * Chill.r, _base.g * Chill.g, _base.b * Chill.b, _base.a);
            _wroteOnce = true;
            _sr.color = _wrote;
        }

        private Color _base;
    }
}
