using UnityEngine; // 유니티 기본 기능 참조

namespace ProjectI.World // 월드 기능 네임스페이스
{
    public sealed class MovingPlatformPassengerTrigger : MonoBehaviour // 이동 플랫폼 탑승자 추적 트리거
    {
        [SerializeField] private Transform platformRoot; // 실제 플랫폼 루트 트랜스폼

        public void Configure(Transform root) // 에디터 자동 설정용 플랫폼 루트 지정
        {
            platformRoot = root; // 플랫폼 루트 저장
        }

        private void OnTriggerEnter(Collider other) // 트리거 진입 처리
        {
            CharacterController controller = other.GetComponent<CharacterController>(); // 진입 대상의 캐릭터 컨트롤러 조회

            if (controller == null || platformRoot == null) // 플레이어 또는 플랫폼 참조 누락 확인
            {
                return; // 탑승 처리 중단
            }

            controller.transform.SetParent(platformRoot, true); // 월드 위치를 유지한 채 플랫폼 자식으로 연결
        }

        private void OnTriggerExit(Collider other) // 트리거 이탈 처리
        {
            CharacterController controller = other.GetComponent<CharacterController>(); // 이탈 대상의 캐릭터 컨트롤러 조회

            if (controller == null || platformRoot == null) // 플레이어 또는 플랫폼 참조 누락 확인
            {
                return; // 이탈 처리 중단
            }

            if (controller.transform.parent == platformRoot) // 현재 플랫폼에 연결된 플레이어인지 확인
            {
                controller.transform.SetParent(null, true); // 월드 위치를 유지한 채 플랫폼 연결 해제
            }
        }
    }
}
