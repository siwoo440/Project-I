using UnityEngine;

namespace ProjectI
{
    /// <summary>
    /// 밝기 시스템 ★핵심. (기획서 PART 5)
    /// 최종 밝기 = 구역 기본(0) + 고정광원(횃불대) + 휴대광원(횃불/랜턴). 구역(Room) 단위.
    /// 밝기 5단계 → 시야·몬스터·함정·보물 스폰에 영향.
    /// TODO: 구역별 밝기 계산, 광원 합산, 밝기 UI 연동. (Phase 1 일차 6)
    /// </summary>
    public class LightSystem : MonoBehaviour
    {
        private void Awake()
        {
            Debug.Log("[LightSystem] 초기화 (스텁)");
        }
    }
}
