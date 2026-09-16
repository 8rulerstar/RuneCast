using UnityEngine;

namespace RuneCast.Core
{
    /// <summary>
    /// IMGUI를 가상 해상도 위에서 그리게 한다.
    ///
    /// **문제:** IMGUI의 글자 크기·좌표는 전부 실제 픽셀이다. 1080p 모니터에 맞춰 잡은
    /// 13px 글씨는 1080×2400 폰에서 물리적으로 읽을 수 없는 크기가 된다.
    /// 화면이 커질수록 UI가 작아지는 건 데스크톱에서도 이미 어긋난 동작이었다.
    ///
    /// **해결:** GUI.matrix에 배율을 걸고, 레이아웃은 항상 고정된 가상 크기(VirtualW/H)로 짠다.
    /// 이러면 좌표·글자·클릭 판정이 한꺼번에 같이 커져서 코드에 배율이 흩어지지 않는다.
    ///
    /// 쓰는 법: OnGUI 맨 앞에서 Begin(), 맨 뒤에서 End().
    /// 그 사이에서는 Screen.width/height 대신 **W / H**를 쓴다.
    /// </summary>
    public static class UiScale
    {
        /// <summary>
        /// 레이아웃을 짜는 기준 높이. 이 높이에서 배율 1이 된다.
        ///
        /// **900에서 780으로 내렸다(= UI가 15% 커진다).** 화면이 여러모로 비어
        /// 보였는데, 원인은 요소 하나하나가 아니라 **전부가 조금씩 작았다는 것**이다.
        /// 버튼과 글씨를 한 곳씩 키우면 서로의 비례가 깨지고 스무 곳을 고쳐도
        /// 여전히 빈다 — 기준을 내리면 글자·버튼·판·여백이 같은 비율로 같이 커진다.
        ///
        /// **대가는 세로 여유다.** 가상 높이가 900에서 780으로 줄어 배치할 자리가
        /// 120만큼 사라진다. 이 값을 또 내리려면 스테이지 선택·타이틀·장 목록의
        /// 세로 산수를 다시 재야 한다 — 넘치면 하단 내비가 카드에 가린다.
        /// </summary>
        public const float ReferenceHeight = 780f;

        private static Matrix4x4 _saved;

        /// <summary>
        /// 화면 높이 기준 배율. 너무 작아지거나 커지지 않게 자른다 —
        /// 3을 넘으면 UI가 화면을 다 먹는다.
        ///
        /// **하한을 0.55에서 0.4로 내렸다.** 하한에 걸리면 가상 높이가 기준보다
        /// 커져서, 위에서 시작하는 목록이 아래 고정 버튼을 뚫고 겹친다.
        /// 0.55일 때 itch.io 기본 임베드(640×360)가 정확히 그 경우였다.
        ///
        /// 기준 높이를 780으로 내린 지금은 **실제 312px 아래에서만 하한에 걸린다**
        /// (780 × 0.4). 360px짜리 임베드도 배율 0.46으로 가상 780이 그대로 나오므로
        /// 하한을 안 탄다. 폰은 실제 높이가 1000px을 넘어 애초에 무관하다.
        ///
        /// 하한이 있어야 하는 이유는 그대로다 — **작아서 찡그리는 것이 겹쳐서
        /// 못 읽는 것보다 낫다.**
        /// </summary>
        public static float Factor
        {
            get { return Mathf.Clamp(Screen.height / ReferenceHeight, 0.4f, 3f); }
        }

        /// <summary>가상 화면 크기. 레이아웃은 전부 이 값을 기준으로 짠다.</summary>
        public static float W { get { return Screen.width / Factor; } }
        public static float H { get { return Screen.height / Factor; } }

        public static void Begin()
        {
            _saved = GUI.matrix;
            float s = Factor;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(s, s, 1f));
        }

        public static void End()
        {
            GUI.matrix = _saved;
        }

        /// <summary>실제 화면 픽셀 좌표를 가상 좌표로. WorldToScreenPoint 결과를 쓸 때 필요하다.</summary>
        public static Vector2 ToVirtual(Vector2 screenPixels)
        {
            float s = Factor;
            return new Vector2(screenPixels.x / s, screenPixels.y / s);
        }

        /// <summary>
        /// 가상 GUI 좌표 한 점을 실제 화면 픽셀로. **Y가 뒤집힌다.**
        ///
        /// `Camera.ScreenToWorldPoint`에 넘기려면 이 변환이 필요하다 —
        /// 안 뒤집으면 화면 위에서 끈 카드가 전장 아래쪽에 떨어진다.
        /// </summary>
        public static Vector2 ToScreenPoint(Vector2 virtualPoint)
        {
            float s = Factor;
            return new Vector2(virtualPoint.x * s, Screen.height - virtualPoint.y * s);
        }

        /// <summary>
        /// 가상 좌표 사각형을 실제 화면 픽셀로. **Y가 뒤집힌다.**
        ///
        /// GUI는 좌상단이 원점이고 터치·마우스 좌표는 좌하단이 원점이다.
        /// 이 둘을 그냥 곱해서 비교하면 화면 위쪽 버튼을 아래쪽에서 눌러야 하는
        /// 상태가 되는데, 화면 한가운데 근처에서는 그럭저럭 맞아 보여서
        /// 한참 뒤에야 드러난다.
        /// </summary>
        public static Rect ToScreenRect(Rect virtualRect)
        {
            float s = Factor;
            float w = virtualRect.width * s;
            float h = virtualRect.height * s;
            float x = virtualRect.x * s;
            float y = Screen.height - (virtualRect.y * s + h);
            return new Rect(x, y, w, h);
        }

        // 가장자리에 붙이는 UI는 0/W/H가 아니라 아래 네 값을 기준으로 잡는다.
        // 데스크톱에서는 안전 영역이 화면 전체라 L=0, T=0, R=W, B=H가 되어 차이가 없다.

        /// <summary>안전 영역 왼쪽 끝.</summary>
        public static float L { get { return SafeArea.x; } }

        /// <summary>안전 영역 위쪽 끝.</summary>
        public static float T { get { return SafeArea.y; } }

        /// <summary>안전 영역 오른쪽 끝.</summary>
        public static float R { get { return SafeArea.xMax; } }

        /// <summary>안전 영역 아래쪽 끝.</summary>
        public static float B { get { return SafeArea.yMax; } }

        /// <summary>
        /// 노치·홈 인디케이터를 피한 안전 영역 (가상 좌표, 좌상단 원점).
        /// UI를 화면 가장자리에 붙일 때는 0이 아니라 이 값을 시작점으로 써야 한다.
        /// </summary>
        public static Rect SafeArea
        {
            get
            {
                Rect sa = Screen.safeArea; // 좌하단 원점, 실제 픽셀
                float s = Factor;

                float x = sa.x / s;
                float w = sa.width / s;

                // GUI는 좌상단 원점이라 y를 뒤집는다
                float y = (Screen.height - sa.yMax) / s;
                float h = sa.height / s;

                return new Rect(x, y, w, h);
            }
        }
    }
}
