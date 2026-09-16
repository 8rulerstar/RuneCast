using UnityEngine;
using RuneCast.Battle;
using RuneCast.Core;
using RuneCast.Gesture;
using RuneCast.Meta;
using RuneCast.Runes;

namespace RuneCast.UI
{
    /// <summary>
    /// 첫 판에서만 나오는 안내.
    ///
    /// **막지 않는다.** 팝업을 띄우고 "확인"을 누르게 하면 사람은 글을 읽지 않고
    /// 닫는 법부터 배운다. 여기서는 전투가 그대로 돌아가는 채로 화면 아래에 한 줄을
    /// 띄우고, **플레이어가 실제로 그 행동을 하면 저절로 사라진다.**
    /// 읽고 넘기는 게 아니라 해보고 넘어가는 구조다.
    ///
    /// 첫 단계에서는 그릴 도형을 **직접 그려 보인다**(RuneTracer). 이 게임에서
    /// "꺾쇠를 그리세요"라고 글로 쓰는 건 거의 전달이 안 된다 — 꺾쇠가 뭔지,
    /// 얼마나 크게, 어느 방향으로 그려야 하는지가 글에는 안 담긴다.
    ///
    /// 단계는 조건이 맞을 때만 뜬다. 마나 안내는 마나가 실제로 모자랐을 때,
    /// 잉크 안내는 실제로 획이 잘렸을 때 — **겪은 직후에 설명해야 붙는다.**
    /// </summary>
    public class TutorialUI : MonoBehaviour
    {
        public GameFlow flow;
        public BattleManager battle;
        public RuneCaster caster;

        private RuneTracer _tracer;
        private GUIStyle _line, _sub;

        private TutorialStep _active;
        private bool _showing;
        private float _shownAt;
        private float _hideAt = -1f;

        /// <summary>글만 뜨는 단계가 저절로 사라지기까지.</summary>
        private const float AutoHide = 5.5f;

        private void Start()
        {
            if (caster != null) caster.Cast += OnCast;
            if (battle != null)
            {
                battle.StageCleared += OnStageCleared;
            }

            // 시연용 궤적. 첫 단계에서만 켠다.
            var go = new GameObject("TutorialRune");
            go.transform.SetParent(transform, false);
            _tracer = go.AddComponent<RuneTracer>();
            _tracer.fixedType = RuneType.Arrow;   // 제일 싸고 제일 그리기 쉬운 도형
            _tracer.autoPlace = false;
            _tracer.visible = false;
            _tracer.drawTime = 1.0f;
            _tracer.holdTime = 0.5f;
            _tracer.fadeTime = 0.4f;

            // 프롤로그와 같은 안내. 첫 판에서도 **손을 대야 한다**를 말해야 한다 —
            // 프롤로그를 건너뛰고 온 사람(설정에서 다시 보기로 초기화한 경우 등)도 있다.
            _hint = go.AddComponent<TouchHint>();
            _hint.tracer = _tracer;
        }

        private TouchHint _hint;

        private void OnDestroy()
        {
            if (caster != null) caster.Cast -= OnCast;
            if (battle != null) battle.StageCleared -= OnStageCleared;
        }

        // ── 단계 진입/종료 ──────────────────────────────────────

        private void Show(TutorialStep step, bool autoHide)
        {
            if (TutorialProgress.Seen(step)) return;
            if (_showing && _active == step) return;

            _active = step;
            _showing = true;
            _shownAt = Time.unscaledTime;
            _hideAt = autoHide ? Time.unscaledTime + AutoHide : -1f;

            _tracer.visible = step == TutorialStep.Draw;

            // **읽기만 하면 되는 안내는 띄우는 즉시 본 것으로 친다.**
            // 반대로 그리기 안내(autoHide 없음)는 실제로 한 번 그려야 끝난다 —
            // 안 그려보고 판을 나갔는데 "봤다"고 기록하면 영영 다시 안 뜬다.
            if (autoHide) TutorialProgress.Mark(step);
        }

        /// <summary>할 일을 해냈다. 기록하고 닫는다.</summary>
        private void Finish()
        {
            if (!_showing) return;

            TutorialProgress.Mark(_active);
            Hide();
        }

