using System.Collections.Generic;
using UnityEngine;
using RuneCast.Battle;
using RuneCast.Core;
using RuneCast.Gesture;
using RuneCast.Meta;

namespace RuneCast.Runes
{
    /// <summary>
    /// 하트 ♡ — 아직 시체가 남아 있는 아군을 되살린다.
    ///
    /// 별 판정이 "몇 명을 살렸나"라서, 한 명이 죽는 순간 그 판의 최고 등급이 확정돼 버렸다.
    /// 남은 시간 동안 잘해도 되돌릴 수 없으니 거기서 이미 진 판이 된다.
    /// 이 룬은 그 지점을 만회 가능하게 만든다.
    ///
    /// **시체가 사라지기 전에만 걸린다**(Unit.CorpseLinger, 1.1초).
    /// 언제든 되살릴 수 있으면 지키는 룬이 전부 무의미해지므로,
    /// "죽는 걸 보고 즉시 반응"이라는 대가를 붙였다. 회복량이 아니라 반응 속도를 요구한다.
    ///
    /// 되살릴 대상이 없으면 마나를 쓰지 않는다 — RuneCaster가 먼저 확인한다.
    /// </summary>
    public class RevivePulse : MonoBehaviour
    {
        private static readonly List<Unit> Buffer = new List<Unit>();
        private static readonly Color Base = new Color(1f, 0.5f, 0.62f);

        private SpriteRenderer _ring;
        private SpriteRenderer _core;
        private float _life;
        private float _duration = 0.75f;
        private float _radius;
        private Color _tint;

        /// <summary>되살릴 수 있는 아군이 하나라도 있는가. 시전 전에 확인한다.</summary>
        public static bool HasTarget(Vector3 center, float radius)
        {
            Unit.CollectCorpsesInRadius(center, radius, Team.Hero, Buffer);
            return Buffer.Count > 0;
        }

        /// <summary>실제로 되살린 수를 돌려준다.</summary>
        public static int Cast(Vector3 center, float radius, float hpFraction, RuneGrade grade)
        {
            Unit.CollectCorpsesInRadius(center, radius, Team.Hero, Buffer);

            int revived = 0;
            for (int i = 0; i < Buffer.Count; i++)
            {
                if (!Buffer[i].Revive(hpFraction)) continue;
                revived++;

                BurstFx.Play(Vfx.ReviveBurst, Buffer[i].transform.position, 1.6f, Base, 46);
                // 혼이 올라가는 파티클. 소생은 판을 뒤집는 순간인데 폭발 시트만으로는
                // "살아났다"보다 "맞았다"에 가까웠다. 드문 이벤트라 비용 걱정도 없다.
                ParticleFx.Spawn(Pfx.Revive, Buffer[i].transform.position, 0.7f);
                FloatingText.Label(Buffer[i].transform.position + new Vector3(0f, 0.7f, 0f),
                    Loc.T("hud.revived"), Base, 20f);
            }

            var go = new GameObject("RevivePulse");
            FxRoot.Adopt(go);
            go.transform.position = center;

            var fx = go.AddComponent<RevivePulse>();
            fx._radius = radius;
            fx._tint = Color.Lerp(Base, GradeVisuals.ColorOf(grade), 0.3f);

            fx._ring = PrimitiveSprites.Spawn("Ring", PrimitiveSprites.Ring(96, 0.1f), fx._tint, 40, go.transform);
            fx._core = PrimitiveSprites.Spawn("Core", PrimitiveSprites.Circle(64),
                new Color(fx._tint.r, fx._tint.g, fx._tint.b, 0.25f), 39, go.transform);

            // 힐과 같은 파일을 낮은 피치로. 소생은 힐보다 무겁게 들려야
            // "더 큰 일이 일어났다"가 귀로 먼저 온다.
            AudioManager.Play(Sfx.CastRevive, GradeVisuals.VolumeOf(grade), 0.78f);

            if (revived > 0) TimeControl.HitStop(0.06f, 0.08f);
            return revived;
        }

        private void Update()
        {
            _life += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(_life / _duration);

            // 밖에서 안으로 모인다 — 다른 룬은 전부 퍼져 나가므로 방향만으로 구분된다
            float s = Mathf.Lerp(1.15f, 0.25f, t) * _radius * 2f;
            _ring.transform.localScale = new Vector3(s, s, 1f);
            _core.transform.localScale = new Vector3(s * 0.6f, s * 0.6f, 1f);

            float alpha = Mathf.Sin(t * Mathf.PI); // 가운데서 가장 진하게
            _ring.color = new Color(_tint.r, _tint.g, _tint.b, alpha);
            _core.color = new Color(_tint.r, _tint.g, _tint.b, alpha * 0.35f);

            if (t >= 1f) Destroy(gameObject);
        }
    }
}
