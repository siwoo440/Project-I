using ProjectI.Items; // 아이템 운반 기능 참조
using ProjectI.Player; // 플레이어 입력 기능 참조
using UnityEngine; // 유니티 기본 기능 참조

namespace ProjectI.Interaction // 상호작용 기능 네임스페이스
{
    [RequireComponent(typeof(PlayerInputReader))] // 플레이어 입력 래퍼 필수 지정
    [RequireComponent(typeof(PlayerCarryController))] // 아이템 운반 기능 필수 지정
    public sealed class PlayerInteractor : MonoBehaviour // 카메라 중앙 기반 상호작용 처리
    {
        [SerializeField] private Camera viewCamera; // 상호작용 Ray 기준 카메라
        [SerializeField] private float interactionDistance = 3f; // 최대 상호작용 거리
        [SerializeField] private LayerMask interactionMask = ~0; // 상호작용 감지 레이어
        private PlayerInputReader inputReader; // 플레이어 입력 래퍼
        private PlayerCarryController carryController; // 아이템 운반 기능
        private IInteractable currentTarget; // 현재 바라보는 상호작용 대상
        private MonoBehaviour currentTargetBehaviour; // 현재 대상의 유니티 컴포넌트 참조
        private float holdElapsed; // 현재 Hold 경과 시간
        private bool holdCompletedThisPress; // 현재 입력에서 Hold 완료 여부

        public bool HasTarget => currentTarget != null; // 현재 대상 존재 여부 반환
        public IInteractable CurrentTarget => currentTarget; // 현재 대상 공개
        public InteractionType CurrentInteractionType => currentTarget == null ? InteractionType.Press : currentTarget.InteractionType; // 현재 상호작용 방식 반환
        public float HoldProgress => currentTarget == null ? 0f : InteractionProgress.Normalize(holdElapsed, currentTarget.HoldDuration); // 현재 Hold 진행도 반환
        public string PromptText => BuildPromptText(); // 현재 화면 안내 문구 반환
        public PlayerCarryController CarryController => carryController; // 월드 아이템이 운반 기능에 접근하도록 공개

        private void Awake() // 상호작용 기능 초기화
        {
            inputReader = GetComponent<PlayerInputReader>(); // 입력 래퍼 참조 획득
            carryController = GetComponent<PlayerCarryController>(); // 운반 기능 참조 획득

            if (viewCamera == null) // 카메라 미지정 확인
            {
                viewCamera = GetComponentInChildren<Camera>(true); // 자식 카메라 자동 조회
            }
        }

        private void Update() // 프레임별 상호작용 처리
        {
            DetectTarget(); // 현재 시선 대상 갱신
            ProcessInteractionInput(); // 현재 대상에 입력 처리
        }

        public void Configure(Camera camera, PlayerInputReader reader, PlayerCarryController carry) // 에디터 자동 설정용 참조 지정
        {
            viewCamera = camera; // 카메라 참조 저장
            inputReader = reader; // 입력 래퍼 참조 저장
            carryController = carry; // 운반 기능 참조 저장
        }

        private void DetectTarget() // 카메라 중앙 Raycast로 대상 감지
        {
            IInteractable detectedTarget = null; // 이번 프레임 감지 대상 초기화
            MonoBehaviour detectedBehaviour = null; // 이번 프레임 대상 컴포넌트 초기화

            if (viewCamera != null) // 카메라 존재 확인
            {
                Ray ray = new Ray(viewCamera.transform.position, viewCamera.transform.forward); // 카메라 중앙 전방 Ray 생성

                if (Physics.Raycast(ray, out RaycastHit hit, interactionDistance, interactionMask, QueryTriggerInteraction.Ignore)) // 전방 물체 감지
                {
                    FindInteractable(hit.collider, out detectedTarget, out detectedBehaviour); // 충돌체에서 상호작용 대상 탐색
                }
            }

            if (ReferenceEquals(detectedTarget, currentTarget)) // 기존 대상과 동일한지 확인
            {
                return; // 대상 변경 처리 불필요
            }

            currentTarget = detectedTarget; // 새 대상 저장
            currentTargetBehaviour = detectedBehaviour; // 새 대상 컴포넌트 저장
            ResetHold(); // 대상 변경 시 Hold 상태 초기화
        }

