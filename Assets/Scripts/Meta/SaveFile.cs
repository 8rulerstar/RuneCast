using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace RuneCast.Meta
{
    /// <summary>
    /// 저장 파일을 잃지 않게 쓰고 읽는다.
    ///
    /// **`File.WriteAllText`는 원자적이지 않다.** 파일을 먼저 비우고 내용을 채우므로,
    /// 그 사이에 앱이 죽으면 **잘리거나 텅 빈 파일**이 남는다. 다음 실행에서
    /// JSON 파싱이 실패하고, 그러면 코드는 조용히 새 데이터를 만든다 —
    /// 경고 로그 한 줄만 남기고 **모아온 것이 전부 사라진다.**
    ///
    /// PC에서는 거의 안 겪는다. 앱을 창 닫기로 끄니까. 그런데 모바일은 다르다:
    /// OS가 백그라운드 앱을 예고 없이 죽이고, 배터리가 나가고, 사용자가 앱을
    /// 위로 밀어 없앤다. 저장이 잦은 게임(업적 카운터, 뽑기, 강화)일수록
    /// 그 순간에 걸릴 확률이 올라간다.
    ///
    /// 출시하는 게임에서 이건 그냥 버그가 아니라 **되돌릴 수 없는 손실**이다.
    /// 플레이어는 왜 사라졌는지 알 수도 없다.
    ///
    /// 그래서 두 가지를 한다:
    ///
    /// **1. 옮겨 쓰기.** 임시 파일에 다 쓴 뒤 본체 자리로 옮긴다. 쓰다가 죽으면
    ///    임시 파일만 깨지고 본체는 지난 내용 그대로 멀쩡하다.
    ///
    /// **2. 직전 것을 남긴다.** 옮기기 전에 본체를 .bak으로 밀어둔다. 본체를 못
    ///    읽으면 .bak을 읽는다. 한 판 분량을 잃을 수는 있어도 전부를 잃지는 않는다.
    /// </summary>
    public static class SaveFile
    {
        /// <summary>
        /// 저장한다. 실패하면 false — 부르는 쪽이 알아야 할 때를 위해서다.
        /// </summary>
        public static bool Write(string path, string json)
        {
            if (string.IsNullOrEmpty(path) || json == null) return false;

            string tmp = path + ".tmp";
            string bak = path + ".bak";

            try
            {
                // 임시 파일에 먼저 다 쓴다. 여기서 죽으면 본체는 안 건드려졌다.
                //
                // Flush(true)로 디스크까지 밀어낸다. 이게 없으면 파일 시스템이
                // 내용을 아직 캐시에 들고 있는 채로 아래 이름 바꾸기가 끝날 수 있고,
                // 그 상태에서 전원이 끊기면 **이름만 바뀐 빈 파일**이 남는다.
                using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    byte[] bytes = new UTF8Encoding(false).GetBytes(json);
                    fs.Write(bytes, 0, bytes.Length);
                    fs.Flush(true);
                }

                // 지금 본체를 직전 것으로 밀어둔다
                if (File.Exists(path))
                {
                    if (File.Exists(bak)) File.Delete(bak);
                    File.Move(path, bak);
                }

                File.Move(tmp, path);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("저장 실패 (" + System.IO.Path.GetFileName(path) + "): " + e.Message);

                // 옮기다 만 흔적은 치운다. 다음 저장이 같은 이름을 다시 쓴다.
                try { if (File.Exists(tmp)) File.Delete(tmp); }
                catch { /* 지우는 것까지 실패하면 더 할 수 있는 게 없다 */ }

                return false;
            }
        }

        /// <summary>
        /// 읽는다. 본체가 없거나 깨졌으면 직전 것을 읽는다. 둘 다 안 되면 null.
        /// </summary>
        public static string Read(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;

            string text = TryRead(path);
            if (text != null) return text;

            string bak = path + ".bak";
            text = TryRead(bak);
            if (text == null) return null;

            // 직전 것으로 살아났다는 걸 남긴다. 조용히 넘어가면 플레이어가
            // "왜 한 판이 사라졌지" 하는 순간에 단서가 하나도 없다.
            Debug.LogWarning("저장 파일이 깨져 직전 것으로 되살립니다: "
                             + System.IO.Path.GetFileName(path));
            return text;
        }

        /// <summary>
        /// 지운다. **직전 것과 임시 파일까지 같이 지워야 한다.**
        /// 본체만 지우면 다음에 읽을 때 .bak이 되살아나서, 초기화를 눌렀는데
        /// 지난 진행이 그대로 돌아온다.
        /// </summary>
        public static void Delete(string path)
        {
            if (string.IsNullOrEmpty(path)) return;

            string[] all = { path, path + ".bak", path + ".tmp" };
            for (int i = 0; i < all.Length; i++)
            {
                try { if (File.Exists(all[i])) File.Delete(all[i]); }
                catch (Exception e)
                {
                    Debug.LogWarning("삭제 실패 (" + System.IO.Path.GetFileName(all[i]) + "): " + e.Message);
                }
            }
        }

        private static string TryRead(string path)
        {
            try
            {
                if (!File.Exists(path)) return null;

                string text = File.ReadAllText(path);

                // 잘린 파일은 대개 빈 문자열이거나 중간에서 끊긴 JSON이다.
                // 여기서 걸러야 부르는 쪽이 "빈 데이터"를 정상으로 착각하지 않는다.
                if (string.IsNullOrEmpty(text)) return null;

                text = text.Trim();
                if (text.Length < 2 || text[0] != '{' || text[text.Length - 1] != '}') return null;

                return text;
            }
            catch (Exception e)
            {
                Debug.LogWarning("읽기 실패 (" + System.IO.Path.GetFileName(path) + "): " + e.Message);
                return null;
            }
        }
    }
}
