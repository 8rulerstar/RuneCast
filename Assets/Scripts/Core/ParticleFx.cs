using UnityEngine;

namespace RuneCast.Core
{
    /// <summary>
    /// 파티클 이펙트 이름표. 실제 프리팹은 `Resources/Particles/`에 있다.
    /// </summary>
    public enum Pfx
    {
        Meteor,      // 별똥별 착탄 (CFXR3 Fire Explosion B)
        Chain,       // 연쇄 번개 타격 (CFXR3 Hit Electric C)
        Heal,        // 회복 (CFXR3 Hit Light B)
        Shield,      // 보호막 (CFXR Impact Glowing Blue)
        Slash,       // 가르기 (CFXR4 Sword Hit Cross)
        Arrow,       // 화살 명중 (CFXR3 Hit Misc A)
        Empower,     // 고양 (CFXR3 Magic Aura Runic — 룬 문자가 도는 오라)
        Revive,      // 소생 (CFXR2 Souls Escape — 혼이 올라간다)
        Pyre,        // 봉화 (CFXR2 Firewall)
        Stars,       // 다중 처치 (CFXR4 Falling Stars)
        Poof,        // 뽑기 카드 (CFXR Magic Poof)
        FireworkBig, // 전설 뽑기 (CFXR4 Firework HDR)
        Frost,       // 서리 장(3장) — 아군이 얼어붙는 순간 (CFXR3 Hit Ice B)
        BossBlast,   // 보스 강타 (Kyeoms 폭발)
        FireworkA,   // 결과 화면 폭죽 (CartoonVFX9X)
        FireworkB,
    }

    /// <summary>
    /// 파티클 프리팹을 얹는 층. 기존 BurstFx(프레임 시트)를 **대체하지 않는다** —
    /// 시트는 픽셀 화풍의 바탕이고, 파티클은 그 위에 얹는 광량이다.
    ///
    /// **왜 프리팹을 Resources로 옮겼나:** 이 프로젝트는 씬 없이 코드로 조립한다
    /// (Bootstrap). 씬에 미리 참조를 꽂아 둘 곳이 없으므로, 실행 중에 찾을 수 있는
    /// 위치는 Resources뿐이다. 팩 전체를 옮기면 안 쓰는 것까지 빌드에 실리니
    /// **쓰는 프리팹만** `tools/slice_ui.py`처럼 골라 옮겼다(.meta째로 — GUID가
    /// 바뀌면 머티리얼 참조가 끊긴다).
    ///
    /// **없으면 조용히 넘어간다.** 파티클은 전부 장식이라, 프리팹이 빠졌다고
    /// 게임이 멈추면 배보다 배꼽이다.
    /// </summary>
    public static class ParticleFx
    {
        private const string Dir = "Particles/";

        // 실패한 로드도 기억한다. 없는 프리팹을 매 호출마다 디스크에서
        // 다시 찾으면, 빠진 에셋 하나가 프레임 드랍으로 바뀐다.
        private static readonly System.Collections.Generic.Dictionary<Pfx, GameObject> Cache =
            new System.Collections.Generic.Dictionary<Pfx, GameObject>();

        private static string Path(Pfx p)
        {
            switch (p)
            {
                case Pfx.Meteor: return Dir + "fx_meteor";
                case Pfx.Chain: return Dir + "fx_chain";
                case Pfx.Heal: return Dir + "fx_heal";
                case Pfx.Shield: return Dir + "fx_shield";
                case Pfx.Slash: return Dir + "fx_slash";
                case Pfx.Arrow: return Dir + "fx_arrow";
                case Pfx.Empower: return Dir + "fx_empower";
                case Pfx.Revive: return Dir + "fx_revive";
                case Pfx.Pyre: return Dir + "fx_pyre";
                case Pfx.Stars: return Dir + "fx_stars";
                case Pfx.Poof: return Dir + "fx_poof";
                case Pfx.FireworkBig: return Dir + "fx_firework_big";
                case Pfx.Frost: return Dir + "fx_frost";
                case Pfx.BossBlast: return Dir + "fx_bossblast";
                case Pfx.FireworkA: return Dir + "fx_firework_a";
                default: return Dir + "fx_firework_b";
            }
        }

        /// <summary>
        /// 하나 터뜨린다. scale은 프리팹 기준 배율(1이 원본 크기).
        ///
        /// sortingOrder를 직접 받는 이유: 전투 스프라이트가 40~55대를 쓴다.
        /// 파티클이 그 밑에 깔리면 유닛에 가려 "뭔가 번쩍했다"만 남는다.
        /// </summary>
        public static void Spawn(Pfx p, Vector3 pos, float scale = 1f, int sortingOrder = 60)
        {
            GameObject prefab;
            if (!Cache.TryGetValue(p, out prefab))
            {
                prefab = Resources.Load<GameObject>(Path(p));
                Cache[p] = prefab;
            }
            if (prefab == null) return;

            GameObject go = Object.Instantiate(prefab, pos, Quaternion.identity, FxRoot.T);
            go.transform.localScale = prefab.transform.localScale * scale;

            float life = 0.5f;
            ParticleSystem[] systems = go.GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < systems.Length; i++)
            {
                ParticleSystem.MainModule main = systems[i].main;

                // **루프는 여기서 끈다.** 이 스포너는 쏘고 잊는(fire-and-forget)
                // 물건이라 루프 프리팹이 섞이면 영원히 남는다. 깔아 두는 이펙트가
                // 필요해지면 참조를 돌려주는 별도 진입로를 만들 것 — BurstFx의
                // StopLoop이 그렇게 한다.
                main.loop = false;

                float end = main.duration + main.startLifetime.constantMax + main.startDelay.constantMax;
                if (end > life) life = end;

                var r = systems[i].GetComponent<ParticleSystemRenderer>();
                if (r != null) r.sortingOrder = sortingOrder;
            }

            // 프리팹에 자체 정리 스크립트(CFXR_Effect)가 있어도 그대로 둔다 —
            // 이 Destroy가 겹쳐 불려도 이미 없는 물건이면 Unity가 무시한다.
            Object.Destroy(go, life + 0.5f);
        }

        /// <summary>
        /// 화면 좌표(가상, 좌상단 원점)에서 터뜨린다. 메뉴 화면용 —
        /// IMGUI 위에는 파티클을 그릴 수 없으니 카메라 앞 세계 좌표로 바꿔 얹는다.
        /// </summary>
        public static void SpawnAtGui(Pfx p, Vector2 guiPos, float scale = 1f)
        {
            Camera cam = Camera.main;
            if (cam == null) return;

            Rect r = UiScale.ToScreenRect(new Rect(guiPos.x, guiPos.y, 0f, 0f));
            float depth = Mathf.Abs(cam.transform.position.z);
            Vector3 world = cam.ScreenToWorldPoint(new Vector3(r.x, r.y, depth > 0.1f ? depth : 10f));

            // 메뉴 배경 위, UI(스크림)보다 아래가 없다 — IMGUI는 항상 맨 위다.
            // 그래도 배경 알갱이(sortingOrder 낮음)보다는 위에 떠야 보인다.
            Spawn(p, world, scale, 80);
        }
    }
}
