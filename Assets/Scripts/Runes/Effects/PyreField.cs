using System.Collections.Generic;
using UnityEngine;
using RuneCast.Battle;
using RuneCast.Core;
using RuneCast.Gesture;

namespace RuneCast.Runes
{
    /// <summary>
    /// 깃발 ᛝ — 그 자리에 남아 안에 있는 적을 계속 태운다.
    ///
    /// 기존 피해 룬 셋은 전부 그 순간에 끝난다(화살·별똥별·연쇄). 그래서 적이 아직
    /// 안 왔거나 흩어져 있으면 쓸 수가 없고, 항상 "지금 어디 모여 있나"만 보게 된다.
    /// 남아 있는 장판은 **적이 올 자리에 미리 까는** 선택지를 만든다.
    ///
    /// 소용돌이와 짝이 되는 것도 노렸다 — 끌어모으고 그 위에 깔면 둘 다 값을 한다.
    /// 룬 하나가 다른 룬을 강하게 만드는 관계가 이 게임의 깊이가 나오는 곳이다.
    ///
    /// 총 피해는 별똥별보다 크지만 **다 들어가려면 적이 끝까지 그 안에 있어야 한다.**
    /// 즉발 룬과 달리 보장되지 않는 대가로 총량을 높게 잡았다.
    /// </summary>
    public class PyreField : MonoBehaviour
    {
        private static readonly List<Unit> Buffer = new List<Unit>();
        private static readonly Color Base = new Color(1f, 0.55f, 0.2f);

        /// <summary>피해 간격. 짧게 하면 숫자가 화면을 덮고, 길면 장판이 꺼진 줄 안다.</summary>
        private const float TickInterval = 0.4f;

        private SpriteRenderer _ring;
        private SpriteRenderer _fill;
        private BurstFx _flame;
        private float _life;
        private float _duration;
        private float _radius;
        private float _tickDamage;
        private float _tickTimer;
        private Color _tint;

        public static void Cast(Vector3 center, float radius, float damagePerTick,
            float duration, RuneGrade grade)
        {
            var go = new GameObject("PyreField");
            FxRoot.Adopt(go);
            go.transform.position = center;

            var fx = go.AddComponent<PyreField>();
            fx._radius = radius;
            fx._duration = duration;
            fx._tickDamage = damagePerTick;
            fx._tint = Color.Lerp(Base, GradeVisuals.ColorOf(grade), 0.3f);

            // 첫 타를 바로 넣는다. 깔았는데 0.4초 동안 아무 일도 안 일어나면
            // 발동이 안 된 줄 알고 한 번 더 그리게 된다.
            fx._tickTimer = 0f;

            fx._ring = PrimitiveSprites.Spawn("Ring", PrimitiveSprites.Ring(96, 0.09f), fx._tint, 36, go.transform);
            fx._ring.transform.localScale = Vector3.one * radius * 2f;

            fx._fill = PrimitiveSprites.Spawn("Fill", PrimitiveSprites.Circle(64),
                new Color(fx._tint.r, fx._tint.g, fx._tint.b, 0.22f), 35, go.transform);
            fx._fill.transform.localScale = Vector3.one * radius * 2f;

            // 반복 재생 불꽃. 링만으로는 "여기 밟으면 아프다"가 안 읽힌다.
            fx._flame = BurstFx.Play(Vfx.FireRing, center, radius * 1.8f,
                new Color(fx._tint.r, fx._tint.g, fx._tint.b, 0.7f), 36, true);

            // 불이 붙는 순간의 화벽 파티클. 지속 불꽃은 위의 루프 시트가 맡고,
            // 이건 점화 한 방이다 — 스포너가 루프를 끄므로 깔린 채 남지 않는다.
            ParticleFx.Spawn(Pfx.Pyre, center, 0.55f + 0.25f * RuneGrading.Flourish(grade));

            AudioManager.Play(Sfx.CastPyre, GradeVisuals.VolumeOf(grade), 1.15f);
        }

        private void Update()
        {
            _life += Time.deltaTime;
            float t = Mathf.Clamp01(_life / _duration);

            _tickTimer -= Time.deltaTime;
            if (_tickTimer <= 0f)
            {
                _tickTimer = TickInterval;

                Unit.CollectInRadius(transform.position, _radius, Team.Enemy, Buffer);
                for (int i = 0; i < Buffer.Count; i++)
                    Buffer[i].TakeDamage(_tickDamage, true);

                // 타격음은 첫 타에만. 매 틱마다 울리면 장판 하나가 전투 소리를 다 덮는다.
                if (Buffer.Count > 0 && _life < TickInterval)
                    AudioManager.Play(Sfx.HitMeteor, 0.5f, 1.2f);
            }

            // 불꽃이 흔들리게. 완전히 정적인 원판은 배경 장식처럼 보여서 밟게 된다.
            float pulse = 1f + 0.05f * Mathf.Sin(_life * 9f);
            _ring.transform.localScale = Vector3.one * _radius * 2f * pulse;

            // 끝나기 직전에 흐려져서 언제 꺼지는지 예고한다.
            // 예고 없이 사라지면 "아직 있는 줄 알고" 유인한 게 헛수고가 된다.
            float alpha = t > 0.75f ? Mathf.InverseLerp(1f, 0.75f, t) : 1f;
            _ring.color = new Color(_tint.r, _tint.g, _tint.b, alpha);
            _fill.color = new Color(_tint.r, _tint.g, _tint.b, alpha * 0.22f);

            if (t >= 1f) Destroy(gameObject);
        }

        private void OnDestroy()
        {
            // 반복 재생 시트는 이 오브젝트의 자식이 아니다. 여기서 안 끄면 계속 탄다.
            if (_flame != null) Destroy(_flame.gameObject);
        }
    }
}
