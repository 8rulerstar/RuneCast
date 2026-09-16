using RuneCast.Gesture;

namespace RuneCast.Meta
{
    /// <summary>
    /// 빨간 점(레드닷) — "여기 들어가면 할 게 있다"는 표시.
    ///
    /// **왜 필요한가:** 지금은 파편이 강화에 충분한지 알려면 대장장이에 직접 들어가
    /// 목록을 훑어야 한다. 스테이지를 깨고 나올 때마다 그걸 확인하는 건 일이고,
    /// 그래서 대부분 안 확인하게 된다 — **모아둔 파편이 그냥 쌓여 있는다.**
    ///
    /// 모바일 성장형 게임이 빨간 점을 다는 이유가 이것이다. 알림 자체가 목적이 아니라
    /// **"쓸 수 있는 자원이 놀고 있다"를 화면 밖에서 알려주는 게** 목적이다.
    ///
    /// 조건을 여기 한 곳에 모은 이유: 점이 떠 있는데 들어가 보면 할 게 없거나,
    /// 할 게 있는데 점이 안 뜨는 게 이런 기능이 망가지는 유일한 방식이다.
    /// 판정과 표시가 떨어져 있으면 반드시 어긋난다.
    /// </summary>
    public static class Notify
    {
        // 세 판정 모두 전체 문양 목록이나 룬 목록을 훑는다. 빨간 점은 타이틀·
        // 스테이지 선택·대장장이 탭에서 매 프레임 물어보는 값이라 그대로 두면
        // 화면에 아무 일이 없어도 계속 순회한다. 저장이 바뀔 때만 다시 판정한다.
        private static int _rev = -1;
        private static int _stageRev = -1;
        private static bool _upgrade, _pull, _unequipped, _achieve;

        private static void Refresh()
        {
            int pv = PlayerData.Revision;
            int sv = StageProgress.Revision + Achievements.Revision;
            if (_rev == pv && _stageRev == sv) return;

            _rev = pv;
            _stageRev = sv;

            _achieve = Achievements.AnyClaimable;
            _upgrade = ComputeCanUpgrade();
            _pull = PlayerData.Shards >= GlyphTable.PullCost;
            _unequipped = ComputeHasUnequipped();
        }

        /// <summary>올릴 수 있는 룬이 하나라도 있는가 (파편이 충분하고 만렙이 아닌).</summary>
        public static bool CanUpgradeAny
        {
            get { Refresh(); return _upgrade; }
        }

        /// <summary>소환을 한 번이라도 돌릴 수 있는가.</summary>
        public static bool CanPull
        {
            get { Refresh(); return _pull; }
        }

        private static bool ComputeCanUpgrade()
        {
            var runes = GlyphTable.Runes;
            int shards = PlayerData.Shards;

            for (int i = 0; i < runes.Length; i++)
            {
                int cost = RuneUpgrades.UpgradeCost(runes[i]);
                if (cost > 0 && shards >= cost) return true;
            }
            return false;
        }

        /// <summary>
        /// 빈 장착 칸에 넣을 문양이 놀고 있는가.
        ///
        /// 뽑아 놓고 안 끼는 게 제일 흔한 손해다 — 소환 연출이 끝나면 그걸로
        /// 끝난 기분이 들어서 장착을 잊는다.
        /// </summary>
        public static bool HasUnequipped
        {
            get { Refresh(); return _unequipped; }
        }

        private static bool ComputeHasUnequipped()
        {
            // **낄 자리가 있는 문양이 하나라도 있는가.** 룬마다 칸이 따로이므로
            // "칸이 남았나"와 "안 낀 문양이 있나"를 따로 물으면 답이 안 나온다 —
            // 대상이 정해진 문양은 그 룬의 칸이 차 있으면 낄 데가 없다.
            var runes = GlyphTable.Runes;
            var all = PlayerData.Glyphs;

            for (int i = 0; i < all.Count; i++)
            {
                if (PlayerData.IsEquipped(all[i].id)) continue;

                for (int r = 0; r < runes.Length; r++)
                {
                    if (PlayerData.EquippedOn(runes[r]).Count >= PlayerData.SlotsPerRune) continue;
                    if (PlayerData.CanEquipOn(all[i].id, runes[r])) return true;
                }
            }

            return false;
        }

        /// <summary>대장장이 버튼에 점을 띄울지. 안쪽 탭 어느 하나라도 할 일이 있으면 뜬다.</summary>
        /// <summary>받을 수 있는 업적 보상이 있는가.</summary>
        public static bool HasAchieveReward
        {
            get { Refresh(); return _achieve; }
        }

        public static bool Workshop
        {
            get { return CanUpgradeAny || CanPull || HasUnequipped || HasAchieveReward; }
        }
    }
}
