using System;
using System.Collections.Generic;
using UnityEngine;
using RuneCast.Meta;

namespace RuneCast.Battle
{
    /// <summary>
    /// 한 스테이지를 실행한다.
    ///
    /// 예전에는 무한 웨이브 + 전멸 시 자동 재시작이었다. 스테이지 형식으로 바꾸면서
    /// **끝이 있는 판**이 됐다 — 마지막 웨이브를 정리하면 클리어, 아군이 전멸하면 실패.
    /// 판을 언제 다시 할지는 GameFlow가 정하므로 여기서 재시작하지 않는다.
    /// </summary>
    public class BattleManager : MonoBehaviour
    {
        public static BattleManager Instance { get; private set; }

        [Header("배치")]
        // **전장을 11.2칸에서 7칸으로 좁혔다.** 물결이 끝날 때마다 아군을
        // 시작선으로 되돌리게 하면서 함께 조정한 값이다.
        //
        // 되돌리기만 하면 매 물결마다 적이 전장을 가로질러 오는 9.3초가 통째로
        // 공짜 시간이 된다. 그 사이에 룬 여섯 방이 나가서 적이 닿기도 전에 죽는다 —
        // 실측으로 전 판이 3별이 됐고, 적을 1.5배로 올리면 이번엔 전멸했다.
        // 중간이 없는 구조였다.
        //
        // 폭을 7칸으로 줄이면 접근이 5.8초가 되어 균형이 돌아온다.
        // 양쪽 대칭이라 화면 한가운데서 적이 튀어나오지도 않는다.
        public float heroLineX = -3.5f;
        public float enemyLineX = 3.5f;

        /// <summary>
        /// 뒷줄이 앞줄보다 얼마나 물러서는가.
        ///
        /// **1.4칸.** 아군 근접 사거리가 1.15이므로, 이만큼 뒤에 서면 앞줄이
        /// 버티는 동안 뒷줄에는 적이 안 닿는다. 대신 앞줄이 무너지면 곧바로
        /// 노출된다 — 그 위험이 없으면 "전부 뒤로"가 언제나 정답이 되고,
        /// 그러면 배치가 고를 것이 없는 설정이 된다.
        /// </summary>
        public float backRowOffset = 1.4f;

        /// <summary>이 유닛이 서야 할 x. 뒷줄은 적에게서 더 멀다.</summary>
        public float SlotX(bool back)
        {
            return back ? heroLineX - backRowOffset : heroLineX;
        }

        /// <summary>이 유닛이 대열에서 서야 할 y. 물결 사이에 여기로 정렬한다.</summary>
        public float SlotY(int index)
        {
            if (Stage == null) return 0f;
            return (index - (Stage.HeroCount - 1) * 0.5f) * SpacingFor(Stage.HeroCount);
        }
        public float lineSpacing = 1.5f;

        [Tooltip("세로로 늘어설 수 있는 최대 폭. 유닛이 많아지면 간격을 좁혀 화면 안에 넣는다.")]
        public float maxColumnSpread = 6.6f;

        /// <summary>
        /// 웨이브 사이 간격(초).
        ///
        /// **2.2에서 3.4로 늘렸다.** 물결을 정리한 순간에 WAVE CLEAR 배너가 뜨는데
        /// 그 배너가 1.8초를 사는지라, 2.2초 간격에서는 배너가 채 사라지기도 전에
        /// 다음 무리가 들어왔다. 정리했다는 신호와 새로 왔다는 신호가 겹치면
        /// 둘 다 아무 뜻이 없어진다 — 한 박자 쉬어야 방금 것이 끝난 게 읽힌다.
        ///
        /// **위치는 안 움직인다.** 아군은 목표가 없으면 제자리에 서고(UnitAI),
        /// 대열은 물결이 끝나는 순간 이미 되돌려진다. 그래서 간격을 늘려도
        /// 전장 구도는 그대로다.
        ///
        /// 대신 **마나가 더 찬다. 그리고 그건 밸런스를 움직인다.**
        /// 물결마다 1.2초씩이니 2장(9~11/초)에서 물결당 11~13이 는다.
        /// sim_player.py로 재보니 **12판이 강화 없이도 3별이 됐다**(2.2초에서는 2별).
        ///
        /// 이 값을 늘린 건 연출 때문이지 난이도를 낮추려는 게 아니었다.
        /// 되돌리지 않은 이유는 12판의 2별이 이미 아슬아슬했기 때문인데,
        /// **"강화해야 3별"이라는 메타의 존재 이유가 여기서 사라진다** —
        /// 마나 회복량이나 12판 구성으로 따로 잡아야 한다.
        ///
        /// 이 값을 또 건드리면 시뮬레이터(tools/sim_battle.py의 WAVE_DELAY)도
        /// 같이 고치고 다시 돌릴 것.
        /// </summary>
        public float waveDelay = 3.4f;

