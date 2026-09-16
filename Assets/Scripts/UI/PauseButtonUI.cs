using UnityEngine;
using RuneCast.Core;
using RuneCast.Gesture;
using RuneCast.Meta;

namespace RuneCast.UI
{
    /// <summary>
    /// 전투 중 화면에 뜨는 일시정지 버튼.
    ///
    /// **이게 없으면 폰에서는 판을 멈출 수도 나갈 수도 없다.** 일시정지는 Esc
    /// 하나뿐이었는데 폰에는 키보드가 없다(`Keyboard.current`가 아예 null이다).
    /// 안드로이드의 뒤로가기 버튼도 그리로 들어오지 않으므로 눌러도 아무 일이
    /// 없다. 시작한 스테이지는 이기거나 져야만 벗어날 수 있었다.
    ///
    /// **왜 GestureHUD에 안 넣었나:** 그쪽 OnGUI는 Repaint에서만 그린다.
    /// 라벨과 텍스처뿐이라 그래도 되고 그래서 프레임당 두 번 도는 걸 막아뒀다.
    /// 거기에 버튼을 넣으면 클릭 이벤트를 못 받아 눌리지 않는다.
    ///
    /// **왼쪽 위 구석에 둔 이유:** 그림은 화면 가운데에 그린다. 오른쪽 위는
    /// 업적 알림이, 아래는 마나 게이지와 튜토리얼이 쓴다. 남는 자리이면서
    /// 그리는 손이 지나갈 일이 제일 적은 곳이다.
    /// </summary>
    public class PauseButtonUI : MonoBehaviour
    {
        public GameFlow flow;

        private const float Size = 40f;
        private const float Margin = 12f;

        private GUIStyle _glyph;

        private void OnGUI()
        {
            if (flow == null || flow.State != GameState.Battle || flow.Paused) return;

            EnsureStyles();

            UiScale.Begin();

            var r = new Rect(UiScale.L + Margin, UiScale.T + Margin, Size, Size);

            // **누르는 자리에서는 그림이 시작되지 않아야 한다.** 안 막으면 버튼을
            // 누르는 동시에 획이 시작돼서, 시전 슬로우가 걸리고 시작음이 난 뒤
            // 너무 짧다며 버려진다. 눌렀는데 화면이 한 번 덜컹하는 것처럼 느껴진다.
            //
            // Repaint에서만 등록한다. OnGUI는 한 프레임에 여러 번 도는데
            // 매번 넣으면 같은 사각형이 프레임마다 서너 개씩 쌓인다.
            if (Event.current.type == EventType.Repaint)
                TraceCapture.BlockScreenRect(UiScale.ToScreenRect(r));

            bool hit = UiSkin.DrawButton(r, "II", _glyph);

            UiScale.End();

            if (!hit) return;

            AudioManager.Play(Sfx.UiClick, 0.5f, 1.45f);
            flow.SetPaused(true);
        }

        private void EnsureStyles()
        {
            if (_glyph != null) return;

            // 아이콘 대신 글자 두 개. 스프라이트를 하나 더 들이지 않아도 되고,
            // 픽셀 글꼴이라 오히려 나머지 UI와 결이 맞는다.
            _glyph = new GUIStyle(GUI.skin.label)
            {
                fontSize = UiSkin.Text.Mid,
                alignment = TextAnchor.MiddleCenter,
            };
            UiSkin.ApplyFont(_glyph);
        }
    }
}
