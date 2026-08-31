using UnityEngine; // 유니티 기본 기능 참조

namespace ProjectI.Power // 전력 시스템 네임스페이스
{
    public sealed class RoomPowerZone : MonoBehaviour // 방 하나의 요청 전력과 실제 전력 공급 관리
    {
        [SerializeField] private string displayName = "ROOM"; // 배전반과 디버그에 표시할 방 이름
        [SerializeField] private bool requestedPower = true; // 배전반에서 요청한 방 전원 상태
        [SerializeField] private bool facilityPowerAvailable; // 발전기와 메인 배전반에서 공급 가능한 전력 상태
        [SerializeField] private PowerConsumer[] consumers; // 이 방에 연결된 모든 공통 전력 소비자

        public event System.Action StateChanged; // 방 요청·실제 전력 상태 변경 이벤트 공개
        public string DisplayName => displayName; // 방 표시 이름 공개
        public bool RequestedPower => requestedPower; // 배전반 스위치 요청 상태 공개
        public bool FacilityPowerAvailable => facilityPowerAvailable; // 상위 전력 공급 가능 상태 공개
        public bool ActualPower => requestedPower && facilityPowerAvailable; // 방의 최종 실제 통전 상태 공개
        public int ConsumerCount => consumers == null ? 0 : consumers.Length; // 연결된 전력 소비자 개수 공개
        public PowerConsumer[] Consumers => consumers; // Validator용 소비자 목록 공개

        private void Awake() // 방 전력 구역 초기화
        {
            ResolveConsumers(); // 자식 전력 소비자 목록 확보
            ApplyPower(); // 저장 상태를 실제 장치에 적용
        }

        private void OnEnable() // 방 전력 구역 활성화 처리
        {
            ResolveConsumers(); // 활성화 직후 소비자 목록 확보
            ApplyPower(); // 현재 전력 상태 재적용
        }

        public void Configure(string targetDisplayName, bool startRequestedPower, PowerConsumer[] targetConsumers) // 자동 Setup용 방 전력 구역 설정
        {
            displayName = string.IsNullOrWhiteSpace(targetDisplayName) ? gameObject.name : targetDisplayName; // 방 표시 이름 저장
            requestedPower = startRequestedPower; // 시작 방 전원 요청 상태 저장
            consumers = targetConsumers; // 방에 연결된 소비자 목록 저장
            ResolveConsumers(); // 누락 소비자 자동 조회
            ApplyPower(); // 새 설정을 실제 장치에 즉시 적용
        }

        public void SetRequestedPower(bool powered) // 배전반 스위치에서 방 전원 요청 변경
        {
            if (requestedPower == powered) // 기존 요청 상태와 동일한지 확인
            {
                return; // 중복 장치 갱신과 이벤트 호출 방지
            }

            requestedPower = powered; // 방별 요청 상태 저장
            ApplyPower(); // 상위 전력 상태와 합산하여 실제 장치 갱신
            NotifyStateChanged(); // 배전반 상태등과 토글 레버 갱신 요청
        }

        public void SetFacilityPowerAvailable(bool available) // 메인 배전반에서 상위 전력 공급 상태 전달
        {
            if (facilityPowerAvailable == available) // 기존 상위 공급 상태와 동일한지 확인
            {
                return; // 불필요한 전체 소비자 갱신 방지
            }

            facilityPowerAvailable = available; // 상위 전력 상태 저장
            ApplyPower(); // 방별 요청 상태와 합산하여 실제 장치 갱신
            NotifyStateChanged(); // 배전반 방 상태등 갱신 요청
        }

        private void ResolveConsumers() // 자식 구조에서 전력 소비자 목록 확보
        {
            if (consumers == null || consumers.Length == 0) // 직렬화 소비자 목록 누락 여부 확인
            {
                consumers = GetComponentsInChildren<PowerConsumer>(true); // 현재 방 아래 모든 소비자 자동 조회
            }
        }

        private void ApplyPower() // 방의 최종 실제 전력 상태를 소비자에 적용
        {
            ResolveConsumers(); // 제어할 소비자 목록 확보
            bool actualPower = ActualPower; // 현재 방의 최종 통전 상태 계산

            foreach (PowerConsumer consumer in consumers) // 방의 모든 소비자 순회
            {
                if (consumer == null) // 유효 소비자 여부 확인
                {
                    continue; // 누락 소비자 건너뜀
                }

                consumer.SetPowerAvailable(actualPower); // 전등과 문 같은 장치에 실제 전력 전달
            }
        }

        private void NotifyStateChanged() // 방 전력 상태 변경 알림 실행
        {
            StateChanged?.Invoke(); // 배전반과 토글 스위치에 상태 변경 전달
        }

        private void OnValidate() // 인스펙터 변경 시 상태 동기화
        {
            ResolveConsumers(); // 에디터에서도 소비자 목록 확보
            ApplyPower(); // 현재 방 전력 상태를 장치에 반영
        }
    }
}
