using UnityEngine; // 유니티 기본 기능 참조

namespace ProjectI.Player // 플레이어 기능 네임스페이스
{
    [RequireComponent(typeof(CharacterController))] // 캐릭터 컨트롤러 필수 지정
    [RequireComponent(typeof(PlayerInputReader))] // 입력 래퍼 필수 지정
    public sealed class PlayerCrouch : MonoBehaviour // 플레이어 웅크리기 처리 컴포넌트
    {
        [SerializeField] private Transform viewTransform; // 1인칭 카메라 시점 트랜스폼
        [SerializeField] private float standingHeight = 1.8f; // 서 있을 때 컨트롤러 높이
        [SerializeField] private float crouchingHeight = 1.1f; // 웅크릴 때 컨트롤러 높이
        [SerializeField] private float standingViewHeight = 1.65f; // 서 있을 때 카메라 높이
        [SerializeField] private float crouchingViewHeight = 0.95f; // 웅크릴 때 카메라 높이
        [SerializeField] private float transitionSpeed = 7f; // 웅크림 전환 속도
        [SerializeField] private LayerMask ceilingMask = ~0; // 천장 검사 레이어
        private CharacterController characterController; // 캐릭터 충돌 컨트롤러
        private PlayerInputReader inputReader; // 플레이어 입력 래퍼

        public bool IsCrouching { get; private set; } // 현재 웅크림 상태 공개
        public bool IsStandBlocked { get; private set; } // 현재 일어서기 차단 여부 공개

        private void Awake() // 웅크리기 컴포넌트 초기화
        {
            characterController = GetComponent<CharacterController>(); // 캐릭터 컨트롤러 참조 획득
            inputReader = GetComponent<PlayerInputReader>(); // 입력 래퍼 참조 획득

            if (viewTransform == null) // 카메라 트랜스폼 미지정 확인
            {
                Camera childCamera = GetComponentInChildren<Camera>(true); // 자식 카메라 조회
                viewTransform = childCamera == null ? null : childCamera.transform; // 카메라 트랜스폼 자동 지정
            }

            if (viewTransform != null && viewTransform.localPosition.y > 0.1f) // 유효한 기존 카메라 높이 확인
            {
                standingViewHeight = viewTransform.localPosition.y; // 기존 카메라 높이를 서 있는 높이로 사용
            }
        }

        private void Update() // 프레임별 웅크리기 처리
        {
            bool crouchRequested = inputReader.CrouchHeld; // 현재 웅크리기 입력 확인
            IsStandBlocked = !crouchRequested && !CanStand(); // 일어서기 시도 시 천장 차단 여부 계산
            IsCrouching = crouchRequested || IsStandBlocked; // 입력 또는 천장 차단에 따라 웅크림 상태 결정
            float targetHeight = IsCrouching ? crouchingHeight : standingHeight; // 목표 컨트롤러 높이 선택
            float targetViewHeight = IsCrouching ? crouchingViewHeight : standingViewHeight; // 목표 카메라 높이 선택
            characterController.height = Mathf.MoveTowards(characterController.height, targetHeight, transitionSpeed * Time.deltaTime); // 컨트롤러 높이 부드럽게 전환
            characterController.center = new Vector3(0f, characterController.height * 0.5f, 0f); // 발 위치를 유지하도록 컨트롤러 중심 조정

            if (viewTransform != null) // 카메라 참조 존재 확인
            {
                Vector3 localPosition = viewTransform.localPosition; // 현재 카메라 로컬 위치 저장
                localPosition.y = Mathf.MoveTowards(localPosition.y, targetViewHeight, transitionSpeed * Time.deltaTime); // 카메라 높이 부드럽게 전환
                viewTransform.localPosition = localPosition; // 카메라 위치 적용
            }
        }

        public void Configure(Transform cameraTransform) // 에디터 자동 설정용 카메라 지정
        {
            viewTransform = cameraTransform; // 카메라 트랜스폼 저장

            if (viewTransform != null && viewTransform.localPosition.y > 0.1f) // 유효한 카메라 높이 확인
            {
                standingViewHeight = viewTransform.localPosition.y; // 현재 카메라 높이를 기준값으로 저장
            }
        }

        private bool CanStand() // 서 있는 높이의 공간 확보 여부 검사
        {
            if (!IsCrouching && characterController.height >= standingHeight - 0.01f) // 이미 서 있는 상태 확인
            {
                return true; // 추가 공간 검사 없이 일어서기 가능 반환
            }

            float radius = Mathf.Max(0.05f, characterController.radius * 0.95f); // 검사 캡슐 반지름 계산
            Vector3 bottom = transform.position + (Vector3.up * (radius + 0.05f)); // 검사 캡슐 하단 중심 계산
            Vector3 top = transform.position + (Vector3.up * (standingHeight - radius)); // 검사 캡슐 상단 중심 계산
            Collider[] overlaps = Physics.OverlapCapsule(bottom, top, radius, ceilingMask, QueryTriggerInteraction.Ignore); // 서 있는 공간과 겹치는 콜라이더 조회

            foreach (Collider overlap in overlaps) // 겹친 콜라이더 순회
            {
                if (overlap == null) // 유효하지 않은 콜라이더 확인
                {
                    continue; // 다음 콜라이더 검사
                }

                if (overlap.transform == transform || overlap.transform.IsChildOf(transform)) // 플레이어 자신의 콜라이더 여부 확인
                {
                    continue; // 자신의 콜라이더는 천장 검사에서 제외
                }

                return false; // 외부 콜라이더가 있으면 일어서기 불가 반환
            }

            return true; // 공간이 비어 있으면 일어서기 가능 반환
        }

        private void OnValidate() // 인스펙터 값 검증
        {
            standingHeight = Mathf.Max(1f, standingHeight); // 서 있는 높이 최소값 보정
            crouchingHeight = Mathf.Clamp(crouchingHeight, 0.6f, standingHeight - 0.1f); // 웅크린 높이 범위 보정
            standingViewHeight = Mathf.Clamp(standingViewHeight, 0.5f, standingHeight); // 서 있는 카메라 높이 보정
            crouchingViewHeight = Mathf.Clamp(crouchingViewHeight, 0.4f, crouchingHeight); // 웅크린 카메라 높이 보정
            transitionSpeed = Mathf.Max(0.1f, transitionSpeed); // 전환 속도 최소값 보정
        }
    }
}
