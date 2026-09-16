using UnityEngine;
using RuneCast.Battle;
using RuneCast.Core;
using RuneCast.Gesture;
using RuneCast.Meta;
using RuneCast.Runes;

namespace RuneCast.UI
{
    /// <summary>
    /// 프롤로그 — 이 게임의 첫 장면.
    ///
    /// 병사들은 이미 진 싸움에 서 있고, 그 앞에 적이 걸어 들어온다. 절망하는 대사가
    /// 두 번 지나간 뒤 **하늘에 도형이 저절로 그려진다**(RuneTracer). 플레이어가 그걸
    /// 따라 그려 룬이 나가면, 병사들은 그것을 신의 강림으로 받아들이고 환호한다.
    ///
    /// **튜토리얼과 같은 원칙으로 만든다** — 막지 않고, 확인 버튼도 없고,
    /// 플레이어가 실제로 그리면 넘어간다. 다른 점은 하나뿐이다: 튜토리얼은
    /// 규칙을 가르치고 이쪽은 **플레이어가 누구인지**를 알려준다.
    ///
    /// **왜 별도 스테이지인가:** 1판은 "자동으로도 이기는 판"으로 시뮬레이터에
    /// 맞춰져 있다(StageDef 참조). 거기에 절망을 얹으면 오크 세 마리 앞에서
    /// "여기서 죽는구나"가 되어 둘 다 망가진다. 프롤로그는 승패가 없는 연출
    /// 구간이라 밸런스 대상 밖이고, 1판 수치를 한 자리도 안 건드린다.
    ///
    /// **왜 승패로 끝나지 않는가:** 이 판은 플레이어가 이기라고 만든 게 아니다.
    /// 첫 룬이 나가는 순간이 결말이고, 남은 적은 그 자리에서 쓸려나간다.
    /// BattleManager가 클리어·전멸을 판정하면 환호 대사 도중에 결과 화면이
    /// 튀어나오므로, 그쪽에 `IsPrologue` 예외를 뒀다.
    /// </summary>
    public class PrologueUI : MonoBehaviour
    {
        public GameFlow flow;
        public BattleManager battle;
        public RuneCaster caster;

        /// <summary>한 박자. 대사 한 줄과 그게 머무는 시간.</summary>
        private struct Beat
        {
            public string Key;      // null이면 대사 없이 지나가는 침묵
            public string SubKey;   // 두 번째 목소리. 없으면 null
            public float Hold;      // 다음 박자까지(초). 0 이하면 그리기를 기다린다
            public bool Rune;       // 이 박자부터 도형을 그려 보인다

            public Beat(string key, string subKey, float hold, bool rune)
            {
                Key = key; SubKey = subKey; Hold = hold; Rune = rune;
            }
        }

        /// <summary>
        /// 세 번째 박자가 **침묵**인 게 이 장면의 핵심이다.
        /// 도형이 그려지는 동안 대사를 얹으면 아무도 도형을 안 본다 —
        /// 이 게임에서 플레이어가 봐야 할 것은 글이 아니라 그림이다.
        /// </summary>
        private static readonly Beat[] Script =
        {
            new Beat("story.p1", "story.p1.sub", 3.2f, false),
            new Beat("story.p2", null,           2.8f, false),
            new Beat(null,       null,           2.4f, true),   // 침묵 — 도형만
            new Beat("story.p3", null,           0f,   true),   // 여기서 기다린다
        };

        /// <summary>
        /// 프롤로그 동안 카메라를 당기는 정도. 1이 기본, 작을수록 확대.
        ///
        /// **0.75보다 더 당기지 못한다.** 아군 넷이 세로로 ±2.25칸을 쓰는데
        /// 16:9에서 세로 반높이가 4.2×0.75 = 3.15밖에 안 남는다. 더 당기면
        /// 양 끝 병사가 화면 밖으로 나가고, 그러면 "우리 편이 몇인가"가 안 읽힌다.
        /// 적 무리는 ±3.3칸이라 가장자리가 잘리는데 **그건 그대로 둔다** —
        /// 화면 밖까지 이어져 보이는 편이 수가 많아 보인다.
        /// </summary>
        public float zoom = 0.75f;

        [Tooltip("확대가 다 들어가기까지(초). 툭 바뀌면 화면이 튄 것처럼 보인다.")]
        public float zoomTime = 1.4f;

