using UnityEngine; // Unity 기본 기능 사용

namespace ProjectI // 프로젝트 공통 네임스페이스
{
    public class CrackedWall : MonoBehaviour, IInteractable // 폭탄을 소모해 파괴하는 금간 벽
    {
        [Header("파괴 설정")] // Inspector 파괴 설정 구분
        [Tooltip("파괴할 벽 외형과 Collider가 포함된 오브젝트")] [SerializeField] GameObject wallVisual; // 파괴할 벽 외형과 Collider가 포함된 오브젝트
        [Tooltip("벽 파괴에 필요한 아이템 이름")] [SerializeField] string requiredItemName = "폭탄"; // 벽 파괴에 필요한 아이템 이름

        bool isDestroyed; // 벽이 이미 파괴되었는지 저장

        public string GetPrompt() // 벽 상태에 맞는 상호작용 안내 반환
        {
            if (isDestroyed) { return "파괴된 벽";  } // 벽이 이미 파괴되었는지 확인// 파괴 완료 안내 반환
            return $"[E] 파괴 ({requiredItemName} 필요)"; // 필요 아이템이 포함된 안내 반환
        }

        public void Interact(PlayerInteractor interactor) // 플레이어가 금간 벽과 상호작용
        {
            if (isDestroyed) { return; } // 이미 파괴된 벽인지 확인 ->  중복 파괴 방지

            if (interactor == null || interactor.Inventory == null) { return; }// 플레이어 인벤토리를 사용할 수 있는지 확인 // 벽 파괴 처리 중단
         
            if (!interactor.Inventory.ConsumeItemByName(requiredItemName)) // 인벤토리에서 폭탄 소모 시도
            {
                Debug.Log($"[CrackedWall] {requiredItemName}이 없어 벽을 파괴할 수 없습니다."); // 필요 아이템 없음 출력
                return; // 벽 파괴 중단
            }

            isDestroyed = true; // 벽을 파괴 상태로 변경
            Debug.Log($"[CrackedWall] {requiredItemName}을 사용해 벽을 파괴했습니다."); // 벽 파괴 결과 출력

            if (wallVisual != null) { wallVisual.SetActive(false); } 
            // 파괴할 벽 외형이 연결되어 있는지 확인// 벽 외형과 Collider 비활성화
            else  { gameObject.SetActive(false); }// 별도 벽 외형이 없는 경우 // 현재 금간 벽 오브젝트 전체 비활성화
        }
    }
}