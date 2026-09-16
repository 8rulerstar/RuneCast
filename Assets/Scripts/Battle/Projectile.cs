using System.Collections.Generic;
using UnityEngine;
using RuneCast.Core;

namespace RuneCast.Battle
{
    /// <summary>
    /// 직선으로 날아가 처음 맞는 적에게 피해를 주고 사라진다.
    /// 물리 없이 프레임마다 거리 검사 — 유닛 수가 적어 충분하다.
    ///
    /// **다 쓴 화살은 지우지 않고 재워 둔다.** 예전에는 한 발마다 GameObject를
    /// 두 개(본체와 스프라이트) 만들고 맞는 순간 지웠다. 화살 룬은 한 번에 네 발이
    /// 나가고 연달아 쓰이므로 전투 내내 이어지는 할당과 파괴가 된다.
    /// PC에서는 티가 안 나지만 모바일에서는 GC가 도는 순간 화면이 걸린다 —
    /// 하필 룬을 쏟아붓는 순간에.
    /// </summary>
    public class Projectile : MonoBehaviour
    {
        public Vector2 direction = Vector2.right;
        public float speed = 11f;
        public float damage = 20f;
        public float hitRadius = 0.38f;
        public float lifetime = 2.5f;
        public Team targetTeam = Team.Enemy;

        private const float DefaultSpeed = 11f;
        private const float DefaultHitRadius = 0.38f;
        private const float DefaultLifetime = 2.5f;

        // 화살 하나가 리스트를 하나씩 들고 있을 이유가 없다. 한 프레임에 여러 발이
        // 날아도 검사는 각자의 Update 안에서 끝나므로 공유해도 섞이지 않는다.
        private static readonly List<Unit> Buffer = new List<Unit>();

        private static readonly Stack<Projectile> Pool = new Stack<Projectile>();

        /// <summary>지금 날아가는 것들. 판이 끝날 때 한 번에 거두려고 들고 있다.</summary>
        private static readonly List<Projectile> Active = new List<Projectile>();

        private SpriteRenderer _sr;
        private bool _usesArrowSprite;
        private bool _asleep;
        private bool _fromRune = true;

        /// <param name="fromRune">
        /// 룬이 낸 것인가. **적이 쏜 것에까지 true를 주면 안 된다** —
        /// 피해 숫자가 크고 금색으로 뜨고(플레이어의 개입 표시다),
        /// 업적의 "룬으로 처치" 집계에도 들어간다.
        /// </param>
        public static Projectile Spawn(Vector3 pos, Vector2 dir, float damage, Team targetTeam,
            Color color, bool fromRune = true)
        {
            Projectile p = null;

            // 전투를 나갈 때 FxRoot.ClearAll이 자고 있던 것까지 지운다. 그래서 꺼낸 게
            // 이미 없어졌을 수 있다 — Unity의 == 오버로드가 그걸 null로 잡아준다.
            while (p == null && Pool.Count > 0) p = Pool.Pop();

            if (p == null)
            {
                p = CreateNew();
            }
            else
            {
                p._asleep = false;
                p.gameObject.SetActive(true);
            }

            p.transform.position = pos;

            // 재워 뒀던 것은 값이 지난 발의 것으로 남아 있다. **전부 다시 넣어야 한다.**
            // 특히 lifetime을 빠뜨리면 두 번째로 쓰인 화살이 태어나자마자 사라진다.
            p.direction = dir.normalized;
            p.damage = damage;
            p.targetTeam = targetTeam;
            p._fromRune = fromRune;
            p.speed = DefaultSpeed;
            p.hitRadius = DefaultHitRadius;
            p.lifetime = DefaultLifetime;

            if (p._sr != null)
            {
                // **적이 쏜 것은 색을 입힌다.** 화살 스프라이트를 흰색 그대로 쓰면
                // 내가 쏜 화살과 생김새가 똑같아진다. 화면을 가로지르는 것이
                // 내 것인지 나를 향한 것인지 구분이 안 되면, 그걸 보고 판단할 수가 없다.
                p._sr.color = (p._usesArrowSprite && fromRune) ? Color.white : color;
                p._sr.transform.localRotation =
                    Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
            }

            Active.Add(p);
            return p;
        }

        private static Projectile CreateNew()
        {
            var go = new GameObject("Arrow");
            FxRoot.Adopt(go);

            // 화살 스프라이트가 있으면 쓰고, 없으면 막대기로 대체한다
            Sprite arrow = SpriteSheet.Single("Sprites/Units/Arrow", 32f);
            SpriteRenderer sr;
            if (arrow != null)
            {
                sr = PrimitiveSprites.Spawn("Sprite", arrow, Color.white, 30, go.transform);
                sr.transform.localScale = Vector3.one * 0.9f;
            }
            else
            {
                sr = PrimitiveSprites.Spawn("Sprite", PrimitiveSprites.Square(16), Color.white, 30, go.transform);
                sr.transform.localScale = new Vector3(0.42f, 0.1f, 1f);
            }

            var p = go.AddComponent<Projectile>();
            p._sr = sr;
            p._usesArrowSprite = arrow != null;
            return p;
        }

        /// <summary>
        /// 지금 날아가는 것을 전부 거둔다. 판이 끝날 때 부른다.
        ///
        /// 씬을 훑는(FindObjectsByType) 대신 목록을 들고 있는다. 매 발마다
        /// 넣고 빼는 값이 훑는 값보다 훨씬 싸고, 무엇보다 자고 있는 것과
        /// 날아가는 것이 목록으로 이미 갈려 있다.
        /// </summary>
        public static void SleepAll()
        {
            // 뒤에서부터 — Sleep이 이 목록에서 자기를 빼낸다.
            for (int i = Active.Count - 1; i >= 0; i--)
                if (Active[i] != null) Active[i].Sleep();

            Active.Clear();
        }

        /// <summary>지우지 않고 재운다. 두 번 불려도 풀에 두 번 들어가지 않는다.</summary>
        private void Sleep()
        {
            if (_asleep) return;
            _asleep = true;
            gameObject.SetActive(false);
            Active.Remove(this);
            Pool.Push(this);
        }

        private void Update()
        {
            lifetime -= Time.deltaTime;
            if (lifetime <= 0f)
            {
                Sleep();
                return;
            }

            transform.position += (Vector3)(direction * speed * Time.deltaTime);

            Unit.CollectInRadius(transform.position, hitRadius, targetTeam, Buffer);
            if (Buffer.Count > 0)
            {
                BurstFx.Play(Vfx.ArrowImpact, transform.position, 1.2f, new Color(1f, 0.92f, 0.6f), 46);

                // 파티클은 세 발에 한 번쯤만. 화살은 이 게임에서 제일 잦은 타격이라
                // 전부 얹으면 (1) 화면이 늘 반짝여서 절정이 사라지고
                // (2) 생성·파괴가 쌓여 모바일 GC를 깨운다 — BurstFx가 풀을 쓰는 이유.
                if (Random.value < 0.34f)
                    ParticleFx.Spawn(Pfx.Arrow, transform.position, 0.45f);

                AudioManager.Play(Sfx.HitArrow, 0.5f);
                Buffer[0].TakeDamage(damage, _fromRune);
                Sleep();
            }
        }
    }
}