        private void FindInteractable(Collider source, out IInteractable interactable, out MonoBehaviour behaviour) // 충돌체 부모에서 인터페이스 구현체 탐색
        {
            interactable = null; // 반환 대상 초기화
            behaviour = null; // 반환 컴포넌트 초기화
            MonoBehaviour[] candidates = source.GetComponentsInParent<MonoBehaviour>(true); // 부모 방향 MonoBehaviour 목록 조회

            foreach (MonoBehaviour candidate in candidates) // 후보 컴포넌트 순회
            {
                if (!(candidate is IInteractable candidateInteractable)) // 상호작용 인터페이스 구현 여부 확인
                {
                    continue; // 다음 후보 검사
                }

                if (!candidateInteractable.CanInteract(this)) // 현재 플레이어가 상호작용 가능한지 확인
                {
                    continue; // 사용할 수 없는 대상 건너뜀
                }

                interactable = candidateInteractable; // 사용 가능한 대상 저장
                behaviour = candidate; // 유니티 컴포넌트 저장
                return; // 첫 유효 대상 사용
            }
        }

        private void ProcessInteractionInput() // 현재 대상의 방식에 따라 입력 처리
        {
            if (currentTarget == null || currentTargetBehaviour == null) // 유효 대상 누락 확인
            {
                ResetHold(); // Hold 상태 초기화
                return; // 입력 처리 중단
            }

            if (!currentTarget.CanInteract(this)) // 대상 상태가 중간에 변경되었는지 확인
            {
                currentTarget = null; // 현재 대상 제거
                currentTargetBehaviour = null; // 현재 대상 컴포넌트 제거
                ResetHold(); // Hold 상태 초기화
                return; // 입력 처리 중단
            }

            if (currentTarget.InteractionType == InteractionType.Hold) // Hold 방식 확인
            {
                ProcessHoldInteraction(); // 길게 누르기 처리
                return; // Hold 처리 후 종료
            }

            if (inputReader.InteractPressed) // Press 또는 Toggle 입력 시작 확인
            {
                currentTarget.Interact(this); // 대상 즉시 실행
            }
        }

        private void ProcessHoldInteraction() // 길게 누르기 상호작용 처리
        {
            if (!inputReader.InteractHeld) // F 유지 상태가 아닌지 확인
            {
                ResetHold(); // 손을 떼면 진행도 초기화
                return; // Hold 처리 중단
            }

            if (holdCompletedThisPress) // 현재 입력에서 이미 완료했는지 확인
            {
                return; // 같은 누름에서 중복 실행 방지
            }

            holdElapsed += Time.deltaTime; // Hold 경과 시간 누적
            float requiredDuration = Mathf.Max(0.01f, currentTarget.HoldDuration); // 필요 시간을 안전한 값으로 보정

            if (holdElapsed < requiredDuration) // 아직 완료 시간에 도달하지 않았는지 확인
            {
                return; // 다음 프레임까지 대기
            }

            currentTarget.Interact(this); // Hold 완료 대상 실행
            holdCompletedThisPress = true; // 같은 입력에서 재실행 방지
            holdElapsed = requiredDuration; // 진행도를 완료 상태로 고정
        }

        private void ResetHold() // Hold 상태 초기화
        {
            holdElapsed = 0f; // 경과 시간 초기화
            holdCompletedThisPress = false; // 완료 상태 초기화
        }

        private string BuildPromptText() // 현재 대상의 UI 문구 생성
        {
            if (currentTarget == null || currentTargetBehaviour == null) // 유효 대상 존재 여부 확인
            {
                return string.Empty; // 안내 문구 없음 반환
            }

            if (!currentTarget.CanInteract(this)) // 현재 사용할 수 있는지 확인
            {
                return string.Empty; // 사용할 수 없으면 문구 숨김
            }

            return currentTarget.InteractionType == InteractionType.Hold ? $"[F 길게] {currentTarget.Prompt}" : $"[F] {currentTarget.Prompt}"; // 입력 방식에 맞는 문구 반환
        }

        private void OnValidate() // 인스펙터 값 검증
        {
            interactionDistance = Mathf.Max(0.5f, interactionDistance); // 상호작용 거리 최소값 보정
        }
    }
}