        [Tooltip("마지막 적을 잡고 결과 화면이 뜨기까지의 여유(초). 연출을 볼 시간.")]
        public float clearDelay = 1.4f;

        /// <summary>
        /// 지고 나서 결과 화면까지 기다리는 시간.
        ///
        /// **예전에는 0이었다.** 마지막 아군이 쓰러지는 순간 결과 화면이 튀어나와서,
        /// 무엇 때문에 졌는지 볼 틈이 없었다. 진 판에서 배울 게 없으면 다시
        /// 할 이유도 줄어든다.
        /// </summary>
        public float failDelay = 1.5f;

        /// <summary>
        /// 판을 시작하고 첫 물결이 나오기까지.
        ///
        /// **예전에는 0이었다.** 스테이지를 고르자마자 적이 들어와서, 전장을
        /// 한 번 훑을 틈이 없었다. 이 게임은 "무엇을 상대하는가"를 보고 어떤 룬을
        /// 쓸지 정하는 게임인데, 판단할 시간을 안 주면 첫 물결은 늘 즉흥이 된다.
        ///
        /// 아군은 이 동안 이미 서 있다. 빈 전장이 아니라 **내 편이 서 있는
        /// 전장**을 먼저 보여주는 게 순서다.
        /// </summary>
        public float startDelay = 3f;

        /// <summary>
        /// 첫 물결까지 남은 시간. 준비 중이 아니면 0.
        ///
        /// **기다리는 시간은 보여야 기다릴 만하다.** 화면에 아무것도 없으면
        /// 멈춘 건지 준비 중인지 알 수 없고, 그러면 짧아도 답답하다.
        /// </summary>
        public float StartCountdown
        {
            // 첫 물결뿐 아니라 물결 사이에도 보여준다. 대열이 방금 되돌아왔고
            // 다음 무리가 언제 오는지 아는 편이 판을 읽는 데 낫다.
            get { return (Running && _waitingForWave) ? Mathf.Max(0f, _timer) : 0f; }
        }

        private bool _failing;

        public StageDef Stage { get; private set; }
        public bool Running { get; private set; }

        /// <summary>1부터 센다. 0이면 아직 첫 웨이브 전.</summary>
        public int WaveIndex { get; private set; }
        public int WaveCount { get { return Stage == null ? 0 : Stage.Waves.Length; } }

        public int HeroesTotal { get; private set; }
        public int HeroesAlive { get { return Unit.CountAlive(Team.Hero); } }

        /// <summary>인자는 획득한 별 수(1~3).</summary>
        public event Action<int> StageCleared;
        public event Action StageFailed;

        /// <summary>웨이브 하나를 정리했을 때. 방치형 요소(Phase 6)가 붙을 자리.</summary>
        public event Action<int> WaveCleared;

        /// <summary>웨이브가 나타났을 때. 인자는 1부터 세는 웨이브 번호.</summary>
        public event Action<int> WaveStarted;

        /// <summary>판이 시작됐을 때. 첫 물결보다 startDelay만큼 앞선다.</summary>
        public event Action<StageDef> StageStarted;

        private float _timer;
        private bool _waitingForWave;
        private bool _finishing;

        private void Awake()
        {
            Instance = this;
        }

        public void StartStage(StageDef stage)
        {
            Stage = stage;

            // **규칙을 먼저 정한다.** 아래 SpawnHeroes가 서리(공격 주기)를 읽고,
            // 적 소환도 안개·역병을 읽는다. 순서가 뒤바뀌면 첫 물결만 규칙 없이
            // 나오는데, 그건 화면에서 티가 안 나서 찾기 어렵다.
            ChapterRules.Begin(stage);

            ClearField();

            WaveIndex = 0;
            Running = true;
            _finishing = false;
            _failing = false;
            _waitingForWave = false;

            HeroesTotal = stage.HeroCount;
            SpawnHeroes();

            // 아군을 세워 두고 잠깐 기다린다. 첫 물결은 그 뒤에 온다.
            _waitingForWave = true;
            _timer = startDelay;

            if (StageStarted != null) StageStarted(stage);
        }

