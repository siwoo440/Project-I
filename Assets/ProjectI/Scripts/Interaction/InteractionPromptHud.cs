using UnityEngine; // 유니티 기본 기능 참조

namespace ProjectI.Interaction // 상호작용 기능 네임스페이스
{
    [RequireComponent(typeof(PlayerInteractor))] // 상호작용 감지 기능 필수 지정
    public sealed class InteractionPromptHud : MonoBehaviour // 화면 중앙 상호작용 안내 연결 (그리기는 GameHud)
    {
        [SerializeField] private PlayerInteractor interactor; // 플레이어 상호작용 기능 참조

        private void Awake() // HUD 초기화
        {
            if (interactor == null) // 상호작용 기능 미지정 확인
            {
                interactor = GetComponent<PlayerInteractor>(); // 같은 오브젝트에서 자동 연결
            }
        }

        public void Configure(PlayerInteractor targetInteractor) // 에디터 자동 설정용 참조 지정
        {
            interactor = targetInteractor; // 상호작용 기능 저장
        }

        public string CurrentPrompt => interactor == null || !interactor.HasTarget ? string.Empty : interactor.PromptText; // 표시할 안내 (정식 HUD GameHud 가 그림)
    }
}
