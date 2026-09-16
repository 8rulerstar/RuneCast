using System.Collections.Generic;
using UnityEngine;
using RuneCast.Battle;
using RuneCast.Core;
using RuneCast.Gesture;
using RuneCast.Meta;
using RuneCast.Runes;

namespace RuneCast.UI
{
    /// <summary>
    /// 대장장이 — 룬 벼리기(강화) / 문양 소환(뽑기) / 문양 장착.
    ///
    /// 세 기능을 한 화면에 둔 이유: 재화가 하나뿐이라 셋이 서로 경쟁한다.
    /// 화면을 나누면 "지금 파편이 몇 개고 뭘 살 수 있나"를 오가며 확인해야 해서
    /// 정작 중요한 선택이 흐려진다. 대신 **소환 연출만은 전체 화면을 덮는다** —
    /// 결과가 목록 옆에 한 줄로 뜨면 확률 표를 확인하는 작업이 되어버린다.
    /// </summary>
    public class WorkshopUI : MonoBehaviour
    {
        public GameFlow flow;
        public RuneCast.Gesture.RuneRegistrar registrar;

        // 강화를 눌렀을 때 숫자가 그냥 바뀌면 오른 걸 못 본다. 차오르게 한다.
        private readonly PowerTicker _power = new PowerTicker();
        private readonly CollectionUI _collection = new CollectionUI();

        private enum Tab { Party, Upgrade, Gacha, Collection, Inscribe }

        /// <summary>
        /// 문양을 끼울 룬. 문양 탭은 **한 번에 룬 하나**를 손본다.
        ///
        /// 아홉 룬의 칸을 한 화면에 다 펴면 스물일곱 칸이 되어 어디를 보는지
        /// 알 수 없다. 하나씩 고르게 하면 "지금 무엇을 키우는 중인가"가 늘 화면에 있다.
        /// </summary>
        private RuneType _slotRune = RuneType.Arrow;
        private Tab _tab = Tab.Upgrade;

        private GUIStyle _title, _mid, _small, _big, _midCenter, _smallCenter, _bigCenter;
        private GUIStyle _midLeft, _smallRight;
        private Vector2 _scroll;

        private GachaReveal _reveal;
        private bool _lastPullAutoEquipped;
        private int _autoEquippedCount;

        /// <summary>이번 뽑기에 딸려 나온 각인권 수. 결과 아래에 한 줄로 알린다.</summary>
        private int _ticketsFromPull;

        private void Awake()
        {
            _reveal = gameObject.AddComponent<GachaReveal>();
        }

        private void EnsureStyles()
        {
            if (_title != null) return;
            _title = new GUIStyle(GUI.skin.label) { fontSize = UiSkin.Text.Title, alignment = TextAnchor.MiddleCenter };
            _big = new GUIStyle(GUI.skin.label) { fontSize = UiSkin.Text.Large, alignment = TextAnchor.MiddleCenter };
            _mid = new GUIStyle(GUI.skin.label) { fontSize = UiSkin.Text.Mid };
            _small = new GUIStyle(GUI.skin.label) { fontSize = UiSkin.Text.Small };
            // 버튼 라벨은 매 프레임 수십 번 그려진다. 여기서 new 하면 그만큼 쓰레기가 쌓인다.
            _midCenter = new GUIStyle(_mid) { alignment = TextAnchor.MiddleCenter };
            _smallCenter = new GUIStyle(_small) { alignment = TextAnchor.MiddleCenter };
            _bigCenter = new GUIStyle(_big) { alignment = TextAnchor.MiddleCenter };
            _midLeft = new GUIStyle(_mid) { alignment = TextAnchor.MiddleLeft };
            _smallRight = new GUIStyle(_small) { alignment = TextAnchor.MiddleRight };

            // **줄바꿈을 끄고 칸 밖을 잘라 낸다.**
            //
            // 기본 IMGUI 라벨 스타일은 wordWrap이 켜져 있다. 높이 20짜리 칸에
            // 긴 글이 들어오면 조용히 두 줄이 되는데, IMGUI는 칸 밖으로 나간
            // 두 번째 줄을 **그냥 그린다.** 목록에서는 그게 아랫줄 글씨 위에
            // 겹쳐 찍혀서 둘 다 못 읽게 된다 — 화면에서 "글씨가 삐져나온" 것들이
            // 대부분 이거였다.
            //
            // 자르기는 마지막 방어선이다. 잘려 보인다는 건 칸이 좁다는 뜻이므로
            // 자리를 다시 잡아야 한다 — 다만 잘리는 편이 겹치는 것보다 낫다.
            foreach (GUIStyle s in new[] { _title, _big, _mid, _small, _midCenter, _smallCenter,
                                           _bigCenter, _midLeft, _smallRight })
            {
                s.wordWrap = false;
                s.clipping = TextClipping.Clip;
            }

            UiSkin.ApplyFont(_title, _big, _mid, _small, _midCenter, _smallCenter, _bigCenter,
                _midLeft, _smallRight);
        }

        private void OnGUI()
        {
            // **각인 중에는 이 화면이 아예 안 그려진다.** 예전엔 각인 오버레이가
            // 위에 덮여도 그 아래 대장장이 버튼이 그대로 살아 있어서,
            // 도형을 그리다 손이 스치면 탭이 바뀌거나 뽑기가 돌아갔다.
            // IMGUI는 그리는 쪽이 스스로 빠지지 않으면 계속 입력을 받는다.
            if (registrar != null && registrar.Active) return;

            // 모든 UI는 가상 해상도 위에서 그린다. 실제 픽셀로 짜면
            // 고해상도 화면에서 글자가 물리적으로 못 읽을 만큼 작아진다.
            UiScale.Begin();
            DrawAll();
            UiScale.End();
        }

