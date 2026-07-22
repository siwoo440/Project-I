using UnityEngine; // Unity 기본 기능과 OnGUI 사용
using UnityEngine.InputSystem; // F5 저수준 키 입력 사용
using UnityEngine.SceneManagement; // 다음 Dungeon Scene 이동 사용

namespace ProjectI // 프로젝트 공통 네임스페이스
{
    public class VillageCampaignFlowUI : MonoBehaviour // 마을 날짜 안내와 다음 던전 출발 및 캠페인 결과 담당
    {
        [Header("Scene 설정")] // Inspector Scene 설정 구분
        [Tooltip("다음 탐험에 사용할 Dungeon Scene 이름")] [SerializeField] string dungeonSceneName = "Dungeon"; // 다음 탐험에 사용할 Dungeon Scene 이름

        [Header("임시 화면")] // Inspector OnGUI 설정 구분
        [Tooltip("날짜와 출발 안내 패널 표시 여부")] [SerializeField] bool showFlowPanel = true; // 날짜와 출발 안내 패널 표시 여부
        bool selectionUnlocked; // 마차 도착 후 던전 선택 허용 여부

        CampaignManager campaignManager; // 캠페인 날짜와 빚 상태 관리 매니저
        VillageSettlementUI settlementUI; // 정산 창 표시 상태 확인용 UI

        GUIStyle titleStyle; // 마을 진행 제목 스타일
        GUIStyle centerStyle; // 마을 진행 상세 스타일
        GUIStyle warningStyle; // 마지막 날과 마감 경고 스타일
        void Awake() // 마을 시작 시 던전 선택 창 잠금
        {
            selectionUnlocked = false; // 마차가 목적지에 도착하기 전까지 선택 차단
        }
        public void LockSelection() // 마차 출발 전 던전 선택 창 잠금
        {
            selectionUnlocked = false; // 선택 입력과 화면 표시 차단
        }

        public void UnlockSelection() // 마차 도착 후 던전 선택 창 활성화
        {
            selectionUnlocked = true; // 선택 입력과 화면 표시 허용
            Cursor.lockState = CursorLockMode.None; // 던전 선택 버튼 조작을 위해 커서 잠금 해제
            Cursor.visible = true; // 마우스 커서 표시
            Debug.Log("[DungeonSelection] 마차 도착으로 던전 선택이 활성화됐습니다."); // 선택 활성화 결과 출력
        }


        void Start() // 마을 캠페인 진행 참조와 커서 상태 초기화
        {
            Time.timeScale = 1f; // 마을에서 정상적으로 입력을 처리하도록 시간 복구
            Cursor.lockState = CursorLockMode.None; // 마을 UI 조작을 위해 커서 잠금 해제
            Cursor.visible = true; // 마우스 커서 표시

            campaignManager = CampaignManager.Instance; // 영구 캠페인 매니저 가져오기

            if (campaignManager == null) // 캠페인 매니저 존재 여부 확인
            {
                campaignManager = FindFirstObjectByType<CampaignManager>(); // 현재 Scene에서 캠페인 매니저 검색
            }

            settlementUI = FindFirstObjectByType<VillageSettlementUI>(); // 현재 마을의 정산 UI 검색

            if (campaignManager == null) // 캠페인 매니저 검색 결과 확인
            {
                Debug.LogError("[VillageCampaignFlowUI] CampaignManager가 없습니다."); // 캠페인 매니저 누락 오류 출력
            }
        }

        void Update() // 던전 선택 키 입력 처리
        {
            if (!selectionUnlocked) // 마차가 목적지에 도착했는지 확인
            {
                return; // 도착 전 숫자 키와 F5 입력 차단
            }
            Keyboard keyboard = Keyboard.current; // 현재 키보드 입력 가져오기

            if (keyboard != null && keyboard.f5Key.wasPressedThisFrame) // F5 입력 여부 확인
            {
                TryDepartDungeon(); // 다음 던전 출발 시도
            }
        }

