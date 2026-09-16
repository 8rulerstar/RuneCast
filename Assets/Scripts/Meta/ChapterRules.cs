using UnityEngine;
using RuneCast.Gesture;

namespace RuneCast.Meta
{
    /// <summary>
    /// 장마다 하나씩 걸리는 규칙.
    ///
    /// **수치만 키운 장은 장이 아니라 물결이다.** 3~7장을 처음 넣을 때는 적 수만
    /// 늘린 껍데기였는데, 그러면 "웨이브 번호만 바뀐 판"이 서른 개 생긴다 —
    /// 이 프로젝트가 적을 만들 때부터 경계해 온 바로 그것이다.
    ///
    /// 그래서 장마다 **다른 질문**을 하나씩 물린다. 판이 아니라 장에 거는 이유는
    /// 여섯 판을 같이 겪어야 규칙이 손에 붙기 때문이다. 판마다 바뀌면 배우기 전에
    /// 지나간다.
    ///
    /// 1·2장의 규칙은 이미 게임에 있다(망령이라는 적, StageDef의 마나 제한).
    /// 여기서는 이름만 들고 있고 실제 동작은 원래 자리에 그대로 둔다 —
    /// 잘 도는 것을 이 표로 옮기면 두 곳이 같은 일을 하게 된다.
    /// </summary>
    public enum ChapterRule
    {
        /// <summary>1장 — 망령. 아군 평타가 거의 안 통하는 적이 나온다(적 쪽에 구현).</summary>
        Wraith = 0,

        /// <summary>2장 — 마나가 유한해진다(StageDef가 판마다 지정).</summary>
        LimitedMana = 1,

        /// <summary>
        /// 3장 — 서리. **아군의 공격 주기가 느려진다.**
        ///
        /// 위력이 아니라 속도를 깎는다. 체력을 깎으면 "더 세게 쳐라"가 되지만
        /// 속도를 깎으면 **고양(Empower)이 처음으로 값을 한다** — 지금까지
        /// 아무도 안 쓰던 룬이다.
        /// </summary>
        Frost = 2,

        /// <summary>
        /// 4장 — 안개. **멀리 있는 적이 흐릿하게 보인다.**
        ///
        /// 이 게임은 전장을 읽고 무엇을 그릴지 정하는 게임이라, 정보를 줄이는
        /// 것이 가장 직접적인 압박이다. 피해를 늘리는 것과 달리 **강화로
        /// 못 이기는 종류**이기도 하다.
        /// </summary>
        Fog = 3,

        /// <summary>
        /// 5장 — 역병. **적이 죽을 때 가까운 아군이 조금 다친다.**
        ///
        /// 지금까지 광역은 언제나 이득이었다. 여기서는 뭉친 적을 한 번에
        /// 쓸어담는 것이 곧 아군 피해가 된다 — 별이 생존 수라서 그대로 손해다.
        /// 소용돌이로 모으는 습관이 처음으로 벌을 받는다.
        /// </summary>
        Plague = 4,

        /// <summary>
        /// 6장 — 봉인. **판마다 룬 하나가 잠긴다.**
        ///
        /// 사람은 손에 익은 두세 개만 쓴다. 그게 잠기면 나머지를 꺼내야 하고,
        /// 그러면 아홉 개를 만든 값을 처음으로 한다. 무작위지만 판 번호로
        /// 정해지므로 **다시 해도 같은 룬이 잠긴다** — 운이 아니라 문제여야 한다.
        /// </summary>
        Sealing = 5,

        /// <summary>
        /// 7장 — 심연. **마나가 시간으로 안 차고 처치로 찬다.**
        ///
        /// 2장이 "아껴 써라"였다면 여기는 그 반대다. 아끼면 말라 죽는다.
        /// 같은 자원으로 정반대의 태도를 요구하는 것이 마지막 장에 맞는다.
        /// </summary>
        Abyss = 6,
    }

    public static class ChapterRules
    {
        /// <summary>지금 판에 걸린 규칙. 전투 밖에서는 1장 것으로 둔다.</summary>
        public static ChapterRule Active { get; private set; }

        /// <summary>지금 잠긴 룬. 봉인 장이 아니면 None.</summary>
        public static RuneType SealedRune { get; private set; }

        public static ChapterRule Of(int chapter)
        {
            int i = Mathf.Clamp(chapter - 1, 0, 6);
            return (ChapterRule)i;
        }

