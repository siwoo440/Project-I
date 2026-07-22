using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectI
{
    /// <summary>
    /// 카메라 전방으로 레이캐스트해 E 일반 상호작용과 F 길게 누르기 상호작용을 감지. (기획서 PART 4.7)
    /// </summary>
    public class PlayerInteractor : MonoBehaviour
    {
        [Tooltip("플레이어가 조준하여 상호작용할 수 있는 최대 거리(m)")] [SerializeField] float interactDistance = 3f;
        [Tooltip("카메라 (없으면 자동)")] [SerializeField] Transform rayOrigin;   // 카메라 (없으면 자동)

        [Header("디버그")] // 임시 OnGUI 설정 구분
        [Tooltip("기존 OnGUI 표시 여부")] [SerializeField] bool showDebug = true; // 기존 OnGUI 표시 여부

        public InventorySystem Inventory { get; private set; } // 현재 플레이어 인벤토리 반환
        public string CurrentPrompt // HUD에 현재 상호작용 문구 전달
        {
            get
            {
                if (currentHold != null) // 길게 누르기 대상 존재 여부 확인
                {
                    return currentHold.GetHoldPrompt(HoldProgress); // 진행률 포함 안내 반환
                }

                return current != null ? current.GetPrompt() : string.Empty; // 일반 상호작용 안내 반환
            }
        }

        public float HoldProgress => currentHold != null // 현재 길게 누르기 진행률 반환
            ? Mathf.Clamp01(holdElapsed / Mathf.Max(0.01f, currentHold.HoldDuration)) // 안전한 진행률 계산
            : 0f; // 대상 없음 진행률

        public event System.Action Interacted; // 일반 또는 길게 누르기 상호작용 완료 전달
        IInteractable current; // 현재 일반 상호작용 대상
        IHoldInteractable currentHold; // 현재 길게 누르기 상호작용 대상
        float holdElapsed; // 현재 F 입력 유지시간
        bool holdCompleted; // 같은 입력의 중복 완료 방지
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
            if (kb == null) // 키보드 존재 여부 확인
            {
                ResetHoldInteraction(); // 입력 유지 상태 초기화
                return; // 입력 처리 중단
            }

            HandleHoldInteraction(kb); // F 길게 누르기 상호작용 처리

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

        void HandleHoldInteraction(Keyboard keyboard) // F 길게 누르기 입력 처리
        {
            if (currentHold == null) // 길게 누르기 대상 존재 여부 확인
            {
                ResetHoldInteraction(); // 남은 진행 상태 초기화
                return; // 처리 중단
            }

            if (!keyboard.fKey.isPressed) // F키 유지 여부 확인
            {
                ResetHoldInteraction(); // 입력 취소 시 진행 초기화
                return; // 처리 중단
            }

            if (holdCompleted) // 이미 완료된 입력인지 확인
            {
                return; // 중복 완료 방지
            }

            holdElapsed += Time.deltaTime; // 현재 프레임 시간 누적

            if (holdElapsed < Mathf.Max(0.01f, currentHold.HoldDuration)) // 필요한 유지시간 도달 여부 확인
            {
                return; // 입력 유지 계속
            }

            holdCompleted = true; // 완료 상태 우선 저장
            currentHold.CompleteHold(this); // 대상의 길게 누르기 완료 처리
            Interacted?.Invoke(); // 상호작용 완료 이벤트 전달
        }

        void ResetHoldInteraction() // 길게 누르기 진행 상태 초기화
        {
            holdElapsed = 0f; // 누적시간 초기화
            holdCompleted = false; // 완료 상태 초기화
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
            IInteractable detected = null; // 새 일반 상호작용 대상
            IHoldInteractable detectedHold = null; // 새 길게 누르기 대상

            if (rayOrigin != null && Physics.Raycast(rayOrigin.position, rayOrigin.forward, out RaycastHit hit, interactDistance)) // 전방 대상 감지
            {
                detected = hit.collider.GetComponentInParent<IInteractable>(); // 일반 상호작용 컴포넌트 검색
                detectedHold = hit.collider.GetComponentInParent<IHoldInteractable>(); // 길게 누르기 컴포넌트 검색
            }

            if (!ReferenceEquals(currentHold, detectedHold)) // 길게 누르기 대상 변경 여부 확인
            {
                ResetHoldInteraction(); // 대상 변경 시 진행 초기화
            }

            current = detected; // 현재 일반 상호작용 대상 갱신
            currentHold = detectedHold; // 현재 길게 누르기 대상 갱신
        }

        void OnGUI() // 기존 임시 조준점과 상호작용 문구 표시
        {
            if (!showDebug || !DebugUIToggleController.PlayerInfoVisible) // Inspector 설정과 F1 표시 상태 확인
            {
                return; // 기존 조준점과 상호작용 문구 표시 중단
            }

            GUI.Label(new Rect(Screen.width / 2f - 4f, Screen.height / 2f - 10f, 20f, 20f), "+"); // 임시 조준점 표시

            if (current != null || currentHold != null) // 상호작용 대상 존재 여부 확인
            {
                GUI.Label(new Rect(Screen.width / 2f - 120f, Screen.height / 2f + 12f, 240f, 20f), CurrentPrompt); // 임시 상호작용 문구 표시
            }
        }
    }
}