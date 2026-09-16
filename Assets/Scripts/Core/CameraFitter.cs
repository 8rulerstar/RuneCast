using UnityEngine;

namespace RuneCast.Core
{
    /// <summary>
    /// 화면비가 어떻든 전장이 다 보이게 카메라를 맞춘다.
    ///
    /// **문제:** orthographicSize는 세로 절반 크기라, 가로로 좁은 화면(4:3, 세로형 폰)에서는
    /// 보이는 가로 폭이 줄어든다. 적은 x=7 근처에서 걸어 들어오는데 4:3에서는
    /// 가로 절반이 5.6밖에 안 돼서 **적이 화면 밖에서 죽는다.**
    ///
    /// **해결:** "이만큼은 반드시 보여야 한다"는 가로 폭을 정하고, 화면비에 따라
    /// 세로를 역산한다. 넓은 화면에서는 기준 세로를 그대로 쓰고(양옆이 남을 뿐),
    /// 좁은 화면에서는 줌아웃해서 가로를 확보한다.
    ///
    /// 기기 회전이나 창 크기 변경도 잡아야 하므로 매 프레임 해상도를 확인한다 —
    /// 해상도 변경 콜백은 플랫폼마다 신뢰도가 다르다.
    /// </summary>
    public class CameraFitter : MonoBehaviour
    {
        /// <summary>이만큼(월드 유닛)은 가로로 반드시 보여야 한다. 적 스폰 지점 + 여유.</summary>
        public float requiredHalfWidth = 7.6f;

        /// <summary>넓은 화면에서 쓸 기준 세로 절반. 이보다 확대되지는 않는다.</summary>
        public float baseHalfHeight = 4.2f;

        /// <summary>연출에서 카메라를 잡으려면 이걸 쓴다. 씬에 하나뿐이다.</summary>
        public static CameraFitter Instance { get; private set; }

        /// <summary>
        /// 연출용 확대. 1이 기본이고 작을수록 당긴다.
        ///
        /// **화면비 보정과 곱해서 쓴다.** 확대를 orthographicSize에 직접 써 버리면
        /// 창 크기가 바뀌는 순간 Apply가 다시 돌면서 확대가 조용히 풀린다 —
        /// 값을 따로 들고 있다가 매번 같이 곱해야 그런 일이 없다.
        /// </summary>
        public float Zoom { get { return _zoom; } }

        private float _zoom = 1f;

        private Camera _cam;
        private int _lastW, _lastH;

        /// <summary>
        /// 확대율을 정한다. 0.7이면 세로로 30% 당긴다.
        ///
        /// **Refitted를 쏘지 않는다.** 그 이벤트를 받는 쪽(Bootstrap)은 배경 둘을
        /// Destroy하고 다시 만드는데, 확대는 부드럽게 밀어야 해서 매 프레임 값이
        /// 바뀐다 — 그대로 두면 1초 남짓 동안 배경을 수십 번 새로 만든다.
        ///
        /// 안 쏴도 되는 이유는 **확대가 1을 넘지 않기 때문이다.** 당기면 보이는
        /// 세상이 좁아질 뿐이라 이미 깔린 배경 안쪽에 머문다. 이 상한을 풀어
        /// 축소까지 허용하게 되면 그때는 알려야 한다.
        /// </summary>
        public void SetZoom(float zoom)
        {
            zoom = Mathf.Clamp(zoom, 0.3f, 1f);
            if (Mathf.Approximately(_zoom, zoom)) return;

            _zoom = zoom;
            Apply();
        }

        /// <summary>화면 크기가 바뀌어 카메라를 다시 맞췄을 때. 배경이 구독한다.</summary>
        public event System.Action Refitted;

        private void Awake()
        {
            Instance = this;
            _cam = GetComponent<Camera>();
            Apply();
        }

        private void Update()
        {
            if (Screen.width == _lastW && Screen.height == _lastH) return;
            Apply();
            if (Refitted != null) Refitted();
        }

        private void Apply()
        {
            _lastW = Screen.width;
            _lastH = Screen.height;

            if (_cam == null) _cam = GetComponent<Camera>();
            if (_cam == null) return;

            float aspect = _cam.aspect > 0.01f ? _cam.aspect : 1.778f;
            _cam.orthographicSize = Mathf.Max(baseHalfHeight, requiredHalfWidth / aspect) * _zoom;
        }

        public float HalfHeight { get { return _cam != null ? _cam.orthographicSize : baseHalfHeight; } }
        public float HalfWidth { get { return HalfHeight * (_cam != null ? _cam.aspect : 1.778f); } }
    }
}
