using UnityEngine; // Unity 기본 기능 사용

namespace ProjectI // 프로젝트 공통 네임스페이스
{
    public class BuffStatue : MonoBehaviour, IInteractable // 플레이어에게 일시적인 이동속도 버프를 주는 조각상
    {
        [Header("버프 설정")] // Inspector 버프 설정 구분
        [Tooltip("적용할 이동속도 배율")] [SerializeField] float speedMultiplier = 1.25f; // 적용할 이동속도 배율
        [Tooltip("이동속도 버프 지속시간")] [SerializeField] float duration = 30f; // 이동속도 버프 지속시간
        [Tooltip("조각상을 한 번만 사용할 수 있는지 결정")] [SerializeField] bool oneUse = true; // 조각상을 한 번만 사용할 수 있는지 결정

        bool hasBeenUsed; // 일회용 조각상 사용 여부 저장

        public string GetPrompt() // 현재 조각상 상태에 맞는 안내 반환
        {
            if (oneUse && hasBeenUsed) // 이미 사용한 일회용 조각상인지 확인
            {
                return "힘을 잃은 조각상"; // 재사용 불가 안내 반환
            }

            return "[E] 조각상의 축복 받기"; // 조각상 상호작용 안내 반환
        }

        public void Interact(PlayerInteractor interactor) // 플레이어가 조각상과 상호작용
        {
            if (oneUse && hasBeenUsed) { return; } // 이미 사용한 조각상인지 확인 -> 중복 버프 적용 방지
            if (interactor == null) // 상호작용한 플레이어 정보가 없는지 확인
            {
                return; // 버프 적용 중단
            }

            PlayerController player = interactor.GetComponent<PlayerController>(); // 상호작용한 오브젝트에서 플레이어 컨트롤러 가져오기

            if (player == null) // 플레이어 컨트롤러를 찾지 못했는지 확인
            {
                return; // 버프 적용 중단
            }

            player.ApplyMovementBuff(speedMultiplier, duration); // 플레이어에게 이동속도 버프 적용
            hasBeenUsed = true; // 조각상 사용 완료 상태 저장

            Debug.Log("[BuffStatue] 플레이어에게 이동속도 축복을 적용했습니다."); // 조각상 사용 결과 출력
        }
    }
}
