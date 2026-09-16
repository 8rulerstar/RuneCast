using System.Collections.Generic;
using UnityEngine;

namespace RuneCast.Core
{
    /// <summary>
    /// 프레임 시트를 재생하고 사라지는 이펙트.
    ///
    /// 두 종류의 시트를 다룬다:
    ///  - 가로 한 줄 (burst_*.png, 384x64 = 64px 6장)
    ///  - 격자 (brackeys predrawn, 예: 6x5 = 30장)
    /// 통짜 이미지로 쓰면 프레임들이 그대로 한 덩어리로 화면에 뜬다.
    ///
    /// **끝난 이펙트는 지우지 않고 재워 둔다.** 타격 하나, 죽음 하나마다
    /// GameObject를 두 개씩 만들고 지웠다. 별똥별로 열 마리를 정리하면 한 프레임에
    /// 스무 개가 생겼다 사라진다. 모바일에서 GC가 도는 순간 화면이 걸리는데,
    /// 하필 제일 화려한 순간이 제일 무거운 순간이 된다.
    ///
    /// **반복 재생하는 것은 재우지 않는다.** 소용돌이나 봉화는 부른 쪽이 참조를
    /// 들고 있다가 나중에 StopLoop을 부른다. 그 사이에 재활용돼서 다른 이펙트가
    /// 되어 있으면 엉뚱한 걸 멈추게 된다. 그런 것들은 몇 개 안 되고 오래 사니
    /// 풀로 얻는 것도 없다.
    /// </summary>
    public class BurstFx : MonoBehaviour
    {
        private const int StripFrameSize = 64;

        private SpriteRenderer _sr;
        private Sprite[] _frames;
        private float _fps = 20f;
        private float _timer;
        private int _frame;
        private Color _tint = Color.white;
        private bool _loop;
        private bool _fadeOut = true;

        private static readonly Stack<BurstFx> Pool = new Stack<BurstFx>();
        private bool _poolable;
        private bool _asleep;

        /// <summary>격자 시트 재생. 실패하면 null.</summary>
        public static BurstFx Play(Vfx vfx, Vector3 pos, float worldSize, Color tint,
            int sortingOrder = 44, bool loop = false, float fpsOverride = 0f, float rotationDeg = 0f)
        {
            Sprite[] frames = VfxLibrary.Frames(vfx);
            if (frames == null || frames.Length == 0) return null;

            float fps = fpsOverride > 0f ? fpsOverride : VfxLibrary.Fps(vfx);
            var fx = Create("Vfx_" + vfx, frames, pos, worldSize, tint, fps, sortingOrder, rotationDeg, !loop);
            fx._loop = loop;
            fx._fadeOut = !loop;
            return fx;
        }

        /// <summary>가로 한 줄짜리 시트 재생 (구형 burst_* 에셋용).</summary>
        public static bool Play(string resourcePath, Vector3 pos, float worldSize, Color tint,
            float fps = 20f, int sortingOrder = 44)
        {
            Sprite[] frames = SpriteSheet.Load(resourcePath, StripFrameSize, StripFrameSize);
            if (frames == null || frames.Length == 0) return false;

            Create("Burst", frames, pos, worldSize, tint, fps, sortingOrder, 0f, true);
            return true;
        }

        private static BurstFx Create(string name, Sprite[] frames, Vector3 pos, float worldSize,
            Color tint, float fps, int sortingOrder, float rotationDeg, bool poolable)
        {
            BurstFx fx = null;

            if (poolable)
            {
                // 전투를 나갈 때 FxRoot.ClearAll이 자고 있던 것까지 지운다.
                // 꺼낸 게 이미 없어졌을 수 있다 — Unity의 == 오버로드가 null로 잡아준다.
                while (fx == null && Pool.Count > 0) fx = Pool.Pop();
            }

            if (fx == null)
            {
                var go = new GameObject(name);
                FxRoot.Adopt(go);

                fx = go.AddComponent<BurstFx>();

                // 프레임 하나가 월드 1유닛(ppu = 프레임 크기)이므로 원하는 크기를 그대로 스케일로 쓴다
                fx._sr = PrimitiveSprites.Spawn("Sprite", frames[0], tint, sortingOrder, go.transform);
            }
            else
            {
                fx._asleep = false;
                fx.gameObject.SetActive(true);
            }

            fx.transform.position = pos;
            fx.transform.rotation = Quaternion.Euler(0f, 0f, rotationDeg);

            // 재워 뒀던 것은 지난 이펙트의 상태가 그대로 남아 있다. **전부 다시 넣는다.**
            // _frame과 _timer를 빠뜨리면 두 번째로 쓰일 때 중간 프레임부터 시작한다.
            fx._frames = frames;
            fx._fps = fps;
            fx._tint = tint;
            fx._frame = 0;
            fx._timer = 0f;
            fx._loop = false;
            fx._fadeOut = true;
            fx._poolable = poolable;

            fx._sr.sprite = frames[0];
            fx._sr.color = tint;
            fx._sr.sortingOrder = sortingOrder;
            fx._sr.transform.localScale = Vector3.one * Mathf.Max(worldSize, 0.1f);
            return fx;
        }

        /// <summary>끝난 이펙트를 치운다. 재울 수 있으면 재우고 아니면 지운다.</summary>
        private void Finish()
        {
            if (!_poolable)
            {
                Destroy(gameObject);
                return;
            }

            if (_asleep) return;
            _asleep = true;
            gameObject.SetActive(false);
            Pool.Push(this);
        }

        /// <summary>반복 재생 중인 이펙트를 끌 때. 남은 프레임을 마저 돌리고 사라진다.</summary>
        public void StopLoop()
        {
            _loop = false;
            _fadeOut = true;
        }

        private void Update()
        {
            // 시전 직후엔 슬로우가 풀리는 중이라 unscaled로 돌린다
            _timer += Time.unscaledDeltaTime;
            float step = 1f / Mathf.Max(_fps, 1f);
            if (_timer < step) return;

            _timer -= step;
            _frame++;

            if (_frame >= _frames.Length)
            {
                if (_loop)
                {
                    _frame = 0;
                }
                else
                {
                    Finish();
                    return;
                }
            }

            _sr.sprite = _frames[_frame];

            // 뒤로 갈수록 옅어지게 — 마지막 프레임에서 툭 끊기지 않도록.
            // 반복 재생 중에는 하면 안 된다(주기마다 깜빡인다).
            if (_fadeOut)
            {
                var c = _tint;
                c.a *= 1f - (_frame / (float)_frames.Length) * 0.4f;
                _sr.color = c;
            }
        }
    }
}
