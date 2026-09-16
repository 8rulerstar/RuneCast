using System.Collections.Generic;
using RuneCast.Battle;

namespace RuneCast.Meta
{
    /// <summary>한 웨이브에 나올 적 한 무리.</summary>
    public struct WaveGroup
    {
        public UnitKind Kind;
        public int Count;
        public float HpScale;
        public float DamageScale;

        public WaveGroup(UnitKind kind, int count, float hpScale = 1f, float damageScale = 1f)
        {
            Kind = kind;
            Count = count;
            HpScale = hpScale;
            DamageScale = damageScale;
        }
    }

    public class WaveDef
    {
        public WaveGroup[] Groups;
        public WaveDef(params WaveGroup[] groups) { Groups = groups; }

        public int TotalCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < Groups.Length; i++) n += Groups[i].Count;
                return n;
            }
        }
    }

    public class StageDef
    {
        // ── 밸런스 근거 ───────────────────────────────────────────────────
        //
        // **눈으로 맞추지 않았다.** tools/ 아래 시뮬레이터가 UnitAI의 규칙을 그대로 돌린다.
        // 표는 이 파일에서 직접 읽으므로(tools/read_stages.py) 손으로 옮길 필요가 없다.
        //
        //   sim_battle.py   개입이 없을 때 / "초당 몇 피해면 이기나"
        //   sim_player.py   **마나를 쓰는 플레이어**를 흉내 낸다 — 42판 전부를 돈다
        //
        // 실측 (2026-08-05, 판당 30회, 강화 1.0 = 아무것도 안 키움):
        //
        //   장            강화 1.0                        강화 1.6      강화 2.2
        //   1~2 (1~12)    전부 3별                         3별           3별
        //   3   (13~18)   3별, 18판만 2별                   3별           3별
        //   4   (19~24)   3별, 22판 2별 · 24판 1별           3별           3별
        //   5   (25~30)   27·28판 2별 · 30판 승률 37%       3별           3별
        //   6   (31~36)   3별, 36판만 2별                   3별           3별
        //   7   (37~42)   37~39판 2별 · 40~42판 전패        41판 1별      42판 1별
        //
        // 읽는 법:
        //  · **1·2장은 강화 없이 전부 3별이다.** 물결 간격을 2.2→3.4초로 늘리면서
        //    그렇게 됐다(연출을 위해 늘렸고, 되돌리지 않았다). 그래서 앞 열두 판은
        //    이제 난이도가 아니라 배우는 구간이다.
        //  · **대장장이가 필요해지는 곳은 5장부터다.** 30판에서 처음으로 강화 없이
        //    지기 시작하고, 7장은 강화 없이 마지막 세 판을 못 넘는다.
        //  · **42판은 만렙(2.2)에서 1별로 겨우 이긴다.** 최종 판으로 의도한 자리다.
        //
        // 시뮬레이터가 **안 세는 것** — 이 표를 믿기 전에 반드시 볼 것:
        //  · 힐·보호막·고양·소생, 그리고 "어디에 쓸지"의 판단 → 실제는 더 쉽다
        //  · **4장 안개는 수치 영향이 0이다.** 유닛이 흐려질 뿐이라 도구가 잴 게 없다 →
        //    4장 숫자는 실제보다 쉽게 나온다
        //  · **6장 봉인은 실제보다 덜 잠긴다.** 시뮬레이터는 룬 넷만 쓰는데 게임은
        //    아홉 개에서 고른다 → 6장도 실제보다 쉽게 나온다.
        //    6장 표가 3장처럼 평평한 건 그래서다 — 적을 더 넣어 봤지만 별 수가
        //    안 움직였다. **표를 보고 6장을 더 조이면 실제로는 과해진다.**
        //    이 장은 사람이 해 보고 정해야 한다.
        //
        // 수치를 고치면 `python3 tools/sim_player.py`를 다시 돌릴 것.

        public int Id;
        public int Chapter;
        public WaveDef[] Waves;

        /// <summary>
        /// 프롤로그인가. **승패로 끝나지 않는다** — 첫 룬이 나가면 `PrologueUI`가 끝낸다.
        ///
        /// 이 표식 하나로 BattleManager의 클리어·전멸 판정만 비켜간다. 별도 상태를
        /// 만들지 않은 이유는 일시정지와 같다 — 상태를 늘리면 "프롤로그 → 설정 →
        /// 돌아오기" 같은 전이가 전부 새 칸이 된다. 실제 구조는 그냥 전투다.
        /// </summary>
        public bool IsPrologue;

        /// <summary>
        /// 이름은 Loc 표에서 가져온다 (`stage.1` …).
        /// 여기에 문자열을 두면 언어를 추가할 때마다 이 파일을 건드려야 한다.
        ///
        /// **이름이 없으면 "N판"으로 떨어진다.** 3~7장은 아직 틀만 있는 판이라
        /// 이름을 안 붙였다. Loc은 키를 못 찾으면 키 자체를 돌려주므로
        /// 그걸로 "없음"을 판별한다 — 화면에 `stage.13`이 뜨는 것보다 낫고,
        /// 이름이 붙는 대로 표에 한 줄만 넣으면 저절로 바뀐다.
        /// </summary>
        public string Name
        {
            get
            {
                string key = "stage." + Id;
                string name = Loc.T(key);
                return name == key ? Loc.F("stage.generic", Id) : name;
            }
        }

        public int HeroCount = 4;
        public float HeroHp = 100f;
        public float HeroDamage = 8f;

        /// <summary>스테이지별 마나 설정. 후반으로 갈수록 조여서 개입 빈도를 제한한다.</summary>
        public bool UnlimitedMana = true;
        public float MaxMana = 100f;
        public float ManaRegen = 9f;

        public int TotalEnemies
        {
            get
            {
                int n = 0;
                for (int i = 0; i < Waves.Length; i++) n += Waves[i].TotalCount;
                return n;
            }
        }
    }

    /// <summary>
    /// 스테이지 목록.
    ///
    /// 에셋이 아니라 코드로 정의한다. 이 프로젝트의 다른 데이터(룬 템플릿 등)와 같은 이유 —
    /// 무엇이 왜 바뀌었는지가 git 로그에 남고, 숫자 하나 고치는 데 에디터를 안 켜도 된다.
    ///
    /// 난이도 곡선의 의도:
    ///  1~4   각 룬을 하나씩 쓸 상황을 만들어 자연스럽게 익히게 한다
    ///  5~8   조합을 요구한다 (모아서 광역, 막고 회복)
    ///  9~12  마나를 조여서 "언제 쓸까"를 판단하게 만든다
    /// </summary>
    public static class StageDatabase
    {
        private static List<StageDef> _all;

        public static List<StageDef> All
        {
            get
            {
                if (_all == null) _all = Build();
                return _all;
            }
        }

        private static StageDef _prologue;

        /// <summary>
        /// 프롤로그. **`All`에 넣지 않는다** — 목록에 뜨면 안 되고, 별을 세는
        /// 모든 계산(잉크 단계·장착 칸)이 판 하나만큼 어긋난다.
        ///
        /// 수치는 밸런스가 아니라 **그림**이다. 넷 앞에 열둘을 세우는 건
        /// "이건 못 이긴다"가 한눈에 읽혀야 하기 때문이고, 아군 체력을 크게 준 건
        /// 대사가 끝나기 전에 누가 쓰러지면 안 되기 때문이다. 아군 공격력을
        /// 낮게 둔 것도 같은 이유 — 이 판은 병사들이 이기면 안 된다.
        /// 시뮬레이터 대상이 아니므로 read_stages.py에도 잡히지 않는다.
        /// </summary>
        public static StageDef Prologue
        {
            get
            {
                if (_prologue == null)
                {
                    _prologue = new StageDef
                    {
                        Id = 0, Chapter = 1, IsPrologue = true,
                        HeroCount = 4,
                        HeroHp = 400f,
                        HeroDamage = 3f,
                        Waves = new[]
                        {
                            new WaveDef(new WaveGroup(UnitKind.Skeleton, 10),
                                        new WaveGroup(UnitKind.Bonelord, 2)),
                        },
                    };
                }
                return _prologue;
            }
        }

        public static StageDef Get(int id)
        {
            var list = All;
            for (int i = 0; i < list.Count; i++)
                if (list[i].Id == id) return list[i];
            return null;
        }

        public static StageDef Next(int id)
        {
            var list = All;
            for (int i = 0; i < list.Count; i++)
                if (list[i].Id == id) return i + 1 < list.Count ? list[i + 1] : null;
            return null;
        }

        /// <summary>그 장의 판들. 장 선택 화면이 쓴다.</summary>
        public static List<StageDef> StagesIn(int chapter)
        {
            var list = new List<StageDef>();
            var all = All;
            for (int i = 0; i < all.Count; i++)
                if (all[i].Chapter == chapter) list.Add(all[i]);
            return list;
        }

        /// <summary>
        /// 그 장이 열렸는가. **첫 판이 열렸으면 장이 열린 것이다** —
        /// 판 해금이 이미 "직전 판을 깼는가"라 장 단위 규칙을 따로 둘 이유가 없다.
        /// </summary>
        public static bool IsChapterUnlocked(int chapter)
        {
            var list = StagesIn(chapter);
            return list.Count > 0 && StageProgress.IsUnlocked(list[0].Id);
        }

        public static int ChapterCount
        {
            get
            {
                int max = 1;
                var list = All;
                for (int i = 0; i < list.Count; i++)
                    if (list[i].Chapter > max) max = list[i].Chapter;
                return max;
            }
        }

        private static List<StageDef> Build()
        {
            var s = new List<StageDef>();
            // ── 1장: 초원 ────────────────────────────────────────

            // 1판 — 첫 판. 자동으로도 이긴다 — 그리는 법을 배우는 자리라 실패로 시작하면 안 된다.
            s.Add(new StageDef
            {
                Id = 1, Chapter = 1,
                HeroCount = 4,
                Waves = new[]
                {
                    new WaveDef(new WaveGroup(UnitKind.Orc, 3)),
                    new WaveDef(new WaveGroup(UnitKind.Orc, 4)),
                },
            });

            // 2판 — 자동으로 이기되 한 명쯤 잃는다. **별 하나가 개입에 달린 첫 판.**
            s.Add(new StageDef
            {
                Id = 2, Chapter = 1,
                HeroCount = 4,
                Waves = new[]
                {
                    new WaveDef(new WaveGroup(UnitKind.Skeleton, 5)),
                    new WaveDef(new WaveGroup(UnitKind.Skeleton, 7)),
                },
            });

            // 3판 — 여기서부터 자동으로는 진다. 본로드는 단단해서 화력을 몰아줘야 한다.
            s.Add(new StageDef
            {
                Id = 3, Chapter = 1,
                HeroCount = 4,
                Waves = new[]
                {
                    new WaveDef(new WaveGroup(UnitKind.Orc, 3)),
                    new WaveDef(new WaveGroup(UnitKind.Bonelord, 2), new WaveGroup(UnitKind.Orc, 2)),
                },
            });

            // 4판 — 빠른 적. 붙기 전에 처리하거나 막아야 한다.
            s.Add(new StageDef
            {
                Id = 4, Chapter = 1,
                HeroCount = 4,
                Waves = new[]
                {
                    new WaveDef(new WaveGroup(UnitKind.Vampire, 3)),
                    new WaveDef(new WaveGroup(UnitKind.Vampire, 3), new WaveGroup(UnitKind.Skeleton, 4)),
                },
            });

            // 5판 — **망령이 처음 나온다.** 한 마리만 — 아군이 때려도 안 죽는다는 규칙 하나만
                // 배우는 판이라 다른 문제를 겹치지 않는다.
            s.Add(new StageDef
            {
                Id = 5, Chapter = 1,
                HeroCount = 5,
                Waves = new[]
                {
                    new WaveDef(new WaveGroup(UnitKind.Skeleton, 6), new WaveGroup(UnitKind.Orc, 2)),
                    new WaveDef(new WaveGroup(UnitKind.Wraith, 1), new WaveGroup(UnitKind.Orc, 3)),
                },
            });

            // 6판 — 1장 마무리. 단단한 것과 손댈 수 없는 것이 같이 온다.
            s.Add(new StageDef
            {
                Id = 6, Chapter = 1,
                HeroCount = 5,
                Waves = new[]
                {
                    new WaveDef(new WaveGroup(UnitKind.Bonelord, 3)),
                    new WaveDef(new WaveGroup(UnitKind.Bonelord, 2, 1.15f, 1.10f), new WaveGroup(UnitKind.Wraith, 1), new WaveGroup(UnitKind.Vampire, 3)),
                },
            });

            // ── 2장: 마나가 조여든다 ─────────────────────────────
            // 이 게임의 원래 설계 — "마나가 없으면 지켜볼 수밖에 없다"가 여기서 작동한다.

            // 7판 — **마나가 유한해지는 첫 판.** 적은 오히려 덜었다 — 새 제약 하나만 배우면 된다.
            s.Add(new StageDef
            {
                Id = 7, Chapter = 2,
                HeroCount = 5,
                UnlimitedMana = false, MaxMana = 120f, ManaRegen = 11f,
                Waves = new[]
                {
                    new WaveDef(new WaveGroup(UnitKind.Skeleton, 8)),
                    // **거미가 처음 나온다.** 마나가 유한해진 판이라 "지금 광역을
                    // 터뜨리면 수가 늘어난다"가 곧바로 손해로 느껴진다.
                    new WaveDef(new WaveGroup(UnitKind.Orc, 4), new WaveGroup(UnitKind.Spider, 3)),
                },
            });

            // 8판 — 망령 둘. 마나가 유한한 상태에서 반드시 써야 하는 곳이 생긴다.
            s.Add(new StageDef
            {
                Id = 8, Chapter = 2,
                HeroCount = 5,
                UnlimitedMana = false, MaxMana = 110f, ManaRegen = 10f,
                Waves = new[]
                {
                    new WaveDef(new WaveGroup(UnitKind.Skeleton, 9)),
                    new WaveDef(new WaveGroup(UnitKind.Wraith, 2), new WaveGroup(UnitKind.Vampire, 3)),
                    // **놀이 처음 나온다.** 사거리가 아군의 두 배라 전선 밖에서 깎는다.
                    // 앞줄이 본로드라 아군이 붙어 있는 시간이 길고, 그동안 계속 맞는다.
                    new WaveDef(new WaveGroup(UnitKind.Bonelord, 2), new WaveGroup(UnitKind.Gnoll, 2), new WaveGroup(UnitKind.Orc, 3)),
                },
            });

            // 9판 — 정예. 물량이 아니라 개체가 세다.
            s.Add(new StageDef
            {
                Id = 9, Chapter = 2,
                HeroCount = 5,
                UnlimitedMana = false, MaxMana = 100f, ManaRegen = 9f,
                Waves = new[]
                {
                    new WaveDef(new WaveGroup(UnitKind.Vampire, 5, 1.15f, 1.10f)),
                    // **주술사가 처음 나온다.** 하나만, 그리고 망령은 뺐다 —
                    // 새 규칙 둘을 한 물결에서 배우게 하면 어느 쪽 때문에 밀렸는지
                    // 알 수가 없다. 여기서는 "체력이 도로 찬다"만 읽으면 된다.
                    new WaveDef(new WaveGroup(UnitKind.Bonelord, 2, 1.15f, 1.05f), new WaveGroup(UnitKind.Shaman, 1), new WaveGroup(UnitKind.Vampire, 3)),
                },
            });

            // 10판 — 세 물결. 마지막에 단단한 것과 망령이 겹친다.
            s.Add(new StageDef
            {
                Id = 10, Chapter = 2,
                HeroCount = 5,
                UnlimitedMana = false, MaxMana = 100f, ManaRegen = 9f,
                Waves = new[]
                {
                    new WaveDef(new WaveGroup(UnitKind.Skeleton, 11)),
                    new WaveDef(new WaveGroup(UnitKind.Orc, 5, 1.15f), new WaveGroup(UnitKind.Spider, 4)),
                    // 단단한 앞줄에 주술사가 붙으면 회복이 처음으로 아프다.
                    // 본로드를 깎는 동안 계속 되돌아오므로 뒤부터 쳐야 한다.
                    new WaveDef(new WaveGroup(UnitKind.Bonelord, 3, 1.10f, 1.10f), new WaveGroup(UnitKind.Shaman, 1), new WaveGroup(UnitKind.Wraith, 2)),
                },
            });

            // 11판 — 망령 셋. 마지막 물결은 사실상 룬으로만 정리해야 한다.
            s.Add(new StageDef
            {
                Id = 11, Chapter = 2,
                HeroCount = 5,
                UnlimitedMana = false, MaxMana = 100f, ManaRegen = 9f,
                Waves = new[]
                {
                    new WaveDef(new WaveGroup(UnitKind.Vampire, 5, 1.10f)),
                    new WaveDef(new WaveGroup(UnitKind.Bonelord, 2, 1.20f, 1.10f), new WaveGroup(UnitKind.Gnoll, 3), new WaveGroup(UnitKind.Skeleton, 5)),
                    new WaveDef(new WaveGroup(UnitKind.Wraith, 3), new WaveGroup(UnitKind.Vampire, 4, 1.15f, 1.10f)),
                },
            });

            // 12판 — 최후. 실측 — 이기는 데 60, 3별에 110.
            s.Add(new StageDef
            {
                Id = 12, Chapter = 2,
                HeroCount = 5,
                UnlimitedMana = false, MaxMana = 100f, ManaRegen = 9f,
                Waves = new[]
                {
                    // 첫 물결부터 고르게 만든다. 해골만 있으면 별똥별 한 방으로
                    // 끝나는데, 거미가 섞이면 그 한 방이 오히려 수를 늘린다.
                    new WaveDef(new WaveGroup(UnitKind.Skeleton, 7), new WaveGroup(UnitKind.Spider, 4)),
                    new WaveDef(new WaveGroup(UnitKind.Vampire, 5, 1.10f, 1.05f), new WaveGroup(UnitKind.Orc, 4)),
                    // 주술사 둘 — 하나를 끊어도 다른 하나가 남는다. 순서를 정해야 한다.
                    new WaveDef(new WaveGroup(UnitKind.Bonelord, 2, 1.15f, 1.10f), new WaveGroup(UnitKind.Wraith, 3), new WaveGroup(UnitKind.Shaman, 2)),
                    // **보스.** 혼자 나온다 — 다른 적을 섞으면 예고된 강타를
                    // 볼 여유가 없어지고, 그러면 보스가 그냥 체력 많은 적이 된다.
                    new WaveDef(new WaveGroup(UnitKind.Troll, 1)),
                },
            });


            // ── 3장: 서리 ──────────────────────────────

            // 13판 — 느려진 것을 체감하는 판. 구성은 평범하게 두고 규칙만 배우게 한다.
            s.Add(new StageDef
            {
                Id = 13, Chapter = 3,
                HeroCount = 5,
                UnlimitedMana = false, MaxMana = 100f, ManaRegen = 9f,
                Waves = new[]
                {
                    new WaveDef(new WaveGroup(UnitKind.Orc, 8)),
                    new WaveDef(new WaveGroup(UnitKind.Vampire, 5), new WaveGroup(UnitKind.Skeleton, 8)),
                },
            });

            // 14판 — 빠른 적. 느린 아군은 따라잡지 못한다 — 붙기 전에 끊어야 한다.
            s.Add(new StageDef
            {
                Id = 14, Chapter = 3,
                HeroCount = 5,
                UnlimitedMana = false, MaxMana = 100f, ManaRegen = 9f,
                Waves = new[]
                {
                    new WaveDef(new WaveGroup(UnitKind.Vampire, 8)),
                    new WaveDef(new WaveGroup(UnitKind.Vampire, 5, 1.10f, 1.00f), new WaveGroup(UnitKind.Gnoll, 4)),
                },
            });

            // 15판 — 단단한 것. 느려진 아군에게 시간은 그대로 피해다.
            s.Add(new StageDef
            {
                Id = 15, Chapter = 3,
                HeroCount = 5,
                UnlimitedMana = false, MaxMana = 100f, ManaRegen = 9f,
                Waves = new[]
                {
                    new WaveDef(new WaveGroup(UnitKind.Bonelord, 4)),
                    new WaveDef(new WaveGroup(UnitKind.Bonelord, 3, 1.10f, 1.05f), new WaveGroup(UnitKind.Orc, 7)),
                },
            });

            // 16판 — 물량. 한 대씩 느리게 치는 아군으로는 못 감당한다.
            s.Add(new StageDef
            {
                Id = 16, Chapter = 3,
                HeroCount = 5,
                UnlimitedMana = false, MaxMana = 100f, ManaRegen = 9f,
                Waves = new[]
                {
                    new WaveDef(new WaveGroup(UnitKind.Skeleton, 13)),
                    new WaveDef(new WaveGroup(UnitKind.Gnoll, 5), new WaveGroup(UnitKind.Skeleton, 8)),
                },
            });

            // 17판 — 망령 복귀. 평타가 안 통하는데 그 평타마저 느리다.
            s.Add(new StageDef
            {
                Id = 17, Chapter = 3,
                HeroCount = 5,
                UnlimitedMana = false, MaxMana = 100f, ManaRegen = 9f,
                Waves = new[]
                {
                    new WaveDef(new WaveGroup(UnitKind.Wraith, 3), new WaveGroup(UnitKind.Orc, 7)),
                    new WaveDef(new WaveGroup(UnitKind.Bonelord, 2, 1.15f, 1.00f), new WaveGroup(UnitKind.Vampire, 6)),
                },
            });

            // 18판 — 3장 마무리. 세 물결.
            s.Add(new StageDef
            {
                Id = 18, Chapter = 3,
                HeroCount = 5,
                UnlimitedMana = false, MaxMana = 100f, ManaRegen = 9f,
                Waves = new[]
                {
                    new WaveDef(new WaveGroup(UnitKind.Skeleton, 10), new WaveGroup(UnitKind.Spider, 4)),
                    new WaveDef(new WaveGroup(UnitKind.Vampire, 7), new WaveGroup(UnitKind.Gnoll, 5)),
                    new WaveDef(new WaveGroup(UnitKind.Bonelord, 3, 1.15f, 1.10f), new WaveGroup(UnitKind.Wraith, 2), new WaveGroup(UnitKind.Shaman, 1)),
                },
            });

            // ── 4장: 안개 ──────────────────────────────

            // 19판 — 놀이 주인공. 사거리 밖에서 때리는데 어디 있는지 잘 안 보인다.
            s.Add(new StageDef
            {
                Id = 19, Chapter = 4,
                HeroCount = 5,
                UnlimitedMana = false, MaxMana = 100f, ManaRegen = 9f,
                Waves = new[]
                {
                    new WaveDef(new WaveGroup(UnitKind.Gnoll, 5), new WaveGroup(UnitKind.Orc, 6)),
                    new WaveDef(new WaveGroup(UnitKind.Gnoll, 5), new WaveGroup(UnitKind.Skeleton, 9)),
                },
            });

            // 20판 — 주술사. 뒤에 서는 적이라 안개와 정확히 겹친다.
            s.Add(new StageDef
            {
                Id = 20, Chapter = 4,
                HeroCount = 5,
                UnlimitedMana = false, MaxMana = 100f, ManaRegen = 9f,
                Waves = new[]
                {
                    new WaveDef(new WaveGroup(UnitKind.Shaman, 2), new WaveGroup(UnitKind.Bonelord, 4)),
                    new WaveDef(new WaveGroup(UnitKind.Shaman, 2), new WaveGroup(UnitKind.Orc, 8)),
                },
            });

            // 21판 — 빠른 적 + 원거리. 앞을 보면 뒤가, 뒤를 보면 앞이 온다.
            s.Add(new StageDef
            {
                Id = 21, Chapter = 4,
                HeroCount = 5,
                UnlimitedMana = false, MaxMana = 100f, ManaRegen = 9f,
                Waves = new[]
                {
                    new WaveDef(new WaveGroup(UnitKind.Vampire, 9)),
                    new WaveDef(new WaveGroup(UnitKind.Gnoll, 7), new WaveGroup(UnitKind.Vampire, 4, 1.10f, 1.00f)),
                },
            });

            // 22판 — 물량 뒤에 원거리. 앞줄을 치우기 전에는 뒤가 안 보인다.
            s.Add(new StageDef
            {
                Id = 22, Chapter = 4,
                HeroCount = 5,
                UnlimitedMana = false, MaxMana = 100f, ManaRegen = 9f,
                Waves = new[]
                {
                    new WaveDef(new WaveGroup(UnitKind.Skeleton, 12), new WaveGroup(UnitKind.Spider, 4)),
                    new WaveDef(new WaveGroup(UnitKind.Gnoll, 6), new WaveGroup(UnitKind.Bonelord, 4, 1.10f, 1.00f)),
                },
            });

            // 23판 — 망령과 주술사가 같이. 둘 다 뒤에 있고 둘 다 룬으로만 풀린다.
            s.Add(new StageDef
            {
                Id = 23, Chapter = 4,
                HeroCount = 5,
                UnlimitedMana = false, MaxMana = 100f, ManaRegen = 9f,
                Waves = new[]
                {
                    new WaveDef(new WaveGroup(UnitKind.Wraith, 3), new WaveGroup(UnitKind.Gnoll, 5)),
                    new WaveDef(new WaveGroup(UnitKind.Shaman, 2), new WaveGroup(UnitKind.Bonelord, 4, 1.15f, 1.05f)),
                },
            });

            // 24판 — 4장 마무리.
            s.Add(new StageDef
            {
                Id = 24, Chapter = 4,
                HeroCount = 5,
                UnlimitedMana = false, MaxMana = 100f, ManaRegen = 9f,
                Waves = new[]
                {
                    new WaveDef(new WaveGroup(UnitKind.Gnoll, 6), new WaveGroup(UnitKind.Skeleton, 10)),
                    new WaveDef(new WaveGroup(UnitKind.Vampire, 7, 1.10f, 1.00f), new WaveGroup(UnitKind.Spider, 5)),
                    new WaveDef(new WaveGroup(UnitKind.Shaman, 2), new WaveGroup(UnitKind.Bonelord, 3, 1.20f, 1.10f), new WaveGroup(UnitKind.Wraith, 2)),
                },
            });

            // ── 5장: 부패 ──────────────────────────────

            // 25판 — 해골 떼. 광역으로 쓸어담던 습관이 처음으로 벌을 받는다.
            s.Add(new StageDef
            {
                Id = 25, Chapter = 5,
                HeroCount = 5,
                UnlimitedMana = false, MaxMana = 100f, ManaRegen = 9f,
                Waves = new[]
                {
                    new WaveDef(new WaveGroup(UnitKind.Skeleton, 12)),
                    new WaveDef(new WaveGroup(UnitKind.Skeleton, 9), new WaveGroup(UnitKind.Spider, 4)),
                },
            });

            // 26판 — 거미. 죽으면 새끼가 나오고 그 새끼도 죽을 때 터진다.
            s.Add(new StageDef
            {
                Id = 26, Chapter = 5,
                HeroCount = 5,
                UnlimitedMana = false, MaxMana = 100f, ManaRegen = 9f,
                Waves = new[]
                {
                    new WaveDef(new WaveGroup(UnitKind.Spider, 6)),
                    new WaveDef(new WaveGroup(UnitKind.Spider, 5), new WaveGroup(UnitKind.Orc, 6)),
                },
            });

            // 27판 — 빠른 적이 섞인 무리. 붙은 채로 죽으면 그 자리가 아군 자리다.
            s.Add(new StageDef
            {
                Id = 27, Chapter = 5,
                HeroCount = 5,
                UnlimitedMana = false, MaxMana = 100f, ManaRegen = 9f,
                Waves = new[]
                {
                    new WaveDef(new WaveGroup(UnitKind.Skeleton, 10), new WaveGroup(UnitKind.Spider, 4)),
                    new WaveDef(new WaveGroup(UnitKind.Vampire, 6, 1.10f, 1.00f), new WaveGroup(UnitKind.Skeleton, 8)),
                },
            });

            // 28판 — 단단한 것과 쪼개지는 것이 같이. 어디서 터뜨릴지가 전부다.
            s.Add(new StageDef
            {
                Id = 28, Chapter = 5,
                HeroCount = 5,
                UnlimitedMana = false, MaxMana = 100f, ManaRegen = 9f,
                Waves = new[]
                {
                    new WaveDef(new WaveGroup(UnitKind.Orc, 8)),
                    new WaveDef(new WaveGroup(UnitKind.Bonelord, 3, 1.15f, 1.00f), new WaveGroup(UnitKind.Spider, 5), new WaveGroup(UnitKind.Gnoll, 3)),
                },
            });

            // 29판 — 망령 복귀. 룬으로 끊어야 하는데 그 룬이 아군을 다치게 한다.
            s.Add(new StageDef
            {
                Id = 29, Chapter = 5,
                HeroCount = 5,
                UnlimitedMana = false, MaxMana = 100f, ManaRegen = 9f,
                Waves = new[]
                {
                    new WaveDef(new WaveGroup(UnitKind.Skeleton, 11), new WaveGroup(UnitKind.Spider, 4)),
                    new WaveDef(new WaveGroup(UnitKind.Wraith, 2), new WaveGroup(UnitKind.Vampire, 6, 1.15f, 1.05f)),
                },
            });

            // 30판 — 5장 마무리.
            s.Add(new StageDef
            {
                Id = 30, Chapter = 5,
                HeroCount = 5,
                UnlimitedMana = false, MaxMana = 100f, ManaRegen = 9f,
                Waves = new[]
                {
                    new WaveDef(new WaveGroup(UnitKind.Spider, 6), new WaveGroup(UnitKind.Skeleton, 8)),
                    new WaveDef(new WaveGroup(UnitKind.Gnoll, 4), new WaveGroup(UnitKind.Vampire, 6, 1.15f, 1.00f)),
                    new WaveDef(new WaveGroup(UnitKind.Bonelord, 3, 1.20f, 1.10f), new WaveGroup(UnitKind.Shaman, 2), new WaveGroup(UnitKind.Wraith, 2)),
                },
            });

            // ── 6장: 봉인 ──────────────────────────────

            // 31판 — 봉인을 처음 겪는 판. 구성은 익숙하게 둔다 — 새 규칙 하나만 배우면 된다.
            s.Add(new StageDef
            {
                Id = 31, Chapter = 6,
                HeroCount = 5,
                UnlimitedMana = false, MaxMana = 100f, ManaRegen = 9f,
                Waves = new[]
                {
                    new WaveDef(new WaveGroup(UnitKind.Orc, 7), new WaveGroup(UnitKind.Skeleton, 6)),
                    new WaveDef(new WaveGroup(UnitKind.Bonelord, 3, 1.15f, 1.00f), new WaveGroup(UnitKind.Vampire, 5)),
                },
            });

            // 32판 — 망령 셋. 잠긴 룬이 무엇이든 남은 것으로 끊어야 한다.
            s.Add(new StageDef
            {
                Id = 32, Chapter = 6,
                HeroCount = 5,
                UnlimitedMana = false, MaxMana = 100f, ManaRegen = 9f,
                Waves = new[]
                {
                    new WaveDef(new WaveGroup(UnitKind.Wraith, 3), new WaveGroup(UnitKind.Orc, 5)),
                    new WaveDef(new WaveGroup(UnitKind.Bonelord, 3, 1.15f, 1.05f), new WaveGroup(UnitKind.Gnoll, 4)),
                },
            });

            // 33판 — 속도와 회복이 같이. 한 손이 묶인 채로 표적을 골라야 한다.
            s.Add(new StageDef
            {
                Id = 33, Chapter = 6,
                HeroCount = 5,
                UnlimitedMana = false, MaxMana = 100f, ManaRegen = 9f,
                Waves = new[]
                {
                    new WaveDef(new WaveGroup(UnitKind.Vampire, 8, 1.10f, 1.00f)),
                    new WaveDef(new WaveGroup(UnitKind.Shaman, 2), new WaveGroup(UnitKind.Skeleton, 10), new WaveGroup(UnitKind.Spider, 3)),
                },
            });

            // 34판 — 원거리와 단단한 것. 잠긴 룬에 따라 답이 달라지는 판.
            s.Add(new StageDef
            {
                Id = 34, Chapter = 6,
                HeroCount = 5,
                UnlimitedMana = false, MaxMana = 100f, ManaRegen = 9f,
                Waves = new[]
                {
                    new WaveDef(new WaveGroup(UnitKind.Gnoll, 6), new WaveGroup(UnitKind.Bonelord, 3, 1.15f, 1.00f)),
                    new WaveDef(new WaveGroup(UnitKind.Wraith, 2), new WaveGroup(UnitKind.Vampire, 6, 1.15f, 1.00f)),
                },
            });

            // 35판 — 물량 뒤에 회복. 광역이 잠기면 특히 길어진다.
            s.Add(new StageDef
            {
                Id = 35, Chapter = 6,
                HeroCount = 5,
                UnlimitedMana = false, MaxMana = 100f, ManaRegen = 9f,
                Waves = new[]
                {
                    new WaveDef(new WaveGroup(UnitKind.Skeleton, 13), new WaveGroup(UnitKind.Spider, 5)),
                    new WaveDef(new WaveGroup(UnitKind.Bonelord, 4, 1.20f, 1.10f), new WaveGroup(UnitKind.Shaman, 2)),
                },
            });

            // 36판 — 6장 마무리.
            s.Add(new StageDef
            {
                Id = 36, Chapter = 6,
                HeroCount = 5,
                UnlimitedMana = false, MaxMana = 100f, ManaRegen = 9f,
                Waves = new[]
                {
                    new WaveDef(new WaveGroup(UnitKind.Vampire, 6, 1.15f, 1.00f), new WaveGroup(UnitKind.Gnoll, 4)),
                    new WaveDef(new WaveGroup(UnitKind.Skeleton, 10), new WaveGroup(UnitKind.Spider, 5)),
                    new WaveDef(new WaveGroup(UnitKind.Bonelord, 3, 1.20f, 1.10f), new WaveGroup(UnitKind.Wraith, 2), new WaveGroup(UnitKind.Shaman, 2)),
                },
            });

            // ── 7장: 심연 ──────────────────────────────

            // 37판 — 잡을 것이 많다. 아끼지 말고 계속 쳐야 마나가 돈다는 걸 배우는 판.
            s.Add(new StageDef
            {
                Id = 37, Chapter = 7,
                HeroCount = 5,
                UnlimitedMana = false, MaxMana = 100f, ManaRegen = 9f,
                Waves = new[]
                {
                    new WaveDef(new WaveGroup(UnitKind.Skeleton, 10), new WaveGroup(UnitKind.Spider, 3)),
                    new WaveDef(new WaveGroup(UnitKind.Orc, 7), new WaveGroup(UnitKind.Vampire, 4, 1.10f, 1.00f)),
                },
            });

            // 38판 — 단단한 것이 섞인다. 잘 안 죽는 적 앞에서는 마나가 마른다.
            s.Add(new StageDef
            {
                Id = 38, Chapter = 7,
                HeroCount = 5,
                UnlimitedMana = false, MaxMana = 100f, ManaRegen = 9f,
                Waves = new[]
                {
                    new WaveDef(new WaveGroup(UnitKind.Skeleton, 10), new WaveGroup(UnitKind.Gnoll, 4)),
                    new WaveDef(new WaveGroup(UnitKind.Bonelord, 3, 1.20f, 1.10f), new WaveGroup(UnitKind.Vampire, 6, 1.15f, 1.00f)),
                },
            });

            // 39판 — 망령 셋. 평타로는 안 죽으니 마나를 써야 하는데, 그 마나를 잡아서 벌어야 한다.
            s.Add(new StageDef
            {
                Id = 39, Chapter = 7,
                HeroCount = 5,
                UnlimitedMana = false, MaxMana = 100f, ManaRegen = 9f,
                Waves = new[]
                {
                    new WaveDef(new WaveGroup(UnitKind.Vampire, 8, 1.15f, 1.00f)),
                    new WaveDef(new WaveGroup(UnitKind.Wraith, 3), new WaveGroup(UnitKind.Skeleton, 9), new WaveGroup(UnitKind.Spider, 4)),
                },
            });

            // 40판 — 세 물결. 회복하는 적 앞에서 마나 순환이 끊긴다.
            s.Add(new StageDef
            {
                Id = 40, Chapter = 7,
                HeroCount = 5,
                UnlimitedMana = false, MaxMana = 100f, ManaRegen = 9f,
                Waves = new[]
                {
                    new WaveDef(new WaveGroup(UnitKind.Skeleton, 12), new WaveGroup(UnitKind.Spider, 5)),
                    new WaveDef(new WaveGroup(UnitKind.Shaman, 1), new WaveGroup(UnitKind.Bonelord, 3, 1.20f, 1.10f)),
                    new WaveDef(new WaveGroup(UnitKind.Gnoll, 4), new WaveGroup(UnitKind.Vampire, 5, 1.15f, 1.05f)),
                },
            });

            // 41판 — 마지막 직전. 잡을 것과 안 죽는 것이 번갈아 온다.
            s.Add(new StageDef
            {
                Id = 41, Chapter = 7,
                HeroCount = 5,
                UnlimitedMana = false, MaxMana = 100f, ManaRegen = 9f,
                Waves = new[]
                {
                    new WaveDef(new WaveGroup(UnitKind.Skeleton, 11), new WaveGroup(UnitKind.Spider, 5)),
                    new WaveDef(new WaveGroup(UnitKind.Wraith, 2), new WaveGroup(UnitKind.Gnoll, 4)),
                    new WaveDef(new WaveGroup(UnitKind.Bonelord, 3, 1.25f, 1.15f), new WaveGroup(UnitKind.Shaman, 1), new WaveGroup(UnitKind.Vampire, 5, 1.15f, 1.00f)),
                },
            });

            // 42판 — **최후.** 보스는 혼자 나온다 — 잡을 것이 하나뿐이라 마나가 안 돈다. 심연의 규칙이 마지막 물결에서 가장 아프게 걸린다.
            s.Add(new StageDef
            {
                Id = 42, Chapter = 7,
                HeroCount = 5,
                UnlimitedMana = false, MaxMana = 100f, ManaRegen = 9f,
                Waves = new[]
                {
                    new WaveDef(new WaveGroup(UnitKind.Skeleton, 12), new WaveGroup(UnitKind.Spider, 5)),
                    new WaveDef(new WaveGroup(UnitKind.Vampire, 7, 1.15f, 1.10f), new WaveGroup(UnitKind.Gnoll, 5)),
                    new WaveDef(new WaveGroup(UnitKind.Bonelord, 4, 1.25f, 1.15f), new WaveGroup(UnitKind.Wraith, 3), new WaveGroup(UnitKind.Shaman, 2)),
                    new WaveDef(new WaveGroup(UnitKind.Troll, 1)),
                },
            });

            return s;
        }

        /// <summary>장 하나에 들어가는 판 수. 1·2장이 여섯씩이라 그대로 맞춘다.</summary>
        public const int StagesPerChapter = 6;

        /// <summary>마지막 장 번호. 3~7장은 아직 틀뿐이다.</summary>
        public const int LastChapter = 7;

    }
}
