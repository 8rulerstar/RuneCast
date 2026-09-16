using UnityEngine;
using RuneCast.Core;

namespace RuneCast.Battle
{
    /// <summary>
    /// 유닛 스프라이트 애니메이션. Animator 컨트롤러 대신 코드로 프레임을 돌린다.
    ///
    /// .controller는 바이너리에 가까운 YAML이라 병합이 안 되고, 상태가 5개뿐인데
    /// 그래프를 그리는 비용이 더 크다. 전환 조건이 복잡해지면 그때 옮기면 된다.
    /// </summary>
    [RequireComponent(typeof(Unit))]
    public class UnitAnimator : MonoBehaviour
    {
        public enum Clip { Idle, Walk, Attack, Hurt, Death }

        [Tooltip("이 유닛의 겉모습. 스프라이트 경로·프레임 크기·배율이 여기서 나온다.")]
        public UnitKind kind = UnitKind.Soldier;

        private float frameRate = 10f;
        private float bodyScale = 2.4f;

        private Unit _unit;
        private UnitAI _ai;
        private SpriteRenderer _sr;

        private Sprite[] _idle, _walk, _attack, _hurt, _death;
        private Sprite[] _current;
        private Clip _clip = Clip.Idle;

        private float _frameTimer;
        private int _frame;

        /// <summary>공격·피격 모션은 끝까지 재생되어야 끊겨 보이지 않는다.</summary>
        private bool _locked;

        private void Awake()
        {
            _unit = GetComponent<Unit>();
            _ai = GetComponent<UnitAI>();
        }

        private void Start()
        {
            // Body는 Unit.Awake가 만든다. Awake 호출 순서에 기대지 않도록 Start에서 잡는다.
            _sr = _unit.Body;

            UnitVisualDef def = UnitVisuals.Of(kind);
            frameRate = def.FrameRate;
            bodyScale = def.BodyScale;

            // 프레임 크기가 팩마다 다르다. 픽셀 단위/유닛도 같은 값으로 줘야
            // 프레임 하나가 정확히 월드 1유닛이 되어 bodyScale 계산이 단순해진다.
            string dir = "Sprites/Units/" + def.Prefix + "-";
            int fs = def.FrameSize;
            _idle = SpriteSheet.Load(dir + "Idle", fs, fs);
            _walk = SpriteSheet.Load(dir + "Walk", fs, fs);
            _attack = SpriteSheet.Load(dir + def.AttackClip, fs, fs);
            _hurt = SpriteSheet.Load(dir + "Hurt", fs, fs);
            _death = SpriteSheet.Load(dir + "Death", fs, fs);

            // 시트를 못 읽었으면 Unit이 만들어 둔 사각형을 그대로 둔다.
            // 에셋이 빠져도 게임은 돌아가야 한다.
            if (_idle == null || _idle.Length == 0)
            {
                enabled = false;
                return;
            }

            _unit.SetSpriteMode(bodyScale);

            // 적은 왼쪽을 보게 뒤집는다. 스프라이트 원본은 오른쪽을 본다.
            if (_unit.team == Team.Enemy) _sr.flipX = true;

            Play(Clip.Idle, false);
        }

        private void OnEnable()
        {
            if (_unit == null) _unit = GetComponent<Unit>();
            _unit.Attacked += OnAttacked;
            _unit.Damaged += OnDamaged;
            _unit.Dying += OnDying;
            _unit.Revived += OnRevived;
        }

        private void OnDisable()
        {
            _unit.Attacked -= OnAttacked;
            _unit.Damaged -= OnDamaged;
            _unit.Dying -= OnDying;
            _unit.Revived -= OnRevived;
        }

        private void OnAttacked() { Play(Clip.Attack, true); }

        private void OnDamaged()
        {
            // 공격 모션 중이면 유지 — 맞을 때마다 끊기면 뭘 하는지 안 읽힌다
            if (_locked && _clip == Clip.Attack) return;
            Play(Clip.Hurt, true);
        }

        private void OnDying() { Play(Clip.Death, true); }

        // 사망 모션은 마지막 프레임에서 멈춰 있고 _locked가 걸려 있다.
        // 대기 모션을 잠금 없이 다시 틀어야 그 자리에서 일어선다.
        private void OnRevived() { Play(Clip.Idle, false); }

        private void Update()
        {
            if (!_locked && _unit.IsAlive)
            {
                Clip want = (_ai != null && _ai.CurrentState == UnitAI.State.Advancing) ? Clip.Walk : Clip.Idle;
                if (want != _clip) Play(want, false);
            }

            Advance();
        }

        private void Play(Clip clip, bool locked)
        {
            // Start 전에 피격 이벤트가 들어올 수 있다
            if (_sr == null) return;

            Sprite[] frames = FramesOf(clip);
            if (frames == null || frames.Length == 0) return;

            _clip = clip;
            _current = frames;
            _frame = 0;
            _frameTimer = 0f;
            _locked = locked;
            _sr.sprite = frames[0];
        }

        private void Advance()
        {
            if (_current == null || _current.Length == 0) return;

            _frameTimer += Time.deltaTime;
            float step = 1f / Mathf.Max(frameRate, 1f);
            if (_frameTimer < step) return;

            _frameTimer -= step;
            _frame++;

            if (_frame >= _current.Length)
            {
                if (_clip == Clip.Death)
                {
                    // 마지막 프레임에서 멈춘다. 제거는 Unit이 타이머로 처리.
                    _frame = _current.Length - 1;
                    return;
                }

                _frame = 0;
                if (_locked) _locked = false; // 1회 재생 끝 → 다시 상태 추종
            }

            _sr.sprite = _current[_frame];
        }

        private Sprite[] FramesOf(Clip clip)
        {
            switch (clip)
            {
                case Clip.Walk: return _walk;
                case Clip.Attack: return _attack;
                case Clip.Hurt: return _hurt;
                case Clip.Death: return _death;
                default: return _idle;
            }
        }
    }
}
