using UnityEngine;

namespace RuneCast.Core
{
    /// <summary>
    /// IMGUI용 스킨. 버튼·패널·게이지를 실제 UI 스프라이트로 그린다.
    ///
    /// **왜 필요한가:** 기본 IMGUI 스킨은 에디터 도구처럼 보인다. 색 사각형을 직접 그리는
    /// 지금 방식도 마찬가지다 — 모서리가 각지고 눌리는 느낌이 없어서 "게임 UI"로 안 읽힌다.
    ///
    /// **왜 uGUI로 안 옮기는가:** 레이아웃이 아직 바뀌는 중이다. 지금 옮기면
    /// 곧 뜯을 화면을 두 번 만든다. GUIStyle에 9-slice 배경만 물려도 인상은 거의 다 바뀐다.
    ///
    /// 9-slice 테두리 값은 스프라이트 픽셀을 실측해서 정했다 —
    /// 버튼은 아래쪽에 두꺼운 그림자 띠가 있어 아래 테두리만 더 크다.
    /// </summary>
    public static class UiSkin
    {
        private const string Dir = "Sprites/UI/";

        /// <summary>
        /// 글자 크기 사다리. **여기 없는 크기는 쓰지 않는다.**
        ///
        /// Unity의 동적 폰트는 (크기 × 스타일) 조합마다 글리프를 따로 구워
        /// 아틀라스에 넣는다. **아틀라스가 넘치면 이미 구운 글자가 밀려나고,
        /// 그 자리에 다른 글자가 그려진다** — 화면에 "있습니다"가 "족습니다"로 뜬다.
        /// 오타처럼 보이지만 오타가 아니다.
        ///
        /// 한글 글꼴이라 글리프가 559자다. 크기가 14종이었고 굵게까지 섞여 있어서
        /// 조합이 스무 개가 넘었다 — 2048×2048 아틀라스로는 감당이 안 되는 양이다.
        /// 다섯 단으로 줄이고 굵게를 뺐다.
        ///
        /// **굵게를 뺀 이유:** Galmuri에는 굵은 자체가 없어서 Unity가 획을 부풀려
        /// 흉내 내는데, 픽셀 글꼴에서는 그게 뭉개져 보이기만 하고
        /// 아틀라스 사용량은 두 배가 된다. 강조는 색으로 한다.
        /// </summary>
        public static class Text
        {
            // **실제로 굽는 크기는 셋뿐이다(13/17/32).** 이름은 다섯 개지만
            // Large는 Mid와, Huge는 Title과 같은 값을 쓴다.
            //
            // 진단으로 확인한 것: 아틀라스가 2048×2048에 글자 3002개로 꽉 차
            // 있었다. 그 상태에서 Unity는 자리가 없으면 아틀라스를 비우고 다시
            // 굽는데(eviction), 그게 OnGUI 도중에 일어나면 **그 프레임에서
            // 이미 그려진 글자들이 옛 좌표를 쓴다.** 멀쩡하지만 틀린 글자가
            // 나오는 게 그래서다 — "이어하기"가 "이어호기"로.
            //
            // 598자 × 5단 = 2990자였다. 3단이면 절반 이하로 떨어져 아틀라스에
            // 여유가 생기고, 미리 다 구워두면 다시 짜일 일이 없다.
            //
            // 이름을 남겨 둔 이유는 호출부 40여 곳을 안 고치기 위해서다.
            // 나중에 여유가 생기면 값을 다시 벌리면 된다.
            public const int Small = 13;
            public const int Mid = 17;
            public const int Large = 17;
            public const int Title = 32;
            public const int Huge = 32;

            /// <summary>뽑기 카드 가운데의 도형 글자. 쓰는 글자가 열 자 남짓이라 예외로 둔다.</summary>
            public const int Glyph = 72;

            /// <summary>
            /// 별(★☆) 전용 크기. **사다리 밖이지만 괜찮다** — 쓰는 글자가 두 자뿐이라
            /// 아틀라스에 얹히는 양이 무시할 수준이다.
            ///
            /// 사다리를 셋으로 줄였을 때 결과 화면의 별이 망가졌다. 40~54를
            /// Snap에 넣으면 전부 32로 뭉개져서 **커지는 연출이 죽었는데**,
            /// 간격은 요청한 크기로 계산해서 별들이 크기는 그대로인 채 옆으로
            /// 벌어지기만 했다.
            /// </summary>
            public const int Star = 40;

            /// <summary>방금 붙은 별이 잠깐 커질 때.</summary>
            public const int StarBig = 54;

            /// <summary>스테이지 목록의 작은 별.</summary>
            public const int StarSmall = 26;

            /// <summary>
            /// 타이틀 로고 전용. **사다리 밖이지만 별·룬 도형과 같은 예외다** —
            /// 쓰는 글자가 대문자 알파벳과 빈칸뿐(27자)이라 아틀라스에 얹히는
            /// 양이 무시할 수준이다.
            ///
            /// 제목이 Huge(32)였는데, 그건 화면 곳곳의 소제목과 **같은 크기**다.
            /// 켜자마자 보이는 화면에서 제목이 소제목만 하면 로고로 안 읽히고
            /// 화면이 비어 보인다. 사다리를 벌리는 대신 여기만 예외로 뒀다 —
            /// 사다리를 건드리면 한글 598자가 통째로 한 단 더 구워진다.
            ///
            /// <see cref="FontWarmup"/>가 이 크기로 대문자를 미리 굽는다.
            /// 제목을 소문자나 한글로 바꾸려면 거기도 같이 고쳐야 한다.
            /// </summary>
            public const int Logo = 64;

