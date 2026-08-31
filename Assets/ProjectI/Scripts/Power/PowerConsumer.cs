using UnityEngine; // 유니티 기본 기능 참조

namespace ProjectI.Power // 전력 시스템 네임스페이스
{
    public sealed class PowerConsumer : MonoBehaviour // 방 전력을 실제 장치 상태로 전달하는 공통 소비자
    {
        [SerializeField] private string displayName = "전력 장치"; // 전력 장치 표시 이름
        [SerializeField] private bool hasPower; // 현재 실제 전력 공급 상태

        public string DisplayName => displayName; // 전력 장치 표시 이름 공개
        public bool HasPower => hasPower; // 현재 실제 전력 공급 상태 공개

        private void OnEnable() // 전력 소비자 활성화 처리
        {
            NotifyReceivers(); // 저장된 전력 상태를 장치에 재전달
        }

        public void Configure(string targetDisplayName, bool startPowered) // 자동 Setup용 전력 소비자 설정
        {
            displayName = string.IsNullOrWhiteSpace(targetDisplayName) ? gameObject.name : targetDisplayName; // 표시 이름 저장
            hasPower = startPowered; // 시작 전력 상태 저장
            NotifyReceivers(); // 새 전력 상태를 장치에 즉시 전달
        }

        public void SetPowerAvailable(bool powered) // 방 전력에서 실제 전력 상태 전달
        {
            if (hasPower == powered) // 기존 상태와 동일한지 확인
            {
                return; // 불필요한 반복 전달 방지
            }

            hasPower = powered; // 새 실제 전력 상태 저장
            NotifyReceivers(); // 연결 장치에 전력 상태 변경 전달
        }

        private void NotifyReceivers() // 같은 장치 구조의 전력 수신 컴포넌트 갱신
        {
            MonoBehaviour[] behaviours = GetComponentsInChildren<MonoBehaviour>(true); // 현재 소비자 아래 모든 MonoBehaviour 조회

            foreach (MonoBehaviour behaviour in behaviours) // 연결 컴포넌트 전체 순회
            {
                if (!(behaviour is IPowerStateReceiver receiver)) // 전력 상태 수신 기능 구현 여부 확인
                {
                    continue; // 전력 장치가 아니면 건너뜀
                }

                receiver.OnPowerStateChanged(hasPower); // 현재 실제 전력 상태 전달
            }
        }

        private void OnValidate() // 인스펙터 변경 시 상태 동기화
        {
            NotifyReceivers(); // 에디터에서도 현재 전력 상태를 장치에 적용
        }
    }
}