        private void DrawAll()
        {
            if (flow == null || flow.State != GameState.Workshop) return;
            EnsureStyles();

            // 예전엔 0.9로 거의 검게 덮었다. 그땐 뒤에 전투 화면이 있어서 가려야 했는데,
            // 이제 메뉴 배경이 따로 있으므로 가릴 것이 없다 — 글씨 대비만 세운다.
            // 막은 아주 옅게. 진한 크림 막은 초원 색을 씻어서 화면 전체가 탁해진다 —
            // 글씨는 거의 다 판(패널) 위에 있으므로 전면 막이 두꺼울 이유가 없다.
            GUI.color = new Color(0.97f, 0.97f, 0.9f, 0.16f);
            GUI.DrawTexture(new Rect(0f, 0f, UiScale.W, UiScale.H), Texture2D.whiteTexture);
            GUI.color = Color.white;

            UiSkin.DrawBanner(new Rect(UiScale.W * 0.5f - 190f, UiScale.T + 16f, 380f, 46f),
                Loc.T("shop.title"), _title);

            // 파편(쓸 것)과 주력(키운 것)을 나란히. 강화를 누르면 오른쪽 숫자가 오르고
            // 왼쪽이 줄어드는 게 한 줄에서 보여야 "썼다"와 "세졌다"가 이어진다.
            //
            // 그림을 앞에 붙인 이유: 이 줄에는 뜻이 다른 숫자가 둘 뿐인데도
            // 글씨만 있으면 어느 쪽이 쓰는 것인지 매번 읽어서 판단해야 한다.
            UiSkin.LabelWithIcon(new Rect(UiScale.W * 0.5f - 226f, UiScale.T + 60f, 216f, 26f),
                UiSkin.Icon.Shard, Loc.T("common.shards") + "  " + PlayerData.Shards,
                _midCenter, UiSkin.Ink.Gold, 20f);

            _power.Update(CombatPower.Total);
            _power.Draw(new Rect(UiScale.W * 0.5f + 10f, UiScale.T + 62f, 216f, 24f),
                _midCenter, _smallCenter);

            // 연출 중에는 뒤쪽 조작을 잠근다. 소환 중에 다른 버튼이 눌리면
            // 결과가 뜨기도 전에 화면이 바뀐다.
            GUI.enabled = !_reveal.Busy;

            DrawTabs();

            if (_tab == Tab.Party) DrawParty();
            else if (_tab == Tab.Upgrade) DrawUpgrade();
            else if (_tab == Tab.Gacha) DrawGacha();
            else if (_tab == Tab.Inscribe) DrawInscribe();
            else _collection.Draw(_mid, _small, _midCenter, _smallCenter, UiButton);

            // 각인 모드 진입로. 룬을 손보는 곳이 여기라 자리가 맞고, 무엇보다
            // **폰에서 이 기능에 닿는 유일한 경로**다 (예전엔 Tab 키뿐이었다).
            // 각인은 이제 탭에 있다. 화면 맨 아래 구석의 버튼은 아무도 안 봤다.
            if (UiButton(new Rect(UiScale.W * 0.5f - 95f, UiScale.B - 66f, 190f, 46f), Loc.T("common.back")))
                flow.GoToStageSelect();

            GUI.enabled = true;

            // 소환 연출은 전체를 덮는다 — 결과를 목록 옆 한 줄로 띄우면
            // 뽑기가 확률 표 확인 작업이 된다.
            _reveal.Draw(_bigCenter, _midCenter, _smallCenter);
            DrawRevealButtons();
        }

        /// <summary>
        /// 어느 룬에 끼울지 고르는 줄.
        ///
        /// **도형으로 고르게 한다.** 이 게임에서 룬을 부르는 이름은 사실 그 도형이고,
        /// 아홉 개를 이름으로 늘어놓으면 줄이 화면을 가로지른다.
        ///
        /// 칸이 다 찬 룬은 위에 점을 찍어 둔다 — 어디가 남았는지 훑어보려고
        /// 룬을 하나씩 눌러 볼 필요가 없어야 한다.
        /// </summary>
        private void DrawRunePicker(float x, float y, float w)
        {
            var runes = GlyphTable.Runes;
            const float sz = 38f, gap = 5f;

            for (int i = 0; i < runes.Length; i++)
            {
                RuneType t = runes[i];
                var r = new Rect(x + i * (sz + gap), y, sz, sz);
                bool on = t == _slotRune;

                if (UiSkin.DrawButton(r, GlyphTable.Glyph(t), _midCenter, on))
                    _slotRune = t;

                // 고른 룬은 테두리로. 눌린 버튼 그림만으로는 아홉 개가 나란할 때
                // 어느 것이 선택인지 곁눈에 안 걸린다 — 탭과 같은 이유다.
                if (on) UiSkin.DrawFrame(new Rect(r.x - 2f, r.y - 2f, r.width + 4f, r.height + 4f));

                // 다 찬 룬은 점으로. 비어 있는 룬을 찾는 게 이 줄의 쓸모다.
                if (PlayerData.EquippedOn(t).Count < PlayerData.SlotsPerRune) continue;

                GUI.color = new Color(0.55f, 0.85f, 0.55f);
                GUI.DrawTexture(new Rect(r.xMax - 9f, r.y + 3f, 6f, 6f), Texture2D.whiteTexture);
                GUI.color = Color.white;
            }

            UiSkin.LabelClipped(new Rect(x + runes.Length * (sz + gap) + 10f, y + 8f, 230f, 22f),
                GlyphTable.RuneName(_slotRune), _mid, UiSkin.Ink.OnDark);
        }

