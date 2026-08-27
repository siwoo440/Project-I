using UnityEngine; // 유니티 기본 기능 참조
using UnityEngine.InputSystem; // 새 Input System 기능 참조

namespace ProjectI.Player // 플레이어 입력 기능 네임스페이스
{
    public sealed class PlayerInputReader : MonoBehaviour // 모든 게임플레이 입력을 InputAction으로 읽는 입력 래퍼
    {
        [SerializeField] private InputActionAsset inputActions; // 프로젝트 입력 액션 에셋
        private InputActionMap playerActionMap; // 플레이어 액션 맵
        private InputAction moveAction; // 이동 액션
        private InputAction lookAction; // 시점 액션
        private InputAction sprintAction; // 달리기 액션
        private InputAction jumpAction; // 점프 액션
        private InputAction crouchAction; // 웅크리기 액션
        private InputAction interactAction; // 상호작용 액션
        private InputAction useAction; // 선택 아이템 사용 액션
        private InputAction dropAction; // 선택 아이템 버리기 액션
        private InputAction slotScrollAction; // 마우스 휠 슬롯 전환 액션
        private InputAction pauseAction; // 커서 잠금·일시정지 액션
        private readonly InputAction[] quickSlotActions = new InputAction[6]; // 빠른 슬롯 1~6 직접 선택 액션

        public Vector2 Move => moveAction == null ? Vector2.zero : moveAction.ReadValue<Vector2>(); // 현재 이동 입력 반환
        public Vector2 Look => lookAction == null ? Vector2.zero : lookAction.ReadValue<Vector2>(); // 현재 시점 입력 반환
        public bool SprintHeld => sprintAction != null && sprintAction.IsPressed(); // 달리기 입력 유지 여부 반환
        public bool JumpPressed => jumpAction != null && jumpAction.WasPressedThisFrame(); // 점프 입력 시작 여부 반환
        public bool CrouchHeld => crouchAction != null && crouchAction.IsPressed(); // 웅크리기 입력 유지 여부 반환
        public bool InteractPressed => interactAction != null && interactAction.WasPressedThisFrame(); // 상호작용 입력 시작 여부 반환
        public bool InteractHeld => interactAction != null && interactAction.IsPressed(); // 상호작용 입력 유지 여부 반환
        public bool InteractReleased => interactAction != null && interactAction.WasReleasedThisFrame(); // 상호작용 입력 해제 여부 반환
        public bool DropPressed => dropAction != null && dropAction.WasPressedThisFrame(); // 선택 아이템 버리기 입력 반환
        public bool UsePressed => useAction != null && useAction.WasPressedThisFrame(); // 선택 아이템 사용 입력 반환
        public bool ThrowPressed => UsePressed; // 기존 5일차 코드 호환용 사용 입력 반환
        public int DirectSlotPressed => ReadDirectSlotPressed(); // 빠른 슬롯 직접 선택값 반환
        public float SlotScrollDelta => slotScrollAction == null ? 0f : slotScrollAction.ReadValue<float>(); // 슬롯 휠 전환값 반환
        public bool PausePressed => pauseAction != null && pauseAction.WasPressedThisFrame(); // 커서 잠금·일시정지 입력 반환
        public InputActionAsset InputActions => inputActions; // 설정 화면 재바인딩용 입력 에셋 공개

        private void OnEnable() // 컴포넌트 활성화 처리
        {
            BindActions(); // 입력 액션 참조 연결
            playerActionMap?.Enable(); // 플레이어 액션 맵 활성화
        }

        private void OnDisable() // 컴포넌트 비활성화 처리
        {
            playerActionMap?.Disable(); // 플레이어 액션 맵 비활성화
        }

        public void Configure(InputActionAsset actionAsset) // 에디터 자동 설정용 입력 에셋 지정
        {
            inputActions = actionAsset; // 입력 액션 에셋 저장
            BindActions(); // 새 입력 에셋 기준으로 액션 참조 다시 연결
        }

        private int ReadDirectSlotPressed() // Input Action 기반 빠른 슬롯 1~6 입력 확인
        {
            for (int index = 0; index < quickSlotActions.Length; index++) // 슬롯 액션 전체 순회
            {
                InputAction action = quickSlotActions[index]; // 현재 슬롯 액션 조회

                if (action != null && action.WasPressedThisFrame()) // 현재 슬롯 액션 입력 여부 확인
                {
                    return index; // 눌린 슬롯 인덱스 반환
                }
            }

            return -1; // 슬롯 직접 선택 입력 없음 반환
        }

        private void BindActions() // 플레이어 Input Action 참조 연결
        {
            if (inputActions == null) // 입력 에셋 누락 확인
            {
                Debug.LogError("[Project I] PlayerInputReader에 Input Action Asset이 연결되지 않았습니다.", this); // 입력 에셋 누락 오류 출력
                return; // 입력 연결 중단
            }

            playerActionMap = inputActions.FindActionMap(GameplayInputActions.Map, true); // Player 액션 맵 조회
            moveAction = playerActionMap.FindAction(GameplayInputActions.Move, true); // Move 액션 조회
            lookAction = playerActionMap.FindAction(GameplayInputActions.Look, true); // Look 액션 조회
            sprintAction = playerActionMap.FindAction(GameplayInputActions.Sprint, true); // Sprint 액션 조회
            jumpAction = playerActionMap.FindAction(GameplayInputActions.Jump, false); // Jump 액션 조회
            crouchAction = playerActionMap.FindAction(GameplayInputActions.Crouch, false); // Crouch 액션 조회
            interactAction = playerActionMap.FindAction(GameplayInputActions.Interact, false); // Interact 액션 조회
            useAction = playerActionMap.FindAction(GameplayInputActions.Use, false); // Use 액션 조회
            dropAction = playerActionMap.FindAction(GameplayInputActions.Drop, false); // Drop 액션 조회
            slotScrollAction = playerActionMap.FindAction(GameplayInputActions.SlotScroll, false); // SlotScroll 액션 조회
            pauseAction = playerActionMap.FindAction(GameplayInputActions.Pause, false); // Pause 액션 조회

            for (int index = 0; index < quickSlotActions.Length; index++) // 슬롯 1~6 액션 순회
            {
                quickSlotActions[index] = playerActionMap.FindAction(GameplayInputActions.QuickSlot(index), false); // 각 슬롯 선택 액션 연결
            }
        }
    }
}
