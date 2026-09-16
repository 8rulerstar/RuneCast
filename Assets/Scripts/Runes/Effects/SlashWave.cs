using System.Collections.Generic;
using UnityEngine;
using RuneCast.Battle;
using RuneCast.Core;

namespace RuneCast.Runes
{
    /// <summary>
    /// 직선 ╱ — 그은 선을 따라 띠 모양 피해.
    ///
    /// 다른 룬은 "어디에 그렸나"만 쓰지만 이건 **그은 선분 자체**를 쓴다.
    /// 짧게 그으면 좁게, 길게 그으면 여러 명을 한 번에 벤다 —
    /// 손이 한 일과 화면에서 일어난 일이 정확히 겹쳐서 제일 직관적인 룬이다.
    /// </summary>
    public class SlashWave : MonoBehaviour
    {
        private static readonly List<Unit> Buffer = new List<Unit>();
        private static readonly Color Tint = new Color(1f, 0.85f, 0.95f);

        private SpriteRenderer _beam;
        private float _life;
        private float _length;
        private float _thickness;
        private Color _tint = Tint;
        private const float Duration = 0.3f;

        public static void Cast(Vector3 from, Vector3 to, float halfWidth, float damage, RuneCast.Gesture.RuneGrade grade)
        {
            Vector3 seg = to - from;
            float len = seg.magnitude;
            if (len < 0.05f) return;

            Vector3 dir = seg / len;

            // 선분과의 수직 거리로 판정. 원형 범위로 하면 "그은 선"이라는 느낌이 사라진다.
            Buffer.Clear();
            for (int i = 0; i < Unit.All.Count; i++)
            {
                Unit u = Unit.All[i];
                if (u == null || !u.IsAlive || u.team != Team.Enemy) continue;

                Vector3 rel = u.transform.position - from;
                float along = Mathf.Clamp(Vector3.Dot(rel, dir), 0f, len);
                Vector3 nearest = from + dir * along;
                if ((u.transform.position - nearest).sqrMagnitude <= halfWidth * halfWidth)
                    Buffer.Add(u);
            }

            var go = new GameObject("SlashWave");
            FxRoot.Adopt(go);
            go.transform.position = (from + to) * 0.5f;
            go.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);

            float flourish = RuneCast.Gesture.RuneGrading.Flourish(grade);
            Color tint = Color.Lerp(Tint, GradeVisuals.ColorOf(grade), 0.5f);

            var fx = go.AddComponent<SlashWave>();
            fx._tint = tint;
            fx._beam = PrimitiveSprites.Spawn("Beam", PrimitiveSprites.Square(8), tint, 43, go.transform);
            fx._length = len;
            fx._thickness = halfWidth * 1.6f;
            fx._beam.transform.localScale = new Vector3(fx._length, fx._thickness, 1f);

            // 벤 자국을 따라 섬광. 궤적과 같은 각도로 눕혀야 "베었다"로 읽힌다.
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            BurstFx.Play(Vfx.SlashStreak, (from + to) * 0.5f, Mathf.Max(len, 1f) * 1.2f,
                tint, 45, false, 0f, angle);

            // 교차 검격 파티클을 가운데 한 방. 선을 따라 여러 개 깔면
            // 파티클끼리 겹쳐 하얗게 떠 버린다 — 절정은 한 곳이면 된다.
            ParticleFx.Spawn(Pfx.Slash, (from + to) * 0.5f, 0.7f + 0.3f * flourish);

            // 등급이 높으면 선을 따라 추가 섬광을 흩뿌린다
            int sparks = Mathf.RoundToInt(flourish * 4f);
            for (int i = 0; i < sparks; i++)
            {
                Vector3 p = from + dir * (len * ((i + 0.5f) / Mathf.Max(sparks, 1)));
                BurstFx.Play(Vfx.SmallHit, p, 0.9f, tint, 45);
            }

            for (int i = 0; i < Buffer.Count; i++)
            {
                BurstFx.Play(Vfx.ArrowImpact, Buffer[i].transform.position,
                    1.3f + 0.4f * flourish, tint, 46);
                Buffer[i].TakeDamage(damage, true);
            }

            AudioManager.Play(Sfx.CastArrow, GradeVisuals.VolumeOf(grade));
        }

        private void Update()
        {
            _life += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(_life / Duration);

            // 두께만 줄여서 사라지게 — 길이가 줄면 어디까지 벴는지를 잘못 알려주게 된다.
            // 매 프레임 현재 값에서 Lerp하면 감쇠가 누적되므로 항상 원래 두께에서 보간한다.
            _beam.transform.localScale = new Vector3(_length, Mathf.Lerp(_thickness, 0f, t), 1f);

            var c = _tint;
            c.a = 1f - t;
            _beam.color = c;

            if (t >= 1f) Destroy(gameObject);
        }
    }
}