        /// <summary>
        /// 지금 고른 룬의 장착 칸. 찬 칸은 등급색으로 채우고 빈 칸은 비워 둔다.
        /// </summary>
        private void DrawEquipSlots(float x, float y)
        {
            const float sz = 34f, gap = 6f;
            int max = PlayerData.SlotsPerRune;
            var slots = PlayerData.EquippedOn(_slotRune);

            for (int i = 0; i < max; i++)
            {
                var r = new Rect(x + i * (sz + gap), y, sz, sz);
                bool filled = i < slots.Count;

                // 빈 칸은 어둡게 눌러 준다. 같은 밝기로 두면 찬 칸과 구분이 안 돼서
                // "칸을 그려 놓은 이유"가 없어진다 — 비어 있음이 보여야 하는 그림이다.
                UiSkin.DrawSlot(r, filled ? Color.white : new Color(0.45f, 0.47f, 0.52f), filled);

                if (!filled) continue;

                GlyphData g = PlayerData.FindGlyph(slots[i]);
                if (g == null) continue;

                // 색 사각형 대신 등급 보석. 목록의 줄과 같은 그림이라
                // "칸에 든 것"과 "목록의 그것"이 같은 물건으로 읽힌다.
                Texture2D gem = UiSkin.Icon.Gem(g.rarity);
                if (gem != null)
                {
                    UiSkin.DrawIcon(new Rect(r.x + 5f, r.y + 5f, sz - 10f, sz - 10f), gem);
                }
                else
                {
                    GUI.color = GlyphTable.RarityColor((GlyphRarity)g.rarity);
                    GUI.DrawTexture(new Rect(r.x + 8f, r.y + 8f, sz - 16f, sz - 16f), Texture2D.whiteTexture);
                    GUI.color = Color.white;
                }
            }
        }

        /// <summary>
        /// 탭 하나. 그림을 왼쪽에 두고 글씨를 그 옆에 붙인다.
        ///
        /// **고른 탭에는 테두리를 덧그린다.** 예전엔 버튼 그림만 바뀌었는데,
        /// 넷이 나란히 있으면 그 차이가 생각보다 안 보인다 — 특히 밝은 배경에서
        /// 눌린 그림과 안 눌린 그림은 몇 픽셀 차이다. 테두리는 색이 아니라
        /// 형태가 달라져서 곁눈으로도 걸린다.
        /// </summary>
        private bool DrawTab(Rect r, Texture2D icon, string label, bool on)
        {
            bool hit = UiSkin.DrawButton(r, "", _midCenter, on, true,
                on ? new Color(0.8f, 1f, 0.72f) : Color.white);

            const float ic = 22f, gap = 6f;
            float textW = UiSkin.TextWidth(label, _midCenter);
            float total = (icon != null ? ic + gap : 0f) + textW;
            float sx = r.center.x - total * 0.5f;

            if (icon != null)
            {
                // 안 고른 탭의 그림은 조금 죽인다. 넷 다 같은 밝기면 색이 네 덩어리로
                // 흩어져서 정작 지금 보고 있는 탭이 어디인지가 안 읽힌다.
                UiSkin.DrawIcon(new Rect(sx, r.center.y - ic * 0.5f - 1f, ic, ic), icon,
                    on ? Color.white : new Color(1f, 1f, 1f, 0.62f));
                sx += ic + gap;
            }

            GUI.color = UiSkin.LabelOnButton;
            GUI.Label(new Rect(sx, r.y - 2f, textW + 4f, r.height), label, _midLeft);
            GUI.color = Color.white;

            if (on) UiSkin.DrawFrame(new Rect(r.x - 2f, r.y - 2f, r.width + 4f, r.height + 4f));

            return hit;
        }

        private void DrawTabs()
        {
            // 탭이 다섯이 되면서 폭을 줄였다. 172×5 + 여백이면 좁은 화면에서 넘친다.
            float w = Mathf.Min(150f, (UiScale.W - 60f - 12f * 4f) / 5f);
            const float h = 46f, gap = 12f;
            float total = w * 5f + gap * 4f;
            float x = (UiScale.W - total) * 0.5f;
            float y = UiScale.T + 96f;

            var partyTab = new Rect(x, y, w, h);
            if (DrawTab(partyTab, UiSkin.Icon.Party, Loc.T("shop.navParty"), _tab == Tab.Party))
                _tab = Tab.Party;

            var upgradeTab = new Rect(x + w + gap, y, w, h);
            if (DrawTab(upgradeTab, UiSkin.Icon.Forge, Loc.T("shop.navUpgrade"), _tab == Tab.Upgrade))
                _tab = Tab.Upgrade;

            // 탭마다 따로 알린다. 대장장이 버튼에만 점이 있으면 들어와서 또 찾아야 한다.
            if (Notify.CanUpgradeAny) UiSkin.DrawRedDot(upgradeTab, 11f);

            var gachaTab = new Rect(x + (w + gap) * 2f, y, w, h);
            if (DrawTab(gachaTab, UiSkin.Icon.Glyph, Loc.T("shop.navGlyph"), _tab == Tab.Gacha))
                _tab = Tab.Gacha;

            if (Notify.CanPull || Notify.HasUnequipped) UiSkin.DrawRedDot(gachaTab, 11f);

            var collTab = new Rect(x + (w + gap) * 3f, y, w, h);
            if (DrawTab(collTab, UiSkin.Icon.Trophy, Loc.T("shop.navCollection"), _tab == Tab.Collection))
                _tab = Tab.Collection;

            if (Notify.HasAchieveReward) UiSkin.DrawRedDot(collTab, 11f);

            var inscTab = new Rect(x + (w + gap) * 4f, y, w, h);
            if (DrawTab(inscTab, UiSkin.Icon.Rune, Loc.T("shop.navInscribe"), _tab == Tab.Inscribe))
                _tab = Tab.Inscribe;

            // 각인권이 있으면 알린다. **이 기능은 아무도 안 찾는다** —
            // 예전엔 화면 맨 아래 구석에 버튼 하나뿐이었고, 무엇을 하는
            // 기능인지 눌러 보기 전에는 알 방법이 없었다.
            if (PlayerData.InscribeTickets > 0) UiSkin.DrawRedDot(inscTab, 11f);
        }