            /// <summary>계산해서 나온 크기를 사다리에 맞춘다. 애니메이션에 쓴다.</summary>
            public static int Snap(float px)
            {
                if (px <= (Small + Mid) * 0.5f) return Small;
                if (px <= (Mid + Title) * 0.5f) return Mid;
                return Title;
            }
        }


        /// <summary>
        /// 게임 글꼴. 없으면 null이고 그 경우 IMGUI 기본 글꼴이 쓰인다.
        ///
        /// **기본 IMGUI 글꼴이 "게임이 아님"을 가장 크게 드러낸다.** 스프라이트를
        /// 아무리 손봐도 글씨가 에디터 글꼴이면 도구처럼 보인다.
        ///
        /// Galmuri11(SIL OFL). 한글 완성형을 다 담으면 5.4MB라, 실제로 화면에 뜨는
        /// 글자만 남겨 64KB로 줄여서 넣었다 — tools/make_font.py 참고.
        /// **글을 고치면 그 스크립트를 다시 돌려야 한다.** 안 그러면 새로 쓴 글자가 없어서
        /// 화면에 네모로 뜬다.
        /// </summary>
        /// <summary>
        /// **크기마다 다른 글꼴 에셋을 쓴다.** 미리 구운(정적) 글꼴은
        /// `GUIStyle.fontSize`를 무시하고 구울 때 정해진 크기로만 그린다.
        ///
        /// 왜 동적 글꼴을 버렸는지는 tools/make_static_fonts.py에 적었다.
        /// 짧게: "이어하기"가 "이어호기"로 뜨는 문제를 네 번 고쳤고 네 번 다
        /// 빗나갔다. 소스도 폰트도 맞고 배율이 1.0이어도 틀렸다 — 즉 뭉개져서
        /// 잘못 읽히는 게 아니라 **진짜로 다른 글리프가 그려졌다.**
        /// 남은 용의자는 자리가 모자라면 통째로 다시 짜이는 동적 아틀라스뿐이고,
        /// 미리 구우면 그 일이 일어날 수가 없다.
        /// </summary>
        private static Font _font;
        private static bool _fontTried;

        /// <summary>
        /// 게임 글꼴.
        ///
        /// **크기별 정적 글꼴을 시도했다가 되돌렸다.** Unity의 정적 글꼴 임포터가
        /// 한글 598자를 굽지 못하고 동적으로 떨어졌고, 그러면 OS에서
        /// "Galmuri11_17" 같은 이름의 글꼴을 찾다 실패해 **글자가 통째로 사라진다.**
        /// 조금 틀린 글자보다 훨씬 나쁜 결과라 되돌렸다.
        ///
        /// "이어하기"가 "이어호기"로 뜨는 문제는 아직 안 잡혔다 — 다섯 번째 시도가
        /// 필요하고, 다음 수는 Unity 글꼴 시스템을 쓰지 않고 직접 구운 아틀라스에서
        /// 글자를 잘라 붙이는 것이다.
        /// </summary>
        public static Font FontFor(int size)
        {
            if (!_fontTried)
            {
                _fontTried = true;
                _font = Resources.Load<Font>("Fonts/Galmuri11");
            }
            return _font;
        }

        /// <summary>스타일에 글꼴과 크기를 물린다.</summary>
        public static void SetTextSize(GUIStyle style, int size)
        {
            if (style == null) return;

            Font f = FontFor(size);
            if (f == null) return;

            style.font = f;
            style.fontSize = size;
        }

        public static Font GameFont
        {
            get { return FontFor(Text.Mid); }
        }

        /// <summary>
        /// 만들어 둔 스타일들에 게임 글꼴을 물린다.
        /// 각 화면의 EnsureStyles 끝에서 한 번 부르면 된다 —
        /// GUI.skin.font를 갈아끼우는 방식은 스타일이 언제 복제됐는지에 따라
        /// 적용이 되기도 안 되기도 해서 조용히 어긋난다.
        /// </summary>
        public static void ApplyFont(params GUIStyle[] styles)
        {
            if (styles == null) return;

            // **스타일에 이미 들어 있는 fontSize를 보고 글꼴을 고른다.**
            // 부르는 쪽(아홉 군데)은 전부 `fontSize = UiSkin.Text.___` 를 먼저
            // 넣고 이걸 부르므로, 호출부를 하나도 안 고치고 정적 글꼴로 옮길 수 있다.
            for (int i = 0; i < styles.Length; i++)
            {
                if (styles[i] == null) continue;
                SetTextSize(styles[i], styles[i].fontSize);

                // **줄바꿈을 끄고 칸 밖은 자른다 — 모든 화면 공통.**
                //
                // 기본 스킨 라벨은 wordWrap이 켜져 있다. 한국어 기준으로 잡은 칸에
                // 더 긴 영어가 들어오면 조용히 두 줄이 되는데, IMGUI는 칸 밖으로
                // 나간 둘째 줄을 그냥 그린다 — 아랫줄 글씨 위에 겹쳐 찍혀서
                // "영어로 바꾸면 화면이 깨진다"가 됐다. 잘리는 건 눈에 보이는
                // 문제라 바로 고치게 되지만, 겹침은 못 읽게 만든다.
                //
                // 여기서 일괄로 처리하는 이유: 화면마다 스타일을 만들 때 각자
                // 챙기게 두면 반드시 한 군데가 빠진다 — 실제로 대장장이만 하고
                // 나머지 여덟 화면이 빠져 있었다. 글꼴을 물리는 이 길목은
                // 모든 스타일이 지나간다.
                styles[i].wordWrap = false;
                styles[i].clipping = TextClipping.Clip;
            }
        }

