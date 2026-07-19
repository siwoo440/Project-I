using UnityEngine; // Unity 기본 기능과 OnGUI 사용

namespace ProjectI // 프로젝트 공통 네임스페이스
{
    public class VillageSettlementUI : MonoBehaviour // 마을 보상 정산과 직접 빚 납부 인터페이스
    {
        [Header("납부 조절 단위")] // Inspector 납부 금액 조절 설정 구분
        [SerializeField][Min(1)] int smallStep = 10; // 소액 조절 단위
        [SerializeField][Min(1)] int mediumStep = 100; // 중간 조절 단위
        [SerializeField][Min(1)] int largeStep = 1000; // 큰 조절 단위

        CampaignManager campaignManager; // 골드와 빚을 관리하는 캠페인 매니저

        int selectedDebtPayment; // 플레이어가 현재 선택한 빚 납부액
        bool hasSettlementData; // 현재 표시할 정산 데이터 존재 여부
        bool settlementCompleted; // 이번 방문의 빚 납부 확정 여부
        bool windowOpen = true; // 현재 정산 창 표시 여부
        GUIStyle titleStyle; // 정산 제목 표시 스타일
        GUIStyle centerStyle; // 정산 정보 표시 스타일

        public bool IsWindowOpen => windowOpen; // 다른 마을 UI에서 정산 창 표시 상태 확인

        void Start() // 마을 도착 후 던전 보상 정산과 UI 초기화
        {
            Time.timeScale = 1f; // 던전 종료로 정지된 게임시간 복구
            Cursor.lockState = CursorLockMode.None; // 마을 UI 조작을 위해 커서 잠금 해제
            Cursor.visible = true; // 마우스 커서 표시

            campaignManager = CampaignManager.Instance; // 영구 캠페인 매니저 가져오기

            if (campaignManager == null) // 캠페인 매니저 존재 여부 확인
            {
                campaignManager = FindFirstObjectByType<CampaignManager>(); // 현재 Scene에서 캠페인 매니저 재검색
            }

            if (campaignManager == null) // 캠페인 매니저 검색 결과 확인
            {
                Debug.LogError("[VillageSettlementUI] CampaignManager가 없습니다."); // 참조 누락 오류 출력
                return; // 정산 초기화 중단
            }

            bool rewardApplied = campaignManager.ApplyCurrentRunReward(); // 던전 확보 가치를 전액 골드로 정산
            hasSettlementData = rewardApplied || campaignManager.HasOpenSettlement; // 신규 또는 진행 중 정산 여부 저장
            selectedDebtPayment = 0; // 초기 빚 납부 선택액을 0골드로 설정

            if (!hasSettlementData) // 정산 가능한 던전 결과가 없는지 확인
            {
                Debug.LogWarning("[VillageSettlementUI] 정산할 던전 결과가 없습니다."); // 결과 없음 경고 출력
            }
        }

        void AdjustPayment(int amount) // 선택한 납부 금액을 안전 범위 안에서 증감
        {
            if (campaignManager == null || settlementCompleted) // 정산 조작 가능 여부 확인
            {
                return; // 금액 조절 중단
            }

            int maximumPayment = campaignManager.MaximumDebtPayment; // 현재 최대 납부 가능액 가져오기
            selectedDebtPayment = Mathf.Clamp(selectedDebtPayment + amount, 0, maximumPayment); // 선택 금액을 납부 가능 범위로 제한
        }

        void ConfirmPayment() // 현재 선택한 빚 납부액 확정
        {
            if (campaignManager == null || settlementCompleted) // 정산 확정 가능 여부 확인
            {
                return; // 중복 확정 방지
            }

            if (campaignManager.ConfirmDebtPayment(selectedDebtPayment)) // 선택한 금액의 실제 납부 성공 여부 확인
            {
                settlementCompleted = true; // 이번 방문의 정산 완료 상태 저장
            }
        }

