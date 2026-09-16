using UnityEngine;

namespace RuneCast.Core
{
    /// <summary>
    /// 화면 전환 암전.
    ///
    /// 화면이 툭툭 바뀌는 것과 한 박자 어두워졌다 밝아지는 것의 차이는 크다.
    /// 전환이 없으면 "화면을 갈아끼웠다"로 보이고, 있으면 "장면이 넘어갔다"로 보인다.
    ///
    /// GUI.depth를 낮게 잡아 다른 모든 UI 위에 그린다 — IMGUI는 depth가 낮을수록 위다.
    /// 실시간(unscaled)으로 도므로 일시정지·히트스톱 중에도 정상 동작한다.
    /// </summary>
    public class ScreenFade : MonoBehaviour
    {
        public static ScreenFade Instance { get; private set; }

        private float _alpha;
        private float _target;
        private float _speed = 4f;

        private void Awake()
        {
            Instance = this;
        }

        /// <summary>어두워졌다가 다시 밝아진다. 화면을 바꾸는 순간에 부른다.</summary>
        public static void Flash(float speed = 5f)
        {
            if (Instance == null) return;
            Instance._alpha = 1f;
            Instance._target = 0f;
            Instance._speed = speed;
        }

        private void Update()
        {
            if (Mathf.Approximately(_alpha, _target)) return;
            _alpha = Mathf.MoveTowards(_alpha, _target, _speed * Time.unscaledDeltaTime);
        }

        private void OnGUI()
        {
            if (_alpha <= 0.001f) return;

            GUI.depth = -1000;
            GUI.color = new Color(0f, 0f, 0f, _alpha);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }
    }
}