        private float _zoomNow = 1f;

        private RuneTracer _tracer;
        private GUIStyle _line;

        private bool _running;
        private int _beat;
        private float _beatAt;      // 지금 박자가 시작된 unscaled 시각

        private bool _cheering;
        private float _cheerAt;
        private float _wipeAt = -1f;

        /// <summary>인식에 실패했을 때 잠깐 끼워 넣는 한 줄. 지나면 원래 박자로 돌아온다.</summary>
        private float _missUntil = -1f;

        /// <summary>환호가 끝나고 스테이지 선택으로 넘어가기까지.</summary>
        private const float CheerHold = 4.0f;

        /// <summary>화살이 날아가 꽂히는 걸 보고 나서 나머지가 쓸려나간다.</summary>
        private const float WipeDelay = 0.55f;

        private const float MissHold = 2.2f;

        private void Start()
        {
            if (caster != null) caster.Cast += OnCast;
            if (battle != null) battle.WaveStarted += OnWaveStarted;

            var go = new GameObject("PrologueRune");
            go.transform.SetParent(transform, false);
            _tracer = go.AddComponent<RuneTracer>();
            _tracer.fixedType = RuneType.Arrow;   // 제일 싸고 제일 그리기 쉬운 도형
            _tracer.autoPlace = false;
            _tracer.visible = false;
            _tracer.drawTime = 1.0f;
            _tracer.holdTime = 0.5f;
            _tracer.fadeTime = 0.4f;

            // 화면을 어둡게 깔고 손이 궤적을 따라간다. 도형만 떠 있으면
            // "저런 게 뜨는구나"로 끝나고 **내가 손을 대야 한다**가 안 읽힌다.
            _hint = go.AddComponent<TouchHint>();
            _hint.tracer = _tracer;
        }

        private TouchHint _hint;

        private void OnDestroy()
        {
            if (caster != null) caster.Cast -= OnCast;
            if (battle != null) battle.WaveStarted -= OnWaveStarted;
        }

        /// <summary>지금 프롤로그를 도는 중인가. TutorialUI가 이걸 보고 비켜선다.</summary>
        public bool Active
        {
            get { return _running && InPrologue; }
        }

        private bool InPrologue
        {
            get
            {
                return flow != null
                    && flow.State == GameState.Battle
                    && flow.CurrentStage != null
                    && flow.CurrentStage.IsPrologue;
            }
        }

        /// <summary>
        /// 대사는 적이 나타난 뒤에 시작한다.
        ///
        /// 판이 시작되자마자 "몇이야?"를 띄우면 셀 것이 화면에 없다. BattleManager는
        /// 아군을 먼저 세워 두고 startDelay만큼 기다렸다가 물결을 넣으므로,
        /// 그 신호를 받아서 시계를 켠다.
        /// </summary>
        private void OnWaveStarted(int waveIndex)
        {
            if (!InPrologue) return;

            _running = true;
            _beat = 0;
            _beatAt = Time.unscaledTime;
            _cheering = false;
            _wipeAt = -1f;
            _missUntil = -1f;
        }

        private void OnCast(RecognitionResult r, bool success, CastFail fail)
        {
            if (!Active || _cheering) return;

            if (!success)
            {
                // **실패가 처벌이 아니라 대사가 된다.** 아무 일도 안 일어난 것을
                // 병사가 대신 말해 주면, 다시 그리는 게 자연스러운 다음 수가 된다.
                _missUntil = Time.unscaledTime + MissHold;
                return;
            }

            _cheering = true;
            _cheerAt = Time.unscaledTime;
            if (_hint != null) _hint.visible = false;   // 해냈으면 안내는 즉시 걷는다
            _wipeAt = Time.unscaledTime + WipeDelay;
            _missUntil = -1f;
            _tracer.visible = false;
        }

