using UnityEngine;
using RuneCast.Core;
using RuneCast.Meta;

namespace RuneCast.UI
{
    /// <summary>
    /// 크레딧 화면.
    ///
    /// **이건 취향이 아니라 의무다.** UI 팩(Crusenho)이 CC BY 4.0이라 출처 표기가
    /// 라이선스 조건이고, 표기 없이 배포하면 라이선스 위반이다.
    /// 이 프로젝트에서 표기 의무가 있는 유일한 에셋이라 이 화면 하나면 해결된다.
    ///
    /// 나머지는 CC-0이거나 표기 불필요지만 같이 적는다 — 무료로 공개한 사람들 덕에
    /// 만든 게임인데 의무가 있는 것만 골라 적는 건 이상하다.
    ///
    /// 목록을 Loc에 넣지 않은 이유: 사람 이름과 팩 이름은 번역 대상이 아니다.
    /// 번역해야 하는 건 분류 제목뿐이라 그것만 Loc을 탄다.
    /// </summary>
    public class CreditsUI : MonoBehaviour
    {
        public GameFlow flow;

        private GUIStyle _title, _section, _item, _small, _midCenter;
        private Vector2 _scroll;

        /// <summary>
        /// (분류 Loc 키, 이름, 라이선스·링크).
        /// CREDITS.md와 같은 내용이다. 저쪽은 왜 골랐는지까지 적힌 작업 기록이고
        /// 이쪽은 배포물에 들어가는 표기다 — 목적이 달라서 따로 둔다.
        /// </summary>
        private static readonly string[,] Entries =
        {
            { "credits.ui",      "Crusenho Agus Hennihuno — Complete UI Essential Pack",
                                 "CC BY 4.0 · crusenho.itch.io" },
            { "credits.music",   "Abstraction (Ben Burnes) — Music Loop Bundle",
                                 "CC0 · abstractionmusic.com" },
            { "credits.sfx",     "Helton Yan — Pixel Combat",
                                 "heltonyan.itch.io" },
            { "credits.units",   "Tiny RPG Character Asset Pack · Enemy Animations Set",
                                 "" },
            { "credits.terrain", "Pixel Frog — Tiny Swords",
                                 "pixelfrog-assets.itch.io" },
            { "credits.vfx",     "Brackeys VFX Bundle (art by CodeManu)",
                                 "CC0" },
            { "credits.engine",  "Unity 6",
                                 "" },
        };

        private void EnsureStyles()
        {
            if (_title != null) return;
            _title = new GUIStyle(GUI.skin.label)
                { fontSize = UiSkin.Text.Title, alignment = TextAnchor.MiddleCenter };
            _section = new GUIStyle(GUI.skin.label) { fontSize = UiSkin.Text.Small };
            _item = new GUIStyle(GUI.skin.label) { fontSize = UiSkin.Text.Mid };
            _small = new GUIStyle(GUI.skin.label) { fontSize = UiSkin.Text.Small };
            _midCenter = new GUIStyle(GUI.skin.label) { fontSize = UiSkin.Text.Mid, alignment = TextAnchor.MiddleCenter };

            UiSkin.ApplyFont(_title, _section, _item, _small, _midCenter);
        }

        private void OnGUI()
        {
            if (flow == null || flow.State != GameState.Credits) return;

            UiScale.Begin();
            DrawAll();
            UiScale.End();
        }

        private void DrawAll()
        {
            EnsureStyles();

            // 크레딧은 읽는 화면이라 대비를 조금 더 준다 (대장장이보다 진하게)
            GUI.color = new Color(0.03f, 0.04f, 0.06f, 0.62f);
            GUI.DrawTexture(new Rect(0f, 0f, UiScale.W, UiScale.H), Texture2D.whiteTexture);
            GUI.color = Color.white;

            UiSkin.DrawBanner(new Rect(UiScale.W * 0.5f - 190f, UiScale.T + 22f, 380f, 46f),
                Loc.T("credits.title"), _title);

            GUI.color = new Color(1f, 1f, 1f, 0.55f);
            GUI.Label(new Rect(0f, UiScale.T + 68f, UiScale.W, 22f), Loc.T("credits.thanks"), _midCenter);
            GUI.color = Color.white;

            const float w = 620f;
            float x = (UiScale.W - w) * 0.5f;
            float top = UiScale.T + 104f;
            float bottom = UiScale.B - 74f;

            // 항목이 늘거나 글꼴이 커져도 잘리지 않도록 스크롤 안에 넣는다.
            // 좁은 폰 화면에서 마지막 줄이 버튼에 가리는 게 흔한 사고다.
            const float rowH = 62f;
            float contentH = Entries.GetLength(0) * rowH;

            _scroll = GUI.BeginScrollView(new Rect(x, top, w, bottom - top),
                _scroll, new Rect(0f, 0f, w - 20f, contentH));

            for (int i = 0; i < Entries.GetLength(0); i++)
            {
                float y = i * rowH;

                GUI.color = new Color(1f, 0.85f, 0.5f, 0.85f);
                GUI.Label(new Rect(4f, y, w - 28f, 18f), Loc.T(Entries[i, 0]), _section);

                GUI.color = Color.white;
                GUI.Label(new Rect(4f, y + 17f, w - 28f, 22f), Entries[i, 1], _item);

                if (!string.IsNullOrEmpty(Entries[i, 2]))
                {
                    GUI.color = new Color(1f, 1f, 1f, 0.45f);
                    GUI.Label(new Rect(4f, y + 38f, w - 28f, 18f), Entries[i, 2], _small);
                }
            }

            GUI.color = Color.white;
            GUI.EndScrollView();

            if (UiSkin.DrawButton(new Rect((UiScale.W - 180f) * 0.5f, UiScale.B - 62f, 180f, 40f),
                    Loc.T("common.close"), _midCenter, true, true, Color.white))
                flow.CloseCredits();
        }
    }
}
