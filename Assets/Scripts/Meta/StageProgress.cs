using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace RuneCast.Meta
{
    [Serializable]
    public class StageRecord
    {
        public int stageId;
        public int stars;
    }

    [Serializable]
    public class ProgressFile
    {
        public List<StageRecord> records = new List<StageRecord>();
    }

    /// <summary>
    /// 스테이지 진행 저장.
    ///
    /// PlayerPrefs가 아니라 JSON 파일인 이유: 무엇이 저장됐는지 눈으로 열어볼 수 있고,
    /// 테스트하다 꼬였을 때 파일 하나만 지우면 된다. 커스텀 룬 저장과 같은 방식.
    ///
    /// **별은 낮은 기록으로 덮어쓰지 않는다.** 한 번 3별을 받은 스테이지를 대충 다시 깼다고
    /// 기록이 깎이면 재도전을 피하게 된다.
    /// </summary>
    public static class StageProgress
    {
        public const int MaxStars = 3;

        private static ProgressFile _data;

        private static string Path
        {
            get { return System.IO.Path.Combine(Application.persistentDataPath, "stage_progress.json"); }
        }

        public static string FilePath { get { return Path; } }

        private static ProgressFile Data
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
                if (json != null) _data = JsonUtility.FromJson<ProgressFile>(json);
            }
            catch (Exception e)
            {
                // 저장 파일이 깨졌다고 게임이 안 켜지면 안 된다
                Debug.LogWarning("진행 상황 로드 실패: " + e.Message);
            }

            if (_data == null) _data = new ProgressFile();
            if (_data.records == null) _data.records = new List<StageRecord>();
        }

        /// <summary>진행이 바뀔 때마다 오른다. PlayerData.Revision과 같은 용도.</summary>
        public static int Revision { get; private set; }

        private static void Save()
        {
            Revision++;

            try
            {
                SaveFile.Write(Path, JsonUtility.ToJson(Data, true));
            }
            catch (Exception e)
            {
                Debug.LogWarning("진행 상황 저장 실패: " + e.Message);
            }
        }

        public static int StarsOf(int stageId)
        {
            var list = Data.records;
            for (int i = 0; i < list.Count; i++)
                if (list[i].stageId == stageId) return list[i].stars;
            return 0;
        }

        public static bool IsCleared(int stageId)
        {
            return StarsOf(stageId) > 0;
        }

        /// <summary>이전 스테이지를 깼거나 첫 스테이지면 열린다.</summary>
        public static bool IsUnlocked(int stageId)
        {
            var all = StageDatabase.All;
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i].Id != stageId) continue;
                if (i == 0) return true;
                return IsCleared(all[i - 1].Id);
            }
            return false;
        }

        /// <summary>기존 기록보다 높을 때만 갱신. 갱신됐으면 true.</summary>
        public static bool Record(int stageId, int stars)
        {
            stars = Mathf.Clamp(stars, 0, MaxStars);
            if (stars <= 0) return false;

            var list = Data.records;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].stageId != stageId) continue;
                if (stars <= list[i].stars) return false;

                list[i].stars = stars;
                Save();
                return true;
            }

            list.Add(new StageRecord { stageId = stageId, stars = stars });
            Save();
            return true;
        }

        public static int TotalStars
        {
            get
            {
                int n = 0;
                var list = Data.records;
                for (int i = 0; i < list.Count; i++) n += list[i].stars;
                return n;
            }
        }

        public static int MaxPossibleStars
        {
            get { return StageDatabase.All.Count * MaxStars; }
        }

        public static void ResetAll()
        {
            _data = new ProgressFile();
            try
            {
                SaveFile.Delete(Path);
            }
            catch (Exception e)
            {
                Debug.LogWarning("진행 상황 삭제 실패: " + e.Message);
            }
        }

        /// <summary>
        /// 생존한 아군 수로 별을 매긴다.
        ///
        /// 점수제(앵그리버드식)가 아니라 생존 기준인 이유: 이 게임에서 플레이어가
        /// 실제로 조절하는 건 "아군을 지켰는가"다. 처치 속도로 매기면
        /// 룬을 아끼지 않고 계속 쏟아붓는 쪽이 유리해져서 마나 설계와 어긋난다.
        /// </summary>
        public static int StarsFor(int heroesAlive, int heroesTotal)
        {
            if (heroesAlive <= 0) return 0;
            if (heroesAlive >= heroesTotal) return 3;
            if (heroesAlive * 2 >= heroesTotal) return 2;
            return 1;
        }
    }
}
