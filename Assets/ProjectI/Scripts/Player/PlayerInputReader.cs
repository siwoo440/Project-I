using UnityEngine; // 유니티 기본 기능 참조
using UnityEngine.InputSystem; // 새 Input System 기능 참조

namespace ProjectI.Player // 플레이어 기능 네임스페이스
{
    public sealed class PlayerInputReader : MonoBehaviour // 플레이어 입력 래퍼
    {
        [SerializeField] private InputActionAsset inputActions; // 프로젝트 입력 액션 에셋
        private InputActionMap playerActionMap; // 플레이어 액션 맵
        private InputAction moveAction; // 이동 액션
        private InputAction lookAction; // 시점 액션
        private InputAction sprintAction; // 달리기 액션
        private InputAction jumpAction; // 점프 액션
        private InputAction crouchAction; // 웅크리기 액션
        private InputAction interactAction; // 상호작용 액션 기반

        public Vector2 Move => moveAction == null ? Vector2.zero : moveAction.ReadValue<Vector2>(); // 현재 이동 입력 반환
        public Vector2 Look => lookAction == null ? Vector2.zero : lookAction.ReadValue<Vector2>(); // 현재 시점 입력 반환
        public bool SprintHeld => sprintAction != null && sprintAction.IsPressed(); // 달리기 입력 유지 여부 반환
        public bool JumpPressed => jumpAction != null && jumpAction.WasPressedThisFrame(); // 점프 입력 시작 여부 반환
        public bool CrouchHeld => (crouchAction != null && crouchAction.IsPressed()) || (Keyboard.current != null && Keyboard.current.leftCtrlKey.isPressed); // Crouch 액션 또는 왼쪽 Ctrl 웅크리기 여부 반환
        public bool InteractPressed => interactAction != null && interactAction.WasPressedThisFrame(); // 상호작용 입력 시작 여부 반환

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
        }

        private void BindActions() // 플레이어 입력 액션 연결
        {
            if (inputActions == null) // 입력 에셋 누락 확인
            {
                Debug.LogError("[Project I] PlayerInputReader에 Input Action Asset이 연결되지 않았습니다.", this); // 입력 에셋 누락 오류 출력
                return; // 입력 연결 중단
            }

            playerActionMap = inputActions.FindActionMap("Player", true); // Player 액션 맵 조회
            moveAction = playerActionMap.FindAction("Move", true); // Move 액션 조회
            lookAction = playerActionMap.FindAction("Look", true); // Look 액션 조회
            sprintAction = playerActionMap.FindAction("Sprint", true); // Sprint 액션 조회
            jumpAction = playerActionMap.FindAction("Jump", false); // Jump 액션 조회
            crouchAction = playerActionMap.FindAction("Crouch", false); // Crouch 액션 조회
            interactAction = playerActionMap.FindAction("Interact", false); // Interact 액션 기반 조회
        }
    }
}