        private static bool _built;
        private static GUIStyle _button, _buttonActive, _panel, _panelAccent, _panelGold;
        private static GUIStyle _bar, _barFill, _banner, _slot, _slotOn;

        public static bool Available { get; private set; }

        public static GUIStyle Button { get { Build(); return _button; } }
        public static GUIStyle ButtonActive { get { Build(); return _buttonActive; } }
        public static GUIStyle Panel { get { Build(); return _panel; } }
        public static GUIStyle PanelAccent { get { Build(); return _panelAccent; } }
        public static GUIStyle Bar { get { Build(); return _bar; } }
        public static GUIStyle BarFill { get { Build(); return _barFill; } }
        public static GUIStyle Banner { get { Build(); return _banner; } }

        private static Texture2D Load(string name)
        {
            var t = Resources.Load<Texture2D>(Dir + name);
            if (t != null)
            {
                // 픽셀아트라 보간하면 뭉개진다. 9-slice로 늘려도 각 픽셀이 살아야 한다.
                t.filterMode = FilterMode.Point;
                t.wrapMode = TextureWrapMode.Clamp;
            }
            return t;
        }

        private static void Build()
        {
            if (_built) return;
            _built = true;

            Texture2D bn = Load("btn_normal");
            Texture2D bh = Load("btn_hover");
            Texture2D ba = Load("btn_active");
            Texture2D bon = Load("btn_on");
            Texture2D panel = Load("panel");
            Texture2D accent = Load("panel_accent");
            Texture2D gold = Load("panel_gold");
            Texture2D bar = Load("bar_bg");
            Texture2D fill = Load("bar_fill");
            Texture2D banner = Load("banner");
            Texture2D slot = Load("slot");
            Texture2D slotOn = Load("slot_on");

            // 하나라도 없으면 스킨을 쓰지 않는다. 반쯤 적용된 UI가 제일 보기 나쁘다.
            Available = bn != null && panel != null && bar != null;
            if (!Available)
            {
                _button = _buttonActive = _panel = _panelAccent = _panelGold =
                    _bar = _barFill = _banner = _slot = _slotOn = GUIStyle.none;
                return;
            }

            // ── 9-slice 테두리는 전부 스프라이트 픽셀에서 실측한 값이다 ──
            //
            // 재는 법: 같은 행(열)이 연속으로 반복되는 구간이 "늘려도 되는 가운데"고
            // 그 바깥이 테두리다. 눈대중으로 찍으면 늘렸을 때 모서리 장식이 뭉개지는데,
            // 픽셀아트에서는 그게 바로 티가 난다.
            //
            // 버튼(둥근 모서리, 32x32)은 상태마다 그림이 위아래로 움직인다 —
            // 뜬 상태 T8/B6, 살짝 눌림 T7/B7, 완전히 눌림 T4/B10.
            // **GUIStyle은 상태별로 테두리를 따로 못 주므로 가장 큰 값(T8/B10)을 쓴다.**
            // 작게 잡으면 눌린 상태에서 그림자 띠가 늘어나 뭉개진다. 크게 잡으면
            // 균일한 몸통이 조금 덜 늘어날 뿐이라 눈에 안 띈다.
            _button = new GUIStyle
            {
                border = new RectOffset(4, 4, 8, 10),
                alignment = TextAnchor.MiddleCenter,
                wordWrap = false,
            };
            _button.normal.background = bn;
            _button.hover.background = bh != null ? bh : bn;
            _button.active.background = ba != null ? ba : bn;
            _button.focused.background = bn;
            _button.onNormal.background = ba != null ? ba : bn;

            // 탭처럼 "켜진 채로 있는" 버튼. 눌린 상태(btn_active)를 그대로 쓰면
            // 손가락이 올라간 뒤에도 계속 눌려 있는 것처럼 보여서 눌리는 맛이 사라진다.
            // 중간 단계(btn_on)를 따로 두면 "선택됨"과 "지금 누르는 중"이 갈린다.
            _buttonActive = new GUIStyle(_button);
            Texture2D onTex = bon != null ? bon : (ba != null ? ba : bn);
            _buttonActive.normal.background = onTex;
            _buttonActive.hover.background = onTex;

            _panel = new GUIStyle { border = new RectOffset(3, 3, 5, 5) };
            _panel.normal.background = panel;

            _panelAccent = new GUIStyle { border = new RectOffset(3, 3, 5, 5) };
            _panelAccent.normal.background = accent != null ? accent : panel;

            _panelGold = new GUIStyle { border = new RectOffset(3, 3, 3, 3) };
            _panelGold.normal.background = gold != null ? gold : panel;

            _bar = new GUIStyle { border = new RectOffset(3, 3, 3, 4) };
            _bar.normal.background = bar;

            // 채움은 단색 3px이라 테두리가 필요 없다
            _barFill = new GUIStyle();
            _barFill.normal.background = fill != null ? fill : Texture2D.whiteTexture;

            _banner = new GUIStyle { border = new RectOffset(4, 3, 3, 3), alignment = TextAnchor.MiddleCenter };
            _banner.normal.background = banner != null ? banner : panel;

            // 문양을 넣는 칸. 네모 패널로 그리던 걸 실제 슬롯 그림으로 바꾼다 —
            // 칸은 테두리가 안으로 파여 있어야 "넣는 자리"로 읽히고,
            // 패널은 반대로 떠 있어야 한다. 같은 그림으로 둘 다 하면 둘 다 안 읽힌다.
            _slot = new GUIStyle { border = new RectOffset(2, 2, 3, 3) };
            _slot.normal.background = slot != null ? slot : panel;

            _slotOn = new GUIStyle { border = new RectOffset(2, 2, 2, 2) };
            _slotOn.normal.background = slotOn != null ? slotOn : (gold != null ? gold : panel);
        }

