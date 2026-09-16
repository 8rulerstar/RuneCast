using System;
using System.Collections.Generic;
using UnityEngine;
using RuneCast.Core;

namespace RuneCast.Battle
{
    public enum Team
    {
        Hero = 0,
        Enemy = 1,
    }

    /// <summary>
    /// 전투 유닛. 지금은 룬을 시험할 샌드백 수준으로만 만든다 —
    /// 스킬도, 이동 패턴도, 진형도 없다. 그건 제스처가 재밌다는 게 확인된 뒤의 일.
    ///
    /// 물리(Rigidbody2D/Collider2D)를 쓰지 않고 거리 계산으로 끝낸다.
    /// 유닛 수가 수십 단위라 비용 차이가 없고, 물리 설정에서 오는 사고를 없앤다.
    /// </summary>
    public class Unit : MonoBehaviour
    {
        public static readonly List<Unit> All = new List<Unit>();

        [Header("소속")]
        public Team team = Team.Hero;

        /// <summary>
        /// 평타가 통하지 않는다. **룬 피해만 받는다.**
        ///
        /// TakeDamage가 이미 fromRune을 받고 있어서(피해 숫자를 크게 띄우려고 만든 것)
        /// 그 값을 그대로 쓴다 — 새 경로를 만들면 어느 효과가 룬인지 두 곳에서
        /// 관리하게 되고 반드시 어긋난다.
        /// </summary>
        /// <summary>
        /// 평타에 강한가(망령). **예전에는 아예 안 통했다.**
        ///
        /// 완전 면역은 "개입하지 않으면 절대 안 죽는다"를 만들었지만, 대가가 컸다 —
        /// 아군이 때리는데 숫자가 하나도 안 뜨면 게임이 고장 난 것처럼 보이고,
        /// 마나가 마른 순간에는 아무것도 할 수 없는 벽이 된다.
        ///
        /// 지금은 평타가 <see cref="NormalDamageScale"/>만큼만 들어간다.
        /// 아군 넷이 붙어 11초, 룬 한 방이면 즉사 — 개입이 여전히 압도적으로
        /// 낫지만 벽은 아니다.
        /// </summary>
        [NonSerialized] public bool resistsNormal;

        /// <summary>
        /// 평타가 들어가는 비율.
        ///
        /// 실측(체력 90, 아군 공격력 8/0.8초, 넷이 붙었을 때 잡는 시간):
        ///   10% → 22.5초   사실상 영원. 벽이던 때와 다를 게 없다
        ///   20% → 11.2초   여기. 판 하나가 20~30초이니 무시하면 확실히 아프다
        ///   30% →  7.5초   아군이 알아서 처리한다. 이 적의 뜻이 사라진다
        /// </summary>
        public const float NormalDamageScale = 0.2f;

        [Header("스탯")]
        public float maxHp = 100f;
        public float attackDamage = 8f;
        public float attackRange = 0.9f;
        public float attackInterval = 0.8f;
        public float moveSpeed = 1.6f;

        public float Hp { get; private set; }
        public float Shield { get; private set; }

        /// <summary>
        /// 보호막이 사라지는 시각. **영구 보호막은 무적이나 마찬가지다.**
        ///
        /// 예전에는 한 번 씌우면 맞을 때까지 남았다. 그래서 전투 초반에 미리
        /// 걸어두는 게 언제나 최선이었고, "언제 씌울까"라는 판단이 사라졌다.
        /// 롤처럼 시간이 지나면 벗겨지게 하면, 맞기 직전에 맞춰 넣는 것이
        /// 실제로 더 나은 선택이 된다.
        /// </summary>
        private float _shieldUntil;

        /// <summary>보호막이 남은 시간. 0이면 없다.</summary>
        public float ShieldSecondsLeft
        {
            get { return Shield <= 0f ? 0f : Mathf.Max(0f, _shieldUntil - Time.time); }
        }
        public bool IsAlive { get { return Hp > 0f; } }
        public float HpRatio { get { return maxHp <= 0f ? 0f : Mathf.Clamp01(Hp / maxHp); } }

