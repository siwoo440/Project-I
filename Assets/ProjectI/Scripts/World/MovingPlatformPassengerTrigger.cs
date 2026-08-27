using System.Collections.Generic; // 현재 트리거 안 탑승자 추적 기능 참조
using UnityEngine; // 유니티 기본 기능 참조

namespace ProjectI.World // 월드 기능 네임스페이스
{
    public sealed class MovingPlatformPassengerTrigger : MonoBehaviour // 이동 플랫폼 탑승자 등록 트리거
    {
        [SerializeField] private MovingPlatform platform; // 실제 이동 플랫폼 기능 참조
        private readonly HashSet<CharacterController> trackedPassengers = new HashSet<CharacterController>(); // 현재 트리거 내부 CharacterController 목록

        public MovingPlatform Platform => platform; // Validator와 디버그용 플랫폼 참조 공개

        private void Awake() // 탑승 감지 초기화
        {
            if (platform == null) // 플랫폼 참조 누락 확인
            {
                platform = GetComponentInParent<MovingPlatform>(); // 부모에서 이동 플랫폼 자동 조회
            }
        }

        public void Configure(MovingPlatform targetPlatform) // 에디터 자동 설정용 플랫폼 지정
        {
            platform = targetPlatform; // 실제 이동 플랫폼 기능 저장
        }

        private void OnTriggerEnter(Collider other) // 트리거 진입 처리
        {
            Register(other); // CharacterController 탑승 등록 시도
        }

        private void OnTriggerStay(Collider other) // 트리거 내부 유지 처리
        {
            Register(other); // Enter 이벤트가 누락되어도 탑승 상태 보정
        }

        private void OnTriggerExit(Collider other) // 트리거 이탈 처리
        {
            CharacterController controller = other.GetComponent<CharacterController>(); // 이탈 대상 CharacterController 조회

            if (controller == null || platform == null) // 플레이어 또는 플랫폼 참조 누락 확인
            {
                return; // 이탈 처리 중단
            }

            trackedPassengers.Remove(controller); // 로컬 탑승자 목록에서 제거
            platform.UnregisterPassenger(controller); // 이동 플랫폼 탑승자 목록에서 제거
        }

        private void OnDisable() // 트리거 비활성화 시 탑승자 정리
        {
            if (platform != null) // 플랫폼 참조 존재 확인
            {
                foreach (CharacterController controller in trackedPassengers) // 등록된 탑승자 순회
                {
                    platform.UnregisterPassenger(controller); // 이동 플랫폼에서 탑승자 해제
                }
            }

            trackedPassengers.Clear(); // 로컬 탑승자 목록 비우기
        }

        private void Register(Collider other) // 충돌체에서 CharacterController를 찾아 탑승 등록
        {
            CharacterController controller = other.GetComponent<CharacterController>(); // 진입 대상 CharacterController 조회

            if (controller == null || platform == null) // 플레이어 또는 플랫폼 참조 누락 확인
            {
                return; // 탑승 등록 중단
            }

            if (trackedPassengers.Add(controller)) // 새 탑승자인지 확인
            {
                platform.RegisterPassenger(controller); // 이동 플랫폼에 CharacterController 등록
            }
        }
    }
}
