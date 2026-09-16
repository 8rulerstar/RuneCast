using UnityEngine;
using RuneCast.Core;
using RuneCast.Meta;

namespace RuneCast.UI
{
    /// <summary>
    /// 설정 화면과 일시정지 화면.
    ///
    /// 값이 바뀌면 즉시 저장·적용한다. "적용" 버튼을 두면 안 누르고 나가는 사람이
    /// 반드시 생기고, 그러면 설정을 바꿨는데 안 바뀌었다고 느낀다.
    /// </summary>
    public class SettingsUI : MonoBehaviour
    {
        public GameFlow flow;

        private GUIStyle _title, _mid, _small, _midCenter;
        private bool _resetArmed;
        private float _resetDoneTime = -99f;
        private float _tutorialResetTime = -99f;

        private void EnsureStyles()
        {
            if (_title != null) return;
            _title = new GUIStyle(GUI.skin.label)
                { fontSize = UiSkin.Text.Title, alignment = TextAnchor.MiddleCenter };
            _mid = new GUIStyle(GUI.skin.label) { fontSize = UiSkin.Text.Mid };
            _small = new GUIStyle(GUI.skin.label) { fontSize = UiSkin.Text.Small };
            _midCenter = new GUIStyle(_mid) { alignment = TextAnchor.MiddleCenter };

            UiSkin.ApplyFont(_title, _mid, _small, _midCenter);
        }

        private void OnGUI()
        {
            // 모든 UI는 가상 해상도 위에서 그린다. 실제 픽셀로 짜면
            // 고해상도 화면에서 글자가 물리적으로 못 읽을 만큼 작아진다.
            UiScale.Begin();
            DrawAll();
            UiScale.End();
        }

        private void DrawAll()
        {
            if (flow == null) return;
            EnsureStyles();

            if (flow.State == GameState.Settings) DrawSettings();
            else if (flow.State == GameState.Battle && flow.Paused) DrawPause();
        }

        // ── 일시정지 ────────────────────────────────────────────

        private void DrawPause()
        {
            Scrim(0.7f);

            UiSkin.DrawBanner(new Rect(UiScale.W * 0.5f - 160f, UiScale.H * 0.5f - 152f, 320f, 46f),
                Loc.T("pause.title"), _title);

            const float bw = 220f, bh = 42f, gap = 10f;
            float x = (UiScale.W - bw) * 0.5f;
            float y = UiScale.H * 0.5f - 80f;

            if (Btn(new Rect(x, y, bw, bh), Loc.T("pause.resume"), true)) flow.SetPaused(false);
            y += bh + gap;
            if (Btn(new Rect(x, y, bw, bh), Loc.T("settings.title"))) flow.GoToSettings();
            y += bh + gap;
            if (Btn(new Rect(x, y, bw, bh), Loc.T("pause.restart"))) flow.RetryStage();
            y += bh + gap;
            if (Btn(new Rect(x, y, bw, bh), Loc.T("pause.quit"))) flow.GoToStageSelect();
        }

        // ── 설정 ────────────────────────────────────────────────