        /// <summary>버튼 위 글자색. 크림색 배경이라 밝은 글씨는 안 읽힌다.</summary>
        public static readonly Color LabelOnButton = new Color(0.16f, 0.13f, 0.1f);

        /// <summary>
        /// 글자색 모음.
        ///
        /// **이걸 만든 이유:** 화면마다 `GUI.color`에 숫자를 직접 적고 있었는데,
        /// 판이 밝은 색으로 바뀐 뒤에도 흰 글씨가 그대로 남은 곳이 여럿이었다
        /// (강화 줄의 "위력 ×1.00", "최대", 빈 레벨 칸). **흰 글씨 60%를 밝은 회색
        /// 판에 올리면 글자가 사라진다** — 안 보이는 게 아니라 아예 없는 것처럼 보인다.
        ///
        /// 어디에 올리는 글씨인지(밝은 판 / 어두운 배경)로 이름을 나눴다.
        /// 새 글씨를 넣을 때 둘 중 하나를 고르게만 해도 같은 실수가 안 난다.
        /// </summary>
        public static class Ink
        {
            /// <summary>밝은 판 위 본문.</summary>
            public static readonly Color OnPanel = new Color(0.13f, 0.12f, 0.11f);

            /// <summary>밝은 판 위 보조 설명. 본문보다 흐리되 읽을 수는 있어야 한다.</summary>
            public static readonly Color OnPanelDim = new Color(0.36f, 0.34f, 0.31f);

            /// <summary>밝은 판 위 강조(이득·증가량). 주황을 어둡게 깔아야 판에서 뜬다.</summary>
            public static readonly Color OnPanelWarm = new Color(0.58f, 0.31f, 0.08f);

            /// <summary>밝은 판 위 잠긴 글씨.</summary>
            public static readonly Color OnPanelMuted = new Color(0.45f, 0.44f, 0.42f);

            /// <summary>
            /// 메뉴 배경 위 본문. **이름과 달리 지금은 어두운 잉크다** —
            /// 배경이 밝은 하늘로 뒤집혔다(MenuBackdrop). 이름을 남긴 이유는
            /// "배경 직접 위"라는 용도가 그대로이기 때문. 쓰는 곳 열몇 군데를
            /// 한 번에 뒤집을 수 있는 게 이 팔레트를 만든 보람이다.
            /// </summary>
            public static readonly Color OnDark = new Color(0.2f, 0.18f, 0.16f);

            /// <summary>메뉴 배경 위 보조 설명.</summary>
            public static readonly Color OnDarkDim = new Color(0.38f, 0.36f, 0.34f);

            /// <summary>재화·강조 숫자. 밝은 바탕에서는 노랑이 죽으므로 진한 청동색.</summary>
            public static readonly Color Gold = new Color(0.62f, 0.42f, 0.06f);

            /// <summary>안 되는 것·모자란 것.</summary>
            public static readonly Color Warn = new Color(0.78f, 0.3f, 0.2f);
        }

        // ── 아이콘 ──────────────────────────────────────────────

        /// <summary>
        /// 화면에 쓰는 낱개 아이콘. `tools/slice_ui.py`가 잘라 넣는다.
        ///
        /// **왜 그림을 넣나:** 이 화면은 "파편", "주력", "각인권"처럼 뜻이 겹치는
        /// 숫자가 한꺼번에 뜬다. 글씨만 있으면 셋을 읽어서 구분해야 하는데,
        /// 그림이 앞에 붙으면 훑는 것만으로 갈린다.
        ///
        /// 없으면 null이고, 그리는 쪽은 조용히 건너뛴다 — 아이콘이 빠졌다고
        /// 화면이 무너지면 안 된다.
        /// </summary>
        public static class Icon
        {
            private static bool _tried;
            private static Texture2D _shard, _power, _forge, _glyph, _trophy, _rune, _ticket, _party;
            private static Texture2D[] _gems;

            private static Texture2D Get(string name)
            {
                var t = Resources.Load<Texture2D>("Sprites/Icons/" + name);
                if (t != null)
                {
                    t.filterMode = FilterMode.Point;
                    t.wrapMode = TextureWrapMode.Clamp;
                }
                return t;
            }

            private static void Load()
            {
                if (_tried) return;
                _tried = true;

                _shard = Get("ic_shard");
                _power = Get("ic_power");
                _forge = Get("ic_forge");
                _glyph = Get("ic_glyph");
                _trophy = Get("ic_trophy");
                _rune = Get("ic_rune");
                _ticket = Get("ic_ticket");
                _party = Get("ic_party");

                _gems = new[]
                {
                    Get("ic_gem_common"), Get("ic_gem_rare"),
                    Get("ic_gem_epic"), Get("ic_gem_legend"),
                };
            }

