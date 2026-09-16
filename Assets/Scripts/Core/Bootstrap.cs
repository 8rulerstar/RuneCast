using UnityEngine;
using RuneCast.Battle;
using RuneCast.Gesture;
using RuneCast.Runes;
using RuneCast.UI;

namespace RuneCast.Core
{
    /// <summary>
    /// 씬 전체를 코드로 조립한다. 컴포넌트를 손으로 붙이거나 인스펙터에서
    /// 참조를 연결할 필요가 없다 — 어느 씬에서 Play를 눌러도 그대로 돌아간다.
    ///
    /// 프로토타입 단계에서 이렇게 하는 이유: 씬 파일은 diff가 안 읽히고 병합도 안 된다.
    /// 구성이 코드에 있으면 무엇이 왜 바뀌었는지 git 로그에 남는다.
    /// 구조가 굳으면 프리팹/씬으로 옮기면 된다.
    /// </summary>
    public class Bootstrap : MonoBehaviour
    {
        private static Bootstrap _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoStart()
        {
            if (_instance != null) return;

            var go = new GameObject("[RuneCast]");
            _instance = go.AddComponent<Bootstrap>();
        }

        private void Awake()
        {
            _instance = this;

            // 저장된 커스텀 룬을 기본 템플릿 위에 얹는다. 인식기가 처음 쓰이기 전에 끝나야 한다.
            CustomRuneStore.LoadIntoLibrary();

            SetUpCamera();
            RebuildBackdrop();
            SetUpSystems();
        }

        private Backdrop _backdrop;
        private MenuBackdrop _menuBackdrop;
        private BattleManager _battle;
        private RuneCast.Meta.GameFlow _flow;

        /// <summary>
        /// 배경. 카메라 크기에 맞춰야 하므로 화면이 바뀌면 다시 만든다.
        /// 움직이는 요소(꽃가루)가 있어 Backdrop 컴포넌트가 따로 존재한다.
        /// </summary>
        private void RebuildBackdrop()
        {
            if (_backdrop != null) Destroy(_backdrop.gameObject);
            if (_menuBackdrop != null) Destroy(_menuBackdrop.gameObject);

            Camera cam = Camera.main;
            float h = cam != null ? cam.orthographicSize * 2f : 11f;
            float w = h * (cam != null ? cam.aspect : 1.78f);

            // 여백을 넉넉히 — 화면비가 달라져도 가장자리가 드러나지 않게
            _backdrop = Backdrop.Create(w * 1.3f, h * 1.3f);

            // **배경은 둘이다.** 전투에서는 초원, 메뉴에서는 어두운 배경.
            // 예전엔 메뉴가 전투 위에 반투명 막을 덮어서 초원이 계속 비쳤다.
            _menuBackdrop = MenuBackdrop.Create(w * 1.3f, h * 1.3f);

            SyncBackdrops();
        }

        private void SetUpCamera()
        {
            Camera cam = Camera.main;
            if (cam == null)
            {
                var go = new GameObject("Main Camera");
                go.tag = "MainCamera";
                cam = go.AddComponent<Camera>();
            }

            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.07f, 0.08f, 0.12f);
            cam.transform.position = new Vector3(0f, 0f, -10f);

            if (cam.GetComponent<CameraShake>() == null) cam.gameObject.AddComponent<CameraShake>();

            // 확대 배율은 CameraFitter가 화면비를 보고 정한다.
            // 고정값을 쓰면 4:3에서 적이 화면 밖에서 죽는다.
            var fitter = cam.GetComponent<CameraFitter>();
            if (fitter == null) fitter = cam.gameObject.AddComponent<CameraFitter>();
            fitter.Refitted += RebuildBackdrop;
        }