        /// <summary>
        /// 각인 탭 — **무엇을 하는 기능인지 먼저 말한다.**
        ///
        /// 예전에는 대장장이 맨 아래에 "각인"이라는 버튼 하나뿐이었다.
        /// 그 말만 보고 "룬의 도형을 내가 그린 것으로 바꾼다"를 알아낼 수는 없다.
        /// 이 게임에서 제일 자기 것처럼 느껴지는 조작인데 아무도 안 눌렀다.
        ///
        /// 탭으로 올린 이유도 같다 — 사람은 탭을 본다. 화면 구석의 버튼은 안 본다.
        /// </summary>
        private void DrawInscribe()
        {
            float top = UiScale.T + 172f;
            const float w = 620f;
            float x = (UiScale.W - w) * 0.5f;

            UiSkin.DrawPanel(new Rect(x, top, w, 150f), new Color(0.9f, 0.9f, 0.88f));

            // 설명 두 줄은 잘라도 된다 — 좁은 화면에서 두 줄이 겹쳐 찍히는 것보다
            // "…"로 끝나는 쪽이 낫다. 원문은 각인 모드 안에서도 다시 나온다.
            UiSkin.LabelClipped(new Rect(x + 10f, top + 14f, w - 20f, 26f),
                Loc.T("inscribe.what"), _bigCenter, UiSkin.Ink.OnPanel);
            UiSkin.LabelClipped(new Rect(x + 20f, top + 48f, w - 40f, 22f),
                Loc.T("inscribe.why1"), _midCenter, UiSkin.Ink.OnPanelDim);
            UiSkin.LabelClipped(new Rect(x + 20f, top + 72f, w - 40f, 22f),
                Loc.T("inscribe.why2"), _midCenter, UiSkin.Ink.OnPanelDim);

            // 남은 장수를 크게, 두루마리 그림과 함께.
            // 이게 이 화면에서 제일 먼저 읽혀야 하는 숫자다.
            int tickets = PlayerData.InscribeTickets;
            UiSkin.LabelWithIcon(new Rect(x, top + 102f, w, 30f), UiSkin.Icon.Ticket,
                Loc.F("inscribe.tickets", tickets), _bigCenter,
                tickets > 0 ? new Color(0.16f, 0.4f, 0.68f) : new Color(0.62f, 0.28f, 0.22f), 26f);

            // 지금 몇 개를 바꿔 놨는지. 각인이 실제로 남아 있다는 증거다.
            UiSkin.LabelClipped(new Rect(x, top + 158f, w, 22f),
                Loc.F("inscribe.customCount", RuneCast.Gesture.CustomRuneStore.LoadedCount),
                _smallCenter, UiSkin.Ink.OnDarkDim);

            var start = new Rect(UiScale.W * 0.5f - 110f, top + 192f, 220f, 46f);
            if (UiButton(start, Loc.T("inscribe.start"), true, tickets > 0) && registrar != null)
                registrar.Toggle();

            if (tickets <= 0)
            {
                UiSkin.LabelClipped(new Rect(x, top + 244f, w, 22f),
                    Loc.T("inscribe.howToGet"), _smallCenter, UiSkin.Ink.Warn);
            }
        }

        // ── 룬 강화 ─────────────────────────────────────────────

        /// <summary>
        /// 강화 줄의 가로 자리. **세 덩어리가 서로 안 겹치게 여기서만 정한다.**
        ///
        /// 예전에는 이름·레벨칸·버튼이 각자 좌표를 들고 있었고, 레벨 칸이
        /// `330 + 10칸 × 22 = 550`까지 뻗는데 버튼이 `620 - 150 = 470`에서
        /// 시작했다 — **80픽셀이 겹쳐서** 만렙에 가까운 룬은 마지막 칸 두 개가
        /// 버튼 밑으로 들어갔다. 셋의 자리를 한곳에 모아 두면 한쪽을 넓힐 때
        /// 다른 쪽이 밀리는 게 코드에서 바로 보인다.
        /// </summary>
        private const float UpW = 640f;        // 줄 전체 폭
        private const float UpNameW = 206f;    // 왼쪽: 이름·위력
        private const float UpGaugeX = 220f;   // 가운데: 레벨 칸·증가량
        private const float UpBtnW = 146f;     // 오른쪽: 강화 버튼

