using System.Collections.Generic;
using UnityEngine;
using RuneCast.Battle;
using RuneCast.Core;

namespace RuneCast.Runes
{
    /// <summary>
    /// 별 ☆ — 낙하 예고 후 광역 타격.
    ///
    /// 즉발이 아니라 예고를 두는 이유는 두 가지다.
    ///  1. 별을 그리는 데 시간이 걸리는 만큼 한 방이 세야 하는데, 즉발로 세게 터지면
    ///     화면에서 뭐가 일어났는지 안 읽힌다.
    ///  2. 낙하 지점이 보이면 "제대로 찍었나"를 스스로 판단할 수 있다.
    /// </summary>
    public class MeteorStrike : MonoBehaviour
    {
        private static readonly List<Unit> Buffer = new List<Unit>();
        private static readonly Color Warn = new Color(1f, 0.72f, 0.25f);
        private static readonly Color Boom = new Color(1f, 0.95f, 0.7f);

        private SpriteRenderer _marker;
        private SpriteRenderer _rock;
        private SpriteRenderer _blast;

        private float _t;
        private float _radius;
        private float _damage;
        private bool _hit;
        private RuneCast.Gesture.RuneGrade _grade;
        private Color _tint;

        private const float FallTime = 0.75f;
        private const float BlastTime = 0.35f;

        public static void Cast(Vector3 center, float radius, float damage, RuneCast.Gesture.RuneGrade grade)
        {
            var go = new GameObject("MeteorStrike");
            FxRoot.Adopt(go);
            go.transform.position = center;

            var fx = go.AddComponent<MeteorStrike>();
            fx._radius = radius;
            fx._damage = damage;
            fx._grade = grade;
            fx._tint = Color.Lerp(Boom, GradeVisuals.ColorOf(grade), 0.4f);

            // 착탄 예고 원
            fx._marker = PrimitiveSprites.Spawn("Marker", PrimitiveSprites.Ring(96, 0.1f), Warn, 39, go.transform);
            fx._marker.transform.localScale = Vector3.one * radius * 2f;

            // 떨어지는 별. 등급이 높을수록 크고 밝게.
            float flourish = RuneCast.Gesture.RuneGrading.Flourish(grade);
            fx._rock = PrimitiveSprites.Spawn("Rock", PrimitiveSprites.Circle(48), fx._tint, 45, go.transform);
            fx._rock.transform.localScale = Vector3.one * (0.45f + 0.45f * flourish);

            // 폭발 범위를 보여주는 원. 실제 피해 반경과 정확히 같아야 하므로 절차 생성.
            fx._blast = PrimitiveSprites.Spawn("Blast", PrimitiveSprites.Circle(96),
                new Color(1f, 0.8f, 0.4f, 0f), 44, go.transform);
        }

        private void Update()
        {
            _t += Time.unscaledDeltaTime;

            if (!_hit)
            {
                float k = Mathf.Clamp01(_t / FallTime);

                // 위에서 낙하. 가속을 줘야 "떨어진다"로 읽힌다.
                float h = Mathf.Lerp(7f, 0f, k * k);
                _rock.transform.localPosition = new Vector3(0f, h, 0f);

                // 예고 원은 착탄이 가까울수록 빠르게 점멸
                var mc = Warn;
                mc.a = 0.35f + 0.45f * Mathf.Abs(Mathf.Sin(_t * Mathf.Lerp(6f, 22f, k)));
                _marker.color = mc;

                if (k >= 1f) Detonate();
                return;
            }

            float b = Mathf.Clamp01((_t - FallTime) / BlastTime);
            float s = Mathf.Lerp(0.3f, 1f, b) * _radius * 2f;
            _blast.transform.localScale = new Vector3(s, s, 1f);
            _blast.color = new Color(_tint.r, _tint.g, _tint.b, 1f - b);

            if (b >= 1f) Destroy(gameObject);
        }

        private void Detonate()
        {
            _hit = true;
            _rock.enabled = false;
            _marker.enabled = false;

            float flourish = RuneCast.Gesture.RuneGrading.Flourish(_grade);

            AudioManager.Play(Sfx.HitMeteor, GradeVisuals.VolumeOf(_grade));

            // 착탄 순간의 충격은 룬 시전 때보다 크게 — 여기가 이 룬의 절정이다.
            // 화면을 아주 잠깐 멈추면 같은 데미지도 훨씬 무겁게 느껴진다.
            CameraShake.Shake(0.12f + 0.25f * flourish, 0.3f);
            TimeControl.HitStop(0.04f, 0.05f + 0.05f * flourish);

            // 착탄 지점 폭발. 시트 폭발이 픽셀 바탕이고, 파티클(CFXR)이 그 위의
            // 광량이다 — 시트 하나만으로는 화면이 번쩍할 뿐 열기가 안 남는다.
            BurstFx.Play(Vfx.MeteorExplosion, transform.position, _radius * 2.6f, _tint, 47);
            ParticleFx.Spawn(Pfx.Meteor, transform.position, 0.8f + 0.5f * flourish);

            // 등급이 높으면 주변에 파편 폭발을 흩뿌린다. 같은 반경이어도 훨씬 요란해 보인다.
            int shards = Mathf.RoundToInt(flourish * 5f);
            for (int i = 0; i < shards; i++)
            {
                float a = Random.Range(0f, Mathf.PI * 2f);
                float r = Random.Range(0.3f, 1f) * _radius;
                Vector3 p = transform.position + new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 0f);
                BurstFx.Play(Vfx.StarBurst, p, 1.3f, _tint, 47);
            }

            Unit.CollectInRadius(transform.position, _radius, Team.Enemy, Buffer);
            for (int i = 0; i < Buffer.Count; i++)
            {
                BurstFx.Play(Vfx.SmallHit, Buffer[i].transform.position, 1.2f, _tint, 48);
                Buffer[i].TakeDamage(_damage, true);
            }
        }
    }
}