        /// <summary>체력이 이 비율 밑이면 위기.</summary>
        public const float CriticalRatio = 0.3f;
        public bool IsCritical { get { return IsAlive && HpRatio <= CriticalRatio; } }

        /// <summary>스프라이트를 그리는 렌더러. UnitAnimator가 프레임을 갈아끼운다.</summary>
        public SpriteRenderer Body { get { return _body; } }

        public event Action<Unit> Died;

        /// <summary>공격 모션 트리거. UnitAI가 실제로 때리는 순간 호출한다.</summary>
        public event Action Attacked;

        /// <summary>체력이 깎였을 때(보호막으로 전부 흡수되면 발생하지 않음).</summary>
        public event Action Damaged;

        /// <summary>사망 판정 직후. 오브젝트는 사망 연출이 끝난 뒤에 사라진다.</summary>
        public event Action Dying;

        /// <summary>소생 룬으로 되살아났을 때. 애니메이터가 사망 모션을 풀어야 한다.</summary>
        public event Action Revived;

        private SpriteRenderer _body;
        private Transform _hpFill;
        private Transform _shieldFill;
        private SpriteRenderer _shieldFillRenderer;
        private SpriteRenderer _hpFillRenderer;
        private SpriteRenderer _shieldGlow;
        private float _flashTimer;
        private float _corpseTimer;
        private Color _baseColor = Color.white;
        private float _slowUntil;
        private float _slowMultiplier = 1f;
        private float _empowerUntil;
        private float _empowerDamage = 1f;
        private float _empowerHaste = 1f;
        private SpriteRenderer _empowerGlow;

        private static readonly Color HeroColor = new Color(0.45f, 0.78f, 1f);
        private static readonly Color EnemyColor = new Color(1f, 0.42f, 0.45f);

        private const float CorpseLinger = 1.1f;

        // 체력바 위치·크기. 캐릭터 스프라이트 머리 위에 오도록 맞춘 값.
        private const float HpBarY = 0.85f;
        private const float HpBarWidth = 0.8f;

        private void Awake()
        {
            Hp = maxHp;
            BuildVisual();
        }

        private void OnEnable() { if (!All.Contains(this)) All.Add(this); }
        private void OnDisable() { All.Remove(this); }

