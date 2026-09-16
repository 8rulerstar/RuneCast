using UnityEngine;
using RuneCast.Core;
using RuneCast.Battle;
using RuneCast.Meta;

namespace RuneCast.UI
{
    /// <summary>
    /// 스테이지 선택 화면과 결과 화면.
    ///
    /// 전투 HUD와 같은 이유로 IMGUI다 — 씬/프리팹 설정이 0이라 구조를 바꾸기 쉽다.
    /// 레이아웃이 굳으면 uGUI로 옮길 것. Pixel UI pack이 그때 쓸모 있다.
    /// </summary>
    public class MetaUI : MonoBehaviour
    {
        public GameFlow flow;

        private GUIStyle _title, _big, _mid, _small, _star, _starSized, _logo, _btnBig;

        // 타이틀 후광. PrimitiveSprites.Glow는 "gl"+크기 문자열 키로 캐시를 찾으므로
        // 매 프레임 부르면 그 문자열이 프레임마다 생긴다 — 한 번 받아 들고 있는다.
        private Texture2D _titleGlow;

        // 상단 바와 타이틀 요약이 같은 것을 쓴다 — 화면을 옮겨 다녀도
        // 숫자가 한 번만 차오르면 되고, 두 개를 두면 각자 다른 값을 보여준다.
        private readonly PowerTicker _power = new PowerTicker();
        private float _resultTime;
        private GameState _shown;

        // 밝은 배경으로 뒤집으며 금색도 청동으로 눌렀다 — 밝은 노랑은 하늘에 먹힌다.
        // 결과 화면(어두운 막 위)만 밝은 금을 따로 쓴다.
        private static readonly Color Gold = new Color(0.62f, 0.42f, 0.06f);
        private static readonly Color GoldOnDark = new Color(1f, 0.85f, 0.35f);
        private static readonly Color Dim = new Color(0.2f, 0.18f, 0.16f, 0.25f);

        private void Start()
        {
            if (flow != null)
            {
                flow.StateChanged += OnStateChanged;

                // ESC가 타이틀로 나가기 전에 장 목록으로 한 단계 물러난다.
                flow.StageSelectBack = StageSelectBack;
            }
        }

        private void OnStateChanged()
        {
            // 결과 화면의 별을 순서대로 띄우려면 진입 시각이 필요하다
            if (flow.State == GameState.Result && _shown != GameState.Result)
            {
                _resultTime = Time.unscaledTime;

                // 되돌리지 않으면 두 번째 클리어부터 별 소리가 안 난다 —
                // 지난 판의 3이 남아 있어서 "이미 다 들었다"고 판단한다.
                _starsHeard = 0;
            }
            _shown = flow.State;
        }

        private void EnsureStyles()
        {
            if (_title != null) return;
            _title = new GUIStyle(GUI.skin.label) { fontSize = UiSkin.Text.Huge, alignment = TextAnchor.MiddleCenter };
            _big = new GUIStyle(GUI.skin.label) { fontSize = UiSkin.Text.Title, alignment = TextAnchor.MiddleCenter };
            _mid = new GUIStyle(GUI.skin.label) { fontSize = UiSkin.Text.Mid, alignment = TextAnchor.MiddleCenter };
            _small = new GUIStyle(GUI.skin.label) { fontSize = UiSkin.Text.Small, alignment = TextAnchor.MiddleCenter };
            _star = new GUIStyle(GUI.skin.label) { fontSize = UiSkin.Text.Large, alignment = TextAnchor.MiddleCenter };
            // 크기만 바꿔 쓰는 별 스타일. 매 프레임 new 하면 GC가 계속 돈다.
            _starSized = new GUIStyle(_star);

            // 타이틀 로고 전용. Huge(32)는 화면 곳곳의 소제목과 같은 값이라,
            // 켜자마자 보이는 화면에서 제목이 소제목만 해 보였다.
            // 예외 크기를 쓰는 근거는 UiSkin.Text.Logo 설명에 있다.
            _logo = new GUIStyle(GUI.skin.label)
                { fontSize = UiSkin.Text.Logo, alignment = TextAnchor.MiddleCenter };

            // 타이틀 버튼은 목록 버튼보다 크다. 이 화면엔 버튼이 셋뿐이고
            // 화면 대부분이 배경이라, 목록과 같은 크기면 여백만 남는다.
            _btnBig = new GUIStyle(GUI.skin.label)
                { fontSize = UiSkin.Text.Title, alignment = TextAnchor.MiddleCenter };

            UiSkin.ApplyFont(_title, _big, _mid, _small, _star, _starSized, _logo, _btnBig);
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

            // 설정 화면이 열렸으면 그쪽이 전체를 덮는다
            if (flow.State == GameState.Title) DrawTitle();
            else if (flow.State == GameState.StageSelect) { DrawStageSelect(); GUI.enabled = true; DrawConfirm(); }
            else if (flow.State == GameState.Result) DrawResult();
        }

        // ── 타이틀 ──────────────────────────────────────────────

        /// <summary>
        /// 타이틀 화면.
        ///
        /// 켜자마자 스테이지 목록이 뜨면 게임이 아니라 도구처럼 보인다.
        /// 한 박자 쉬어 가는 화면이 있어야 "시작했다"는 감각이 생긴다.
        ///
        /// **왼쪽은 비워 둔다.** 거기서 TitleRune이 룬을 그린다 —
        /// 이 게임이 뭔지 말해 주는 건 글씨가 아니라 그 그림이다.
        /// 좁은 화면(4:3 등)에서는 룬이 가운데로 오므로 글씨를 아래로 내린다.
        /// </summary>
        private void DrawTitle()
        {
            DrawScrim(0.20f);

            // 언어 버튼 — 우상단. 설정 안에도 있지만 **타이틀에서 바로 바꿀 수 있어야 한다**:
            // 기본이 영어가 되면서, 한국어 사용자가 "설정 → 언어"라는 영어 메뉴를
            // 읽고 찾아 들어가야 하는 문제가 생겼다. 라벨은 **지금 언어**다 —
            // 상태 표시로 읽히고, 두 개뿐이라 누르면 바뀐다는 건 눌러 보면 안다.
            if (Button(new Rect(UiScale.R - 150f, UiScale.T + 14f, 136f, 42f),
                    Loc.LanguageName(Loc.Current)))
                GameSettings.Language = Loc.Current == Lang.Korean ? Lang.English : Lang.Korean;

            // 전체화면 — 웹에서만. itch 임베드가 작을 때 이게 없으면 답이 없다.
            // 브라우저는 사용자 입력 안에서만 전체화면을 허락하므로 버튼 클릭이 정확히 그 자리다.
            // 데스크톱 빌드는 설정에 이미 있고, 폰 브라우저는 itch의 전체화면 버튼이 맡는다.
            if (Application.platform == RuntimePlatform.WebGLPlayer)
            {
                if (Button(new Rect(UiScale.R - 150f, UiScale.T + 64f, 136f, 42f),
                        Loc.T("title.fullscreen")))
                    Screen.fullScreen = !Screen.fullScreen;
            }

            // 화면비에 따라 두 배치를 쓴다. 가로로 길면 좌우로 나누고,
            // 좁으면 위아래로 쌓는다 — 좁은 화면에서 좌우로 나누면 둘 다 낀다.
            bool wide = UiScale.W / UiScale.H > 1.5f;

            float cx = wide ? UiScale.W * 0.68f : UiScale.W * 0.5f;
            // 기준 높이를 780으로 내리면서 위로 올렸다. 예전 값(가로 H*0.5-150,
            // 세로 H*0.5+10)은 가상 높이 900을 전제한 것이라, 그대로 두면
            // 버튼 셋이 하단 안전영역을 뚫는다 — 폰에서는 마지막 버튼이 안 눌린다.
            float top = wide ? UiScale.H * 0.42f - 130f : UiScale.H * 0.37f;

            // 글씨 뒤 금빛 후광 + 아주 느린 맥동. 검은 하늘에 맨 글씨만 있으면
            // 로고가 아니라 자리표시자처럼 보인다 — 뒤에 빛이 고여 있어야
            // "여기가 제목"이라는 무게가 생긴다. 맥동은 알아챌 듯 말 듯한 폭으로:
            // 눈에 띄게 깜빡이면 버튼처럼 읽혀서 누르고 싶어진다.
            if (_titleGlow == null) _titleGlow = PrimitiveSprites.Glow(128).texture;
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 0.9f);

