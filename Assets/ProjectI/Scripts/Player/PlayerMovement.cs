using System; // 착지 이벤트 기능 참조
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
        [SerializeField] private float crouchSpeedMultiplier = 0.55f; // 웅크린 상태 이동 속도 배율
        [SerializeField] private float strafeMultiplier = 0.9f; // 좌우 이동 속도 배율
        [SerializeField] private float backwardMultiplier = 0.8f; // 후진 이동 속도 배율
        [SerializeField] private float jumpHeight = 1.2f; // 기본 점프 높이
        [SerializeField] private float gravity = -20f; // 중력 가속도
        [SerializeField] private float groundStickVelocity = -2f; // 지면 밀착용 하강 속도
        private CharacterController characterController; // 캐릭터 충돌 컨트롤러
        private PlayerInputReader inputReader; // 플레이어 입력 래퍼
        private PlayerStamina stamina; // 플레이어 스태미나
        private PlayerCrouch crouch; // 플레이어 웅크리기 상태
        private PlayerHealth health; // 플레이어 체력 상태
        private float verticalVelocity; // 현재 수직 속도
        private float airbornePeakY; // 공중 상태 최고 높이
        private bool wasGrounded = true; // 이전 프레임 지상 상태

        public event Action<float> Landed; // 착지 시 실제 추락 거리 전달 이벤트

        public float CurrentPlanarSpeed { get; private set; } // 현재 수평 이동 속도 공개
        public float CurrentVerticalVelocity => verticalVelocity; // 현재 수직 속도 공개
        public float LastLandingDistance { get; private set; } // 마지막 착지 추락 거리 공개
        public bool IsSprinting { get; private set; } // 현재 달리기 여부 공개
        public bool IsGrounded { get; private set; } // 현재 지상 여부 공개

        private void Awake() // 이동 컴포넌트 초기화
        {
            characterController = GetComponent<CharacterController>(); // 캐릭터 컨트롤러 참조 획득
            inputReader = GetComponent<PlayerInputReader>(); // 입력 래퍼 참조 획득
            stamina = GetComponent<PlayerStamina>(); // 스태미나 참조 획득
            crouch = GetComponent<PlayerCrouch>(); // 웅크리기 참조 획득
            health = GetComponent<PlayerHealth>(); // 체력 참조 획득
            airbornePeakY = transform.position.y; // 시작 높이를 공중 최고점으로 초기화
        }

        private void Update() // 프레임별 이동 처리
        {
            bool groundedBeforeMove = characterController.isGrounded; // 이전 이동 결과 기준 지상 상태 확인

            bool canControl = health == null || !health.IsDead; // 생존 상태에서만 플레이어 입력 허용
            Vector2 moveInput = canControl ? inputReader.Move : Vector2.zero; // 사망 상태에서는 수평 이동 입력 차단
            Vector2 directionalInput = ApplyDirectionalSpeed(moveInput); // 방향별 이동 배율 적용
            Vector3 localMove = new Vector3(directionalInput.x, 0f, directionalInput.y); // 로컬 이동 벡터 생성

            if (localMove.sqrMagnitude > 1f) // 대각선 입력 초과 확인
            {
                localMove.Normalize(); // 대각선 속도 정규화
            }

            bool isMoving = localMove.sqrMagnitude > 0.0001f; // 실제 이동 입력 여부 계산
            bool crouching = crouch != null && crouch.IsCrouching; // 현재 웅크림 상태 확인
            bool sprintRequested = canControl && inputReader.SprintHeld && !crouching; // 생존하고 웅크리지 않을 때만 달리기 요청 허용
            IsSprinting = stamina.UpdateSprint(sprintRequested, isMoving, Time.deltaTime); // 스태미나 기반 달리기 상태 갱신
            float targetSpeed = IsSprinting ? sprintSpeed : walkSpeed; // 현재 목표 이동 속도 선택

            if (crouching) // 웅크린 상태 확인
            {
                targetSpeed *= crouchSpeedMultiplier; // 웅크린 이동 속도 적용
            }

            Vector3 worldMove = transform.TransformDirection(localMove) * targetSpeed; // 플레이어 몸체 방향 기준 월드 이동 계산
            CurrentPlanarSpeed = new Vector3(worldMove.x, 0f, worldMove.z).magnitude; // 현재 수평 속도 저장

            if (groundedBeforeMove && verticalVelocity < 0f) // 지면 접촉 중 하강 상태 확인
            {
                verticalVelocity = groundStickVelocity; // 지면 밀착용 하강 속도 적용
            }

            if (canControl && groundedBeforeMove && inputReader.JumpPressed && !crouching) // 생존 상태의 지상 점프 입력 확인
            {
                verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity); // 목표 점프 높이에 맞는 초기 속도 계산
                airbornePeakY = transform.position.y; // 점프 시작 높이를 공중 최고점으로 초기화
            }
            else if (!groundedBeforeMove) // 공중 상태 확인
            {
                verticalVelocity += gravity * Time.deltaTime; // 중력 가속도 누적
                airbornePeakY = Mathf.Max(airbornePeakY, transform.position.y); // 공중 최고 높이 갱신
            }

            Vector3 motion = worldMove + (Vector3.up * verticalVelocity); // 수평 이동과 수직 이동 결합
            CollisionFlags collisionFlags = characterController.Move(motion * Time.deltaTime); // 충돌을 반영한 최종 이동 실행
            bool groundedAfterMove = (collisionFlags & CollisionFlags.Below) != 0 || characterController.isGrounded; // 이동 후 지상 상태 계산

            if (!groundedAfterMove) // 공중 상태 확인
            {
                airbornePeakY = Mathf.Max(airbornePeakY, transform.position.y); // 이동 후 공중 최고 높이 갱신
            }

            if (groundedAfterMove && !wasGrounded && verticalVelocity <= 0f) // 새롭게 착지한 상태 확인
            {
                LastLandingDistance = Mathf.Max(0f, airbornePeakY - transform.position.y); // 실제 최고점과 착지점의 높이 차이 계산
                Landed?.Invoke(LastLandingDistance); // 착지 이벤트 발생
                airbornePeakY = transform.position.y; // 다음 추락을 위해 최고점 초기화
            }

            if (groundedAfterMove && verticalVelocity < 0f) // 착지 후 하강 속도 확인
            {
                verticalVelocity = groundStickVelocity; // 지면 밀착용 하강 속도로 보정
            }

            IsGrounded = groundedAfterMove; // 현재 지상 상태 공개값 갱신
            wasGrounded = groundedAfterMove; // 다음 프레임 비교용 지상 상태 저장
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
            crouchSpeedMultiplier = Mathf.Clamp(crouchSpeedMultiplier, 0.1f, 1f); // 웅크림 이동 속도 배율 보정
            strafeMultiplier = Mathf.Clamp(strafeMultiplier, 0.1f, 1f); // 좌우 배율 범위 보정
            backwardMultiplier = Mathf.Clamp(backwardMultiplier, 0.1f, 1f); // 후진 배율 범위 보정
            jumpHeight = Mathf.Max(0.1f, jumpHeight); // 점프 높이 최소값 보정
            gravity = Mathf.Min(-0.1f, gravity); // 중력 방향을 아래쪽으로 고정
        }
    }
}
