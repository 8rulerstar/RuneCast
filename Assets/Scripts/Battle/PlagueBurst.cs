using System.Collections.Generic;
using UnityEngine;
using RuneCast.Core;
using RuneCast.Meta;

namespace RuneCast.Battle
{
    /// <summary>
    /// 역병(5장) — 적이 죽을 때 가까운 아군이 조금 다친다.
    ///
    /// **지금까지 광역은 언제나 이득이었다.** 소용돌이로 모아서 별똥별을 떨구는
    /// 것이 늘 정답이었고, 그 조합을 만들려고 소용돌이에서 피해를 뺐던 것이기도
    /// 하다. 여기서는 그 습관이 처음으로 벌을 받는다 — 뭉친 적을 한 번에
    /// 쓸어담으면 그 자리에 있던 아군이 함께 깎인다.
    ///
    /// 별이 생존 수라서 이건 곧바로 점수 손해다. 죽이는 속도와 지키는 것이
    /// 처음으로 어긋나는 장이다.
    ///
    /// **피해를 작게 잡았다(6).** 크게 잡으면 답이 "광역을 쓰지 마라"로 굳어서
    /// 룬 절반이 죽는다. 물어야 할 것은 "쓰지 마라"가 아니라 **"어디서 터뜨릴까"**다.
    /// </summary>
    public class PlagueBurst : MonoBehaviour
    {
        private static readonly List<Unit> Buffer = new List<Unit>();

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
            // 한 번만. 쪼개지는 적(거미)은 죽음이 여러 번 오갈 수 있다.
            if (_fired) return;
            _fired = true;

            float dmg = ChapterRules.PlagueDamage;
            if (dmg <= 0f) return;

            Vector3 at = transform.position;

            Buffer.Clear();
            Unit.CollectInRadius(at, ChapterRules.PlagueRadius, Team.Hero, Buffer);

            int hit = 0;
            for (int i = 0; i < Buffer.Count; i++)
            {
                if (Buffer[i] == null || !Buffer[i].IsAlive) continue;
                Buffer[i].TakeDamage(dmg);
                hit++;
            }

            // **아무도 안 닿았으면 아무 연출도 하지 않는다.**
            // 멀리서 죽은 적마다 초록 폭발이 터지면 화면이 그걸로 덮이고,
            // 그러면 "가까이서 죽으면 다친다"는 규칙이 오히려 안 읽힌다.
            if (hit == 0) return;

            // **터지는 걸 보여준다.** 체력이 조용히 깎이면 플레이어는 자기가 한
            // 일과 이어 붙이지 못하고 그냥 "아군이 약하다"고 느낀다.
            // 초록빛으로 — 룬 피해(금색)·평타(흰색)와 색으로 갈린다.
            BurstFx.Play(Vfx.SmallHit, at, 1.5f, new Color(0.55f, 0.9f, 0.35f), 46);
        }
    }
}