            // 후광도 글씨를 따라 키운다. 글씨만 키우면 후광이 로고 안쪽에 갇혀서
            // "뒤에 빛이 고여 있다"가 아니라 "글씨에 얼룩이 졌다"로 보인다.
            // 상자를 화면 안으로 자른다. 가운데 정렬이라 넘쳐도 글씨는 제자리에
            // 오지만, 좁은 화면에서 상자만 화면 밖으로 나가 있으면 나중에 이 값을
            // 기준으로 무언가를 배치할 때 조용히 어긋난다.
            float logoW = Mathf.Min(800f, UiScale.W - 24f);

            GUI.color = new Color(1f, 0.78f, 0.3f, 0.3f + 0.08f * pulse);
            GUI.DrawTexture(new Rect(cx - logoW * 0.54f, top - 110f, logoW * 1.08f, 300f), _titleGlow);
            GUI.color = Color.white;

            // 밝은 하늘 위라 글씨는 진한 청동. 맥동은 색이 아니라 밝기로 준다.
            GUI.color = Color.Lerp(new Color(0.42f, 0.27f, 0.05f), new Color(0.55f, 0.36f, 0.08f), pulse);
            GUI.Label(new Rect(cx - logoW * 0.5f, top - 12f, logoW, 104f), Loc.T("title"), _logo);
            GUI.color = Color.white;

            // 부제(태그라인)를 뺐다. 무엇을 하는 게임인지는 타이틀에서 룬이
            // 저절로 그려지는 것(RuneTracer)이 이미 말한다. 그 위에 문장을 얹으면
            // 그림이 말한 걸 글로 한 번 더 설명하는 꼴이라 촌스러워진다.

            bool hasProgress = StageProgress.TotalStars > 0;

            // 이어하기라면 지금까지의 성과를 보여준다. 켜자마자 "내 계정"이라는 감각이
            // 생기는 자리이고, 주력은 그러라고 만든 숫자다.
            // 로고가 커진 만큼 아래로 내린다 (로고 상자가 top-12에서 104 높이).
            float y = top + (wide ? 120f : 108f);
            if (hasProgress)
            {
                DrawTitleSummary(cx, y);
                y += 60f;
            }

            // 목록 버튼(300×58)보다 크게 잡는다. 이 화면은 버튼이 셋뿐이고
            // 나머지가 전부 배경이라, 같은 크기를 쓰면 화면이 비어 보인다.
            //
            // **세로 화면에서는 한 치수 줄인다.** 좁은 배치는 로고를 화면 중앙
            // 아래에 놓고 버튼을 그 밑에 쌓는데, 가로와 같은 크기로 세 개를 쌓으면
            // 마지막 버튼이 안전영역(UiScale.B) 아래로 내려간다. 폰에서는
            // 홈 인디케이터에 깔려서 눌리지 않는 버튼이 된다.
            // **화면보다 넓으면 안 된다.** 360 가상폭(안드로이드 세로)에서
            // 340짜리 버튼이 안전영역 밖으로 삐져나갔다.
            float bw = Mathf.Min(wide ? 400f : 340f, UiScale.W - 48f);
            float bh = wide ? 78f : 66f;
            float gap = wide ? 16f : 14f;
            float bx = cx - bw * 0.5f;

            // **언제나 "게임 시작"이다.** 예전엔 진행이 있으면 "이어하기"로 바뀌었는데,
            // 어차피 같은 곳(스테이지 선택)으로 간다. 같은 버튼이 이름을 바꾸면
            // 다른 곳으로 가는 줄 알고 한 번 더 생각하게 된다.
            // 이어서 하는 중이라는 건 바로 위의 진행 요약(주력·별·판)이 이미 말한다.
            // 처음 누르면 프롤로그로 간다. **버튼 이름은 그대로 "게임 시작"이다** —
            // 위와 같은 이유이기도 하고, 프롤로그를 예고하면 그게 연출이 아니라
            // 건너뛸 수 있는 무언가로 보인다. 한 번 보고 나면 다시 오지 않는다.
            if (TitleButton(new Rect(bx, y, bw, bh), Loc.T("title.start"), true))
            {
                // 타이틀에서 들어올 때는 장 목록부터. 판을 깨고 돌아올 때는
                // 펼쳐 둔 장이 그대로 남아야 하므로 그쪽에서는 건드리지 않는다.
                _openChapter = 0;

                if (TutorialProgress.Seen(TutorialStep.Prologue)) flow.GoToStageSelect();
                else flow.StartPrologue();
            }

            // 할 게 남아 있으면 여기서부터 알린다 — 스테이지 선택까지 안 들어가도 보이게
            if (hasProgress && Notify.Workshop)
                UiSkin.DrawRedDot(new Rect(bx, y, bw, bh));

            y += bh + gap;
            if (TitleButton(new Rect(bx, y, bw, bh - 10f), Loc.T("select.settings"))) flow.GoToSettings();

            // iOS는 앱을 코드로 끄는 걸 권장하지 않는다 (심사에서 지적받는 항목)
#if !UNITY_IOS
            y += bh + gap - 10f;
            if (TitleButton(new Rect(bx, y, bw, bh - 10f), Loc.T("title.quit"))) flow.QuitGame();
#endif

            GUI.color = Color.white;
        }