        private void SetUpSystems()
        {
            // ── 오디오 (다른 시스템이 Play를 부르기 전에 먼저) ──
            gameObject.AddComponent<AudioManager>();

            // ── 시간 제어 (슬로우·히트스톱을 합쳐 timeScale을 정하는 유일한 곳) ──
            gameObject.AddComponent<TimeControl>();

            // ── 떠오르는 숫자 (피해·회복·보호막) ──
            gameObject.AddComponent<FloatingText>();
            gameObject.AddComponent<KillFeed>();

            // ── 화면 전환 암전 ──
            gameObject.AddComponent<ScreenFade>();

            // ── 전투 ──
            var battle = gameObject.AddComponent<BattleManager>();
            battle.WaveCleared += OnWaveCleared;
            _battle = battle;

            // ── 제스처 입력 ──
            var capture = gameObject.AddComponent<TraceCapture>();

            var traceGo = new GameObject("TraceRenderer");
            traceGo.transform.SetParent(transform, false);
            traceGo.AddComponent<LineRenderer>();
            var trace = traceGo.AddComponent<TraceRenderer>();
            trace.Bind(capture);

            // ── 룬 ──
            var mana = gameObject.AddComponent<ManaPool>();
            var caster = gameObject.AddComponent<RuneCaster>();
            caster.Bind(capture);

            // 시전이 성공하면 그은 선이 인식된 도형으로 정돈된다.
            // capture.Bind보다 뒤여야 한다 — TraceRenderer가 먼저 페이드를 걸고,
            // 그 위에 이 연출이 덮어써야 순서가 맞는다.
            trace.BindCaster(caster);

            // ── 각인 모드 ──
            var registrar = gameObject.AddComponent<RuneRegistrar>();
            registrar.Bind(capture, caster);

            // ── 스테이지 흐름 ──
            // 전투는 GameFlow가 시작한다. Bootstrap은 조립만 하고 게임을 켜지 않는다.
            var flow = gameObject.AddComponent<RuneCast.Meta.GameFlow>();
            flow.Bind(battle, capture, trace, mana, caster);
            caster.flow = flow;
            _flow = flow;

            // ESC가 무엇을 닫을지 정하려면 흐름이 각인 모드를 알아야 한다.
            // registrar가 flow보다 먼저 만들어지므로 여기서 이어 준다.
            flow.BindRegistrar(registrar);

            // ── UI ──
            var hud = gameObject.AddComponent<GestureHUD>();
            hud.caster = caster;
            hud.mana = mana;
            hud.battle = battle;
            hud.capture = capture;
            hud.registrar = registrar;
            hud.flow = flow;

            // 장마다 화면에 얇은 색을 깐다. HUD보다 뒤(GUI.depth)에 그려진다.
            gameObject.AddComponent<ChapterAtmosphere>().flow = flow;

            // 첫 장면. 병사들이 절망하고, 플레이어의 첫 룬을 강림으로 받아들인다.
            // 튜토리얼보다 먼저 붙여서 대사가 안내 위에 그려지게 한다 —
            // 겹칠 일은 TutorialUI가 프롤로그 중에 물러나므로 없지만, 순서가
            // 뜻을 말해 준다: 이야기가 먼저고 규칙은 그다음이다.
            var prologue = gameObject.AddComponent<PrologueUI>();
            prologue.flow = flow;
            prologue.battle = battle;
            prologue.caster = caster;

            // 첫 판에서만 나오는 안내. 전투를 막지 않고, 실제로 해보면 사라진다.
            var tutorial = gameObject.AddComponent<TutorialUI>();
            tutorial.flow = flow;
            tutorial.battle = battle;
            tutorial.caster = caster;

            // 화면 아래 카드 줄. 끌어다 놓아 아군을 낸다.
            // **일시정지 버튼보다 먼저** 붙인다 — 둘 다 TraceCapture에 자리를
            // 등록하는데, 등록은 순서와 무관하지만 화면 아래를 나눠 쓰므로
            // 자리 다툼이 생기면 여기부터 보게 된다.
            var deploy = gameObject.AddComponent<DeployBar>();
            deploy.flow = flow;
            deploy.battle = battle;
            deploy.mana = mana;

            // 전투 중 화면 일시정지 버튼. 폰에는 Esc가 없어서 이게 유일한 출구다.
            gameObject.AddComponent<PauseButtonUI>().flow = flow;

            var meta = gameObject.AddComponent<MetaUI>();
            meta.flow = flow;

            // 타이틀에서 룬이 저절로 그려진다. 이 게임이 뭔지 말해 주는 건
            // 글씨가 아니라 그 그림이다.
            var titleGo = new GameObject("TitleRune");
            titleGo.transform.SetParent(transform, false);
            titleGo.AddComponent<RuneTracer>().flow = flow;

            // 내 정보 — 흩어진 현황을 한 화면에 모아 보는 곳. 바꾸지는 않는다.
            gameObject.AddComponent<ProfileUI>().flow = flow;

            var workshop = gameObject.AddComponent<WorkshopUI>();
            workshop.flow = flow;
            workshop.registrar = registrar;

            var settings = gameObject.AddComponent<SettingsUI>();
            settings.flow = flow;

            // 업적 달성 알림. 없으면 달성해도 화면에 아무 일이 없어서
            // 기록 탭을 일부러 열기 전엔 알 수가 없다.
            gameObject.AddComponent<AchievementToast>();

            // 크레딧은 라이선스 조건이라 빼면 안 된다 (UI 팩이 CC BY 4.0)
            var credits = gameObject.AddComponent<CreditsUI>();
            credits.flow = flow;

            // 저장된 설정을 실제 시스템(음량·슬로우·디버그 패널)에 반영
            RuneCast.Meta.GameSettings.Apply();
        }