        /// <summary>기록하지 않고 치운다. 아직 못 해낸 안내를 화면에서만 걷을 때.</summary>
        private void Hide()
        {
            _showing = false;
            _tracer.visible = false;
        }

        /// <summary>
        /// 프롤로그 중에는 통째로 비켜선다.
        ///
        /// 두 오버레이가 같은 자리에 겹치는 것도 문제지만, 더 큰 건 순서다.
        /// 프롤로그는 **플레이어가 누구인지**를 말하는 장면이고 튜토리얼은
        /// 규칙을 가르치는 자리다. "잘 그릴수록 세다"가 강림 장면 위에 뜨면
        /// 방금 본 것이 이야기가 아니라 조작 설명이 된다.
        ///
        /// 여기서 물러난 덕에 <see cref="TutorialStep.Draw"/>가 기록되지 않으므로,
        /// 그리기 안내는 1판에서 제대로 한 번 뜬다. 프롤로그에서 그려 봤더라도
        /// 규칙을 배우는 자리는 1판이 맞다.
        /// </summary>
        private bool InPrologue
        {
            get
            {
                return flow != null && flow.CurrentStage != null && flow.CurrentStage.IsPrologue;
            }
        }

        private void OnCast(RecognitionResult r, bool success, CastFail fail)
        {
            if (InPrologue) return;

            if (success)
            {
                // **성공했으면 지금 무엇이 떠 있든 그리기는 배운 것이다.**
                //
                // 예전엔 "그리기 안내가 떠 있을 때만" 기록했다. 그런데 그 사이에
                // 잉크·마나 안내가 끼어들면 _active가 바뀌어서 조건이 어긋났고,
                // 그러면 그리기 안내가 기록되지 않아 **판마다 계속 다시 떴다.**
                // 화면에 무엇이 떠 있느냐가 아니라 무슨 일이 일어났느냐로 판단해야 한다.
                TutorialProgress.Mark(TutorialStep.Draw);
                if (_showing && _active == TutorialStep.Draw) Hide();

                Show(TutorialStep.Grade, true);
                return;
            }

            // 실패는 겪은 직후에만 설명한다. 미리 알려주면 무슨 말인지 모른다.
            if (fail == CastFail.Ink) Show(TutorialStep.Ink, true);
            else if (fail == CastFail.Mana) Show(TutorialStep.Mana, true);
        }

        private void OnStageCleared(int stars)
        {
            Show(TutorialStep.Cleared, true);
        }

        private void Update()
        {
            if (InPrologue) { if (_showing) Hide(); return; }

            // 전투 밖에서는 안내가 남아 있을 이유가 없다 (클리어 안내만 결과 화면까지 간다)
            bool inBattle = flow == null || flow.State == GameState.Battle;

            // 전투를 나가면 화면에서만 걷는다. 기록은 하지 않는다 —
            // 그리기 안내는 실제로 그려봐야 끝나야 하기 때문.
            if (_showing && !inBattle && _active != TutorialStep.Cleared) Hide();

            if (_showing && _hideAt > 0f && Time.unscaledTime > _hideAt) Hide();

            // 첫 전투에 들어왔는데 아직 한 번도 안 그려봤다면 그리기 안내
            if (!_showing && inBattle && battle != null && battle.Running
                && !TutorialProgress.Seen(TutorialStep.Draw))
                Show(TutorialStep.Draw, false);

            // 망령이 처음 나타나면 규칙을 알려준다. **겪기 전에 알려야 하는 유일한 경우다** —
            // 다른 안내는 겪은 뒤가 낫지만, 이건 모르면 아군이 계속 헛손질하는 걸
            // 보면서도 왜 그런지 알 수가 없다.
            if (!_showing && inBattle && !TutorialProgress.Seen(TutorialStep.Wraith)
                && HasWraithOnField())
                Show(TutorialStep.Wraith, true);

            if (_tracer.visible) PlaceTracer();

            // 그리기 안내(Draw)일 때만. 등급·잉크·마나 안내는 글로만 하는
            // 것이라, 거기까지 화면을 어둡게 하면 전투를 못 본다.
            if (_hint != null)
                _hint.visible = _showing && _active == TutorialStep.Draw && _tracer.visible;
        }