        /// <summary>타이틀의 진행 요약 — 주력 · 별 · 깬 판.</summary>
        private void DrawTitleSummary(float cx, float y)
        {
            // 버튼 폭과 맞춘다. 요약만 좁으면 가운데 정렬인데도 어긋나 보인다.
            float w = UiScale.W / UiScale.H > 1.5f ? 400f : 340f;
            var r = new Rect(cx - w * 0.5f, y, w, 46f);
            UiSkin.DrawPanel(r, new Color(0.95f, 0.93f, 0.78f, 0.95f));

            int cleared = 0;
            var all = StageDatabase.All;
            for (int i = 0; i < all.Count; i++)
                if (StageProgress.IsCleared(all[i].Id)) cleared++;

            _power.Update(CombatPower.Total);
            _power.Draw(new Rect(r.x + 12f, r.y + 8f, 150f, 24f), _small, _small);

            GUI.color = Gold;
            GUI.Label(new Rect(r.x + w - 162f, r.y + 8f, 150f, 24f),
                string.Format("★ {0}   {1}/{2}", StageProgress.TotalStars, cleared, all.Count), _small);
            GUI.color = Color.white;
        }

        // ── 스테이지 선택 ────────────────────────────────────────

        /// <summary>
        /// 지금 펼쳐 본 장. 0이면 장 목록.
        ///
        /// **별도 화면(GameState)으로 만들지 않았다.** 일시정지와 같은 이유다 —
        /// 상태를 늘리면 설정·크레딧으로 드나드는 전이가 전부 새 칸이 된다.
        /// 실제로는 같은 화면의 두 모습이다.
        /// </summary>
        private int _openChapter;

        /// <summary>
        /// 뒤로. 장을 펼쳐 봤다면 장 목록으로 먼저 돌아간다.
        /// ESC를 받는 GameFlow가 이걸 먼저 물어본다. true면 여기서 처리한 것이다.
        /// </summary>
        private bool StageSelectBack()
        {
            if (_openChapter == 0) return false;
            _openChapter = 0;
            return true;
        }

        private void DrawStageSelect()
        {
            // 확인 창이 떠 있으면 뒤쪽은 안 눌리게 한다. IMGUI는 그리는 쪽이
            // 스스로 빠지지 않으면 계속 입력을 받는다.
            GUI.enabled = _confirm == null;

            DrawScrim(0.22f);

            DrawTopBar();
            DrawInkPanel();

            // 장을 아직 안 골랐으면 장 목록을 그린다.
            if (_openChapter == 0)
            {
                DrawChapterGrid();
                DrawBottomNav();
                return;
            }

            DrawNextStageCta();

            var all = StageDatabase.StagesIn(_openChapter);

            const float chh = 112f, gap = 16f;
            int perRow; float cw;
            // 최소 폭은 좁은 화면에서 **두 개가 들어가는** 값이어야 한다.
            // 150으로 올렸다가 세로 화면이 한 줄에 하나가 되어 여섯 줄로 쌓였다.
            GridMetrics(124f, 210f, gap, 3, out perRow, out cw);

            // 한 장만 그리므로 장 머리글도 한 번뿐이다. 예전엔 전 판을 한 화면에
            // 늘어놓고 장마다 머리글을 끼웠는데, 그 방식은 세로가 모자라면
            // **남은 판을 조용히 잘라 버렸다.** 판이 42개가 되면서 못 쓰게 됐다.
            float listTop = UiScale.T + 214f;
            float listBottom = UiScale.B - 84f;
            float rows = Mathf.Ceil(all.Count / (float)perRow);
            float listH = rows * (chh + gap) + 30f;
            float y = listTop + Mathf.Max(0f, (listBottom - listTop - listH) * 0.5f);
            int col = 0;

            GUI.color = UiSkin.Ink.OnDark;
            GUI.Label(new Rect(0f, y, UiScale.W, 24f), Loc.F("select.chapter", _openChapter), _mid);

            // **폰에는 ESC가 없다.** 장 안으로 들어오는 길을 만들었으면 나가는
            // 길도 화면에 있어야 한다 — 하단 내비는 전부 다른 화면으로 가는
            // 버튼이라 거기 섞으면 "한 단계 뒤로"가 아니라 "여길 떠남"이 된다.
            if (Button(new Rect(UiScale.L + 24f, y - 8f, 132f, 40f), Loc.T("common.back")))
                _openChapter = 0;

            y += 30f;

            for (int i = 0; i < all.Count; i++)
            {
                if (col >= perRow)
                {
                    col = 0;
                    y += chh + gap;
                }

                // 하단 내비 자리를 침범하면 버튼이 카드에 가린다
                if (y + chh > UiScale.B - 80f) break;

                float rowWidth = Mathf.Min(perRow, all.Count - i + col) * (cw + gap) - gap;
                float x0 = (UiScale.W - rowWidth) * 0.5f;
                DrawStageCard(new Rect(x0 + col * (cw + gap), y, cw, chh), all[i]);
                col++;
            }

            DrawBottomNav();
        }

        /// <summary>
        /// 장 목록.
        ///
        /// 판이 12개일 때는 전부 한 화면에 늘어놓는 게 나았다. 42개가 되면서
        /// 그게 불가능해졌고, 무한 스크롤을 붙이는 것보다 **한 단계를 두는 쪽**을
        /// 골랐다 — 스크롤은 지금 어디쯤인지가 안 남고, 이 게임은 "어느 장까지
        /// 왔는가"가 진행의 단위다.
        ///
        /// 카드에 별을 모아 보여주는 게 핵심이다. 장을 여는 이유가 "몇 개 남았나"
        /// 하나뿐이라, 그 숫자가 목록에 없으면 전부 들어가 봐야 안다.
        /// </summary>
        /// <summary>
        /// 카드 격자의 한 줄 개수와 폭을 화면에 맞춘다.
        ///
        /// **세로 화면에서 카드가 잘리던 걸 막는다.** 예전엔 카드 폭이 상수라
        /// 좁은 화면에서는 한 줄에 하나씩 놓였고, 그러면 줄 수가 늘어 아래쪽
        /// 카드가 하단 내비에 걸려 통째로 안 그려졌다 — 7장 중 5·6·7장이
        /// 화면에 아예 없는 상태가 된다.
        ///
        /// 최소 폭을 정해 두고 몇 개가 들어가는지 먼저 센 다음, 남는 자리를
        /// 카드가 나눠 갖는다. 넓은 화면에서 한 줄에 다 늘어놓지 않는 건
        /// 카드가 너무 넓어지면 격자로 안 읽히기 때문이다.
        /// </summary>
        private static void GridMetrics(float minW, float maxW, float gap, int maxPerRow,
            out int perRow, out float cardW)
        {
            float avail = UiScale.W - 80f;
            perRow = Mathf.Max(1, Mathf.FloorToInt((avail + gap) / (minW + gap)));
            perRow = Mathf.Min(perRow, maxPerRow);
            cardW = Mathf.Min(maxW, (avail - (perRow - 1) * gap) / perRow);
        }