        /// <summary>
        /// 남은 적을 쓸어버린다.
        ///
        /// 화살 한 발이 실제로 잡을 수 있는 건 한둘뿐인데, 그걸로 끝내면
        /// 병사들이 환호할 이유가 없다. **첫 개입은 신의 개입이어야 한다.**
        /// 룬 피해로 처리하므로 죽는 연출·처치 표시가 평소와 같은 길을 탄다.
        /// </summary>
        private void WipeEnemies()
        {
            ScreenFade.Flash(10f);
            TimeControl.HitStop(0.22f, 0.5f);
            CameraShake.Shake(0.3f, 0.4f);
            AudioManager.Play(Sfx.WaveClear, 0.9f);

            float x = battle != null ? battle.enemyLineX : 3.5f;
            BurstFx.Play(Vfx.StarBurst, new Vector3(x, 0f, 0f), 4.0f,
                new Color(1f, 0.92f, 0.62f), 60);

            var all = Unit.All;
            for (int i = all.Count - 1; i >= 0; i--)
            {
                Unit u = all[i];
                if (u == null || !u.IsAlive || u.team != Team.Enemy) continue;
                u.TakeDamage(99999f, true);
            }
        }

        /// <summary>
        /// 확대를 밀고 당긴다.
        ///
        /// **early return 위에서 돈다.** 프롤로그를 벗어나면 아래가 전부 안 도는데,
        /// 확대를 푸는 일까지 거기 있으면 스테이지를 나갔을 때 당겨진 화면이
        /// 그대로 남는다. 목표값만 상황에 따라 바꾸고 되돌리는 건 항상 돌게 둔다.
        /// </summary>
        private void UpdateZoom()
        {
            var fitter = CameraFitter.Instance;
            if (fitter == null) return;

            float target = (_running && InPrologue) ? zoom : 1f;
            if (Mathf.Approximately(_zoomNow, target)) return;

            float step = Time.unscaledDeltaTime / Mathf.Max(0.01f, zoomTime);
            _zoomNow = Mathf.MoveTowards(_zoomNow, target, step * (1f - zoom));
            fitter.SetZoom(_zoomNow);
        }

        private void Update()
        {
            UpdateZoom();

            if (!_running) return;

            // 전투를 벗어났다면(일시정지 후 나가기 등) 조용히 접는다
            if (!InPrologue) { _running = false; _tracer.visible = false;
                               if (_hint != null) _hint.visible = false; return; }

            if (_cheering)
            {
                if (_wipeAt > 0f && Time.unscaledTime >= _wipeAt)
                {
                    _wipeAt = -1f;
                    WipeEnemies();
                }

                if (Time.unscaledTime - _cheerAt >= CheerHold)
                {
                    _running = false;
                    _tracer.visible = false;
                    if (_hint != null) _hint.visible = false;
                    if (flow != null) flow.EndPrologue();
                }
                return;
            }

            // 실패 한 줄이 떠 있는 동안은 박자를 세우지 않는다
            if (_missUntil > 0f)
            {
                if (Time.unscaledTime >= _missUntil) _missUntil = -1f;
            }
            else if (_beat < Script.Length - 1 && Script[_beat].Hold > 0f
                     && Time.unscaledTime - _beatAt >= Script[_beat].Hold)
            {
                _beat++;
                _beatAt = Time.unscaledTime;
            }

            _tracer.visible = Script[_beat].Rune;
            if (_tracer.visible) PlaceTracer();

            // 손 안내는 **기다리는 박자에서만** 켠다. 도형이 처음 그려지는
            // 침묵 구간에는 도형 자체가 주인공이라 손이 시선을 나눈다.
            if (_hint != null) _hint.visible = _tracer.visible && Script[_beat].Hold <= 0f;
        }

        /// <summary>
        /// 시연 도형을 아군 앞쪽에 놓는다. TutorialUI와 같은 자리 —
        /// 실제로 그리게 될 자리 근처여야 따라 그리기가 자연스럽다.
        /// </summary>
        private void PlaceTracer()
        {
            Camera cam = Camera.main;
            if (cam == null) return;

            float h = cam.orthographicSize * 2f;
            float w = h * cam.aspect;

            _tracer.SetPlacement(new Vector3(w * 0.06f, h * 0.06f, 0f), h * 0.15f);
        }

        // ── 그리기 ──────────────────────────────────────────────

