using UnityEngine; // 유니티 기본 기능 참조
using UnityEngine.InputSystem; // 키보드 입력 상태 참조

namespace ProjectI.Player // 플레이어 기능 네임스페이스
{
    [RequireComponent(typeof(PlayerInputReader))] // 입력 래퍼 필수 지정
    public sealed class PlayerLook : MonoBehaviour // 1인칭 시점 처리 컴포넌트
    {
        [SerializeField] private Transform viewTransform; // 카메라 시점 트랜스폼
        [SerializeField] private float horizontalSensitivity = 0.08f; // 좌우 마우스 감도
        [SerializeField] private float verticalSensitivity = 0.08f; // 상하 마우스 감도
        [SerializeField] private float minimumPitch = -85f; // 아래쪽 시점 제한
        [SerializeField] private float maximumPitch = 85f; // 위쪽 시점 제한
        private PlayerInputReader inputReader; // 플레이어 입력 래퍼
        private float pitch; // 현재 상하 회전값

        public bool IsCursorLocked => Cursor.lockState == CursorLockMode.Locked; // 커서 잠금 상태 공개

        private void Awake() // 시점 컴포넌트 초기화
        {
            inputReader = GetComponent<PlayerInputReader>(); // 입력 래퍼 참조 획득

            if (viewTransform == null) // 카메라 트랜스폼 미지정 확인
            {
                Camera childCamera = GetComponentInChildren<Camera>(true); // 자식 카메라 조회
                viewTransform = childCamera == null ? null : childCamera.transform; // 카메라 트랜스폼 자동 지정
            }
        }

        private void Start() // 플레이 시작 처리
        {
            SetCursorLocked(true); // 플레이 시작 시 커서 잠금
        }

        private void Update() // 프레임별 시점 처리
        {
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) // ESC 입력 확인
            {
                SetCursorLocked(!IsCursorLocked); // 커서 잠금 상태 전환
            }

            if (!IsCursorLocked || viewTransform == null) // 시점 입력 처리 가능 여부 확인
            {
                return; // 잠금 해제 상태에서는 시점 회전 중지
            }

            Vector2 lookInput = inputReader.Look; // 현재 시점 입력 읽기
            float yawDelta = lookInput.x * horizontalSensitivity; // 좌우 회전량 계산
            float pitchDelta = lookInput.y * verticalSensitivity; // 상하 회전량 계산
            transform.Rotate(Vector3.up, yawDelta, Space.Self); // 플레이어 몸체 좌우 회전
            pitch = Mathf.Clamp(pitch - pitchDelta, minimumPitch, maximumPitch); // 상하 회전값 제한
            viewTransform.localRotation = Quaternion.Euler(pitch, 0f, 0f); // 카메라 상하 회전 적용
        }

        public void Configure(Transform cameraTransform) // 에디터 자동 설정용 카메라 지정
        {
            viewTransform = cameraTransform; // 카메라 시점 트랜스폼 저장
        }

        private static void SetCursorLocked(bool locked) // 커서 잠금 상태 적용
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None; // 커서 잠금 모드 적용
            Cursor.visible = !locked; // 잠금 상태에 맞춰 커서 표시 전환
        }

        private void OnValidate() // 인스펙터 값 검증
        {
            horizontalSensitivity = Mathf.Max(0.001f, horizontalSensitivity); // 좌우 감도 최소값 보정
            verticalSensitivity = Mathf.Max(0.001f, verticalSensitivity); // 상하 감도 최소값 보정
            minimumPitch = Mathf.Clamp(minimumPitch, -89f, 0f); // 아래쪽 제한 범위 보정
            maximumPitch = Mathf.Clamp(maximumPitch, 0f, 89f); // 위쪽 제한 범위 보정
        }
    }
}
