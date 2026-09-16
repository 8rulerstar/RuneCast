using UnityEngine;

namespace RuneCast.Battle
{
    /// <summary>
    /// 가장 가까운 적으로 걸어가서 사거리에 들면 때린다. 그게 전부다.
    /// 계획서대로 "지켜만 봐도 뭐가 일어나는지 읽히는" 최소선만 구현한다.
    /// </summary>
    [RequireComponent(typeof(Unit))]
    public class UnitAI : MonoBehaviour
    {
        public enum State { Advancing, Engaging }

        public State CurrentState { get; private set; }

        [Tooltip("적이 하나도 없을 때 전진할 방향. 히어로는 오른쪽, 적은 왼쪽.")]
        public Vector2 marchDirection = Vector2.right;

        private Unit _unit;
        private Unit _target;
        private float _attackCooldown;
        private float _retargetTimer;

        private void Awake()
        {
            _unit = GetComponent<Unit>();
            marchDirection = _unit.team == Team.Hero ? Vector2.right : Vector2.left;
            // 같은 프레임에 전원이 동시에 때리면 리듬이 기계적으로 보인다
            _attackCooldown = Random.Range(0f, _unit.attackInterval);
        }

        private void Update()
        {
            if (!_unit.IsAlive) return;

            _attackCooldown -= Time.deltaTime;
            _retargetTimer -= Time.deltaTime;

            // 매 프레임 전체 탐색은 낭비. 0.2초마다면 충분히 반응한다.
            if (_target == null || !_target.IsAlive || _retargetTimer <= 0f)
            {
                _target = Unit.NearestEnemyOf(_unit);
                _retargetTimer = 0.2f;
            }

            float speed = _unit.moveSpeed * _unit.SpeedMultiplier;

            if (_target == null)
            {
                CurrentState = State.Advancing;

                // **아군은 목표가 없으면 그 자리에 선다.**
                //
                // 물결이 끝나는 순간 BattleManager가 대열을 시작선으로 되돌리므로
                // (ReformLine), 여기서 따로 걸어 돌아갈 필요가 없다. 이 분기는
                // 되돌아온 뒤 다음 물결을 기다리는 동안에 해당한다.
                //
                // 앞서 세 가지를 해봤고 전부 어색했다 — 계속 행군(오른쪽으로
                // 밀림), 걸어서 복귀(물러서는 것처럼 보임), 전선까지 전진
                // (되돌린 대열을 도로 흐트러뜨림).
                if (_unit.team == Team.Hero)
                {
                    CurrentState = State.Engaging;
                    return;
                }

                transform.position += (Vector3)(marchDirection.normalized * speed * Time.deltaTime);
                return;
            }

            Vector3 toTarget = _target.transform.position - transform.position;
            float dist = toTarget.magnitude;

            if (dist > _unit.attackRange)
            {
                CurrentState = State.Advancing;
                transform.position += toTarget / Mathf.Max(dist, 1e-4f) * speed * Time.deltaTime;
            }
            else
            {
                CurrentState = State.Engaging;

                // 공격력이 0인 유닛(주술사)은 휘두르지 않는다. 그냥 두면 타격
                // 애니메이션과 타격음이 계속 나면서 피해는 0이라, 때리는데 안
                // 아픈 것처럼 보인다 — 화면이 거짓말을 한다.
                if (_unit.attackDamage <= 0f) return;

                if (_attackCooldown <= 0f)
                {
                    _unit.NotifyAttack();

                    if (_unit.ranged)
                    {
                        // **날아가는 것이 보여야 한다.** 멀리서 즉시 피해가 들어가면
                        // 어디서 맞았는지 알 수가 없고, 그러면 저 적을 먼저 잡아야
                        // 한다는 판단 자체가 안 선다.
                        Vector2 dir = toTarget / Mathf.Max(dist, 1e-4f);
                        Projectile.Spawn(transform.position + (Vector3)(dir * 0.3f), dir,
                            _unit.attackDamage * _unit.AttackMultiplier, Team.Hero,
                            new Color(1f, 0.55f, 0.42f), false);

                        RuneCast.Core.AudioManager.Play(RuneCast.Core.Sfx.CastArrow, 0.32f, 0.85f);
                    }
                    else
                    {
                        // 근접 타격음. 여러 유닛이 동시에 때리므로 AudioManager가
                        // 최소 간격으로 솎아낸다 — 안 그러면 소리가 뭉개진다.
                        RuneCast.Core.AudioManager.Play(RuneCast.Core.Sfx.HitMelee, 0.4f);

                        _target.TakeDamage(_unit.attackDamage * _unit.AttackMultiplier);
                    }

                    _attackCooldown = _unit.attackInterval / _unit.HasteMultiplier;
                }
            }
        }
    }
}
