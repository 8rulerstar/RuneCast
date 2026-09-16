using System;
using UnityEngine;
using RuneCast.Battle;
using RuneCast.Core;
using RuneCast.Gesture;
using RuneCast.Runes;

namespace RuneCast.Meta
{
    public enum GameState
    {
        Title,
        StageSelect,
        Battle,
        Result,
        Workshop, // 강화·뽑기·문양 장착
        Profile,  // 내 정보 — 보기만 한다
        Settings,
        Credits,  // 에셋 출처 표기 (CC BY 4.0 라이선스 조건)
    }

    /// <summary>
    /// 화면 흐름. 스테이지 선택 → 전투 → 결과 → (다시 / 다음 / 목록).
    ///
    /// 전투 밖에서는 **궤적 입력을 막는다.** 안 그러면 스테이지 목록에서 마우스를 끌 때마다
    /// 슬로우가 걸리고 룬이 발동한다. 입력을 끄는 건 TraceCapture를 비활성화하는 것으로 충분하고,
    /// 그러면 OnDisable이 timeScale까지 되돌려 준다.
    /// </summary>
    public class GameFlow : MonoBehaviour
    {
        public static GameFlow Instance { get; private set; }

        public GameState State { get; private set; }
        public StageDef CurrentStage { get; private set; }

        /// <summary>결과 화면에 쓸 마지막 판 결과.</summary>
        public bool LastCleared { get; private set; }
        public int LastStars { get; private set; }
        public bool LastWasNewRecord { get; private set; }
        public int LastHeroesAlive { get; private set; }
        public int LastHeroesTotal { get; private set; }

        public event Action StateChanged;

        private BattleManager _battle;
        private TraceCapture _capture;
        private TraceRenderer _trace;
        private ManaPool _mana;
        private RuneCaster _caster;

        public void Bind(BattleManager battle, TraceCapture capture, TraceRenderer trace,
            ManaPool mana, RuneCaster caster)
        {
            _battle = battle;
            _capture = capture;
            _trace = trace;
            _mana = mana;
            _caster = caster;

            _battle.StageCleared += OnCleared;
            _battle.StageFailed += OnFailed;
        }

        private void Awake()
        {
            Instance = this;
            GameSettings.Load();   // 언어를 먼저 정해야 첫 화면부터 제대로 나온다
            StageProgress.Load();
            PlayerData.Load();
            PlayerData.PruneEquipped();
        }

        /// <summary>
        /// 스테이지 선택 화면이 스스로 뒤로 갈 수 있으면 여기에 꽂는다.
        /// true를 돌려주면 그 화면이 처리한 것이고, ESC는 거기서 멈춘다.
        ///
        /// 화면 안쪽 상태(어느 장을 펼쳤는가)를 GameFlow가 알 필요는 없다 —
        /// 아는 쪽이 답하게 두면 장이 늘어나도 여기는 안 바뀐다.
        /// </summary>
        public System.Func<bool> StageSelectBack;

        /// <summary>각인 모드. ESC가 무엇을 닫을지 정하는 데 쓴다.</summary>
        private RuneCast.Gesture.RuneRegistrar _registrar;

        public void BindRegistrar(RuneCast.Gesture.RuneRegistrar r)
        {
            _registrar = r;
        }

        private void Update()
        {
            if (!Hotkeys.Down(Hotkey.Escape)) return;

            // **각인 모드가 먼저다.** 예전엔 ESC가 곧장 일시정지로 갔다.
            // 각인 중에 누르면 각인은 켜진 채로 일시정지까지 겹쳐서,
            // 두 화면이 동시에 떠 있고 어느 쪽이 반응하는지 알 수 없었다.
            if (_registrar != null && _registrar.Active)
            {
                _registrar.Toggle();
                return;
            }

            if (State == GameState.Credits) CloseCredits();
            else if (State == GameState.Settings) CloseSettings();
            else if (State == GameState.Battle) TogglePause();
            else if (State == GameState.Workshop) GoToStageSelect();
            else if (State == GameState.Profile) GoToStageSelect();
            // 스테이지 선택 안에도 되돌아갈 곳이 생겼다(장을 펼쳐 본 상태).
            // 그쪽이 먼저 먹고, 처리하지 않았을 때만 타이틀로 나간다 —
            // ESC 한 번에 두 단계를 건너뛰면 어디로 갔는지 못 따라간다.
            else if (State == GameState.StageSelect)
            {
                if (StageSelectBack == null || !StageSelectBack()) GoToTitle();
            }
        }

