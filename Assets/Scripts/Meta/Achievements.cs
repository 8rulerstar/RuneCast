using System;
using System.IO;
using UnityEngine;

namespace RuneCast.Meta
{
    /// <summary>업적. 값은 저장 파일에 비트로 들어가므로 **번호를 바꾸면 안 된다.**</summary>
    public enum Achievement
    {
        // ── 온보딩 — 처음 며칠을 열어 준다 ──
        FirstBlood = 0,     // 룬을 처음 발동
        FirstClear = 8,     // 첫 스테이지 클리어

        // ── 숙련 — 잘 그릴 이유 ──
        Perfectionist = 1,  // PERFECT 30회 (누적)
        SharpEye = 9,       // 한 판에서 PERFECT 5회
        Untouched = 3,      // 아군 하나도 안 잃고 클리어

        // ── 발견 — 있는 줄도 모르고 지나칠 것들을 가리킨다 ──
        Scholar = 7,        // 한 판에서 룬 9종 모두 사용
        Inscriber = 10,     // 커스텀 룬을 등록해 본다
        Rally = 11,         // 고양 한 번으로 아군 4명 이상
        Reviver = 6,        // 소생으로 10명 살리기
        WraithSlayer = 2,   // 망령 20마리 처치

        // ── 장기 ──
        Slayer = 12,        // 룬으로 200마리 처치
        Caster = 13,        // 룬 500회 시전
        Finisher = 14,      // 마지막 스테이지 클리어
        Conqueror = 5,      // 모든 스테이지 3별

        // ── 순환형 — 이미 쓴 파편을 돌려주는 꼴이라 배지 수준으로만 ──
        Master = 15,        // 룬 하나를 만렙까지
        Collector = 4,      // 문양 40개 보유
    }

    [Serializable]
    public class AchieveFile
    {
        public int done;            // 달성 비트마스크
        public int claimed;         // 보상 받은 비트마스크

        public int perfects;
        public int wraiths;
        public int revived;
        public int kills;
        public int casts;
    }

    /// <summary>
    /// 업적.
    ///
    /// **왜 넣나:** 이 게임의 목표가 "스테이지 12개 클리어"뿐이면 잘 그릴 이유가 없다.
    /// 별은 아군을 지키면 받으므로 안전하게만 하면 되고, 그러면 **PERFECT를 노릴
    /// 이유도, 안 쓰던 룬을 써볼 이유도 없다.** 업적은 그 이유를 만든다.
    ///
    /// 보상은 파편과 스킨이다. 성능이 붙는 보상은 안 준다 —
    /// 그러면 업적이 "해도 되는 것"이 아니라 "해야 하는 것"이 된다.
    ///
    /// 진행 기록(PERFECT 횟수 등)을 여기 같이 둔 이유: 조건과 세는 곳이 떨어져 있으면
    /// 반드시 어긋난다. 이 파일 하나만 보면 무엇을 세고 언제 달성인지 다 보인다.
    /// </summary>
    public static class Achievements
    {
        public const int PerfectGoal = 30;
        public const int WraithGoal = 20;
        public const int GlyphGoal = 40;
        public const int ReviveGoal = 10;
        public const int RunPerfectGoal = 5;
        public const int RallyGoal = 4;
        public const int KillGoal = 200;
        public const int CastGoal = 500;

        /// <summary>
        /// 보상 총액. **업적을 고치면 이 값을 반드시 다시 세라.**
        ///
        /// 세지 않으면 조용히 어긋난다. 파편을 쓰는 곳은 룬 강화(9종 만렙 ≈ 9,180)와
        /// 스킨(400)인데, 업적 보상이 그 절반을 넘어가면 **강화를 위해 판을 도는
        /// 이유가 사라진다** — 업적만 훑으면 되기 때문이다.
        ///
        /// 지금 3,540 / 강화 총액 9,180 ≈ 39%.
        /// `python tools/check_achievements.py`가 이 값과 실제 합을 대조한다.
        /// </summary>
        public const int TotalReward = 3540;

        /// <summary>업적당 파편 보상.</summary>
        /// <summary>
        /// 업적당 파편.
        ///
        /// 배분 원칙 — 초반은 열어주고, 이미 파편을 쓴 뒤에 주는 건 상징적으로:
        ///  · 온보딩은 작게 (첫 강화에 보태는 정도)
        ///  · 숙련·발견은 중간 (해볼 이유가 되어야 한다)
        ///  · 장기는 크되 도달이 어렵게
        ///  · 순환형은 최소. 강화에 9,180을 쓴 뒤 돌려주는 건 경제가 아니라 환급이다
        /// </summary>
        public static int Reward(Achievement a)
        {
            switch (a)
            {
                // 온보딩
                case Achievement.FirstBlood: return 30;
                case Achievement.FirstClear: return 60;

                // 숙련
                case Achievement.Perfectionist: return 250;
                case Achievement.SharpEye: return 150;
                case Achievement.Untouched: return 120;

                // 발견
                case Achievement.Scholar: return 300;
                case Achievement.Inscriber: return 200;
                case Achievement.Rally: return 150;
                case Achievement.Reviver: return 200;
                case Achievement.WraithSlayer: return 250;

                // 장기
                case Achievement.Slayer: return 250;
                case Achievement.Caster: return 200;
                case Achievement.Finisher: return 300;
                case Achievement.Conqueror: return 800;

                // 순환형 (배지)
                case Achievement.Master: return 80;
                case Achievement.Collector: return 200;

                default: return 50;
            }
        }

