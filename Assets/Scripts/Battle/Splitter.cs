using UnityEngine;
using RuneCast.Core;

namespace RuneCast.Battle
{
    /// <summary>
    /// 거미가 죽으면 새끼 둘로 쪼개진다.
    ///
    /// **광역기의 뜻을 바꾼다.** 지금까지 별똥별은 "여럿을 한 번에 지운다"였다.
    /// 거미가 섞이면 같은 한 방이 오히려 수를 늘린다 — 셋을 쓸어서 여섯을 만든다.
    ///
    /// 그러면 고를 것이 생긴다. 아군이 앞줄을 정리한 뒤에 터뜨릴지, 지금
    /// 터뜨리고 새끼는 아군에게 맡길지. 망령·주술사·놀이 전부 **어디를** 칠지를
    /// 묻는 반면 이쪽은 **언제** 칠지를 묻는다. 같은 질문만 반복하면 적을 늘려도
    /// 전장은 그대로다.
    ///
    /// 새끼는 다시 안 쪼개진다. 끝없이 늘어나면 정리할 방법이 없고, 그건
    /// 어려운 게 아니라 못 이기는 것이다.
    /// </summary>
    public class Splitter : MonoBehaviour
    {
        /// <summary>쪼개져 나오는 수.</summary>
        private const int Shards = 2;

        /// <summary>좌우로 벌어지는 거리. 겹쳐 나오면 둘인지 하나인지 안 보인다.</summary>
        private const float Spread = 0.5f;

        private Unit _unit;
        private float _hpScale = 1f;
        private float _dmgScale = 1f;
        private bool _done;

        /// <summary>
        /// 웨이브 배수를 물려받는다.
        ///
        /// 안 넘기면 후반 스테이지에서 어미만 세지고 새끼는 기본값으로 나온다.
        /// 그러면 쪼개는 순간 판이 갑자기 쉬워져서, 이 적의 뜻이 뒤집힌다.
        /// </summary>
        public void Configure(float hpScale, float dmgScale)
        {
            _hpScale = hpScale;
            _dmgScale = dmgScale;
        }

        private void Awake()
        {
            _unit = GetComponent<Unit>();
            if (_unit != null) _unit.Dying += OnDying;
        }

        private void OnDestroy()
        {
            if (_unit != null) _unit.Dying -= OnDying;
        }

        private void OnDying()
        {
            // Dying은 한 번만 오지만, 여기서 새 유닛을 만드는 만큼 확실히 막는다.
            if (_done) return;
            _done = true;

            // 전투가 끝난 뒤에는 쪼개지지 않는다. 마지막 거미를 잡아 물결을
            // 정리했는데 새끼가 튀어나오면 클리어가 취소된 것처럼 보인다.
            BattleManager bm = BattleManager.Instance;
            if (bm == null || !bm.Running) return;

            Vector3 at = transform.position;

            for (int i = 0; i < Shards; i++)
            {
                float side = i == 0 ? -1f : 1f;
                Vector3 p = at + new Vector3(0.1f, side * Spread, 0f);
                bm.SpawnEnemy(UnitKind.Spiderling, p, _hpScale, _dmgScale);
            }

            // 쪼개지는 게 보여야 한다. 소리 없이 수가 늘면 버그로 읽힌다.
            BurstFx.Play(Vfx.SmallHit, at, 1.5f, new Color(0.78f, 0.62f, 1f), 47);
            AudioManager.Play(Sfx.DeathEnemy, 0.55f, 1.4f);
        }
    }
}