        private void BuildVisual()
        {
            // 스프라이트를 못 찾는 상황에서도 유닛이 보이도록 사각형으로 시작한다.
            // UnitAnimator가 시트를 읽는 데 성공하면 SetSpriteMode로 바꿔 간다.
            _baseColor = team == Team.Hero ? HeroColor : EnemyColor;

            _body = PrimitiveSprites.Spawn("Body", PrimitiveSprites.Square(32), _baseColor, 10, transform);
            _body.transform.localScale = Vector3.one * 0.55f;

            // 삼각형인 이유: 쉴드 룬의 도형이 삼각형이다.
            // 링(원)을 쓰면 힐(원)과 표시가 겹쳐서 뭘 받았는지 헷갈린다.
            _shieldGlow = PrimitiveSprites.Spawn("Shield", PrimitiveSprites.TriangleRing(96, 0.09f),
                new Color(0.6f, 0.95f, 1f, 0f), 9, transform);
            _shieldGlow.transform.localScale = Vector3.one * 1.6f;

            // 고양 표시. 보호막(삼각 테두리)과 겹쳐 걸릴 수 있으므로 발밑에 깔리는
            // 원판으로 만든다 — 테두리끼리 겹치면 둘 다 안 읽힌다.
            //
            // 아군만 만든다. 고양은 아군 전용이라 적에게 붙이면 웨이브마다 쓰지도 않을
            // SpriteRenderer가 적 수만큼 늘어난다.
            if (team == Team.Hero)
            {
                _empowerGlow = PrimitiveSprites.Spawn("Empower", PrimitiveSprites.Circle(48),
                    new Color(1f, 0.8f, 0.35f, 0f), 8, transform);
                _empowerGlow.transform.localPosition = new Vector3(0f, -0.28f, 0f);
                _empowerGlow.transform.localScale = new Vector3(0.85f, 0.32f, 1f);
            }

            var bg = PrimitiveSprites.Spawn("HpBg", PrimitiveSprites.Square(8), new Color(0f, 0f, 0f, 0.65f), 20, transform);
            bg.transform.localPosition = new Vector3(0f, HpBarY, 0f);
            bg.transform.localScale = new Vector3(HpBarWidth + 0.05f, 0.13f, 1f);

            var fill = PrimitiveSprites.Spawn("HpFill", PrimitiveSprites.Square(8), new Color(0.4f, 1f, 0.5f), 21, transform);
            fill.transform.localPosition = new Vector3(0f, HpBarY, 0f);
            fill.transform.localScale = new Vector3(HpBarWidth, 0.09f, 1f);
            _hpFill = fill.transform;
            _hpFillRenderer = fill;

            // **보호막을 체력바 위에 겹쳐 보여준다.**
            //
            // 예전에는 몸 주변 빛의 진하기로만 나타냈다. 그러면 남은 양을 알 수가
            // 없고, 알 수 없는 방어는 **무적처럼 느껴진다** — 실제로는 30을 흡수하고
            // 사라지는데도 "저 유닛은 지금 안 죽는다"로 읽힌다.
            //
            // 롤이 그렇듯 체력 오른쪽에 흰 칸으로 얹는다. 길이가 곧 남은 양이라
            // 언제 벗겨지는지가 보인다.
            var shield = PrimitiveSprites.Spawn("ShieldFill", PrimitiveSprites.Square(8),
                new Color(0.85f, 0.95f, 1f, 0.95f), 22, transform);
            shield.transform.localPosition = new Vector3(0f, HpBarY, 0f);
            shield.transform.localScale = new Vector3(0f, 0.09f, 1f);
            _shieldFill = shield.transform;
            _shieldFillRenderer = shield;
        }

        /// <summary>실제 캐릭터 스프라이트를 쓰게 전환. 색 틴트를 없애고 크기를 키운다.</summary>
        public void SetSpriteMode(float scale)
        {
            // 망령만 틴트를 남긴다. 다른 적과 겉이 같으면 평타가 안 통한다는 걸
            // 때려 보기 전에는 알 수 없다 — 그건 알려주는 게 아니라 숨기는 것이다.
            // 망령만 틴트를 남긴다. 주술사는 전용 아트가 생겨서 색을 입힐 이유가
            // 없어졌다 — 지팡이를 든 실루엣이 이미 "때리는 놈이 아니다"를 말한다.
            _baseColor = resistsNormal ? new Color(0.55f, 0.78f, 1f, 0.85f) : Color.white;
            _body.color = _baseColor;
            _body.transform.localScale = Vector3.one * scale;

            // 망령 고리는 몸보다 커야 "둘러싼 것"으로 읽힌다. 고리는 스프라이트를
            // 갈아끼우기 전에 만들어져서 몸 크기를 몰랐다 — 여기가 아는 유일한 자리다.
            if (_wardRing != null)
                _wardRing.transform.localScale = Vector3.one * (scale * 1.35f);
        }

        /// <summary>망령 표시 — 몸 주위를 도는 고리. 룬으로만 잡힌다는 표식.</summary>
        private SpriteRenderer _wardRing;

        /// <summary>멀리서 던지는가. UnitAI가 때리는 대신 투사체를 낸다.</summary>
        [NonSerialized] public bool ranged;

        /// <summary>대열에서 몇 번째인가. 물결 사이에 줄을 맞추는 데 쓴다.</summary>
        [NonSerialized] public int formationSlot;

        /// <summary>마지막으로 체력을 깎은 것이 룬이었나. 업적 집계에만 쓴다.</summary>
        private bool _lastHitFromRune;

        /// <summary>망령 표시. 평타 저항 + 몸을 감싸는 고리.</summary>
        public void MarkResistant()
        {
            resistsNormal = true;

            _wardRing = PrimitiveSprites.Spawn("Ward", PrimitiveSprites.Ring(96, 0.10f),
                new Color(0.6f, 0.85f, 1f, 0.75f), 11, transform);
            _wardRing.transform.localScale = Vector3.one * 1.25f;

            if (_body != null) _body.color = _baseColor = new Color(0.55f, 0.78f, 1f, 0.85f);
        }

