using System.Collections.Generic;
using UnityEngine;
using RuneCast.Battle;
using RuneCast.Core;
using RuneCast.Gesture;

namespace RuneCast.Runes
{
    /// <summary>
    /// 무한대 ∞ — 범위 안 아군의 공격력·공격속도를 올린다.
    ///
    /// 룬 여섯 개가 전부 "적을 어떻게 할까"(피해·둔화) 아니면 "아군을 어떻게 버틸까"
    /// (회복·보호막)였다. **아군을 강하게 만드는 수단은 하나도 없었다.**
    /// 그래서 아군이 이길 수 없는 조합이 나오면 플레이어가 할 수 있는 게 지연뿐이었다.
    ///
    /// 버는 시간이 아니라 결과를 바꾸는 룬이라 지속시간을 짧게 잡았다.
    /// 오래 걸리면 전투 시작에 한 번 깔고 잊는 룬이 된다 —
    /// 그건 "개입"이 아니라 "준비"라서 이 게임이 재미있는 지점이 아니다.
    /// </summary>
    public class EmpowerAura : MonoBehaviour
    {
        private static readonly List<Unit> Buffer = new List<Unit>();
        private static readonly Color Base = new Color(1f, 0.82f, 0.35f);

        private readonly List<SpriteRenderer> _rings = new List<SpriteRenderer>();
        private float _life;
        private float _duration = 0.6f;
        private float _radius;
        private Color _tint;

        public static void Cast(Vector3 center, float radius, float damageMultiplier,
            float hasteMultiplier, float duration, RuneGrade grade)
        {
            Unit.CollectInRadius(center, radius, Team.Hero, Buffer);
            for (int i = 0; i < Buffer.Count; i++)
                Buffer[i].Empower(damageMultiplier, hasteMultiplier, duration);

            RuneCast.Meta.Achievements.OnEmpower(Buffer.Count);

            var go = new GameObject("EmpowerAura");
            FxRoot.Adopt(go);
            go.transform.position = center;

            var fx = go.AddComponent<EmpowerAura>();
            fx._radius = radius;
            fx._tint = Color.Lerp(Base, GradeVisuals.ColorOf(grade), 0.3f);

            // 고리 두 개가 서로 반대로 도는 8자. 그린 도형이 그대로 남아야
            // 무엇이 발동했는지 알아보기 쉽다.
            for (int i = 0; i < 2; i++)
            {
                var sr = PrimitiveSprites.Spawn("Loop", PrimitiveSprites.Ring(96, 0.14f), fx._tint, 40, go.transform);
                sr.transform.localPosition = new Vector3((i == 0 ? -0.5f : 0.5f) * radius, 0f, 0f);
                fx._rings.Add(sr);
            }

            // 실제로 누가 강해졌는지. 범위만 보여주면 가장자리 유닛이 들어갔는지 알 수 없다.
            for (int i = 0; i < Buffer.Count; i++)
            {
                BurstFx.Play(Vfx.EmpowerWave, Buffer[i].transform.position,
                    1.2f + 0.4f * RuneGrading.Flourish(grade), fx._tint, 46);
                // 룬 문자가 도는 오라(CFXR Magic Aura Runic). 룬 게임의 버프에
                // 이보다 맞는 그림이 없다 — 팩을 뒤졌을 때 이걸 보고 골랐다.
                ParticleFx.Spawn(Pfx.Empower, Buffer[i].transform.position, 0.55f);
                FloatingText.Label(Buffer[i].transform.position + new Vector3(0f, 0.55f, 0f),
                    string.Format("×{0:F2}", damageMultiplier), Base, 16f);
            }

            // 전용 클립이 없어서 보호막 소리를 높은 피치로 쓴다.
            // 같은 파일이라도 피치가 3도 이상 벌어지면 다른 사건으로 들린다.
            AudioManager.Play(Sfx.CastEmpower, GradeVisuals.VolumeOf(grade), 1.35f);
        }

        private void Update()
        {
            _life += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(_life / _duration);

            for (int i = 0; i < _rings.Count; i++)
            {
                float s = Mathf.Lerp(0.2f, 1.05f, t) * _radius;
                _rings[i].transform.localScale = new Vector3(s, s, 1f);
                _rings[i].color = new Color(_tint.r, _tint.g, _tint.b, 1f - t);
            }

            // 위로 떠오르게 — 힘이 올라간다는 방향을 준다
            transform.position += Vector3.up * 0.7f * Time.unscaledDeltaTime;

            if (t >= 1f) Destroy(gameObject);
        }
    }
}
