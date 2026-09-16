using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace RuneCast.Gesture
{
    [Serializable]
    public class CustomRuneData
    {
        public string name;
        public int type;   // RuneType
        public float[] xs;
        public float[] ys;
    }

    [Serializable]
    public class CustomRuneFile
    {
        public List<CustomRuneData> runes = new List<CustomRuneData>();
    }

    /// <summary>
    /// 사용자가 각인한 룬을 디스크에 보관한다.
    ///
    /// 샘플 3장을 "평균 내지 않고" 3개의 템플릿으로 각각 등록하는 게 핵심이다.
    /// 점군은 시작점이 매번 달라서 인덱스별 평균을 내면 형태가 뭉개진다.
    /// $P는 한 클래스에 템플릿이 여럿인 걸 원래 전제하므로, 그대로 넣는 쪽이
    /// 코드도 짧고 인식률도 높다 — 그리는 버릇의 편차까지 같이 학습된다.
    /// </summary>
    public static class CustomRuneStore
    {
        private static string Path
        {
            get { return System.IO.Path.Combine(Application.persistentDataPath, "custom_runes.json"); }
        }

        public static string FilePath { get { return Path; } }
        public static int LoadedCount { get; private set; }

        public static void LoadIntoLibrary()
        {
            LoadedCount = 0;
            try
            {
                string json = RuneCast.Meta.SaveFile.Read(Path);
                if (json == null) return;

                var file = JsonUtility.FromJson<CustomRuneFile>(json);
                if (file == null || file.runes == null) return;

                for (int i = 0; i < file.runes.Count; i++)
                {
                    var d = file.runes[i];
                    if (d.xs == null || d.ys == null || d.xs.Length != d.ys.Length || d.xs.Length < 4) continue;

                    var pts = new List<GPoint>(d.xs.Length);
                    for (int k = 0; k < d.xs.Length; k++) pts.Add(new GPoint(d.xs[k], d.ys[k]));

                    RuneTemplateLibrary.Add(new RuneTemplate(d.name, (RuneType)d.type, pts));
                    LoadedCount++;
                }
            }
            catch (Exception e)
            {
                // 저장 파일이 깨졌다고 게임이 안 켜지면 안 된다
                Debug.LogWarning("커스텀 룬 로드 실패: " + e.Message);
            }
        }

        public static void Append(RuneType type, List<GPoint> points, string label)
        {
            try
            {
                CustomRuneFile file = null;
                string json = RuneCast.Meta.SaveFile.Read(Path);
                if (json != null) file = JsonUtility.FromJson<CustomRuneFile>(json);
                if (file == null) file = new CustomRuneFile();
                if (file.runes == null) file.runes = new List<CustomRuneData>();

                var d = new CustomRuneData
                {
                    name = label,
                    type = (int)type,
                    xs = new float[points.Count],
                    ys = new float[points.Count],
                };
                for (int i = 0; i < points.Count; i++)
                {
                    d.xs[i] = points[i].X;
                    d.ys[i] = points[i].Y;
                }

                file.runes.Add(d);
                RuneCast.Meta.SaveFile.Write(Path, JsonUtility.ToJson(file, true));

                RuneTemplateLibrary.Add(new RuneTemplate(label, type, points));
                LoadedCount++;
            }
            catch (Exception e)
            {
                Debug.LogWarning("커스텀 룬 저장 실패: " + e.Message);
            }
        }

        public static void DeleteAll()
        {
            try
            {
                RuneCast.Meta.SaveFile.Delete(Path);
            }
            catch (Exception e)
            {
                Debug.LogWarning("커스텀 룬 삭제 실패: " + e.Message);
            }
            LoadedCount = 0;
            RuneTemplateLibrary.Reset();
        }
    }
}
