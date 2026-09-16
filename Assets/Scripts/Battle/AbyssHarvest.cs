using UnityEngine;
using RuneCast.Meta;
using RuneCast.Runes;

namespace RuneCast.Battle
{
    /// <summary>
    /// 심연(7장) — 적이 죽으면 마나가 찬다.
    ///
    /// **2장의 정반대다.** 2장은 "마나가 유한하니 아껴 써라"였다. 여기서는
    /// 시간으로는 거의 안 차고(<see cref="ChapterRules.ManaRegenScale"/> 0.35)
    /// 잡아야만 찬다 — 아끼면 말라 죽는다.
    ///
    /// 같은 자원 하나로 정반대의 태도를 요구하는 것이 마지막 장에 맞는다.
    /// 새 자원을 만들었으면 배울 것이 하나 더 늘었을 뿐이지만, 이미 아는 것의
    /// 뜻이 뒤집히면 지금까지의 습관을 다시 봐야 한다.
    ///
    /// 유닛에 붙여 두는 이유는 <see cref="PlagueBurst"/>와 같다 — 죽을 때
    /// 일어나는 일은 죽는 쪽이 들고 있어야 구독 해제가 저절로 맞는다.
    /// </summary>
    public class AbyssHarvest : MonoBehaviour
    {
        private Unit _unit;
        private bool _fired;

        private void Start()
        {
            _unit = GetComponent<Unit>();
            if (_unit == null) { enabled = false; return; }
            _unit.Died += OnDied;
        }

        private void OnDestroy()
        {
            if (_unit != null) _unit.Died -= OnDied;
        }

        private void OnDied(Unit u)
        {
            if (_fired) return;
            _fired = true;

            float gain = ChapterRules.ManaPerKill;
            if (gain <= 0f || ManaPool.Instance == null) return;

            ManaPool.Instance.Gain(gain);

            // **회수되는 게 보여야 규칙이 된다.**
            //
            // 마나가 시간으로 안 차는 건 화면에서 "아무 일도 없음"이라, 알려주지
            // 않으면 플레이어는 그냥 마나가 부족한 판이라고 느낀다. 잡은 자리에서
            // 숫자가 떠올라 게이지로 이어져야 "잡으면 찬다"가 손에 붙는다.
            //
            // 마나색(연보라)으로 띄운다 — 피해(금색·흰색)와 섞이면 안 된다.
            RuneCast.Core.FloatingText.Label(
                transform.position + new Vector3(0f, 0.5f, 0f),
                "+" + Mathf.RoundToInt(gain),
                new Color(0.78f, 0.72f, 1f), 26f);

            // 게이지가 밝아지는 건 ManaPool.Gain 이 시각을 남기고 HUD가 읽는다 —
            // 숫자는 죽은 자리에 뜨고 게이지는 화면 아래라, 둘을 이어 주지 않으면
            // 시선이 옮겨 가지 않는다.
        }
    }
}