        // ── 장 목록 — 옆으로 넘기는 방식 ────────────────────────
        //
        // **격자에서 넘기기로 바꿨다.** 일곱 장을 작은 카드로 늘어놓으면 좁은
        // 화면에서 넉 줄이 되고, 무엇보다 **적어 보인다.** 한 장이 화면을 채우고
        // 손으로 넘기면 뒤에 더 있다는 게 손끝으로 읽힌다.
        //
        // 카드에 그 장의 **상징 적**을 크게 띄운다. 대표 그림을 새로 그리는 대신
        // 게임에 이미 있는 스프라이트를 쓴다 — 화풍이 어긋날 일이 없고,
        // 적을 바꾸면 목록도 저절로 따라온다.

        /// <summary>지금 보고 있는 장(실수). 넘기는 동안 값이 이어진다.</summary>
        private float _pager = 0f;

        /// <summary>손을 뗐을 때 맞춰 갈 장. 0부터 센다.</summary>
        private int _pagerTarget = 0;

        private bool _pagerDrag;
        private float _pagerGrabX;
        private float _pagerGrabAt;
        private float _pagerMoved;

        /// <summary>이만큼 넘겨야 다음 장으로 친다. 손떨림을 탭으로 안 읽히게 하는 값이기도 하다.</summary>
        private const float TapSlop = 12f;

        /// <summary>
        /// 그 장을 한마디로 보여줄 적.
        ///
        /// 각 장의 **규칙과 맞물리는** 적을 골랐다 — 서리는 못 따라잡는 빠른 적,
        /// 안개는 뒤에 숨는 원거리, 부패는 죽으면 쪼개지는 것, 봉인은 표적을
        /// 고르게 만드는 것, 심연은 잡을 게 없는 보스.
        /// </summary>
        private static UnitKind FaceOf(int chapter)
        {
            switch (chapter)
            {
                case 1:  return UnitKind.Wraith;
                case 2:  return UnitKind.Bonelord;
                case 3:  return UnitKind.Vampire;
                case 4:  return UnitKind.Gnoll;
                case 5:  return UnitKind.Spider;
                case 6:  return UnitKind.Shaman;
                default: return UnitKind.Troll;
            }
        }

        private void DrawChapterGrid()
        {
            int n = StageDatabase.ChapterCount;

            float cw = Mathf.Min(430f, UiScale.W - 90f);
            float chh = Mathf.Min(300f, UiScale.B - UiScale.T - 300f);
            float step = cw * 0.80f;                 // 옆 카드가 살짝 보이게 겹친다

            float top = UiScale.T + 214f;
            float bottom = UiScale.B - 84f;
            float cy = (top + bottom) * 0.5f;

            var area = new Rect(0f, top, UiScale.W, bottom - top);
            HandlePagerInput(area, step, n);

            GUI.color = UiSkin.Ink.OnDark;
            GUI.Label(new Rect(0f, top - 4f, UiScale.W, 24f), Loc.T("select.chapterPick"), _mid);

            // 가운데에서 먼 것부터 그려야 가운데 카드가 위에 온다.
            for (int pass = 1; pass >= 0; pass--)
            {
                for (int ch = 1; ch <= n; ch++)
                {
                    float d = (ch - 1) - _pager;
                    if (Mathf.Abs(d) > 1.7f) continue;

                    bool center = Mathf.Abs(d) < 0.5f;
                    if ((pass == 0) != center) continue;

                    // 멀수록 작고 흐리다. 깊이가 있어야 "옆에 더 있다"로 읽힌다.
                    float k = Mathf.Clamp01(1f - Mathf.Abs(d) * 0.28f);
                    float w = cw * k, h = chh * k;

                    var r = new Rect(UiScale.W * 0.5f + d * step - w * 0.5f, cy - h * 0.5f, w, h);
                    DrawChapterCard(r, ch, k, center);
                }
            }

            DrawPagerDots(n, bottom - 26f);
        }

        /// <summary>
        /// 끌어서 넘긴다.
        ///
        /// **탭과 끌기를 거리로 가른다.** 카드를 누르는 것과 넘기는 것이 같은
        /// 손짓으로 시작하므로, 움직인 거리가 TapSlop을 넘으면 그때부터 넘기기로
        /// 보고 카드 클릭은 취소한다. 안 그러면 넘길 때마다 장이 열린다.
        /// </summary>
        private void HandlePagerInput(Rect area, float step, int n)
        {
            Event e = Event.current;

            if (e.type == EventType.MouseDown && area.Contains(e.mousePosition))
            {
                _pagerDrag = true;
                _pagerGrabX = e.mousePosition.x;
                _pagerGrabAt = _pager;
                _pagerMoved = 0f;
            }
            else if (e.type == EventType.MouseDrag && _pagerDrag)
            {
                float dx = e.mousePosition.x - _pagerGrabX;
                _pagerMoved = Mathf.Max(_pagerMoved, Mathf.Abs(dx));
                _pager = Mathf.Clamp(_pagerGrabAt - dx / step, -0.35f, n - 1 + 0.35f);
                e.Use();
            }
            else if (e.type == EventType.MouseUp && _pagerDrag)
            {
                _pagerDrag = false;

                // 반 칸을 넘겼으면 다음 장으로. 그 아래는 제자리로 돌아간다.
                _pagerTarget = Mathf.Clamp(Mathf.RoundToInt(_pager), 0, n - 1);
                if (_pagerMoved > TapSlop) e.Use();
            }

            if (!_pagerDrag)
                _pager = Mathf.MoveTowards(_pager, _pagerTarget,
                    Time.unscaledDeltaTime * 7f * Mathf.Max(0.35f, Mathf.Abs(_pagerTarget - _pager)));
        }

        /// <summary>지금 몇 번째인지. 넘기는 동안 어디쯤인지가 없으면 길을 잃는다.</summary>
        private void DrawPagerDots(int n, float y)
        {
            const float d = 11f, gap = 11f;
            float x = (UiScale.W - (n * d + (n - 1) * gap)) * 0.5f;

            for (int i = 0; i < n; i++)
            {
                bool on = Mathf.Abs(_pager - i) < 0.5f;
                GUI.color = on ? new Color(1f, 0.85f, 0.42f, 0.95f)
                               : new Color(1f, 1f, 1f, 0.28f);
                GUI.DrawTexture(new Rect(x + i * (d + gap), y, d, d), Texture2D.whiteTexture);
            }
            GUI.color = Color.white;
        }

