using System.Collections.Generic;
using UnityEngine;
using RuneCast.Core;

namespace RuneCast.Battle
{
    /// <summary>
    /// 주술사가 주변 적을 주기적으로 회복시킨다.
    ///
    /// **왜 이 적을 만들었나:** 적들이 능력치만 다르고 행동은 전부 같았다.
    /// 가까운 것을 향해 걸어가서 때린다 — 오크도 해골도 본로드도. 그러면
    /// 전장에 읽을 게 없다. 어디를 칠지 고를 이유가 없으니 개입은 "마나가
    /// 차면 제일 센 걸 쓴다"로 굳는다.
    ///
    /// 주술사는 **표적을 고르게 만든다.** 얘를 놔두면 아군이 깎아 놓은 체력이
    /// 계속 되돌아온다. 그럼 앞줄부터 치던 손이 뒤를 보게 된다.
    ///
    /// **아군은 이 적을 못 잡는다** — 구조적으로. 아군은 가장 가까운 적을
    /// 노리는데 주술사는 사거리가 길어 한참 뒤에 선다. 앞줄이 다 죽기 전에는
    /// 아무도 거기까지 안 간다. 망령이 "평타가 안 통해서" 플레이어 몫이라면,
    /// 주술사는 "손이 안 닿아서" 플레이어 몫이다. 규칙을 새로 배울 게 없다.
    ///
    /// **반드시 예고한다.** 회복이 소리 없이 터지면 플레이어는 체력이 왜 도로
    /// 찼는지 모른 채 계속 앞줄만 친다. 1초간 고리가 부풀고 소리가 올라간 뒤에
    /// 터지므로, 그 사이에 끊을 수 있다 — 그게 이 적의 전부다.
    /// </summary>
    public class ShamanAura : MonoBehaviour
    {
        /// <summary>
        /// 회복이 터지는 간격.
        ///
        /// **물결 하나가 지속되는 시간보다 짧아야 한다.** 처음엔 3.4초로 뒀는데
        /// 마지막 물결에서 주술사가 살아 있는 게임 시간이 2.8초라, 주기가
        /// 0.17초 차이로 안 차서 **한 번도 안 터졌다.** 시전 슬로우 때문에
        /// 게임 시간이 실제 시간의 7할 정도로 흐르는 것도 겹쳤다.
        /// </summary>
        private const float Cycle = 2.2f;

        /// <summary>
        /// 첫 회복까지. 주기보다 짧게 잡는다 — 소환되고 한참 아무것도 안 하면
        /// 플레이어가 저게 뭘 하는 적인지 배울 기회 자체가 없다.
        /// </summary>
        private const float FirstDelay = 1.4f;

        /// <summary>터지기 전에 부풀어 오르는 시간. 이 안에 죽이면 아무 일도 없다.</summary>
        private const float Windup = 0.8f;

        /// <summary>
        /// 회복이 닿는 거리.
        ///
        /// **주술사가 서는 거리(3.8)와 앞줄(1.1) 사이가 2.7쯤 벌어진다.**
        /// 반경이 그보다 작으면 회복이 아무에게도 안 닿아서 아무 일도 안 하는
        /// 적이 된다 — 처음 4.6/3.2로 뒀을 때 실제로 그랬다.
        ///
        /// 4.4에서 3.6으로 줄인 건 **고리가 진실을 보여주게 하려고**다.
        /// 고리 크기는 이 프로젝트 규칙상 반경×2인데(PyreField·Vortex와 같다),
        /// 4.4면 지름 8.8로 화면 절반을 덮는다. 3.6이면 앞줄에는 여전히 닿으면서
        /// 고리가 읽을 만한 크기가 된다.
        /// </summary>
        private const float Radius = 3.6f;
        private const float HealAmount = 22f;

        /// <summary>쉬는 동안의 고리 크기. 몸을 감싸는 정도 — "저기 뭔가 있다"만 알린다.</summary>
        private const float IdleScale = 1.1f;

        private static readonly Color Calm = new Color(0.45f, 0.95f, 0.6f, 0.35f);
        private static readonly Color Charged = new Color(0.65f, 1f, 0.75f, 0.9f);

        private static readonly List<Unit> Buffer = new List<Unit>();

        private Unit _unit;
        private SpriteRenderer _ring;
        private float _timer = Cycle - FirstDelay;

        private void Awake()
        {
            _unit = GetComponent<Unit>();

            _ring = PrimitiveSprites.Spawn("ShamanRing", PrimitiveSprites.Ring(96, 0.08f), Calm, 11, transform);
            _ring.transform.localScale = Vector3.one * IdleScale;
        }

        private void Update()
        {
            if (_unit == null || !_unit.IsAlive)
            {
                if (_ring != null) _ring.enabled = false;
                return;
            }

            _timer += Time.deltaTime;

            float toBurst = Cycle - _timer;

            if (toBurst > Windup)
            {
                // 쉬는 동안에도 옅게 보여야 "저기 뭔가 있다"가 읽힌다.
                _ring.color = Calm;
                _ring.transform.localScale = Vector3.one * IdleScale;
                return;
            }

            // 차오르는 중 — 커지고 밝아진다. 눈이 잡는 건 색보다 크기 변화다.
            float k = 1f - Mathf.Clamp01(toBurst / Windup);
            _ring.color = Color.Lerp(Calm, Charged, k);
            // **끝까지 부풀면 실제 회복 범위와 같아진다.** 예전엔 반경의 0.62배까지만
            // 커져서, 고리 밖에 있는 적도 회복됐다. 화면이 거짓말을 하면 표식이
            // 없느니만 못하다 — 플레이어가 "저 밖은 안전하다"고 잘못 배운다.
            _ring.transform.localScale = Vector3.one * Mathf.Lerp(IdleScale, Radius * 2f, k * k);

            if (_timer < Cycle) return;

            _timer = 0f;
            Burst();
        }

        private void Burst()
        {
            // **자기 편을 회복시킨다.** 예전엔 Team.Enemy로 박혀 있었다 —
            // 적 전용 능력이었으니 맞는 값이었지만, 아군 수도사가 같은 일을
            // 하게 되면서 주인의 편을 따라가야 한다. 규칙이 양쪽에서 같으므로
            // 플레이어는 주술사에게서 배운 것을 그대로 읽으면 된다.
            Unit owner = GetComponent<Unit>();
            Unit.CollectInRadius(transform.position, Radius,
                owner != null ? owner.team : Team.Enemy, Buffer);

            int healed = 0;
            for (int i = 0; i < Buffer.Count; i++)
            {
                if (Buffer[i] == _unit) continue;   // 자기 자신은 안 고친다
                Buffer[i].Heal(HealAmount);
                healed++;
            }

            // 아무도 못 고쳤어도 연출은 낸다. 안 그러면 "혼자 남은 주술사"가
            // 아무 일도 안 하는 것처럼 보여서, 굳이 잡을 이유가 없어 보인다.
            BurstFx.Play(Vfx.HealWave, transform.position, Radius * 1.1f,
                new Color(0.6f, 1f, 0.7f), 24);
            AudioManager.Play(Sfx.CastHeal, healed > 0 ? 0.5f : 0.3f, 0.78f);

            _ring.color = Calm;
            _ring.transform.localScale = Vector3.one * IdleScale;
        }
    }
}