        /// <summary>내 정보. 아무것도 바꾸지 않으므로 전투 상태를 건드릴 것도 없다.</summary>
        public void GoToProfile()
        {
            State = GameState.Profile;
            if (_battle != null) _battle.Abort();
            SetInputEnabled(false);
            Raise();
        }

        public void GoToWorkshop()
        {
            State = GameState.Workshop;
            if (_battle != null) _battle.Abort();
            if (_capture != null) _capture.SetInkLimit(float.MaxValue);

            SetInputEnabled(false);
            Raise();
        }

        // ── 일시정지 ────────────────────────────────────────────
        //
        // 일시정지를 별도 상태가 아니라 전투 위의 플래그로 둔다.
        // 상태로 만들면 "일시정지 → 설정 → 돌아오기" 같은 전이가 표를 두 배로 만든다.
        // 전투는 계속 Battle이고 시간만 멈춘 것이 실제 구조에 더 가깝다.

        public bool Paused { get; private set; }

        public void SetPaused(bool paused)
        {
            if (State != GameState.Battle) return;

            Paused = paused;
            TimeControl.SetPaused(paused);

            // 멈춘 동안 궤적을 받으면 시간이 멈춘 채로 룬이 나간다
            if (_caster != null) _caster.castingEnabled = !paused;
            if (_capture != null) _capture.captureEnabled = !paused;
            if (paused && _trace != null) _trace.Clear();

            Raise();
        }

        public void TogglePause()
        {
            SetPaused(!Paused);
        }

        // ── 설정 ────────────────────────────────────────────────

        private GameState _settingsReturn = GameState.StageSelect;

        public void GoToSettings()
        {
            if (State == GameState.Settings) return;

            _settingsReturn = State;
            State = GameState.Settings;

            // 전투 중에 열었다면 시간은 멈춘 채로 둔다 — 설정을 보는 동안
            // 뒤에서 전투가 흘러가면 그것만으로 판이 망가진다.
            if (_settingsReturn == GameState.Battle) TimeControl.SetPaused(true);
            if (_caster != null) _caster.castingEnabled = false;

            Raise();
        }

        /// <summary>
        /// 크레딧. 설정에서만 들어간다 — 설정과 같은 반환 지점을 쓰면
        /// 설정 → 크레딧 → 닫기가 게임 화면으로 튀어버려서, 돌아올 곳을 따로 기억한다.
        /// </summary>
        public void GoToCredits()
        {
            if (State == GameState.Credits) return;

            _creditsReturn = State;
            State = GameState.Credits;
            Raise();
        }

        public void CloseCredits()
        {
            State = _creditsReturn;
            Raise();
        }

        private GameState _creditsReturn = GameState.Settings;

        public void CloseSettings()
        {
            State = _settingsReturn;

            if (State == GameState.Battle)
            {
                // 일시정지 화면을 거쳐 왔다면 그 상태를 유지한다
                TimeControl.SetPaused(Paused);
                if (_caster != null) _caster.castingEnabled = !Paused;
            }
            else if (_caster != null)
            {
                _caster.castingEnabled = false;
            }

            Raise();
        }

        private void Start()
        {
            GoToTitle();
        }