        /// <summary>판이 시작될 때 BattleManager가 부른다.</summary>
        public static void Begin(StageDef stage)
        {
            if (stage == null) { Active = ChapterRule.Wraith; SealedRune = RuneType.None; return; }

            Active = stage.IsPrologue ? ChapterRule.Wraith : Of(stage.Chapter);
            SealedRune = Active == ChapterRule.Sealing ? PickSealed(stage.Id) : RuneType.None;
        }

        /// <summary>
        /// 잠글 룬을 고른다. **판 번호로 정한다 — 무작위가 아니다.**
        ///
        /// 무작위면 다시 할 때마다 달라져서, 진 이유가 내 잘못인지 운인지
        /// 구분이 안 된다. 판마다 고정이면 "이 판은 화살이 없는 판"이 되고
        /// 그건 풀 수 있는 문제다.
        ///
        /// 소생은 절대 안 잠근다 — 아군이 죽은 판에서 유일한 되돌리기라,
        /// 그게 잠기면 규칙이 아니라 사고가 된다.
        /// </summary>
        private static RuneType PickSealed(int stageId)
        {
            var runes = GlyphTable.Runes;
            int n = 0;
            for (int i = 0; i < runes.Length; i++) if (runes[i] != RuneType.Revive) n++;
            if (n == 0) return RuneType.None;

            int pick = stageId % n;
            for (int i = 0; i < runes.Length; i++)
            {
                if (runes[i] == RuneType.Revive) continue;
                if (pick-- == 0) return runes[i];
            }
            return RuneType.None;
        }

        // ── 각 규칙이 내놓는 값 ──────────────────────────────────
        //
        // 규칙마다 "얼마나"를 여기 한 곳에 모은다. 흩어 놓으면 균형을 볼 때
        // 파일을 일곱 개 열어야 한다.

        /// <summary>서리 — 아군 공격 주기 배수. 1보다 크면 느리다.</summary>
        public static float HeroIntervalScale
        {
            get { return Active == ChapterRule.Frost ? 1.30f : 1f; }
        }

        /// <summary>안개 — 이 거리보다 먼 적은 흐려진다. 0이면 안개 없음.</summary>
        public static float FogDistance
        {
            get { return Active == ChapterRule.Fog ? 4.2f : 0f; }
        }

        /// <summary>역병 — 적이 죽을 때 이 반경 안 아군이 받는 피해.</summary>
        public static float PlagueRadius { get { return 1.6f; } }
        public static float PlagueDamage
        {
            get { return Active == ChapterRule.Plague ? 6f : 0f; }
        }

        /// <summary>
        /// 심연 — 시간 회복 배수. 처치당 회복은 아래 ManaPerKill.
        ///
        /// **처음 0.35로 잡았다가 시뮬레이터로 0.50까지 올렸다.** 0.35에서는
        /// 강화 없이 7장 전패(승률 0%)였다. 새 규칙이 어려운 게 아니라
        /// 그냥 못 지나가는 벽이었다.
        ///
        /// 0.50에서는 강화 없이 승률 67%에 평균 1.3명 생존 — 이기긴 하는데
        /// 별은 하나다. 강화 1.6이면 전원 생존 3별.
        /// **여기서 "강화해야 3별"이 돌아온다** — 물결 간격을 3.4초로 늘리면서
        /// 전 판이 강화 없이 3별이 되어 사라졌던 대장장이의 존재 이유다.
        ///
        /// 0.65까지 올리면 강화 없이도 4.2명이 살아남아 다시 무의미해진다.
        /// 훑어본 값: 0.35(전패) · 0.50(67%) · 0.65(100%/4.2명) · 0.80(100%/4.8명).
        /// </summary>
        public static float ManaRegenScale
        {
            get { return Active == ChapterRule.Abyss ? 0.50f : 1f; }
        }
        public static float ManaPerKill
        {
            get { return Active == ChapterRule.Abyss ? 7f : 0f; }
        }

        public static bool IsSealed(RuneType t)
        {
            return SealedRune != RuneType.None && t == SealedRune;
        }

        /// <summary>화면에 띄울 규칙 이름·설명 키.</summary>
        public static string NameKey { get { return "rule." + Active.ToString().ToLowerInvariant(); } }
        public static string DescKey { get { return NameKey + ".sub"; } }
    }
}
