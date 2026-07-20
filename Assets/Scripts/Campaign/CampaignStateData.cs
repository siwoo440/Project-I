using UnityEngine; // 직렬화와 수치 제한 기능 사용

namespace ProjectI // 프로젝트 공통 네임스페이스
{
    [System.Serializable] // Unity Inspector와 이후 저장 시스템에서 사용할 수 있도록 지정
    public class CampaignStateData // 골드와 빚 및 날짜를 보관하는 캠페인 상태
    {
        [SerializeField] int currentDay; // 현재 진행할 날짜
        [SerializeField] int deadlineDay; // 빚 상환 마지막 날짜
        [SerializeField] int gold; // 현재 보유 골드
        [SerializeField] int remainingDebt; // 현재 남은 빚
        [SerializeField] int completedRuns; // 완료한 던전 탐험 횟수
        [SerializeField] bool campaignWon; // 빚 상환 성공 여부
        [SerializeField] bool campaignFailed; // 상환 기한 초과 여부

        public int CurrentDay => currentDay; // 현재 날짜 반환
        public int DeadlineDay => deadlineDay; // 상환 마지막 날짜 반환
        public int Gold => gold; // 보유 골드 반환
        public int RemainingDebt => remainingDebt; // 남은 빚 반환
        public int CompletedRuns => completedRuns; // 완료 탐험 횟수 반환
        public bool CampaignWon => campaignWon; // 캠페인 성공 여부 반환
        public bool CampaignFailed => campaignFailed; // 캠페인 실패 여부 반환
        public int RemainingDays => Mathf.Max(0, deadlineDay - currentDay + 1); // 오늘을 포함한 남은 날짜 반환

        public void Initialize(int startingGold, int startingDebt, int lastDay) // 새로운 캠페인 상태 초기화
        {
            currentDay = 1; // 캠페인 시작 날짜를 1일차로 설정
            deadlineDay = Mathf.Max(1, lastDay); // 마지막 날짜를 최소 1일로 제한
            gold = Mathf.Max(0, startingGold); // 시작 골드를 음수가 되지 않도록 저장
            remainingDebt = Mathf.Max(0, startingDebt); // 시작 빚을 음수가 되지 않도록 저장
            completedRuns = 0; // 완료 탐험 횟수 초기화
            campaignWon = remainingDebt == 0; // 시작 빚이 없으면 성공 상태 적용
            campaignFailed = false; // 캠페인 실패 상태 초기화
        }
        public void ApplySavedState(int savedCurrentDay, int savedDeadlineDay, int savedGold, int savedRemainingDebt, int savedCompletedRuns, bool savedWon, bool savedFailed) // 저장 파일의 캠페인 상태 적용
        {
            currentDay = Mathf.Max(1, savedCurrentDay); // 저장된 현재 날짜를 최소 1일로 제한
            deadlineDay = Mathf.Max(1, savedDeadlineDay); // 저장된 마감 날짜를 최소 1일로 제한
            gold = Mathf.Max(0, savedGold); // 저장된 골드를 음수가 되지 않도록 적용
            remainingDebt = Mathf.Max(0, savedRemainingDebt); // 저장된 빚을 음수가 되지 않도록 적용
            completedRuns = Mathf.Max(0, savedCompletedRuns); // 완료 탐험 횟수를 음수가 되지 않도록 적용
            campaignWon = savedWon || remainingDebt == 0; // 저장된 성공 또는 빚 전액 상환 상태 적용
            campaignFailed = !campaignWon && (savedFailed || currentDay > deadlineDay); // 성공하지 못한 기한 초과 상태 적용
        }
        public void AddGold(int amount) // 정산 보상을 보유 골드에 추가
        {
            gold += Mathf.Max(0, amount); // 음수가 아닌 보상만 보유 골드에 추가
        }
        public bool TrySpendGold(int requestedAmount) // 보유 골드 안에서 상점 구매 비용 지불 시도
        {
            int safeAmount = Mathf.Max(0, requestedAmount); // 요청 금액을 음수가 되지 않도록 제한

            if (safeAmount == 0) // 무료 아이템인지 확인
            {
                return true; // 골드 차감 없이 구매 허용
            }

            if (gold < safeAmount) // 현재 골드가 구매 비용보다 적은지 확인
            {
                return false; // 골드 부족으로 구매 실패 반환
            }

            gold -= safeAmount; // 구매 비용만큼 보유 골드 차감
            return true; // 정상 구매 비용 지불 반환
        }

        public int PayDebt(int requestedAmount) // 보유 골드와 남은 빚 안에서 실제 납부 처리
        {
            int safeRequest = Mathf.Max(0, requestedAmount); // 요청 금액을 음수가 되지 않도록 제한
            int actualPayment = Mathf.Min(safeRequest, gold, remainingDebt); // 실제 납부 가능 금액 계산
            gold -= actualPayment; // 실제 납부액만큼 보유 골드 차감
            remainingDebt -= actualPayment; // 실제 납부액만큼 남은 빚 감소

            return actualPayment; // 실제 납부된 금액 반환
        }

        public void CompleteDay() // 정산 완료 후 하루 진행과 캠페인 결과 판정
        {
            completedRuns++; // 완료한 던전 탐험 횟수 증가
            currentDay++; // 다음 날짜로 진행

            if (remainingDebt <= 0) // 빚을 모두 상환했는지 확인
            {
                remainingDebt = 0; // 남은 빚을 정확히 0으로 제한
                campaignWon = true; // 캠페인 성공 상태 활성화
                campaignFailed = false; // 캠페인 실패 상태 해제
                return; // 기한 초과 검사 중단
            }

            if (currentDay > deadlineDay) // 마지막 정산 날짜가 지났는지 확인
            {
                campaignFailed = true; // 캠페인 실패 상태 활성화
            }
        }
    }
}