        private void DrawSettings()
        {
            Scrim(0.92f);

            UiSkin.DrawBanner(new Rect(UiScale.W * 0.5f - 190f, UiScale.T + 22f, 380f, 46f),
                Loc.T("settings.title"), _title);

            const float w = 560f;
            float x = (UiScale.W - w) * 0.5f;
            float y = UiScale.T + 96f;
            const float rowH = 46f;

            // 언어 — 맨 위에 둔다. 이 화면을 처음 여는 이유가 대개 언어라서.
            Row(x, ref y, w, Loc.T("settings.language"));
            {
                const float bw = 120f;
                float bx = x + w - bw * 2f - 10f;
                if (Btn(new Rect(bx, y - rowH + 6f, bw, 32f), Loc.LanguageName(Lang.Korean),
                        GameSettings.Language == Lang.Korean))
                    GameSettings.Language = Lang.Korean;
                if (Btn(new Rect(bx + bw + 10f, y - rowH + 6f, bw, 32f), Loc.LanguageName(Lang.English),
                        GameSettings.Language == Lang.English))
                    GameSettings.Language = Lang.English;
            }

            Row(x, ref y, w, Loc.T("settings.bgm"));
            GameSettings.BgmVolume = Slider(x + w - 250f, y - rowH + 12f, 250f,
                GameSettings.BgmVolume, 0f, 1f);

            Row(x, ref y, w, Loc.T("settings.sfx"));
            GameSettings.SfxVolume = Slider(x + w - 250f, y - rowH + 12f, 250f,
                GameSettings.SfxVolume, 0f, 1f);

            Row(x, ref y, w, Loc.T("settings.slowmo"));
            GameSettings.CastTimeScale = Slider(x + w - 250f, y - rowH + 12f, 250f,
                GameSettings.CastTimeScale, 0.05f, 1f);
            GUI.color = new Color(1f, 1f, 1f, 0.4f);
            GUI.Label(new Rect(x + 6f, y - 14f, w, 18f), Loc.T("settings.slowmoHint"), _small);
            GUI.color = Color.white;
            y += 10f;

            // 폰에는 창 모드가 없다. 눌러도 아무 일이 안 일어나는 항목을 두면
            // 설정이 고장난 것처럼 보인다.
            if (!Application.isMobilePlatform)
            {
                Row(x, ref y, w, Loc.T("settings.fullscreen"));
                if (Btn(new Rect(x + w - 120f, y - rowH + 6f, 120f, 32f),
                        GameSettings.Fullscreen ? Loc.T("common.on") : Loc.T("common.off"),
                        GameSettings.Fullscreen))
                    GameSettings.Fullscreen = !GameSettings.Fullscreen;
            }

            // 개발용 두 가지는 아래쪽에 모아 둔다 — 평소에 건드릴 것이 아니라는 게
            // 자리로 읽혀야 한다.
            Row(x, ref y, w, Loc.T("settings.infiniteInk"));
            if (Btn(new Rect(x + w - 120f, y - rowH + 6f, 120f, 32f),
                    GameSettings.InfiniteInk ? Loc.T("common.on") : Loc.T("common.off"),
                    GameSettings.InfiniteInk))
                GameSettings.InfiniteInk = !GameSettings.InfiniteInk;

            Row(x, ref y, w, Loc.T("settings.debug"));
            if (Btn(new Rect(x + w - 120f, y - rowH + 6f, 120f, 32f),
                    GameSettings.ShowDebugPanel ? Loc.T("common.on") : Loc.T("common.off"),
                    GameSettings.ShowDebugPanel))
                GameSettings.ShowDebugPanel = !GameSettings.ShowDebugPanel;

            // 튜토리얼 다시 보기. 되돌릴 수 없는 초기화와 달리 안전한 조작이라
            // 경고 없이 한 번에 실행한다.
            y += 6f;
            if (Btn(new Rect(x, y, w, 36f), Loc.T("settings.tutorial")))
            {
                TutorialProgress.ResetAll();
                _tutorialResetTime = Time.unscaledTime;
            }

            if (Time.unscaledTime - _tutorialResetTime < 2.5f)
            {
                GUI.color = new Color(0.75f, 1f, 0.8f);
                GUI.Label(new Rect(x, y + 38f, w, 20f), Loc.T("settings.tutorialDone"), _midCenter);
                GUI.color = Color.white;
            }
            y += 44f;

            // 크레딧. 초기화 위에 둔다 — 초기화는 되돌릴 수 없는 버튼이라
            // 화면 맨 아래에 혼자 떨어져 있어야 실수로 눌리지 않는다.
            y += 10f;
            if (Btn(new Rect(x, y, w, 40f), Loc.T("credits.title"))) flow.GoToCredits();

            // 초기화 — 두 번 눌러야 실행된다. 되돌릴 수 없는 버튼을 한 번에 두면
            // 반드시 누가 실수로 누른다.
            y += 24f;
            bool armed = _resetArmed;
            if (Btn(new Rect(x, y, w, 40f),
                    armed ? Loc.T("settings.resetWarn") : Loc.T("settings.reset"), false, true, armed))
            {
                if (armed)
                {
                    StageProgress.ResetAll();
                    PlayerData.ResetAll();
                    TutorialProgress.ResetAll();
                    Achievements.ResetAll();
                    _resetArmed = false;
                    _resetDoneTime = Time.unscaledTime;
                }
                else _resetArmed = true;
            }

            if (Time.unscaledTime - _resetDoneTime < 2.5f)
            {
                GUI.color = new Color(1f, 0.8f, 0.4f);
                GUI.Label(new Rect(x, y + 44f, w, 22f), Loc.T("settings.resetDone"), _midCenter);
                GUI.color = Color.white;
            }

            if (Btn(new Rect((UiScale.W - 180f) * 0.5f, UiScale.B - 62f, 180f, 40f),
                    Loc.T("common.close"), true))
            {
                _resetArmed = false;
                flow.CloseSettings();
            }
        }

        private void Row(float x, ref float y, float w, string label)
        {
            const float rowH = 46f;
            UiSkin.DrawPanel(new Rect(x, y, w, rowH - 6f), new Color(0.55f, 0.57f, 0.6f, 0.55f));
            GUI.Label(new Rect(x + 12f, y + 8f, w * 0.5f, 24f), label, _mid);
            y += rowH;
        }

        /// <summary>슬라이더 + 현재값 표시. GUI.HorizontalSlider는 스킨 기본값이라 그대로 쓴다.</summary>
        private float Slider(float x, float y, float w, float value, float min, float max)
        {
            float v = GUI.HorizontalSlider(new Rect(x, y + 8f, w - 56f, 20f), value, min, max);
            GUI.color = new Color(1f, 1f, 1f, 0.7f);
            GUI.Label(new Rect(x + w - 48f, y, 48f, 24f), Mathf.RoundToInt(v * 100f) + "%", _mid);
            GUI.color = Color.white;
            return v;
        }

        /// <summary>
        /// 설정·일시정지의 막.
        ///
        /// 일시정지는 **전투 위에 뜨는 화면이라 진하게 덮어야 한다** — 뒤에서 전투가
        /// 비치면 멈춘 건지 아닌지 헷갈린다. 설정을 메뉴에서 열었을 때는 이미
        /// 메뉴 배경이 불투명하므로 이 막은 그 위에 한 겹 더 얹히는 셈이고,
        /// 그래도 어색하지 않은 값으로 잡았다.
        /// </summary>
        private static void Scrim(float alpha)
        {
            GUI.color = new Color(0.03f, 0.04f, 0.03f, alpha);
            GUI.DrawTexture(new Rect(0f, 0f, UiScale.W, UiScale.H), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        private bool Btn(Rect r, string label, bool active = false, bool enabled = true, bool danger = false)
        {
            Color tint = danger ? new Color(1f, 0.62f, 0.55f)
                : active ? new Color(0.78f, 1f, 0.7f) : Color.white;
            return UiSkin.DrawButton(r, label, _midCenter, active, enabled, tint);
        }

    }
}
