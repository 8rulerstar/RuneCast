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
    /// 화면 아래 카드 줄 — 끌어다 놓아 아군을 낸다 (클래시 로얄·냥코 방식).
    ///
    /// **입력이 룬과 겹친다. 그게 이 화면의 전부다.**
    /// 둘 다 화면을 드래그하는 조작이라, 무엇을 하려던 건지 구분할 방법이
    /// 없으면 유닛을 내려다 룬이 나가고 룬을 그리다 유닛이 떨어진다.
    ///
    /// 클래시 로얄이 쓰는 규칙을 그대로 쓴다 — **어디서 시작했는가로 가른다.**
    ///  · 카드 위에서 시작한 드래그 = 배치
    ///  · 그 밖에서 시작한 드래그 = 룬
    ///
    /// 이걸 `TraceCapture.BlockScreenRect`로 못 박는다. 이미 화면 버튼(일시정지)이
    /// 쓰던 장치라 새로 만들 것이 없었다 — 카드 줄이 매 프레임 자기 자리를
    /// 등록하면 거기서 시작한 획은 아예 안 만들어진다.
    ///
    /// **마나를 룬과 나눠 쓴다.** 엘릭서를 따로 두지 않은 이유는 그러면
    /// "룬을 쓸까 유닛을 낼까"가 사라지기 때문이다. 자원이 하나여야 선택이 있다.
    /// </summary>
    public class DeployBar : MonoBehaviour
    {
        public GameFlow flow;
        public BattleManager battle;
        public ManaPool mana;

        private GUIStyle _name, _cost;
        private readonly List<DeckCard> _hand = new List<DeckCard>();

        /// <summary>지금 끌고 있는 카드. -1이면 없음.</summary>
        private int _drag = -1;
        private Vector2 _dragPos;

        // 카드 크기. 아래에 깔리므로 전장을 너무 먹으면 안 되지만,
        // 손가락으로 집어야 하니 작아도 안 된다.
        private const float MaxCardW = 116f, CardH = 132f, Gap = 12f;

        /// <summary>
        /// 지금 카드 줄이 먹는 높이. 0이면 안 떠 있다.
        ///
        /// **전투 HUD가 이 값만큼 마나 게이지를 올린다.** 둘 다 화면 아래에
        /// 붙는데, 안 비키면 카드가 게이지를 덮는다 — 클래시 로얄이 엘릭서 바를
        /// 카드 바로 위에 두는 것과 같은 자리 배분이다.
        /// </summary>
        public static float BarHeight { get; private set; }

        private bool InBattle
        {
            get
            {
                return flow != null && flow.State == GameState.Battle && !flow.Paused
                    && battle != null && battle.Running
                    && flow.CurrentStage != null && !flow.CurrentStage.IsPrologue;
            }
        }

        private void EnsureStyles()
        {
            if (_name != null) return;
            _name = new GUIStyle(GUI.skin.label)
                { fontSize = UiSkin.Text.Mid, alignment = TextAnchor.MiddleCenter };
            _cost = new GUIStyle(GUI.skin.label)
                { fontSize = UiSkin.Text.Title, alignment = TextAnchor.MiddleCenter };
            UiSkin.ApplyFont(_name, _cost);
        }

        private void OnGUI()
        {
            if (!InBattle) { _drag = -1; BarHeight = 0f; return; }

            EnsureStyles();
            UiScale.Begin();
            Draw();
            UiScale.End();
        }

        private Rect BarRect(out float x0, out float cardW)
        {
            _hand.Clear();
            _hand.AddRange(Deck.Hand());

            int n = Mathf.Max(1, _hand.Count);

            // **좁은 화면에서 넘치면 안 된다.** 세로 폰의 가상 폭이 360인데
            // 116짜리 카드 셋(+여백)이면 372라 화면 밖으로 나간다.
            cardW = Mathf.Min(MaxCardW, (UiScale.W - 48f - (n - 1) * Gap) / n);

            float total = n * cardW + (n - 1) * Gap;
            x0 = (UiScale.W - total) * 0.5f;
            float y = UiScale.B - CardH - 12f;
            return new Rect(x0 - 10f, y - 10f, total + 20f, CardH + 20f);
        }

        private void Draw()
        {
            float x0, cardW;
            Rect bar = BarRect(out x0, out cardW);
            if (_hand.Count == 0) { BarHeight = 0f; return; }

            BarHeight = bar.height;

            // **여기서 시작한 드래그는 획이 되지 않는다.** 매 프레임 등록해야 한다 —
            // TraceCapture가 매 프레임 목록을 비운다.
            TraceCapture.BlockScreenRect(UiScale.ToScreenRect(bar));

            UiSkin.DrawPanel(bar, new Color(0.14f, 0.13f, 0.16f, 0.80f));

            float y = bar.y + 10f;
            for (int i = 0; i < _hand.Count; i++)
            {
                var card = _hand[i];
                var r = new Rect(x0 + i * (cardW + Gap), y, cardW, CardH);

                bool afford = mana == null || mana.unlimited || mana.Current >= card.Cost;
                DrawCard(r, card, afford, i == _drag);
                HandleCardInput(r, i, afford);
            }

            if (_drag >= 0) DrawGhost();
        }

        /// <summary>
        /// 카드 한 장.
        ///
        /// 그림은 그 용사의 스프라이트 첫 칸이다 — 목록 카드와 같은 방식이라
        /// 새로 그릴 것이 없고, 유닛을 바꾸면 카드도 저절로 따라온다.
        /// </summary>
        private void DrawCard(Rect r, DeckCard card, bool afford, bool dragging)
        {
            // 못 내는 카드는 어둡게. 마나 게이지를 읽지 않아도 손이 안 간다.
            Color bg = dragging ? new Color(0.95f, 0.92f, 0.72f, 0.98f)
                     : afford   ? new Color(0.93f, 0.90f, 0.80f, 0.96f)
                                : new Color(0.55f, 0.54f, 0.56f, 0.92f);
            UiSkin.DrawPanel(r, bg);

            var def = UnitVisuals.Of(card.Kind);
            Texture2D sheet = Resources.Load<Texture2D>("Sprites/Units/" + def.Prefix + "-Idle");
            UiSkin.DrawSpriteFrame(new Rect(r.x, r.y + 4f, r.width, r.height - 44f),
                sheet, def.FrameSize, 0,
                afford ? Color.white : new Color(0.35f, 0.35f, 0.4f, 0.9f), 1.8f);

            GUI.color = afford ? UiSkin.Ink.OnPanel : UiSkin.Ink.OnPanelMuted;
            GUI.Label(new Rect(r.x, r.yMax - 46f, r.width, 22f), Loc.T(card.NameKey), _name);

            // 비용은 크게. 이 화면에서 카드를 고르는 근거가 사실상 이 숫자뿐이다.
            GUI.color = afford ? UiSkin.Ink.OnPanelWarm : UiSkin.Ink.OnPanelMuted;
            GUI.Label(new Rect(r.x, r.yMax - 30f, r.width, 28f), card.Cost.ToString(), _cost);
            GUI.color = Color.white;
        }

        /// <summary>
        /// 집고 끌고 놓는다.
        ///
        /// **놓는 순간에만 마나를 쓴다.** 집는 순간에 빼면 끌다가 마음이 바뀌어
        /// 되돌릴 때 돌려줘야 하고, 그 되돌리기를 빠뜨리는 곳이 반드시 생긴다.
        /// </summary>
        private void HandleCardInput(Rect r, int index, bool afford)
        {
            Event e = Event.current;

            if (e.type == EventType.MouseDown && r.Contains(e.mousePosition) && afford)
            {
                _drag = index;
                _dragPos = e.mousePosition;
                e.Use();
            }
            else if (e.type == EventType.MouseDrag && _drag == index)
            {
                _dragPos = e.mousePosition;
                e.Use();
            }
            else if (e.type == EventType.MouseUp && _drag == index)
            {
                TryDrop(_hand[index]);
                _drag = -1;
                e.Use();
            }
        }

        private void TryDrop(DeckCard card)
        {
            Camera cam = Camera.main;
            if (cam == null || battle == null) return;

            Vector3 world = cam.ScreenToWorldPoint(UiScale.ToScreenPoint(_dragPos));
            world.z = 0f;

            // 자기 쪽이 아니면 아무 일도 없다. 마나도 안 쓴다 —
            // 못 놓는 자리에 놓았다고 자원을 뺏으면 규칙이 아니라 벌이다.
            if (!Deck.CanDeployAt(world))
            {
                AudioManager.Play(Sfx.FailMana, 0.7f, 0.66f);
                return;
            }

            if (mana != null && !mana.unlimited)
            {
                if (mana.Current < card.Cost)
                {
                    AudioManager.Play(Sfx.FailMana, 0.8f);
                    return;
                }
                mana.Spend(card.Cost);
            }

            battle.DeployHero(card.Kind, world);

            AudioManager.Play(Sfx.DrawBegin, 0.7f);
            BurstFx.Play(Vfx.HealWave, world, 1.3f, new Color(0.7f, 0.9f, 1f), 46);
        }

        /// <summary>
        /// 끌고 있는 동안 손끝에 유닛이 붙어 다닌다.
        ///
        /// **놓을 수 있는 자리인지 색으로 미리 말한다.** 놓아 보고 나서야
        /// 안 되는 걸 알면, 그 사이 몇 초가 통째로 낭비다.
        /// </summary>
        private void DrawGhost()
        {
            Camera cam = Camera.main;
            if (cam == null) return;

            Vector3 world = cam.ScreenToWorldPoint(UiScale.ToScreenPoint(_dragPos));
            world.z = 0f;
            bool ok = Deck.CanDeployAt(world);

            var def = UnitVisuals.Of(_hand[_drag].Kind);
            Texture2D sheet = Resources.Load<Texture2D>("Sprites/Units/" + def.Prefix + "-Idle");

            var box = new Rect(_dragPos.x - 52f, _dragPos.y - 62f, 104f, 104f);
            UiSkin.DrawSpriteFrame(box, sheet, def.FrameSize, 0,
                ok ? new Color(1f, 1f, 1f, 0.9f) : new Color(1f, 0.5f, 0.45f, 0.7f), 1.8f);

            // 놓일 자리를 바닥에 표시한다. 유닛 그림만 따라다니면 발이 어디에
            // 닿는지 알 수 없어서, 놓고 나면 늘 생각보다 위/아래에 선다.
            GUI.color = ok ? new Color(0.6f, 1f, 0.7f, 0.5f) : new Color(1f, 0.4f, 0.35f, 0.45f);
            GUI.DrawTexture(new Rect(_dragPos.x - 34f, _dragPos.y - 6f, 68f, 12f),
                Texture2D.whiteTexture);
            GUI.color = Color.white;
        }
    }
}
