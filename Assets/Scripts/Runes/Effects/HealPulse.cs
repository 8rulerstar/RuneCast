using System.Collections.Generic;
using UnityEngine;
using RuneCast.Battle;
using RuneCast.Core;
using RuneCast.Gesture;

namespace RuneCast.Runes
{
    /// <summary>
    /// 원 ○ — 그린 원 안의 아군을 즉시 회복.
    ///
    /// 등급이 올라가면 링이 여러 겹으로 퍼진다. 회복량 숫자는 화면에 안 보이므로,
    /// "잘 그렸다"를 전달하는 건 사실상 이 연출뿐이다.
    /// </summary>
    public class HealPulse : MonoBehaviour
    {
        private static readonly List<Unit> Buffer = new List<Unit>();
        private static readonly Color Base = new Color(0.45f, 1f, 0.6f);

        private readonly List<SpriteRenderer> _rings = new List<SpriteRenderer>();
        private float _life;
        private float _duration = 0.5f;
        private float _radius;
        private Color _tint;

        public static void Cast(Vector3 center, float radius, float healAmount, RuneGrade grade)
        {
            Unit.CollectInRadius(center, radius, Team.Hero, Buffer);
            for (int i = 0; i < Buffer.Count; i++) Buffer[i].Heal(healAmount);

            var go = new GameObject("HealPulse");
            FxRoot.Adopt(go);
            go.transform.position = center;

            var fx = go.AddComponent<HealPulse>();
            fx._radius = radius;

            // 등급색을 힐 고유색에 섞는다. 완전히 등급색으로 바꾸면 무슨 룬인지 안 읽힌다.
            fx._tint = Color.Lerp(Base, GradeVisuals.ColorOf(grade), 0.35f);

            // 링 1겹(조잡) ~ 3겹(완벽)
            int ringCount = 1 + Mathf.RoundToInt(RuneGrading.Flourish(grade) * 2f);
            for (int i = 0; i < ringCount; i++)
            {
                var sr = PrimitiveSprites.Spawn("Ring", PrimitiveSprites.Ring(96, 0.12f), fx._tint, 40, go.transform);
                fx._rings.Add(sr);
            }

            // 범위 전체를 덮는 물결. 링이 '어디까지'를 알려준다면 이건 '무슨 일이 일어났나'를 알려준다.
            //
            // **작고 옅게 쓴다.** 예전엔 반경의 2.4배로 깔았는데, 이 그림은
            // 사실 타오르는 파란 불꽃이라 그 크기로 덮으면 회복이 아니라
            // 지속 피해처럼 보였다. 라이브러리에 회복에 맞는 그림이 없으므로
            // 크기와 진하기를 줄여 **고리가 주인공이 되게** 했다.
            var wave = fx._tint;
            wave.a *= 0.45f;
            BurstFx.Play(Vfx.HealWave, center, radius * 1.15f, wave, 41);

            // 회복된 유닛마다 반짝임. 누가 실제로 회복됐는지가 링만으로는 안 읽힌다.
            // 시트의 SmallHit은 폭발 그림이라 "맞았다"에 가까웠다 — 위로 흩어지는
            // 빛 파티클(CFXR Hit Light)을 겹쳐 "좋은 일"쪽으로 기울인다.
            for (int i = 0; i < Buffer.Count; i++)
            {
                BurstFx.Play(Vfx.SmallHit, Buffer[i].transform.position,
                    1.1f + 0.4f * RuneGrading.Flourish(grade), fx._tint, 46);
                ParticleFx.Spawn(Pfx.Heal, Buffer[i].transform.position, 0.5f);
            }
        }

        private void Update()
        {
            _life += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(_life / _duration);

            for (int i = 0; i < _rings.Count; i++)
            {
                // 겹마다 시간차를 둬서 물결처럼 번지게
                float lag = i * 0.14f;
                float rt = Mathf.Clamp01((_life - lag) / _duration);
                if (rt <= 0f)
                {
                    _rings[i].color = new Color(_tint.r, _tint.g, _tint.b, 0f);
                    continue;
                }

                float s = Mathf.Lerp(0.25f, 1f, rt) * _radius * 2f;
                _rings[i].transform.localScale = new Vector3(s, s, 1f);
                _rings[i].color = new Color(_tint.r, _tint.g, _tint.b, 1f - rt);
            }

            if (t >= 1f && _life > _duration + _rings.Count * 0.14f) Destroy(gameObject);
        }
    }
}
