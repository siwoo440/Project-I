using UnityEngine; // 유니티 기본 기능 참조

namespace ProjectI.Player // 플레이어 기능 네임스페이스
{
    [RequireComponent(typeof(CharacterController))] // 캐릭터 컨트롤러 필수 지정
    [RequireComponent(typeof(PlayerInputReader))] // 입력 래퍼 필수 지정
    [RequireComponent(typeof(PlayerStamina))] // 스태미나 컴포넌트 필수 지정
    public sealed class PlayerMovement : MonoBehaviour // 1인칭 이동 처리 컴포넌트
    {
        [SerializeField] private float walkSpeed = 4.2f; // 기본 걷기 속도
        [SerializeField] private float sprintSpeed = 6.6f; // 달리기 속도
        [SerializeField] private float strafeMultiplier = 0.9f; // 좌우 이동 속도 배율
        [SerializeField] private float backwardMultiplier = 0.8f; // 후진 속도 배율
        [SerializeField] private float groundStickVelocity = -2f; // 지면 밀착용 하강 속도
        [SerializeField] private float basicGravity = -20f; // 3일차 기본 중력
        private CharacterController characterController; // 캐릭터 충돌 컨트롤러
        private PlayerInputReader inputReader; // 플레이어 입력 래퍼
        private PlayerStamina stamina; // 플레이어 스태미나
        private float verticalVelocity; // 현재 수직 속도

        public float CurrentPlanarSpeed { get; private set; } // 현재 수평 이동 속도 공개
        public bool IsSprinting { get; private set; } // 현재 달리기 여부 공개

        private void Awake() // 이동 컴포넌트 초기화
        {
            characterController = GetComponent<CharacterController>(); // 캐릭터 컨트롤러 참조 획득
            inputReader = GetComponent<PlayerInputReader>(); // 입력 래퍼 참조 획득
            stamina = GetComponent<PlayerStamina>(); // 스태미나 참조 획득
        }

        private void Update() // 프레임별 이동 처리
        {
            Vector2 moveInput = inputReader.Move; // 현재 이동 입력 읽기
            Vector2 directionalInput = ApplyDirectionalSpeed(moveInput); // 방향별 이동 배율 적용
            Vector3 localMove = new Vector3(directionalInput.x, 0f, directionalInput.y); // 로컬 이동 벡터 생성

            if (localMove.sqrMagnitude > 1f) // 대각선 입력 초과 확인
            {
                localMove.Normalize(); // 대각선 속도 정규화
            }

            bool isMoving = localMove.sqrMagnitude > 0.0001f; // 실제 이동 입력 여부 계산
            IsSprinting = stamina.UpdateSprint(inputReader.SprintHeld, isMoving, Time.deltaTime); // 스태미나 기반 달리기 상태 갱신
            float targetSpeed = IsSprinting ? sprintSpeed : walkSpeed; // 현재 목표 이동 속도 선택
            Vector3 worldMove = transform.TransformDirection(localMove) * targetSpeed; // 카메라 몸체 방향 기준 월드 이동 계산
            CurrentPlanarSpeed = new Vector3(worldMove.x, 0f, worldMove.z).magnitude; // 현재 수평 속도 저장

            if (characterController.isGrounded && verticalVelocity < 0f) // 지면 접촉 상태 확인
            {
                verticalVelocity = groundStickVelocity; // 지면 밀착용 하강 속도 적용
            }
            else // 공중 상태 처리
            {
                verticalVelocity += basicGravity * Time.deltaTime; // 기본 중력 누적
            }

            Vector3 motion = worldMove + (Vector3.up * verticalVelocity); // 수평 이동과 중력 결합
            characterController.Move(motion * Time.deltaTime); // 충돌을 반영한 최종 이동 실행
        }

        private Vector2 ApplyDirectionalSpeed(Vector2 input) // 방향별 이동 속도 배율 적용
        {
            Vector2 adjusted = input; // 원본 입력 복사
            adjusted.x *= strafeMultiplier; // 좌우 이동 속도 보정

            if (adjusted.y < 0f) // 후진 입력 확인
            {
                adjusted.y *= backwardMultiplier; // 후진 이동 속도 보정
            }

            return adjusted; // 보정된 입력 반환
        }

        private void OnValidate() // 인스펙터 값 검증
        {
            walkSpeed = Mathf.Max(0.1f, walkSpeed); // 걷기 속도 최소값 보정
            sprintSpeed = Mathf.Max(walkSpeed, sprintSpeed); // 달리기 속도를 걷기 이상으로 보정
            strafeMultiplier = Mathf.Clamp(strafeMultiplier, 0.1f, 1f); // 좌우 배율 범위 보정
            backwardMultiplier = Mathf.Clamp(backwardMultiplier, 0.1f, 1f); // 후진 배율 범위 보정
            basicGravity = Mathf.Min(-0.1f, basicGravity); // 중력 방향을 아래쪽으로 고정
        }
    }
}
