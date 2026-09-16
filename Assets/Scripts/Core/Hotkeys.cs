using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace RuneCast.Core
{
    public enum Hotkey
    {
        Tab,
        Num1,
        Num2,
        Num3,
        Num4,
        Num5,
        Num6,
        Num7,
        R,
        M,
        F1,
        Escape,
    }

    /// <summary>
    /// 키 입력 호환 계층. TraceCapture와 같은 이유로 여기 한 곳에만 가둔다 —
    /// Input System이 켜진 프로젝트에서는 레거시 Input.GetKeyDown이 예외를 던지므로
    /// 두 방식을 모두 지원해야 어느 프로젝트 설정에서도 돌아간다.
    /// </summary>
    public static class Hotkeys
    {
#if ENABLE_INPUT_SYSTEM
        public static bool Down(Hotkey key)
        {
            var kb = Keyboard.current;
            if (kb == null) return false;

            switch (key)
            {
                case Hotkey.Tab: return kb.tabKey.wasPressedThisFrame;
                case Hotkey.Num1: return kb.digit1Key.wasPressedThisFrame;
                case Hotkey.Num2: return kb.digit2Key.wasPressedThisFrame;
                case Hotkey.Num3: return kb.digit3Key.wasPressedThisFrame;
                case Hotkey.Num4: return kb.digit4Key.wasPressedThisFrame;
                case Hotkey.Num5: return kb.digit5Key.wasPressedThisFrame;
                case Hotkey.Num6: return kb.digit6Key.wasPressedThisFrame;
                case Hotkey.Num7: return kb.digit7Key.wasPressedThisFrame;
                case Hotkey.R: return kb.rKey.wasPressedThisFrame;
                case Hotkey.M: return kb.mKey.wasPressedThisFrame;
                case Hotkey.F1: return kb.f1Key.wasPressedThisFrame;
                case Hotkey.Escape: return kb.escapeKey.wasPressedThisFrame;
                default: return false;
            }
        }
#else
        public static bool Down(Hotkey key)
        {
            KeyCode code;
            switch (key)
            {
                case Hotkey.Tab: code = KeyCode.Tab; break;
                case Hotkey.Num1: code = KeyCode.Alpha1; break;
                case Hotkey.Num2: code = KeyCode.Alpha2; break;
                case Hotkey.Num3: code = KeyCode.Alpha3; break;
                case Hotkey.Num4: code = KeyCode.Alpha4; break;
                case Hotkey.Num5: code = KeyCode.Alpha5; break;
                case Hotkey.Num6: code = KeyCode.Alpha6; break;
                case Hotkey.Num7: code = KeyCode.Alpha7; break;
                case Hotkey.R: code = KeyCode.R; break;
                case Hotkey.M: code = KeyCode.M; break;
                case Hotkey.F1: code = KeyCode.F1; break;
                case Hotkey.Escape: code = KeyCode.Escape; break;
                default: return false;
            }
            return Input.GetKeyDown(code);
        }
#endif
    }
}
