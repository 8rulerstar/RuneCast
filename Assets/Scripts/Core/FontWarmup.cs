using UnityEngine;

namespace RuneCast.Core
{
    /// <summary>
    /// 쓰는 글자를 전부 미리 굽고, 보간 없이 그리게 한다.
    ///
    /// **아틀라스가 도중에 다시 짜이면 글자가 뒤바뀐다.** Unity의 동적 글꼴은
    /// 아틀라스가 꽉 차면 비우고 그 프레임에 요청된 것만 다시 굽는다. 그게
    /// OnGUI 도중에 일어나면 이미 그려진 글자들은 옛 좌표를 가리키고, 그러면
    /// 네모도 빈칸도 아닌 **멀쩡하지만 틀린 글자**가 나온다.
    ///
    /// **실측으로 잡았다.** 게임 안에서 진단을 찍어 보니 2048×2048(최대 크기)
    /// 아틀라스에 글자가 3002개 들어 있었다. 시작 시점의 글리프 좌표는 맞는데
    /// 화면은 틀렸으니, 어긋나는 지점은 그리는 도중일 수밖에 없었다.
    /// 크기를 3단으로 줄이자 1830개로 떨어졌고 글자가 바르게 나왔다.
    ///
    /// **에디터의 아틀라스는 Play를 껐다 켜도 안 비워진다.** 이 수정이 먹었는지
    /// 확인하려면 Unity를 완전히 종료했다 켜야 한다 — 그걸 몰라서 고친 뒤에도
    /// 한 번 더 "그대로다"라는 결론을 낼 뻔했다.
    ///
    /// 그래서 두 가지를 같이 한다:
    ///  · 크기 사다리를 5단에서 3단으로 줄여 굽는 양을 절반 이하로
    ///    (<see cref="UiSkin.Text"/>)
    ///  · 시작할 때 전부 구워서 그 뒤로 새로 구울 것이 없게
    ///
    /// 필터를 Point로 두는 건 별개다. UiScale이 GUI 행렬을 화면 높이에 맞춰
    /// 늘리므로 배율이 1이 아니면 구운 그림이 늘거나 줄고, 그때 보간이 끼면
    /// 픽셀 글꼴의 1픽셀 획이 회색으로 번진다.
    /// </summary>
    public static class FontWarmup
    {
        /// <summary>실제로 굽는 크기. UiSkin.Text의 서로 다른 값들이다.</summary>
        private static readonly int[] Sizes = { UiSkin.Text.Small, UiSkin.Text.Mid, UiSkin.Text.Title };

        /// <summary>
        /// 큰 크기(<see cref="UiSkin.Text.Logo"/>)로 굽는 글자.
        ///
        /// 타이틀 제목("RUNE CAST")과 **웨이브 카운트다운 숫자**가 쓴다.
        /// 대문자 26 + 빈칸 + 숫자 10 = 37자라 이 크기로 구워도 부담이 없다.
        /// </summary>
        private const string LogoChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ 0123456789";

        private static bool _hooked;
        private static bool _busy;

        /// <summary>Bootstrap에서 한 번 부른다.</summary>
        public static void Run()
        {
            Font f = UiSkin.GameFont;
            if (f == null) return;

            if (!_hooked)
            {
                _hooked = true;

                // 그래도 다시 짜이면 곧바로 다시 채운다. 어긋난 채로 한 프레임이라도
                // 그려지는 것보다 낫다.
                Font.textureRebuilt += OnRebuilt;
            }

            Bake(f);
        }

        private static void OnRebuilt(Font f)
        {
            if (_busy || f != UiSkin.GameFont) return;
            Bake(f);
        }

        private static void Bake(Font f)
        {
            _busy = true;
            try
            {
                for (int i = 0; i < Sizes.Length; i++)
                    f.RequestCharactersInTexture(FontCharset.All, Sizes[i], FontStyle.Normal);

                // 72px는 뽑기 카드의 룬 도형 열두 자뿐이다. 전체를 이 크기로
                // 구우면 아틀라스가 혼자서 몇 배로 뛴다.
                f.RequestCharactersInTexture(FontCharset.Big, UiSkin.Text.Glyph, FontStyle.Normal);

                // 타이틀 로고. 대문자와 빈칸뿐이라 27자다 — 별과 같은 이유로 예외.
                // **글자를 지정해서 굽는다.** 제목이 소문자나 한글로 바뀌면
                // 여기서 안 구워진 글자가 나오고, 그러면 그 글자를 그리는 순간
                // 아틀라스가 다시 짜인다(이 파일 맨 위 설명 참고).
                f.RequestCharactersInTexture(LogoChars, UiSkin.Text.Logo, FontStyle.Normal);

                // 별은 두 자뿐이라 사다리 밖 크기를 써도 아틀라스에 거의 안 얹힌다.
                f.RequestCharactersInTexture("★☆", UiSkin.Text.StarSmall, FontStyle.Normal);
                f.RequestCharactersInTexture("★☆", UiSkin.Text.Star, FontStyle.Normal);
                f.RequestCharactersInTexture("★☆", UiSkin.Text.StarBig, FontStyle.Normal);

                // 다시 짜이면 텍스처가 새로 만들어지므로 필터를 다시 건다.
                if (f.material != null && f.material.mainTexture != null)
                    f.material.mainTexture.filterMode = FilterMode.Point;
            }
            finally
            {
                _busy = false;
            }
        }
    }
}
