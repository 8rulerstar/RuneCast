using System.Collections.Generic;
using UnityEngine;
using RuneCast.Battle;
using RuneCast.Core;

namespace RuneCast.Runes
{
    /// <summary>
    /// 지그재그 Z — 가까운 적부터 튕겨가는 연쇄 번개.
    ///
    /// 그린 위치에서 가장 가까운 적을 첫 표적으로 잡고, 거기서 다시 가장 가까운
    /// 다른 적으로 옮겨간다. 적이 뭉쳐 있을수록 이득이라 소용돌이와 짝이 된다.
    /// 튈 때마다 피해가 줄어서, 무조건 강한 룬이 되지 않게 했다.
    /// </summary>
    public class ChainLightning : MonoBehaviour
    {
        private static readonly Color Tint = new Color(0.75f, 0.9f, 1f);
        private const float FalloffPerHop = 0.75f;

        private readonly List<LineRenderer> _bolts = new List<LineRenderer>();
        private float _life;
        private Color _tint = Tint;
        private float _width = 0.09f;
        private const float Duration = 0.4f;

        public static void Cast(Vector3 origin, float maxJumpRange, float damage, int maxHops, RuneCast.Gesture.RuneGrade grade)
        {
            // 1단계: 표적 사슬을 먼저 확정한다. 피해는 아래에서 한 번에 적용 —
            // 중간에 죽어서 목록이 흔들리면 번개 궤적과 실제 피해가 어긋난다.
            var hit = new List<Unit>();
            Vector3 from = origin;

            for (int hop = 0; hop < maxHops; hop++)
            {
                Unit next = NearestUnhit(from, maxJumpRange, hit);
                if (next == null) break;

                hit.Add(next);
                from = next.transform.position;
            }

            if (hit.Count == 0) return;

            float flourish = RuneCast.Gesture.RuneGrading.Flourish(grade);

            var go = new GameObject("ChainLightning");
            FxRoot.Adopt(go);
            var fx = go.AddComponent<ChainLightning>();
            fx._tint = Color.Lerp(Tint, GradeVisuals.ColorOf(grade), 0.4f);
            fx._width = 0.07f + 0.06f * flourish; // 등급이 높으면 번개가 굵어진다

            Vector3 prev = origin;
            float d = damage;
            for (int i = 0; i < hit.Count; i++)
            {
                fx.AddBolt(prev, hit[i].transform.position);

                // 완벽에 가까우면 같은 경로에 가지를 하나 더 겹쳐 굵고 어지럽게 보이게
                if (flourish > 0.5f) fx.AddBolt(prev, hit[i].transform.position);

                BurstFx.Play(Vfx.ChainRing, hit[i].transform.position,
                    1.5f + 0.5f * flourish, fx._tint, 46);

                // 전기 파티클을 겹친다. 연쇄는 많아야 대여섯 대상이라 부담이 없고,
                // 시트 고리만으로는 "감전"이 아니라 "동그라미가 떴다"로 읽혔다.
                ParticleFx.Spawn(Pfx.Chain, hit[i].transform.position, 0.55f + 0.25f * flourish);

                if (i > 0) d *= FalloffPerHop;
                hit[i].TakeDamage(d, true);

                prev = hit[i].transform.position;
            }

            AudioManager.Play(Sfx.HitChain, GradeVisuals.VolumeOf(grade) * 0.9f);
        }

        private static Unit NearestUnhit(Vector3 from, float range, List<Unit> exclude)
        {
            Unit best = null;
            float bestSqr = range * range;

            for (int i = 0; i < Unit.All.Count; i++)
            {
                Unit u = Unit.All[i];
                if (u == null || !u.IsAlive || u.team != Team.Enemy) continue;
                if (exclude.Contains(u)) continue;

                float d = (u.transform.position - from).sqrMagnitude;
                if (d <= bestSqr) { bestSqr = d; best = u; }
            }
            return best;
        }

        private void AddBolt(Vector3 a, Vector3 b)
        {
            var go = new GameObject("Bolt");
            FxRoot.Adopt(go);
            go.transform.SetParent(transform, false);

            var lr = go.AddComponent<LineRenderer>();
            lr.material = PrimitiveSprites.SpriteMaterial;
            lr.useWorldSpace = true;
            lr.widthMultiplier = _width;
            lr.sortingOrder = 45;
            lr.startColor = lr.endColor = _tint;

            // 직선이면 번개로 안 읽힌다. 중간을 지그재그로 흔든다.
            const int segments = 6;
            lr.positionCount = segments + 1;

            Vector3 dir = (b - a);
            Vector3 normal = new Vector3(-dir.y, dir.x, 0f).normalized;

            for (int i = 0; i <= segments; i++)
            {
                float t = i / (float)segments;
                float wobble = (i == 0 || i == segments) ? 0f : Random.Range(-0.22f, 0.22f);
                lr.SetPosition(i, a + dir * t + normal * wobble);
            }

            _bolts.Add(lr);
        }

        private void Update()
        {
            _life += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(_life / Duration);

            var c = _tint;
            c.a = 1f - t;
            for (int i = 0; i < _bolts.Count; i++)
            {
                if (_bolts[i] == null) continue;
                _bolts[i].startColor = c;
                _bolts[i].endColor = c;
            }

            if (t >= 1f) Destroy(gameObject);
        }
    }
}
