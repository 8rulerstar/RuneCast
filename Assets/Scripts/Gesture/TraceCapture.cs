using System;
using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace RuneCast.Gesture
{
    /// <summary>
    /// 포인터 드래그로 궤적을 모은다.
    ///
    /// 입력 API 의존은 의도적으로 ReadPointer() 하나에만 가둬 뒀다.
    /// Input System이 켜진 프로젝트(Unity 6 기본)와 레거시 Input Manager 양쪽에서
    /// 그대로 돌아간다 — 어느 쪽이 활성인지는 Unity가 정의하는 심볼로 갈린다.
    /// </summary>
    public class TraceCapture : MonoBehaviour
    {
        [Header("수집")]
        [Tooltip("이 픽셀만큼 움직여야 점을 하나 추가한다. 손떨림 노이즈 제거용.")]
        public float minPointDistance = 8f;

        [Tooltip("이보다 짧은 궤적은 클릭으로 보고 버린다.")]
        public float minStrokeLength = 40f;

        [Header("시전 중 시간")]
        [Tooltip("드래그하는 동안의 timeScale. 0이면 완전 정지.")]
        [Range(0f, 1f)]
        public float castTimeScale = 0.2f;

        public bool slowMotionEnabled = true;

        /// <summary>
        /// 획을 받을 것인가.
        ///
        /// **시전만 막는 걸로는 부족했다.** 예전에는 일시정지나 결과 화면에서
        /// `castingEnabled`만 껐는데, 그러면 룬이 안 나갈 뿐 **선은 그대로
        /// 그려지고 잉크도 닳는다.** 메뉴를 띄워 놓고 화면을 문지르면 그림이
        /// 그려지는 게 보였다.
        ///
        /// 각인 모드는 시전을 멈추면서도 획은 받아야 하므로, 두 스위치를
        /// 따로 둔다.
        /// </summary>
        public bool captureEnabled = true;

        /// <summary>드래그 시작.</summary>
        public event Action StrokeBegan;

        /// <summary>점이 하나 추가됨. 인자는 화면 좌표 누적 리스트.</summary>
        public event Action<List<Vector2>> StrokeUpdated;

        /// <summary>드래그 종료. 인자는 화면 좌표 궤적. 너무 짧으면 호출되지 않는다.</summary>
        public event Action<List<Vector2>> StrokeCompleted;

        /// <summary>너무 짧아서 버려짐 — 궤적 렌더러가 지우도록.</summary>
        public event Action StrokeDiscarded;

        /// <summary>잉크를 다 써서 더는 안 그려짐. 소리·연출용으로 한 번만 발생한다.</summary>
        public event Action InkExhausted;

        private readonly List<Vector2> _screenPoints = new List<Vector2>(256);
        private bool _drawing;

        private float _inkUsed;
        private float _inkMax = float.MaxValue;
        private bool _inkOut;

        public bool IsDrawing { get { return _drawing; } }
        public List<Vector2> ScreenPoints { get { return _screenPoints; } }

        /// <summary>이번 획에 쓴 길이(픽셀).</summary>
        public float InkUsed { get { return _inkUsed; } }

        /// <summary>이번 획에 쓸 수 있는 길이(픽셀).</summary>
        public float InkMax { get { return _inkMax; } }

        public float InkRatio { get { return _inkMax <= 0f ? 0f : Mathf.Clamp01(_inkUsed / _inkMax); } }
        public bool InkOut { get { return _inkOut; } }

        /// <summary>
        /// 잉크 한도를 설정한다. 스테이지를 시작할 때 GameFlow가 넣어준다.
        /// float.MaxValue를 주면 사실상 무제한.
        /// </summary>
        public void SetInkLimit(float pixels)
        {
            _inkMax = pixels > 0f ? pixels : float.MaxValue;
        }

        private void Start()
        {
            // 픽셀 고정값이면 고해상도 폰에서 필터가 사실상 없는 것과 같아진다.
            // 손가락은 마우스보다 굵고 흔들려서 오히려 더 걸러야 하는데 반대로 동작한다.
            float scale = RuneCast.Core.UiScale.Factor;
            minPointDistance *= scale;
            minStrokeLength *= scale;

            // 터치는 접촉면이 넓어 미세한 떨림이 크게 잡힌다
            if (Application.isMobilePlatform) minPointDistance *= 1.5f;
        }

        // 화면 버튼이 차지한 자리. UI가 매 프레임 등록하고 여기서 매 프레임 비운다.
        //
        // **없으면 버튼을 누르는 동시에 획이 시작된다.** 시전 슬로우가 걸리고
        // 시작음이 난 뒤 너무 짧다며 버려지는데, 눌렀을 뿐인데 화면이 한 번
        // 덜컹하는 것처럼 느껴진다.
        private static readonly List<Rect> BlockedScreenRects = new List<Rect>();

        /// <summary>이 자리(실제 화면 픽셀)에서는 획을 시작하지 않는다.</summary>
        public static void BlockScreenRect(Rect screenRect)
        {
            BlockedScreenRects.Add(screenRect);
        }

        private static bool IsBlocked(Vector2 screenPos)
        {
            for (int i = 0; i < BlockedScreenRects.Count; i++)
                if (BlockedScreenRects[i].Contains(screenPos)) return true;
            return false;
        }

        private void Update()
        {
            if (!captureEnabled)
            {
                // 그리던 중에 꺼졌으면 확실히 끊는다. 안 그러면 슬로우가
                // 걸린 채로 굳고 선이 화면에 남는다.
                Cancel();
                return;
            }

            bool down, held, up;
            Vector2 pos;
            ReadPointer(out down, out held, out up, out pos);

            // 버튼 위에서 누른 것이면 획으로 치지 않는다. 이미 그리는 중이면
            // 막지 않는다 — 획이 버튼 위를 지나갈 수 있어야 한다.
            if (down && !_drawing && IsBlocked(pos)) down = false;

            // else-if 로 묶으면 안 된다. 한 프레임 안에서 누르고 떼는 짧은 클릭일 때
            // down 분기만 타고 up이 영영 처리되지 않아, 슬로우가 걸린 채로 굳는다.
            if (down) Begin(pos);
            if (_drawing && held) Continue(pos);
            if (_drawing && up) End();

            // UI가 매 프레임 다시 등록한다. 여기서 비워야 버튼이 사라진 뒤에도
            // 그 자리가 계속 막혀 있는 일이 없다.
            BlockedScreenRects.Clear();
        }

        private void OnDisable()
        {
            // 드래그 도중에 비활성화되면 슬로우가 걸린 채로 남는다
            Cancel();
        }

        private void OnApplicationFocus(bool focused)
        {
            // 창 밖에서 버튼을 떼면 up 이벤트가 안 들어온다
            if (!focused) Cancel();
        }

        /// <summary>
        /// 안드로이드에서는 OnApplicationFocus가 안 불릴 수 있다.
        /// 여기까지 걸어야 앱을 내렸다 올렸을 때 그리던 획이 확실히 끊긴다.
        /// </summary>
        private void OnApplicationPause(bool paused)
        {
            if (paused) Cancel();
        }

        private void Cancel()
        {
#if ENABLE_INPUT_SYSTEM
            // 잡고 있던 손가락도 놓는다. 이걸 안 놓으면 앱이 돌아온 뒤
            // 그 번호의 손가락이 다시 닿을 때까지 아무 입력도 안 잡힌다.
            _strokeTouchId = -1;
#endif

            if (!_drawing) return;

            _drawing = false;
            RestoreTime();
            _screenPoints.Clear();
            if (StrokeDiscarded != null) StrokeDiscarded();
        }

        // ── 입력 추상화 ──────────────────────────────────────────

#if ENABLE_INPUT_SYSTEM
        /// <summary>
        /// 지금 획을 그리고 있는 손가락. -1이면 아무도 안 잡고 있다.
        ///
        /// **왜 손가락을 하나 정해서 끝까지 따라가나:** 예전에는 primaryTouch를
        /// 썼는데, 그건 "제일 먼저 닿은 손가락"이다. 가로로 들고 두 손으로
        /// 쥐면 **받치던 엄지가 먼저 닿아 있고**, 검지로 그리는 획은 두 번째
        /// 손가락이라 아예 안 읽힌다. 화면을 만지는데 아무 일도 안 일어난다.
        /// </summary>
        private static int _strokeTouchId = -1;
#endif

        private static void ReadPointer(out bool down, out bool held, out bool up, out Vector2 pos)
        {
#if ENABLE_INPUT_SYSTEM
            // **터치를 먼저 본다.** 예전에는 마우스를 먼저 보고 있으면 곧장
            // 반환했는데, Input System은 실제 마우스가 없어도 Mouse.current를
            // 만들어 두는 경우가 있다(기기에 따라, 그리고 에디터의 터치 시뮬레이션).
            // 그러면 폰에서 **터치가 통째로 죽는다** — 손으로 그리는 게 전부인 게임에서.
            var touch = Touchscreen.current;
            if (touch != null && ReadTouch(touch, out down, out held, out up, out pos)) return;

            var mouse = Mouse.current;
            if (mouse != null)
            {
                down = mouse.leftButton.wasPressedThisFrame;
                held = mouse.leftButton.isPressed;
                up = mouse.leftButton.wasReleasedThisFrame;
                pos = mouse.position.ReadValue();
                return;
            }

            down = held = up = false;
            pos = Vector2.zero;
#else
            down = Input.GetMouseButtonDown(0);
            held = Input.GetMouseButton(0);
            up = Input.GetMouseButtonUp(0);
            pos = Input.mousePosition;
#endif
        }

#if ENABLE_INPUT_SYSTEM
        /// <summary>터치에서 읽었으면 true. 만지는 손가락이 없으면 false(마우스로 넘어간다).</summary>
        private static bool ReadTouch(Touchscreen touch,
            out bool down, out bool held, out bool up, out Vector2 pos)
        {
            down = held = up = false;
            pos = Vector2.zero;

            var all = touch.touches;

            // 이미 잡은 손가락이 있으면 그것만 따라간다. 그리는 도중에 다른
            // 손가락이 닿아도 획이 그리로 튀지 않는다 — 튀면 도형이 망가진다.
            if (_strokeTouchId >= 0)
            {
                for (int i = 0; i < all.Count; i++)
                {
                    var t = all[i];
                    if (t.touchId.ReadValue() != _strokeTouchId) continue;

                    pos = t.position.ReadValue();
                    up = t.press.wasReleasedThisFrame;
                    held = t.press.isPressed;
                    if (up || !held) _strokeTouchId = -1;
                    return true;
                }

                // 잡고 있던 손가락이 목록에서 사라졌다. 뗀 것으로 친다 —
                // 안 그러면 획이 영영 안 끝나서 잉크가 멎은 채로 남는다.
                _strokeTouchId = -1;
                up = true;
                return true;
            }

            // 이번 프레임에 새로 닿은 손가락을 잡는다.
            for (int i = 0; i < all.Count; i++)
            {
                var t = all[i];
                if (!t.press.wasPressedThisFrame) continue;

                _strokeTouchId = t.touchId.ReadValue();
                down = true;
                held = true;
                pos = t.position.ReadValue();
                return true;
            }

            // 닿아 있는 손가락이 하나도 없으면 터치 화면이 아닌 셈 치고 마우스로.
            for (int i = 0; i < all.Count; i++)
                if (all[i].press.isPressed) return true;

            return false;
        }
#endif

        private void Begin(Vector2 pos)
        {
            // 그리는 중에 또 Begin이 들어오면 잉크가 되감긴다
            if (_drawing) return;

            _drawing = true;
            _screenPoints.Clear();
            _screenPoints.Add(pos);

            _inkUsed = 0f;
            _inkOut = false;

            if (slowMotionEnabled) RuneCast.Core.TimeControl.SetCastScale(castTimeScale);

            // 슬로우가 걸리는 순간을 소리로 알린다. 시각적으로는 서서히 느려질 뿐이라
            // "이제 그리는 중"이라는 전환이 분명하지 않다.
            RuneCast.Core.AudioManager.Play(RuneCast.Core.Sfx.DrawBegin, 0.45f);

            if (StrokeBegan != null) StrokeBegan();
        }

        private void Continue(Vector2 pos)
        {
            if (_inkOut) return;

            Vector2 last = _screenPoints[_screenPoints.Count - 1];
            float step = Vector2.Distance(last, pos);
            if (step < minPointDistance) return;

            float remaining = _inkMax - _inkUsed;

            // 남은 잉크가 이번 구간보다 짧으면 딱 그만큼만 긋고 멈춘다.
            // 구간 단위로 자르면 한도가 들쭉날쭉해져서 "얼마나 그릴 수 있는지"를 못 배운다.
            if (step >= remaining)
            {
                if (remaining > 1f)
                {
                    _screenPoints.Add(last + (pos - last).normalized * remaining);
                    _inkUsed = _inkMax;
                    if (StrokeUpdated != null) StrokeUpdated(_screenPoints);
                }

                _inkOut = true;

                // 선이 멈춘 걸 눈으로 알아채기 전에 손이 먼저 계속 움직인다.
                // 소리가 있어야 "왜 안 그려지지"를 겪지 않는다.
                RuneCast.Core.AudioManager.Play(RuneCast.Core.Sfx.FailMana, 0.55f);

                if (InkExhausted != null) InkExhausted();
                return;
            }

            _inkUsed += step;
            _screenPoints.Add(pos);
            if (StrokeUpdated != null) StrokeUpdated(_screenPoints);
        }

        private void End()
        {
            _drawing = false;
            RestoreTime();

            // 이번 획이 잉크 부족으로 잘렸는지. RuneCaster가 발동 전에 확인한다.
            StrokeRanOutOfInk = _inkOut;

            float length = 0f;
            for (int i = 1; i < _screenPoints.Count; i++)
                length += Vector2.Distance(_screenPoints[i - 1], _screenPoints[i]);

            if (_screenPoints.Count >= 4 && length >= minStrokeLength)
            {
                if (StrokeCompleted != null) StrokeCompleted(_screenPoints);
            }
            else
            {
                _screenPoints.Clear();
                if (StrokeDiscarded != null) StrokeDiscarded();
            }
        }

        /// <summary>
        /// 마지막 획이 잉크를 다 써서 잘렸는가.
        ///
        /// 잘린 획을 그대로 시전시키면 안 된다. 4분의 3만 그려진 원도 원으로 인식되는데
        /// 바운딩박스는 큰 원 그대로라, **일부러 크게 그리다 잘리는 쪽이 이득**이 된다.
        /// 그러면 잉크 제한 자체가 무의미해진다.
        /// </summary>
        public bool StrokeRanOutOfInk { get; private set; }

        private void RestoreTime()
        {
            // 되돌릴 값을 기억하지 않는다. 요구를 거두면 TimeControl이 알아서 합친다.
            RuneCast.Core.TimeControl.SetCastScale(1f);
        }

        /// <summary>화면 좌표 궤적을 인식기용 GPoint 리스트로 변환.</summary>
        public static List<GPoint> ToGPoints(List<Vector2> screenPoints)
        {
            var pts = new List<GPoint>(screenPoints.Count);
            for (int i = 0; i < screenPoints.Count; i++)
                pts.Add(new GPoint(screenPoints[i].x, screenPoints[i].y));
            return pts;
        }
    }
}