        /// <summary>전투를 멈추고 필드를 비운다. 스테이지 선택으로 나갈 때.</summary>
        public void Abort()
        {
            Running = false;
            _finishing = false;
            _failing = false;
            ClearField();
        }

        private void ClearField()
        {
            for (int i = Unit.All.Count - 1; i >= 0; i--)
                if (Unit.All[i] != null) Destroy(Unit.All[i].gameObject);
            Unit.All.Clear();

            // 화살·룬 효과·터지는 시트까지 전부 [Fx] 밑에 모여 있다.
            // 예전엔 화살만 지웠고, 그래서 봉화·소용돌이·별똥별 예고가 스테이지를 나간 뒤에도
            // 메뉴 화면 위에서 계속 돌아갔다.
            RuneCast.Core.FxRoot.ClearAll();

            // 모아둔 처치 수도 여기서 비운다. 전투 중에 나가면 창이 열린 채로
            // 남아서, 0.3초 뒤 스테이지 선택 화면 위에 "싹쓸이"가 떠오른다.
            // 시작할 때도 지나는 길목이라 지난 판 몫이 다음 판에 얹히는 것도 같이 막힌다.
            RuneCast.Core.KillFeed.Clear();
        }

        private void Update()
        {
            if (!Running || Stage == null) return;

            if (_finishing || _failing)
            {
                // 마지막 한 방 뒤에 연출을 볼 시간을 준다
                _timer -= Time.unscaledDeltaTime;
                if (_timer > 0f) return;

                if (_failing)
                {
                    _failing = false;
                    Running = false;
                    if (StageFailed != null) StageFailed();
                }
                else
                {
                    Finish();
                }
                return;
            }

            // **프롤로그는 승패로 끝나지 않는다.** 첫 룬이 나가는 순간이 결말이고,
            // 그 시점은 PrologueUI가 정한다. 여기서 판정하면 환호 대사 도중에
            // 결과 화면이 튀어나온다. 아군 체력을 크게 준 것도 이 예외에 기대지
            // 않으려는 것이다 — 둘 다 있어야 대사가 끝까지 간다.
            if (Unit.CountAlive(Team.Hero) == 0)
            {
                if (!Stage.IsPrologue) BeginFinale(false);
                return;
            }

            if (_waitingForWave)
            {
                _timer -= Time.deltaTime;
                if (_timer <= 0f) SpawnNextWave();
                return;
            }

            if (Unit.CountAlive(Team.Enemy) > 0) return;

            if (WaveCleared != null) WaveCleared(WaveIndex);

            if (WaveIndex >= Stage.Waves.Length)
            {
                // 프롤로그는 적을 전부 쓸어도 클리어가 아니다 — 위와 같은 이유.
                // 쓸어버리는 것 자체가 PrologueUI가 연출한 결말이므로,
                // 여기서 다시 끝내면 그 위에 결과 화면이 덮인다.
                if (!Stage.IsPrologue) BeginFinale(true);
                return;
            }

            _waitingForWave = true;
            _timer = waveDelay;

            ReformLine();
        }

        /// <summary>
        /// 아군을 시작 대열로 되돌린다. 물결이 끝나는 순간에만 부른다.
        ///
        /// **순간이동이다.** 걸어서 돌려보내는 것도, 그 자리에 세워 두는 것도
        /// 해봤는데 각각 다른 이유로 어색했다 — 걸어 돌아가면 물러서는 것처럼
        /// 보이고, 세워 두면 매 물결 조금씩 오른쪽으로 밀려 판이 끝날 때쯤
        /// 다섯이 구석에 몰린다.
        ///
        /// **가리는 게 요령이다.** WAVE CLEAR 배너가 뜨고 화면이 한 번 번쩍이는
        /// 바로 그 프레임에 옮긴다. 시선이 배너와 섬광에 가 있어서 옮겨진 걸
        /// 잘 못 본다. 게임에서 흔히 쓰는 방식이다.
        ///
        /// 이러면 매 물결이 **같은 구도에서 시작한다.** 어디서 싸우게 될지가
        /// 매번 같으므로 판을 읽는 데 드는 품이 줄어든다.
        /// </summary>
        private void ReformLine()
        {
            RuneCast.Core.ScreenFade.Flash(6f);

            var all = Unit.All;

            for (int i = 0; i < all.Count; i++)
            {
                Unit u = all[i];
                if (u == null || !u.IsAlive || u.team != Team.Hero) continue;

                // **카드로 내려놓은 유닛은 안 옮긴다.** 대열 복귀는 시작 편성을
                // 제자리로 돌리는 것인데, 내가 고른 자리까지 되돌리면
                // 배치가 물결 하나마다 무효가 된다.
                if (u.formationSlot < 0) continue;

                // 되돌릴 때도 뒷줄은 뒷줄로. 앞줄에 세워 버리면 물결 하나가
                // 지날 때마다 배치가 저절로 풀린다.
                u.transform.position = new Vector3(
                    SlotX(PlayerData.PartyBack(u.formationSlot)), SlotY(u.formationSlot), 0f);
            }
        }