        private void DrawChapterCard(Rect r, int chapter, float k, bool center)
        {
            var list = StageDatabase.StagesIn(chapter);
            if (list.Count == 0) return;

            bool open = StageDatabase.IsChapterUnlocked(chapter);

            int stars = 0, max = list.Count * 3;
            for (int i = 0; i < list.Count; i++) stars += StageProgress.StarsOf(list[i].Id);

            // **양피지색 판에 어두운 잉크.** 화면의 나머지가 전부 그 규칙이다
            // (상단 바·확인 창·강화 목록). 처음엔 여기만 어두운 카드로 만들었는데,
            // 그러면 이 화면에서만 규칙이 뒤집혀서 어색하고 — 무엇보다 어두운 판에
            // 어두운 잉크(Ink.OnDark)를 얹어 놔서 **글씨가 안 읽혔다.**
            //
            // 그 장의 바닥 색을 크림색에 섞는다. 목록에서 본 색이 판에 들어가면
            // 그대로 나오므로 카드가 그 장의 **미리보기**가 되고, 섞는 쪽이라
            // 회색으로 죽지 않는다.
            Color cream = new Color(0.96f, 0.93f, 0.80f);
            Color bg = open
                ? Color.Lerp(cream, Backdrop.CardAccentOf(chapter), 0.45f)
                // 잠긴 장도 회색이 아니라 **바랜 양피지**다. 회색은 UI가 아니라
                // 고장 난 것처럼 보인다.
                : new Color(0.72f, 0.69f, 0.62f, 0.95f);

            UiSkin.DrawPanel(r, bg);

            // 상징 적. 잠긴 장은 실루엣만 — 무엇이 기다리는지는 알려주되
            // 자세히는 안 보여준다.
            var def = UnitVisuals.Of(FaceOf(chapter));
            Texture2D sheet = Resources.Load<Texture2D>("Sprites/Units/" + def.Prefix + "-Idle");
            var face = new Rect(r.center.x - r.height * 0.34f, r.y + r.height * 0.10f,
                                r.height * 0.68f, r.height * 0.68f);
            UiSkin.DrawSpriteFrame(face, sheet, def.FrameSize, 0,
                open ? Color.white : new Color(0f, 0f, 0f, 0.55f), 1.7f);

            // 장 번호와 규칙 이름
            GUI.color = open ? UiSkin.Ink.OnPanel : UiSkin.Ink.OnPanelMuted;
            GUI.Label(new Rect(r.x, r.y + 12f, r.width, 38f),
                Loc.F("select.chapter", chapter), _big);

            if (open)
            {
                // 규칙 이름은 이 카드에서 제일 중요한 한 줄이다 — 한 단 키운다.
                GUI.color = UiSkin.Ink.OnPanelWarm;
                GUI.Label(new Rect(r.x, r.yMax - 86f, r.width, 30f),
                    Loc.T(ChapterRuleNameKey(chapter)), _big);

                var bar = new Rect(r.x + 34f, r.yMax - 48f, r.width - 68f, 12f);
                UiSkin.DrawPanel(bar, new Color(0.16f, 0.15f, 0.14f, 0.85f));
                if (stars > 0)
                    UiSkin.DrawPanel(new Rect(bar.x, bar.y, bar.width * stars / max, bar.height),
                        new Color(1f, 0.82f, 0.32f, 0.95f));

                GUI.color = UiSkin.Ink.OnPanelDim;
                GUI.Label(new Rect(r.x, r.yMax - 34f, r.width, 22f),
                    string.Format("\u2605 {0}/{1}   \u00b7   {2}", stars, max,
                        Loc.F("select.chapterRange", list[0].Id, list[list.Count - 1].Id)), _small);
            }
            else
            {
                GUI.color = UiSkin.Ink.OnPanelMuted;
                GUI.Label(new Rect(r.x, r.yMax - 60f, r.width, 26f),
                    Loc.F("select.chapterLock", chapter - 1), _mid);
            }
            GUI.color = Color.white;

            // **가운데 카드만 눌린다.** 옆 카드를 누르면 넘어가는 게 아니라
            // 열려 버려서, 훑어보다 잘못 들어가는 일이 생긴다.
            if (!center || !open) return;

            UiSkin.DrawFrame(r, new Color(1f, 0.86f, 0.5f, 0.55f));

            Event e = Event.current;
            if (e.type == EventType.MouseUp && r.Contains(e.mousePosition)
                && _pagerMoved <= TapSlop && !_pagerDrag)
            {
                AudioManager.Play(Sfx.DrawBegin, 0.5f);
                _openChapter = chapter;
                e.Use();
            }
        }

        /// <summary>장 규칙 이름 키. ChapterRules.Of와 같은 순서를 쓴다.</summary>
        private static string ChapterRuleNameKey(int chapter)
        {
            return "rule." + ChapterRules.Of(chapter).ToString().ToLowerInvariant();
        }


        // ── 상단 재화 바 ────────────────────────────────────────
        //
        // 재화를 화면마다 다른 자리에 두면 "얼마 있더라"를 볼 때마다 눈이 헤맨다.
        // 항상 같은 줄에 두는 게 이 관습의 전부이고, 그것만으로 화면이 정돈된다.

        private void DrawTopBar()
        {
            const float h = 52f;
            var bar = new Rect(UiScale.L + 12f, UiScale.T + 10f, UiScale.W - UiScale.L * 2f - 24f, h);
            UiSkin.DrawPanel(bar, new Color(0.95f, 0.93f, 0.78f, 0.95f));

            GUI.color = UiSkin.Ink.OnPanel;
            GUI.Label(new Rect(bar.x + 16f, bar.y + 8f, 260f, 26f), Loc.T("title"), _mid);
            GUI.color = Color.white;

            // 주력 — 키운 것 전부가 이 숫자 하나로 모인다.
            // 강화를 눌러도 화면에서 달라지는 게 없던 문제를 이게 메운다.
            _power.Update(CombatPower.Total);
            _power.Draw(new Rect(bar.x + bar.width * 0.5f - 130f, bar.y + 8f, 260f, 26f),
                _mid, _small);

            GUI.color = Gold;
            GUI.Label(new Rect(bar.xMax - 320f, bar.y + 8f, 150f, 26f),
                string.Format("★ {0}/{1}", StageProgress.TotalStars, StageProgress.MaxPossibleStars), _mid);

            GUI.Label(new Rect(bar.xMax - 170f, bar.y + 8f, 158f, 26f),
                Loc.T("common.shards") + " " + PlayerData.Shards, _mid);
            GUI.color = Color.white;
        }

        // ── 다음에 할 것 ────────────────────────────────────────
        //
        // 격자만 있으면 "어디부터 하지"를 매번 눈으로 찾아야 한다.
        // 다음에 도전할 판을 크게 띄우고 나머지는 아래에 둔다 —
        // 켜자마자 누를 것이 화면에서 제일 큰 물건이어야 한다.

        private StageDef NextStage()
        {
            var all = StageDatabase.All;

            // 아직 못 깬 것 중 첫 번째
            for (int i = 0; i < all.Count; i++)
                if (StageProgress.IsUnlocked(all[i].Id) && !StageProgress.IsCleared(all[i].Id))
                    return all[i];

            // 다 깼으면 별이 모자란 것 중 첫 번째 (3별 채우기)
            for (int i = 0; i < all.Count; i++)
                if (StageProgress.IsUnlocked(all[i].Id) && StageProgress.StarsOf(all[i].Id) < 3)
                    return all[i];

            return all.Count > 0 ? all[all.Count - 1] : null;
        }

