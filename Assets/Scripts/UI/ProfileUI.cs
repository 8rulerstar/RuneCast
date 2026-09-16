using UnityEngine;
using RuneCast.Core;
using RuneCast.Gesture;
using RuneCast.Meta;

namespace RuneCast.UI
{
    /// <summary>
    /// 내 정보 — 지금까지 모은 것을 한 화면에서 본다.
    ///
    /// **정보가 흩어져 있어서 만들었다.** 룬 레벨은 대장장이의 강화 탭, 장착한
    /// 문양은 문양 탭, 별은 스테이지 선택, 주력은 상단 바. "내가 지금 얼마나
    /// 셌지"를 알려면 네 군데를 돌아야 했다.
    ///
    /// **여기서는 아무것도 바꾸지 않는다.** 강화도 장착도 대장장이에서 한다.
    /// 보는 곳과 바꾸는 곳을 섞으면 둘 다 복잡해진다 — 여기는 읽는 곳이고,
    /// 바꾸고 싶으면 대장장이로 가는 버튼이 아래에 있다.
    /// </summary>
    public class ProfileUI : MonoBehaviour
    {
        public GameFlow flow;

        private GUIStyle _title, _big, _bigLeft, _mid, _small, _midLeft, _smallLeft, _smallRight;

        private static readonly Color Gold = new Color(1f, 0.86f, 0.5f);
        private static readonly Color Dim = new Color(1f, 1f, 1f, 0.45f);

        private void OnGUI()
        {
            if (flow == null || flow.State != GameState.Profile) return;

            EnsureStyles();

            UiScale.Begin();
            Draw();
            UiScale.End();
        }

        private void EnsureStyles()
        {
            if (_title != null) return;

            _title = new GUIStyle(GUI.skin.label)
                { fontSize = UiSkin.Text.Title, alignment = TextAnchor.MiddleCenter };
            _big = new GUIStyle(GUI.skin.label)
                { fontSize = UiSkin.Text.Large, alignment = TextAnchor.MiddleCenter };
            _mid = new GUIStyle(GUI.skin.label)
                { fontSize = UiSkin.Text.Mid, alignment = TextAnchor.MiddleCenter };
            _small = new GUIStyle(GUI.skin.label)
                { fontSize = UiSkin.Text.Small, alignment = TextAnchor.MiddleCenter };
            _bigLeft = new GUIStyle(GUI.skin.label) { fontSize = UiSkin.Text.Large };
            _midLeft = new GUIStyle(GUI.skin.label) { fontSize = UiSkin.Text.Mid };
            _smallLeft = new GUIStyle(GUI.skin.label) { fontSize = UiSkin.Text.Small };
            _smallRight = new GUIStyle(GUI.skin.label)
                { fontSize = UiSkin.Text.Small, alignment = TextAnchor.MiddleRight };

            UiSkin.ApplyFont(_title, _big, _bigLeft, _mid, _small, _midLeft, _smallLeft, _smallRight);
        }

        // 레이아웃을 좌표로 손계산하지 않는다. 섹션마다 그린 높이를 돌려주고
        // 다음 섹션이 그 아래에서 시작한다 — 문양 슬롯은 별을 모으면 늘어나므로
        // 고정 좌표로 짜면 언젠가 아래 버튼과 겹친다.
        private const float PanelW = 700f;

