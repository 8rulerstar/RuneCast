using UnityEngine;
using RuneCast.Core;
using RuneCast.Meta;

namespace RuneCast.UI
{
    /// <summary>
    /// 장마다 화면 전체에 얇은 색을 깐다.
    ///
    /// **바닥 색만으로는 절반만 바뀐다.** `Backdrop.TintOf`가 타일을 물들이지만
    /// 유닛과 이펙트는 그대로라, 서리 장에서도 오크는 여전히 한여름 초록이다.
    /// 화면 위에 한 겹을 덮으면 그 위의 모든 것이 같은 공기를 쓴다.
    ///
    /// **아주 옅게(최대 0.16) 깐다.** 이 게임은 화면 위에 손으로 선을 긋는
    /// 게임이라, 막이 두꺼우면 궤적과 배경의 대비가 같이 죽는다. 배경 타일은
    /// 이미 물들어 있으므로 여기서는 "덮는" 게 아니라 "얹는" 정도면 된다.
    ///
    /// 안개(4장)만 조금 더 두껍다 — 그 장의 규칙 자체가 "잘 안 보인다"라
    /// 화면이 흐린 것이 곧 설명이다.
    /// </summary>
    public class ChapterAtmosphere : MonoBehaviour
    {
        public GameFlow flow;

        /// <summary>
        /// HUD보다 뒤에 그린다. IMGUI는 depth가 클수록 먼저(= 뒤에) 그려진다.
        ///
        /// 스크립트 실행 순서에 기대면 안 된다 — 그건 프로젝트 설정에 따라
        /// 달라지고, 어느 날 조용히 HUD 위에 막이 덮인다.
        /// </summary>
        private const int Depth = 100;

        private void OnGUI()
        {
            if (Event.current.type != EventType.Repaint) return;
            if (flow == null || flow.State != GameState.Battle) return;

            Color c = TintOf(ChapterRules.Active);
            if (c.a <= 0f) return;

            int saved = GUI.depth;
            GUI.depth = Depth;

            GUI.color = c;
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUI.depth = saved;
        }

        /// <summary>
        /// 장 규칙별 공기.
        ///
        /// 1·2장은 0이다 — 이미 만들어 둔 화면이고, 여기서 색을 얹으면
        /// 지금까지 맞춰 온 초원·밤의 인상이 바뀐다. 새 장에만 건다.
        /// </summary>
        private static Color TintOf(ChapterRule rule)
        {
            switch (rule)
            {
                case ChapterRule.Frost:   return new Color(0.55f, 0.75f, 1.00f, 0.16f);

                // **안개만 두껍다(0.30).** 다른 장의 막은 분위기지만 이 장은
                // 막 자체가 규칙이다 — "잘 안 보인다"를 화면이 직접 말한다.
                case ChapterRule.Fog:     return new Color(0.86f, 0.88f, 0.90f, 0.30f);

                case ChapterRule.Plague:  return new Color(0.62f, 0.85f, 0.35f, 0.10f);
                case ChapterRule.Sealing: return new Color(0.62f, 0.45f, 0.90f, 0.11f);
                case ChapterRule.Abyss:   return new Color(0.42f, 0.06f, 0.12f, 0.20f);
                default:                  return new Color(0f, 0f, 0f, 0f);
            }
        }
    }
}