        private void Update()
        {
            if (!IsAlive)
            {
                // 사망 연출이 끝나면 제거. 그 사이에도 IsAlive가 false라
                // 탐색·광역 판정에서는 이미 빠져 있다.
                _corpseTimer -= Time.deltaTime;
                if (_corpseTimer <= 0f)
                {
                    Destroy(gameObject);
                    return;
                }

                // **마지막 0.4초 동안 흐려진다.** 예전엔 시체가 멀쩡히 서 있다가
                // 툭 사라졌다. 사망 애니메이션이 없는 적(새로 들인 Tiny Swords
                // 시트에는 Death가 없다)에서는 특히 티가 난다 — 죽은 줄도 모르고
                // 서 있다가 없어지는 것처럼 보인다.
                if (_body != null && _corpseTimer < 0.4f)
                {
                    var c = _body.color;
                    _body.color = new Color(c.r, c.g, c.b, _corpseTimer / 0.4f);
                }
                return;
            }

            if (Shield > 0f && Time.time >= _shieldUntil)
            {
                Shield = 0f;
                RefreshShieldVisual();
                RefreshHpBar();

                // 벗겨지는 걸 알린다. 소리 없이 사라지면 다음 타격에 왜
                // 체력이 깎였는지 알 수가 없다.
                AudioManager.Play(Sfx.ShieldAbsorb, 0.3f, 1.5f);
            }
            else if (Shield > 0f)
            {
                // 곧 벗겨질 때 깜빡이려면 매 프레임 손봐야 한다.
                // 피해를 받을 때만 갱신하면 남은 시간이 화면에 안 나타난다.
                RefreshShieldBar();
            }

            if (_flashTimer > 0f)
            {
                _flashTimer -= Time.deltaTime;
                _body.color = Color.Lerp(_baseColor, Color.white, Mathf.Clamp01(_flashTimer / 0.12f));
            }

            if (_wardRing != null)
            {
                // 천천히 돈다. 멈춰 있으면 장식으로 읽히고, 빠르면 소용돌이 룬과 헷갈린다.
                _wardRing.transform.Rotate(0f, 0f, 42f * Time.deltaTime);

                float a = 0.55f + 0.22f * Mathf.Sin(Time.time * 2.6f);
                var wc = _wardRing.color;
                _wardRing.color = new Color(wc.r, wc.g, wc.b, a);
            }

            if (_empowerGlow != null)
            {
                // 남은 시간이 아니라 맥동으로 보여준다. 게이지를 하나 더 붙이면
                // 머리 위가 복잡해지는데, 이 정보는 "지금 세다"만 읽히면 충분하다.
                float a = IsEmpowered ? 0.3f + 0.22f * Mathf.Sin(Time.time * 7f) : 0f;
                var c = _empowerGlow.color;
                if (!Mathf.Approximately(c.a, a)) _empowerGlow.color = new Color(c.r, c.g, c.b, a);
            }
        }

        public void NotifyAttack()
        {
            if (Attacked != null) Attacked();
        }

        public void TakeDamage(float amount)
        {
            TakeDamage(amount, false);
        }