        private void Draw()
        {
            GUI.color = new Color(0f, 0f, 0f, 0.82f);
            GUI.DrawTexture(new Rect(0f, 0f, UiScale.W, UiScale.H), Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUI.color = Gold;
            GUI.Label(new Rect(0f, UiScale.T + 14f, UiScale.W, 36f), Loc.T("profile.title"), _title);
            GUI.color = Color.white;

            float x = (UiScale.W - PanelW) * 0.5f;
            float y = UiScale.T + 56f;

            y += DrawSummary(x, y) + 18f;
            DrawRunes(x, y);

            const float bw = 190f, bh = 44f, gap = 14f;
            float by = UiScale.B - bh - 14f;
            float bx = (UiScale.W - bw * 2f - gap) * 0.5f;

            // 바꾸고 싶으면 여기서 곧장 갈 수 있게. 읽다가 "고쳐야겠다"가
            // 떠오르는 자리이므로, 그때 화면을 두 번 거치게 하면 안 된다.
            if (UiSkin.DrawButton(new Rect(bx, by, bw, bh), Loc.T("select.workshop"), _mid))
                flow.GoToWorkshop();
            if (UiSkin.DrawButton(new Rect(bx + bw + gap, by, bw, bh), Loc.T("common.back"), _mid, true))
                flow.GoToStageSelect();
        }

        /// <summary>맨 위 한 줄 — 주력·별·파편. 제일 자주 궁금한 세 개다.</summary>
        private float DrawSummary(float x, float y)
        {
            const float h = 72f;
            UiSkin.DrawPanel(new Rect(x, y, PanelW, h), new Color(0.9f, 0.92f, 0.95f));

            float cw = PanelW / 3f;
            Cell(x, y, cw, "\u25ce", Loc.T("profile.power"), CombatPower.Total.ToString(), Gold);
            Cell(x + cw, y, cw, "\u2605", Loc.T("profile.stars"),
                StageProgress.TotalStars + " / " + (StageDatabase.All.Count * 3), Gold);
            Cell(x + cw * 2f, y, cw, "\u25c8", Loc.T("common.shards"),
                PlayerData.Shards.ToString(), new Color(0.55f, 0.78f, 1f));

            // 칸 사이 세로 실선. 세 값이 한 덩어리로 뭉쳐 보이는 걸 막는다.
            GUI.color = new Color(0.16f, 0.15f, 0.13f, 0.15f);
            for (int i = 1; i < 3; i++)
                GUI.DrawTexture(new Rect(x + cw * i, y + 14f, 1f, h - 28f), Texture2D.whiteTexture);
            GUI.color = Color.white;

            return h;
        }

        private void Cell(float x, float y, float w, string icon, string label, string value, Color iconColor)
        {
            GUI.color = iconColor;
            GUI.Label(new Rect(x + 18f, y + 20f, 34f, 34f), icon, _big);
            GUI.color = new Color(0.45f, 0.43f, 0.41f);
            GUI.Label(new Rect(x + 54f, y + 14f, w - 64f, 18f), label, _smallLeft);
            GUI.color = new Color(0.14f, 0.13f, 0.12f);
            GUI.Label(new Rect(x + 54f, y + 32f, w - 64f, 28f), value, _bigLeft);
            GUI.color = Color.white;
        }

        /// <summary>
        /// 룬 아홉 종의 강화 상태.
        ///
        /// 이 게임은 룬을 잠가 두지 않는다 — 처음부터 아홉 개를 다 쓸 수 있고
        /// 강화만 쌓인다. 그래서 여기가 답해야 할 질문은 "무엇을 얼마나 키웠나"다.
        ///
        /// **숫자만으로는 그 답이 안 나온다.** Lv 4가 어느 정도인지는 만렙이
        /// 몇인지를 알아야 읽히고, 위력이 얼마나 붙었는지는 또 따로 계산해야 한다.
        /// 막대로 진행도를, 옆에 실제 배율을 같이 보여준다.
        /// </summary>
        private float DrawRunes(float x, float y)
        {
            GUI.color = Dim;
            GUI.Label(new Rect(x + 2f, y, PanelW, 20f), Loc.T("profile.runes"), _smallLeft);
            GUI.color = Color.white;

            float top = y + 24f;
            var runes = GlyphTable.Runes;

            const float gap = 8f, rh = 58f;
            int cols = 3;
            float rw = (PanelW - gap * (cols - 1)) / cols;

            for (int i = 0; i < runes.Length; i++)
            {
                RuneType t = runes[i];
                var r = new Rect(x + (i % cols) * (rw + gap), top + (i / cols) * (rh + gap), rw, rh);

                int lv = PlayerData.LevelOf(t);
                bool max = lv >= PlayerData.MaxRuneLevel;
                float ratio = (lv - 1) / (float)(PlayerData.MaxRuneLevel - 1);

                UiSkin.DrawSlot(r, lv > 1 ? Color.white : new Color(0.74f, 0.76f, 0.82f));

                // 룬 도형을 아이콘으로. 이 게임에서 룬을 부르는 이름은 사실
                // 이 도형이므로, 글자보다 이게 먼저 눈에 들어와야 한다.
                GUI.color = max ? Gold : new Color(0.28f, 0.3f, 0.36f);
                GUI.Label(new Rect(r.x + 10f, r.y + 8f, 36f, 40f), GlyphTable.Glyph(t), _big);

                GUI.color = new Color(0.16f, 0.15f, 0.13f);
                GUI.Label(new Rect(r.x + 48f, r.y + 7f, rw - 130f, 20f), GlyphTable.RuneName(t), _midLeft);

                // 만렙은 글자로 못 박는다. 막대가 꽉 찬 것과 거의 찬 것은
                // 눈으로 안 갈린다.
                GUI.color = max ? Gold : new Color(0.42f, 0.4f, 0.38f);
                GUI.Label(new Rect(r.xMax - 78f, r.y + 7f, 68f, 20f),
                    max ? Loc.T("common.max") : Loc.F("profile.level", lv), _smallRight);
                GUI.color = Color.white;

                var bar = new Rect(r.x + 48f, r.y + 32f, rw - 116f, 8f);
                UiSkin.DrawBar(bar, ratio, max ? Gold : new Color(0.45f, 0.75f, 1f));

                GUI.color = new Color(0.38f, 0.36f, 0.34f);
                GUI.Label(new Rect(bar.xMax + 6f, r.y + 27f, 58f, 18f),
                    Loc.F("profile.mult", RuneUpgrades.PowerMultiplier(t)), _smallLeft);
                GUI.color = Color.white;

                // **문양 칸을 작은 점으로.** 룬마다 세 칸이라 목록을 따로 두면
                // 스물일곱 줄이 된다. 점 세 개면 "이 룬은 다 찼나"가 한눈에 보이고,
                // 색이 등급을 말한다.
                var slots = PlayerData.EquippedOn(t);
                for (int k = 0; k < PlayerData.SlotsPerRune; k++)
                {
                    var d = new Rect(r.x + 48f + k * 11f, r.y + 43f, 8f, 8f);

                    GlyphData g = k < slots.Count ? PlayerData.FindGlyph(slots[k]) : null;
                    GUI.color = g != null
                        ? GlyphTable.RarityColor((GlyphRarity)g.rarity)
                        : new Color(0f, 0f, 0f, 0.18f);
                    GUI.DrawTexture(d, Texture2D.whiteTexture);
                }
                GUI.color = Color.white;
            }

            int rows = (runes.Length + cols - 1) / cols;
            return 24f + rows * (rh + gap) - gap;
        }

    }
}
