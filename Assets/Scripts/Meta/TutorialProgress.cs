using System;
using System.IO;
using UnityEngine;

namespace RuneCast.Meta
{
    /// <summary>튜토리얼 단계. 값은 저장 파일에 비트로 들어가므로 **번호를 바꾸면 안 된다.**</summary>
    public enum TutorialStep
    {
        /// <summary>첫 전투 — 드래그해서 도형을 그린다</summary>
        Draw = 0,

        /// <summary>첫 시전 성공 — 잘 그릴수록 세다</summary>
        Grade = 1,

        /// <summary>처음 잉크가 잘렸을 때 — 그릴 수 있는 길이에 한도가 있다</summary>
        Ink = 2,

        /// <summary>처음 마나가 모자랐을 때</summary>
        Mana = 3,

        /// <summary>첫 클리어 — 별이 무엇을 여는가</summary>
        Cleared = 4,

        /// <summary>망령이 처음 나왔을 때 — 평타가 안 통한다</summary>
        Wraith = 5,

        /// <summary>
        /// 프롤로그를 봤다. **맨 뒤에 붙인다** — 앞 번호를 건드리면 이미 저장된
        /// 비트마스크가 통째로 다른 뜻이 된다.
        ///
        /// 안내가 아니라 이야기지만 같은 파일에 둔다. 물어보는 게 똑같기 때문이다:
        /// "이걸 이미 봤는가". 파일을 나누면 초기화도 두 군데를 손대야 한다.
        /// </summary>
        Prologue = 6,
    }

    [Serializable]
    public class TutorialFile
    {
        /// <summary>본 단계의 비트마스크.</summary>
        public int seen;
    }

    /// <summary>
    /// 어떤 안내를 이미 봤는지 기억한다.
    ///
    /// **한 번만 보여주는 게 핵심이다.** 매번 뜨면 두 번째 판부터는 걷어내야 하는
    /// 방해물이 되고, 그때부터 플레이어는 안내를 읽지 않고 닫는 법부터 배운다.
    ///
    /// 진행(별)이나 소지품(파편)과 파일을 나눈 이유: 성격이 다르고,
    /// 밸런스를 보려고 진행만 지우는 일이 잦은데 그때 튜토리얼까지 다시 뜨면
    /// 매번 넘기는 게 일이 된다. 반대로 설정에서 전체 초기화를 하면 같이 지워진다.
    /// </summary>
    public static class TutorialProgress
    {
        private static TutorialFile _data;

        private static string Path
        {
            get { return System.IO.Path.Combine(Application.persistentDataPath, "tutorial.json"); }
        }

        private static TutorialFile Data
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
                if (json != null) _data = JsonUtility.FromJson<TutorialFile>(json);
            }
            catch (Exception e)
            {
                Debug.LogWarning("튜토리얼 기록 로드 실패: " + e.Message);
            }
            if (_data == null) _data = new TutorialFile();
        }

        private static void Save()
        {
            try
            {
                SaveFile.Write(Path, JsonUtility.ToJson(Data));
            }
            catch (Exception e)
            {
                Debug.LogWarning("튜토리얼 기록 저장 실패: " + e.Message);
            }
        }

        public static bool Seen(TutorialStep step)
        {
            return (Data.seen & (1 << (int)step)) != 0;
        }

        /// <summary>본 것으로 표시. 이미 본 단계면 파일을 다시 쓰지 않는다.</summary>
        public static void Mark(TutorialStep step)
        {
            if (Seen(step)) return;
            Data.seen |= 1 << (int)step;
            Save();
        }

        /// <summary>처음부터 다시 보기. 설정에서 부른다.</summary>
        public static void ResetAll()
        {
            _data = new TutorialFile();
            try
            {
                SaveFile.Delete(Path);
            }
            catch (Exception e)
            {
                Debug.LogWarning("튜토리얼 기록 삭제 실패: " + e.Message);
            }
        }
    }
}