        public static string Name(Achievement a) { return Loc.T("ach." + a.ToString().ToLowerInvariant()); }
        public static string Desc(Achievement a) { return Loc.T("ach." + a.ToString().ToLowerInvariant() + ".sub"); }

        /// <summary>지금 얼마나 왔나 (0~1). 세는 업적만 의미가 있다.</summary>
        public static float Progress(Achievement a)
        {
            switch (a)
            {
                case Achievement.Perfectionist: return Mathf.Clamp01(Data.perfects / (float)PerfectGoal);
                case Achievement.WraithSlayer: return Mathf.Clamp01(Data.wraiths / (float)WraithGoal);
                case Achievement.Reviver: return Mathf.Clamp01(Data.revived / (float)ReviveGoal);
                case Achievement.Collector: return Mathf.Clamp01(PlayerData.Glyphs.Count / (float)GlyphGoal);
                case Achievement.Slayer: return Mathf.Clamp01(Data.kills / (float)KillGoal);
                case Achievement.Caster: return Mathf.Clamp01(Data.casts / (float)CastGoal);
                default: return Done(a) ? 1f : 0f;
            }
        }

        public static string ProgressText(Achievement a)
        {
            switch (a)
            {
                case Achievement.Perfectionist: return Data.perfects + " / " + PerfectGoal;
                case Achievement.WraithSlayer: return Data.wraiths + " / " + WraithGoal;
                case Achievement.Reviver: return Data.revived + " / " + ReviveGoal;
                case Achievement.Collector: return PlayerData.Glyphs.Count + " / " + GlyphGoal;
                case Achievement.Slayer: return Data.kills + " / " + KillGoal;
                case Achievement.Caster: return Data.casts + " / " + CastGoal;
                default: return "";
            }
        }

        // ── 저장 ────────────────────────────────────────────────

        private static AchieveFile _data;

        private static string Path
        {
            get { return System.IO.Path.Combine(Application.persistentDataPath, "achievements.json"); }
        }

        private static AchieveFile Data
        {
            get { if (_data == null) Load(); return _data; }
        }

        /// <summary>
        /// **받을 수 있는 보상**이 달라질 때만 오른다 (달성·수령·불러오기·초기화).
        /// 빨간 점(Notify)이 이걸 본다 — 세는 숫자가 늘 때마다 오르면
        /// 적을 하나 잡을 때마다 문양 목록을 다시 훑게 된다.
        /// </summary>
        public static int Revision { get; private set; }

        /// <summary>
        /// 세는 숫자(처치·시전·PERFECT…)가 바뀔 때 오른다.
        /// 진행 막대처럼 **숫자만 보는 쪽**이 이걸 본다.
        /// Revision과 나눈 이유: 둘의 갱신 빈도가 백 배쯤 차이 난다.
        /// </summary>
        public static int CounterRevision { get; private set; }

        public static void Load()
        {
            _data = null;
            try
            {
                string json = SaveFile.Read(Path);
                if (json != null) _data = JsonUtility.FromJson<AchieveFile>(json);
            }
            catch (Exception e) { Debug.LogWarning("업적 로드 실패: " + e.Message); }
            if (_data == null) _data = new AchieveFile();
            Revision++;
        }

        private static bool _dirty;

        /// <summary>
        /// 바뀌었다고만 표시한다. **파일은 여기서 쓰지 않는다.**
        ///
        /// 예전엔 세는 곳마다 곧장 파일을 썼다. `OnRuneKill`은 룬으로 적을 하나
        /// 쓰러뜨릴 때마다 불리는데, 별똥별로 12마리를 한 번에 정리하면
        /// **한 프레임에 디스크 쓰기가 12번** 일어났다. 모바일에서는 그대로 끊김이다.
        ///
        /// 안전한 지점(스테이지 끝·보상 수령·앱이 백그라운드로 갈 때)에서 한 번만 쓴다.
        /// </summary>
        private static void MarkDirty()
        {
            _dirty = true;
            CounterRevision++;
        }

        /// <summary>미뤄둔 저장을 실제로 쓴다.</summary>
        public static void Flush()
        {
            if (!_dirty) return;
            _dirty = false;

            SaveFile.Write(Path, JsonUtility.ToJson(Data));
        }

        public static bool Done(Achievement a) { return (Data.done & (1 << (int)a)) != 0; }
        public static bool Claimed(Achievement a) { return (Data.claimed & (1 << (int)a)) != 0; }

