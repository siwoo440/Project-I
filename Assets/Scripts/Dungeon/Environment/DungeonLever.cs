using UnityEngine; // Unity 기본 기능 사용

namespace ProjectI // 프로젝트 공통 네임스페이스
{
    public class DungeonLever : MonoBehaviour, IInteractable // 연결된 문을 작동시키는 레버
    {
        [Header("연결 설정")] // Inspector 연결 설정 구분
        [Tooltip("레버가 작동시킬 문")] [SerializeField] DungeonDoor targetDoor; // 레버가 작동시킬 문
        [Tooltip("회전시킬 레버 손잡이")] [SerializeField] Transform leverHandle; // 회전시킬 레버 손잡이

        [Header("레버 설정")] // Inspector 레버 설정 구분
        [Tooltip("사용 전 손잡이 로컬 회전")] [SerializeField] Vector3 offRotation = new Vector3(0f, 0f, -30f); // 사용 전 손잡이 로컬 회전
        [Tooltip("사용 후 손잡이 로컬 회전")] [SerializeField] Vector3 onRotation = new Vector3(0f, 0f, 30f); // 사용 후 손잡이 로컬 회전
        [Tooltip("한 번만 사용할 수 있는지 결정")] [SerializeField] bool oneUse = true; // 한 번만 사용할 수 있는지 결정

        bool isOn; // 현재 레버 작동 상태 저장
        bool hasBeenUsed; // 일회용 레버 사용 여부 저장

        void Awake() // 레버 손잡이 초기 상태 설정
        {
            if (leverHandle != null) // 손잡이가 연결되어 있는지 확인
            {
                leverHandle.localRotation = Quaternion.Euler(offRotation); // 손잡이를 사용 전 방향으로 회전
            }
        }

        public string GetPrompt() // 현재 레버 상태에 맞는 안내 반환
        {
            if (oneUse && hasBeenUsed) // 사용 완료된 일회용 레버인지 확인
            {
                return "이미 작동한 레버"; // 재사용 불가 안내 반환
            }

            return "[E] 레버 당기기"; // 레버 상호작용 안내 반환
        }

        public void Interact(PlayerInteractor interactor) // 플레이어가 레버를 작동
        {
            if (oneUse && hasBeenUsed) // 이미 사용한 일회용 레버인지 확인
            {
                return; // 중복 작동 방지
            }

            if (targetDoor == null) // 작동시킬 문이 연결되지 않았는지 확인
            {
                Debug.LogError("[Lever] Target Door가 연결되지 않았습니다."); // 참조 누락 오류 출력
                return; // 레버 작동 중단
            }

            isOn = !isOn; // 현재 레버 상태 반전

            if (leverHandle != null) // 레버 손잡이가 연결되어 있는지 확인
            {
                Vector3 targetRotation = isOn ? onRotation : offRotation; // 현재 상태에 맞는 회전값 선택
                leverHandle.localRotation = Quaternion.Euler(targetRotation); // 레버 손잡이 회전 적용
            }

            targetDoor.ToggleFromMechanism(); // 연결된 문 상태 전환
            hasBeenUsed = true; // 레버 사용 완료 상태 저장

            Debug.Log("[Lever] 연결된 문을 작동했습니다."); // 레버 작동 결과 출력
        }
    }
}