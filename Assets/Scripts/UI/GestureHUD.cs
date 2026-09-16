using System.Collections.Generic;
using UnityEngine;
using RuneCast.Battle;
using RuneCast.Core;
using RuneCast.Gesture;
using RuneCast.Meta;
using RuneCast.Runes;

namespace RuneCast.UI
{
    /// <summary>
    /// 튜닝용 오버레이 + 최소 게임 UI.
    ///
    /// IMGUI(OnGUI)를 쓴 이유: Canvas 계층을 만들 필요가 없어 씬 설정이 0이다.
    /// 인식 임계값 조정은 이 프로젝트에서 반복 횟수가 가장 많은 작업이라,
    /// 화면에 숫자가 바로 뜨는 게 Console 로그보다 훨씬 빠르다.
    /// 최종 UI는 나중에 uGUI로 다시 만들 것.
    /// </summary>
    public class GestureHUD : MonoBehaviour
    {
        public RuneCaster caster;
        public ManaPool mana;
        public BattleManager battle;
        public TraceCapture capture;
        public RuneRegistrar registrar;
        public RuneCast.Meta.GameFlow flow;

        [Tooltip("템플릿별 거리 표를 띄운다. 튜닝 끝나면 끄면 된다.")]
        public bool showDebugPanel = true;

        private RecognitionResult _last;
        private bool _lastSuccess;
        private CastFail _lastFail;
        private float _lastTime = -99f;

        private GUIStyle _big, _mid, _small;
        private GUIStyle _bigCenter, _midCenter, _smallCenter, _bannerStyle, _gradeStyle, _subCenter;
        private GUIStyle _titleCenter;

        private float _bannerTime = -99f;
        private string _bannerText;

        private void Start()
        {
            if (caster != null) caster.Cast += OnCast;
            if (battle != null)
            {
                battle.WaveStarted += OnWaveStarted;
                battle.StageStarted += OnStageStarted;
                battle.WaveCleared += OnWaveCleared;
            }
        }

        /// <summary>마지막 물결인가. 배너 색과 글이 달라진다.</summary>
        private bool _bannerFinal;

        /// <summary>판 이름을 띄우는 중인가. 물결 배너보다 크고 오래 머문다.</summary>
        private bool _bannerStage;

        /// <summary>
        /// 판이 시작될 때 이름을 띄운다.
        ///
        /// **어디에 들어왔는지가 화면에 있어야 한다.** 스테이지 선택에서 이름을
        /// 보고 눌렀어도, 전투 화면으로 넘어오면 그 이름은 사라진다. 판마다
        /// 성격이 다른데(물량이냐 정예냐 망령이냐) 무엇을 고른 건지 잊은 채로
        /// 시작하게 된다.
        /// </summary>
        private void OnStageStarted(RuneCast.Meta.StageDef stage)
        {
            if (stage == null) return;

            _bannerStage = true;
            _bannerFinal = false;
            _bannerText = stage.Id + ".  " + stage.Name;
            _bannerTime = Time.unscaledTime;

            // **장 규칙을 판 이름과 같이 알린다.**
            //
            // 규칙은 장에 걸리므로 여섯 판 내내 같다. 그래도 매 판 띄우는 이유는,
            // 며칠 만에 이어서 하면 무엇이 걸려 있었는지 기억이 안 나기 때문이다.
            // 이름만 뜨면 겪고 나서야 알게 되고, 그때는 이미 아군이 하나 죽어 있다 —
            // 그래서 한 줄 설명까지 같이 띄운다.
            _ruleName = Loc.T(ChapterRules.NameKey);
            _ruleDesc = ChapterRules.Active == ChapterRule.Sealing
                ? Loc.F(ChapterRules.DescKey, GlyphTable.RuneName(ChapterRules.SealedRune))
                : Loc.T(ChapterRules.DescKey);
        }

        /// <summary>이번 판에 걸린 장 규칙. 판 이름 배너 아래 같이 뜬다.</summary>
        private string _ruleName, _ruleDesc;

        private void OnWaveStarted(int wave)
        {
            // **마지막 물결이 다른 물결과 똑같이 떴다.** 소리만 반음 낮았는데,
            // 그리는 중에는 화면 위쪽을 안 보고 있어서 그 차이를 못 잡는다.
            // 여기가 마지막이라는 걸 알아야 마나를 아낄지 쏟을지 정할 수 있다.
            _bannerStage = false;
            _bannerFinal = battle != null && wave >= battle.WaveCount;

            // 적이 화면 밖에서 걸어 들어오는 구조라, 알려주지 않으면
            // 방금 웨이브가 넘어간 건지 아직 남은 건지 알 수가 없다.
            _bannerText = _bannerFinal
                ? Loc.T("hud.waveFinal")
                : Loc.F("hud.wave", wave, battle.WaveCount);
            _bannerTime = Time.unscaledTime;

            // 배너만 뜨고 소리가 없었다. 그리던 중이면 화면 위쪽을 안 보고 있어서
            // 새 무리가 들어온 걸 놓친다 — 마지막 물결인지 아닌지가 판단의 근거인데.
            //
            // **클리어 소리를 쓰면 안 된다.** 물결을 정리한 직후 2초 뒤에 다음 물결이
            // 시작되므로 같은 소리가 연달아 나고, 그러면 둘 다 아무 뜻이 없어진다.
            // 낮게 깐 착탄음이 "뭔가 다가온다"로 읽힌다. 마지막 물결은 더 낮게.
            AudioManager.Play(Sfx.HitMeteor, 0.55f, wave >= battle.WaveCount ? 0.62f : 0.78f);
        }

