using System.Collections.Generic;
using UnityEngine;
using RuneCast.Battle;
using RuneCast.Core;

namespace RuneCast.Runes
{
    /// <summary>
    /// 나선 ◎ — 적을 중심으로 끌어당기고 둔화. 피해는 없다.
    ///
    /// 피해를 안 주는 게 핵심이다. 기존 룬 넷이 전부 "피해 아니면 회복"이라
    /// 선택지가 사실상 강도 차이뿐이었다. 적을 모아두면 별똥별·연쇄 번개가
    /// 훨씬 잘 들어가므로, 이 룬은 다른 룬을 강하게 만드는 방식으로 값을 한다.
    /// </summary>
    public class Vortex : MonoBehaviour
    {
        private static readonly List<Unit> Buffer = new List<Unit>();
        private static readonly Color Tint = new Color(0.7f, 0.6f, 1f);

        private SpriteRenderer _ring;
        private SpriteRenderer _core;
        private SpriteRenderer _inner;
        private Color _tint = Tint;
        private BurstFx _swirl;
        private float _life;
        private float _radius;
        private float _pullSpeed;
        private float _duration;
        private float _slowMultiplier;

        public static void Cast(Vector3 center, float radius, float duration, float pullSpeed,
            float slowMultiplier, RuneCast.Gesture.RuneGrade grade)
        {
            var go = new GameObject("Vortex");
            FxRoot.Adopt(go);
            go.transform.position = center;

            float flourish = RuneCast.Gesture.RuneGrading.Flourish(grade);

            var fx = go.AddComponent<Vortex>();
            fx._radius = radius;
            fx._duration = duration;
            fx._slowMultiplier = slowMultiplier;

            // 잘 그릴수록 더 세게 빨아들인다 — 지속시간은 RuneCaster가 이미 늘렸다
            fx._pullSpeed = pullSpeed * (1f + 0.6f * flourish);
            fx._tint = Color.Lerp(Tint, GradeVisuals.ColorOf(grade), 0.35f);

            fx._ring = PrimitiveSprites.Spawn("Ring", PrimitiveSprites.Ring(96, 0.08f), fx._tint, 38, go.transform);
            fx._ring.transform.localScale = Vector3.one * radius * 2f;

            fx._core = PrimitiveSprites.Spawn("Core", PrimitiveSprites.Circle(64),
                new Color(fx._tint.r, fx._tint.g, fx._tint.b, 0.25f), 37, go.transform);

            // 실제 회전 연출은 스프라이트 시트를 반복 재생해서 만든다.
            // 절차 생성 링만으로는 "돌고 있다"는 읽혀도 "빨아들인다"는 안 읽힌다.
            // 링은 남겨 둔다 — 그게 실제 흡인 반경을 정확히 표시하는 유일한 요소다.
            fx._swirl = BurstFx.Play(Vfx.VortexSwirl, center, radius * 2.4f,
                new Color(fx._tint.r, fx._tint.g, fx._tint.b, 0.85f), 37, true);

            // 등급이 높으면 안쪽에 반대로 도는 링을 겹쳐 소용돌이가 깊어 보이게
            if (flourish > 0.5f)
            {
                fx._inner = PrimitiveSprites.Spawn("Inner", PrimitiveSprites.Ring(96, 0.06f),
                    fx._tint, 38, go.transform);
                fx._inner.transform.localScale = Vector3.one * radius * 1.2f;
            }

            AudioManager.Play(Sfx.CastVortex, GradeVisuals.VolumeOf(grade) * 0.9f);
        }

        private void Update()
        {
            _life += Time.deltaTime;
            float t = Mathf.Clamp01(_life / _duration);

            Unit.CollectInRadius(transform.position, _radius, Team.Enemy, Buffer);
            for (int i = 0; i < Buffer.Count; i++)
            {
                Unit u = Buffer[i];

                Vector3 toCenter = transform.position - u.transform.position;
                float dist = toCenter.magnitude;

                // 중심에 완전히 겹치면 서로 밀어내는 힘이 없어 한 점에 뭉친다.
                // 안쪽에 여유를 남겨 둔다.
                if (dist > 0.35f)
                    u.transform.position += toCenter / dist * _pullSpeed * Time.deltaTime;

                u.ApplySlow(_slowMultiplier, 0.25f);
            }

            // 빨려드는 느낌을 주려고 회전 + 수축
            transform.Rotate(0f, 0f, -220f * Time.deltaTime);

            float ringScale = _radius * 2f * Mathf.Lerp(1f, 0.75f, t);
            _ring.transform.localScale = Vector3.one * ringScale;
            _core.transform.localScale = Vector3.one * ringScale * Mathf.Lerp(0.15f, 0.5f, t);

            float alpha = 1f - t * t; // 끝에서 급히 사라지게
            _ring.color = new Color(_tint.r, _tint.g, _tint.b, alpha);
            _core.color = new Color(_tint.r, _tint.g, _tint.b, alpha * 0.3f);

            if (_inner != null)
            {
                // 바깥 링과 반대로 돌려서 깊이를 만든다 (부모 회전을 상쇄하고 더 돌린다)
                _inner.transform.localRotation = Quaternion.Euler(0f, 0f, _life * 520f);
                _inner.transform.localScale = Vector3.one * ringScale * 0.55f;
                _inner.color = new Color(_tint.r, _tint.g, _tint.b, alpha * 0.8f);
            }

            if (t >= 1f) Destroy(gameObject);
        }

        private void OnDestroy()
        {
            // 반복 재생 중인 시트는 이 오브젝트의 자식이 아니라 따로 떠 있다.
            // 여기서 안 끄면 소용돌이가 끝나도 계속 돌아간다.
            if (_swirl != null) Destroy(_swirl.gameObject);
        }
    }
}