        public void GoToTitle()
        {
            State = GameState.Title;
            if (_battle != null) _battle.Abort();
            if (_capture != null) _capture.SetInkLimit(float.MaxValue);

            SetInputEnabled(false);
            Raise();
        }

        /// <summary>게임을 끝낸다. 모바일에서는 앱을 내리고, 에디터에서는 플레이를 멈춘다.</summary>
        public void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        public void GoToStageSelect()
        {
            State = GameState.StageSelect;
            if (_battle != null) _battle.Abort();

            // 각인 모드는 전투 밖에서도 쓰므로 여기서는 잉크를 풀어 준다.
            // 도형을 등록할 때까지 길이 제한을 받으면 큰 도형을 아예 못 가르친다.
            if (_capture != null) _capture.SetInkLimit(float.MaxValue);

            SetInputEnabled(false);
            Raise();
        }

        // 이번 판에 쓴 룬 종류. "한 판에서 9종 다 쓰기" 업적을 위해 센다.
        private int _runeMask;
        private int _runeKindsThisStage;

        /// <summary>룬이 발동할 때마다 RuneCaster가 알려준다.</summary>
        public void NotifyRuneUsed(RuneCast.Gesture.RuneType type)
        {
            int bit = 1 << (int)type;
            if ((_runeMask & bit) != 0) return;

            _runeMask |= bit;
            _runeKindsThisStage++;
        }

        public void StartStage(StageDef stage)
        {
            if (stage == null) return;

            CurrentStage = stage;
            _runeMask = 0;
            _runeKindsThisStage = 0;
            Achievements.BeginStage();

            State = GameState.Battle;

            if (_mana != null)
            {
                _mana.unlimited = stage.UnlimitedMana;
                _mana.max = stage.MaxMana;
                _mana.regenPerSecond = stage.ManaRegen;
                _mana.Refill();
            }

            // 잉크 한도는 지금까지 모은 별로 정해지고, 장착한 문양이 여기에 곱해진다.
            // 해상도가 바뀌어도 체감이 같도록 화면 높이 기준으로 환산한다.
            if (_capture != null)
                // 개발용 무한 잉크. 설정에서 켠다 — 도형을 시험하거나 큰 룬의
                // 연출을 볼 때 제한이 방해만 되는 경우가 있다.
                _capture.SetInkLimit(GameSettings.InfiniteInk
                    ? float.MaxValue
                    : InkBudget.CurrentPixels * Loadout.InkMultiplier());

            Paused = false;
            TimeControl.SetPaused(false);

            SetInputEnabled(true);
            if (_battle != null) _battle.StartStage(stage);
            Raise();
        }

        public void RetryStage()
        {
            StartStage(CurrentStage);
        }

        // ── 프롤로그 ────────────────────────────────────────────
        //
        // 전투와 같은 길을 탄다. 상태를 새로 만들지 않은 이유는 일시정지와 같다 —
        // 화면 하나를 늘리면 설정·크레딧으로 드나드는 전이가 전부 새 칸이 된다.
        // 실제로 벌어지는 일은 그냥 전투이고, 끝내는 방식만 다르다.

        /// <summary>타이틀에서 "게임 시작"을 처음 눌렀을 때.</summary>
        public void StartPrologue()
        {
            StartStage(StageDatabase.Prologue);
        }

        /// <summary>
        /// 프롤로그를 끝낸다. `PrologueUI`가 환호 대사까지 마치고 부른다.
        ///
        /// **별도 결과 화면으로 가지 않는다.** 별도 파편도 없는 판이라 결과 화면에
        /// 띄울 게 없고, 첫 장면 직후에 "STAGE CLEAR"가 뜨면 방금 본 것이
        /// 이야기가 아니라 판 하나였던 것이 된다.
        /// </summary>
        public void EndPrologue()
        {
            TutorialProgress.Mark(TutorialStep.Prologue);
            GoToStageSelect();
        }

