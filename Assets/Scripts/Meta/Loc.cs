using System.Collections.Generic;

namespace RuneCast.Meta
{
    public enum Lang
    {
        Korean = 0,
        English = 1,
    }

    /// <summary>
    /// 문자열 현지화.
    ///
    /// 표를 코드에 두는 이유는 이 프로젝트의 다른 데이터와 같다 — 무엇이 왜 바뀌었는지
    /// git 로그에 남고, 에디터를 안 켜도 고칠 수 있다. 항목이 수백 개를 넘어가면
    /// CSV나 에셋으로 옮길 것.
    ///
    /// **키를 못 찾으면 예외 대신 키 자체를 반환한다.** 번역이 빠졌을 때 게임이 멈추는 것보다
    /// 화면에 `stage.name.3` 같은 게 보이는 편이 낫다 — 뭐가 빠졌는지 바로 알 수 있다.
    /// </summary>
    public static class Loc
    {
        public static Lang Current = Lang.Korean;

        public static string LanguageName(Lang l)
        {
            return l == Lang.Korean ? "한국어" : "English";
        }

        public static string T(string key)
        {
            string[] v;
            if (!Table.TryGetValue(key, out v)) return key;
            int i = (int)Current;
            return i < v.Length && !string.IsNullOrEmpty(v[i]) ? v[i] : v[0];
        }

        public static string F(string key, params object[] args)
        {
            return string.Format(T(key), args);
        }

