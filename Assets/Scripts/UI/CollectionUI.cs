using System;
using UnityEngine;
using RuneCast.Core;
using RuneCast.Meta;

namespace RuneCast.UI
{
    /// <summary>
    /// 대장장이의 "기록" 탭 — 업적과 궤적 스킨.
    ///
    /// 둘을 한 화면에 둔 이유: **업적이 스킨을 연다.** 따로 두면 왜 이 스킨이
    /// 잠겨 있는지 보려고 화면을 오가야 하고, 그러면 업적이 목표로 안 읽힌다.
    ///
    /// WorkshopUI에서 그린다. 화면을 또 만들지 않은 건 대장장이가 이미
    /// "모아둔 것을 쓰는 곳"이고, 여기가 그 안의 한 탭이면 충분해서다.
    /// </summary>
    public class CollectionUI
    {
        private Vector2 _scroll;

        // 업적 줄마다 "12 / 30" 같은 진행 문구와 "파편 250 받기" 버튼 글자를
        // 만든다. 화면에 여덟 줄이 보이고 OnGUI가 프레임당 두세 번 도니
        // 프레임마다 문자열이 수십 개씩 생긴다 — 값은 룬을 쓸 때나 바뀌는데.
        private string[] _progressText;
        private string[] _claimText;
        private int _textRev = -1;

        private void EnsureText(Achievement[] values)
        {
            // 세는 숫자와 달성 상태를 둘 다 본다
            int rev = Achievements.Revision + Achievements.CounterRevision + PlayerData.Revision;
            if (_progressText != null && _textRev == rev) return;

            _textRev = rev;
            if (_progressText == null || _progressText.Length != values.Length)
            {
                _progressText = new string[values.Length];
                _claimText = new string[values.Length];
            }

            for (int i = 0; i < values.Length; i++)
            {
                _progressText[i] = Achievements.ProgressText(values[i]);
                _claimText[i] = Loc.F("ach.claim", Achievements.Reward(values[i]));
            }
        }

