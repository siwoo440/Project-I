using ProjectI.Interaction; // 기존 F 상호작용 인터페이스 참조
using UnityEngine; // 유니티 배열·수치 보정 기능 참조

namespace ProjectI.Economy // 사무소 경제 기능 네임스페이스
{
    [RequireComponent(typeof(Collider))] // 장부 F 상호작용용 Collider 필수 지정
    public sealed class DebtLedger : MonoBehaviour, IInteractable // 공동 자금을 사용해 6단계 계약 채무를 순차 상환
    {
        [SerializeField] private CampaignEconomy economy; // 채무 상환에 사용할 사무소 공동 경제 상태
        [SerializeField] private int[] debtTargets = { 1000, 1500, 2250, 3375, 5062, 7593 }; // 기획서 기준 6단계 채무 목표
        [SerializeField] private int currentPhaseIndex; // 현재 상환 중인 0기반 단계
        [SerializeField] private int paidInCurrentPhase; // 현재 단계에서 이미 상환한 금액

        public string Prompt => BuildPrompt(); // 현재 단계·잔여액·공동 자금 안내 문구
        public InteractionType InteractionType => InteractionType.Press; // 장부는 F 한 번 누를 때 가능한 금액 상환
        public float HoldDuration => 0f; // 길게 누르기 시간 불필요
        public int CurrentPhase => IsCompleted ? debtTargets.Length : currentPhaseIndex + 1; // 화면 표시용 현재 단계 공개
        public int CurrentTarget => IsCompleted ? 0 : debtTargets[currentPhaseIndex]; // 현재 단계 목표 금액 공개
        public int PaidInCurrentPhase => paidInCurrentPhase; // 현재 단계 누적 납부액 공개
        public int RemainingDebt => IsCompleted ? 0 : Mathf.Max(0, CurrentTarget - paidInCurrentPhase); // 현재 단계 남은 채무 공개
        public bool IsCompleted => debtTargets == null || debtTargets.Length == 0 || currentPhaseIndex >= debtTargets.Length; // 6단계 전체 완료 여부 공개

        private void Awake() // 장부 런타임 참조 초기화
        {
            ResolveEconomy(); // 현재 씬 공동 경제 상태 자동 연결
            NormalizeState(); // 직렬화된 단계와 납부 상태 범위 보정
        }

        public void Configure(CampaignEconomy targetEconomy) // 에디터 자동 구성용 경제 상태 연결
        {
            economy = targetEconomy; // 공동 자금 상태 저장
            NormalizeState(); // 단계 배열과 현재 진행 상태 보정
        }

        public bool CanInteract(PlayerInteractor interactor) // 현재 공동 자금으로 채무 상환이 가능한지 확인
        {
            ResolveEconomy(); // 최신 공동 경제 상태 확보
            return economy != null && !IsCompleted && RemainingDebt > 0 && economy.SharedFunds > 0; // 미완료 채무와 사용할 자금이 있을 때만 상호작용 허용
        }

        public void Interact(PlayerInteractor interactor) // F 입력으로 현재 가능한 최대 금액 상환
        {
            PayAvailableFunds(); // 현재 단계 남은 채무와 공동 자금 중 작은 금액을 자동 납부
        }

        public int PayAvailableFunds() // 현재 공동 자금 범위에서 현재 단계 채무를 최대한 상환
        {
            ResolveEconomy(); // 상환 시점 공동 경제 상태 확보
            NormalizeState(); // 단계 진행 상태 안전 보정

            if (economy == null || IsCompleted) // 경제 상태 또는 남은 채무 존재 여부 확인
            {
                return 0; // 상환할 금액 없음 반환
            }

            int payment = Mathf.Min(economy.SharedFunds, RemainingDebt); // 공동 자금과 현재 남은 채무 중 실제 납부 가능 금액 계산

            if (payment <= 0 || !economy.TrySpend(payment)) // 유효 납부 금액과 공동 자금 차감 성공 여부 확인
            {
                return 0; // 상환 실패 반환
            }

            paidInCurrentPhase += payment; // 현재 단계 누적 납부액 증가
            int completedPhase = currentPhaseIndex + 1; // 단계 완료 로그용 현재 단계 번호 저장

            if (paidInCurrentPhase >= CurrentTarget) // 현재 단계 목표 달성 여부 확인
            {
                currentPhaseIndex++; // 다음 채무 단계로 진행
                paidInCurrentPhase = 0; // 새 단계 납부액 초기화
                Debug.Log($"[Project I] 채무 {completedPhase}단계 상환 완료", this); // 개발용 단계 완료 로그 출력
            }

            return payment; // 실제 공동 자금에서 납부한 금액 반환
        }

        private string BuildPrompt() // 현재 장부 상태의 F 안내 문구 생성
        {
            ResolveEconomy(); // 안내 생성 시 공동 자금 상태 확보

            if (IsCompleted) // 전체 채무 완료 여부 확인
            {
                return "계약 채무 전액 상환 완료"; // 캠페인 경제 목표 완료 안내 반환
            }

            int funds = economy == null ? 0 : economy.SharedFunds; // 현재 공동 자금 조회
            return $"채무 {CurrentPhase}단계 / 남은 {RemainingDebt} / 공동 자금 {funds} / 가능한 금액 상환"; // 현재 단계 경제 상태 안내 반환
        }

        private void ResolveEconomy() // 현재 씬 공동 경제 상태 자동 연결
        {
            if (economy == null) // 연결된 경제 상태 누락 여부 확인
            {
                economy = Object.FindFirstObjectByType<CampaignEconomy>(); // 현재 씬의 CampaignEconomy 조회
            }
        }

        private void NormalizeState() // 채무 단계 배열과 현재 진행 상태 안전 보정
        {
            if (debtTargets == null || debtTargets.Length == 0) // 기획서 기본 단계 배열 누락 확인
            {
                debtTargets = new[] { 1000, 1500, 2250, 3375, 5062, 7593 }; // 6단계 기본 상환 목표 복구
            }

            for (int index = 0; index < debtTargets.Length; index++) // 모든 단계 목표 순회
            {
                debtTargets[index] = Mathf.Max(1, debtTargets[index]); // 각 단계 목표 최소값 보장
            }

            currentPhaseIndex = Mathf.Clamp(currentPhaseIndex, 0, debtTargets.Length); // 현재 단계 인덱스 범위 보정
            paidInCurrentPhase = IsCompleted ? 0 : Mathf.Clamp(paidInCurrentPhase, 0, debtTargets[currentPhaseIndex] - 1); // 완료 여부에 맞춰 현재 단계 납부액 보정
        }

        private void OnValidate() // 인스펙터 채무 값 검증
        {
            NormalizeState(); // 편집 중 단계 배열과 진행 상태 보정
        }
    }
}