        private void DrawNextStageCta()
        {
            StageDef st = NextStage();
            if (st == null) return;

            // 상단 재화 바가 T+10에서 T+52까지다. 라벨(20px)이 버튼 위에 붙으므로
            // 버튼은 최소 T+72부터 시작해야 라벨이 바에 안 씹힌다.
            const float w = 470f, h = 74f;
            var r = new Rect((UiScale.W - w) * 0.5f, UiScale.T + 78f, w, h);

            GUI.color = UiSkin.Ink.OnDarkDim;
            GUI.Label(new Rect(r.x, r.y - 20f, r.width, 20f), Loc.T("select.next"), _small);
            GUI.color = Color.white;

            int stars = StageProgress.StarsOf(st.Id);
            string label = st.Id + ". " + st.Name + (stars > 0 ? "   " + Stars(stars) : "");

            if (Button(r, label, true)) _confirm = st;
        }

        private static string Stars(int n)
        {
            return n >= 3 ? "★★★" : n == 2 ? "★★☆" : n == 1 ? "★☆☆" : "";
        }

        // ── 하단 내비게이션 ──────────────────────────────────────
        //
        // 갈 수 있는 곳을 한 줄에 모은다. 우상단에 흩어 두면 화면마다 자리가 달라져서
        // 매번 찾게 되고, 폰에서는 손가락이 제일 안 닿는 자리이기도 하다.

        private void DrawBottomNav()
        {
            const float bw = 200f, bh = 60f, gap = 14f;
            float total = bw * 4f + gap * 3f;
            float x = (UiScale.W - total) * 0.5f;
            float y = UiScale.B - bh - 12f;

            var shop = new Rect(x, y, bw, bh);
            if (Button(shop, Loc.T("select.workshop"))) flow.GoToWorkshop();

            // 쓸 수 있는 파편이 놀고 있으면 알린다. 이게 없으면 파편이 그냥 쌓인다.
            if (Notify.Workshop) UiSkin.DrawRedDot(shop);

            if (Button(new Rect(x + bw + gap, y, bw, bh), Loc.T("select.settings")))
                flow.GoToSettings();

            if (Button(new Rect(x + (bw + gap) * 2f, y, bw, bh), Loc.T("profile.title")))
                flow.GoToProfile();

            if (Button(new Rect(x + (bw + gap) * 3f, y, bw, bh), Loc.T("title.home")))
                flow.GoToTitle();

            GUI.color = new Color(0.3f, 0.28f, 0.26f, 0.6f);
            GUI.Label(new Rect(0f, y - 20f, UiScale.W, 18f), Loc.T("select.hint"), _small);
            GUI.color = Color.white;
        }

        /// <summary>
        /// 잉크 성장 표시. 별을 모으면 한 번에 그을 수 있는 길이가 늘고,
        /// 그게 곧 룬의 크기다. 이 인과가 안 보이면 별을 모을 이유가 없다.
        /// </summary>
        private void DrawInkPanel()
        {
            int stars = StageProgress.TotalStars;
            int tier = InkBudget.TierOf(stars);
            int need = InkBudget.StarsToNextTier(stars);

            const float w = 380f, h = 18f;
            float x = (UiScale.W - w) * 0.5f;
            float y = UiScale.T + 172f;

            GUI.color = UiSkin.Ink.OnDarkDim;
            GUI.Label(new Rect(0f, y - 22f, UiScale.W, 20f),
                Loc.F("ink.tier", tier + 1, InkBudget.TierCount), _small);

            // 단계를 칸으로 보여준다. 막대 하나보다 "몇 번 더 오르나"가 읽힌다.
            float seg = w / InkBudget.TierCount;
            for (int i = 0; i < InkBudget.TierCount; i++)
            {
                GUI.color = i <= tier ? new Color(0.85f, 0.58f, 0.15f, 0.95f) : new Color(0.2f, 0.18f, 0.16f, 0.14f);
                GUI.DrawTexture(new Rect(x + i * seg + 2f, y, seg - 4f, h), Texture2D.whiteTexture);
            }

            GUI.color = new Color(0.3f, 0.28f, 0.26f, 0.7f);
            GUI.Label(new Rect(0f, y + h + 3f, UiScale.W, 20f),
                need < 0 ? Loc.T("ink.maxTier") : Loc.F("ink.next", need), _small);

            GUI.color = Color.white;
        }

        // 스테이지 카드의 글자는 언어가 바뀔 때 말고는 안 변한다. 그런데 카드 12장을
        // OnGUI가 프레임당 두세 번 그리므로, 그대로 두면 프레임마다 수십 개의 문자열이
        // 새로 생긴다. 모바일에서 이런 게 모이면 GC가 끊김으로 나타난다.
        private static string[] _cardTitle, _cardInfo;
        private static Lang _cardLang = (Lang)(-1);

        private static void EnsureCardText()
        {
            if (_cardTitle != null && _cardLang == GameSettings.Language) return;
            _cardLang = GameSettings.Language;

            var all = StageDatabase.All;
            _cardTitle = new string[all.Count];
            _cardInfo = new string[all.Count];

            for (int i = 0; i < all.Count; i++)
            {
                _cardTitle[i] = all[i].Id + ". " + all[i].Name;
                _cardInfo[i] = Loc.F("select.waveInfo", all[i].Waves.Length, all[i].TotalEnemies);
            }
        }

        private static string CardTitle(StageDef st)
        {
            EnsureCardText();
            int i = st.Id - 1;
            return i >= 0 && i < _cardTitle.Length ? _cardTitle[i] : st.Name;
        }

        private static string CardInfo(StageDef st)
        {
            EnsureCardText();
            int i = st.Id - 1;
            return i >= 0 && i < _cardInfo.Length ? _cardInfo[i] : "";
        }

