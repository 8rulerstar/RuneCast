using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using RuneCast.Battle;
using RuneCast.Gesture;

namespace RuneCast.Meta
{
    [Serializable]
    public class GlyphData
    {
        public int id;         // 인스턴스 고유번호 (장착 목록이 이걸로 참조한다)
        public int rarity;     // GlyphRarity
        public int effect;     // GlyphEffect
        public int targetRune; // RuneType. 0(None)이면 모든 룬에 적용
        public float value;
    }

    /// <summary>한 룬에 낀 문양들.</summary>
    [Serializable]
    public class RuneLoadoutData
    {
        public int rune;
        public List<int> glyphs = new List<int>();
    }

    [Serializable]
    public class RuneLevelData
    {
        public int rune;
        public int level;
    }

    [Serializable]
    public class PlayerFile
    {
        public int shards;
        public int nextGlyphId = 1;

        /// <summary>
        /// 각인권. 룬의 도형을 내가 그린 것으로 바꿀 때 한 장 쓴다.
        ///
        /// **처음에 두 장을 준다.** 뽑기 운에만 맡기면 이 기능이 있는 줄도 모르고
        /// 끝나는 사람이 생긴다. 도형을 바꾸는 건 이 게임에서 가장 자기 것처럼
        /// 느껴지는 조작인데, 그걸 확률 뒤에 완전히 숨기면 손해다.
        /// </summary>
        public int inscribeTickets = 2;
        public List<RuneLevelData> runeLevels = new List<RuneLevelData>();
        public List<GlyphData> glyphs = new List<GlyphData>();
        /// <summary>
        /// 예전 방식(전체 공용 장착). **읽기 전용으로만 남긴다** — 새 형식으로
        /// 옮기는 데만 쓰고 그 뒤로는 안 채운다. 저장 파일을 지우게 하지 않으려면
        /// 옛 필드를 남겨 둬야 한다.
        /// </summary>
        public List<int> equipped = new List<int>();

        /// <summary>룬별 장착. JsonUtility는 Dictionary를 못 다루므로 목록으로 둔다.</summary>
        public List<RuneLoadoutData> loadouts = new List<RuneLoadoutData>();

        /// <summary>고른 궤적 스킨. 비어 있으면 기본.</summary>
        public string skin = "";

        /// <summary>파편으로 산 스킨들. 별·업적으로 열리는 건 여기 안 들어간다.</summary>
        public List<string> ownedSkins = new List<string>();

        /// <summary>
        /// 부대 편성. 칸 하나가 용사 하나다.
        ///
        /// **비어 있으면 전부 전사로 친다.** 예전 저장 파일에는 이 항목이 없고,
        /// JsonUtility는 없는 필드를 기본값으로 두므로 그대로 빈 목록이 온다.
        /// 그때 "용사가 없다"가 되면 판이 시작되자마자 진다.
        /// </summary>
        public List<PartySlotData> party = new List<PartySlotData>();

        /// <summary>용사 종류별 강화 레벨.</summary>
        public List<HeroLevelData> heroLevels = new List<HeroLevelData>();
    }

    [Serializable]
    public class PartySlotData
    {
        public int kind;   // UnitKind
        public bool back;  // 뒷줄인가
    }

    [Serializable]
    public class HeroLevelData
    {
        public int kind;   // UnitKind
        public int level;
    }

    /// <summary>
    /// 파편·룬 레벨·문양 보관.
    ///
    /// 스테이지 진행(별)과 파일을 나눈 이유: 별은 "어디까지 갔나"이고 이쪽은 "뭘 모았나"라
    /// 성격이 다르다. 밸런스를 보려고 한쪽만 지우는 일이 잦은데, 한 파일이면 같이 날아간다.
    ///
    /// **재화는 파편 하나뿐이다.** 강화와 뽑기가 같은 재화를 쓰므로
    /// "이번엔 강화할까 뽑을까"라는 선택이 생긴다. 재화를 나누면 그 선택이 사라진다.
    /// </summary>
    public static class PlayerData
    {
        public const int MaxRuneLevel = 10;