        public void Draw(GUIStyle mid, GUIStyle small, GUIStyle midCenter, GUIStyle smallCenter,
            Func<Rect, string, bool, bool, bool> button)
        {
            float top = UiScale.T + 172f;
            float bottom = UiScale.B - 80f;

            const float w = 660f;
            float x = (UiScale.W - w) * 0.5f;

            // ── 궤적 스킨 ────────────────────────────────────────
            //
            // 소제목은 띠 위에 얹는다. 맨 글씨는 목록의 첫 줄과 안 갈린다 —
            // 특히 이 화면은 스킨과 업적 두 목록이 이어져 있어서, 경계가
            // 글씨 하나뿐이면 어디서 목록이 바뀌는지 훑어서는 모른다.
            UiSkin.DrawRibbon(new Rect(x + w * 0.5f - 90f, top - 2f, 180f, 18f),
                Loc.T("skin.title"), smallCenter);

            const float sw = 156f, sh = 62f, gap = 8f;
            var skins = TraceSkins.All;
            int cols = 4;

            for (int i = 0; i < skins.Length; i++)
            {
                TraceSkinDef d = skins[i];
                var r = new Rect(x + (i % cols) * (sw + gap),
                                 top + 26f + (i / cols) * (sh + gap), sw, sh);

                bool open = TraceSkins.IsUnlocked(d);
                bool cur = open && TraceSkins.Current.Key == d.Key;

                UiSkin.DrawSlot(r, open ? Color.white : new Color(0.42f, 0.44f, 0.5f), cur);

                // 스킨은 색이 전부다. **이름보다 색 견본이 먼저 보여야 한다.**
                var swatch = new Rect(r.x + 10f, r.y + 10f, r.width - 20f, 12f);
                GUI.color = open ? new Color(d.Glow.r, d.Glow.g, d.Glow.b, 0.85f)
                                 : new Color(0.3f, 0.3f, 0.34f, 0.8f);
                GUI.DrawTexture(new Rect(swatch.x - 2f, swatch.y - 3f, swatch.width + 4f, swatch.height + 6f),
                    Texture2D.whiteTexture);
                GUI.color = open ? d.Core : new Color(0.45f, 0.45f, 0.5f);
                GUI.DrawTexture(swatch, Texture2D.whiteTexture);

                GUI.color = open ? new Color(0.16f, 0.15f, 0.13f) : new Color(1f, 1f, 1f, 0.6f);
                GUI.Label(new Rect(r.x, r.y + 26f, r.width, 20f), TraceSkins.Name(d), smallCenter);

                GUI.color = new Color(0.35f, 0.33f, 0.3f, open ? 0.8f : 0f);
                GUI.Label(new Rect(r.x, r.y + 42f, r.width, 18f),
                    cur ? Loc.T("skin.using") : "", smallCenter);
                GUI.color = Color.white;

                if (!open)
                {
                    // 왜 못 쓰는지. 이유가 없으면 잠긴 칸은 그냥 빈칸이다.
                    GUI.color = new Color(1f, 0.85f, 0.55f, 0.85f);
                    GUI.Label(new Rect(r.x, r.y + 42f, r.width, 18f),
                        TraceSkins.LockReason(d), smallCenter);
                    GUI.color = Color.white;
                }

                // 판정을 그림 위에 겹치지 않게 투명 버튼으로 받는다
                if (!GUI.Button(r, GUIContent.none, GUIStyle.none)) continue;

                if (cur) continue;

                if (open)
                {
                    PlayerData.SelectSkin(d.Key);
                    AudioManager.Play(Sfx.UiClick, 0.5f, 1.45f);
                }
                else if (d.Unlock == SkinUnlock.Shards && PlayerData.BuySkin(d.Key, d.Amount))
                {
                    PlayerData.SelectSkin(d.Key);
                    AudioManager.Play(Sfx.RuneLearned, 0.8f, 1.15f);
                }
                else
                {
                    AudioManager.Play(Sfx.UiDenied, 0.5f, 1.7f);
                }
            }

            int skinRows = (skins.Length + cols - 1) / cols;
            float listTop = top + 26f + skinRows * (sh + gap) + 14f;

            // ── 업적 ─────────────────────────────────────────────
            UiSkin.DrawRibbon(new Rect(x + w * 0.5f - 90f, listTop - 2f, 180f, 18f),
                Loc.T("ach.title"), smallCenter);

            listTop += 26f;
            float listH = Mathf.Max(60f, bottom - listTop);

            var values = (Achievement[])Enum.GetValues(typeof(Achievement));
            EnsureText(values);
            const float rowH = 52f;
            var content = new Rect(0f, 0f, w - 20f, Mathf.Max(values.Length * (rowH + 4f), listH));

            _scroll = GUI.BeginScrollView(new Rect(x, listTop, w, listH), _scroll, content);

            float step = rowH + 4f;
            int first = Mathf.Max(0, Mathf.FloorToInt(_scroll.y / step) - 1);
            int last = Mathf.Min(values.Length, Mathf.CeilToInt((_scroll.y + listH) / step) + 1);

            for (int i = first; i < last; i++)
            {
                Achievement a = values[i];
                var r = new Rect(0f, i * step, content.width, rowH);

                bool done = Achievements.Done(a);
                bool claimed = Achievements.Claimed(a);

                if (done && !claimed) UiSkin.DrawPanel(r, new Color(0.95f, 0.88f, 0.7f), UiSkin.PanelKind.Gold);
                else UiSkin.DrawPanel(r, done ? new Color(0.84f, 0.87f, 0.9f)
                                              : new Color(0.62f, 0.64f, 0.68f));

                // 세는 업적은 얼마나 왔는지 막대로. 숫자만 있으면 남은 양이 안 읽힌다.
                // **막대가 있는 줄은 설명 폭을 막대 앞에서 끊는다** — 예전에는 설명이
                // 400 너비로 막대 시작(끝에서 300)을 지나 그 밑으로 흘렀다.
                string prog = _progressText[i];
                bool hasBar = !string.IsNullOrEmpty(prog) && !done;
                float descW = (hasBar ? r.xMax - 306f : r.xMax - 170f) - (r.x + 14f);

                var nameC = new Color(0.14f, 0.13f, 0.12f, done ? 1f : 0.75f);
                UiSkin.LabelClipped(new Rect(r.x + 14f, r.y + 5f, r.xMax - 170f - r.x - 14f, 22f),
                    Achievements.Name(a), mid, nameC);

                var descC = new Color(0.34f, 0.32f, 0.3f, done ? 0.9f : 0.7f);
                UiSkin.LabelClipped(new Rect(r.x + 14f, r.y + 27f, descW, 20f),
                    Achievements.Desc(a), small, descC);

                if (hasBar)
                {
                    var bar = new Rect(r.xMax - 300f, r.y + 30f, 130f, 12f);
                    UiSkin.DrawBar(bar, Achievements.Progress(a), new Color(0.55f, 0.8f, 1f));

                    UiSkin.LabelClipped(new Rect(bar.xMax + 6f, r.y + 26f, 80f, 20f), prog, small,
                        new Color(0.3f, 0.29f, 0.27f));
                }

                var btn = new Rect(r.xMax - 156f, r.y + 8f, 140f, 36f);
                if (claimed)
                {
                    GUI.color = new Color(0.3f, 0.29f, 0.27f, 0.8f);
                    GUI.Label(btn, Loc.T("ach.claimed"), midCenter);
                    GUI.color = Color.white;
                }
                else if (button(btn, _claimText[i], done, done))
                {
                    Achievements.Claim(a);
                }
            }

            GUI.EndScrollView();
        }
    }
}