        /// <summary>
        /// fromRune이면 숫자를 크고 금색으로 띄운다.
        ///
        /// 평타와 룬 피해를 같은 크기로 보여주면 화면이 숫자로 뒤덮여서
        /// 정작 중요한 "내 룬이 얼마나 들어갔나"가 묻힌다. 이 게임에서 플레이어가
        /// 확인해야 하는 건 자기 개입의 크기지 자동전투의 매 타격이 아니다.
        /// </summary>
        public void TakeDamage(float amount, bool fromRune)
        {
            if (!IsAlive || amount <= 0f) return;

            Vector3 textAt = transform.position + new Vector3(0f, 0.55f, 0f);

            // 망령: 평타는 조금만 들어간다.
            //
            // **숫자가 뜨는 게 중요하다.** 완전 면역이던 때는 아군이 때려도
            // 아무 일도 안 일어나서 게임이 고장 난 것처럼 보였다. 작게라도
            // 깎이는 게 보이면 "약하게 들어간다"가 저절로 읽힌다.
            if (resistsNormal && !fromRune)
            {
                amount *= NormalDamageScale;

                // 튕기는 소리는 남긴다. 이 적이 다르다는 걸 귀로도 알린다.
                // 여럿이 동시에 때리므로 AudioManager가 최소 간격으로 솎아낸다.
                AudioManager.Play(Sfx.ShieldAbsorb, 0.28f, 0.7f);
            }

            // 보호막부터 깎는다
            if (Shield > 0f)
            {
                float absorbed = Mathf.Min(Shield, amount);
                Shield -= absorbed;
                amount -= absorbed;
                RefreshShieldVisual();

                // 보호막이 막아냈다는 걸 소리로 알린다. 이게 없으면 쉴드를 쓴 보람이
                // 화면에 안 나타난다 — 체력이 안 깎이는 건 '아무 일도 안 일어난 것'과 같아 보인다.
                if (absorbed > 0f)
                {
                    AudioManager.Play(Sfx.ShieldAbsorb, 0.55f);
                    FloatingText.Label(textAt, RuneCast.Meta.Loc.T("hud.blocked"), new Color(0.6f, 0.88f, 1f), 15f);
                }
            }

            if (amount > 0f)
            {
                _lastHitFromRune = fromRune;
                Hp -= amount;
                _flashTimer = 0.12f;
                FloatingText.Damage(textAt, amount, fromRune);

                // 아군이 맞을 때만. 적 피격음까지 넣으면 전투 내내 소음이 된다.
                if (Hp > 0f && team == Team.Hero) AudioManager.Play(Sfx.HitHero, 0.5f);
                if (Hp > 0f && Damaged != null) Damaged();
            }

            RefreshHpBar();

            if (Hp <= 0f)
            {
                Hp = 0f;

                // 아군 사망은 크게, 적 사망은 작게. 둘 다 들려야 전황이 귀로 읽힌다.
                if (team == Team.Hero) AudioManager.Play(Sfx.DeathHero, 0.9f);
                else AudioManager.Play(Sfx.DeathEnemy, 0.45f);

                // 쓰러지는 순간 한 번 터진다. 사망 모션은 몇 프레임에 걸쳐 재생돼서
                // 여럿이 동시에 쓰러질 때 "쓸어버렸다"가 화면에 안 남는다.
                // 아군 쪽을 더 크고 붉게 — 잃은 것이 눈에 더 오래 남아야 한다.
                BurstFx.Play(Vfx.SmallHit, transform.position,
                    team == Team.Hero ? 1.5f : 1.05f,
                    team == Team.Hero ? new Color(1f, 0.55f, 0.5f) : new Color(1f, 0.85f, 0.6f),
                    45);

                _corpseTimer = CorpseLinger;
                if (_hpFillRenderer != null) _hpFillRenderer.enabled = false;
                if (_shieldGlow != null) _shieldGlow.enabled = false;
                if (_shieldFillRenderer != null) _shieldFillRenderer.enabled = false;
                if (_wardRing != null) _wardRing.enabled = false;

                // 고양 표시도 꺼야 한다. Update가 죽은 유닛에서 바로 반환하므로
                // 그냥 두면 알파가 죽는 순간 값에 얼어붙어 시체 밑에 원판이 남는다.
                _empowerUntil = 0f;
                if (_empowerGlow != null)
                {
                    var eg = _empowerGlow.color;
                    _empowerGlow.color = new Color(eg.r, eg.g, eg.b, 0f);
                }

                if (team == Team.Enemy)
                {
                    // 한꺼번에 여럿이 죽었는지 KillFeed가 모아서 판단한다.
                    // 여기서 바로 크게 터뜨리면 여덟 마리가 여덟 번 터진다.
                    KillFeed.Report(transform.position, _lastHitFromRune);

                    if (resistsNormal) RuneCast.Meta.Achievements.OnWraithKilled();

                    // 룬으로 끝냈을 때만 센다. 아군 평타로 죽은 건 플레이어의 몫이 아니다.
                    if (_lastHitFromRune) RuneCast.Meta.Achievements.OnRuneKill();
                }

                if (Dying != null) Dying();
                if (Died != null) Died(this);
            }
        }

