using ProjectI.Interaction; // 기존 F 상호작용 인터페이스 참조
using ProjectI.Persistence; // 일차 저장·마감 서비스 참조
using UnityEngine; // 유니티 기본 기능 참조

namespace ProjectI.Loop // 원정 루프 기능 네임스페이스
{
    [RequireComponent(typeof(Collider))] // 플레이어 시선 상호작용용 Collider 필수 지정
    public sealed class DayEndLedgerInteractable : MonoBehaviour, IInteractable // 사무소에서 오늘 원정을 마감하고 다음 날로 넘기는 장부
    {
        [SerializeField] private float holdDuration = 1.2f; // 실수 방지용 길게 누르기 시간

        public string Prompt => BuildPrompt(); // 현재 일차 단계에 맞는 안내 문구
        public InteractionType InteractionType => InteractionType.Hold; // 길게 눌러 마감
        public float HoldDuration => holdDuration; // 길게 누르기 시간 공개

        public bool CanInteract(PlayerInteractor interactor) // 안내 문구를 항상 보여주기 위해 플레이어면 허용
        {
            return interactor != null && DailySnapshotService.Instance != null; // 저장 서비스가 있을 때 사용 가능
        }

        public void Interact(PlayerInteractor interactor) // 길게 누르기 완료 시 일차 마감 요청
        {
            DailySnapshotService service = DailySnapshotService.Instance; // 저장 서비스 조회

            if (service == null || service.DayPhase != ExpeditionDayPhase.Returned) // 오늘 원정 귀환 여부 확인
            {
                Debug.Log($"[Project I] 일차 마감 장부 / {BuildPrompt()}", this); // 마감 불가 안내
                return; // 마감 요청 생략
            }

            service.CompleteCurrentDay(); // 불변 Day_N 기록과 다음 일차 Current 생성
        }

        private string BuildPrompt() // 현재 일차 단계별 안내 문구 생성
        {
            DailySnapshotService service = DailySnapshotService.Instance; // 저장 서비스 조회

            if (service == null || !service.IsInitialized) // 저장 시스템 준비 여부 확인
            {
                return "일차 기록 준비 중"; // 준비 전 문구
            }

            if (service.IsDayCompletionInProgress || service.IsRestoreInProgress) // 처리 중 여부 확인
            {
                return "일차 기록 처리 중"; // 처리 중 문구
            }

            switch (service.DayPhase) // 원정 단계별 문구 선택
            {
                case ExpeditionDayPhase.Returned: return $"{service.CurrentDay}일차 업무 마감 — 다음 날로"; // 마감 가능 문구
                case ExpeditionDayPhase.OnExpedition: return $"{service.CurrentDay}일차 원정 중"; // 원정 중 문구
                default: return $"{service.CurrentDay}일차 — 원정을 다녀온 뒤 마감할 수 있습니다"; // 준비 단계 문구
            }
        }

        private void OnValidate() // 인스펙터 값 검증
        {
            holdDuration = Mathf.Max(0.2f, holdDuration); // 최소 길게 누르기 시간 보장
        }
    }
}