        private static PlayerFile _data;

        private static string Path
        {
            get { return System.IO.Path.Combine(Application.persistentDataPath, "player_data.json"); }
        }

        public static string FilePath { get { return Path; } }

        private static PlayerFile Data
        {
            get
            {
                if (_data == null) Load();
                return _data;
            }
        }

        public static void Load()
        {
            _data = null;
            try
            {
                string json = SaveFile.Read(Path);
                if (json != null) _data = JsonUtility.FromJson<PlayerFile>(json);
            }
            catch (Exception e)
            {
                Debug.LogWarning("플레이어 데이터 로드 실패: " + e.Message);
            }

            if (_data == null) _data = new PlayerFile();
            if (_data.runeLevels == null) _data.runeLevels = new List<RuneLevelData>();
            if (_data.glyphs == null) _data.glyphs = new List<GlyphData>();
            if (_data.equipped == null) _data.equipped = new List<int>();
            if (_data.loadouts == null) _data.loadouts = new List<RuneLoadoutData>();

            MigrateEquipped();
            if (_data.ownedSkins == null) _data.ownedSkins = new List<string>();
            if (_data.nextGlyphId < 1) _data.nextGlyphId = 1;

            Revision++;
            Loadout.Invalidate();
        }

        // 10연차처럼 한 프레임에 십수 번 바뀌는 경우 매번 파일을 쓰면 끊긴다.
        // 배치 안에서는 저장을 미뤘다가 끝날 때 한 번만 쓴다.
        private static int _batchDepth;
        private static bool _dirtyInBatch;

        public static void BeginBatch()
        {
            _batchDepth++;
        }

        public static void EndBatch()
        {
            _batchDepth = Mathf.Max(0, _batchDepth - 1);
            if (_batchDepth > 0 || !_dirtyInBatch) return;

            _dirtyInBatch = false;
            WriteFile();
        }

        /// <summary>
        /// 열린 배치가 있으면 강제로 닫고 쓴다.
        ///
        /// BeginBatch/EndBatch는 finally로 짝을 맞춰 두었지만, 그건 부르는 쪽이
        /// 지켜야 하는 약속이다. 언젠가 누가 안 지키면 그때부터 **저장이 조용히
        /// 멈춘다** — 오류도 로그도 없이. 앱이 내려갈 때 한 번 밀어내면 최악의
        /// 경우에도 잃는 게 한 판을 넘지 않는다.
        /// </summary>
        public static void FlushBatch()
        {
            if (_batchDepth == 0 && !_dirtyInBatch) return;

            _batchDepth = 0;
            if (!_dirtyInBatch) return;

            _dirtyInBatch = false;
            WriteFile();
        }

        /// <summary>
        /// 저장 데이터가 바뀔 때마다 오른다. 파생 값을 캐시하는 쪽(주력·알림)이
        /// 언제 다시 계산해야 하는지 판단하는 데 쓴다.
        ///
        /// 배치 중에도 올린다 — 파일에 쓰는 시점이 아니라 **값이 바뀐 시점**이
        /// 캐시가 낡는 시점이기 때문이다.
        /// </summary>
        public static int Revision { get; private set; }

        private static void Save()
        {
            Revision++;

            if (_batchDepth > 0)
            {
                _dirtyInBatch = true;
                return;
            }
            WriteFile();
        }

        private static void WriteFile()
        {
            SaveFile.Write(Path, JsonUtility.ToJson(Data, true));
        }

        // ── 각인권 ──────────────────────────────────────────────

        public static int InscribeTickets { get { return Data.inscribeTickets; } }

        public static void AddInscribeTickets(int n)
        {
            if (n <= 0) return;
            Data.inscribeTickets += n;
            Save();
        }