        /// <summary>
        /// 부대 편성.
        ///
        /// 왼쪽은 **칸**(전장에 서는 자리), 오른쪽은 **용사 종류**(해금과 강화).
        /// 둘을 한 화면에 둔 이유는 고르는 이유가 서로에게 있어서다 —
        /// 사수를 키웠으면 칸에 넣고 싶어지고, 칸에 넣었으면 키우고 싶어진다.
        ///
        /// **칸이 여덟인데 판에는 넷다섯만 나간다.** 위에서부터 쓰므로 아래 칸은
        /// 판이 커질 때를 위한 자리다. 그 규칙을 안내문 한 줄로 말해 둔다 —
        /// 안 그러면 넣어 둔 용사가 왜 안 나오는지 알 방법이 없다.
        /// </summary>
        private void DrawParty()
        {
            const float rowH = 52f, gap = 6f;
            float colW = Mathf.Min(360f, (UiScale.W - 90f) * 0.5f);
            float x0 = (UiScale.W - (colW * 2f + 30f)) * 0.5f;
            float y0 = UiScale.T + 172f;

            UiSkin.LabelClipped(new Rect(x0, y0 - 26f, colW * 2f + 30f, 20f),
                Loc.T("party.hint"), _small, UiSkin.Ink.OnDarkDim);

            // ── 왼쪽: 칸 ──
            var unlocked = HeroRoster.Unlocked();

            for (int i = 0; i < PlayerData.PartySize; i++)
            {
                var r = new Rect(x0, y0 + i * (rowH + gap), colW, rowH);
                if (r.yMax > UiScale.B - 70f) break;

                UnitKind kind = PlayerData.PartyKind(i);
                bool back = PlayerData.PartyBack(i);

                // 이 판에 실제로 나가는 칸만 밝게. 나머지는 예비석이라는 게
                // 색으로 먼저 읽혀야 안내문을 안 읽어도 안다.
                bool active = i < 5;
                UiSkin.DrawPanel(r, active ? new Color(0.93f, 0.90f, 0.82f)
                                           : new Color(0.80f, 0.82f, 0.85f));

                UiSkin.LabelClipped(new Rect(r.x + 12f, r.y + 6f, 58f, 20f),
                    Loc.F("party.slot", i + 1), _small, UiSkin.Ink.OnPanelDim);

                UiSkin.LabelClipped(new Rect(r.x + 12f, r.y + 25f, 130f, 22f),
                    Loc.T(HeroRoster.Of(kind).NameKey), _mid,
                    active ? UiSkin.Ink.OnPanel : UiSkin.Ink.OnPanelMuted);

                // 종류 바꾸기 — 열린 용사들을 돌아가며 고른다. 목록을 따로 띄우지
                // 않은 건 지금 종류가 셋뿐이라 누르는 편이 빠르기 때문이다.
                // 넷을 넘어가면 그때 목록으로 바꿀 것.
                if (unlocked.Count > 1 &&
                    UiSkin.DrawButton(new Rect(r.xMax - 168f, r.y + 10f, 74f, 32f),
                        Loc.T("party.swap"), _smallCenter, false, true, Color.white))
                {
                    int at = unlocked.IndexOf(kind);
                    PlayerData.SetPartyKind(i, unlocked[(at + 1) % unlocked.Count]);
                    AudioManager.Play(Sfx.DrawBegin, 0.5f);
                }

                if (UiSkin.DrawButton(new Rect(r.xMax - 86f, r.y + 10f, 74f, 32f),
                        Loc.T(back ? "party.back" : "party.front"), _smallCenter,
                        back, true, Color.white))
                {
                    PlayerData.SetPartyBack(i, !back);
                    AudioManager.Play(Sfx.DrawBegin, 0.5f);
                }
            }

            // ── 오른쪽: 용사 종류 ──
            float x1 = x0 + colW + 30f;

            for (int i = 0; i < HeroRoster.Count; i++)
            {
                var def = HeroRoster.At(i);
                var r = new Rect(x1, y0 + i * (rowH * 1.6f + gap), colW, rowH * 1.6f);
                if (r.yMax > UiScale.B - 70f) break;

                bool open = HeroRoster.IsUnlocked(def.Kind);
                int lv = PlayerData.HeroLevel(def.Kind);
                bool maxed = lv >= HeroRoster.MaxLevel;
                int cost = HeroRoster.UpgradeCost(lv);
                bool afford = open && !maxed && PlayerData.Shards >= cost;

                UiSkin.DrawPanel(r, !open ? new Color(0.72f, 0.75f, 0.79f)
                    : afford ? new Color(0.93f, 0.90f, 0.82f)
                             : new Color(0.85f, 0.87f, 0.90f));

                UiSkin.LabelClipped(new Rect(r.x + 14f, r.y + 8f, colW - 28f, 24f),
                    Loc.T(def.NameKey), _mid, open ? UiSkin.Ink.OnPanel : UiSkin.Ink.OnPanelMuted);

                UiSkin.LabelClipped(new Rect(r.x + 14f, r.y + 32f, colW - 28f, 20f),
                    Loc.T(def.RoleKey), _small, UiSkin.Ink.OnPanelDim);

                if (!open)
                {
                    UiSkin.LabelClipped(new Rect(r.x + 14f, r.y + 54f, colW - 28f, 20f),
                        Loc.F("party.locked", def.UnlockStars), _small, UiSkin.Ink.OnPanelWarm);
                    continue;
                }

                UiSkin.LabelClipped(new Rect(r.x + 14f, r.y + 54f, colW - 120f, 20f),
                    Loc.F("party.level", lv, HeroRoster.PowerAt(lv)), _small, UiSkin.Ink.OnPanelDim);

                if (!maxed &&
                    UiSkin.DrawButton(new Rect(r.xMax - 118f, r.y + 46f, 104f, 32f),
                        Loc.F("party.upgrade", cost), _smallCenter, afford, true, Color.white))
                {
                    if (PlayerData.UpgradeHero(def.Kind))
                        AudioManager.Play(Sfx.DrawBegin, 0.6f);
                }
                else if (maxed)
                {
                    UiSkin.LabelClipped(new Rect(r.xMax - 118f, r.y + 54f, 104f, 20f),
                        Loc.T("common.max"), _smallRight, UiSkin.Ink.OnPanelMuted);
                }
            }
        }