        private void DrawStageCard(Rect r, StageDef st)
        {
            bool unlocked = StageProgress.IsUnlocked(st.Id);
            int stars = StageProgress.StarsOf(st.Id);

            // 잠긴 칸은 어둡게 눌러 둔다 — 테두리 색만으로는 한눈에 안 갈린다
            // 잠긴 카드가 푸른 회색이었다 — 화면에서 제일 못생긴 자리였고,
            // 목록의 절반이 그 색이라 화면 전체가 회색으로 읽혔다.
            UiSkin.DrawPanel(r, unlocked ? Color.white : new Color(0.74f, 0.70f, 0.63f, 0.95f));

            if (!unlocked)
            {
                GUI.color = new Color(0.34f, 0.31f, 0.27f, 0.9f);
                GUI.Label(new Rect(r.x, r.y + 36f, r.width, 34f), Loc.T("common.locked"), _big);
                GUI.color = Color.white;
                return;
            }

            GUI.color = UiSkin.Available ? new Color(0.14f, 0.13f, 0.12f) : Color.white;
            GUI.Label(new Rect(r.x, r.y + 8f, r.width, 30f), CardTitle(st), _big);

            GUI.color = UiSkin.Available ? new Color(0.3f, 0.29f, 0.27f) : new Color(1f, 1f, 1f, 0.45f);
            GUI.Label(new Rect(r.x, r.y + 40f, r.width, 22f), CardInfo(st), _mid);

            if (!st.UnlimitedMana)
            {
                GUI.color = new Color(0.24f, 0.36f, 0.58f, 0.95f);
                GUI.Label(new Rect(r.x, r.y + 62f, r.width, 20f), Loc.T("select.manaLimited"), _small);
            }

            DrawStars(new Rect(r.x, r.y + 62f, r.width, 28f), stars, 3);

            // 라벨을 다 그린 뒤 투명 버튼을 덮는다
            GUI.color = Color.white;
            if (GUI.Button(r, GUIContent.none, GUIStyle.none))
            {
                AudioManager.Play(Sfx.UiClick, 0.5f, 1.35f);
                _confirm = st;
            }
        }

        // ── 시작 확인 ───────────────────────────────────────────

        /// <summary>
        /// 들어가기 전에 한 번 묻는 창. null이면 안 떠 있다.
        ///
        /// **예전엔 누르는 즉시 전투가 시작됐다.** 목록을 훑다가 손이 스치면
        /// 그대로 들어가 버리고, 무엇을 상대할지 모른 채 첫 물결을 맞는다.
        ///
        /// 이 창은 확인만 받는 게 아니라 **판을 미리 보여준다.** 물결이 몇 개인지,
        /// 무엇이 나오는지, 마나가 얼마인지. 어떤 룬을 쓸지는 그걸 보고 정하는
        /// 것이므로, 들어가기 전에 알아야 할 정보다.
        /// </summary>
        private StageDef _confirm;

        private void DrawConfirm()
        {
            if (_confirm == null) return;

            DrawScrim(0.72f);

            const float w = 600f, h = 356f;
            var box = new Rect((UiScale.W - w) * 0.5f, (UiScale.H - h) * 0.5f, w, h);
            UiSkin.DrawPanel(box, new Color(0.9f, 0.92f, 0.96f));

            GUI.color = new Color(0.16f, 0.15f, 0.13f);
            GUI.Label(new Rect(box.x, box.y + 18f, w, 30f),
                _confirm.Id + ".  " + _confirm.Name, _big);
            GUI.color = Color.white;

            DrawStars(new Rect(box.x, box.y + 54f, w, 30f),
                StageProgress.StarsOf(_confirm.Id), 3);

            // 무엇을 상대하는지. **이게 이 창의 진짜 목적이다.**
            GUI.color = new Color(0.34f, 0.32f, 0.3f);
            GUI.Label(new Rect(box.x + 28f, box.y + 96f, w - 56f, 22f),
                Loc.F("confirm.waves", _confirm.Waves.Length), _mid);
            GUI.Label(new Rect(box.x + 28f, box.y + 122f, w - 56f, 22f),
                EnemyLine(_confirm), _small);
            GUI.Label(new Rect(box.x + 28f, box.y + 152f, w - 56f, 22f),
                _confirm.UnlimitedMana
                    ? Loc.T("confirm.manaFree")
                    : Loc.F("confirm.mana", (int)_confirm.MaxMana, (int)_confirm.ManaRegen),
                _small);
            GUI.color = Color.white;

            const float bw = 210f, bh = 56f, gap = 18f;
            float bx = box.x + (w - bw * 2f - gap) * 0.5f;
            float by = box.y + h - bh - 26f;

            if (Button(new Rect(bx, by, bw, bh), Loc.T("common.back"))) _confirm = null;
            if (Button(new Rect(bx + bw + gap, by, bw, bh), Loc.T("confirm.start"), true))
            {
                StageDef go = _confirm;
                _confirm = null;
                AudioManager.Play(Sfx.DrawBegin, 0.5f);

                // 판을 마치고 목록으로 돌아오면 **그 판이 있던 장**이 펼쳐져 있어야
                // 한다. "다음 도전"은 장을 건너뛸 수 있어서, 시작하는 쪽에서 맞춰
                // 두지 않으면 엉뚱한 장으로 돌아온다.
                _openChapter = go.Chapter;

                flow.StartStage(go);
            }
        }