        /// <summary>한 장 쓴다. 없으면 false.</summary>
        public static bool SpendInscribeTicket()
        {
            if (Data.inscribeTickets <= 0) return false;
            Data.inscribeTickets--;
            Save();
            return true;
        }

        // ── 파편 ────────────────────────────────────────────────

        public static int Shards { get { return Data.shards; } }

        public static void AddShards(int amount)
        {
            if (amount <= 0) return;
            Data.shards += amount;
            Save();
        }

        public static bool TrySpend(int amount)
        {
            if (amount <= 0 || Data.shards < amount) return false;
            Data.shards -= amount;
            Save();
            return true;
        }

        // ── 부대 편성 ───────────────────────────────────────────
        //
        // 판마다 인원이 다르므로(4~5, 나중에 더) 편성은 **넉넉히 들고 있다가
        // 앞에서부터 필요한 만큼 쓴다.** 판별로 따로 저장하면 판이 42개인데
        // 편성이 42벌이 되고, 그건 아무도 관리하고 싶어하지 않는다.

        /// <summary>편성이 들고 있는 최대 칸. 지금 제일 큰 판이 5명이라 여유를 뒀다.</summary>
        public const int PartySize = 8;

        /// <summary>
        /// i번 칸의 용사. 저장된 게 없거나 잠긴 용사면 전사로 떨어진다.
        ///
        /// **잠긴 용사를 걸러내는 게 중요하다.** 진행을 초기화하면 별이 0이 되는데
        /// 편성은 남는다 — 그대로 두면 아직 못 여는 용사가 전장에 선다.
        /// </summary>
        public static UnitKind PartyKind(int i)
        {
            if (i >= 0 && i < Data.party.Count)
            {
                var kind = (UnitKind)Data.party[i].kind;
                if (HeroRoster.IsUnlocked(kind)) return kind;
            }
            return UnitKind.Warrior;
        }

        public static bool PartyBack(int i)
        {
            return i >= 0 && i < Data.party.Count && Data.party[i].back;
        }

        private static void EnsureParty()
        {
            while (Data.party.Count < PartySize)
                Data.party.Add(new PartySlotData { kind = (int)UnitKind.Warrior, back = false });
        }

        public static void SetPartyKind(int i, UnitKind kind)
        {
            if (i < 0 || i >= PartySize) return;
            EnsureParty();
            Data.party[i].kind = (int)kind;
            Save();
        }

        public static void SetPartyBack(int i, bool back)
        {
            if (i < 0 || i >= PartySize) return;
            EnsureParty();
            Data.party[i].back = back;
            Save();
        }

        // ── 용사 강화 ───────────────────────────────────────────

        /// <summary>1부터 시작한다. 룬 레벨과 같은 규칙.</summary>
        public static int HeroLevel(UnitKind kind)
        {
            var list = Data.heroLevels;
            for (int i = 0; i < list.Count; i++)
                if (list[i].kind == (int)kind) return Mathf.Max(1, list[i].level);
            return 1;
        }

        /// <summary>파편을 쓰고 한 단계 올린다. 실패하면 아무것도 안 바뀐다.</summary>
        public static bool UpgradeHero(UnitKind kind)
        {
            int lv = HeroLevel(kind);
            if (lv >= HeroRoster.MaxLevel) return false;
            if (!TrySpend(HeroRoster.UpgradeCost(lv))) return false;

            var list = Data.heroLevels;
            for (int i = 0; i < list.Count; i++)
                if (list[i].kind == (int)kind) { list[i].level = lv + 1; Save(); return true; }

            list.Add(new HeroLevelData { kind = (int)kind, level = lv + 1 });
            Save();
            return true;
        }

        // ── 궤적 스킨 ───────────────────────────────────────────

        public static string SkinKey
        {
            get { return Data.skin; }
        }

