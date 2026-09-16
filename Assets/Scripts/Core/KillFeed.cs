using UnityEngine;
using RuneCast.Meta;

namespace RuneCast.Core
{
    /// <summary>
    /// 한 번에 여럿을 쓰러뜨린 순간을 한 사건으로 묶어서 보여준다.
    ///
    /// **이게 없으면 여덟 마리를 쓸어도 한 마리 잡은 것과 화면이 같다.** 죽음은
    /// 각자 알아서 작은 폭발 하나와 소리 하나를 냈다. 여덟이 동시에 죽으면
    /// 작은 폭발 여덟 개가 겹칠 뿐이고, 소리는 간격 제한에 걸려 오히려 한 번만
    /// 났다 — 판을 뒤집은 한 방이 가장 밋밋하게 지나갔다.
    ///
    /// 이 게임이 플레이어에게 주는 보상은 **개입이 통했다는 감각** 하나다.
    /// 자동전투는 알아서 굴러가므로, 내가 그린 것이 판을 바꿨다는 게 화면에
    /// 남지 않으면 그릴 이유가 없어진다.
    ///
    /// **룬으로 낸 결과일 때만 크게 알린다.** 아군이 알아서 쓸어낸 것까지 같이
    /// 띄우면 큰 글씨의 뜻이 흐려진다. 큰 글씨는 "네가 했다"는 뜻이어야 한다.
    /// </summary>
    public class KillFeed : MonoBehaviour
    {
        /// <summary>이 안에 죽은 것들은 한 번의 일로 친다.</summary>
        private const float Window = 0.30f;

        /// <summary>이 수부터 따로 알린다. 둘까지는 흔해서 매번 뜨면 잔소리가 된다.</summary>
        private const int MinCount = 3;

        private static KillFeed _instance;

        private int _count;
        private int _byRune;
        private Vector3 _sum;
        private float _closeAt;

        private void Awake()
        {
            _instance = this;
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        /// <summary>적이 쓰러질 때 Unit이 부른다.</summary>
        public static void Report(Vector3 at, bool byRune)
        {
            if (_instance != null) _instance.Add(at, byRune);
        }

        /// <summary>판이 끝나거나 다시 시작할 때. 안 비우면 다음 판 첫 죽음에 얹힌다.</summary>
        public static void Clear()
        {
            if (_instance == null) return;
            _instance._count = 0;
            _instance._byRune = 0;
            _instance._sum = Vector3.zero;
        }

        private void Add(Vector3 at, bool byRune)
        {
            _count++;
            if (byRune) _byRune++;
            _sum += at;

            // 창은 첫 죽음에서만 연다. 죽을 때마다 늘리면 전투가 이어지는 내내
            // 안 닫히고, 그러면 서로 다른 두 번의 일이 한 번으로 합쳐진다.
            if (_count == 1) _closeAt = Time.unscaledTime + Window;
        }

        private void Update()
        {
            if (_count == 0 || Time.unscaledTime < _closeAt) return;

            int n = _count;
            int rune = _byRune;
            Vector3 at = _sum / n;

            _count = 0;
            _byRune = 0;
            _sum = Vector3.zero;

            if (n < MinCount) return;

            // 절반 넘게 룬으로 죽였을 때만 내 공으로 친다. 아군이 여덟을 잡는데
            // 마지막 한 마리를 룬으로 끝냈다고 "싹쓸이"가 뜨면 거짓말이다.
            if (rune * 2 <= n) return;

            Celebrate(n, at);
        }

        private void Celebrate(int n, Vector3 at)
        {
            // 셋에서 여덟 사이를 0~1로. 그 위는 더 세지지 않는다 —
            // 스무 마리를 잡았다고 화면이 스무 배로 흔들리면 무슨 일이 났는지 안 보인다.
            float k = Mathf.Clamp01((n - MinCount) / 5f);

            // 시간이 잠깐 멎어야 "한 방이었다"가 읽힌다. 클수록 더 느리게,
            // 더 길게. 다만 0.16초를 넘기면 멎은 게 아니라 끊긴 것처럼 느껴진다.
            TimeControl.HitStop(Mathf.Lerp(0.10f, 0.02f, k), Mathf.Lerp(0.07f, 0.16f, k));
            CameraShake.Shake(Mathf.Lerp(0.16f, 0.42f, k), Mathf.Lerp(0.22f, 0.36f, k));

            BurstFx.Play(Vfx.StarBurst, at, Mathf.Lerp(2.2f, 4.2f, k),
                new Color(1f, 0.86f, 0.55f), 55);

            // 큰 정리(6마리 이상)에는 별이 쏟아진다. 문턱을 둔 이유는
            // 서너 마리는 별똥별 한 방마다 나오기 때문 — 매번 쏟아지면
            // 정말 큰 순간에 보여줄 게 남지 않는다.
            if (k > 0.5f) ParticleFx.Spawn(Pfx.Stars, at, 0.9f, 56);

            FloatingText.Label(at + new Vector3(0f, 0.9f, 0f), TextFor(n),
                new Color(1f, 0.88f, 0.5f), Mathf.Lerp(26f, 40f, k));

            // 처치음을 낮게 깔아 무게를 준다. 새 소리를 만들지 않은 건 배울 게
            // 하나 더 늘어나서가 아니라, 이 순간에 처음 듣는 소리가 나면
            // 무슨 일이 났는지 해석하느라 오히려 늦게 읽히기 때문이다.
            AudioManager.Play(Sfx.HitMeteor, Mathf.Lerp(0.7f, 1f, k), Mathf.Lerp(0.72f, 0.55f, k));
        }

        private static string TextFor(int n)
        {
            if (n >= 8) return Loc.T("kill.wipe");
            if (n >= 5) return Loc.T("kill.sweep");
            return Loc.F("kill.multi", n);
        }
    }
}
