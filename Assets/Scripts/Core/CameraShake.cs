using UnityEngine;

namespace RuneCast.Core
{
    /// <summary>
    /// 카메라 흔들기. 잘 그렸을 때 "묵직하다"를 전달하는 가장 싼 수단이다.
    ///
    /// 원위치를 기억했다가 되돌리는 방식이라 카메라를 다른 데서 움직이면 충돌한다.
    /// 지금은 카메라가 고정이므로 문제없고, 이동 카메라가 생기면 오프셋을
    /// 부모 트랜스폼으로 분리해야 한다.
    /// </summary>
    public class CameraShake : MonoBehaviour
    {
        public static CameraShake Instance { get; private set; }

        private Vector3 _origin;
        private float _amount;
        private float _decay;

        private void Awake()
        {
            Instance = this;
            _origin = transform.localPosition;
        }

        public static void Shake(float amount, float duration = 0.25f)
        {
            if (Instance == null || amount <= 0f) return;

            // 더 센 흔들림이 진행 중이면 덮어쓰지 않는다
            if (amount < Instance._amount) return;

            Instance._amount = amount;
            Instance._decay = amount / Mathf.Max(duration, 0.05f);
        }

        private void LateUpdate()
        {
            if (_amount <= 0f)
            {
                transform.localPosition = _origin;
                return;
            }

            // 슬로우 중에도 같은 속도로 잦아들게 unscaled 사용
            _amount -= _decay * Time.unscaledDeltaTime;
            if (_amount < 0f) _amount = 0f;

            transform.localPosition = _origin + new Vector3(
                Random.Range(-_amount, _amount),
                Random.Range(-_amount, _amount),
                0f);
        }
    }
}