            public static Texture2D Shard { get { Load(); return _shard; } }
            public static Texture2D Power { get { Load(); return _power; } }
            public static Texture2D Forge { get { Load(); return _forge; } }
            public static Texture2D Glyph { get { Load(); return _glyph; } }
            public static Texture2D Trophy { get { Load(); return _trophy; } }
            public static Texture2D Rune { get { Load(); return _rune; } }
            public static Texture2D Ticket { get { Load(); return _ticket; } }

            /// <summary>부대 편성 — 은빛 투구. 모루(Forge)와 헷갈리지 않는 실루엣이어야 한다.</summary>
            public static Texture2D Party { get { Load(); return _party; } }

            /// <summary>등급 보석. 범위 밖 값은 제일 낮은 등급으로 떨어뜨린다.</summary>
            public static Texture2D Gem(int rarity)
            {
                Load();
                if (_gems == null) return null;
                return _gems[Mathf.Clamp(rarity, 0, _gems.Length - 1)];
            }
        }

        /// <summary>
        /// 아이콘 하나. **비율을 지켜 칸 안에 맞춘다.**
        ///
        /// 잘라 낸 아이콘은 투명 여백을 떼어 내서 저마다 크기가 다르다(20×28, 32×32…).
        /// 칸에 그냥 늘려 그리면 어떤 건 홀쭉하고 어떤 건 납작해져서, 나란히 놓았을 때
        /// 같은 팩에서 온 그림으로 안 보인다.
        /// </summary>
        /// <summary>
        /// 가로 스트립 시트에서 한 칸만 잘라 그린다.
        ///
        /// 유닛 스프라이트를 UI에 띄우려고 만들었다. `Resources.Load`로 받은
        /// 텍스처를 UV로 잘라 쓰므로 **스프라이트를 따로 만들 필요가 없다** —
        /// 장 목록에 쓸 대표 그림을 새로 그리는 대신 게임에 이미 있는 적을
        /// 그대로 보여줄 수 있다.
        ///
        /// zoom은 칸 안에서 더 당겨 보는 값이다. Tiny Swords 시트는 캐릭터가
        /// 프레임의 절반쯤만 차지해서, 1.0으로 그리면 큰 카드 안에서도 작아 보인다.
        /// </summary>
        public static void DrawSpriteFrame(Rect box, Texture2D tex, int frameSize,
            int frameIndex, Color tint = default(Color), float zoom = 1f)
        {
            if (tex == null || frameSize <= 0) return;
            if (tint == default(Color)) tint = Color.white;

            int frames = Mathf.Max(1, tex.width / frameSize);
            frameIndex = Mathf.Clamp(frameIndex, 0, frames - 1);

            float fw = frameSize / (float)tex.width;
            float inset = (1f - 1f / Mathf.Max(0.01f, zoom)) * 0.5f;

            // 텍스처 좌표는 아래가 0이다. 세로도 같은 비율로 당겨야 안 찌그러진다.
            var uv = new Rect(fw * (frameIndex + inset), inset,
                              fw * (1f - inset * 2f), 1f - inset * 2f);

            // 칸이 정사각이므로 상자도 정사각으로 맞춘다. 안 그러면 늘어난다.
            float s = Mathf.Min(box.width, box.height);
            var dst = new Rect(box.center.x - s * 0.5f, box.center.y - s * 0.5f, s, s);

            GUI.color = tint;
            GUI.DrawTextureWithTexCoords(dst, tex, uv, true);
            GUI.color = Color.white;
        }

        /// <summary>
        /// 그림자를 깔고 글씨를 얹는다.
        ///
        /// **배경 위에 바로 놓는 글씨는 배경을 고를 수가 없다.** 메뉴 배경은
        /// 밝은 하늘이고 전장은 풀밭이며 그 위로 이펙트가 지나간다. 어떤 색을
        /// 골라도 어딘가에서는 묻히므로, 한 겹 깔아 주는 쪽이 색을 바꾸는 것보다
        /// 확실하다 — 판(패널)을 깔면 배경을 가리고, 그건 더 큰 손해다.
        /// </summary>
        public static void LabelShadowed(Rect r, string text, GUIStyle style, Color color,
            float shadow = 0.5f)
        {
            GUI.color = new Color(0f, 0f, 0f, shadow * color.a);
            GUI.Label(new Rect(r.x + 2f, r.y + 2f, r.width, r.height), text, style);

            GUI.color = color;
            GUI.Label(r, text, style);
            GUI.color = Color.white;
        }

        public static void DrawIcon(Rect box, Texture2D tex, Color tint = default(Color))
        {
            if (tex == null) return;
            if (tint == default(Color)) tint = Color.white;

            float s = Mathf.Min(box.width / tex.width, box.height / tex.height);
            float w = tex.width * s, h = tex.height * s;

            GUI.color = tint;
            GUI.DrawTexture(new Rect(box.center.x - w * 0.5f, box.center.y - h * 0.5f, w, h), tex);
            GUI.color = Color.white;
        }

        // ── 테두리와 띠 ─────────────────────────────────────────

        private static bool _extraBuilt;
        private static GUIStyle _frame, _ribbon;