        private void DrawUpgrade()
        {
            RuneType[] runes = GlyphTable.Runes;

            const float rowH = 56f;
            float x = (UiScale.W - UpW) * 0.5f;
            float y = UiScale.T + 172f;

            UiSkin.LabelClipped(new Rect(x, y - 24f, UpW, 20f),
                Loc.T("shop.upgradeHint"), _small, UiSkin.Ink.OnDarkDim);

            float gaugeW = UpW - UpGaugeX - UpBtnW - 28f;

            for (int i = 0; i < runes.Length; i++)
            {
                RuneType rune = runes[i];
                var r = new Rect(x, y + i * (rowH + 6f), UpW, rowH);

                int level = PlayerData.LevelOf(rune);
                int cost = RuneUpgrades.UpgradeCost(rune);
                bool maxed = cost < 0;
                bool afford = !maxed && PlayerData.Shards >= cost;

                // 지금 올릴 수 있는 룬은 판을 따뜻하게 바꾼다. 버튼만 켜 두면
                // 아홉 줄 중 어디를 눌러야 하는지 훑어서 찾아야 한다.
                UiSkin.DrawPanel(r, maxed ? new Color(0.72f, 0.75f, 0.79f)
                    : afford ? new Color(0.93f, 0.9f, 0.82f)
                             : new Color(0.82f, 0.85f, 0.88f));

                DrawUpgradeFlash(r, rune);

                UiSkin.LabelClipped(new Rect(r.x + 14f, r.y + 7f, UpNameW, 22f),
                    GlyphTable.RuneName(rune) + "   Lv." + level, _mid,
                    maxed ? UiSkin.Ink.OnPanelMuted : UiSkin.Ink.OnPanel);

                // 예전엔 이 줄이 **흰 글씨 60%**였다. 판이 밝은 회색이라
                // 화면에서 글자가 통째로 사라져 있었다.
                UiSkin.LabelClipped(new Rect(r.x + 14f, r.y + 30f, UpNameW, 20f),
                    Loc.F("shop.power", Loadout.PowerMultiplier(rune), ManaPool.EffectiveCost(rune)),
                    _small, UiSkin.Ink.OnPanelDim);

                DrawLevelGauge(new Rect(r.x + UpGaugeX, r.y + 14f, gaugeW, 12f), level);

                // 이 강화가 주력을 얼마나 올리는지. 투자 결과가 버튼 옆에 미리 보인다.
                //
                // 예전에는 `y + 44`에 높이 18로 그려서 줄(54) 밖으로 8픽셀 나갔다 —
                // 판 아래 틈에 걸쳐 다음 줄 위로 흘렀다.
                int gain = CombatPower.GainFromUpgrade(rune);
                if (gain > 0 && !maxed)
                {
                    UiSkin.LabelClipped(new Rect(r.x + UpGaugeX, r.y + 32f, gaugeW, 18f),
                        Loc.F("shop.powerGain", gain), _small, UiSkin.Ink.OnPanelWarm);
                }

                var btn = new Rect(r.xMax - UpBtnW - 14f, r.y + 10f, UpBtnW, 36f);
                if (maxed)
                {
                    // "최대"도 흰 글씨 35%였다. 밝은 판 위에서는 아무것도 아니다.
                    UiSkin.LabelClipped(btn, Loc.T("common.max"), _midCenter, UiSkin.Ink.OnPanelMuted);
                }
                else if (UiButton(btn, Loc.F("shop.upgrade", cost), false, afford) && afford)
                {
                    if (RuneUpgrades.TryUpgrade(rune))
                    {
                        AudioManager.Play(Sfx.GradeGreat, 0.7f);
                        _flashRune = rune;
                        _flashAt = Time.unscaledTime;
                    }
                }
            }
        }

        /// <summary>
        /// 레벨 칸. 어두운 홈을 깔고 그 안을 채운다.
        ///
        /// 예전에는 빈 칸을 **흰색 12%**로 그렸는데, 밝은 판 위에서는 보이지 않아
        /// 사실상 "찬 칸만 떠 있는" 그림이었다. 남은 칸이 안 보이면 이 게이지는
        /// 있으나 마나다 — 얼마나 더 갈 수 있는지가 이 줄의 쓸모다.
        /// </summary>
        private void DrawLevelGauge(Rect r, int level)
        {
            int max = PlayerData.MaxRuneLevel;
            float seg = r.width / max;

            for (int k = 0; k < max; k++)
            {
                var cell = new Rect(r.x + k * seg, r.y, seg - 3f, r.height);

                GUI.color = new Color(0.28f, 0.3f, 0.34f, 0.55f);
                GUI.DrawTexture(cell, Texture2D.whiteTexture);

                if (k >= level) continue;

                // 마지막으로 찬 칸은 조금 밝게. 지금 레벨이 몇인지 세지 않고도 읽힌다.
                GUI.color = k == level - 1 ? new Color(0.85f, 1f, 0.62f) : new Color(0.5f, 0.76f, 0.38f);
                GUI.DrawTexture(new Rect(cell.x + 1f, cell.y + 1f, cell.width - 2f, cell.height - 2f),
                    Texture2D.whiteTexture);
            }

            GUI.color = Color.white;
        }

        /// <summary>방금 강화한 룬. 그 줄이 한 번 번쩍인다.</summary>
        private RuneType _flashRune = RuneType.None;
        private float _flashAt = -99f;

