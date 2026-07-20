using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectI
{
    /// <summary>
    /// 카메라 전방으로 레이캐스트해 상호작용 대상 감지 + E(상호작용) / Q(선택 아이템 버리기). (기획서 PART 4.7)
    /// </summary>
    public class PlayerInteractor : MonoBehaviour
    {
        [SerializeField] float interactDistance = 3f;
        [SerializeField] Transform rayOrigin;   // 카메라 (없으면 자동)

        [Header("디버그")] // 임시 OnGUI 설정 구분
        [SerializeField] bool showDebug = true; // 기존 OnGUI 표시 여부

        public InventorySystem Inventory { get; private set; } // 현재 플레이어 인벤토리 반환
        public string CurrentPrompt => current != null ? current.GetPrompt() : string.Empty; // HUD에 현재 상호작용 문구 전달

        public event System.Action Interacted; // E 상호작용이 실행된 사실 전달
        IInteractable current;
        PlayerController playerController; // 회복 아이템 효과를 받을 플레이어
        void Awake()
        {
            Inventory = GetComponent<InventorySystem>();
            playerController = GetComponent<PlayerController>(); // 같은 오브젝트의 PlayerController 가져오기

            if (rayOrigin == null)
            {
                var cam = GetComponentInChildren<Camera>();
                if (cam != null) rayOrigin = cam.transform;
            }
        }

        void Update()
        {
            DetectTarget();

            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.eKey.wasPressedThisFrame && current != null) // E 입력과 상호작용 대상 존재 여부 확인
            {
                current.Interact(this); // 현재 대상의 상호작용 실행
                Interacted?.Invoke(); // 상호작용 효과음 재생을 위해 이벤트 전달
            }

            if (kb.qKey.wasPressedThisFrame && Inventory != null) Inventory.DropSelected();
            if (kb.rKey.wasPressedThisFrame) // 회복 아이템 사용 입력 확인
            {
                TryUseSelectedRecoveryItem(); // 현재 선택한 회복 아이템 사용 시도
            }
        }

        void TryUseSelectedRecoveryItem() // 현재 선택한 회복 아이템 사용
        {
            if (Inventory == null || playerController == null) // 인벤토리와 플레이어 참조 확인
            {
                return; // 사용 처리 중단
            }

            ICarryable selectedItem = Inventory.SelectedItem; // 현재 선택 소지품 가져오기
            MonoBehaviour itemBehaviour = selectedItem as MonoBehaviour; // 선택 소지품의 Unity 컴포넌트 가져오기

            if (itemBehaviour == null) // 선택 소지품 존재 여부 확인
            {
                Debug.Log("[PlayerInteractor] 사용할 아이템이 선택되지 않았습니다."); // 선택 아이템 없음 출력
                return; // 사용 처리 중단
            }

            RecoveryItem recoveryItem = itemBehaviour.GetComponent<RecoveryItem>(); // 선택 소지품의 회복 기능 검색

            if (recoveryItem == null) // 회복 아이템 여부 확인
            {
                Debug.Log($"[PlayerInteractor] {selectedItem.DisplayName}은 사용할 수 있는 회복 아이템이 아닙니다."); // 사용 불가 아이템 출력
                return; // 사용 처리 중단
            }

            if (recoveryItem.TryUse(playerController)) // 실제 회복 효과 적용 성공 여부 확인
            {
                Inventory.ConsumeSelected(); // 사용에 성공한 선택 아이템 소모
            }
        }


        void DetectTarget()
        {
            current = null;
            if (rayOrigin == null) return;
            if (Physics.Raycast(rayOrigin.position, rayOrigin.forward, out RaycastHit hit, interactDistance))
                current = hit.collider.GetComponentInParent<IInteractable>();
        }

        void OnGUI() // 기존 임시 조준점과 상호작용 문구 표시
        {
            if (!showDebug || !DebugUIToggleController.PlayerInfoVisible) // Inspector 설정과 F1 표시 상태 확인
            {
                return; // 기존 조준점과 상호작용 문구 표시 중단
            }

            GUI.Label(new Rect(Screen.width / 2f - 4f, Screen.height / 2f - 10f, 20f, 20f), "+"); // 임시 조준점 표시

            if (current != null) // 상호작용 대상 존재 여부 확인
            {
                GUI.Label(new Rect(Screen.width / 2f - 120f, Screen.height / 2f + 12f, 240f, 20f), current.GetPrompt()); // 임시 상호작용 문구 표시
            }
        }
    }
}