        public void NextStage()
        {
            StageDef next = CurrentStage != null ? StageDatabase.Next(CurrentStage.Id) : null;
            if (next == null) { GoToStageSelect(); return; }
            StartStage(next);
        }

        public bool HasNextStage
        {
            get { return CurrentStage != null && StageDatabase.Next(CurrentStage.Id) != null; }
        }

        /// <summary>이번 판에서 얻은 파편. 결과 화면이 보여준다.</summary>
        public int LastShards { get; private set; }

        private void OnCleared(int stars)
        {
            LastCleared = true;
            LastStars = stars;
            LastHeroesAlive = _battle.HeroesAlive;
            LastHeroesTotal = _battle.HeroesTotal;

            bool firstClear = !StageProgress.IsCleared(CurrentStage.Id);
            LastWasNewRecord = StageProgress.Record(CurrentStage.Id, stars);

            // 첫 클리어에 크게 준다. 반복 파밍보다 앞으로 나아가는 쪽이 이득이어야
            // 쉬운 스테이지를 무한히 도는 지루한 최적해가 안 생긴다.
            int baseShards = 12 + CurrentStage.Id * 4 + stars * 10;
            LastShards = firstClear ? baseShards * 3 : Mathf.RoundToInt(baseShards * 0.5f);
            PlayerData.AddShards(LastShards);

            Achievements.OnStageCleared(LastHeroesAlive, LastHeroesTotal, _runeKindsThisStage);

            // 승리 소리는 BattleManager가 마지막 적이 쓰러지는 순간에 낸다.
            // 여기(결과 화면이 뜰 때)서 내면 1.4초 늦는다.
            EndBattle();
        }

        private void OnFailed()
        {
            LastCleared = false;
            LastStars = 0;
            LastHeroesAlive = 0;
            LastHeroesTotal = _battle.HeroesTotal;
            LastWasNewRecord = false;
            LastShards = 0;

            EndBattle();
        }

        private void EndBattle()
        {
            State = GameState.Result;
            SetInputEnabled(false);
            Raise();
        }

        /// <summary>
        /// 전투 밖에서는 룬이 발동하지 않게 한다.
        ///
        /// TraceCapture 자체를 끄지 않는 이유: 각인 모드가 궤적을 받아야 해서다.
        /// 스테이지 선택 화면에서도 도형을 등록할 수 있어야 하므로,
        /// 수집은 살려두고 **발동과 슬로우만** 끈다.
        /// </summary>
        private void SetInputEnabled(bool on)
        {
            if (_caster != null) _caster.castingEnabled = on;
            if (_capture != null)
            {
                _capture.slowMotionEnabled = on;

                // **획 수집도 같이 끈다.** 시전만 막으면 선은 그대로 그려져서,
                // 메뉴를 띄워 놓고 화면을 문지르면 그림이 남는다.
                // 각인 모드는 자기가 다시 켠다.
                _capture.captureEnabled = on || (_registrar != null && _registrar.Active);
            }

            if (!on)
            {
                if (_trace != null) _trace.Clear();
                FloatingText.Clear(); // 결과 화면 위에 전투 숫자가 떠 있으면 지저분하다

                // 마지막 한 방으로 판이 끝나면 창이 열린 채로 남는다. 안 비우면
                // 다음 판 첫 죽음에 지난 판 몫이 얹혀서 엉뚱하게 "싹쓸이"가 뜬다.
                RuneCast.Core.KillFeed.Clear();
                TimeControl.ResetAll(); // 슬로우·히트스톱이 남아 있으면 UI까지 느려진다
            }
        }

        private GameState _fadedFrom = GameState.Title;

        private void Raise()
        {
            // 화면이 실제로 바뀔 때만 암전한다. 일시정지 토글처럼 같은 화면 안의
            // 변화까지 암전하면 눈이 피로해진다.
            if (State != _fadedFrom)
            {
                _fadedFrom = State;
                ScreenFade.Flash();
            }

            if (StateChanged != null) StateChanged();
        }
    }
}