        private static void BuildExtra()
        {
            if (_extraBuilt) return;
            _extraBuilt = true;

            Texture2D frame = Load("frame_sel");
            Texture2D ribbon = Load("ribbon");

            // 모서리만 금빛이고 변 가운데는 어둡다. 테두리를 8로 잡아야
            // 늘렸을 때 금빛 모서리가 남는다 — 작게 잡으면 어두운 변만 남아
            // 강조가 안 되고, 24(전체)로 잡으면 늘어나질 않는다.
            _frame = new GUIStyle { border = new RectOffset(8, 8, 8, 8) };
            _frame.normal.background = frame;

            // 사다리꼴이라 **가로만** 늘린다. 위아래 테두리를 0으로 두면
            // 세로 전체가 늘어나는 구간이 되는데, 부르는 쪽이 원래 높이로만
            // 그리므로 실제로는 안 늘어난다.
            _ribbon = new GUIStyle { border = new RectOffset(18, 18, 0, 0), alignment = TextAnchor.MiddleCenter };
            _ribbon.normal.background = ribbon;
        }

        /// <summary>
        /// 속이 빈 테두리. **판 위에 겹쳐서** 지금 고른 것을 표시한다.
        ///
        /// 색만 바꿔서 선택을 알리는 방법은 이 화면에서 이미 한 번 실패했다
        /// (문양 목록의 장착 표시). 판 자체를 바꾸거나, 이렇게 테두리를
        /// 덧그려야 훑을 때 눈에 걸린다.
        /// </summary>
        public static void DrawFrame(Rect r, Color tint = default(Color))
        {
            BuildExtra();
            if (_frame == null || _frame.normal.background == null) return;

            GUI.color = tint == default(Color) ? Color.white : tint;
            GUI.Box(r, GUIContent.none, _frame);
            GUI.color = Color.white;
        }

        /// <summary>
        /// 소제목 띠. 목록 위의 "궤적 스킨" 같은 말머리에 쓴다.
        ///
        /// 맨 글씨로 두면 그게 제목인지 목록의 첫 줄인지 안 갈린다.
        /// 높이는 그림 원래 높이(18)에 맞춰 두는 게 좋다 — 세로로 늘리면
        /// 빗변의 계단이 뭉개진다.
        /// </summary>
        public static void DrawRibbon(Rect r, string label, GUIStyle labelStyle, Color tint = default(Color))
        {
            BuildExtra();

            if (_ribbon != null && _ribbon.normal.background != null)
            {
                GUI.color = tint == default(Color) ? Color.white : tint;
                GUI.Box(r, GUIContent.none, _ribbon);
                GUI.color = Color.white;
            }

            GUI.color = LabelOnButton;
            GUI.Label(new Rect(r.x, r.y - 1f, r.width, r.height), label, labelStyle);
            GUI.color = Color.white;
        }

        // ── 글씨가 칸을 넘을 때 ─────────────────────────────────

        /// <summary>
        /// 재는 데만 쓰는 그릇. 매 프레임 수십 번 부르므로 여기서 new 하면
        /// 그만큼 쓰레기가 쌓인다 — 목록 한 줄마다 한 번씩이다.
        /// </summary>
        private static readonly GUIContent _measure = new GUIContent();

        public static float TextWidth(string s, GUIStyle style)
        {
            if (string.IsNullOrEmpty(s) || style == null) return 0f;
            _measure.text = s;
            return style.CalcSize(_measure).x;
        }

        /// <summary>
        /// 칸에 안 들어가면 뒤를 잘라 "…"를 붙인다.
        ///
        /// **왜 필요한가:** IMGUI 라벨은 칸을 넘겨도 알아서 안 멈춘다. 기본 스타일은
        /// 줄바꿈까지 켜져 있어서, 긴 글이 들어오면 **아래 줄 위로 두 번째 줄이
        /// 그려진다.** 목록에서는 그게 아랫줄 글씨와 겹쳐 둘 다 못 읽게 된다.
        ///
        /// 글자 크기를 줄여서 맞추는 방법은 쓰면 안 된다 — 크기 사다리(13/17/32)
        /// 밖으로 나가는 순간 글꼴 아틀라스가 다시 짜인다.
        /// </summary>
        public static string Clip(string s, GUIStyle style, float width)
        {
            if (string.IsNullOrEmpty(s) || style == null) return s;
            if (TextWidth(s, style) <= width) return s;

            // 한 자씩 떼면 긴 글에서 한 프레임에 수십 번 재게 된다. 절반씩 좁혀
            // 들어가면 스무 자든 이백 자든 여덟 번 안쪽에서 끝난다.
            int lo = 0, hi = s.Length;
            while (lo < hi)
            {
                int mid = (lo + hi + 1) / 2;
                if (TextWidth(s.Substring(0, mid) + "…", style) <= width) lo = mid;
                else hi = mid - 1;
            }

            return lo <= 0 ? "…" : s.Substring(0, lo) + "…";
        }

        /// <summary>칸을 넘지 않는 라벨. 넘치면 잘라서 "…"로 끝낸다.</summary>
        public static void LabelClipped(Rect r, string s, GUIStyle style, Color color)
        {
            GUI.color = color;
            GUI.Label(r, Clip(s, style, r.width), style);
            GUI.color = Color.white;
        }