        /// <summary>이 판에 나오는 적 종류를 한 줄로. 수는 안 센다 — 무엇이 나오냐가 중요하다.</summary>
        private static string EnemyLine(StageDef st)
        {
            var seen = new System.Collections.Generic.List<UnitKind>();
            for (int w = 0; w < st.Waves.Length; w++)
                for (int g = 0; g < st.Waves[w].Groups.Length; g++)
                {
                    UnitKind k = st.Waves[w].Groups[g].Kind;
                    if (!seen.Contains(k)) seen.Add(k);
                }

            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < seen.Count; i++)
            {
                if (i > 0) sb.Append("  ·  ");
                sb.Append(UnitVisuals.DisplayName(seen[i]));
            }
            return sb.ToString();
        }

        // ── 결과 ────────────────────────────────────────────────

        private int _starsHeard;

        private void DrawResult()
        {
            DrawScrim(0.78f);

            float age = Time.unscaledTime - _resultTime;
            bool cleared = flow.LastCleared;

            GUI.color = cleared ? new Color(0.7f, 1f, 0.6f) : new Color(1f, 0.55f, 0.5f);
            GUI.Label(new Rect(0f, UiScale.H * 0.5f - 190f, UiScale.W, 60f),
                Loc.T(cleared ? "result.clear" : "result.failed"), _title);

            GUI.color = new Color(1f, 1f, 1f, 0.8f);
            GUI.Label(new Rect(0f, UiScale.H * 0.5f - 132f, UiScale.W, 26f),
                flow.CurrentStage != null ? flow.CurrentStage.Id + ". " + flow.CurrentStage.Name : "", _mid);

            if (cleared)
            {
                // 별을 하나씩 늦게 띄운다. 한 번에 다 뜨면 3별인지 2별인지 눈에 안 들어온다.
                int shown = Mathf.Clamp(Mathf.FloorToInt(age / 0.35f), 0, flow.LastStars);

                // 뜰 때마다 소리. **음을 하나씩 올린다** — 같은 음이 세 번 나면
                // 몇 개가 떴는지 귀로는 안 세어지고, 올라가면 세 번째가 절정이 된다.
                if (shown > _starsHeard)
                {
                    _starsHeard = shown;
                    AudioManager.Play(Sfx.StarGain, 0.8f, 1f + 0.18f * (shown - 1));
                    CameraShake.Shake(shown >= 3 ? 0.14f : 0.06f);

                    // 3별 완성 순간에만 폭죽. 별마다 터뜨리면 세 번째가 절정이라는
                    // 음계(위)와 어긋난다 — 소리가 올라가다 마지막에 화면이 응답한다.
                    if (shown >= 3)
                    {
                        ParticleFx.SpawnAtGui(Pfx.FireworkA,
                            new Vector2(UiScale.W * 0.3f, UiScale.H * 0.28f), 1.1f);
                        ParticleFx.SpawnAtGui(Pfx.FireworkB,
                            new Vector2(UiScale.W * 0.7f, UiScale.H * 0.30f), 1.1f);
                        ParticleFx.SpawnAtGui(Pfx.FireworkBig,
                            new Vector2(UiScale.W * 0.5f, UiScale.H * 0.22f), 0.9f);
                    }
                }

                // 방금 뜬 별은 살짝 커졌다 돌아온다. 셋이 나란히 있으면 어느 게
                // 방금 붙은 건지 안 보인다.
                float last = age - (shown - 1) * 0.35f;
                float pop = shown > 0 && last < 0.28f ? 1f - last / 0.28f : 0f;
                DrawStars(new Rect(0f, UiScale.H * 0.5f - 96f, UiScale.W, 44f), shown, 3,
                    UiSkin.Text.Star, pop);

                GUI.color = new Color(1f, 1f, 1f, 0.7f);
                GUI.Label(new Rect(0f, UiScale.H * 0.5f - 44f, UiScale.W, 24f),
                    Loc.F("result.survived", flow.LastHeroesAlive, flow.LastHeroesTotal), _mid);

                if (age > 1.0f)
                {
                    GUI.color = GoldOnDark;
                    GUI.Label(new Rect(0f, UiScale.H * 0.5f - 18f, UiScale.W, 24f),
                        flow.LastWasNewRecord ? Loc.F("result.newRecord", flow.LastShards)
                                              : Loc.F("result.shards", flow.LastShards), _mid);
                }
            }
            else
            {
                GUI.color = new Color(1f, 1f, 1f, 0.6f);
                GUI.Label(new Rect(0f, UiScale.H * 0.5f - 70f, UiScale.W, 24f),
                    Loc.T("result.wiped"), _mid);
            }

            float by = UiScale.H * 0.5f + 30f;
            const float bw = 200f, bh = 60f, bgap = 14f;

            bool showNext = cleared && flow.HasNextStage;
            int count = showNext ? 3 : 2;
            float total = count * bw + (count - 1) * bgap;
            float bx = (UiScale.W - total) * 0.5f;

            if (Button(new Rect(bx, by, bw, bh), Loc.T("result.retry"))) flow.RetryStage();
            bx += bw + bgap;

            if (showNext)
            {
                if (Button(new Rect(bx, by, bw, bh), Loc.T("result.next"), true)) flow.NextStage();
                bx += bw + bgap;
            }

            if (Button(new Rect(bx, by, bw, bh), Loc.T("result.toSelect"))) flow.GoToStageSelect();
        }

        // ── 공용 ────────────────────────────────────────────────

        /// <summary>
        /// 글씨 뒤 대비용 얇은 막.
        ///
        /// 예전엔 이걸로 전투 화면을 가렸다(0.62~0.78). 타이틀·스테이지 선택은 이제
        /// 배경 자체가 갈리므로 가릴 것이 없다 — **남긴 건 배경의 떠다니는 알갱이와
        /// 글씨가 겹칠 때의 대비를 세우는 몫뿐**이라 아주 옅게 깐다(0.2 안팎).
        ///
        /// **결과 화면만 진하다.** 거기는 뒤에 방금 싸운 전장이 그대로 있어서
        /// 제대로 눌러 줘야 글씨가 읽힌다.
        /// </summary>
        private static void DrawScrim(float alpha)
        {
            // 배경이 밝아지면서 두 갈래가 됐다: 메뉴(옅게)는 **밝은 막** —
            // 어두운 잉크 글씨의 대비를 세우는 쪽이고, 결과·확인창(진하게)은
            // 여전히 **어두운 막** — 뒤(전장·목록)를 눌러야 하는 모달이다.
            // 0.5를 경계로 갈랐다. 호출부에 bool을 들리는 것보다 실수할 곳이 적다.
            // 옅은 쪽은 더 옅게 깐다. 크림 막이 진하면 풀색이 씻겨서
            // "탁한 초원"이 된다 — 글씨는 대부분 판 위에 있으니 막은 거들 뿐이다.
            if (alpha < 0.5f)
                GUI.color = new Color(0.97f, 0.97f, 0.9f, alpha * 0.45f);
            else
                GUI.color = new Color(0.02f, 0.03f, 0.05f, alpha);

            GUI.DrawTexture(new Rect(0f, 0f, UiScale.W, UiScale.H), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        /// <summary>
        /// 별 줄. <paramref name="pop"/>이 0보다 크면 **방금 붙은 별만** 커진다.
        ///
        /// **간격은 고정이다.** 예전에는 커지는 크기로 간격까지 계산해서, 별
        /// 하나가 붙을 때마다 셋이 통째로 옆으로 벌어졌다. 눈이 잡는 건
        /// "하나가 방금 붙었다"인데 화면은 "줄 전체가 움직인다"를 말하고 있었다.
        /// </summary>
        private void DrawStars(Rect area, int filled, int total, int size = UiSkin.Text.StarSmall,
            float pop = 0f)
        {
            float w = size * 1.25f;
            float x0 = area.x + (area.width - total * w) * 0.5f;

            for (int i = 0; i < total; i++)
            {
                // 방금 붙은 별만 큰 글꼴로. 나머지는 기본 크기 그대로다.
                bool popping = pop > 0.5f && i == filled - 1;
                UiSkin.SetTextSize(_starSized, popping ? UiSkin.Text.StarBig : size);

                GUI.color = i < filled ? Gold : Dim;
                GUI.Label(new Rect(x0 + i * w, area.y, w, area.height),
                    i < filled ? "★" : "☆", _starSized);
            }
            GUI.color = Color.white;
        }

        /// <summary>타이틀 전용 버튼. 글씨가 한 단 크다는 것만 다르다.</summary>
        private bool TitleButton(Rect r, string label, bool highlight = false)
        {
            if (UiSkin.DrawButton(r, label, _btnBig, highlight,
                    true, highlight ? new Color(1f, 0.93f, 0.72f) : Color.white))
            {
                AudioManager.Play(Sfx.DrawBegin, 0.5f);
                return true;
            }
            return false;
        }

        private bool Button(Rect r, string label, bool highlight = false)
        {
            if (UiSkin.DrawButton(r, label, _mid, highlight,
                    true, highlight ? new Color(1f, 0.93f, 0.72f) : Color.white))
            {
                AudioManager.Play(Sfx.DrawBegin, 0.5f);
                return true;
            }
            return false;
        }

    }
}