        // ko, en
        private static readonly Dictionary<string, string[]> Table = new Dictionary<string, string[]>
        {
            // ── 공통 ────────────────────────────────────────────
            { "common.back",        new[] { "돌아가기", "Back" } },
            { "common.close",       new[] { "닫기", "Close" } },
            { "common.on",          new[] { "켜짐", "On" } },
            { "common.off",         new[] { "꺼짐", "Off" } },
            { "common.max",         new[] { "최대", "MAX" } },
            { "common.locked",      new[] { "잠김", "Locked" } },
            { "common.shards",      new[] { "파편", "Shards" } },

            // ── 내 정보 ─────────────────────────────────────────
            { "profile.title",      new[] { "내 정보", "Profile" } },
            { "profile.power",      new[] { "주력", "Power" } },
            { "profile.stars",      new[] { "모은 별", "Stars" } },
            { "profile.runes",      new[] { "룬 강화 현황", "Rune levels" } },
            { "profile.level",      new[] { "Lv {0}", "Lv {0}" } },
            { "profile.mult",       new[] { "\u00d7{0:F2}", "\u00d7{0:F2}" } },

            // ── 시작 확인 ───────────────────────────────────────
            { "confirm.start",      new[] { "시작", "Start" } },
            { "confirm.waves",      new[] { "물결 {0}개", "{0} waves" } },
            { "confirm.mana",       new[] { "마나 {0}  ·  초당 {1} 회복", "Mana {0}  ·  {1}/s" } },
            { "confirm.manaFree",   new[] { "마나 제한 없음", "Unlimited mana" } },

            // ── 각인권 ──────────────────────────────────────────
            { "shop.ticketGain",    new[] { "각인권 {0}장을 얻었다", "Got {0} Inscription Ticket(s)" } },
            { "shop.navInscribe",   new[] { "각인", "Inscribe" } },
            { "inscribe.what",      new[] { "룬의 도형을 내가 그린 것으로 바꾼다",
                                            "Redraw a rune's shape yourself" } },
            { "inscribe.why1",      new[] { "잘 안 그려지는 룬이 있으면 손에 맞는 모양으로 다시 가르칠 수 있다.",
                                            "If a rune keeps failing, teach it a shape your hand likes." } },
            { "inscribe.why2",      new[] { "원래 도형도 그대로 남으니, 둘 중 아무거나 그려도 나간다.",
                                            "The original shape still works — either one casts it." } },
            { "inscribe.start",     new[] { "각인하러 가기", "Start inscribing" } },
            { "inscribe.customCount", new[] { "지금까지 등록한 도형 {0}개", "{0} custom shapes registered" } },
            { "inscribe.howToGet",  new[] { "각인권은 문양 소환에서 가끔 나옵니다",
                                            "Tickets drop from glyph summons" } },
            { "inscribe.tickets",   new[] { "남은 각인권 {0}장", "{0} ticket(s) left" } },
            { "inscribe.noTicket",  new[] { "각인권이 없다", "No tickets left" } },
            // 각인은 대체가 아니라 추가라, 남의 도형과 닮으면 그 룬이 안 나가게 된다.
            { "inscribe.tooSimilar", new[] { "{0} 룬과 너무 닮았다. 다르게 그려라",
                                             "Too close to {0}. Draw something different" } },

            // ── 쓸어버린 순간 ───────────────────────────────────
            // 큰 글씨는 "네가 했다"는 뜻으로만 쓴다. KillFeed 설명 참고.
            { "kill.multi",         new[] { "{0} 처치", "{0} Down" } },
            { "kill.sweep",         new[] { "쓸어버렸다", "Swept Away" } },
            { "kill.wipe",          new[] { "싹쓸이", "Wipeout" } },

            // ── 스테이지 선택 ───────────────────────────────────
            { "title",              new[] { "RUNE CAST", "RUNE CAST" } },
            { "title.start",        new[] { "게임 시작", "Play" } },
            { "title.fullscreen",   new[] { "전체화면", "Fullscreen" } },
            { "title.quit",         new[] { "종료", "Quit" } },
            { "select.workshop",    new[] { "대장장이", "Blacksmith" } },
            { "select.settings",    new[] { "설  정", "Settings" } },
            { "select.chapter",     new[] { "— {0}장 —", "— Chapter {0} —" } },
            // 3~7장은 아직 이름이 없다. StageDef.Name이 여기로 떨어진다.
            { "stage.generic",      new[] { "{0}판", "Stage {0}" } },
            { "select.chapterPick", new[] { "장을 고르세요", "Pick a chapter" } },
            { "select.chapterRange",new[] { "{0}~{1}판", "Stages {0}-{1}" } },
            { "select.chapterWip",  new[] { "준비 중", "Work in progress" } },
            // "…열립니다"로 쓰고 싶었지만 '열'이 잘라낸 글꼴에 없다.
            // make_font.py를 돌리면 쓸 수 있다.
            { "select.chapterLock", new[] { "{0}장을 먼저 끝내세요", "Clear chapter {0} first" } },
            { "select.hint",        new[] { "판을 골라 시작하세요",
                                            "Pick a stage to begin" } },
            { "select.waveInfo",    new[] { "웨이브 {0} · 적 {1}", "{0} waves · {1} enemies" } },
            { "select.manaLimited", new[] { "마나 제한", "Limited mana" } },

            // ── 잉크 ────────────────────────────────────────────
            { "ink.tier",           new[] { "잉크 {0} / {1}", "Ink {0} / {1}" } },
            { "ink.next",           new[] { "★ {0}개 더 모으면 잉크가 늘어납니다",
                                            "Collect {0} more ★ to increase ink" } },
            { "ink.maxTier",        new[] { "최대 단계", "Max tier" } },
            { "ink.label",          new[] { "잉크", "Ink" } },
            { "ink.empty",          new[] { "잉크 소진", "Out of ink" } },

            // ── 전투 HUD ────────────────────────────────────────
            { "hud.mana",           new[] { "마나 {0}", "Mana {0}" } },
            { "hud.manaInf",        new[] { "마나 ∞", "Mana ∞" } },
            { "hud.stageLine",      new[] { "웨이브 {0} / {1}     아군 {2} / {3}",
                                            "Wave {0} / {1}     Heroes {2} / {3}" } },
            { "hud.wave",           new[] { "WAVE {0} / {1}", "WAVE {0} / {1}" } },
            { "hud.waveClear",      new[] { "WAVE CLEAR", "WAVE CLEAR" } },
            { "hud.getReady",       new[] { "곧 시작합니다", "Get ready" } },
            // 마지막 물결은 번호 대신 이렇게. 남은 수를 세는 것보다
            // "이게 끝"이라는 게 먼저 읽혀야 마나를 쏟을지 정할 수 있다.
            { "hud.waveFinal",      new[] { "FINAL WAVE", "FINAL WAVE" } },
            { "hud.failShape",      new[] { "인식 실패", "Not recognized" } },
            { "hud.failMana",       new[] { "마나 부족", "Not enough mana" } },
            { "hud.failInk",        new[] { "잉크 부족 — 더 작게", "Out of ink — draw smaller" } },
            { "hud.blocked",        new[] { "막음", "Blocked" } },
            { "tut.wraith",         new[] { "저 적은 아군의 공격이 거의 안 통합니다",
                                            "Your allies barely scratch that one" } },
            { "tut.wraith.sub",     new[] { "룬으로 끊는 게 훨씬 빠릅니다 · 푸른 고리가 표식입니다",
                                            "A rune ends it far faster · The blue ring marks them" } },

            { "hud.revived",        new[] { "부활!", "Revived!" } },
            { "hud.failNoCorpse",   new[] { "되살릴 아군 없음", "No one to revive" } },
            { "hud.hintDraw",       new[] { "드래그해서 그리기 · 그리는 동안 슬로우",
                                            "Drag to draw · Time slows while drawing" } },
            { "hud.hintKeys",       new[] { "Tab — 각인 모드      F1 — 룬 충돌 진단      Esc — 일시정지",
                                            "Tab — Inscribe      F1 — Rune diagnostics      Esc — Pause" } },
            { "hud.hintMusic",      new[] { "M — 다음 곡      ♪ {0}", "M — Next track      ♪ {0}" } },
            { "hud.customRunes",    new[] { "커스텀 템플릿 {0}개 적용됨", "{0} custom templates loaded" } },

            // ── 결과 ────────────────────────────────────────────
            { "result.clear",       new[] { "STAGE CLEAR", "STAGE CLEAR" } },
            { "result.failed",      new[] { "FAILED", "FAILED" } },
            { "result.survived",    new[] { "아군 생존  {0} / {1}", "Heroes alive  {0} / {1}" } },
            { "result.wiped",       new[] { "아군이 전멸했습니다", "Your heroes were wiped out" } },
            { "result.newRecord",   new[] { "신기록!      파편 +{0}", "New record!      Shards +{0}" } },
            { "result.shards",      new[] { "파편 +{0}", "Shards +{0}" } },
            { "result.retry",       new[] { "다시 하기", "Retry" } },
            { "result.next",        new[] { "다음 스테이지", "Next stage" } },
            { "result.toSelect",    new[] { "스테이지 선택", "Stage select" } },

            // ── 일시정지 ────────────────────────────────────────
            { "pause.title",        new[] { "일시정지", "Paused" } },
            { "pause.resume",       new[] { "계속하기", "Resume" } },
            { "pause.restart",      new[] { "처음부터", "Restart" } },
            // "포기하고"는 플레이어를 탓하는 말이다. 다시 도전하려고 나가는
            // 경우가 더 많은데, 그때마다 졌다고 말할 이유가 없다.
            { "pause.quit",         new[] { "스테이지 나가기", "Leave stage" } },

            // ── 설정 ────────────────────────────────────────────
            { "settings.title",     new[] { "설  정", "Settings" } },
            { "settings.language",  new[] { "언어", "Language" } },
            { "settings.bgm",       new[] { "배경음", "Music" } },
            { "settings.sfx",       new[] { "효과음", "Sound" } },
            { "settings.slowmo",    new[] { "시전 중 슬로우", "Slow-mo while drawing" } },
            { "settings.slowmoHint",new[] { "낮을수록 느려집니다 · 그리기가 급하면 낮추세요",
                                            "Lower is slower. Reduce it if drawing feels rushed." } },
            { "settings.fullscreen",new[] { "전체 화면", "Fullscreen" } },
            { "settings.debug",     new[] { "인식 진단 패널", "Recognition debug panel" } },
            { "settings.reset",     new[] { "진행 상황 초기화", "Reset all progress" } },
            { "settings.resetWarn", new[] { "별·파편·강화·문양이 전부 사라집니다. 한 번 더 누르면 실행됩니다.",
                                            "Stars, shards, upgrades and glyphs will all be lost. Click again to confirm." } },
            { "settings.resetDone", new[] { "초기화했습니다", "Progress reset." } },

            // ── 공방 ────────────────────────────────────────────
            { "shop.title",         new[] { "대 장 장 이", "BLACKSMITH" } },
            { "shop.summoning",     new[] { "문양을 부르는 중…", "Summoning a glyph…" } },
            { "shop.autoEquipHint", new[] { "장착됨 — 대장장이에서 바꿀 수 있습니다",
                                            "Equipped — change it anytime here" } },
            { "shop.slotsFullHint", new[] { "장착 칸이 가득 찼습니다 — 목록에서 교체하세요",
                                            "All slots full — swap it from the list" } },
            { "shop.again",         new[] { "한 번 더  {0}", "Again  {0}" } },
            { "shop.pull10",        new[] { "10연 소환  {0}", "Summon ×10  {0}" } },
            { "shop.pull10Hint",    new[] { "{0} 이상 1개 확정 · 개당 가격은 같습니다",
                                            "One {0}+ guaranteed · same price per glyph" } },
            { "shop.autoEquipN",    new[] { "{0}개 자동 장착됨", "{0} auto-equipped" } },
            { "shop.confirm",       new[] { "확인", "OK" } },
            { "shop.navUpgrade",    new[] { "룬 벼리기", "Forge runes" } },
            { "shop.navGlyph",      new[] { "문양 소환", "Summon glyphs" } },
            { "shop.owned",         new[] { "보유 문양 {0}개", "{0} glyphs owned" } },
            { "shop.upgradeHint",   new[] { "레벨은 위력만 올립니다 · 범위·마나·잉크는 문양이 맡습니다",
                                            "Levels raise power only. Size, mana and ink come from glyphs." } },
            { "shop.power",         new[] { "위력 ×{0:F2}   마나 {1}", "Power ×{0:F2}   Mana {1}" } },
            { "shop.upgrade",       new[] { "강화  {0}", "Upgrade  {0}" } },
            { "shop.pull",          new[] { "소환  {0}", "Summon  {0}" } },
            { "shop.equip",         new[] { "장착", "Equip" } },
            { "shop.unequip",       new[] { "해제", "Unequip" } },
            { "shop.dismantle",     new[] { "분해 {0}", "Scrap {0}" } },
            { "shop.empty",         new[] { "아직 문양이 없습니다. 소환해 보세요.",
                                            "No glyphs yet. Try summoning one." } },

            // ── 각인 모드 ───────────────────────────────────────
            { "inscribe.title",     new[] { "각 인  모 드", "INSCRIBE MODE" } },
            { "inscribe.target",    new[] { "대상: {0}   —   {1} / {2} 회", "Target: {0}   —   {1} / {2}" } },
            { "inscribe.hint",      new[] { "같은 도형을 3번 그리면 등록됩니다 (평균내지 않고 3개 다 저장)",
                                            "Draw the same shape 3 times (all three are stored, not averaged)" } },
            { "inscribe.wipe",      new[] { "등록한 룬 전부 삭제", "Delete all custom runes" } },
            { "inscribe.wipeWarn",  new[] { "한 번 더 누르면 삭제됩니다", "Press again to delete" } },
            { "inscribe.exit",      new[] { "나가기", "Exit" } },
            { "inscribe.path",      new[] { "저장 위치: {0}", "Saved to: {0}" } },

            // ── 진단 ────────────────────────────────────────────
            { "diag.distances",     new[] { "매칭 거리 (작을수록 닮음)", "Match distance (lower = closer)" } },
            { "diag.reject",        new[] { "거부 임계값  {0:F3}", "Reject threshold  {0:F3}" } },
            { "diag.title",         new[] { "룬 충돌 진단  (F1로 닫기)", "Rune collision check  (F1 to close)" } },
            { "diag.hint",          new[] { "두 룬 사이 최소 거리. 임계값 {0:F2}보다 충분히 커야 안전",
                                            "Min distance between runes. Should exceed {0:F2} comfortably" } },

            // ── 크레딧 ──────────────────────────────────────────
            { "common.power",       new[] { "주력 {0}", "Power {0}" } },
            { "select.next",        new[] { "다음 도전", "Up next" } },
            { "title.home",         new[] { "타이틀", "Title" } },
            { "shop.powerGain",     new[] { "주력 +{0}", "Power +{0}" } },

            // ── 튜토리얼 (첫 1회) ───────────────────────────────
            { "tut.draw",           new[] { "화면을 드래그해서 도형을 그리세요",
                                            "Drag on the screen to draw a shape" } },
            { "tut.draw.sub",       new[] { "그리는 동안 시간이 느려집니다 · 떠 있는 도형을 따라 그려보세요",
                                            "Time slows while you draw \u00b7 Copy the floating shape" } },

            { "tut.grade",          new[] { "모양이 정확할수록 강하게 나갑니다",
                                            "The closer your shape, the stronger the rune" } },
            { "tut.grade.sub",      new[] { "GOOD보다 PERFECT가 세 배 넘게 셉니다",
                                            "PERFECT hits over three times harder than GOOD" } },

            { "tut.ink",            new[] { "한 번에 그을 수 있는 길이에 한도가 있습니다",
                                            "There is a limit to how much you can draw at once" } },
            { "tut.ink.sub",        new[] { "잘린 획은 발동하지 않습니다 · 별을 모으면 한도가 늘어납니다",
                                            "A cut-off stroke does not cast \u00b7 Stars raise the limit" } },

            { "tut.mana",           new[] { "마나가 모자랍니다",
                                            "Not enough mana" } },
            { "tut.mana.sub",       new[] { "마나는 시간이 지나면 찹니다 · 싼 룬부터 쓰세요",
                                            "Mana refills over time \u00b7 Start with cheaper runes" } },

            { "tut.cleared",        new[] { "별을 모으면 더 크게 그릴 수 있습니다",
                                            "Collect stars to draw bigger" } },
            { "tut.cleared.sub",    new[] { "아군을 많이 살릴수록 별을 많이 받습니다 · 파편은 대장장이에서 씁니다",
                                            "Save more allies for more stars \u00b7 Spend shards at the workshop" } },

            { "tut.dismiss",        new[] { "눌러서 닫기", "Tap to dismiss" } },

            { "settings.tutorial",  new[] { "튜토리얼 다시 보기", "Replay tutorial" } },
            { "settings.tutorialDone", new[] { "다음 판부터 다시 나옵니다", "It will show again next run" } },

            // ── 궤적 스킨 ───────────────────────────────────────
            { "skin.title",         new[] { "궤적 스킨", "Trail skin" } },
            { "skin.using",         new[] { "사용 중", "In use" } },
            { "skin.needStars",     new[] { "★ {0}", "★ {0}" } },
            { "skin.buy",           new[] { "파편 {0}", "{0} shards" } },
            { "skin.default",       new[] { "기본", "Default" } },
            { "skin.ember",         new[] { "잉걸", "Ember" } },
            { "skin.moss",          new[] { "이끼", "Moss" } },
            { "skin.ash",           new[] { "잿빛", "Ash" } },
            { "skin.wraith",        new[] { "망령", "Wraith" } },
            { "skin.gold",          new[] { "금빛", "Gold" } },
            { "skin.void",          new[] { "공허", "Void" } },

            // ── 업적 ────────────────────────────────────────────
            { "ach.unlocked",       new[] { "업적 달성", "Achievement unlocked" } },
            { "ach.title",          new[] { "업적", "Achievements" } },
            { "ach.claim",          new[] { "파편 {0} 받기", "Claim {0}" } },
            { "ach.claimed",        new[] { "받음", "Claimed" } },

            { "ach.firstblood",     new[] { "첫 룬", "First Rune" } },
            { "ach.firstclear",     new[] { "첫 승", "First Win" } },
            { "ach.firstclear.sub", new[] { "스테이지를 하나 클리어한다", "Clear a stage" } },
            { "ach.sharpeye",       new[] { "한 판의 손끝", "Steady Hand" } },
            { "ach.sharpeye.sub",   new[] { "한 판에서 PERFECT 5번", "Land 5 PERFECT casts in one stage" } },
            { "ach.inscriber",      new[] { "각인사", "Inscriber" } },
            { "ach.inscriber.sub",  new[] { "나만의 도형을 룬으로 등록한다", "Register your own shape as a rune" } },
            { "ach.rally",          new[] { "함성", "Rally" } },
            { "ach.rally.sub",      new[] { "고양 한 번으로 아군 4명 이상", "Empower 4 or more allies at once" } },
            { "ach.slayer",         new[] { "학살자", "Slayer" } },
            { "ach.slayer.sub",     new[] { "룬으로 적 200마리를 쓰러뜨린다", "Kill 200 enemies with runes" } },
            { "ach.caster",         new[] { "숙련된 손", "Practised Hand" } },
            { "ach.caster.sub",     new[] { "룬을 500번 발동한다", "Cast 500 runes" } },
            { "ach.finisher",       new[] { "완주", "The End" } },
            { "ach.finisher.sub",   new[] { "마지막 스테이지를 클리어한다", "Clear the final stage" } },
            { "ach.master",         new[] { "정통한 자", "Mastery" } },
            { "ach.master.sub",     new[] { "룬 하나를 만렙까지 올린다", "Take one rune to max level" } },
            { "ach.firstblood.sub", new[] { "룬을 한 번 발동한다", "Cast a rune once" } },
            { "ach.perfectionist",  new[] { "완벽주의", "Perfectionist" } },
            { "ach.perfectionist.sub", new[] { "PERFECT 판정 30번", "Land 30 PERFECT casts" } },
            { "ach.wraithslayer",   new[] { "망령 사냥꾼", "Wraith Slayer" } },
            { "ach.wraithslayer.sub", new[] { "망령 20마리를 쓰러뜨린다", "Destroy 20 wraiths" } },
            { "ach.untouched",      new[] { "한 명도 잃지 않고", "Not One Lost" } },
            { "ach.untouched.sub",  new[] { "아군을 하나도 잃지 않고 클리어", "Clear a stage with everyone alive" } },
            { "ach.collector",      new[] { "수집가", "Collector" } },
            { "ach.collector.sub",  new[] { "문양 40개를 모은다", "Own 40 glyphs" } },
            { "ach.conqueror",      new[] { "정복자", "Conqueror" } },
            { "ach.conqueror.sub",  new[] { "모든 스테이지 3별", "Three stars on every stage" } },
            { "ach.reviver",        new[] { "돌아오라", "Come Back" } },
            { "ach.reviver.sub",    new[] { "소생으로 10명을 살린다", "Revive 10 allies" } },
            { "ach.scholar",        new[] { "모든 룬", "Every Rune" } },
            { "ach.scholar.sub",    new[] { "한 판에서 룬 9종을 모두 쓴다", "Use all nine runes in one stage" } },

            { "shop.navCollection", new[] { "기록", "Collection" } },
            { "settings.infiniteInk", new[] { "개발: 잉크 무한", "Dev: infinite ink" } },

            { "credits.title",      new[] { "만든 것들", "Credits" } },
            { "credits.thanks",     new[] { "이 게임은 아래 창작물들 덕에 만들어졌습니다",
                                            "This game was made possible by the work below" } },
            { "credits.ui",         new[] { "UI", "UI" } },
            { "credits.music",      new[] { "음악", "Music" } },
            { "credits.sfx",        new[] { "효과음", "Sound effects" } },
            { "credits.units",      new[] { "캐릭터", "Characters" } },
            { "credits.terrain",    new[] { "배경·지형", "Terrain" } },
            { "credits.vfx",        new[] { "이펙트", "Visual effects" } },
            { "credits.engine",     new[] { "엔진", "Engine" } },

            // ── 장 규칙 ─────────────────────────────────────────
            //
            // 판이 시작될 때 배너 아래 한 줄로 뜬다. **이름과 한 줄 설명이 같이
            // 나와야 한다** — 이름만 뜨면 무슨 일이 벌어지는지 겪고 나서야 알고,
            // 겪을 때는 이미 아군이 하나 죽어 있다.
            //
            // 글자가 잘라낸 글꼴 안에 있는지 확인하고 골랐다. make_font.py를
            // 돌리면 더 자연스러운 말을 쓸 수 있다.
            { "rule.wraith",        new[] { "망령", "Wraiths" } },
            { "rule.wraith.sub",    new[] { "아군의 공격이 거의 통하지 않는 적이 있다",
                                            "Some enemies barely feel your heroes' blows" } },
            { "rule.limitedmana",   new[] { "마나 제한", "Limited Mana" } },
            { "rule.limitedmana.sub", new[] { "마나가 무한하지 않다 · 아껴야 한다",
                                              "Mana no longer refills for free" } },
            { "rule.frost",         new[] { "서리", "Frost" } },
            { "rule.frost.sub",     new[] { "아군의 공격이 느려진다 · 고양이 값을 한다",
                                            "Your heroes strike slower — Empower earns its cost" } },
            { "rule.fog",           new[] { "안개", "Fog" } },
            { "rule.fog.sub",       new[] { "먼 적은 잘 보이지 않는다",
                                            "Distant enemies are hard to make out" } },
            // "역병"을 쓰고 싶었지만 '병'이 잘라낸 글꼴에 없다. 부패도 같은 뜻이다.
            { "rule.plague",        new[] { "부패", "Plague" } },
            { "rule.plague.sub",    new[] { "적이 죽을 때 가까운 아군이 다친다",
                                            "Dying enemies hurt the allies standing near them" } },
            { "rule.sealing",       new[] { "봉인", "Sealing" } },
            { "rule.sealing.sub",   new[] { "이 판에서는 {0} 룬을 쓸 수 없다",
                                            "{0} is sealed for this stage" } },
            { "rule.abyss",         new[] { "심연", "Abyss" } },
            { "rule.abyss.sub",     new[] { "마나가 시간으로 차지 않는다 · 잡아야 찬다",
                                            "Mana no longer trickles in — kills refill it" } },
            { "hud.failSealed",     new[] { "봉인된 룬", "Sealed rune" } },

            // ── 부대 ────────────────────────────────────────────
            //
            // **"궁수"·"앞줄"·"뒷줄"을 못 썼다.** 잘라낸 글꼴에 궁·앞·뒷이 없다.
            // make_font.py를 돌리면 셋 다 쓸 수 있고, 그때 이 표만 고치면 된다
            // (폰트 파일은 안 변하고 FontCharset.cs만 갱신된다).
            { "shop.navParty",      new[] { "부대", "Party" } },
            { "party.title",        new[] { "부대 편성", "Party" } },
            { "party.hint",         new[] { "판마다 인원이 다릅니다 · 위 칸부터 나갑니다",
                                            "Party size varies by stage · top slots go first" } },
            { "party.slot",         new[] { "칸 {0}", "Slot {0}" } },
            { "party.front",        new[] { "전방", "Front" } },
            { "party.back",         new[] { "후방", "Back" } },
            { "party.swap",         new[] { "교체", "Swap" } },
            { "party.locked",       new[] { "★ {0} 필요", "Needs ★ {0}" } },
            { "party.lockHint",     new[] { "별을 모으면 쓸 수 있습니다", "Collect stars to unlock" } },
            { "party.level",        new[] { "Lv {0}   위력 ×{1:F2}", "Lv {0}   Power ×{1:F2}" } },
            { "party.upgrade",      new[] { "강화  {0}", "Upgrade  {0}" } },
            // 후방은 사거리가 긴 용사만 값을 한다. 근접을 뒤에 두면 그냥 늦게 붙는다.
            { "party.backHint",     new[] { "후방은 거리를 두고 공격하는 용사에게 어울립니다",
                                            "The back row suits units that strike from range" } },

            { "hero.warrior",       new[] { "전사", "Warrior" } },
            { "hero.warrior.role",  new[] { "적을 막는다", "Holds the line" } },
            { "hero.archer",        new[] { "사수", "Archer" } },
            { "hero.archer.role",   new[] { "거리를 두고 공격한다", "Strikes from range" } },
            { "hero.monk",          new[] { "수도사", "Monk" } },
            { "hero.monk.role",     new[] { "아군을 회복시킨다", "Heals allies" } },

            // ── 룬 이름 ─────────────────────────────────────────
            { "rune.heal",          new[] { "힐", "Heal" } },
            { "rune.arrow",         new[] { "화살", "Arrow" } },
            { "rune.shield",        new[] { "보호막", "Shield" } },
            { "rune.meteor",        new[] { "별똥별", "Meteor" } },
            { "rune.chain",         new[] { "연쇄번개", "Chain" } },
            { "rune.vortex",        new[] { "소용돌이", "Vortex" } },
            { "rune.slash",         new[] { "가르기", "Slash" } },
            { "rune.empower",       new[] { "고양", "Empower" } },
            { "rune.revive",        new[] { "소생", "Revive" } },
            { "rune.pyre",          new[] { "봉화", "Pyre" } },
            { "rune.all",           new[] { "모든 룬", "All runes" } },

            // ── 도형 이름 ───────────────────────────────────────
            { "shape.circle",       new[] { "원", "Circle" } },
            { "shape.chevron",      new[] { "꺾쇠", "Chevron" } },
            { "shape.triangle",     new[] { "삼각형", "Triangle" } },
            { "shape.star",         new[] { "별", "Star" } },
            { "shape.zigzag",       new[] { "지그재그", "Zigzag" } },
            { "shape.spiral",       new[] { "나선", "Spiral" } },
            { "shape.line",         new[] { "직선", "Line" } },
            { "shape.infinity",     new[] { "무한대", "Infinity" } },
            { "shape.heart",        new[] { "하트", "Heart" } },
            { "shape.banner",       new[] { "깃발", "Banner" } },

            // ── 문양 ────────────────────────────────────────────
            { "glyph.common",       new[] { "일반", "Common" } },
            { "glyph.rare",         new[] { "희귀", "Rare" } },
            { "glyph.epic",         new[] { "영웅", "Epic" } },
            { "glyph.legendary",    new[] { "전설", "Legendary" } },
            { "glyph.power",        new[] { "위력", "Power" } },
            { "glyph.size",         new[] { "범위", "Size" } },
            { "glyph.mana",         new[] { "마나 소모", "Mana cost" } },
            { "glyph.ink",          new[] { "잉크 소모", "Ink cost" } },
            { "glyph.grade",        new[] { "판정", "Accuracy" } },

            // ── 프롤로그 ────────────────────────────────────────
            //
            // 이 게임에서 플레이어가 누구인지를 알려주는 유일한 자리다.
            // **플레이어는 화가이고, 병사들에게는 신이다.** 병사들은 하늘에
            // 나타난 도형을 계시로 받아들이지, 누가 그걸 손으로 그리고 있는지는
            // 끝내 모른다. 그 어긋남이 이 게임의 정체다.
            //
            // 대사를 짧게 자른 이유: 전투가 돌아가는 중에 겹쳐 뜨는 글이라
            // 두 줄을 넘기면 아무도 안 읽는다. 튜토리얼과 같은 제약이다.
            { "story.p1",           new[] { "몇이야?", "How many?" } },
            { "story.p1.sub",       new[] { "세지 마.", "Don't count." } },
            { "story.p2",           new[] { "여기서 죽는구나.", "So this is where we die." } },
            // 세 번째 박자는 침묵이다 — 도형이 그려지는 동안 대사가 없다.
            { "story.p3",           new[] { "…저게 뭐지?", "…what is that?" } },
            { "story.p4",           new[] { "신이다.", "A god." } },
            { "story.p4.sub",       new[] { "신께서 우릴 보고 계셔!", "A god is watching over us!" } },
            // 인식에 실패했을 때. 못 그린 걸 탓하지 않고 **아무 일도 없었다는 사실만**
            // 말한다. 그래야 다시 그리는 게 다음 수로 이어진다.
            { "story.pmiss",        new[] { "…아무 일도 없었어.", "…nothing happened." } },
            { "stage.0",            new[] { "그 날", "That Day" } },

            // ── 스테이지 이름 ───────────────────────────────────
            { "stage.1",            new[] { "첫 교전", "First Contact" } },
            { "stage.2",            new[] { "해골 떼", "Bone Horde" } },
            { "stage.3",            new[] { "단단한 놈", "The Tough One" } },
            { "stage.4",            new[] { "빠른 습격", "Swift Raid" } },
            { "stage.5",            new[] { "닿지 않는 것", "Out of Reach" } },
            { "stage.6",            new[] { "본로드의 진군", "March of Bonelords" } },
            { "stage.7",            new[] { "아껴 쓰기", "Ration Your Power" } },
            { "stage.8",            new[] { "밀려드는 무리", "Endless Tide" } },
            { "stage.9",            new[] { "정예", "Elites" } },
            { "stage.10",           new[] { "포위", "Encircled" } },
            { "stage.11",           new[] { "밤의 군세", "Host of Night" } },
            { "stage.12",           new[] { "최후의 진", "The Last Line" } },
        };
    }
}
