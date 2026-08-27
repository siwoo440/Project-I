using UnityEngine; // 유니티 기본 기능 참조

namespace ProjectI.Interaction // 상호작용 기능 네임스페이스
{
    [RequireComponent(typeof(PlayerInteractor))] // 상호작용 감지 기능 필수 지정
    public sealed class InteractionPromptHud : MonoBehaviour // 화면 중앙 상호작용 안내 UI
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

        private void OnGUI() // 즉시 모드 GUI로 안내 문구 출력
        {
            if (interactor == null || !interactor.HasTarget) // 표시할 대상 존재 여부 확인
            {
                return; // UI 출력 중단
            }

            string prompt = interactor.PromptText; // 현재 안내 문구 조회

            if (string.IsNullOrEmpty(prompt)) // 표시할 문구 누락 확인
            {
                return; // UI 출력 중단
            }

            float width = 360f; // 안내 박스 너비 지정
            float height = interactor.CurrentInteractionType == InteractionType.Hold ? 70f : 42f; // Hold 여부에 따른 높이 지정
            float x = (Screen.width - width) * 0.5f; // 화면 중앙 X 위치 계산
            float y = (Screen.height * 0.5f) + 46f; // 조준선 아래 Y 위치 계산
            GUI.Box(new Rect(x, y, width, height), prompt); // 상호작용 안내 박스 출력

            if (interactor.CurrentInteractionType == InteractionType.Hold) // Hold 방식 확인
            {
                float progress = interactor.HoldProgress; // 현재 진행도 조회
                GUI.Box(new Rect(x + 16f, y + 40f, width - 32f, 14f), string.Empty); // 진행도 외곽 출력
                GUI.DrawTexture(new Rect(x + 19f, y + 43f, (width - 38f) * progress, 8f), Texture2D.whiteTexture); // Hold 진행도 출력
            }
        }
    }
}
