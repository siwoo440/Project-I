using UnityEngine;

namespace ProjectI
{
    /// <summary>
    /// 경제 · 빚 상환(페이즈) · 판매/구매. (기획서 PART 8)
    /// 골드는 도굴단 공유(공동 지갑). 판매가 = 구매가 × 0.8.
    /// TODO: 빚 페이즈(목표금액+기한), 보물 감정, 상점. (Phase 3 블록 A~B)
    /// </summary>
    public class EconomyManager : MonoBehaviour
    {
        private void Awake()
        {
            Debug.Log("[EconomyManager] 초기화 (스텁)");
        }
    }
}
