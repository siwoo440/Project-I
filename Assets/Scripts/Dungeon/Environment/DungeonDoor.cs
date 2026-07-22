using System.Collections; // 문 이동 애니메이션 코루틴 사용
using UnityEngine; // Unity 기본 기능 사용

namespace ProjectI // 프로젝트 공통 네임스페이스
{
    public class DungeonDoor : MonoBehaviour, IInteractable // 직접 또는 레버로 작동하는 던전 문
    {
        [Header("문 설정")] // Inspector 문 설정 구분
        [Tooltip("실제로 움직일 문 패널")] [SerializeField] Transform doorPanel; // 실제로 움직일 문 패널
        [Tooltip("닫힌 위치에서 열린 위치까지의 이동량")] [SerializeField] Vector3 openLocalOffset = new Vector3(0f, 3.2f, 0f); // 닫힌 위치에서 열린 위치까지의 이동량
        [Tooltip("문이 완전히 이동하는 데 걸리는 시간")] [SerializeField] float moveDuration = 0.75f; // 문이 완전히 이동하는 데 걸리는 시간
        [Tooltip("플레이어가 문을 직접 작동할 수 있는지 결정")] [SerializeField] bool allowDirectInteraction = true; // 플레이어가 문을 직접 작동할 수 있는지 결정

        [Header("잠금 설정")] // Inspector 잠금 설정 구분
        [Tooltip("문을 처음 열 때 아이템이 필요한지 결정")] [SerializeField] bool requiresItem = false; // 문을 처음 열 때 아이템이 필요한지 결정
        [Tooltip("잠금 해제에 사용할 아이템 이름")] [SerializeField] string requiredItemName = "열쇠"; // 잠금 해제에 사용할 아이템 이름

        bool isOpen; // 현재 문이 열려 있는지 저장
        bool isMoving; // 현재 문이 이동 중인지 저장
        bool isUnlocked; // 잠긴 문이 해제되었는지 저장
        Vector3 closedLocalPosition; // 문 패널의 닫힌 로컬 위치

        public bool IsOpen => isOpen; // 외부에서 문의 열림 상태 확인

        void Awake() // 문 패널과 초기 상태 설정
        {
            if (doorPanel == null) // 문 패널이 Inspector에 연결되지 않았는지 확인
            {
                doorPanel = transform; // 현재 오브젝트 자체를 문 패널로 사용
            }

            closedLocalPosition = doorPanel.localPosition; // 시작 위치를 문의 닫힌 위치로 저장
            isUnlocked = !requiresItem; // 아이템이 필요하지 않은 문은 처음부터 잠금 해제
        }

        public string GetPrompt() // 현재 문 상태에 맞는 상호작용 안내 반환
        {
            if (isMoving) // 문이 이동 중인지 확인
            {
                return "문 작동 중"; // 이동 중 안내 반환
            }

            if (!allowDirectInteraction) // 직접 상호작용할 수 없는 문인지 확인
            {
                return "레버로 작동하는 문"; // 레버 사용 안내 반환
            }

            if (!isUnlocked) // 문이 잠겨 있는지 확인
            {
                return $"[E] 열기 ({requiredItemName} 필요)"; // 필요 아이템 안내 반환
            }

            return isOpen ? "[E] 문 닫기" : "[E] 문 열기"; // 현재 상태에 따른 열기 또는 닫기 안내 반환
        }

        public void Interact(PlayerInteractor interactor) // 플레이어가 E키로 문과 상호작용
        {
            if (!allowDirectInteraction || isMoving) // 직접 상호작용 불가 또는 이동 중인지 확인
            {
                return; // 문 상호작용 중단
            }

            if (!isUnlocked) // 문이 잠겨 있는지 확인
            {
                if (interactor == null || interactor.Inventory == null) // 플레이어 인벤토리를 찾을 수 있는지 확인
                {
                    return; // 잠금 해제 처리 중단
                }

                if (!interactor.Inventory.ConsumeItemByName(requiredItemName)) // 인벤토리에서 필요 아이템 소모 시도
                {
                    Debug.Log($"[Door] {requiredItemName}이 없어 문을 열 수 없습니다."); // 필요 아이템이 없음을 출력
                    return; // 잠금 상태 유지
                }

                isUnlocked = true; // 아이템 소모 후 문 잠금 해제
                Debug.Log($"[Door] {requiredItemName}을 사용해 문을 열었습니다."); // 잠금 해제 결과 출력
            }

            ToggleDoor(); // 문의 열림 상태 전환
        }

        public void ToggleFromMechanism() // 레버 같은 외부 장치에서 문 상태 전환
        {
            if (isMoving) // 문이 이동 중인지 확인
            {
                return; // 중복 문 이동 방지
            }

            ToggleDoor(); // 잠금 여부와 관계없이 장치로 문 작동
        }

        void ToggleDoor() // 현재 상태의 반대로 문 이동 시작
        {
            StartCoroutine(MoveDoor(!isOpen)); // 현재 상태 반대 방향으로 문 이동
        }

        IEnumerator MoveDoor(bool open) // 지정한 상태로 문 패널 이동
        {
            isMoving = true; // 문을 이동 중 상태로 변경

            Vector3 startPosition = doorPanel.localPosition; // 현재 문 패널 위치 저장
            Vector3 targetPosition = open ? closedLocalPosition + openLocalOffset : closedLocalPosition; // 열기 또는 닫기 목표 위치 계산
            float safeDuration = Mathf.Max(0.01f, moveDuration); // 이동시간이 0이 되지 않도록 제한
            float elapsed = 0f; // 현재까지 지난 이동시간 초기화

            while (elapsed < safeDuration) // 이동시간이 끝날 때까지 반복
            {
                elapsed += Time.deltaTime; // 프레임 시간을 이동시간에 누적
                float progress = Mathf.Clamp01(elapsed / safeDuration); // 이동 진행률을 0에서 1로 계산
                doorPanel.localPosition = Vector3.Lerp(startPosition, targetPosition, progress); // 진행률에 따라 문 위치 보간
                yield return null; // 다음 프레임까지 대기
            }

            doorPanel.localPosition = targetPosition; // 문을 정확한 최종 위치에 배치
            isOpen = open; // 최종 문 상태 저장
            isMoving = false; // 문 이동 상태 종료
        }
    }
}