        public void TryDepartDungeon() // 정산과 캠페인 상태를 확인한 뒤 Dungeon Scene 이동
        {
            if (campaignManager == null) // 캠페인 매니저 존재 여부 확인
            {
                Debug.LogError("[VillageFlow] CampaignManager가 없어 출발할 수 없습니다."); // 출발 불가 오류 출력
                return; // 던전 출발 중단
            }

            if (settlementUI != null && settlementUI.IsWindowOpen) // 정산 창이 아직 열려 있는지 확인
            {
                Debug.LogWarning("[VillageFlow] 마을 정산을 먼저 확인해야 합니다."); // 정산 미완료 경고 출력
                return; // 정산 전 출발 차단
            }

            if (!campaignManager.CanStartNextRun) // 현재 캠페인 상태에서 출발 가능한지 확인
            {
                Debug.LogWarning($"[VillageFlow] 출발할 수 없습니다 — {campaignManager.GetDeadlineMessage()}"); // 캠페인 상태에 맞는 출발 차단 사유 출력
                return; // 던전 출발 중단
            }

            if (string.IsNullOrWhiteSpace(dungeonSceneName)) // Dungeon Scene 이름 입력 여부 확인
            {
                Debug.LogError("[VillageFlow] Dungeon Scene 이름이 비어 있습니다."); // Scene 이름 누락 오류 출력
                return; // Dungeon Scene 이동 중단
            }

            if (!Application.CanStreamedLevelBeLoaded(dungeonSceneName)) // Build Profile의 Dungeon Scene 등록 여부 확인
            {
                Debug.LogError($"[VillageFlow] Build Profile에서 {dungeonSceneName} Scene을 찾을 수 없습니다."); // Scene 등록 누락 오류 출력
                return; // Dungeon Scene 이동 중단
            }

            Time.timeScale = 1f; // Scene 이동 전 게임시간 정상화
            Debug.Log($"[VillageFlow] {campaignManager.State.CurrentDay}일차 던전으로 출발합니다."); // 출발 날짜 출력
            SceneManager.LoadScene(dungeonSceneName); // Dungeon Scene 로드
        }

        void OnGUI() // 날짜와 마감 및 출발과 캠페인 결과 화면 표시
        {
            if (!selectionUnlocked) // 마차가 목적지에 도착했는지 확인
            {
                return; // 도착 전 던전 선택 화면 숨김
            }

            if (!showFlowPanel) // 마을 진행 패널 표시 여부 확인
            {
                return; // 마을 진행 패널 표시 중단
            }

            if (settlementUI != null && settlementUI.IsWindowOpen) // 정산 창이 화면에 표시 중인지 확인
            {
                return; // 정산 UI와 겹치지 않도록 진행 패널 숨김
            }

            if (campaignManager == null) // 캠페인 매니저 존재 여부 확인
            {
                GUI.Box(new Rect(20f, 20f, 420f, 80f), "CampaignManager가 없습니다."); // 참조 누락 화면 표시
                return; // 마을 진행 패널 표시 중단
            }

            InitializeStyles(); // OnGUI 표시 스타일 초기화

            float width = 620f; // 마을 진행 패널 너비
            float height = 350f; // 마을 진행 패널 높이
            float x = (Screen.width - width) * 0.5f; // 화면 중앙 가로 위치
            float y = (Screen.height - height) * 0.5f; // 화면 중앙 세로 위치

            GUI.Box(new Rect(x, y, width, height), string.Empty); // 마을 진행 패널 배경 표시

            if (campaignManager.State.CampaignWon) // 빚 전액 상환 성공 여부 확인
            {
                DrawCampaignWon(x, y, width); // 캠페인 성공 화면 표시
                return; // 일반 출발 안내 표시 중단
            }

            if (campaignManager.State.CampaignFailed) // 상환 기한 초과 여부 확인
            {
                DrawCampaignFailed(x, y, width); // 캠페인 실패 화면 표시
                return; // 일반 출발 안내 표시 중단
            }

            DrawActiveCampaign(x, y, width); // 진행 중인 캠페인의 날짜와 출발 안내 표시
        }

        void InitializeStyles() // OnGUI 스타일을 한 번만 생성
        {
            if (titleStyle != null) // 스타일 생성 완료 여부 확인
            {
                return; // 중복 스타일 생성 방지
            }

            titleStyle = new GUIStyle(GUI.skin.label); // 제목 스타일 생성
            titleStyle.fontSize = 28; // 제목 글자 크기 설정
            titleStyle.alignment = TextAnchor.MiddleCenter; // 제목 중앙 정렬

            centerStyle = new GUIStyle(GUI.skin.label); // 상세 정보 스타일 생성
            centerStyle.fontSize = 17; // 상세 정보 글자 크기 설정
            centerStyle.alignment = TextAnchor.MiddleCenter; // 상세 정보 중앙 정렬

            warningStyle = new GUIStyle(GUI.skin.label); // 마감 경고 스타일 생성
            warningStyle.fontSize = 19; // 마감 경고 글자 크기 설정
            warningStyle.alignment = TextAnchor.MiddleCenter; // 마감 경고 중앙 정렬
            warningStyle.normal.textColor = new Color(1f, 0.55f, 0.25f); // 경고 문구를 주황색으로 설정
        }

