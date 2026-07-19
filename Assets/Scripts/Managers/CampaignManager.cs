using UnityEngine; // Unity 기본 기능 사용

namespace ProjectI // 프로젝트 공통 네임스페이스
{
    public class CampaignManager : MonoBehaviour // 골드와 빚 및 날짜와 정산 상태 관리
    {
        public static CampaignManager Instance { get; private set; } // 현재 활성 캠페인 매니저 접근점

        [Header("시작 설정")] // Inspector 캠페인 시작 설정 구분
        [SerializeField][Min(0)] int startingGold = 0; // 캠페인 시작 보유 골드
        [SerializeField][Min(0)] int startingDebt = 10000; // 캠페인 시작 빚
        [SerializeField][Min(1)] int deadlineDay = 10; // 빚 상환 마지막 날짜

        [Header("임시 HUD")] // Inspector 캠페인 상태 화면 설정 구분
        [SerializeField] bool showCampaignHud = true; // 보유 골드와 빚 HUD 표시 여부

        [SerializeField] CampaignStateData state = new CampaignStateData(); // 현재 캠페인 진행 상태

        bool settlementOpen; // 현재 플레이어 납부 선택을 기다리는 상태
        int lastRunReward; // 최근 탐험에서 정산한 보물 가치
        int lastDebtPayment; // 최근 정산에서 납부한 빚
        int lastSettlementDay; // 최근 정산을 진행한 날짜
        string lastRunReason; // 최근 탐험 종료 원인

        public CampaignStateData State => state; // 현재 캠페인 상태 반환
        public bool HasOpenSettlement => settlementOpen; // 현재 납부 선택 가능 여부
        public int LastRunReward => lastRunReward; // 최근 탐험 보상 반환
        public int LastDebtPayment => lastDebtPayment; // 최근 빚 납부액 반환
        public int LastSettlementDay => lastSettlementDay; // 최근 정산 날짜 반환
        public string LastRunReason => lastRunReason; // 최근 탐험 종료 원인 반환
        public int MaximumDebtPayment => Mathf.Min(state.Gold, state.RemainingDebt); // 현재 최대 납부 가능액 반환
        public bool CanStartNextRun => !settlementOpen && !state.CampaignWon && !state.CampaignFailed && state.CurrentDay <= state.DeadlineDay; // 정산과 캠페인 상태를 확인한 다음 던전 출발 가능 여부
        public bool IsLastAvailableDay => CanStartNextRun && state.CurrentDay == state.DeadlineDay; // 현재 날짜가 마지막 출발 기회인지 반환
        void Awake() // 캠페인 매니저 싱글톤과 초기 상태 설정
        {
            if (Instance != null && Instance != this) // 기존 캠페인 매니저 존재 여부 확인
            {
                Destroy(gameObject); // 중복 캠페인 매니저 제거
                return; // 중복 초기화 중단
            }

            Instance = this; // 현재 오브젝트를 전역 캠페인 매니저로 저장
            transform.SetParent(null); // DontDestroyOnLoad 적용을 위해 루트로 분리
            DontDestroyOnLoad(gameObject); // Scene 전환 후에도 골드와 빚 상태 유지
            state.Initialize(startingGold, startingDebt, deadlineDay); // 새로운 캠페인 상태 초기화
        }

        void OnDestroy() // 캠페인 매니저 싱글톤 참조 정리
        {
            if (Instance == this) // 현재 오브젝트가 등록된 캠페인 매니저인지 확인
            {
                Instance = null; // 전역 캠페인 매니저 참조 초기화
            }
        }

        public bool ApplyCurrentRunReward() // 현재 던전 결과의 확보 가치를 보유 골드로 한 번만 정산
        {
            if (settlementOpen) // 기존 정산의 납부 선택이 진행 중인지 확인
            {
                return false; // 동일 결과 중복 정산 방지
            }

            if (state.CampaignWon || state.CampaignFailed) // 캠페인이 이미 종료됐는지 확인
            {
                return false; // 종료된 캠페인에 추가 보상 적용 방지
            }

            RunResultManager resultManager = RunResultManager.Instance; // 현재 던전 결과 매니저 가져오기

            if (resultManager == null || !resultManager.HasResult) // 정산 가능한 던전 결과 존재 여부 확인
            {
                return false; // 결과가 없으면 정산 중단
            }

            RunResultData result = resultManager.CurrentResult; // 정산할 던전 결과 가져오기
            lastRunReward = result.SecuredValue; // 확보한 보물 가치 저장
            lastRunReason = result.GetEndReasonText(); // 탐험 종료 원인 저장
            lastDebtPayment = 0; // 이전 빚 납부액 초기화
            lastSettlementDay = state.CurrentDay; // 이번 정산 날짜 저장

            state.AddGold(lastRunReward); // 확보한 보물 가치를 전액 보유 골드로 지급
            settlementOpen = true; // 플레이어 빚 납부 선택 상태 활성화
            resultManager.ClearCurrentResult(); // 정산한 던전 결과를 제거해 중복 지급 방지

            Debug.Log($"[Campaign] {lastSettlementDay}일차 보상 정산 — {lastRunReward}골드 지급"); // 보상 지급 결과 출력
            return true; // 보상 정산 성공 반환
        }