        public void Heal(float amount)
        {
            if (!IsAlive || amount <= 0f) return;

            // 이미 가득이면 회복량이 아니라 0을 띄워야 정직하다 —
            // 안 그러면 만피에 힐을 쏟아붓고도 잘 쓴 줄 안다.
            float before = Hp;
            Hp = Mathf.Min(maxHp, Hp + amount);
            RefreshHpBar();

            FloatingText.Heal(transform.position + new Vector3(0f, 0.55f, 0f), Hp - before);
        }

        /// <summary>보호막이 유지되는 시간(초).</summary>
        public const float ShieldDuration = 6f;

        public void AddShield(float amount)
        {
            if (!IsAlive || amount <= 0f) return;
            Shield += amount;

            // 겹쳐 씌우면 시간도 새로 센다. 남은 시간이 짧은 보호막 위에
            // 새로 씌웠는데 곧바로 같이 벗겨지면 억울하다.
            _shieldUntil = Time.time + ShieldDuration;

            RefreshShieldVisual();
            RefreshHpBar();

            FloatingText.Shield(transform.position + new Vector3(0f, 0.55f, 0f), amount);
        }

        /// <summary>
        /// 이동 둔화. 더 강한 둔화가 이미 걸려 있으면 덮어쓰지 않는다 —
        /// 약한 걸 나중에 걸어서 강한 게 풀리면 플레이어 입장에서 앞뒤가 안 맞는다.
        /// </summary>
        public void ApplySlow(float multiplier, float duration)
        {
            if (!IsAlive) return;

            float end = Time.time + duration;
            if (Time.time < _slowUntil && _slowMultiplier < multiplier)
            {
                _slowUntil = Mathf.Max(_slowUntil, end);
                return;
            }

            _slowMultiplier = Mathf.Clamp01(multiplier);
            _slowUntil = end;
        }

        public float SpeedMultiplier
        {
            get { return Time.time < _slowUntil ? _slowMultiplier : 1f; }
        }

        public bool IsSlowed { get { return Time.time < _slowUntil; } }

        /// <summary>
        /// 고양. 공격력과 공격속도를 함께 올린다.
        ///
        /// 둘을 같이 거는 이유: 공격력만 올리면 표시된 숫자가 커지는 것 말고는
        /// 화면에서 달라지는 게 없다. 공격속도가 같이 붙어야 "빨라졌다"가 눈에 보인다.
        ///
        /// 둔화와 같은 규칙으로 덧씌운다 — 약한 걸 나중에 걸어서 강한 게 풀리면 안 된다.
        /// </summary>
        public void Empower(float damageMultiplier, float hasteMultiplier, float duration)
        {
            if (!IsAlive) return;

            float end = Time.time + duration;
            if (Time.time < _empowerUntil && _empowerDamage > damageMultiplier)
            {
                _empowerUntil = Mathf.Max(_empowerUntil, end);
                return;
            }

            _empowerDamage = Mathf.Max(1f, damageMultiplier);
            _empowerHaste = Mathf.Max(1f, hasteMultiplier);
            _empowerUntil = end;
        }

        public bool IsEmpowered { get { return IsAlive && Time.time < _empowerUntil; } }

        /// <summary>공격력 배율. UnitAI가 실제 피해에 곱한다.</summary>
        public float AttackMultiplier { get { return IsEmpowered ? _empowerDamage : 1f; } }

        /// <summary>공격 주기 배율. 값이 클수록 빨리 때린다(간격이 나뉜다).</summary>
        public float HasteMultiplier { get { return IsEmpowered ? _empowerHaste : 1f; } }

