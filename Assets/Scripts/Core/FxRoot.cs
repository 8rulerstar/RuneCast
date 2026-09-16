using UnityEngine;

namespace RuneCast.Core
{
    /// <summary>
    /// 런타임에 생기는 모든 이펙트가 매달리는 자리.
    ///
    /// 이게 없을 때의 문제: 전투를 나가도 이펙트가 그대로 남았다.
    /// BattleManager.ClearField는 유닛과 화살만 지웠는데, 룬 효과는 각자
    /// 루트 GameObject라 아무도 안 건드렸다. 봉화(4.5초)를 깔고 바로 스테이지를
    /// 나가면 **스테이지 선택 화면 한가운데서 불이 계속 탔다.**
    /// 소용돌이·별똥별도 마찬가지고, 별똥별은 낙하 예고까지 떠 있었다.
    ///
    /// 이펙트마다 개별로 정리 코드를 넣는 대신 한 부모 밑에 모은다 —
    /// 새 룬을 추가할 때 정리를 빠뜨릴 수 없게 하는 게 목적이다.
    /// </summary>
    public static class FxRoot
    {
        private static Transform _root;

        public static Transform T
        {
            get
            {
                // 씬을 다시 로드하면 파괴되므로 null 검사를 매번 한다 (Unity의 == 오버로드)
                if (_root == null)
                {
                    var go = new GameObject("[Fx]");
                    _root = go.transform;
                }
                return _root;
            }
        }

        /// <summary>이펙트 오브젝트를 등록한다. worldPositionStays로 위치를 유지한다.</summary>
        public static void Adopt(GameObject go)
        {
            if (go != null) go.transform.SetParent(T, true);
        }

        /// <summary>남아 있는 이펙트를 전부 지운다. 전투를 시작하거나 나갈 때.</summary>
        public static void ClearAll()
        {
            if (_root == null) return;

            for (int i = _root.childCount - 1; i >= 0; i--)
                Object.Destroy(_root.GetChild(i).gameObject);
        }
    }
}
