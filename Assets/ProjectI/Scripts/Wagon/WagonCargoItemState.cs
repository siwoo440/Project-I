using UnityEngine; // 유니티 컴포넌트와 직렬화 기능 참조

namespace ProjectI.Wagon // 마차 시스템 네임스페이스
{
    [DisallowMultipleComponent] // 한 아이템에 확보 상태 컴포넌트 중복 방지
    public sealed class WagonCargoItemState : MonoBehaviour // 월드 아이템의 현재 마차 확보 상태 기록
    {
        [SerializeField] private bool isSecured; // 현재 마차 적재 구역 확보 여부
        [SerializeField] private WagonCargoArea securedArea; // 현재 확보 판정을 부여한 마차 적재 구역

        public bool IsSecured => isSecured; // 외부 원정 손실 시스템용 확보 여부 공개
        public WagonCargoArea SecuredArea => securedArea; // 현재 확보 마차 적재 구역 공개

        public void SetSecured(WagonCargoArea area, bool secured) // 마차 적재 상태 갱신
        {
            isSecured = secured && area != null; // 유효 적재 구역이 있을 때만 확보 상태 활성화
            securedArea = isSecured ? area : null; // 확보 해제 시 마차 참조도 제거
        }
    }
}
