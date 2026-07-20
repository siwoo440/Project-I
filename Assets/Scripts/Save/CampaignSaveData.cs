using System; // 직렬화 속성 사용

namespace ProjectI // 프로젝트 공통 네임스페이스
{
    [Serializable] // JsonUtility가 저장할 수 있도록 직렬화 지정
    public class CampaignSaveData // 캠페인 저장 파일에 기록할 데이터
    {
        public int saveVersion = 1; // 저장 파일 버전
        public int currentDay = 1; // 저장된 현재 날짜
        public int deadlineDay = 10; // 저장된 빚 상환 마감 날짜
        public int gold = 0; // 저장된 보유 골드
        public int remainingDebt = 10000; // 저장된 남은 빚
        public int completedRuns = 0; // 저장된 완료 탐험 횟수
        public bool campaignWon = false; // 저장된 캠페인 성공 여부
        public bool campaignFailed = false; // 저장된 캠페인 실패 여부
        public string[] pendingShopItemKeys = Array.Empty<string>(); // 다음 탐험에 전달할 구매품 식별자
        public int[] remainingShopStocks = Array.Empty<int>(); // 현재 마을 상점의 남은 재고
        public string savedAt = string.Empty; // 저장한 날짜와 시간
    }
}