        private static bool HasWraithOnField()
        {
            var all = Unit.All;
            for (int i = 0; i < all.Count; i++)
            {
                Unit u = all[i];
                if (u != null && u.IsAlive && u.team == Team.Enemy && u.resistsNormal) return true;
            }
            return false;
        }

        /// <summary>
        /// 시연 도형을 아군 앞쪽에 놓는다. 화면 한복판에 두면 정작 그려야 할 자리를 가리고,
        /// 구석에 두면 안 본다. 실제로 그리게 될 자리 근처가 맞다.
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

        private void EnsureStyles()
        {
            if (_line != null) return;
            _line = new GUIStyle(GUI.skin.label)
                { fontSize = UiSkin.Text.Large, alignment = TextAnchor.MiddleCenter };
            _sub = new GUIStyle(GUI.skin.label)
                { fontSize = UiSkin.Text.Small, alignment = TextAnchor.MiddleCenter };
            UiSkin.ApplyFont(_line, _sub);
        }

        private void OnGUI()
        {
            if (!_showing) return;

            // Repaint만 걸러내면 클릭을 못 받는다. 그리기는 Repaint에서만 하되
            // 마우스 이벤트는 통과시켜야 눌러서 닫을 수 있다.
            if (Event.current.type != EventType.Repaint &&
                Event.current.type != EventType.MouseDown) return;

            UiScale.Begin();
            Draw();
            UiScale.End();
        }

        /// <summary>
        /// 안내를 눌러서 닫는다.
        ///
        /// 자동으로 사라지는 것만 믿으면 안 된다 — 그리기 안내는 성공할 때까지 떠 있게
        /// 설계했는데, 그게 방해가 된다고 느끼는 순간 닫을 방법이 없으면 그냥 갇힌다.
        /// **닫으면 본 것으로 친다.** 스스로 껐다면 더 볼 생각이 없다는 뜻이다.
        /// </summary>
        private void HandleDismiss(Rect box)
        {
            if (Event.current.type != EventType.MouseDown) return;
            if (!box.Contains(Event.current.mousePosition)) return;

            TutorialProgress.Mark(_active);
            Hide();
            Event.current.Use();
        }

        private void Draw()
        {
            EnsureStyles();

            string key = "tut." + _active.ToString().ToLowerInvariant();
            string title = Loc.T(key);
            string hint = Loc.T(key + ".sub");

            // 뜰 때 살짝 올라오며 나타난다. 툭 나타나면 언제 바뀌었는지 안 보인다.
            float age = Time.unscaledTime - _shownAt;
            float appear = Mathf.Clamp01(age / 0.25f);
            float alpha = appear;

            // 사라지기 직전엔 서서히 흐려진다
            if (_hideAt > 0f)
            {
                float left = _hideAt - Time.unscaledTime;
                if (left < 0.6f) alpha *= Mathf.Clamp01(left / 0.6f);
            }

            const float w = 620f, h = 62f;
            float x = (UiScale.W - w) * 0.5f;

            // 화면 아래쪽. 위는 스테이지 정보와 룬 목록이 있고, 가운데는 그리는 자리다.
            float y = UiScale.B - h - 96f + (1f - appear) * 14f;

            var box = new Rect(x, y, w, h);
            HandleDismiss(box);

            if (Event.current.type != EventType.Repaint) return;

            GUI.color = new Color(1f, 1f, 1f, alpha);
            UiSkin.DrawPanel(box, new Color(0.30f, 0.33f, 0.40f, 0.95f * alpha));

            GUI.color = new Color(1f, 0.92f, 0.68f, alpha);
            GUI.Label(new Rect(x, y + 8f, w, 26f), title, _line);

            GUI.color = new Color(1f, 1f, 1f, 0.65f * alpha);
            GUI.Label(new Rect(x, y + 34f, w, 22f), hint, _sub);

            GUI.color = new Color(1f, 1f, 1f, 0.32f * alpha);
            GUI.Label(new Rect(x, y + h - 4f, w, 18f), Loc.T("tut.dismiss"), _sub);

            GUI.color = Color.white;
        }
    }
}