        /// <summary>
        /// 아이콘 + 글씨를 한 덩어리로. 아이콘은 왼쪽에, 글씨는 그 옆에 붙인다.
        /// 가운데 정렬 스타일을 주면 둘을 합친 폭을 기준으로 가운데를 맞춘다 —
        /// 아이콘을 무시하고 글씨만 가운데에 두면 덩어리가 왼쪽으로 치우쳐 보인다.
        /// </summary>
        public static void LabelWithIcon(Rect r, Texture2D icon, string s, GUIStyle style,
            Color color, float iconSize = 18f)
        {
            if (icon == null)
            {
                LabelClipped(r, s, style, color);
                return;
            }

            const float gap = 5f;
            float textW = Mathf.Min(TextWidth(s, style), r.width - iconSize - gap);
            float total = iconSize + gap + textW;

            float x = r.x;
            if (style.alignment == TextAnchor.MiddleCenter || style.alignment == TextAnchor.UpperCenter)
                x = r.center.x - total * 0.5f;

            DrawIcon(new Rect(x, r.center.y - iconSize * 0.5f, iconSize, iconSize), icon);

            // 아이콘 옆 글씨는 항상 왼쪽 붙임으로 그린다. 가운데 정렬 스타일을
            // 그대로 쓰면 남은 칸 안에서 또 가운데를 잡아 간격이 벌어진다.
            GUI.color = color;
            GUI.Label(new Rect(x + iconSize + gap, r.y, textW + 2f, r.height),
                Clip(s, style, textW + 2f), style.alignment == TextAnchor.MiddleLeft ? style : LeftOf(style));
            GUI.color = Color.white;
        }

        // 가운데 정렬 스타일마다 왼쪽 정렬 짝을 하나씩 만들어 둔다.
        // 그리는 도중에 style.alignment를 바꿨다 되돌리는 방법은
        // 같은 스타일을 쓰는 다른 라벨이 그 사이에 그려지면 어긋난다.
        private static readonly System.Collections.Generic.Dictionary<GUIStyle, GUIStyle> _left =
            new System.Collections.Generic.Dictionary<GUIStyle, GUIStyle>();

        private static GUIStyle LeftOf(GUIStyle style)
        {
            GUIStyle s;
            if (_left.TryGetValue(style, out s)) return s;

            s = new GUIStyle(style) { alignment = TextAnchor.MiddleLeft };
            _left[style] = s;
            return s;
        }

        /// <summary>
        /// 스킨 버튼 하나. 세 화면이 각자 그리던 걸 여기로 모았다 —
        /// 버튼 모양을 바꿀 때 세 군데를 고치게 두면 반드시 하나가 빠진다.
        ///
        /// tint를 주면 배경에 색을 입힌다(선택된 탭, 위험한 버튼 등).
        /// </summary>
        public static bool DrawButton(Rect r, string label, GUIStyle labelStyle,
            bool active = false, bool enabled = true, Color tint = default(Color))
        {
            Build();

            if (tint == default(Color)) tint = Color.white;
            if (!enabled) tint = new Color(tint.r, tint.g, tint.b, 0.35f);

            bool clicked;

            if (Available)
            {
                // 버튼 밑 그림자. 배경이 밝아지면서 크림색 버튼이 바탕에 묻었다 —
                // 스프라이트를 어둡게 물들여 세 픽셀 아래 먼저 찍으면
                // 버튼이 바닥에서 떠 보이고, 그게 "누를 수 있는 것"의 신호다.
                // Box로 그린다 — Button으로 찍으면 그림자 영역이 클릭까지 받는다.
                if (enabled)
                {
                    GUI.color = new Color(0.2f, 0.12f, 0.05f, 0.35f);
                    GUI.Box(new Rect(r.x, r.y + 3f, r.width, r.height), GUIContent.none,
                        active ? _buttonActive : _button);
                }

                GUI.color = tint;
                clicked = GUI.Button(r, GUIContent.none, active ? _buttonActive : _button);
                GUI.color = Color.white;

                // 버튼 스프라이트는 위쪽이 비어 있고 아래에 그림자 띠가 있다.
                // 글자를 정중앙에 두면 아래로 치우쳐 보이므로 살짝 올린다.
                var text = new Rect(r.x, r.y - 2f, r.width, r.height);
                GUI.color = enabled ? LabelOnButton : new Color(LabelOnButton.r, LabelOnButton.g, LabelOnButton.b, 0.5f);
                GUI.Label(text, label, labelStyle);
                GUI.color = Color.white;
            }
            else
            {
                GUI.color = !enabled ? new Color(0.1f, 0.1f, 0.1f, 0.8f)
                    : active ? new Color(0.3f, 0.5f, 0.25f, 0.95f) : new Color(0.16f, 0.18f, 0.16f, 0.95f);
                GUI.DrawTexture(r, Texture2D.whiteTexture);
                GUI.color = enabled ? Color.white : new Color(1f, 1f, 1f, 0.35f);
                GUI.Label(new Rect(r.x, r.y + (r.height - 22f) * 0.5f, r.width, 22f), label, labelStyle);
                GUI.color = Color.white;
                clicked = GUI.Button(r, GUIContent.none, GUIStyle.none);
            }

            // **소리는 여기서 낸다.** 화면마다 붙이면 반드시 어딘가 빠지고,
            // 그러면 어떤 버튼은 소리가 나고 어떤 버튼은 안 나는 게 된다 —
            // 그건 소리가 없는 것보다 나쁘다.
            if (clicked)
            {
                if (enabled) AudioManager.Play(Sfx.UiClick, 0.5f, 1.45f);
                // 피치를 멀리 띄운다. 전투의 마나 부족음(1.0)과 같은 파일이라
                // 가까이 두면 UI 거절인지 전투 실패인지 귀로 안 갈린다.
                else AudioManager.Play(Sfx.UiDenied, 0.5f, 1.7f);
            }

            return enabled && clicked;
        }