        /// <summary>
        /// 어느 배경을 보일지 정한다. 전투에서만 초원, 그 밖에서는 메뉴 배경.
        ///
        /// IMGUI로 불투명하게 덮는 방법은 못 쓴다 — OnGUI는 항상 씬 위에 그려져서
        /// 타이틀에서 룬을 그리는 RuneTracer까지 같이 가려 버린다.
        /// </summary>
        private void SyncBackdrops()
        {
            // **UI 상태가 아니라 "전투가 진행 중인가"로 판단한다.**
            //
            // 상태로 판단하면 전투 중에 일시정지 → 설정을 열었을 때 상태가 Settings로
            // 바뀌면서 초원이 사라진다. 유닛은 그대로 서 있는데 바닥만 없어지는 셈이다.
            // BattleManager.Running은 스테이지를 시작할 때 켜지고 나갈 때(Abort) 꺼지므로
            // 그 사이에 어떤 화면을 열든 배경은 초원으로 유지된다.
            //
            // 시작 첫 프레임에도 맞는다 — 아직 전투를 시작 안 했으니 메뉴 배경이다.
            // 결과 화면도 전투로 친다. 클리어한 전장이 뒤에 남아 있어야 방금 한 판의
            // 결과라는 게 읽히고, 무엇보다 유닛은 그대로 서 있는데 바닥만 메뉴 배경으로
            // 바뀌면 유닛이 허공에 뜬다.
            bool inBattle = (_battle != null && _battle.Running)
                            || (_flow != null && _flow.State == RuneCast.Meta.GameState.Result);

            if (_backdrop != null)
            {
                _backdrop.SetVisible(inBattle);

                // 장이 바뀌면 바닥도 바뀐다. 매 프레임 부르지만 안쪽에서
                // 같은 스프라이트면 그대로 두므로 값이 싸다.
                if (_battle != null && _battle.Stage != null)
                    _backdrop.SetChapter(_battle.Stage.Chapter);
            }
            if (_menuBackdrop != null) _menuBackdrop.SetVisible(!inBattle);

            // 음악도 같은 기준으로 간다. 배경만 갈라 놓고 음악이 그대로면
            // 화면이 바뀐 게 귀로는 안 읽힌다.
            AudioManager.SetGroup(inBattle ? AudioManager.BgmGroup.Battle
                                           : AudioManager.BgmGroup.Menu);

            // **전투 중에는 화면을 안 끈다.**
            //
            // 이 게임은 자동전투다. 용사들이 알아서 싸우는 동안 플레이어가 화면을
            // 30초씩 안 만지는 건 정상이고 오히려 의도한 리듬이다. 그런데 폰은
            // 그걸 "안 쓰는 중"으로 보고 화면을 어둡게 하다 잠근다 —
            // **하필 개입해야 할 순간에.**
            //
            // 메뉴에서는 원래대로 둔다. 대장장이 화면을 켜 놓고 자리를 비웠는데
            // 배터리가 마르면 그건 그것대로 잘못이다.
            Screen.sleepTimeout = inBattle ? SleepTimeout.NeverSleep : SleepTimeout.SystemSetting;
        }

        private void Start()
        {
            // 첫 화면이 그려지기 전에 글꼴 아틀라스를 다 채워둔다.
            // 늦게 하면 이미 그려진 글자가 어긋난다 — FontWarmup 설명 참고.
            FontWarmup.Run();

            // **손으로 그리는 게임이라 프레임이 곧 입력 해상도다.** 30프레임이면
            // 획을 초당 서른 번밖에 못 읽어서, 빨리 그린 곡선이 각져 들어온다.
            // 인식기가 보는 점 자체가 뭉툭해지는 것이라 그리는 감각 이전의 문제다.
            //
            // 120Hz 기기에서 120을 다 쓰지 않는 이유는 그 위로는 인식에 보탬이
            // 없고 배터리와 발열만 늘기 때문이다. 모바일에서 발열은 곧 성능 저하다.
            Application.targetFrameRate = 60;
        }

        private void Update()
        {
            SyncBackdrops();
        }

        /// <summary>
        /// 앱이 백그라운드로 갈 때. 저장을 흘려보내고 전투를 멈춰 둔다.
        ///
        /// **모바일은 종료 없이 죽는다.** OnApplicationQuit은 안 불릴 수 있어서
        /// 여기가 마지막으로 믿을 수 있는 지점이다.
        /// </summary>
        private void OnApplicationPause(bool paused)
        {
            if (!paused) return;

            RuneCast.Meta.Achievements.Flush();
            RuneCast.Meta.PlayerData.FlushBatch();

            // **전투 중이었으면 멈춰 둔다.** 앱이 뒤로 가면 게임도 같이 멎지만,
            // 돌아오는 순간 아무 예고 없이 다시 굴러간다. 전화를 받고 돌아왔더니
            // 이미 맞고 있는 셈이다 — 알림 하나, 다른 앱 확인 한 번에도 그렇다.
            //
            // 자동전투라서 더 그렇다. 손을 놓은 사이에 전황이 바뀌어 있는데
            // 그걸 읽을 시간을 안 주면 개입할 기회 자체가 없다.
            if (_flow != null && _flow.State == RuneCast.Meta.GameState.Battle)
                _flow.SetPaused(true);
        }

        private void OnApplicationQuit()
        {
            RuneCast.Meta.Achievements.Flush();
            RuneCast.Meta.PlayerData.FlushBatch();
        }

        /// <summary>
        /// 방치형 요소(Phase 6)가 붙을 자리. 지금은 소리만 낸다 —
        /// 성장·보상은 제스처가 재밌다는 게 확인된 뒤에 설계한다.
        /// </summary>
        private static void OnWaveCleared(int wave)
        {
            AudioManager.Play(Sfx.WaveClear, 0.85f);
        }
    }
}