        /// <summary>
        /// 강화한 줄을 번쩍인다.
        ///
        /// 주력 숫자는 화면 위쪽에서 차오르는데, 누른 손가락은 아래쪽 버튼에 있다.
        /// 그 사이에 아무 일도 안 일어나면 **누른 자리에서는 파편이 사라진 것만**
        /// 보인다. 누른 곳에서도 무언가 응답해야 한다.
        /// </summary>
        private void DrawUpgradeFlash(Rect r, RuneType rune)
        {
            if (rune != _flashRune) return;

            const float dur = 0.42f;
            float k = (Time.unscaledTime - _flashAt) / dur;
            if (k < 0f || k > 1f) return;

            GUI.color = new Color(1f, 0.97f, 0.8f, 0.55f * (1f - k));
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        // ── 문양 ────────────────────────────────────────────────

        private void DrawGacha()
        {
            const float w = 700f;
            float x = (UiScale.W - w) * 0.5f;
            float y = UiScale.T + 172f;

            bool afford1 = PlayerData.Shards >= GlyphTable.PullCost;
            if (UiButton(new Rect(x, y, 200f, 44f), Loc.F("shop.pull", GlyphTable.PullCost), false, afford1))
                TryPull();

            bool afford10 = PlayerData.Shards >= GlyphTable.MultiCost;
            if (UiButton(new Rect(x + 212f, y, 210f, 44f),
                    Loc.F("shop.pull10", GlyphTable.MultiCost), true, afford10))
                TryPullMulti();

            // 확정 조건을 버튼 옆에 붙여 둔다. 10연차가 왜 있는지가 여기서 읽혀야 한다 —
            // 가격이 같으므로 확정이 유일한 이유다.
            //
            // 버튼과 같은 줄, 오른쪽 남는 자리에 넣는다. 예전엔 버튼 **아래**(y+46)에
            // 뒀는데 룬 고르는 줄이 y+44에서 시작해서 — 힌트가 룬 버튼 위에
            // 겹쳐 찍혔다. 둘 다 읽을 수 없었다.
            UiSkin.LabelClipped(new Rect(x + 434f, y + 12f, w - 434f, 20f),
                Loc.F("shop.pull10Hint", GlyphTable.RarityName(GlyphTable.MultiGuarantee)),
                _smallRight, UiSkin.Ink.Gold);

            // 어느 룬을 손볼지 먼저 고른다. 문양은 이제 룬마다 따로 낀다.
            DrawRunePicker(x, y + 56f, w);

            // 장착 칸을 글씨가 아니라 실제 칸으로 보여준다.
            // 숫자는 읽어야 알지만 빈 칸은 **보면 안다** — 몇 개 더 낄 수 있는지가
            // 이 화면에서 제일 자주 확인하는 정보다.
            DrawEquipSlots(x, y + 102f);

            UiSkin.LabelClipped(new Rect(x + 140f, y + 110f, 300f, 20f),
                Loc.F("shop.owned", PlayerData.Glyphs.Count), _small, UiSkin.Ink.OnDarkDim);

            // 보유 목록
            float listTop = y + 148f;
            float listH = UiScale.B - listTop - 80f;
            var view = new Rect(x, listTop, w, listH);

            List<GlyphData> glyphs = PlayerData.Glyphs;
            float rowH = 46f;
            var content = new Rect(0f, 0f, w - 20f, Mathf.Max(glyphs.Count * (rowH + 4f), listH));

            _scroll = GUI.BeginScrollView(view, _scroll, content);

            if (glyphs.Count == 0)
            {
                UiSkin.LabelClipped(new Rect(0f, 20f, content.width, 24f),
                    Loc.T("shop.empty"), _midCenter, UiSkin.Ink.OnDarkDim);
            }

            // **보이는 줄만 그린다.**
            //
            // GUI.BeginScrollView는 스크롤 밖을 잘라내 줄 뿐, 그리는 일 자체는
            // 그대로 시킨다. 10연차를 몇 번 돌리면 문양이 300개가 되는데 그러면
            // 300줄을 매번 그리고 줄마다 GlyphTable.Describe가 문자열을 만든다.
            // OnGUI가 프레임당 두세 번 도니 프레임마다 문자열 900개다.
            //
            // 화면에 실제로 보이는 건 8줄 남짓이다.
            float step = rowH + 4f;
            int first = Mathf.Max(0, Mathf.FloorToInt(_scroll.y / step) - 1);
            int last = Mathf.Min(glyphs.Count, Mathf.CeilToInt((_scroll.y + listH) / step) + 1);

            for (int i = first; i < last; i++)
            {
                GlyphData g = glyphs[i];
                var r = new Rect(0f, i * step, content.width, rowH);
                bool equipped = PlayerData.IsEquipped(g.id);

                // 장착 중인 문양은 금색 판. 예전엔 같은 회색 판에 초록 물을 들였는데,
                // 색만 다르면 목록을 훑을 때 눈에 안 걸린다. 판 자체가 달라야 한다.
                // 금색을 그대로 쓰면 목록 한 줄이 화면에서 제일 밝은 물건이 된다.
                // 눈에 걸리기만 하면 되므로 톤을 낮춘다.
                if (equipped) UiSkin.DrawPanel(r, new Color(0.93f, 0.86f, 0.72f), UiSkin.PanelKind.Gold);
                else UiSkin.DrawPanel(r, new Color(0.8f, 0.82f, 0.85f));

                // 등급을 왼쪽 색띠 + 보석으로. 목록이 길어지면 글씨보다 그림이 먼저 읽힌다.
                // 띠만 있던 때는 5픽셀이라 등급 넷의 색이 곁눈에 안 갈렸다.
                GUI.color = GlyphTable.RarityColor((GlyphRarity)g.rarity);
                GUI.DrawTexture(new Rect(r.x + 4f, r.y + 6f, 5f, r.height - 12f), Texture2D.whiteTexture);
                GUI.color = Color.white;

                UiSkin.DrawIcon(new Rect(r.x + 14f, r.y + (r.height - 26f) * 0.5f, 26f, 26f),
                    UiSkin.Icon.Gem(g.rarity));

                // 설명 폭은 버튼 자리 앞까지만. IMGUI 라벨은 칸이 넘쳐도 안 멈추므로
                // 길면 장착 버튼 밑으로 파고든다 — 잘라서 "…"로 끝낸다.
                float textW = (r.xMax - 224f) - (r.x + 48f);
                UiSkin.LabelClipped(new Rect(r.x + 48f, r.y + 4f, textW, 22f),
                    GlyphTable.Describe(g), _mid, UiSkin.Ink.OnPanel);

                UiSkin.LabelClipped(new Rect(r.x + 48f, r.y + 25f, textW, 18f),
                    GlyphTable.RarityName((GlyphRarity)g.rarity), _small, UiSkin.Ink.OnPanelDim);

                // 지금 고른 룬에 낄 수 있는 문양인가. 대상이 정해진 문양은
                // 그 룬에만 들어가므로, 못 끼는 것은 버튼을 눌러도 소용이 없다 —
                // 눌리지 않게 해서 왜 안 되는지를 손끝에서 알게 한다.
                bool fits = PlayerData.CanEquipOn(g.id, _slotRune);

                if (UiButton(new Rect(r.xMax - 216f, r.y + 6f, 100f, 34f),
                        Loc.T(equipped ? "shop.unequip" : "shop.equip"), equipped, equipped || fits))
                {
                    bool ok = equipped
                        ? PlayerData.Unequip(g.id)
                        : PlayerData.Equip(g.id, _slotRune);

                    AudioManager.Play(ok ? Sfx.DrawBegin : Sfx.UiDenied, 0.5f, ok ? 1f : 1.7f);
                }

                if (UiButton(new Rect(r.xMax - 108f, r.y + 6f, 100f, 34f),
                        Loc.F("shop.dismantle", GlyphTable.DismantleValue((GlyphRarity)g.rarity))))
                {
                    PlayerData.Dismantle(g.id);
                    AudioManager.Play(Sfx.FailShape, 0.5f);
                    break; // 목록이 바뀌었으므로 이번 프레임은 여기서 끝낸다
                }
            }

            GUI.EndScrollView();
        }

        /// <summary>
        /// 소환 한 번. 결과는 즉시 보관하되 **연출이 끝날 때까지 화면에 알리지 않는다** —
        /// 목록에 새 줄이 먼저 나타나면 연출이 끝나기도 전에 결과를 알아버린다.
        /// </summary>
        private void TryPull()
        {
            if (_reveal.Busy) return;
            if (!PlayerData.TrySpend(GlyphTable.PullCost)) return;

            GlyphData g = PlayerData.AddGlyph(GlyphTable.Roll());

            // 칸이 비어 있으면 바로 끼워 준다. 뽑고 나서 목록을 뒤져 장착까지 해야 하면
            // 소환의 흥이 사무 처리로 끝난다.
            _lastPullAutoEquipped = PlayerData.EquippedOn(_slotRune).Count < PlayerData.SlotsPerRune
                                    && PlayerData.Equip(g.id, _slotRune);

            GrantTickets(1);

            _reveal.Begin(g);
        }

        /// <summary>
        /// 10연차. 결과를 먼저 다 뽑아 저장하고 연출에 넘긴다 —
        /// 카드가 뒤집힐 때마다 뽑으면 중간에 나가버렸을 때 결과가 사라진다.
        /// </summary>
        private void TryPullMulti()
        {
            if (_reveal.Busy) return;
            if (!PlayerData.TrySpend(GlyphTable.MultiCost)) return;

            var rolled = GlyphTable.RollMulti();

            // 문양 10개 추가 + 자동 장착까지 저장이 십수 번 일어난다. 한 번으로 묶는다.
            //
            // **finally로 닫는 게 중요하다.** 중간에서 예외가 나면 배치가 열린 채로
            // 남고, 그때부터 이 판이 끝날 때까지 **모든 저장이 미뤄지기만 하고
            // 파일에 안 써진다.** 강화도 스킨도 스테이지 진행도 전부 사라진다.
            // 파편은 이미 빠져나간 뒤라 더 나쁘다.
            PlayerData.BeginBatch();
            try
            {
                var saved = new List<GlyphData>(rolled.Count);
                for (int i = 0; i < rolled.Count; i++) saved.Add(PlayerData.AddGlyph(rolled[i]));

                // 빈 칸은 좋은 것부터 채운다. 순서대로 끼우면 일반 문양이 자리를 차지하고
                // 정작 전설이 밖에 남는다.
                saved.Sort((a, b) => b.rarity.CompareTo(a.rarity));

                // **지금 고른 룬의 빈 칸만 채운다.** 아홉 룬에 알아서 흩뿌리면
                // 플레이어가 짜 놓은 구성이 뽑기 한 번에 흐트러진다.
                _autoEquippedCount = 0;
                for (int i = 0; i < saved.Count; i++)
                {
                    if (PlayerData.EquippedOn(_slotRune).Count >= PlayerData.SlotsPerRune) break;
                    if (PlayerData.Equip(saved[i].id, _slotRune)) _autoEquippedCount++;
                }
            }
            finally
            {
                PlayerData.EndBatch();
            }

            GrantTickets(rolled.Count);

            _reveal.BeginMulti(rolled);
        }

        /// <summary>
        /// 뽑은 수만큼 각인권을 굴린다.
        ///
        /// 문양 카드와 따로 주는 이유는 GlyphTable 쪽에 적었다 — 각인권은 등급이
        /// 없어서 카드로 만들면 "꽝"처럼 보인다.
        /// </summary>
        private void GrantTickets(int pulls)
        {
            _ticketsFromPull = GlyphTable.RollInscribeTickets(pulls);
            if (_ticketsFromPull > 0) PlayerData.AddInscribeTickets(_ticketsFromPull);
        }

        /// <summary>결과 카드 아래의 조작. 연출이 끝난 뒤에만 뜬다.</summary>
        private void DrawRevealButtons()
        {
            if (!_reveal.ShowingCard) return;

            float cy = UiScale.H * 0.5f - 20f;
            const float bw = 190f, bh = 44f, gap = 12f;
            float total = bw * 2f + gap;
            float x = (UiScale.W - total) * 0.5f;
            float y = cy + (_reveal.IsMulti ? 138f : 118f);

            GUI.color = new Color(1f, 1f, 1f, 0.6f);
            string note = _reveal.IsMulti
                ? (_autoEquippedCount > 0 ? Loc.F("shop.autoEquipN", _autoEquippedCount) : Loc.T("shop.slotsFullHint"))
                : Loc.T(_lastPullAutoEquipped ? "shop.autoEquipHint" : "shop.slotsFullHint");
            GUI.Label(new Rect(0f, y - 26f, UiScale.W, 20f), note, _smallCenter);
            GUI.color = Color.white;

            // **각인권은 카드 밖에서 따로 알린다.** 등급이 없어서 카드로 만들면
            // 꽝처럼 보이고, 카드 격자는 GlyphData 전용이라 끼울 자리도 없다.
            // 색을 문양과 다르게 줘서 "이건 다른 것"이 한눈에 읽히게 했다.
            if (_ticketsFromPull > 0)
            {
                UiSkin.LabelWithIcon(new Rect(0f, y - 48f, UiScale.W, 22f), UiSkin.Icon.Ticket,
                    Loc.F("shop.ticketGain", _ticketsFromPull), _midCenter,
                    new Color(0.72f, 0.92f, 1f), 20f);
            }

            int againCost = _reveal.IsMulti ? GlyphTable.MultiCost : GlyphTable.PullCost;
            bool afford = PlayerData.Shards >= againCost;
            if (UiButton(new Rect(x, y, bw, bh), Loc.F("shop.again", againCost), false, afford))
            {
                bool multi = _reveal.IsMulti;
                _reveal.Dismiss();
                if (multi) TryPullMulti(); else TryPull();
            }

            if (UiButton(new Rect(x + bw + gap, y, bw, bh), Loc.T("shop.confirm"), true))
                _reveal.Dismiss();
        }

        // ── 공용 ────────────────────────────────────────────────

        private bool UiButton(Rect r, string label, bool active = false, bool enabled = true)
        {
            return UiSkin.DrawButton(r, label, _midCenter, active, enabled,
                active ? new Color(0.78f, 1f, 0.7f) : Color.white);
        }

    }
}
