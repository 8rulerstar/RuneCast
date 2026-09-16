using UnityEngine;
using RuneCast.Gesture;

namespace RuneCast.Runes
{
    /// <summary>
    /// 마나. 쿨다운이 없는 자원 소모형이라 이 값 하나가 개입 빈도를 전부 결정한다.
    /// 고갈됐을 때 "손 놓고 지켜봐야 하는" 구간이 생기는 게 설계 의도.
    /// </summary>
    public class ManaPool : MonoBehaviour
    {
        /// <summary>
        /// 마나 무제한. 제스처 인식 자체를 먼저 검증해야 하는 단계라,
        /// 자원 제약이 시험 횟수를 막으면 안 된다. 인식률이 잡히면 끄고 밸런스를 본다.
        /// </summary>
        public bool unlimited = true;

        public float max = 100f;
        public float regenPerSecond = 9f;

        public float Current { get; private set; }
        public float Ratio { get { return unlimited ? 1f : (max <= 0f ? 0f : Current / max); } }

        /// <summary>씬에 하나뿐이다. 적이 죽을 때 마나를 주는 쪽이 찾아 쓴다.</summary>
        public static ManaPool Instance { get; private set; }

        private void Awake()
        {
            Instance = this;
            Current = max;
        }

        /// <summary>스테이지를 시작할 때 가득 채운다.</summary>
        public void Refill()
        {
            Current = max;
        }

        private void Update()
        {
            // **전투 시간과 같은 속도로 찬다.**
            //
            // 예전엔 unscaledDeltaTime을 썼다. "슬로우 중에 오래 고민한다고 마나가
            // 덜 차면 이중 처벌"이라는 이유였는데, 구멍이 두 개 있었다:
            //
            //  1. 일시정지 중에도 찼다. 멈추고 10초 기다렸다 풀면 마나가 가득이다.
            //  2. **드래그를 누른 채 가만히 있으면** 시간은 0.2배로 흐르는데 마나는
            //     실시간으로 찼다. 잉크는 움직여야 닳으므로 아무 대가 없이
            //     게임 시간 대비 5배로 마나를 벌 수 있었다.
            //
            // 2장 전체가 "마나가 없으면 지켜볼 수밖에 없다"에 걸려 있는데
            // 그게 통째로 무너지는 구멍이었다.
            //
            // 원래 걱정했던 "고민하는 시간"은 사실 슬로우가 아니다 — 슬로우는 **그리는
            // 동안만** 걸린다. 무엇을 그릴지 정하는 시간은 원래 속도로 흐르므로
            // 이중 처벌이 아니다. 적도 같이 느려지니 비율은 그대로다.
            // 심연(7장)은 시간 회복을 거의 막는다. 대신 적을 잡으면 찬다(Gain).
            Current = Mathf.Min(max,
                Current + regenPerSecond * RuneCast.Meta.ChapterRules.ManaRegenScale * Time.deltaTime);
        }

        /// <summary>
        /// 마나를 즉시 얻는다. 심연(7장)에서 적을 잡을 때 부른다.
        ///
        /// 무제한 판에서는 아무 뜻이 없으므로 조용히 지나간다 — 부르는 쪽이
        /// 판마다 조건을 확인하게 두면 그 확인이 빠지는 곳이 반드시 생긴다.
        /// </summary>
        public void Gain(float amount)
        {
            if (unlimited || amount <= 0f) return;
            Current = Mathf.Min(max, Current + amount);
            LastGainTime = Time.unscaledTime;
        }

        /// <summary>
        /// 마지막으로 회수한 시각. HUD가 게이지를 잠깐 밝히는 데 쓴다.
        ///
        /// **모자라서 튕긴 것과 반대 신호다.** 실패는 붉게 떨고 회수는 밝게
        /// 부푼다 — 같은 게이지에서 일어나므로 방향이 반대여야 헷갈리지 않는다.
        /// </summary>
        public float LastGainTime { get; private set; }

        public static int CostOf(RuneType type)
        {
            switch (type)
            {
                case RuneType.Slash: return 12;  // 제일 빨리 그려지므로 제일 싸다
                case RuneType.Arrow: return 15;
                case RuneType.Vortex: return 22; // 피해가 없는 대신 다른 룬을 강하게 만든다
                case RuneType.Heal: return 25;
                case RuneType.Chain: return 28;
                case RuneType.Shield: return 30;
                case RuneType.Empower: return 32; // 결과를 바꾸지만 아군이 살아 있어야 값을 한다
                case RuneType.Pyre: return 38;    // 총 피해는 제일 크되 다 들어간다는 보장이 없다
                case RuneType.Meteor: return 45;
                case RuneType.Revive: return 55;  // 판을 되돌리는 유일한 수단이라 제일 비싸다
                default: return 0;
            }
        }

        /// <summary>문양의 마나 할인까지 반영한 실제 비용. UI도 이 값을 보여줘야 한다.</summary>
        public static int EffectiveCost(RuneType type)
        {
            return Mathf.Max(1, Mathf.RoundToInt(CostOf(type) * RuneCast.Meta.Loadout.ManaMultiplier(type)));
        }

        public bool CanAfford(RuneType type)
        {
            return unlimited || Current >= EffectiveCost(type);
        }

        /// <summary>
        /// 정해진 양을 쓴다. 카드 배치(DeployBar)가 부른다.
        ///
        /// 룬은 `TrySpend(RuneType)`으로 표에서 값을 찾지만, 카드는 자기 비용을
        /// 들고 있으므로 값을 직접 넘긴다. 부르는 쪽이 이미 낼 수 있는지
        /// 확인한 뒤에 부른다 — 여기서 또 막으면 실패가 두 곳에서 난다.
        /// </summary>
        public void Spend(float amount)
        {
            if (unlimited || amount <= 0f) return;
            Current = Mathf.Max(0f, Current - amount);
        }

        public bool TrySpend(RuneType type)
        {
            if (unlimited) return true;

            int cost = EffectiveCost(type);
            if (Current < cost) return false;
            Current -= cost;
            return true;
        }
    }
}
