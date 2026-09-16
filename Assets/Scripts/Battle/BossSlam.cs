using System.Collections.Generic;
using UnityEngine;
using RuneCast.Core;

namespace RuneCast.Battle
{
    /// <summary>
    /// 보스의 예고된 강타.
    ///
    /// **장의 끝에 마침표를 찍으려고 만들었다.** 지금까지 12판 마지막은
    /// "적이 좀 더 많은 물결"이었고, 그러면 다 깨고도 끝났다는 느낌이 없다.
    ///
    /// 규칙은 하나다 — **크게 휘두르기 전에 멈춰 선다.** 1.2초 동안 몸이
    /// 붉어지고 부풀다가 주변 아군을 한꺼번에 후려친다. 그 사이에 보호막을
    /// 씌우거나 소용돌이로 끌어내면 막을 수 있다.
    ///
    /// 예고 없이 때리는 보스는 만들지 않는다. 맞고 나서야 배우는 건 어려운 게
    /// 아니라 불친절한 것이다. 이 게임에서 플레이어가 할 수 있는 건 개입 하나뿐인데,
    /// 개입할 시간을 안 주면 그 하나를 뺏는 셈이다.
    ///
    /// **왜 시간이 아니라 체력으로 세지 않나:** 체력 기준이면 룬을 아낄수록
    /// 강타가 늦게 와서, 세게 치는 것이 벌이 된다. 이 게임은 개입을 권해야 한다.
    /// </summary>
    public class BossSlam : MonoBehaviour
    {
        /// <summary>강타 사이 간격.</summary>
        private const float Cycle = 7.5f;

        /// <summary>
        /// 첫 강타까지.
        ///
        /// **주기와 같게 두면 한 번도 안 터진다.** 처음 7.5초로 뒀더니
        /// 시뮬레이터에서 강타를 껐다 켜도 결과가 한 자리도 안 달라졌다 —
        /// 보스가 그만큼 오래 살지 못한다. 주술사에서 똑같이 겪었던 함정이다.
        ///
        /// 2.5초면 다가오는 동안 한 번, 붙은 뒤 몇 번을 볼 수 있다.
        /// 등장하자마자 때리지도 않으므로 뭘 하는 놈인지 볼 시간은 있다.
        /// </summary>
        private const float FirstDelay = 2.5f;

        /// <summary>예고 시간. **룬 하나를 그리고도 남아야 한다** —
        /// 별똥별이 1.3초, 보호막이 0.6초쯤 걸린다.</summary>
        private const float Windup = 1.2f;

        /// <summary>
        /// 닿는 거리.
        ///
        /// **처음엔 3.2였는데 그건 대열 전체를 덮는 값이다.** 아군 다섯의 간격이
        /// 1.5라 세로 폭이 6.0칸이고, 가운데에서 3.2면 양 끝까지 닿는다.
        /// 그래서 대열이 조금만 달라져도 전멸이냐 아니냐로 갈렸다 — 물결 사이
        /// 위치 처리를 바꿀 때마다 12판 승률이 100%와 0% 사이를 오갔다.
        ///
        /// 2.4면 서너 명이 든다. **누가 원 안에 있느냐**가 판단거리가 되고,
        /// 대열이 조금 흔들려도 결과가 뒤집히지 않는다.
        /// </summary>
        private const float Radius = 2.4f;

        /// <summary>
        /// 강타 한 번의 피해.
        ///
        /// **시뮬레이터에 맞춰 낮추지 않았다.** 흉내내기는 보호막이나 소용돌이를
        /// 안 쓴다 — 이 보스를 상대하는 핵심 대응을 통째로 빼고 재는 셈이다.
        /// 그래서 시뮬레이터가 3별을 받을 만큼 낮추면 실제로는 너무 쉬워진다.
        ///
        /// 24면 흉내내기 기준으로 만렙에서 2별이다. 남은 별 하나는 강타를
        /// 실제로 막아낸 사람 몫으로 둔다 — 마지막 판의 3별은 강화만으로
        /// 가져갈 수 있는 것이 아니어야 한다.
        ///
        /// 실측(강화 1.0/1.6/2.2 순, 생존 수):
        ///   34 → 1.67 / 3.00 / 4.03   너무 세다. 만렙에서도 2별이 빠듯
        ///   24 → 2.70 / 3.40 / 4.50   여기
        ///   16 → 3.00 / 4.03 / 5.00   너무 약하다. 안 막아도 3별
        /// </summary>
        private const float Damage = 24f;

        private static readonly List<Unit> Buffer = new List<Unit>();

        private Unit _unit;
        private SpriteRenderer _ring;
        private float _timer = Cycle - FirstDelay;

        private void Awake()
        {
            _unit = GetComponent<Unit>();

            // 닿는 범위를 그대로 보여준다. 여기 서 있으면 맞는다는 뜻이고,
            // 그게 정확해야 물러설지 막을지 고를 수 있다.
            _ring = PrimitiveSprites.Spawn("SlamRing", PrimitiveSprites.Ring(96, 0.07f),
                new Color(1f, 0.4f, 0.3f, 0f), 12, transform);
            _ring.transform.localScale = Vector3.one * Radius * 2f;
        }

        private void Update()
        {
            if (_unit == null || !_unit.IsAlive)
            {
                if (_ring != null) _ring.enabled = false;
                return;
            }

            _timer += Time.deltaTime;
            float toSlam = Cycle - _timer;

            if (toSlam > Windup)
            {
                // 쉬는 동안에는 고리를 감춘다. 늘 떠 있으면 경고가 배경이 된다.
                SetRing(0f, Radius * 2f);
                return;
            }

            // 차오르는 중 — 고리가 조여들면서 진해진다. 줄어드는 원은
            // "시간이 줄고 있다"를 글자 없이 말한다.
            float k = 1f - Mathf.Clamp01(toSlam / Windup);
            SetRing(Mathf.Lerp(0.25f, 0.95f, k), Radius * 2f * Mathf.Lerp(1.25f, 1f, k));

            if (_timer < Cycle) return;

            _timer = 0f;
            Slam();
        }

        private void SetRing(float alpha, float scale)
        {
            if (_ring == null) return;
            _ring.color = new Color(1f, 0.4f, 0.3f, alpha);
            _ring.transform.localScale = Vector3.one * scale;
        }

        private void Slam()
        {
            Unit.CollectInRadius(transform.position, Radius, Team.Hero, Buffer);

            for (int i = 0; i < Buffer.Count; i++)
                Buffer[i].TakeDamage(Damage);

            // 맞은 사람이 없어도 연출은 낸다. 피했다는 것도 결과다 —
            // 아무 일도 안 일어나면 잘 피한 건지 그냥 안 온 건지 모른다.
            BurstFx.Play(Vfx.MeteorExplosion, transform.position, Radius * 1.6f,
                new Color(1f, 0.62f, 0.45f), 48);
            // 보스 강타에만 다른 팩(Kyeoms)의 폭발을 쓴다. 별똥별과 같은 그림이면
            // "내 룬이 떨어졌나?"가 된다 — 위협은 내 기술과 화면에서 갈려야 한다.
            ParticleFx.Spawn(Pfx.BossBlast, transform.position, 0.9f);
            CameraShake.Shake(0.45f, 0.35f);
            AudioManager.Play(Sfx.HitMeteor, 1f, 0.6f);

            SetRing(0f, Radius * 2f);
        }
    }
}