        /// <summary>
        /// 판이 끝나는 순간의 박자.
        ///
        /// **예전에는 이 자리가 비어 있었다.** 이길 때는 조용히 1.4초를 기다렸다가
        /// 결과 화면이 떴고, 질 때는 그 기다림조차 없이 곧바로 떴다. 판 하나가
        /// 끝나는 자리인데 아무 일도 안 일어나면, 이겼다는 것도 졌다는 것도
        /// 결과 화면의 글자로만 알게 된다.
        ///
        /// 시간을 늦추는 게 핵심이다. 마지막 한 방이 눈에 남으려면 그 순간이
        /// 길어져야 한다 — 쓸어버린 순간(KillFeed)에 쓰는 것과 같은 방식이다.
        ///
        /// **이길 때와 질 때의 결이 달라야 한다.** 이길 때는 짧고 밝게 끊고,
        /// 질 때는 더 느리고 길게 늘어진다. 같은 연출이면 결과가 귀와 눈으로
        /// 안 갈린다.
        /// </summary>
        private void BeginFinale(bool cleared)
        {
            _finishing = cleared;
            _failing = !cleared;
            _timer = cleared ? clearDelay : failDelay;

            // **날아가던 것들을 거둔다.** 판이 끝나도 화살은 계속 날았다.
            // 마지막 순간을 보여주려고 멈춰 세운 화면 위로 뭔가가 가로질러
            // 지나가면, 그게 무엇인지 몰라서 눈이 그리로 끌린다.
            Projectile.SleepAll();

            // 전선 한가운데. 마지막 일이 벌어진 자리에 제일 가깝다.
            Vector3 at = new Vector3((heroLineX + enemyLineX) * 0.5f, 0f, 0f);

            if (cleared)
            {
                RuneCast.Core.TimeControl.HitStop(0.30f, 0.55f);
                RuneCast.Core.CameraShake.Shake(0.22f, 0.30f);
                // **크기를 5.5에서 3.0으로 줄였다.** 이 그림은 별이 사방으로
                // 흩어지는 것이라, 크게 쓰면 별 하나하나가 화면을 가로질러
                // 날아간다. 무엇이 터진 건지 대신 별이 지나가는 것만 보인다.
                RuneCast.Core.BurstFx.Play(RuneCast.Core.Vfx.StarBurst, at, 3.0f,
                    new Color(1f, 0.92f, 0.62f), 55);

                // **소리는 여기서 낸다.** 예전엔 결과 화면이 뜰 때(1.4초 뒤) 났다.
                // 이겼다는 신호가 마지막 적이 쓰러진 순간이 아니라 화면이 바뀔 때
                // 오면, 그 1.4초 동안은 이긴 줄도 모르고 기다리게 된다.
                RuneCast.Core.AudioManager.Play(RuneCast.Core.Sfx.WaveClear, 0.9f);
            }
            else
            {
                // 더 느리게, 더 길게. 지는 건 무너지는 것이지 끊기는 게 아니다.
                RuneCast.Core.TimeControl.HitStop(0.16f, 0.85f);
                RuneCast.Core.CameraShake.Shake(0.34f, 0.45f);
                RuneCast.Core.BurstFx.Play(RuneCast.Core.Vfx.SmallHit, at, 4.5f,
                    new Color(1f, 0.42f, 0.36f), 55);
                RuneCast.Core.AudioManager.Play(RuneCast.Core.Sfx.Defeat);
            }
        }

        private void Finish()
        {
            Running = false;
            _finishing = false;
            _failing = false;

            int stars = StageProgress.StarsFor(HeroesAlive, HeroesTotal);
            if (StageCleared != null) StageCleared(stars);
        }

        /// <summary>
        /// 유닛 수가 많아지면 간격을 좁혀서 세로로 화면을 벗어나지 않게 한다.
        /// </summary>
        private float SpacingFor(int count)
        {
            if (count <= 1) return lineSpacing;
            return Mathf.Min(lineSpacing, maxColumnSpread / (count - 1));
        }

