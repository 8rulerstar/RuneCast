using UnityEngine;

namespace RuneCast.Core
{
    /// <summary>
    /// Time.timeScale을 쓰는 유일한 곳.
    ///
    /// 예전에는 TraceCapture가 직접 값을 저장했다 되돌렸다. 거기에 히트스톱을 얹으면
    /// 서로의 값을 "원래 값"으로 착각해서 덮어쓴다 — 슬로우가 영영 안 풀리는 버그가
    /// 정확히 그 구조에서 나왔었다.
    ///
    /// 이제 요청자들은 자기 요구값만 말하고, 최종 timeScale은 여기서 **둘 중 더 느린 쪽**으로
    /// 정한다. 되돌릴 값을 아무도 기억하지 않으므로 어긋날 수가 없다.
    /// </summary>
    public class TimeControl : MonoBehaviour
    {
        private static float _castScale = 1f;
        private static float _hitStopScale = 1f;
        private static float _hitStopUntil;
        private static bool _paused;

        /// <summary>일시정지. 다른 어떤 요구보다 우선한다.</summary>
        public static void SetPaused(bool paused)
        {
            _paused = paused;
        }

        public static bool IsPaused { get { return _paused; } }

        /// <summary>시전 슬로우. 1이면 해제.</summary>
        public static void SetCastScale(float scale)
        {
            _castScale = Mathf.Clamp01(scale);
        }

        /// <summary>
        /// 타격 순간의 정지. 더 센(더 느린) 요청이 우선이고,
        /// 지속 시간은 실시간 기준이라 슬로우 중에도 같은 길이로 끝난다.
        /// </summary>
        public static void HitStop(float scale, float duration)
        {
            float until = Time.unscaledTime + duration;

            // 이미 더 강한 정지가 걸려 있으면 시간만 늘린다
            if (Time.unscaledTime < _hitStopUntil && _hitStopScale <= scale)
            {
                _hitStopUntil = Mathf.Max(_hitStopUntil, until);
                return;
            }

            _hitStopScale = Mathf.Clamp01(scale);
            _hitStopUntil = until;
        }

        public static void ResetAll()
        {
            _castScale = 1f;
            _hitStopScale = 1f;
            _hitStopUntil = 0f;
            _paused = false;
            Time.timeScale = 1f;
        }

        private void LateUpdate()
        {
            if (_paused)
            {
                Time.timeScale = 0f;
                return;
            }

            float hit = Time.unscaledTime < _hitStopUntil ? _hitStopScale : 1f;
            Time.timeScale = Mathf.Min(_castScale, hit);
        }

        private void OnDisable()
        {
            // 이 컴포넌트가 꺼진 채로 timeScale이 낮게 남으면 게임이 멎은 것처럼 보인다
            Time.timeScale = 1f;
        }
    }
}