        void OnGUI() // 마을 정산과 빚 납부 인터페이스 표시
        {
            if (!windowOpen) // 정산 확인 후 창이 닫혔는지 확인
            {
                return; // 정산 창 표시 중단
            }

            if (campaignManager == null) // 캠페인 매니저 존재 여부 확인
            {
                GUI.Box(new Rect(20f, 20f, 380f, 80f), "CampaignManager가 없습니다."); // 참조 누락 안내 표시
                return; // 정산 UI 표시 중단
            }

            if (titleStyle == null) // GUI 스타일 초기화 여부 확인
            {
                titleStyle = new GUIStyle(GUI.skin.label); // 정산 제목 스타일 생성
                titleStyle.fontSize = 26; // 정산 제목 글자 크기 설정
                titleStyle.alignment = TextAnchor.MiddleCenter; // 정산 제목 중앙 정렬
                centerStyle = new GUIStyle(GUI.skin.label); // 정산 상세 스타일 생성
                centerStyle.fontSize = 16; // 정산 상세 글자 크기 설정
                centerStyle.alignment = TextAnchor.MiddleCenter; // 정산 상세 중앙 정렬
            }

            float width = 620f; // 정산 패널 너비
            float height = 430f; // 정산 패널 높이
            float x = (Screen.width - width) * 0.5f; // 화면 중앙 가로 위치
            float y = (Screen.height - height) * 0.5f; // 화면 중앙 세로 위치

            GUI.Box(new Rect(x, y, width, height), string.Empty); // 정산 패널 배경 표시
            GUI.Label(new Rect(x + 10f, y + 15f, width - 20f, 40f), "마을 정산소", titleStyle); // 정산소 제목 표시

            if (!hasSettlementData) // 정산 가능한 탐험 결과 존재 여부 확인
            {
                GUI.Label(new Rect(x + 20f, y + 100f, width - 40f, 30f), "정산할 던전 결과가 없습니다.", centerStyle); // 결과 없음 안내 표시

                if (GUI.Button(new Rect(x + 160f, y + 160f, width - 320f, 45f), "정산 창 닫기")) // 결과 없음 창 닫기 버튼 확인
                {
                    windowOpen = false; // 정산 창 닫기
                }

                return; // 나머지 정산 UI 표시 중단
            }

            if (settlementCompleted) // 빚 납부 확정 여부 확인
            {
                DrawCompletedSettlement(x, y, width); // 최종 정산 결과 표시
                return; // 납부 조작 UI 표시 중단
            }

            selectedDebtPayment = Mathf.Clamp(selectedDebtPayment, 0, campaignManager.MaximumDebtPayment); // 현재 선택액을 최신 납부 가능 범위로 제한

            GUI.Label(new Rect(x + 20f, y + 65f, width - 40f, 25f), $"{campaignManager.LastSettlementDay}일차 탐험 결과", centerStyle); // 정산 날짜 표시
            GUI.Label(new Rect(x + 20f, y + 95f, width - 40f, 25f), $"종료 원인: {campaignManager.LastRunReason}", centerStyle); // 탐험 종료 원인 표시
            GUI.Label(new Rect(x + 20f, y + 125f, width - 40f, 25f), $"보물 정산: +{campaignManager.LastRunReward}골드", centerStyle); // 전액 지급된 보물 가치 표시
            GUI.Label(new Rect(x + 20f, y + 155f, width - 40f, 25f), $"보유 골드: {campaignManager.State.Gold} / 남은 빚: {campaignManager.State.RemainingDebt}", centerStyle); // 골드와 빚 표시
            GUI.Label(new Rect(x + 20f, y + 190f, width - 40f, 30f), $"선택한 납부액: {selectedDebtPayment}골드", titleStyle); // 현재 선택한 납부액 표시

            if (GUI.Button(new Rect(x + 40f, y + 235f, 125f, 35f), $"-{largeStep}")) // 큰 금액 감소 버튼 확인
            {
                AdjustPayment(-largeStep); // 선택 금액 크게 감소
            }

            if (GUI.Button(new Rect(x + 175f, y + 235f, 125f, 35f), $"-{mediumStep}")) // 중간 금액 감소 버튼 확인
            {
                AdjustPayment(-mediumStep); // 선택 금액 중간 단위 감소
            }

            if (GUI.Button(new Rect(x + 310f, y + 235f, 125f, 35f), $"-{smallStep}")) // 작은 금액 감소 버튼 확인
            {
                AdjustPayment(-smallStep); // 선택 금액 작은 단위 감소
            }

            if (GUI.Button(new Rect(x + 445f, y + 235f, 125f, 35f), "0골드")) // 납부액 초기화 버튼 확인
            {
                selectedDebtPayment = 0; // 납부 없이 진행하도록 선택액 초기화
            }

            if (GUI.Button(new Rect(x + 40f, y + 280f, 125f, 35f), $"+{smallStep}")) // 작은 금액 증가 버튼 확인
            {
                AdjustPayment(smallStep); // 선택 금액 작은 단위 증가
            }

            if (GUI.Button(new Rect(x + 175f, y + 280f, 125f, 35f), $"+{mediumStep}")) // 중간 금액 증가 버튼 확인
            {
                AdjustPayment(mediumStep); // 선택 금액 중간 단위 증가
            }

            if (GUI.Button(new Rect(x + 310f, y + 280f, 125f, 35f), $"+{largeStep}")) // 큰 금액 증가 버튼 확인
            {
                AdjustPayment(largeStep); // 선택 금액 크게 증가
            }

            if (GUI.Button(new Rect(x + 445f, y + 280f, 125f, 35f), "최대")) // 최대 납부 버튼 확인
            {
                selectedDebtPayment = campaignManager.MaximumDebtPayment; // 보유 골드와 남은 빚 중 작은 값 선택
            }

            GUI.Label(new Rect(x + 20f, y + 325f, width - 40f, 20f), $"최대 납부 가능액: {campaignManager.MaximumDebtPayment}골드", centerStyle); // 최대 납부 가능 금액 표시

            if (GUI.Button(new Rect(x + 120f, y + 360f, width - 240f, 45f), $"{selectedDebtPayment}골드 납부 확정")) // 최종 납부 확정 버튼 확인
            {
                ConfirmPayment(); // 선택한 금액으로 빚 납부 확정
            }
        }

