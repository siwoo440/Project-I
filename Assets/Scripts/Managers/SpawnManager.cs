using UnityEngine;

namespace ProjectI
{
    /// <summary>
    /// 몬스터·함정·보물 랜덤 스폰. (기획서 PART 6.4, 7.9)
    /// 최종 스폰량 = 던전 스폰테이블 × 밝기 배율 × 리스크 × 인원 스케일링 + 시간 위협.
    /// TODO: 밝기 연동 스폰, 스폰 테이블 로드. (Phase 1 일차 13)
    /// </summary>
    public class SpawnManager : MonoBehaviour
    {
        private void Awake()
        {
            Debug.Log("[SpawnManager] 초기화 (스텁)");
        }
    }
}