        public bool ConfirmDebtPayment(int requestedAmount) // 플레이어가 선택한 빚 납부액 확정
        {
            if (!settlementOpen) // 납부할 정산이 열려 있는지 확인
            {
                return false; // 중복 또는 잘못된 납부 방지
            }

            lastDebtPayment = state.PayDebt(requestedAmount); // 보유 골드와 남은 빚 안에서 실제 납부
            settlementOpen = false; // 이번 정산의 납부 선택 종료
            state.CompleteDay(); // 납부 후 다음 날짜로 진행

            Debug.Log($"[Campaign] 빚 {lastDebtPayment}골드 납부 — 보유 {state.Gold}골드, 남은 빚 {state.RemainingDebt}골드"); // 납부 결과 출력
            return true; // 빚 납부 확정 성공 반환
        }

        public string GetDeadlineMessage() // 현재 날짜와 캠페인 상태에 맞는 마감 안내 반환
        {
            if (state.CampaignWon) { return "빚을 모두 상환했습니다."; }// 빚 전액 상환 여부 확인 -> 캠페인 성공 문구 반환
            
            if (state.CampaignFailed) { return "빚을 갚지 못한 채 상환 기한이 끝났습니다."; }
            // 상환 기한 초과 여부 확인 ->  캠페인 실패 문구 반환

            if (settlementOpen) { return "빚 납부를 확정해야 다음 탐험을 시작할 수 있습니다."; }
            // 마을 정산 진행 여부 확인 ->  정산 진행 안내 반환

            if (state.RemainingDays <= 1) { return "오늘이 빚을 갚을 수 있는 마지막 날입니다!";  }
            // 마지막 출발 기회인지 확인 -> 마지막 날 경고 반환

            if (state.RemainingDays <= 3) { return $"상환 기한이 얼마 남지 않았습니다. 남은 기회 {state.RemainingDays}일"; }
             // 마감이 3일 이하로 남았는지 확인 -> 마감 임박 경고 반환

            return $"상환 기한까지 {state.RemainingDays}일 남았습니다."; // 일반 날짜 안내 반환
        }

        [ContextMenu("캠페인 상태 초기화")]
        public void ResetCampaign() // 개발 테스트를 위해 캠페인 상태 초기화
        {
            state.Initialize(startingGold, startingDebt, deadlineDay); // Inspector 시작값으로 캠페인 재설정
            settlementOpen = false; // 진행 중인 정산 상태 해제
            lastRunReward = 0; // 최근 보상 초기화
            lastDebtPayment = 0; // 최근 납부액 초기화
            lastSettlementDay = 0; // 최근 정산 날짜 초기화
            lastRunReason = string.Empty; // 최근 종료 원인 초기화

            if (RunResultManager.Instance != null) // 결과 매니저 존재 여부 확인
            {
                RunResultManager.Instance.ClearCurrentResult(); // 테스트용 이전 던전 결과 초기화
            }

            Debug.Log("[Campaign] 캠페인 상태를 초기화했습니다."); // 초기화 완료 출력
        }

        void OnGUI() // 보유 골드와 빚 및 날짜를 임시 HUD로 표시
        {
            if (!showCampaignHud) // 캠페인 HUD 표시 여부 확인
            {
                return; // HUD 표시 중단
            }

            float width = 320f; // 캠페인 HUD 너비
            float height = 115f; // 캠페인 HUD 높이
            float x = Screen.width - width - 10f; // 화면 오른쪽 HUD 위치
            float y = Mathf.Max(10f, Screen.height - height - 10f); // 화면 아래쪽 HUD 위치

            GUI.Box(new Rect(x, y, width, height), "캠페인"); // 캠페인 HUD 배경 표시
            GUI.Label(new Rect(x + 10f, y + 25f, width - 20f, 20f), $"날짜: {state.CurrentDay}일차 / 마감 {state.DeadlineDay}일차"); // 날짜와 마감 표시
            GUI.Label(new Rect(x + 10f, y + 45f, width - 20f, 20f), $"보유 골드: {state.Gold}"); // 현재 보유 골드 표시
            GUI.Label(new Rect(x + 10f, y + 65f, width - 20f, 20f), $"남은 빚: {state.RemainingDebt}"); // 현재 남은 빚 표시
            GUI.Label(new Rect(x + 10f, y + 85f, width - 20f, 20f), $"남은 기회: {state.RemainingDays}일"); // 남은 정산 기회 표시
        }
    }
}