        /// <summary>
        /// 소생. 시체가 사라지기 전(CorpseLinger)에만 가능하다.
        ///
        /// 시간 제한을 두는 게 핵심이다 — 언제든 되살릴 수 있으면 아군을 지킬 이유가
        /// 없어져서 쉴드·힐이 통째로 무의미해진다. "죽은 걸 본 순간 바로 그려야" 살아난다.
        /// </summary>
        public bool Revive(float hpFraction)
        {
            if (IsAlive || _corpseTimer <= 0f) return false;

            Hp = Mathf.Clamp(maxHp * hpFraction, 1f, maxHp);
            Shield = 0f;
            _corpseTimer = 0f;

            if (_hpFillRenderer != null) _hpFillRenderer.enabled = true;
            if (_shieldGlow != null) _shieldGlow.enabled = true;

            // 죽는 순간의 피격 플래시 색이 그대로 굳어 있다
            _flashTimer = 0f;
            if (_body != null) _body.color = _baseColor;
            RefreshHpBar();
            RefreshShieldVisual();

            if (Revived != null) Revived();
            return true;
        }

        private void RefreshHpBar()
        {
            if (_hpFill == null) return;

            // 스케일만 줄이면 **가운데를 기준으로 양쪽에서 줄어든다.**
            // 스프라이트 기준점이 한가운데라서 그렇다 — 체력바는 왼쪽 끝이 고정된 채
            // 오른쪽만 닳아야 "얼마 남았나"가 길이로 읽힌다.
            //
            // 줄어든 만큼 왼쪽으로 밀어서 왼쪽 모서리를 제자리에 붙들어 둔다.
            //   가득: 중심 0            왼끝 -W/2
            //   절반: 중심 -W/4         왼끝 -W/2   (폭 W/2)
            var s = _hpFill.localScale;
            s.x = HpBarWidth * HpRatio;
            _hpFill.localScale = s;

            var p = _hpFill.localPosition;
            p.x = -HpBarWidth * (1f - HpRatio) * 0.5f;
            _hpFill.localPosition = p;

            // 위기 상태를 색으로도 알린다 — 예고 없이 죽는 상황을 만들지 않기 위해
            if (_hpFillRenderer != null)
                _hpFillRenderer.color = IsCritical ? new Color(1f, 0.75f, 0.2f) : new Color(0.4f, 1f, 0.5f);

            RefreshShieldBar();
        }

        /// <summary>
        /// 보호막 칸. 체력이 끝나는 자리에서 오른쪽으로 이어 붙인다.
        ///
        /// **최대 체력을 기준으로 길이를 잰다.** 그래야 "체력 절반짜리 보호막"이
        /// 눈에 절반으로 보인다. 막대를 넘치면 잘라서, 아무리 두꺼운 보호막도
        /// 바 밖으로 나가지 않는다.
        /// </summary>
        private void RefreshShieldBar()
        {
            if (_shieldFill == null) return;

            float ratio = maxHp <= 0f ? 0f : Mathf.Clamp01(Shield / maxHp);
            if (ratio <= 0f)
            {
                _shieldFill.localScale = new Vector3(0f, 0.09f, 1f);
                return;
            }

            // 체력이 끝나는 지점부터 시작해서, 바 오른쪽 끝을 넘지 않게 자른다.
            float start = HpRatio;
            float end = Mathf.Min(1f, start + ratio);
            float w = Mathf.Max(0f, end - start);

            _shieldFill.localScale = new Vector3(HpBarWidth * w, 0.09f, 1f);

            // 왼쪽 모서리를 체력 끝에 붙인다. 기준점이 한가운데라 반폭만큼 민다.
            float leftEdge = -HpBarWidth * 0.5f + HpBarWidth * start;
            _shieldFill.localPosition = new Vector3(leftEdge + HpBarWidth * w * 0.5f, HpBarY, 0f);

            // 곧 벗겨지면 깜빡인다. 언제 없어지는지 모르면 시간이 있다는 걸
            // 판단에 못 쓴다.
            if (_shieldFillRenderer == null) return;
            float left = ShieldSecondsLeft;
            float a = left < 1.5f ? 0.35f + 0.6f * Mathf.Abs(Mathf.Sin(Time.time * 9f)) : 0.95f;
            _shieldFillRenderer.color = new Color(0.85f, 0.95f, 1f, a);
        }