        public enum PanelKind { Plain, Accent, Gold }

        /// <summary>패널 배경. 스킨이 없으면 반투명 사각형.</summary>
        public static void DrawPanel(Rect r, Color tint, bool accent = false)
        {
            DrawPanel(r, tint, accent ? PanelKind.Accent : PanelKind.Plain);
        }

        public static void DrawPanel(Rect r, Color tint, PanelKind kind)
        {
            Build();

            if (!Available)
            {
                GUI.color = tint;
                GUI.DrawTexture(r, Texture2D.whiteTexture);
                GUI.color = Color.white;
                return;
            }

            GUIStyle st = kind == PanelKind.Gold ? _panelGold
                : kind == PanelKind.Accent ? _panelAccent : _panel;

            GUI.color = tint;
            GUI.Box(r, GUIContent.none, st);
            GUI.color = Color.white;
        }

        /// <summary>문양을 넣는 칸. selected면 금테 슬롯으로 바뀐다.</summary>
        public static void DrawSlot(Rect r, Color tint, bool selected = false)
        {
            Build();

            if (!Available)
            {
                GUI.color = selected ? new Color(0.5f, 0.42f, 0.2f, 0.9f) : new Color(0f, 0f, 0f, 0.5f);
                GUI.DrawTexture(r, Texture2D.whiteTexture);
                GUI.color = Color.white;
                return;
            }

            GUI.color = tint;
            GUI.Box(r, GUIContent.none, selected ? _slotOn : _slot);
            GUI.color = Color.white;
        }

        /// <summary>
        /// 빨간 점. 버튼 오른쪽 위 모서리에 걸친다.
        ///
        /// 맥동시키는 이유: 정적인 점은 UI 장식으로 읽혀서 며칠 지나면 안 보인다.
        /// 크기가 아주 작아서 움직임 말고는 눈에 걸릴 방법이 없다.
        /// </summary>
        // PrimitiveSprites.Circle(32)는 "ci" + size 로 키를 만들어 찾는다 —
        // 부르는 쪽이 매 프레임이면 그 문자열이 프레임마다 새로 생긴다.
        private static Texture2D _dot;

        public static void DrawRedDot(Rect anchor, float size = 13f)
        {
            if (_dot == null) _dot = PrimitiveSprites.Circle(32).texture;

            float pulse = 1f + 0.16f * Mathf.Sin(Time.unscaledTime * 5.5f);
            float d = size * pulse;
            var r = new Rect(anchor.xMax - d * 0.55f, anchor.y - d * 0.4f, d, d);

            // 어두운 테두리를 먼저 깔아야 밝은 버튼 위에서도 점이 떠 보인다
            GUI.color = new Color(0.25f, 0.03f, 0.03f, 0.9f);
            GUI.DrawTexture(new Rect(r.x - 1.5f, r.y - 1.5f, r.width + 3f, r.height + 3f), _dot);

            GUI.color = new Color(0.95f, 0.22f, 0.2f);
            GUI.DrawTexture(r, _dot);
            GUI.color = Color.white;
        }

        /// <summary>
        /// 제목 띠. 제목을 맨 글씨로 두면 화면에 계층이 안 생긴다 —
        /// 어디까지가 한 덩어리인지 눈이 못 잡는 게 "못생김"의 큰 부분이다.
        /// </summary>
        public static void DrawBanner(Rect r, string label, GUIStyle labelStyle, Color tint = default(Color))
        {
            Build();

            if (tint == default(Color)) tint = Color.white;

            if (Available)
            {
                GUI.color = tint;
                GUI.Box(r, GUIContent.none, _banner);
                GUI.color = Color.white;
            }

            GUI.color = LabelOnButton;
            GUI.Label(new Rect(r.x, r.y - 1f, r.width, r.height), label, labelStyle);
            GUI.color = Color.white;
        }

        /// <summary>
        /// 게이지 하나. 스킨이 없으면 사각형으로 대체한다.
        /// 색은 채움 스프라이트가 흰 계열이라 GUI.color로 입힌다.
        /// </summary>
        public static void DrawBar(Rect r, float ratio, Color fillColor)
        {
            Build();
            ratio = Mathf.Clamp01(ratio);

            if (!Available)
            {
                GUI.color = new Color(0f, 0f, 0f, 0.6f);
                GUI.DrawTexture(r, Texture2D.whiteTexture);
                GUI.color = fillColor;
                GUI.DrawTexture(new Rect(r.x + 2f, r.y + 2f, (r.width - 4f) * ratio, r.height - 4f), Texture2D.whiteTexture);
                GUI.color = Color.white;
                return;
            }

            GUI.color = Color.white;
            GUI.Box(r, GUIContent.none, _bar);

            if (ratio <= 0.001f) return;

            const float pad = 3f;
            var inner = new Rect(r.x + pad, r.y + pad, (r.width - pad * 2f) * ratio, r.height - pad * 2f);

            GUI.color = fillColor;
            GUI.Box(inner, GUIContent.none, _barFill);
            GUI.color = Color.white;
        }
    }
}
