using ProjectI.Interaction; // 기존 F 상호작용 공통 인터페이스 참조
using UnityEngine; // 유니티 컴포넌트·검색 기능 참조

namespace ProjectI.Expedition // 원정 테스트 기능 네임스페이스
{
    public sealed class ExpeditionReturnTerminal : MonoBehaviour, IInteractable // 현재 플레이어 상태 기준 원정 결과를 확정하는 Day22 테스트 단말
    {
        [SerializeField] private ExpeditionOutcomeController outcomeController; // 원정 결과 판정 컨트롤러 참조

        public string Prompt => "현재 상태로 원정 귀환 판정"; // 화면 상호작용 안내 문구
        public InteractionType InteractionType => InteractionType.Press; // F 한 번 누르기 방식 사용
        public float HoldDuration => 0f; // 길게 누르기 시간 불필요

        private void Awake() // 원정 결과 컨트롤러 초기 연결
        {
            ResolveController(); // 씬의 결과 컨트롤러 자동 조회
        }

        public void Configure(ExpeditionOutcomeController controller) // Day22 에디터 자동 설정용 결과 컨트롤러 지정
        {
            outcomeController = controller; // 원정 결과 컨트롤러 저장
        }

        public bool CanInteract(PlayerInteractor interactor) // 살아있는 플레이어가 결과 단말을 사용할 수 있는지 확인
        {
            ResolveController(); // 최신 결과 컨트롤러 참조 보정
            return interactor != null && outcomeController != null && !outcomeController.HasResolved; // 결과 미확정 상태에서만 사용 허용
        }

        public void Interact(PlayerInteractor interactor) // F 입력으로 현재 생존 인원 기준 귀환 결과 처리
        {
            ResolveController(); // 결과 컨트롤러 참조 확인

            if (outcomeController != null) // 유효 결과 컨트롤러 확인
            {
                outcomeController.ResolveFromCurrentPlayers(); // 정상·부분·실패 판정과 미회수품 손실 처리
            }
        }

        private void ResolveController() // 씬의 원정 결과 컨트롤러 자동 조회
        {
            if (outcomeController == null) // 인스펙터 참조 누락 확인
            {
                outcomeController = Object.FindFirstObjectByType<ExpeditionOutcomeController>(); // 현재 씬 첫 결과 컨트롤러 조회
            }
        }
    }
}