        private void OnWaveCleared(int wave)
        {
            if (battle == null || wave >= battle.WaveCount) return; // 마지막은 결과 화면이 알린다
            _bannerText = Loc.T("hud.waveClear");
            _bannerTime = Time.unscaledTime;
            _bannerFinal = false;
            _bannerStage = false;
        }

        /// <summary>
        /// 첫 물결까지의 준비 시간.
        ///
        /// 숫자 하나를 크게 띄운다. **얼마나 남았는지가 보여야 그 시간을 쓸 수 있다** —
        /// 어떤 룬을 먼저 쓸지 정하거나, 손을 화면에 올려 두거나.
        /// 모르고 기다리면 아무리 짧아도 답답하다.
        /// </summary>
        private void DrawCountdown()
        {
            if (battle == null) return;

            float left = battle.StartCountdown;
            if (left <= 0f) return;

            // **마지막 3초에만 뜬다.** 물결 간격을 3.4초로 늘리면서, 물결을 정리한
            // 직후에 WAVE CLEAR 배너와 카운트다운이 동시에 뜨게 됐다. 방금 끝난
            // 것과 곧 올 것을 한꺼번에 알리면 둘 다 안 읽힌다.
            // 첫 물결 전(startDelay 3초)에는 처음부터 끝까지 보인다.
            if (left > 3f) return;

            // 초 단위로 끊어 보여준다. 소수점이 흐르면 눈이 그걸 읽느라 바쁘다.
            int n = Mathf.CeilToInt(left);

            // 숫자가 바뀌는 순간마다 커졌다 돌아온다. 정지한 숫자는 줄고 있는지
            // 안 줄고 있는지 구분이 안 된다.
            float frac = left - Mathf.Floor(left);
            float pop = frac > 0.75f ? (frac - 0.75f) / 0.25f : 0f;

            float y = UiScale.H * 0.42f;

            // **글씨를 한 단 키우고 알파를 흔들지 않는다.**
            // 예전엔 17px에 알파가 0.35~0.85로 계속 오르내렸다. 밝은 풀밭 위에
            // 옅은 크림색 작은 글씨라 배경에 묻혔다 — 깜빡이는 것처럼 보이는 게
            // 아니라 그냥 안 보였다. 맥동은 숫자에만 남긴다(줄고 있다는 신호).
            var label = new Rect(0f, y - 44f, UiScale.W, 38f);
            Shadowed(label, Loc.T("hud.getReady"), _titleCenter,
                new Color(1f, 0.95f, 0.78f, 1f));

            // 숫자는 로고 크기(64). 사다리 밖이지만 FontWarmup이 숫자까지
            // 미리 구워 둔다 — UiSkin.Text.Logo 설명 참고.
            UiSkin.SetTextSize(_bigCenter, UiSkin.Text.Logo);
            Shadowed(new Rect(0f, y, UiScale.W, 76f), n.ToString(), _bigCenter,
                new Color(1f, 0.95f, 0.75f, 0.7f + 0.3f * pop));

            GUI.color = Color.white;
        }

        /// <summary>
        /// 어두운 그림자를 깔고 글씨를 얹는다.
        ///
        /// 전장 위에 뜨는 글씨는 배경을 고를 수가 없다 — 밝은 풀밭일 수도 있고
        /// 폭발 한복판일 수도 있다. 상자를 깔면 전장을 가리므로, 한 픽셀 밀어
        /// 검게 한 번 더 그리는 쪽이 싸고 덜 가린다.
        /// </summary>
        private static void Shadowed(Rect r, string text, GUIStyle style, Color color)
        {
            GUI.color = new Color(0f, 0f, 0f, 0.55f * color.a);
            GUI.Label(new Rect(r.x + 2f, r.y + 2f, r.width, r.height), text, style);

            GUI.color = color;
            GUI.Label(r, text, style);
        }

