using System.Collections.Generic;
using UnityEngine;

namespace RuneCast.Core
{
    /// <summary>
    /// 월드 좌표 위에 뜨는 숫자 — 피해량·회복량·보호막.
    ///
    /// 이게 없으면 플레이어가 "얼마나 아팠는지"를 체력바 길이 변화로만 짐작해야 한다.
    /// 룬 등급이 위력을 바꾸는 게 이 게임의 핵심인데, **숫자가 안 보이면
    /// PERFECT와 GOOD의 차이가 화면에 드러나지 않는다.** 연출보다 이게 먼저다.
    ///
    /// TextMesh나 TMP 대신 IMGUI로 그린다. 폰트 에셋을 프로젝트에 들일 필요가 없고,
    /// HUD가 이미 IMGUI라 같은 계층에서 정렬이 예측된다.
    /// </summary>
    public class FloatingText : MonoBehaviour
    {
        public static FloatingText Instance { get; private set; }

        private struct Entry
        {
            public Vector3 World;
            public string Text;
            public Color Color;
            public float Born;
            public float Life;
            public float Size;
            public float DriftX;
        }

        private readonly List<Entry> _entries = new List<Entry>(64);
        private GUIStyle _style;
        private Camera _cam;

        // 한 프레임에 수십 개가 겹치면 읽히지도 않고 GC만 먹는다
        private const int MaxEntries = 40;

        private static readonly Color DamageColor = new Color(1f, 0.95f, 0.85f);
        private static readonly Color CritColor = new Color(1f, 0.82f, 0.35f);
        private static readonly Color HealColor = new Color(0.55f, 1f, 0.6f);
        private static readonly Color ShieldColor = new Color(0.6f, 0.88f, 1f);

        private void Awake()
        {
            Instance = this;
        }

        private static void Push(Vector3 world, string text, Color color, float size, float life)
        {
            if (Instance == null) return;

            var list = Instance._entries;
            if (list.Count >= MaxEntries) list.RemoveAt(0);

            list.Add(new Entry
            {
                World = world,
                Text = text,
                Color = color,
                Born = Time.unscaledTime,
                Life = life,
                Size = size,
                // 같은 자리에 여러 개가 뜨면 겹쳐서 못 읽는다. 좌우로 흩뿌린다.
                DriftX = Random.Range(-26f, 26f),
            });
        }

        /// <summary>피해량. big을 켜면 크고 금색으로 — 룬 한 방처럼 강조할 때.</summary>
        public static void Damage(Vector3 world, float amount, bool big = false)
        {
            if (amount < 1f) return;
            Push(world, Mathf.RoundToInt(amount).ToString(),
                big ? CritColor : DamageColor, big ? 26f : 17f, big ? 1.1f : 0.8f);
        }

        public static void Heal(Vector3 world, float amount)
        {
            if (amount < 1f) return;
            Push(world, "+" + Mathf.RoundToInt(amount), HealColor, 20f, 1.0f);
        }

        public static void Shield(Vector3 world, float amount)
        {
            if (amount < 1f) return;
            Push(world, "◈" + Mathf.RoundToInt(amount), ShieldColor, 19f, 1.0f);
        }

        /// <summary>글자 그대로. 등급 표시처럼 숫자가 아닌 것.</summary>
        public static void Label(Vector3 world, string text, Color color, float size = 22f)
        {
            Push(world, text, color, size, 1.0f);
        }

        public static void Clear()
        {
            if (Instance != null) Instance._entries.Clear();
        }

        private void OnGUI()
        {
            // Repaint에서만. 이 패널도 라벨뿐이라 다른 이벤트에서 도는 건 전부 낭비다
            // (숫자가 40개 떠 있으면 프레임마다 좌표 변환과 라벨 배치를 두 벌씩 했다).
            if (Event.current.type != EventType.Repaint) return;

            // 모든 UI는 가상 해상도 위에서 그린다. 실제 픽셀로 짜면
            // 고해상도 화면에서 글자가 물리적으로 못 읽을 만큼 작아진다.
            UiScale.Begin();
            DrawAll();
            UiScale.End();
        }

        private void DrawAll()
        {
            if (_entries.Count == 0) return;
            if (_cam == null) _cam = Camera.main;
            if (_cam == null) return;

            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                };

                // 피해 숫자만 다른 글꼴이면 화면에서 제일 먼저 눈에 띈다
                UiSkin.ApplyFont(_style);
            }

            float now = Time.unscaledTime;

            for (int i = _entries.Count - 1; i >= 0; i--)
            {
                Entry e = _entries[i];
                float t = (now - e.Born) / e.Life;
                if (t >= 1f)
                {
                    _entries.RemoveAt(i);
                    continue;
                }

                Vector3 sp = _cam.WorldToScreenPoint(e.World);
                if (sp.z < 0f) continue;

                // WorldToScreenPoint는 실제 픽셀을 준다. UI는 가상 좌표에서 그리므로 변환한다.
                Vector2 vp = UiScale.ToVirtual(new Vector2(sp.x, sp.y));

                // 위로 뜨면서 감속. 처음에 확 튀어야 눈에 걸린다.
                float rise = Mathf.Sqrt(t) * 52f;
                float x = vp.x + e.DriftX * t;
                float y = UiScale.H - vp.y - rise;

                // **글자 크기를 4의 배수로 끊는다.**
                //
                // 동적 폰트는 요청한 크기마다 글리프를 새로 구워 아틀라스에 넣는다.
                // 여기서 크기를 매끄럽게 애니메이션하면 숫자 하나가 뜨는 동안에도
                // 14, 15, 16… 하고 크기가 계속 바뀌고, 화면에 숫자가 수십 개 뜨는
                // 이 게임에서는 아틀라스가 넘쳐서 **이미 구워둔 글자가 밀려나
                // 화면에서 깨져 보인다.** 한글 글꼴을 넣으면서 글리프가 늘어 더 심해졌다.
                //
                // 끊어도 팝 연출은 거의 그대로다 — 0.3초 안에 끝나는 움직임이라
                // 단이 지는 게 눈에 띄지 않는다.
                float raw = e.Size * Mathf.Lerp(1.15f, 0.95f, Mathf.Min(t * 3f, 1f));
                UiSkin.SetTextSize(_style, UiSkin.Text.Snap(raw));

                // 어두운 그림자를 먼저 깔아야 밝은 배경 위에서도 읽힌다
                GUI.color = new Color(0f, 0f, 0f, 0.55f * (1f - t));
                GUI.Label(new Rect(x - 60f + 1.5f, y - 14f + 1.5f, 120f, 28f), e.Text, _style);

                GUI.color = new Color(e.Color.r, e.Color.g, e.Color.b, 1f - t * t);
                GUI.Label(new Rect(x - 60f, y - 14f, 120f, 28f), e.Text, _style);
            }

            GUI.color = Color.white;
        }
    }
}