        /// <summary>
        /// 대사는 **한 크기, 제일 큰 것**으로만 쓴다.
        ///
        /// 처음엔 안내(TutorialUI)를 그대로 본떠서 큰 줄 17 + 작은 줄 13이었는데,
        /// 그건 화면 구석에서 규칙을 알려주는 글의 크기지 사람이 하는 말의 크기가
        /// 아니었다. 게다가 두 줄이 서로 다른 사람의 말인데 크기가 다르면
        /// **한쪽이 다른 쪽의 각주처럼** 읽힌다.
        ///
        /// 구운 크기는 셋뿐이고(13/17/32) 대사는 한글이라 로고 크기(64)를 쓸 수
        /// 없다. 그래서 32가 여기서 쓸 수 있는 제일 큰 값이다.
        /// 왼쪽 정렬인 건 대화창의 기본이다 — 가운데 정렬은 줄마다 시작점이
        /// 달라서 눈이 매번 줄 앞을 다시 찾는다.
        /// </summary>
        private void EnsureStyles()
        {
            if (_line != null) return;
            _line = new GUIStyle(GUI.skin.label)
                { fontSize = UiSkin.Text.Title, alignment = TextAnchor.MiddleLeft, wordWrap = true };
            UiSkin.ApplyFont(_line);
        }

        private void OnGUI()
        {
            if (!Active) return;
            if (Event.current.type != EventType.Repaint) return;

            // 눌러서 닫는 길을 두지 않는다. 튜토리얼은 방해가 되면 걷어낼 수 있어야
            // 하지만, 이건 한 번뿐인 장면이고 길어야 15초다. 닫기를 달면
            // 그리기를 기다리는 마지막 박자까지 같이 사라진다.
            UiScale.Begin();
            Draw();
            UiScale.End();
        }

        private void Draw()
        {
            EnsureStyles();

            string key, subKey;
            float shownAt;

            if (_cheering)
            {
                key = "story.p4"; subKey = "story.p4.sub"; shownAt = _cheerAt;
            }
            else if (_missUntil > 0f)
            {
                key = "story.pmiss"; subKey = null; shownAt = _missUntil - MissHold;
            }
            else
            {
                key = Script[_beat].Key; subKey = Script[_beat].SubKey; shownAt = _beatAt;
            }

            if (key == null) return;   // 침묵 — 도형만 보여준다

            // 뜰 때 살짝 올라오며 나타난다. 툭 나타나면 언제 바뀌었는지 안 보인다.
            float appear = Mathf.Clamp01((Time.unscaledTime - shownAt) / 0.3f);

            // 대화창. **화면 폭의 대부분을 쓴다** — 좁은 상자에 큰 글씨를 넣으면
            // 짧은 대사도 줄이 넘어간다. 위 한도(980)는 넓은 화면에서 한 줄이
            // 너무 길어져 눈이 되돌아오는 거리가 멀어지는 걸 막는다.
            float w = Mathf.Min(UiScale.W - 96f, 980f);
            float h = subKey == null ? 132f : 190f;
            float x = (UiScale.W - w) * 0.5f;

            // 아래에 붙이되 여백은 남긴다. 예전엔 62짜리 상자를 바닥에서 96 띄워
            // 놨는데, 얇은 띠가 화면 밑에 깔린 꼴이라 "구석의 안내"로 보였다.
            float y = UiScale.B - h - 44f + (1f - appear) * 16f;

            var box = new Rect(x, y, w, h);

            GUI.color = new Color(1f, 1f, 1f, appear);
            UiSkin.DrawPanel(box, new Color(0.13f, 0.14f, 0.19f, 0.96f * appear));
            UiSkin.DrawFrame(box, new Color(1f, 0.86f, 0.55f, 0.5f * appear));

            // 환호는 금빛으로. 절망하던 목소리와 같은 색이면 무엇이 바뀌었는지
            // 글을 읽어야만 알게 된다.
            Color tint = _cheering
                ? new Color(1f, 0.88f, 0.45f, appear)
                : new Color(0.90f, 0.92f, 0.97f, appear);

            const float pad = 42f, lineH = 46f;

            GUI.color = tint;
            GUI.Label(new Rect(x + pad, y + 26f, w - pad * 2f, lineH), Loc.T(key), _line);

            if (subKey != null)
            {
                // 둘째 목소리는 **들여쓰고 조금 어둡게.** 주고받는 말이라는 걸
                // 이름표 없이 알리는 제일 싼 방법이다 — 아군이 전부 같은
                // 스프라이트라 누가 말했는지는 어차피 못 가리킨다.
                GUI.color = new Color(tint.r, tint.g, tint.b, 0.78f * appear);
                GUI.Label(new Rect(x + pad + 44f, y + 26f + lineH + 14f, w - pad * 2f - 44f, lineH),
                    Loc.T(subKey), _line);
            }

            GUI.color = Color.white;
        }
    }
}