        public static void SelectSkin(string key)
        {
            if (Data.skin == key) return;
            Data.skin = key;
            Save();
        }

        public static bool OwnsSkin(string key)
        {
            return Data.ownedSkins.Contains(key);
        }

        /// <summary>파편으로 산다. 살 수 없으면 false.</summary>
        public static bool BuySkin(string key, int cost)
        {
            if (OwnsSkin(key)) return false;
            if (!TrySpend(cost)) return false;

            Data.ownedSkins.Add(key);
            Save();
            return true;
        }

        // ── 룬 레벨 ─────────────────────────────────────────────

        public static int LevelOf(RuneType rune)
        {
            var list = Data.runeLevels;
            for (int i = 0; i < list.Count; i++)
                if (list[i].rune == (int)rune) return Mathf.Max(1, list[i].level);
            return 1;
        }

        public static void SetLevel(RuneType rune, int level)
        {
            level = Mathf.Clamp(level, 1, MaxRuneLevel);
            var list = Data.runeLevels;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].rune != (int)rune) continue;
                list[i].level = level;
                Save();
                return;
            }
            list.Add(new RuneLevelData { rune = (int)rune, level = level });
            Save();
        }

        // ── 문양 ────────────────────────────────────────────────

        public static List<GlyphData> Glyphs { get { return Data.glyphs; } }
        public static List<int> Equipped { get { return Data.equipped; } }

        public static GlyphData FindGlyph(int id)
        {
            var list = Data.glyphs;
            for (int i = 0; i < list.Count; i++)
                if (list[i].id == id) return list[i];
            return null;
        }

        public static GlyphData AddGlyph(GlyphData g)
        {
            g.id = Data.nextGlyphId++;
            Data.glyphs.Add(g);
            Save();
            return g;
        }

        /// <summary>분해. 문양을 파편으로 되돌린다.</summary>
        public static void Dismantle(int glyphId)
        {
            GlyphData g = FindGlyph(glyphId);
            if (g == null) return;

            Unequip(glyphId);
            Data.glyphs.Remove(g);
            Data.shards += GlyphTable.DismantleValue((GlyphRarity)g.rarity);
            Loadout.Invalidate();
            Save();
        }

        /// <summary>
        /// 룬 하나에 낄 수 있는 칸 수.
        ///
        /// **전체 공용 2~5칸에서 룬마다 3칸으로 바꿨다.** 예전에는 문양 하나가
        /// 모든 룬에 걸리거나(대상 없음) 한 룬에만 걸렸는데, 어느 쪽이든 낄 자리는
        /// 하나뿐이라 "무엇을 낄까"만 고르면 됐다.
        ///
        /// 이제는 **어느 룬을 키울까**를 고른다. 화살에 세 개를 몰아 주면 화살이
        /// 세지고 다른 룬은 맨몸이다. 칸이 룬마다 있으니 자리가 남아돌 것 같지만,
        /// 문양은 뽑아야 나오므로 실제로 조이는 건 칸이 아니라 문양 수다.
        /// </summary>
        public const int SlotsPerRune = 3;

        /// <summary>이 룬에 낀 문양 목록. 없으면 빈 목록을 만들어 돌려준다.</summary>
        public static List<int> EquippedOn(RuneType rune)
        {
            var list = Data.loadouts;
            for (int i = 0; i < list.Count; i++)
                if (list[i].rune == (int)rune) return list[i].glyphs;

            var made = new RuneLoadoutData { rune = (int)rune };
            list.Add(made);
            return made.glyphs;
        }

        /// <summary>어느 룬에 끼워져 있나. 안 끼워져 있으면 None.</summary>
        public static RuneType EquippedWhere(int glyphId)
        {
            var list = Data.loadouts;
            for (int i = 0; i < list.Count; i++)
                if (list[i].glyphs.Contains(glyphId)) return (RuneType)list[i].rune;
            return RuneType.None;
        }

        public static bool IsEquipped(int glyphId)
        {
            return EquippedWhere(glyphId) != RuneType.None;
        }

        /// <summary>
        /// 이 문양을 이 룬에 낄 수 있나.
        ///
        /// 대상이 정해진 문양은 그 룬에만 들어간다. 대상이 없는 문양(None)은
        /// 아무 룬에나 들어간다 — **그래서 대상 없는 쪽이 더 귀하다.**
        /// 예전에는 "모든 룬에 적용"이라 셌는데, 지금은 "어디든 낄 수 있다"라서 세다.
        /// </summary>
        public static bool CanEquipOn(int glyphId, RuneType rune)
        {
            GlyphData g = FindGlyph(glyphId);
            if (g == null || rune == RuneType.None) return false;
            return g.targetRune == (int)RuneType.None || g.targetRune == (int)rune;
        }

        /// <summary>이 룬에 낀다. 칸이 꽉 찼거나 못 끼는 문양이면 false.</summary>
        public static bool Equip(int glyphId, RuneType rune)
        {
            if (!CanEquipOn(glyphId, rune)) return false;

            // 다른 룬에 끼워져 있으면 먼저 뺀다. 하나를 두 곳에 낄 수는 없다.
            Unequip(glyphId);

            var slots = EquippedOn(rune);
            if (slots.Count >= SlotsPerRune) return false;

            slots.Add(glyphId);
            Loadout.Invalidate();
            Save();
            return true;
        }

        /// <summary>어디에 끼워져 있든 뺀다.</summary>
        public static bool Unequip(int glyphId)
        {
            bool removed = false;
            var list = Data.loadouts;
            for (int i = 0; i < list.Count; i++)
                if (list[i].glyphs.Remove(glyphId)) removed = true;

            if (removed)
            {
                Loadout.Invalidate();
                Save();
            }
            return removed;
        }

        /// <summary>
        /// 예전 형식(전체 공용 장착)을 룬별로 옮긴다.
        ///
        /// 대상이 정해진 문양은 그 룬으로 간다. 대상이 없는 문양은 **어디로 갈지
        /// 정할 근거가 없으므로** 장착을 풀어 둔다 — 임의로 꽂아 넣으면 플레이어가
        /// 의도하지 않은 구성이 되고, 그건 지워지는 것보다 알아채기 어렵다.
        /// 문양 자체는 그대로 있으니 다시 끼우면 된다.
        /// </summary>
        private static void MigrateEquipped()
        {
            if (_data.equipped.Count == 0) return;

            for (int i = 0; i < _data.equipped.Count; i++)
            {
                int id = _data.equipped[i];
                GlyphData g = FindGlyph(id);
                if (g == null || g.targetRune == (int)RuneType.None) continue;

                var slots = EquippedOn((RuneType)g.targetRune);
                if (slots.Count < SlotsPerRune && !slots.Contains(id)) slots.Add(id);
            }

            _data.equipped.Clear();
            Loadout.Invalidate();
        }

        /// <summary>사라진 문양 정리. 파일을 손으로 고쳤을 때 대비.</summary>
        public static void PruneEquipped()
        {
            var list = Data.loadouts;
            for (int i = 0; i < list.Count; i++)
            {
                var slots = list[i].glyphs;
                for (int k = slots.Count - 1; k >= 0; k--)
                    if (FindGlyph(slots[k]) == null) slots.RemoveAt(k);

                while (slots.Count > SlotsPerRune) slots.RemoveAt(slots.Count - 1);
            }

            Loadout.Invalidate();
        }

        public static void ResetAll()
        {
            _data = new PlayerFile();
            Revision++;
            Loadout.Invalidate();
            try
            {
                if (File.Exists(Path)) File.Delete(Path);
            }
            catch (Exception e)
            {
                Debug.LogWarning("플레이어 데이터 삭제 실패: " + e.Message);
            }
        }
    }
}