        private void SpawnHeroes()
        {
            // **편성에서 읽는다.** 저장된 게 없으면 전부 전사로 떨어지므로
            // (PlayerData.PartyKind), 편성을 한 번도 안 건드린 사람도 예전과
            // 똑같은 판을 한다.
            float spacing = SpacingFor(Stage.HeroCount);

            for (int i = 0; i < Stage.HeroCount; i++)
            {
                float y = (i - (Stage.HeroCount - 1) * 0.5f) * spacing;
                int slot = i;

                UnitKind kind = PlayerData.PartyKind(i);
                bool back = PlayerData.PartyBack(i);
                UnitStatBlock stats = UnitStats.Of(kind);

                // 강화는 위력만 올린다(HeroRoster). 체력·공격력은 판이 정하고
                // 거기에 배율을 곱한다 — 종류마다 다른 건 사거리·주기·속도뿐이다.
                float power = HeroRoster.PowerOf(kind);

                Spawn(Team.Hero, kind, new Vector3(SlotX(back), y, 0f), u =>
                {
                    u.formationSlot = slot;
                    u.maxHp = Stage.HeroHp * power;
                    u.attackDamage = Stage.HeroDamage * power;
                    u.attackRange = stats.AttackRange;

                    // 서리(3장)는 **아군만** 느려진다. 적까지 같이 느려지면
                    // 서로 상쇄돼서 아무 일도 안 일어난다.
                    u.attackInterval = stats.AttackInterval * ChapterRules.HeroIntervalScale;
                    u.moveSpeed = stats.MoveSpeed;

                    // 원거리 용사는 발사체를 쏜다. 근접이 앞에 서 있는 동안
                    // 뒤에서 때리는 그림이 나와야 배치가 뜻을 가진다.
                    //
                    // **피해가 0이면 안 쏜다.** 수도사는 사거리가 길지만 때리지
                    // 않는다 — 그대로 두면 아무것도 안 하는 화살이 계속 날아간다.
                    u.ranged = stats.AttackRange >= 2f && stats.AttackDamage > 0f;

                    // 수도사는 주기적으로 주변 아군을 회복시킨다. **적 주술사와
                    // 같은 부품을 쓴다** — 예고 고리가 부풀고 터지는 것까지 같아서,
                    // 플레이어는 이미 배운 규칙으로 읽는다.
                    if (kind == UnitKind.Monk) u.gameObject.AddComponent<ShamanAura>();

                    // 서리(3장)는 아군에게만 걸린다. 느려진 것이 화면에서 안 보여서
                    // 얼음 연출과 찬 색을 같이 붙인다.
                    if (ChapterRules.HeroIntervalScale > 1f)
                        u.gameObject.AddComponent<FrostChill>();
                });
            }
        }

        /// <summary>
        /// 전투 중에 아군을 하나 내려놓는다. 카드 배치(DeployBar)가 부른다.
        ///
        /// **판 시작 때 서는 아군과 같은 규칙을 탄다** — 판의 체력·공격력에
        /// 강화 배율을 곱하고, 장 규칙(서리)도 그대로 걸린다. 배치로 낸 유닛만
        /// 다른 규칙을 타면 "카드로 낸 게 더 세다/약하다"를 따로 배워야 한다.
        ///
        /// 대열 자리(formationSlot)는 -1이다. 물결 사이 대열 복귀는 시작 편성만
        /// 되돌리는 것이고, 내가 놓은 자리는 내가 정한 것이라 옮기면 안 된다.
        /// </summary>
        public Unit DeployHero(UnitKind kind, Vector3 pos)
        {
            if (!Running || Stage == null) return null;

            UnitStatBlock stats = UnitStats.Of(kind);
            float power = HeroRoster.PowerOf(kind);

            return Spawn(Team.Hero, kind, pos, u =>
            {
                u.formationSlot = -1;
                u.maxHp = Stage.HeroHp * power;
                u.attackDamage = Stage.HeroDamage * power;
                u.attackRange = stats.AttackRange;
                u.attackInterval = stats.AttackInterval * ChapterRules.HeroIntervalScale;
                u.moveSpeed = stats.MoveSpeed;
                u.ranged = stats.AttackRange >= 2f && stats.AttackDamage > 0f;

                if (kind == UnitKind.Monk) u.gameObject.AddComponent<ShamanAura>();
                if (ChapterRules.HeroIntervalScale > 1f)
                    u.gameObject.AddComponent<FrostChill>();
            });
        }