        void DrawActiveCampaign(float x, float y, float width) // 진행 중인 캠페인의 날짜와 출발 안내 표시
        {
            string title = campaignManager.IsLastAvailableDay // 마지막 출발 기회 여부 확인
                ? "마지막 날" // 마지막 날 제목
                : $"{campaignManager.State.CurrentDay}일차"; // 일반 날짜 제목

            GUI.Label(new Rect(x + 10f, y + 20f, width - 20f, 45f), title, titleStyle); // 현재 날짜 제목 표시
            GUI.Label(new Rect(x + 20f, y + 80f, width - 40f, 25f), $"보유 골드: {campaignManager.State.Gold}", centerStyle); // 보유 골드 표시
            GUI.Label(new Rect(x + 20f, y + 115f, width - 40f, 25f), $"남은 빚: {campaignManager.State.RemainingDebt}", centerStyle); // 남은 빚 표시
            GUI.Label(new Rect(x + 20f, y + 150f, width - 40f, 25f), $"상환 마감: {campaignManager.State.DeadlineDay}일차", centerStyle); // 상환 마감 날짜 표시
            GUI.Label(new Rect(x + 20f, y + 190f, width - 40f, 35f), campaignManager.GetDeadlineMessage(), warningStyle); // 현재 마감 안내 표시

            if (GUI.Button(new Rect(x + 130f, y + 255f, width - 260f, 50f), "[F5] 다음 던전으로 출발")) // 출발 버튼 입력 확인
            {
                TryDepartDungeon(); // Dungeon Scene 이동 시도
            }

            GUI.Label(new Rect(x + 20f, y + 315f, width - 40f, 20f), "정산을 완료한 뒤에만 출발할 수 있습니다.", centerStyle); // 출발 조건 안내 표시
        }

        void DrawCampaignWon(float x, float y, float width) // 빚 전액 상환 성공 화면 표시
        {
            GUI.Label(new Rect(x + 10f, y + 35f, width - 20f, 50f), "빚 상환 완료", titleStyle); // 캠페인 성공 제목 표시
            GUI.Label(new Rect(x + 20f, y + 115f, width - 40f, 30f), "기한 안에 모든 빚을 갚았습니다.", centerStyle); // 성공 이유 표시
            GUI.Label(new Rect(x + 20f, y + 160f, width - 40f, 30f), $"완료한 탐험: {campaignManager.State.CompletedRuns}회", centerStyle); // 완료 탐험 횟수 표시
            GUI.Label(new Rect(x + 20f, y + 205f, width - 40f, 30f), $"남은 골드: {campaignManager.State.Gold}", centerStyle); // 최종 남은 골드 표시
            GUI.Label(new Rect(x + 20f, y + 270f, width - 40f, 30f), "캠페인이 종료되어 추가 던전 출발이 차단됩니다.", warningStyle); // 추가 출발 차단 안내 표시
        }

        void DrawCampaignFailed(float x, float y, float width) // 상환 기한 초과 실패 화면 표시
        {
            GUI.Label(new Rect(x + 10f, y + 35f, width - 20f, 50f), "상환 기한 종료", titleStyle); // 캠페인 실패 제목 표시
            GUI.Label(new Rect(x + 20f, y + 115f, width - 40f, 30f), "기한 안에 빚을 모두 갚지 못했습니다.", centerStyle); // 실패 이유 표시
            GUI.Label(new Rect(x + 20f, y + 160f, width - 40f, 30f), $"남은 빚: {campaignManager.State.RemainingDebt}", centerStyle); // 최종 남은 빚 표시
            GUI.Label(new Rect(x + 20f, y + 205f, width - 40f, 30f), $"완료한 탐험: {campaignManager.State.CompletedRuns}회", centerStyle); // 완료 탐험 횟수 표시
            GUI.Label(new Rect(x + 20f, y + 270f, width - 40f, 30f), "캠페인이 종료되어 추가 던전 출발이 차단됩니다.", warningStyle); // 추가 출발 차단 안내 표시
        }
    }
}