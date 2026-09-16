using UnityEngine;
using RuneCast.Meta;

namespace RuneCast.Battle
{
    /// <summary>
    /// 안개(4장) — 아군에게서 먼 적일수록 흐려진다.
    ///
    /// **정보를 줄이는 규칙이다.** 이 게임은 전장을 읽고 무엇을 그릴지 정하는
    /// 게임이라, 무엇이 오고 있는지를 늦게 알려주는 것이 피해를 늘리는 것보다
    /// 직접적인 압박이다. 강화로 못 이기는 종류이기도 하다 — 위력을 올려도
    /// 안 보이는 건 그대로 안 보인다.
    ///
    /// **완전히 숨기지는 않는다.** 아예 안 보이면 화면이 고장 난 것처럼 보이고,
    /// 무엇보다 마나를 어디에 쓸지 판단할 근거가 통째로 사라져서 그냥 운이 된다.
    /// 망령을 완전 면역에서 20%로 내린 것과 같은 판단이다.
    /// </summary>
    public class FogVeil : MonoBehaviour
    {
        /// <summary>가장 흐릴 때의 알파. 실루엣은 남아야 "뭔가 온다"가 읽힌다.</summary>
        private const float MinAlpha = 0.22f;

        /// <summary>이만큼 더 멀어지면 완전히 흐려진다.</summary>
        private const float FadeSpan = 3.0f;

        private Unit _unit;
        private SpriteRenderer _sr;
        private Color _base;
        private bool _ready;

        private void Start()
        {
            _unit = GetComponent<Unit>();
            _sr = _unit != null ? _unit.Body : null;
            if (_sr == null) { enabled = false; return; }

            _base = _sr.color;
            _ready = true;
        }

        private void LateUpdate()
        {
            // **LateUpdate에서 칠한다.** Unit이 피격·망령 표시 등으로 색을 건드리므로,
            // 같은 프레임에 나중에 덮어써야 안개가 안 지워진다.
            if (!_ready) return;

            float near = ChapterRules.FogDistance;
            if (near <= 0f) { _sr.color = _base; return; }

            // 기준은 카메라가 아니라 **제일 가까운 아군**이다. 화면 어디에 있느냐가
            // 아니라 "우리 편이 볼 수 있느냐"가 안개의 뜻이다.
            float d = NearestHeroDistance();
            float t = Mathf.Clamp01((d - near) / FadeSpan);
            float a = Mathf.Lerp(1f, MinAlpha, t);

            _sr.color = new Color(_base.r, _base.g, _base.b, _base.a * a);
        }

        private float NearestHeroDistance()
        {
            float best = float.MaxValue;
            var all = Unit.All;
            Vector3 me = transform.position;

            for (int i = 0; i < all.Count; i++)
            {
                Unit u = all[i];
                if (u == null || !u.IsAlive || u.team != Team.Hero) continue;

                float d = (u.transform.position - me).sqrMagnitude;
                if (d < best) best = d;
            }
            return best == float.MaxValue ? 0f : Mathf.Sqrt(best);
        }
    }
}
