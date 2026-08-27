using UnityEngine; // 유니티 기본 기능 참조

namespace ProjectI.Interaction // 상호작용 기능 네임스페이스
{
    public sealed class TestInteractable : MonoBehaviour, IInteractable // Press/Hold/Toggle 시험용 상호작용 물체
    {
        [SerializeField] private string prompt = "작동"; // 화면 안내 문구
        [SerializeField] private InteractionType interactionType = InteractionType.Press; // 상호작용 방식
        [SerializeField] private float holdDuration = 1.5f; // 길게 누르기 완료 시간
        [SerializeField] private Transform indicator; // 상태 표시용 오브젝트
        private bool toggled; // Toggle 현재 상태
        private Vector3 baseScale; // 상태 표시 오브젝트 기본 크기

        public string Prompt => prompt; // 현재 안내 문구 반환
        public InteractionType InteractionType => interactionType; // 현재 상호작용 방식 반환
        public float HoldDuration => interactionType == InteractionType.Hold ? holdDuration : 0f; // Hold일 때만 필요 시간 반환
        public bool IsToggled => toggled; // Toggle 상태 공개

        private void Awake() // 시험 물체 초기화
        {
            if (indicator == null) // 상태 표시 오브젝트 누락 확인
            {
                indicator = transform; // 자기 자신을 상태 표시 대상으로 사용
            }

            baseScale = indicator.localScale; // 기본 크기 저장
            ApplyVisualState(); // 초기 상태 시각화 적용
        }

        public bool CanInteract(PlayerInteractor interactor) // 상호작용 가능 여부 반환
        {
            return interactor != null && isActiveAndEnabled; // 활성 상태이고 플레이어가 존재할 때 허용
        }

        public void Interact(PlayerInteractor interactor) // 시험 상호작용 실행
        {
            if (interactionType == InteractionType.Toggle) // Toggle 방식 확인
            {
                toggled = !toggled; // 현재 상태 반전
            }
            else
            {
                toggled = true; // Press/Hold는 작동 상태로 표시
            }

            ApplyVisualState(); // 상태 시각화 갱신
            Debug.Log($"[Project I] {name} 상호작용 실행 - {interactionType}", this); // 개발용 실행 로그 출력
        }

        public void Configure(string displayPrompt, InteractionType type, float duration, Transform visualIndicator = null) // 에디터 자동 설정용 값 지정
        {
            prompt = displayPrompt; // 안내 문구 저장
            interactionType = type; // 상호작용 방식 저장
            holdDuration = Mathf.Max(0.1f, duration); // Hold 시간 최소값 보정
            indicator = visualIndicator == null ? transform : visualIndicator; // 상태 표시 대상 저장
            baseScale = indicator.localScale; // 현재 크기를 기본 크기로 저장
            ApplyVisualState(); // 설정값 시각화 적용
        }

        private void ApplyVisualState() // 시험 물체 상태를 크기로 표시
        {
            if (indicator == null) // 상태 표시 대상 누락 확인
            {
                return; // 시각화 처리 중단
            }

            Vector3 targetScale = toggled ? baseScale * 1.2f : baseScale; // 작동 여부에 따른 크기 계산
            indicator.localScale = targetScale; // 상태 표시 크기 적용
        }

        private void OnValidate() // 인스펙터 값 검증
        {
            holdDuration = Mathf.Max(0.1f, holdDuration); // Hold 시간 최소값 보정
        }
    }
}