        /// <summary>웨이브 전환 배너. 화면 위쪽에 잠깐 떴다 사라진다.</summary>
        private void DrawBanner()
        {
            // 판 이름은 더 오래 머문다. 첫 물결이 오기 전에 읽을 시간이 있어야 한다.
            float life = _bannerStage ? 2.4f : 1.8f;
            float fadeAt = life - 0.4f;

            float age = Time.unscaledTime - _bannerTime;
            if (age > life || string.IsNullOrEmpty(_bannerText)) return;

            // 떴다가 잠깐 머물고 사라진다
            float alpha = age < 0.2f ? age / 0.2f : (age > fadeAt ? (life - age) / 0.4f : 1f);

            float y = UiScale.H * 0.22f;

            // **띠가 가운데에서 좌우로 열린다.** 그냥 나타났다 사라지기만 하면
            // 언제 뜬 건지 눈이 못 잡는다 — 그리는 중에는 시선이 자기 획에 있고,
            // 화면 위쪽의 변화는 움직임이 있어야 곁눈에 걸린다.
            float open = Mathf.Clamp01(age / 0.22f);
            open = 1f - (1f - open) * (1f - open);   // 빠르게 열리고 부드럽게 멎는다
            float bw = UiScale.W * open;

            // 마지막 물결은 붉게. 색이 먼저 읽히고 글자는 그 다음이다.
            Color bar = _bannerFinal ? new Color(0.30f, 0.05f, 0.06f) : new Color(0f, 0f, 0f);
            Color ink = _bannerFinal ? new Color(1f, 0.72f, 0.55f) : new Color(1f, 0.95f, 0.75f);

            GUI.color = new Color(bar.r, bar.g, bar.b, 0.55f * alpha);
            GUI.DrawTexture(new Rect((UiScale.W - bw) * 0.5f, y - 6f, bw, 56f), Texture2D.whiteTexture);

            // 위아래 실선. 띠가 배경 위에 그냥 얹힌 게 아니라 하나의 판으로 읽힌다.
            GUI.color = new Color(ink.r, ink.g, ink.b, 0.55f * alpha);
            GUI.DrawTexture(new Rect((UiScale.W - bw) * 0.5f, y - 6f, bw, 1f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect((UiScale.W - bw) * 0.5f, y + 49f, bw, 1f), Texture2D.whiteTexture);

            // 글자는 띠가 어느 정도 열린 뒤에 얹는다. 같이 나오면 글자가
            // 잘려 나온 것처럼 보인다.
            if (open < 0.6f) return;

            GUI.color = new Color(ink.r, ink.g, ink.b, alpha);
            UiSkin.SetTextSize(_bigCenter, UiSkin.Text.Title);
            GUI.Label(new Rect(0f, y, UiScale.W, 46f), _bannerText,
                _bannerStage ? _bigCenter : _bannerStyle);

            // 판 이름 배너에만 장 규칙을 덧붙인다. 물결 배너(WAVE 2/3)에까지
            // 붙이면 판 도중에 규칙 설명이 계속 튀어나온다.
            if (_bannerStage && !string.IsNullOrEmpty(_ruleName))
            {
                GUI.color = new Color(1f, 0.82f, 0.42f, alpha);
                GUI.Label(new Rect(0f, y + 46f, UiScale.W, 26f), "— " + _ruleName + " —", _midCenter);

                GUI.color = new Color(ink.r, ink.g, ink.b, 0.78f * alpha);
                GUI.Label(new Rect(0f, y + 70f, UiScale.W, 24f), _ruleDesc, _smallCenter);
            }

            GUI.color = Color.white;
        }

        private void OnCast(RecognitionResult r, bool success, CastFail fail)
        {
            _last = r;
            _lastSuccess = success;
            _lastFail = fail;
            _lastTime = Time.unscaledTime;

            // 마나가 모자라 실패했으면 마나 게이지가 반응한다.
            // 화면 가운데 글자만으로는 "왜 안 나갔는지"가 손끝에서 안 읽힌다 —
            // 그리던 시선은 자기 획에 있지 화면 중앙에 있지 않다.
            if (fail == CastFail.Mana) _manaFlash = Time.unscaledTime;
        }

        private float _manaFlash = -99f;

        private void EnsureStyles()
        {
            if (_big != null) return;
            _big = new GUIStyle(GUI.skin.label) { fontSize = UiSkin.Text.Title };
            _mid = new GUIStyle(GUI.skin.label) { fontSize = UiSkin.Text.Mid };
            _small = new GUIStyle(GUI.skin.label) { fontSize = UiSkin.Text.Small };

            // 가운데 정렬본은 매 프레임 새로 만들지 않는다. OnGUI에서 GUIStyle을 new 하면
            // 프레임마다 쓰레기가 쌓여서, 화면에 아무 일이 없어도 GC가 계속 돈다.
            _bigCenter = new GUIStyle(_big) { alignment = TextAnchor.MiddleCenter };
            _midCenter = new GUIStyle(_mid) { alignment = TextAnchor.MiddleCenter };
            _smallCenter = new GUIStyle(_small) { alignment = TextAnchor.MiddleCenter };
            _bannerStyle = new GUIStyle(GUI.skin.label)
                { fontSize = UiSkin.Text.Title, alignment = TextAnchor.MiddleCenter };
            _gradeStyle = new GUIStyle(_bigCenter);
            _subCenter = new GUIStyle(_midCenter) { fontSize = UiSkin.Text.Mid };

            // "잠시 후 시작합니다" 전용. _bigCenter는 카운트다운 숫자가 크기를
            // 바꿔 가며 쓰므로(SetTextSize) 같이 쓰면 서로의 크기를 덮어쓴다.
            _titleCenter = new GUIStyle(GUI.skin.label)
                { fontSize = UiSkin.Text.Title, alignment = TextAnchor.MiddleCenter };

            UiSkin.ApplyFont(_big, _mid, _small, _bigCenter, _midCenter, _smallCenter,
                _bannerStyle, _gradeStyle, _subCenter, _titleCenter);
        }

        private void OnGUI()
        {
            // OnGUI는 한 프레임에 여러 번 불린다(Layout, Repaint, 입력 이벤트마다).
            // 이 HUD에는 버튼이 하나도 없고 라벨과 텍스처뿐이라 Repaint에서만 그리면 된다.
            // 그냥 두면 문자열 생성과 레이아웃 계산이 프레임마다 두 번씩 돌았다.
            if (Event.current.type != EventType.Repaint) return;

            // 모든 UI는 가상 해상도 위에서 그린다. 실제 픽셀로 짜면
            // 고해상도 화면에서 글자가 물리적으로 못 읽을 만큼 작아진다.
            UiScale.Begin();
            DrawAll();
            UiScale.End();
        }

        private void DrawAll()
        {
            EnsureStyles();

            // 전투 HUD는 전투 중에만. 각인 모드와 진단 패널은 어디서나 열 수 있어야
            // 스테이지 선택 화면에서도 도형을 등록·확인할 수 있다.
            if (flow == null || flow.State == GameState.Battle)
            {
                DrawManaBar();
                DrawStageBar();
                DrawBanner();
                DrawCountdown();
                DrawCastFeedback();
                if (showDebugPanel) DrawDebugPanel();
            }

            if (registrar != null && registrar.Active) DrawRegistration();
            if (_showSeparation) DrawSeparation();
        }

        private bool _showSeparation;

        private void Update()
        {
            if (Hotkeys.Down(Hotkey.F1)) _showSeparation = !_showSeparation;

            // 눈금이 켜지는 순간은 여기서 잡는다. OnGUI는 Repaint에서만 도는데
            // 그 사이에 문턱을 넘었다 말았다 하면 빛나는 걸 놓친다.
            TrackCostThresholds();
        }

        /// <summary>
        /// 각인 모드 오버레이. 남은 샘플 수를 계속 보여줘야 흐름이 끊기지 않는다.
        ///
        /// **화면 버튼이 주 조작 수단이다.** 예전엔 숫자키로만 대상을 고를 수 있어서
        /// 폰에서는 이 기능 자체에 손이 닿지 않았다. 키보드는 있으면 쓰는 지름길이다.
        /// </summary>
        private void DrawRegistration()
        {
            GUI.color = new Color(0.05f, 0.03f, 0.1f, 0.72f);
            GUI.DrawTexture(new Rect(0f, 0f, UiScale.W, UiScale.H), Texture2D.whiteTexture);

            GUI.color = new Color(1f, 0.85f, 0.45f);
            GUI.Label(new Rect(0f, UiScale.T + 24f, UiScale.W, 44f), Loc.T("inscribe.title"), _bigCenter);

            GUI.color = Color.white;
            GUI.Label(new Rect(0f, UiScale.T + 70f, UiScale.W, 26f),
                Loc.F("inscribe.target", GlyphTable.RuneName(registrar.Target),
                    registrar.Collected, RuneRegistrar.SamplesNeeded), _midCenter);

            GUI.color = new Color(1f, 1f, 1f, 0.6f);
            GUI.Label(new Rect(0f, UiScale.T + 98f, UiScale.W, 24f), Loc.T("inscribe.hint"), _midCenter);

            // 세 번을 다 그려야 한 장이 나간다. 그리는 중에 남은 장수가 보여야
            // "이번 것을 제대로 그려야 한다"가 읽힌다.
            GUI.color = new Color(0.72f, 0.92f, 1f, 0.9f);
            GUI.Label(new Rect(0f, UiScale.T + 122f, UiScale.W, 22f),
                Loc.F("inscribe.tickets", RuneCast.Meta.PlayerData.InscribeTickets), _smallCenter);
            GUI.color = Color.white;

            // **거절당했으면 이유를 말해 준다.** 세 번을 다 그렸는데 아무 일도
            // 안 일어나면 못 알아들은 줄 알고 계속 다시 그린다.
            if (registrar.LastCommit == RuneRegistrar.CommitResult.TooSimilar)
            {
                GUI.color = new Color(1f, 0.62f, 0.5f);
                GUI.Label(new Rect(0f, UiScale.T + 146f, UiScale.W, 22f),
                    Loc.F("inscribe.tooSimilar", GlyphTable.RuneName(registrar.Collided)), _midCenter);
                GUI.color = Color.white;
            }
            else if (registrar.LastCommit == RuneRegistrar.CommitResult.NoTicket)
            {
                GUI.color = new Color(1f, 0.62f, 0.5f);
                GUI.Label(new Rect(0f, UiScale.T + 146f, UiScale.W, 22f),
                    Loc.T("inscribe.noTicket"), _midCenter);
                GUI.color = Color.white;
            }
            GUI.color = Color.white;

            // 대상 룬 고르기 — 도형 글자를 같이 띄운다. 이름만 있으면 "무슨 도형을
            // 그려야 하는지"가 안 보이는데, 지금 하려는 일이 바로 그것이다.
            const float bw = 100f, bh = 40f, gap = 8f;
            int cols = Mathf.Min(5, Legend.Length);
            int rows = (Legend.Length + cols - 1) / cols;
            float gridW = cols * bw + (cols - 1) * gap;
            float gx = (UiScale.W - gridW) * 0.5f;
            float gy = UiScale.T + 132f;

            for (int i = 0; i < Legend.Length; i++)
            {
                RuneType t = Legend[i];
                var rr = new Rect(gx + (i % cols) * (bw + gap),
                                  gy + (i / cols) * (bh + gap), bw, bh);

                if (UiSkin.DrawButton(rr, InscribeLabel(i, t),
                        _smallCenter, registrar.Target == t, true, Color.white))
                    registrar.SelectTarget(t);
            }

            float by = gy + rows * (bh + gap) + 12f;

            // 삭제는 되돌릴 수 없어 두 번 눌러야 한다. 나가기 바로 옆이라 색으로도 갈라 놓는다.
            const float aw = 190f;

            if (UiSkin.DrawButton(new Rect(UiScale.W * 0.5f - aw - 8f, by, aw, 38f),
                    _wipeArmed ? Loc.T("inscribe.wipeWarn") : Loc.T("inscribe.wipe"),
                    _smallCenter, _wipeArmed, true, new Color(1f, 0.62f, 0.55f)))
            {
                if (_wipeArmed) { registrar.DeleteAll(); _wipeArmed = false; }
                else _wipeArmed = true;
            }

            if (UiSkin.DrawButton(new Rect(UiScale.W * 0.5f + 8f, by, aw, 38f),
                    Loc.T("inscribe.exit"), _smallCenter, true, true, Color.white))
            {
                _wipeArmed = false;
                registrar.Toggle();
            }

            GUI.color = new Color(1f, 1f, 1f, 0.35f);
            GUI.Label(new Rect(0f, UiScale.B - 40f, UiScale.W, 20f),
                Loc.F("inscribe.path", CustomRuneStore.FilePath), _smallCenter);
            GUI.color = Color.white;
        }

        private bool _wipeArmed;

        // 각인 화면의 룬 버튼 글자. 언어가 바뀔 때 말고는 안 변한다.
        private string[] _inscribeLabel;
        private Lang _inscribeLang = (Lang)(-1);

        private string InscribeLabel(int i, RuneType t)
        {
            if (_inscribeLabel == null || _inscribeLang != GameSettings.Language)
            {
                _inscribeLang = GameSettings.Language;
                _inscribeLabel = new string[Legend.Length];
                for (int k = 0; k < Legend.Length; k++)
                    _inscribeLabel[k] = GlyphTable.Glyph(Legend[k]) + " " + GlyphTable.RuneName(Legend[k]);
            }
            return _inscribeLabel[i];
        }

        private void DrawManaBar()
        {
            const float w = 320f, h = 20f;
            float x = UiScale.L + 16f;

            // **카드 줄 위로 올린다.** 배치 카드가 화면 아래를 먹으므로 그만큼
            // 비켜야 게이지가 안 가린다. 카드가 없으면(프롤로그 등) 0이라
            // 예전 자리 그대로다 — 클래시 로얄의 엘릭서 바와 같은 배치가 된다.
            float y = UiScale.B - h - 20f - DeployBar.BarHeight;

            // 잉크 — 그리는 중에만 뜬다. 안 그릴 때는 항상 가득이라 볼 필요가 없고,
            // 상시 표시하면 화면만 복잡해진다.
            if (capture != null && capture.IsDrawing)
            {
                float iy = y - h - 8f;
                float left = 1f - capture.InkRatio;

                Color inkColor = capture.InkOut
                    ? new Color(1f, 0.35f, 0.3f)
                    : Color.Lerp(new Color(1f, 0.5f, 0.35f), new Color(0.95f, 0.9f, 0.7f), left);

                // 다 쓴 순간 떨린다. 선이 멈춘 걸 알아채기 전에 손이 먼저 계속 움직인다.
                float ix = capture.InkOut ? x + Mathf.Sin(Time.unscaledTime * 55f) * 4f : x;
                UiSkin.DrawBar(new Rect(ix, iy, w, h), left, inkColor);

                GUI.Label(new Rect(x + w + 10f, iy - 3f, 200f, 24f),
                    Loc.T(capture.InkOut ? "ink.empty" : "ink.label"), _mid);
            }

            if (mana == null) return;

            Color manaColor = mana.unlimited
                ? new Color(0.75f, 0.6f, 1f)
                : (mana.Ratio > 0.25f ? new Color(0.4f, 0.65f, 1f) : new Color(1f, 0.5f, 0.4f));

            // 모자라서 튕긴 직후엔 붉게 번쩍이고 좌우로 떤다
            float mf = Time.unscaledTime - _manaFlash;
            float mx = x;
            float mh = h;
            if (mf < 0.4f)
            {
                float k = 1f - mf / 0.4f;
                manaColor = Color.Lerp(manaColor, new Color(1f, 0.35f, 0.3f), k);
                mx += Mathf.Sin(mf * 70f) * 6f * k;
            }

            // **회수는 반대 방향으로 알린다** (심연, 7장). 실패가 붉게 떠는 것이라면
            // 회수는 밝게 부푼다 — 같은 게이지에서 일어나므로 결이 반대여야
            // 무엇이 일어났는지 안 읽고도 갈린다.
            float gf = Time.unscaledTime - mana.LastGainTime;
            if (gf < 0.35f)
            {
                float k = 1f - gf / 0.35f;
                manaColor = Color.Lerp(manaColor, new Color(0.85f, 0.8f, 1f), k * 0.8f);
                mh = h + 4f * k;
            }

            var manaRect = new Rect(mx, y - (mh - h) * 0.5f, w, mh);
            UiSkin.DrawBar(manaRect, mana.Ratio, manaColor);
            DrawCostMarks(manaRect);

            GUI.Label(new Rect(x + w + 10f, y - 3f, 260f, 24f),
                mana.unlimited ? Loc.T("hud.manaInf") : ManaText(Mathf.RoundToInt(mana.Current)), _mid);
        }

        /// <summary>
        /// 마나 게이지 위에 룬 비용을 눈금으로 새긴다.
        ///
        /// **"지금 뭘 쓸 수 있나"를 숫자를 읽어야 알 수 있었다.** 게이지는 얼마나
        /// 찼는지만 말하고, 별똥별이 45라는 건 외우고 있어야 했다. 그리는 도중에는
        /// 시선이 자기 획에 있으므로 숫자를 읽을 여유가 없다.
        ///
        /// 눈금이 있으면 게이지를 곁눈으로 보는 것만으로 "저 선을 넘었다"가 읽힌다.
        /// 넘긴 눈금은 밝고 못 넘긴 것은 어둡다 — **막 넘어선 눈금은 잠깐 빛난다.**
        /// 쓸 수 있게 된 순간이 게이지에서 일어나야, 마나가 차기를 기다리는 시간이
        /// 그냥 기다림이 아니라 지켜보는 시간이 된다.
        ///
        /// 같은 비용이 겹치면 한 번만 그린다. 룬이 아홉이라 그냥 두면 눈금이
        /// 촘촘해져서 게이지 자체가 안 보인다.
        /// </summary>
        private void DrawCostMarks(Rect bar)
        {
            if (mana == null || mana.unlimited || mana.max <= 0f) return;

            RefreshCosts();

            float cur = mana.Current;

            for (int i = 0; i < _cost.Length; i++)
            {
                int c = _cost[i];
                if (c <= 0 || c > mana.max) continue;

                // 같은 값이 앞에 이미 있었으면 건너뛴다
                bool dup = false;
                for (int k = 0; k < i; k++)
                    if (_cost[k] == c) { dup = true; break; }
                if (dup) continue;

                float t = c / mana.max;
                float mxk = bar.x + bar.width * t;
                bool ready = cur >= c;

                // 막 넘어선 눈금은 잠깐 빛난다. 어느 룬이 열렸는지가 그 순간에 보인다.
                float since = Time.unscaledTime - _costLitAt[i];
                float pop = ready && since < 0.45f ? 1f - since / 0.45f : 0f;

                GUI.color = ready
                    ? Color.Lerp(new Color(1f, 1f, 1f, 0.85f), new Color(1f, 0.95f, 0.6f), pop)
                    : new Color(0f, 0f, 0f, 0.35f);

                float over = pop * 3f;
                GUI.DrawTexture(new Rect(mxk - 1f, bar.y - over, 2f, bar.height + over * 2f),
                    Texture2D.whiteTexture);
                GUI.color = Color.white;
            }
        }

        /// <summary>눈금이 켜진 시각. 켜지는 순간을 알아야 빛나게 할 수 있다.</summary>
        private readonly float[] _costLitAt = new float[Legend.Length];
        private readonly bool[] _costWasReady = new bool[Legend.Length];

        private void TrackCostThresholds()
        {
            if (mana == null || mana.unlimited) return;

            RefreshCosts();

            for (int i = 0; i < _cost.Length; i++)
            {
                bool ready = mana.Current >= _cost[i];
                if (ready && !_costWasReady[i]) _costLitAt[i] = Time.unscaledTime;
                _costWasReady[i] = ready;
            }
        }

        // ── 전투 화면의 문자열 캐시 ────────────────────────────
        //
        // 이 둘은 매 프레임 그려지는데 값은 드물게 바뀐다. 그대로 두면 OnGUI가
        // 프레임당 두세 번 도는 만큼 string.Format이 계속 돌아 쓰레기가 쌓인다.
        // 전투 화면은 이 게임에서 프레임이 제일 중요한 곳이다.

        // 언어도 같이 본다. 값이 그대로인 채로 언어만 바뀌면 낡은 말이 남는다.
        private Lang _textLang = (Lang)(-1);

        private int _manaShown = -1;
        private string _manaText = "";

        private string ManaText(int v)
        {
            if (v != _manaShown || _textLang != GameSettings.Language)
            {
                _textLang = GameSettings.Language;
                _manaShown = v;
                _manaText = Loc.F("hud.mana", v);
            }
            return _manaText;
        }

        // 스테이지 제목도 매 프레임 조립하고 있었다. 판이 바뀔 때만 만들면 된다.
        private int _stTitleId = -1;
        private Lang _stTitleLang = (Lang)(-1);
        private string _stTitleText = "";

        private string StageTitleText()
        {
            if (battle.Stage.Id != _stTitleId || _stTitleLang != GameSettings.Language)
            {
                _stTitleId = battle.Stage.Id;
                _stTitleLang = GameSettings.Language;
                _stTitleText = _stTitleId + ". " + battle.Stage.Name;
            }
            return _stTitleText;
        }

        private int _slWave = -1, _slAlive = -1;
        private Lang _slLang = (Lang)(-1);
        private string _slText = "";

        private string StageLineText()
        {
            if (battle.WaveIndex != _slWave || battle.HeroesAlive != _slAlive
                || _slLang != GameSettings.Language)
            {
                _slWave = battle.WaveIndex;
                _slAlive = battle.HeroesAlive;
                _slLang = GameSettings.Language;
                _slText = Loc.F("hud.stageLine", _slWave, battle.WaveCount,
                    _slAlive, battle.HeroesTotal);
            }
            return _slText;
        }

        private void DrawStageBar()
        {
            float x = UiScale.L + 16f, y = UiScale.T + 14f;
            GUI.color = Color.white;

            if (battle != null && battle.Stage != null)
            {
                GUI.Label(new Rect(x, y, 420f, 40f),
                    StageTitleText(), _big);

                // 남은 웨이브를 보여주는 게 중요하다 — 끝이 있는 판이라
                // "몇 번만 더 버티면 되는가"가 판단의 근거가 된다.
                GUI.color = new Color(1f, 1f, 1f, 0.75f);
                GUI.Label(new Rect(x + 2f, y + 36f, 420f, 22f), StageLineText(), _mid);
                GUI.color = Color.white;
            }

            // 룬이 9종이 되면서 한 줄로 세우면 화면 왼쪽이 통째로 목록이 된다.
            // 2열로 접고, 칸을 고정 폭으로 나눠 그린다 — 서식 문자열의 자릿수 맞춤은
            // 비례 폭 글꼴에서 어차피 안 맞는다.
            RefreshCosts();

            const float rowH = 21f, colW = 208f;

            // 목록 뒤에 얇은 판을 깐다. 초원 위에 맨 글씨를 올리면 밝은 풀 타일과
            // 겹치는 자리에서 글자가 사라진다 — 시전 중에 비용을 확인하는 목록이라
            // 안 보이는 순간이 있으면 안 된다.
            {
                int backRows = (Legend.Length + 1) / 2;
                // 폭: 두 번째 열의 비용 숫자(cx+146, 폭 46)까지 덮어야 한다
                var back = new Rect(x - 8f, y + 56f, colW + 200f + 16f, backRows * rowH + 12f);
                UiSkin.DrawPanel(back, new Color(0.06f, 0.08f, 0.10f, 0.55f));
            }
            int rows = (Legend.Length + 1) / 2;
            float ly = y + 62f;

            for (int i = 0; i < Legend.Length; i++)
            {
                RuneType t = Legend[i];
                float cx = x + (i / rows) * colW;
                float cy = ly + (i % rows) * rowH;

                // **잠긴 룬은 목록에서 먼저 알려준다.**
                //
                // 예전엔 그려 보고 "봉인된 룬"이 뜨는 게 전부였다. 그러면 규칙을
                // 배우는 값이 슬로우로 흘려보낸 시간 + 놓친 한 박자다. 목록은
                // 시전 중에 비용을 확인하는 자리이므로, 여기서 갈리면 그리기 전에 안다.
                bool sealed_ = ChapterRules.IsSealed(t);
                bool affordable = mana == null || mana.unlimited || mana.Current >= _cost[i];

                GUI.color = sealed_ ? new Color(1f, 0.55f, 0.5f, 0.5f)
                    : affordable ? new Color(1f, 1f, 1f, 0.85f)
                                 : new Color(1f, 1f, 1f, 0.35f);

                GUI.Label(new Rect(cx, cy, 22f, 22f), GlyphTable.Glyph(t), _mid);
                GUI.Label(new Rect(cx + 24f, cy, 118f, 22f), GlyphTable.RuneName(t), _mid);

                // 잠긴 룬은 비용 대신 자물쇠. 값이 얼마인지는 이 판에서 뜻이 없다.
                GUI.Label(new Rect(cx + 146f, cy, 46f, 22f),
                    sealed_ ? "×" : _costText[i], _mid);

                // 이름 위에 줄을 긋는다. 색만으로는 "마나가 모자란 것"과 구분이 안 된다.
                if (sealed_ && Event.current.type == EventType.Repaint)
                {
                    GUI.color = new Color(1f, 0.5f, 0.45f, 0.55f);
                    GUI.DrawTexture(new Rect(cx, cy + 11f, 168f, 1f), Texture2D.whiteTexture);
                }
            }

            ly += rows * rowH;

            GUI.color = new Color(1f, 1f, 1f, 0.5f);
            GUI.Label(new Rect(x, ly + 6f, 420f, 20f), Loc.T("hud.hintDraw"), _small);
            ly += 6f;

            // **폰에는 키보드가 없다.** 단축키 안내와 곡 전환(M)을 그대로 띄우면
            // 눌러도 아무 일이 안 일어나는 조작을 알려주는 셈이 된다.
            // 곡 전환은 애초에 어느 곡이 어울리는지 고르려고 만든 개발용 기능이다.
            if (!Application.isMobilePlatform)
            {
                GUI.Label(new Rect(x, ly + 22f, 460f, 20f), Loc.T("hud.hintKeys"), _small);
                ly += 16f;

                if (AudioManager.Instance != null)
                {
                    GUI.Label(new Rect(x, ly + 22f, 460f, 20f),
                        Loc.F("hud.hintMusic", AudioManager.Instance.CurrentBgmName), _small);
                    ly += 16f;
                }
            }

            if (CustomRuneStore.LoadedCount > 0)
                GUI.Label(new Rect(x, ly + 22f, 460f, 20f),
                    Loc.F("hud.customRunes", CustomRuneStore.LoadedCount), _small);
            GUI.color = Color.white;
        }

        // 활성 룬 목록은 GlyphTable 한 곳에서만 관리한다. 예전엔 여기에 따로 적어뒀는데,
        // 룬을 추가할 때마다 두 곳을 고쳐야 해서 문양 뽑기 대상과 HUD 목록이 어긋났다.
        // (가르기는 밸런스 때문에 템플릿이 꺼져 있어 양쪽 모두에서 빠진다.)
        private static readonly RuneType[] Legend = GlyphTable.Runes;

        // 비용 문자열은 매 프레임 만들지 않는다. 값이 바뀌는 건 문양을 바꿔 낄 때뿐이라,
        // 그때만 다시 만들면 int.ToString()이 만드는 쓰레기가 사라진다.
        private readonly int[] _cost = new int[Legend.Length];
        private readonly string[] _costText = new string[Legend.Length];
        private int _costVersion = -1;

        private void RefreshCosts()
        {
            if (_costVersion == Loadout.Version && _costText[0] != null) return;
            _costVersion = Loadout.Version;

            for (int i = 0; i < Legend.Length; i++)
            {
                _cost[i] = ManaPool.EffectiveCost(Legend[i]);
                _costText[i] = _cost[i].ToString();
            }
        }

        private void DrawCastFeedback()
        {
            float age = Time.unscaledTime - _lastTime;
            if (age > 1.6f) return;

            float alpha = Mathf.Clamp01(1.6f - age);
            string text;
            Color color;

            GUIStyle centered = _bigCenter;

            if (_lastSuccess)
            {
                RuneGrade grade = RuneGrading.Of(_last.Score);
                color = GradeVisuals.ColorOf(grade);

                // 등급을 크게, 룬 이름과 점수는 아래에 작게.
                // 플레이어가 알아야 하는 건 "얼마나 잘 그렸나"지 소수점이 아니다.
                float pop = Mathf.Max(0f, 1f - age * 5f); // 뜨는 순간 살짝 커졌다 돌아온다
                // 크기를 4의 배수로 끊는다. 매끄럽게 바꾸면 동적 폰트가 크기마다
                // 글리프를 새로 구워서 아틀라스가 넘친다 (FloatingText 쪽에 자세히 적어 뒀다).
                UiSkin.SetTextSize(_gradeStyle, UiSkin.Text.Snap(38f + 16f * pop));
                GUIStyle big = _gradeStyle;

                GUI.color = new Color(color.r, color.g, color.b, alpha);
                GUI.Label(new Rect(UiScale.W * 0.5f - 220f, UiScale.H * 0.5f - 165f, 440f, 60f),
                    RuneGrading.DisplayName(grade), big);

                GUIStyle sub = _subCenter;
                GUI.color = new Color(color.r, color.g, color.b, alpha * 0.85f);
                GUI.Label(new Rect(UiScale.W * 0.5f - 220f, UiScale.H * 0.5f - 108f, 440f, 26f),
                    string.Format("{0}   ×{1:F2}", GlyphTable.RuneName(_last.Type), PowerOf(_last.Score)), sub);

                GUI.color = Color.white;
                return;
            }

            text = RuneCaster.ReasonText(_lastFail);
            color = new Color(1f, 0.6f, 0.5f);

            GUI.color = new Color(color.r, color.g, color.b, alpha);
            GUI.Label(new Rect(UiScale.W * 0.5f - 200f, UiScale.H * 0.5f - 150f, 400f, 46f), text, centered);
            GUI.color = Color.white;
        }

        /// <summary>RuneCaster와 같은 식으로 배율을 계산해 보여준다.</summary>
        private float PowerOf(float score)
        {
            if (caster == null) return 1f;
            float eased = score * score * (3f - 2f * score);
            return Mathf.Lerp(caster.minPower, caster.maxPower, eased);
        }

        private void DrawDebugPanel()
        {
            if (caster == null || caster.LastDistances == null || caster.LastDistances.Count == 0) return;

            const float w = 250f;
            float x = UiScale.R - w - 14f, y = UiScale.T + 14f;

            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(new Rect(x - 8f, y - 6f, w + 16f, 150f), Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUI.Label(new Rect(x, y, w, 20f), Loc.T("diag.distances"), _small);
            GUI.Label(new Rect(x, y + 18f, w, 20f),
                Loc.F("diag.reject", PointCloudRecognizer.RejectDistance), _small);

            float ly = y + 40f;
            List<KeyValuePair<string, float>> d = caster.LastDistances;
            for (int i = 0; i < Mathf.Min(5, d.Count); i++)
            {
                GUI.color = d[i].Value <= PointCloudRecognizer.RejectDistance
                    ? new Color(0.7f, 1f, 0.75f)
                    : new Color(1f, 1f, 1f, 0.45f);
                GUI.Label(new Rect(x, ly, w, 18f),
                    string.Format("{0,-14} {1:F4}", d[i].Key, d[i].Value), _small);
                ly += 17f;
            }
            GUI.color = Color.white;
        }

        // 전멸/클리어 화면은 MetaUI가 그린다. 결과 처리는 스테이지 흐름의 일부라
        // 전투 HUD가 아니라 메타 쪽에 있는 게 맞다.

        /// <summary>
        /// 룬 충돌 진단. 룬을 추가할 때마다 여기 숫자를 확인해야 한다 —
        /// 비슷한 도형을 넣으면 새 룬이 아니라 기존 룬이 망가진다.
        /// </summary>
        private void DrawSeparation()
        {
            const float w = 430f, h = 210f;
            float x = (UiScale.W - w) * 0.5f, y = 90f;

            GUI.color = new Color(0f, 0f, 0f, 0.82f);
            GUI.DrawTexture(new Rect(x - 12f, y - 10f, w + 24f, h + 20f), Texture2D.whiteTexture);

            GUI.color = new Color(1f, 0.9f, 0.6f);
            GUI.Label(new Rect(x, y, w, 24f), Loc.T("diag.title"), _mid);

            GUI.color = new Color(1f, 1f, 1f, 0.55f);
            GUI.Label(new Rect(x, y + 24f, w, 20f),
                Loc.F("diag.hint", PointCloudRecognizer.RejectDistance), _small);

            // 캐시된 쪽을 쓴다. Analyze는 템플릿 쌍마다 점군 매칭을 도는 무거운 계산이라
            // 매 프레임 부르면 패널을 여는 순간 게임이 멎는다.
            var pairs = TemplateSeparation.AnalyzeCached(RuneTemplateLibrary.Templates);
            float ly = y + 48f;
            for (int i = 0; i < pairs.Count && i < 8; i++)
            {
                var p = pairs[i];

                // 임계값에 근접하면 경고색. 어설프게 그렸을 때 서로 넘어갈 수 있다는 뜻.
                GUI.color = p.MinDistance < PointCloudRecognizer.RejectDistance
                    ? new Color(1f, 0.45f, 0.4f)
                    : (p.MinDistance < PointCloudRecognizer.RejectDistance * 1.5f
                        ? new Color(1f, 0.82f, 0.4f)
                        : new Color(0.65f, 1f, 0.7f));

                GUI.Label(new Rect(x, ly, w, 20f), string.Format("{0:F3}   {1}  ↔  {2}",
                    p.MinDistance, GlyphTable.RuneName(p.A), GlyphTable.RuneName(p.B)), _small);
                ly += 18f;
            }

            GUI.color = Color.white;
        }
    }
}
