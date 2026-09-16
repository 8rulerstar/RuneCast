using UnityEngine;
using RuneCast.Core;
using RuneCast.Meta;

namespace RuneCast.UI
{
    /// <summary>
    /// 업적을 달성하면 잠깐 떠오르는 알림.
    ///
    /// **이게 없으면 업적이 있는 줄도 모른다.** 달성 순간에는 소리만 났고 화면에는
    /// 아무 일도 없었다 — 대장장이의 기록 탭을 일부러 열어봐야 알 수 있었다.
    /// 목표를 만들려고 넣은 것인데 목표가 달성됐다는 걸 안 알려주면 아무 소용이 없다.
    ///
    /// 전투 중에도 뜬다. 다만 **화면 위쪽 구석**이다 — 가운데는 그리는 자리고,
    /// 아래는 튜토리얼과 마나 게이지가 쓴다. 지금 하던 일을 가리지 않아야 한다.
    /// </summary>
    public class AchievementToast : MonoBehaviour
    {
        private const float Life = 3.4f;

        private GUIStyle _name, _sub;
        private Achievement _shown;
        private float _shownAt = -99f;

        // 떠 있는 동안 매 프레임 만들 이유가 없다. 뜰 때 한 번만.
        private string _rewardText = "";

        private void Update()
        {
            // Achievements가 달성 시각을 남긴다. 여기서는 새 것이 왔는지만 본다.
            if (Achievements.RecentAt <= _shownAt) return;

            _shown = Achievements.Recent;
            _shownAt = Achievements.RecentAt;
            _rewardText = Loc.T("common.shards") + " +" + Achievements.Reward(_shown);
        }

        private void OnGUI()
        {
            float age = Time.unscaledTime - _shownAt;
            if (age > Life) return;
            if (Event.current.type != EventType.Repaint) return;

            EnsureStyles();

            UiScale.Begin();
            Draw(age);
            UiScale.End();
        }

        private void EnsureStyles()
        {
            if (_name != null) return;
            _name = new GUIStyle(GUI.skin.label)
                { fontSize = UiSkin.Text.Mid, alignment = TextAnchor.MiddleLeft };
            _sub = new GUIStyle(GUI.skin.label)
                { fontSize = UiSkin.Text.Small, alignment = TextAnchor.MiddleLeft };
            UiSkin.ApplyFont(_name, _sub);
        }

        private void Draw(float age)
        {
            const float w = 320f, h = 58f;

            // 오른쪽에서 밀려 들어왔다 밀려 나간다. 알림이 화면 안에서 그냥 나타나면
            // 언제 뜬 건지 눈이 못 잡는다.
            float inK = Mathf.Clamp01(age / 0.30f);
            float outK = age > Life - 0.5f ? Mathf.Clamp01((Life - age) / 0.5f) : 1f;
            float slide = (1f - inK) * (1f - inK);

            float x = UiScale.R - w - 16f + slide * (w + 24f);
            float y = UiScale.T + 16f;
            float alpha = outK;

            var r = new Rect(x, y, w, h);

            GUI.color = new Color(1f, 1f, 1f, alpha);
            UiSkin.DrawPanel(r, new Color(0.95f, 0.86f, 0.62f, alpha), UiSkin.PanelKind.Gold);

            GUI.color = new Color(0.32f, 0.26f, 0.16f, alpha * 0.9f);
            GUI.Label(new Rect(r.x + 14f, r.y + 6f, w - 28f, 18f), Loc.T("ach.unlocked"), _sub);

            GUI.color = new Color(0.16f, 0.13f, 0.09f, alpha);
            GUI.Label(new Rect(r.x + 14f, r.y + 24f, w - 100f, 24f), Achievements.Name(_shown), _name);

            // 보상은 오른쪽에. 받으러 가야 한다는 걸 여기서 알려 준다.
            GUI.color = new Color(0.42f, 0.32f, 0.12f, alpha);
            GUI.Label(new Rect(r.xMax - 96f, r.y + 24f, 82f, 24f), _rewardText, _sub);

            GUI.color = Color.white;
        }
    }
}