        private void RefreshShieldVisual()
        {
            if (_shieldGlow == null) return;
            var c = _shieldGlow.color;
            c.a = Mathf.Clamp01(Shield / 40f) * 0.9f;
            _shieldGlow.color = c;
        }

        /// <summary>같은 편이 아닌 가장 가까운 생존 유닛.</summary>
        /// <summary>
        /// 가장 가까운 적. 단 **때릴 수 있는 상대를 먼저 고른다.**
        ///
        /// 망령은 평타가 20%만 들어간다. 그냥 가까운 순으로 고르면 아군 넷이
        /// 망령에 달라붙어 십여 초를 쓰고, 그 사이 옆의 잘 잡히는 적이 아군을 팬다.
        /// 같은 시간으로 다섯 배를 잡을 수 있는데 제일 안 잡히는 걸 고르는 셈이다.
        ///
        /// 아예 무시하지는 않는다. 다른 적이 하나도 없으면 망령이라도 붙잡고
        /// 있어야 한다 — 목표가 없으면 아군은 계속 앞으로 행군해 화면 밖으로 나간다.
        ///
        /// 이렇게 두면 망령의 뜻이 선명해진다. 아군은 뒤로 미루는 상대이므로
        /// **저건 내가 빨리 끊는 편이 낫다**는 게 읽힌다.
        /// </summary>
        public static Unit NearestEnemyOf(Unit self)
        {
            Unit best = null, fallback = null;
            float bestSqr = float.MaxValue, fallbackSqr = float.MaxValue;
            Vector3 p = self.transform.position;

            // 룬만 통하는 상대를 뒤로 미루는 건 평타로 싸우는 쪽뿐이다.
            bool skipWards = self.team == Team.Hero;

            for (int i = 0; i < All.Count; i++)
            {
                Unit u = All[i];
                if (u == null || u == self || !u.IsAlive || u.team == self.team) continue;

                float d = (u.transform.position - p).sqrMagnitude;

                if (skipWards && u.resistsNormal)
                {
                    if (d < fallbackSqr) { fallbackSqr = d; fallback = u; }
                    continue;
                }

                if (d < bestSqr) { bestSqr = d; best = u; }
            }

            return best != null ? best : fallback;
        }

        public static int CountAlive(Team team)
        {
            int n = 0;
            for (int i = 0; i < All.Count; i++)
                if (All[i] != null && All[i].IsAlive && All[i].team == team) n++;
            return n;
        }

        /// <summary>중심에서 radius 안의 생존 유닛을 모은다. 광역 효과 공통 진입점.</summary>
        public static void CollectInRadius(Vector3 center, float radius, Team team, List<Unit> result)
        {
            result.Clear();
            float sqr = radius * radius;
            for (int i = 0; i < All.Count; i++)
            {
                Unit u = All[i];
                if (u == null || !u.IsAlive || u.team != team) continue;
                if ((u.transform.position - center).sqrMagnitude <= sqr) result.Add(u);
            }
        }

        /// <summary>
        /// 아직 사라지지 않은 시체를 모은다. 소생 룬 전용.
        /// _corpseTimer가 남아 있는 것만 — 연출이 끝난 유닛은 이미 화면에 없으므로
        /// 되살리면 아무 데서도 안 죽은 것이 튀어나오는 것처럼 보인다.
        /// </summary>
        public static void CollectCorpsesInRadius(Vector3 center, float radius, Team team, List<Unit> result)
        {
            result.Clear();
            float sqr = radius * radius;
            for (int i = 0; i < All.Count; i++)
            {
                Unit u = All[i];
                if (u == null || u.IsAlive || u.team != team || u._corpseTimer <= 0f) continue;
                if ((u.transform.position - center).sqrMagnitude <= sqr) result.Add(u);
            }
        }
    }
}
