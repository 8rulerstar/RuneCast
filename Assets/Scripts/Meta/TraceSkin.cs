using UnityEngine;

namespace RuneCast.Meta
{
    public enum SkinUnlock
    {
        Always,     // 처음부터
        Stars,      // 별을 모으면
        Shards,     // 파편으로 산다
        Achieve,    // 업적으로 열린다
    }

    public struct TraceSkinDef
    {
        public string Key;          // Loc 키 접미사 (skin.<Key>)
        public Color Core;          // 밝은 심
        public Color Glow;          // 굵고 옅은 글로우
        public float WidthScale;
        public SkinUnlock Unlock;
        public int Amount;          // 별 수 / 파편 값
        public Achievement Need;    // Unlock == Achieve 일 때
    }

    /// <summary>
    /// 궤적 스킨.
    ///
    /// **꾸미기를 붙일 자리가 여기밖에 없다.** 이 게임에는 조작하는 캐릭터가 없고,
    /// 유닛은 자동으로 싸우는 배경에 가깝다. 반면 **플레이어가 화면에서 제일 오래
    /// 쳐다보는 물건은 자기가 그은 선**이다. 그 선의 색과 굵기를 바꾸는 게
    /// 이 게임에서 유일하게 "내 것"이라고 느껴질 수 있는 꾸미기다.
    ///
    /// 전투에는 아무 영향이 없다. 성능이 붙으면 꾸미기가 아니라 밸런스가 되고,
    /// 그러면 마음에 드는 걸 고를 수 없게 된다 — 제일 센 걸 써야 하니까.
    /// </summary>
    public static class TraceSkins
    {
        public static readonly TraceSkinDef[] All =
        {
            new TraceSkinDef
            {
                Key = "default", WidthScale = 1f, Unlock = SkinUnlock.Always,
                Core = new Color(0.60f, 0.88f, 1.00f, 0.95f),
                Glow = new Color(0.35f, 0.55f, 1.00f, 0.40f),
            },
            new TraceSkinDef
            {
                Key = "ember", WidthScale = 1.05f, Unlock = SkinUnlock.Stars, Amount = 6,
                Core = new Color(1.00f, 0.82f, 0.45f, 0.95f),
                Glow = new Color(1.00f, 0.42f, 0.12f, 0.42f),
            },
            new TraceSkinDef
            {
                Key = "moss", WidthScale = 0.95f, Unlock = SkinUnlock.Stars, Amount = 14,
                Core = new Color(0.72f, 1.00f, 0.70f, 0.95f),
                Glow = new Color(0.24f, 0.70f, 0.35f, 0.40f),
            },
            new TraceSkinDef
            {
                Key = "ash", WidthScale = 0.85f, Unlock = SkinUnlock.Shards, Amount = 400,
                Core = new Color(0.92f, 0.92f, 0.95f, 0.95f),
                Glow = new Color(0.45f, 0.47f, 0.55f, 0.40f),
            },
            new TraceSkinDef
            {
                Key = "wraith", WidthScale = 1.15f, Unlock = SkinUnlock.Achieve,
                Need = Achievement.WraithSlayer,
                Core = new Color(0.72f, 0.92f, 1.00f, 0.90f),
                Glow = new Color(0.30f, 0.80f, 0.95f, 0.46f),
            },
            new TraceSkinDef
            {
                Key = "gold", WidthScale = 1.10f, Unlock = SkinUnlock.Achieve,
                Need = Achievement.Perfectionist,
                Core = new Color(1.00f, 0.95f, 0.72f, 0.98f),
                Glow = new Color(1.00f, 0.75f, 0.20f, 0.48f),
            },
            new TraceSkinDef
            {
                Key = "void", WidthScale = 1.20f, Unlock = SkinUnlock.Achieve,
                Need = Achievement.Conqueror,
                Core = new Color(0.86f, 0.70f, 1.00f, 0.95f),
                Glow = new Color(0.52f, 0.18f, 0.85f, 0.50f),
            },
        };

        public static string Name(TraceSkinDef d)
        {
            return Loc.T("skin." + d.Key);
        }

        public static bool IsUnlocked(TraceSkinDef d)
        {
            switch (d.Unlock)
            {
                case SkinUnlock.Always: return true;
                case SkinUnlock.Stars: return StageProgress.TotalStars >= d.Amount;
                case SkinUnlock.Achieve: return Achievements.Done(d.Need);
                case SkinUnlock.Shards: return PlayerData.OwnsSkin(d.Key);
                default: return false;
            }
        }

        /// <summary>왜 아직 못 쓰는지. 잠긴 것만 이유를 보여줘야 목표가 된다.</summary>
        public static string LockReason(TraceSkinDef d)
        {
            switch (d.Unlock)
            {
                case SkinUnlock.Stars: return Loc.F("skin.needStars", d.Amount);
                case SkinUnlock.Achieve: return Achievements.Name(d.Need);
                case SkinUnlock.Shards: return Loc.F("skin.buy", d.Amount);
                default: return "";
            }
        }

        public static TraceSkinDef Current
        {
            get
            {
                string key = PlayerData.SkinKey;
                for (int i = 0; i < All.Length; i++)
                    if (All[i].Key == key && IsUnlocked(All[i])) return All[i];

                return All[0];
            }
        }
    }
}
