using UnityEngine; // 유니티 컴포넌트 직렬화 기능 참조

namespace ProjectI.Economy // 사무소 경제 기능 네임스페이스
{
    [DisallowMultipleComponent] // 한 아이템에 사무소 보관 상태 중복 부착 방지
    public sealed class OfficeStoredItemState : MonoBehaviour // 전멸과 원정 손실에서 보호되는 사무소 단상 보관 상태
    {
        [SerializeField] private bool isOfficeStored; // 현재 사무소 단상 영구 보관 여부
        [SerializeField] private OfficeStoragePedestal pedestal; // 현재 아이템을 보관하고 있는 단상 참조

        public bool IsOfficeStored => isOfficeStored; // 원정 손실 시스템용 사무소 보호 여부 공개
        public OfficeStoragePedestal Pedestal => pedestal; // 현재 보관 단상 공개

        public void SetStored(OfficeStoragePedestal targetPedestal, bool stored) // 사무소 단상 보관 상태 갱신
        {
            isOfficeStored = stored && targetPedestal != null; // 유효 단상이 있을 때만 영구 보관 상태 활성화
            pedestal = isOfficeStored ? targetPedestal : null; // 보관 해제 시 단상 참조 제거
        }
    }
}
