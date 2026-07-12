using UnityEngine;

namespace ProjectI
{
    /// <summary>
    /// 절차적 던전 생성 (하이브리드). (기획서 PART 6.1)
    /// 손으로 만든 방/복도 프리팹을 랜덤 조립. 방 = 밝기 계산 단위.
    /// TODO: 프리팹 조립, 구역 연결, 밀도 배치. (Phase 1 일차 7)
    /// </summary>
    public class DungeonGenerator : MonoBehaviour
    {
        private void Awake()
        {
            Debug.Log("[DungeonGenerator] 초기화 (스텁)");
        }
    }
}
