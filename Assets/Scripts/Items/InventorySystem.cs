using UnityEngine;

namespace ProjectI
{
    /// <summary>
    /// 인벤토리 · 무게 · 아이템. (기획서 PART 4.4, 8)
    /// 핫바 5칸 + 무게 한계(둘 다 적용). 활성 손 슬롯 1개(휠 전환).
    /// 무게 구간별 페널티(이동속도·스태미너 회복).
    /// TODO: 슬롯/무게, 줍기·버리기, 두손 운반 보물. (Phase 1 일차 4, 8)
    /// </summary>
    public class InventorySystem : MonoBehaviour
    {
        private void Awake()
        {
            Debug.Log("[InventorySystem] 초기화 (스텁)");
        }
    }
}