        private void SpawnNextWave()
        {
            _waitingForWave = false;
            WaveDef wave = Stage.Waves[WaveIndex];
            WaveIndex++;

            int total = wave.TotalCount;
            float spacing = SpacingFor(total);
            int slot = 0;

            for (int g = 0; g < wave.Groups.Length; g++)
            {
                WaveGroup group = wave.Groups[g];
                UnitStatBlock stats = UnitStats.Of(group.Kind);

                for (int i = 0; i < group.Count; i++)
                {
                    float y = (slot - (total - 1) * 0.5f) * spacing;

                    // 두 줄로 살짝 어긋나게 세운다. 한 줄이면 전부 동시에 붙어서
                    // 전투가 한 순간에 몰리고 개입할 틈이 사라진다.
                    float x = enemyLineX + (slot % 2) * 0.8f;
                    slot++;

                    UnitKind kind = group.Kind;
                    Spawn(Team.Enemy, kind, new Vector3(x, y, 0f), u =>
                    {
                        // 망령은 평타가 20%만 들어간다. Spawn이 오브젝트를 꺼둔 채로
                        // 컴포넌트를 붙이므로 여기서 표식을 달아도 Awake보다 늦지 않다.
                        if (kind == UnitKind.Wraith) u.MarkResistant();

                        if (kind == UnitKind.Shaman) u.gameObject.AddComponent<ShamanAura>();

                        if (kind == UnitKind.Gnoll) u.ranged = true;

                        if (kind == UnitKind.Troll) u.gameObject.AddComponent<BossSlam>();

                        if (kind == UnitKind.Spider)
                        {
                            var sp = u.gameObject.AddComponent<Splitter>();
                            sp.Configure(group.HpScale, group.DamageScale);
                        }

                        // 장 규칙은 적에게 붙는다. 둘 다 "이 적이 어떻게 보이고
                        // 죽을 때 무슨 일이 나는가"라 유닛이 들고 있는 게 맞다.
                        if (ChapterRules.FogDistance > 0f) u.gameObject.AddComponent<FogVeil>();
                        if (ChapterRules.PlagueDamage > 0f) u.gameObject.AddComponent<PlagueBurst>();
                        if (ChapterRules.ManaPerKill > 0f) u.gameObject.AddComponent<AbyssHarvest>();

                        u.maxHp = stats.MaxHp * group.HpScale;
                        u.attackDamage = stats.AttackDamage * group.DamageScale;
                        u.attackRange = stats.AttackRange;
                        u.attackInterval = stats.AttackInterval;
                        u.moveSpeed = stats.MoveSpeed;
                    });
                }
            }

            if (WaveStarted != null) WaveStarted(WaveIndex);
        }

        /// <summary>
        /// 스탯을 Awake 전에 넣어야 한다. AddComponent는 Awake를 즉시 실행하는데
        /// Unit.Awake가 Hp = maxHp를 잡아버려서, 나중에 maxHp를 바꾸면 Hp는 기본값에 남는다.
        /// 비활성 상태로 만들어 두면 SetActive(true) 시점까지 Awake가 미뤄진다.
        /// </summary>
        /// <summary>
        /// 전투 중에 적을 하나 더 낸다. 거미가 쪼개질 때 쓴다.
        ///
        /// **웨이브 소환과 같은 길을 쓴다.** 따로 만들면 능력치 적용이나 표식
        /// 부착 같은 걸 한쪽에만 넣고 다른 쪽엔 빠뜨리게 된다.
        /// </summary>
        public Unit SpawnEnemy(UnitKind kind, Vector3 pos, float hpScale, float dmgScale)
        {
            UnitStatBlock stats = UnitStats.Of(kind);

            return Spawn(Team.Enemy, kind, pos, u =>
            {
                u.maxHp = stats.MaxHp * hpScale;
                u.attackDamage = stats.AttackDamage * dmgScale;
                u.attackRange = stats.AttackRange;
                u.attackInterval = stats.AttackInterval;
                u.moveSpeed = stats.MoveSpeed;
            });
        }

        private Unit Spawn(Team team, UnitKind kind, Vector3 pos, Action<Unit> configure)
        {
            var go = new GameObject(kind.ToString());
            go.SetActive(false);
            go.transform.position = pos;

            var unit = go.AddComponent<Unit>();
            unit.team = team;
            configure(unit);

            go.AddComponent<UnitAI>();

            var anim = go.AddComponent<UnitAnimator>();
            anim.kind = kind;

            go.SetActive(true);
            return unit;
        }
    }
}