        /// <summary>받을 수 있는 보상이 있는가. 빨간 점이 이걸 본다.</summary>
        public static bool AnyClaimable
        {
            get
            {
                foreach (Achievement a in Enum.GetValues(typeof(Achievement)))
                    if (Done(a) && !Claimed(a)) return true;
                return false;
            }
        }

        public static bool Claim(Achievement a)
        {
            if (!Done(a) || Claimed(a)) return false;

            Data.claimed |= 1 << (int)a;
            PlayerData.AddShards(Reward(a));

            Revision++;
            _dirty = true;
            Flush();
            return true;
        }

        /// <summary>달성 처리. 이미 달성했으면 아무 일도 없다.</summary>
        private static void Unlock(Achievement a)
        {
            if (Done(a)) return;

            Data.done |= 1 << (int)a;

            // 달성과 수령은 즉시 쓴다. 미루다 앱이 죽으면 보상이 사라지고,
            // 그건 세는 숫자 몇 개를 잃는 것과 무게가 다르다.
            Revision++;
            _dirty = true;
            Flush();

            Core.AudioManager.Play(Core.Sfx.RuneLearned, 0.85f, 1.1f);
            Recent = a;
            RecentAt = Time.unscaledTime;
        }

        /// <summary>방금 달성한 것. UI가 잠깐 띄운다.</summary>
        public static Achievement Recent { get; private set; }
        public static float RecentAt { get; private set; }

        // ── 세는 곳 ─────────────────────────────────────────────
        //
        // 전투 쪽에서 사건이 일어날 때마다 불러 준다. 조건 판정은 전부 여기서 한다 —
        // 부르는 쪽이 "이제 달성인가?"를 판단하기 시작하면 곧 두 곳이 어긋난다.

        /// <summary>한 판 안에서만 세는 것. 스테이지를 시작할 때 GameFlow가 비운다.</summary>
        private static int _runPerfects;

        public static void BeginStage()
        {
            _runPerfects = 0;
        }

        public static void OnRuneCast(bool perfect)
        {
            Unlock(Achievement.FirstBlood);

            Data.casts++;
            if (Data.casts >= CastGoal) Unlock(Achievement.Caster);

            if (perfect)
            {
                Data.perfects++;
                if (Data.perfects >= PerfectGoal) Unlock(Achievement.Perfectionist);

                _runPerfects++;
                if (_runPerfects >= RunPerfectGoal) Unlock(Achievement.SharpEye);
            }
            MarkDirty();
        }

        /// <summary>룬으로 적을 쓰러뜨렸다. 평타로 죽은 건 세지 않는다.</summary>
        public static void OnRuneKill()
        {
            Data.kills++;
            if (Data.kills >= KillGoal) Unlock(Achievement.Slayer);
            MarkDirty();
        }

        /// <summary>고양이 몇 명에게 걸렸나.</summary>
        public static void OnEmpower(int count)
        {
            if (count >= RallyGoal) Unlock(Achievement.Rally);
        }

        /// <summary>커스텀 룬을 등록했다. 각인 모드는 아무도 안 가리키던 기능이다.</summary>
        public static void OnInscribed()
        {
            Unlock(Achievement.Inscriber);
        }

        public static void OnWraithKilled()
        {
            Data.wraiths++;
            if (Data.wraiths >= WraithGoal) Unlock(Achievement.WraithSlayer);
            MarkDirty();
        }

        public static void OnRevived(int count)
        {
            if (count <= 0) return;
            Data.revived += count;
            if (Data.revived >= ReviveGoal) Unlock(Achievement.Reviver);
            MarkDirty();
        }

        public static void OnStageCleared(int heroesAlive, int heroesTotal, int runeKindsUsed)
        {
            Unlock(Achievement.FirstClear);

            if (heroesAlive >= heroesTotal) Unlock(Achievement.Untouched);

            // 마지막 스테이지를 깼는가
            var list = StageDatabase.All;
            if (list.Count > 0 && StageProgress.IsCleared(list[list.Count - 1].Id))
                Unlock(Achievement.Finisher);

            // 룬 하나라도 만렙인가
            var runes = GlyphTable.Runes;
            for (int i = 0; i < runes.Length; i++)
                if (PlayerData.LevelOf(runes[i]) >= PlayerData.MaxRuneLevel)
                {
                    Unlock(Achievement.Master);
                    break;
                }
            if (runeKindsUsed >= GlyphTable.Runes.Length) Unlock(Achievement.Scholar);

            if (PlayerData.Glyphs.Count >= GlyphGoal) Unlock(Achievement.Collector);

            // 모든 스테이지 3별
            var all = StageDatabase.All;
            bool perfect = all.Count > 0;
            for (int i = 0; i < all.Count; i++)
                if (StageProgress.StarsOf(all[i].Id) < 3) { perfect = false; break; }
            if (perfect) Unlock(Achievement.Conqueror);

            Flush();
        }

        public static void ResetAll()
        {
            _data = new AchieveFile();
            Revision++;
            try { SaveFile.Delete(Path); }
            catch (Exception e) { Debug.LogWarning("업적 삭제 실패: " + e.Message); }
        }
    }
}
