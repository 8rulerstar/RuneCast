using System.Collections.Generic;
using UnityEngine;
using RuneCast.Battle;
using RuneCast.Core;
using RuneCast.Gesture;

namespace RuneCast.Runes
{
    /// <summary>
    /// 삼각형 △ — 그린 삼각형 안의 아군에게 보호막.
    ///
    /// 판정도 표시도 **삼각형**이다. 예전에는 바운딩 상자로 판정하고 상자를 그렸는데,
    /// 삼각형을 그렸는데 네모(그리고 유닛에는 동그란 링)가 뜨니
    /// 무엇이 발동했는지 연결이 끊겼다. 그린 도형과 결과가 같은 모양이어야 한다.
    ///
    /// 벽 오브젝트를 세우는 대신 보호막을 주는 이유: 벽은 유닛 이동·경로에
    /// 간섭해서 AI를 건드려야 하고, 그러면 "전투는 최소 뼈대" 원칙이 깨진다.
    /// 흡수량은 Unit.TakeDamage가 이미 처리한다.
    /// </summary>
    public class ShieldZone : MonoBehaviour
    {
        private static readonly List<Unit> Buffer = new List<Unit>();
        private static readonly Color Base = new Color(0.55f, 0.85f, 1f);

        private readonly List<LineRenderer> _outlines = new List<LineRenderer>();
        private float _life;
        private Color _tint;
        private const float Duration = 0.7f;

        public static void Cast(Vector3 center, Vector2 size, float shieldAmount, RuneGrade grade)
        {
            float halfX = Mathf.Max(size.x, 0.6f) * 0.5f;
            float halfY = Mathf.Max(size.y, 0.6f) * 0.5f;

            // 그린 영역에 내접하는 위쪽 꼭짓점 삼각형
            Vector3 top = center + new Vector3(0f, halfY, 0f);
            Vector3 bl = center + new Vector3(-halfX, -halfY, 0f);
            Vector3 br = center + new Vector3(halfX, -halfY, 0f);

            Buffer.Clear();
            for (int i = 0; i < Unit.All.Count; i++)
            {
                Unit u = Unit.All[i];
                if (u == null || !u.IsAlive || u.team != Team.Hero) continue;
                if (InTriangle(u.transform.position, top, bl, br)) Buffer.Add(u);
            }
            for (int i = 0; i < Buffer.Count; i++) Buffer[i].AddShield(shieldAmount);

            var go = new GameObject("ShieldZone");
            FxRoot.Adopt(go);
            go.transform.position = center;

            var fx = go.AddComponent<ShieldZone>();
            fx._tint = Color.Lerp(Base, GradeVisuals.ColorOf(grade), 0.35f);

            float flourish = RuneGrading.Flourish(grade);
            int layers = 1 + Mathf.RoundToInt(flourish * 2f); // 테두리 1~3겹
            for (int k = 0; k < layers; k++)
            {
                float shrink = 1f - k * 0.13f;
                fx.AddOutline(
                    center + (top - center) * shrink,
                    center + (bl - center) * shrink,
                    center + (br - center) * shrink,
                    0.06f + 0.02f * flourish);
            }

            // 보호막을 받은 유닛에게 기운이 모여드는 연출. 테두리만으로는
            // "누가 받았는지"가 안 읽힌다 — 삼각형 안에 적도 같이 서 있기 때문.
            for (int i = 0; i < Buffer.Count; i++)
            {
                BurstFx.Play(Vfx.ShieldCharge, Buffer[i].transform.position,
                    1.6f + 0.5f * flourish, fx._tint, 46);
                ParticleFx.Spawn(Pfx.Shield, Buffer[i].transform.position, 0.5f);
            }
        }

        /// <summary>부호 판정 3회로 삼각형 내부 여부를 본다.</summary>
        private static bool InTriangle(Vector3 p, Vector3 a, Vector3 b, Vector3 c)
        {
            float d1 = Cross(p, a, b);
            float d2 = Cross(p, b, c);
            float d3 = Cross(p, c, a);

            bool hasNeg = d1 < 0f || d2 < 0f || d3 < 0f;
            bool hasPos = d1 > 0f || d2 > 0f || d3 > 0f;
            return !(hasNeg && hasPos);
        }

        private static float Cross(Vector3 p, Vector3 a, Vector3 b)
        {
            return (p.x - b.x) * (a.y - b.y) - (a.x - b.x) * (p.y - b.y);
        }

        private void AddOutline(Vector3 a, Vector3 b, Vector3 c, float width)
        {
            var go = new GameObject("Outline");
            FxRoot.Adopt(go);
            go.transform.SetParent(transform, false);

            var lr = go.AddComponent<LineRenderer>();
            lr.material = PrimitiveSprites.SpriteMaterial;
            lr.useWorldSpace = true;
            lr.loop = true;
            lr.widthMultiplier = width;
            lr.numCornerVertices = 4;
            lr.numCapVertices = 4;
            lr.sortingOrder = 40;
            lr.startColor = lr.endColor = _tint;

            lr.positionCount = 3;
            lr.SetPosition(0, a);
            lr.SetPosition(1, b);
            lr.SetPosition(2, c);

            _outlines.Add(lr);
        }

        private void Update()
        {
            _life += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(_life / Duration);

            var c = _tint;
            c.a = 1f - t;
            for (int i = 0; i < _outlines.Count; i++)
            {
                if (_outlines[i] == null) continue;
                _outlines[i].startColor = c;
                _outlines[i].endColor = c;
            }

            if (t >= 1f) Destroy(gameObject);
        }
    }
}