        void DrawCompletedSettlement(float x, float y, float width) // 확정된 마을 정산 결과 표시
        {
            string campaignResult = "다음 탐험 준비"; // 기본 캠페인 진행 문구

            if (campaignManager.State.CampaignWon) // 빚 상환 완료 여부 확인
            {
                campaignResult = "빚을 모두 상환했습니다!"; // 캠페인 성공 문구 적용
            }
            else if (campaignManager.State.CampaignFailed) // 상환 기한 초과 여부 확인
            {
                campaignResult = "상환 기한이 끝났습니다."; // 캠페인 실패 문구 적용
            }

            GUI.Label(new Rect(x + 20f, y + 90f, width - 40f, 40f), "정산 완료", titleStyle); // 정산 완료 제목 표시
            GUI.Label(new Rect(x + 20f, y + 145f, width - 40f, 25f), $"보물 판매: +{campaignManager.LastRunReward}골드", centerStyle); // 보물 판매 결과 표시
            GUI.Label(new Rect(x + 20f, y + 180f, width - 40f, 25f), $"빚 납부: -{campaignManager.LastDebtPayment}골드", centerStyle); // 빚 납부 결과 표시
            GUI.Label(new Rect(x + 20f, y + 215f, width - 40f, 25f), $"현재 보유 골드: {campaignManager.State.Gold}", centerStyle); // 정산 후 보유 골드 표시
            GUI.Label(new Rect(x + 20f, y + 250f, width - 40f, 25f), $"남은 빚: {campaignManager.State.RemainingDebt}", centerStyle); // 정산 후 남은 빚 표시
            GUI.Label(new Rect(x + 20f, y + 285f, width - 40f, 25f), $"다음 날짜: {campaignManager.State.CurrentDay}일차", centerStyle); // 다음 날짜 표시
            GUI.Label(new Rect(x + 20f, y + 335f, width - 40f, 35f), campaignResult, titleStyle); // 캠페인 진행 결과 표시
            if (GUI.Button(new Rect(x + 150f, y + 375f, width - 300f, 40f), "정산 확인")) // 정산 결과 확인 버튼 입력
            {
                windowOpen = false; // 정산 창을 닫고 하루 안내 화면 표시 허용
            }
        }
    }
}