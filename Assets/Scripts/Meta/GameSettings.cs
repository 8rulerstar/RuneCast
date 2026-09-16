using System;
using System.IO;
using UnityEngine;

namespace RuneCast.Meta
{
    [Serializable]
    public class SettingsFile
    {
        public int language = -1;   // -1이면 아직 정한 적 없음 → 영어로 시작
        public float bgmVolume = 0.3f;
        public float sfxVolume = 0.65f;
        public float castTimeScale = 0.2f;
        public bool fullscreen = true;
        public bool showDebugPanel;

        /// <summary>개발용 — 잉크 한도를 무시한다. 도형을 마음껏 시험할 때.</summary>
        public bool infiniteInk;
    }

    /// <summary>
    /// 설정. 값이 바뀌면 즉시 저장한다 —
    /// 설정 화면에 "적용" 버튼을 두면 안 누르고 나가는 사람이 반드시 생긴다.
    /// </summary>
    public static class GameSettings
    {
        private static SettingsFile _data;

        private static string Path
        {
            get { return System.IO.Path.Combine(Application.persistentDataPath, "settings.json"); }
        }

        private static SettingsFile Data
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
                if (json != null) _data = JsonUtility.FromJson<SettingsFile>(json);
            }
            catch (Exception e)
            {
                Debug.LogWarning("설정 로드 실패: " + e.Message);
            }

            if (_data == null) _data = new SettingsFile();

            // 첫 실행은 **영어.** 시스템 언어를 따르던 것을 바꿨다 — 주 배포처(itch.io)가
            // 영어권이고, 타이틀 화면에 언어 버튼이 생겨서 한국어 사용자가 되찾는 비용이
            // 클릭 한 번이다. 반대(영어 사용자가 한국어를 만나는 것)는 메뉴를 못 읽어서
            // 설정까지 가는 길 자체를 못 찾는다.
            if (_data.language < 0)
                _data.language = (int)Lang.English;

            Loc.Current = (Lang)Mathf.Clamp(_data.language, 0, 1);
        }

        private static void Save()
        {
            try
            {
                SaveFile.Write(Path, JsonUtility.ToJson(Data, true));
            }
            catch (Exception e)
            {
                Debug.LogWarning("설정 저장 실패: " + e.Message);
            }
        }

        public static Lang Language
        {
            get { return (Lang)Mathf.Clamp(Data.language, 0, 1); }
            set
            {
                Data.language = (int)value;
                Loc.Current = value;
                Save();
            }
        }

        public static float BgmVolume
        {
            get { return Data.bgmVolume; }
            set { Data.bgmVolume = Mathf.Clamp01(value); Apply(); Save(); }
        }

        public static float SfxVolume
        {
            get { return Data.sfxVolume; }
            set { Data.sfxVolume = Mathf.Clamp01(value); Apply(); Save(); }
        }

        /// <summary>낮을수록 그리는 동안 더 느려진다. 손이 느린 사람을 위한 난이도 손잡이.</summary>
        public static float CastTimeScale
        {
            get { return Data.castTimeScale; }
            set { Data.castTimeScale = Mathf.Clamp(value, 0.05f, 1f); Apply(); Save(); }
        }

        public static bool Fullscreen
        {
            get { return Data.fullscreen; }
            set { Data.fullscreen = value; Screen.fullScreen = value; Save(); }
        }

        /// <summary>
        /// 개발용 잉크 무한.
        ///
        /// 잉크 제한은 이 게임의 핵심 제약이라 평소엔 켜면 안 된다. 다만 새 도형을
        /// 시험하거나 큰 룬의 연출을 볼 때는 제한이 방해만 된다.
        /// 진단 패널과 같은 자리(설정 아래쪽)에 둔다 — 개발용이라는 게 보이는 자리다.
        /// </summary>
        public static bool InfiniteInk
        {
            get { return Data.infiniteInk; }
            set { Data.infiniteInk = value; Save(); }
        }

        public static bool ShowDebugPanel
        {
            get { return Data.showDebugPanel; }
            set { Data.showDebugPanel = value; Apply(); Save(); }
        }

        /// <summary>설정값을 실제 시스템에 반영한다. 값이 바뀔 때마다 호출된다.</summary>
        public static void Apply()
        {
            var audio = Core.AudioManager.Instance;
            if (audio != null)
            {
                audio.bgmVolume = Data.bgmVolume;
                audio.sfxVolume = Data.sfxVolume;
                audio.RefreshBgmVolume();
            }

            var capture = UnityEngine.Object.FindAnyObjectByType<Gesture.TraceCapture>();
            if (capture != null) capture.castTimeScale = Data.castTimeScale;

            var hud = UnityEngine.Object.FindAnyObjectByType<UI.GestureHUD>();
            if (hud != null) hud.showDebugPanel = Data.showDebugPanel;
        }
    }
}
