using UnityEngine; // Unity 기본 기능과 OnGUI 사용
using UnityEngine.InputSystem; // 숫자 키와 F5 저수준 입력 사용

namespace ProjectI // 프로젝트 공통 네임스페이스
{
    public class VillageDungeonSelectionUI : MonoBehaviour // 마을 마차의 던전 경로 선택과 출발 인터페이스
    {
        [Header("임시 선택 화면")] // Inspector 선택 화면 설정 구분
        [SerializeField] bool showSelectionPanel = true; // 던전 선택 패널 표시 여부

        DungeonSelectionManager selectionManager; // 던전 경로 선택과 Scene 이동 매니저
        CampaignManager campaignManager; // 캠페인 종료와 출발 가능 상태 확인
        VillageSettlementUI settlementUI; // 마을 정산 창 표시 상태 확인

        GUIStyle titleStyle; // 던전 선택 제목 스타일
        GUIStyle centerStyle; // 던전 선택 상세 스타일
        GUIStyle warningStyle; // 캠페인 경고 스타일

        bool selectionUnlocked; // 마차 도착 후 던전 선택 허용 여부

        public bool IsSelectionUnlocked => selectionUnlocked; // 현재 던전 선택 허용 상태 반환

        public void LockSelection() // 마차 출발 전 던전 선택 화면 잠금
        {
            selectionUnlocked = false; // 선택 화면과 키 입력 차단
        }

        public void UnlockSelection() // 마차 도착 후 던전 선택 화면 활성화
        {
            selectionUnlocked = true; // 선택 화면과 키 입력 허용
            Cursor.lockState = CursorLockMode.None; // 마우스 버튼 조작을 위해 커서 잠금 해제
            Cursor.visible = true; // 마우스 커서 표시
            Debug.Log("[DungeonSelection] 마차 도착으로 던전 선택이 활성화됐습니다."); // 활성화 결과 출력
        }
        void Awake() // 마을 시작 시 선택 화면 초기 잠금
        {
            selectionUnlocked = false; // 마차 도착 전 선택 차단
        }
        void Start() // 던전 선택 관련 참조와 마우스 커서 초기화
        {
            Time.timeScale = 1f; // 마을에서 정상적으로 입력을 처리하도록 시간 복구

            selectionManager = DungeonSelectionManager.Instance; // 영구 던전 선택 매니저 가져오기

            if (selectionManager == null) // 던전 선택 매니저 존재 여부 확인
            {
                selectionManager = FindFirstObjectByType<DungeonSelectionManager>(); // 현재 Scene에서 던전 선택 매니저 검색
            }

            campaignManager = CampaignManager.Instance; // 영구 캠페인 매니저 가져오기

            if (campaignManager == null) // 캠페인 매니저 존재 여부 확인
            {
                campaignManager = FindFirstObjectByType<CampaignManager>(); // 현재 Scene에서 캠페인 매니저 검색
            }

            settlementUI = FindFirstObjectByType<VillageSettlementUI>(); // 마을 정산 UI 검색

            if (selectionManager == null) // 던전 선택 매니저 검색 결과 확인
            {
                Debug.LogError("[VillageDungeonSelectionUI] DungeonSelectionManager가 없습니다."); // 던전 선택 매니저 누락 오류 출력
            }

            if (campaignManager == null) // 캠페인 매니저 검색 결과 확인
            {
                Debug.LogError("[VillageDungeonSelectionUI] CampaignManager가 없습니다."); // 캠페인 매니저 누락 오류 출력
            }
        }

        void Update() // 숫자 키 경로 선택과 F5 출발 입력 처리
        {
            if (!selectionUnlocked) // 마차 도착 여부 확인
            {
                return; // 도착 전 선택 키 입력 차단
            }

            if (settlementUI != null && settlementUI.IsWindowOpen) // 정산 창 표시 여부 확인
            {
                return; // 정산 중 던전 선택 입력 차단
            }

            Keyboard keyboard = Keyboard.current; // 현재 키보드 입력 가져오기

            if (keyboard == null || selectionManager == null) // 입력과 선택 매니저 존재 여부 확인
            {
                return; // 던전 선택 입력 처리 중단
            }

            if (keyboard.digit1Key.wasPressedThisFrame) // 숫자 1번 입력 확인
            {
                selectionManager.SelectRoute(0); // 첫 번째 던전 경로 선택
            }

            if (keyboard.digit2Key.wasPressedThisFrame) // 숫자 2번 입력 확인
            {
                selectionManager.SelectRoute(1); // 두 번째 던전 경로 선택
            }

            if (keyboard.digit3Key.wasPressedThisFrame) // 숫자 3번 입력 확인
            {
                selectionManager.SelectRoute(2); // 세 번째 던전 경로 선택
            }

            if (keyboard.f5Key.wasPressedThisFrame) // F5 출발 입력 확인
            {
                TryDepart(); // 선택한 던전으로 출발 시도
            }
        }

        void TryDepart() // 정산 창과 던전 선택 상태를 확인한 뒤 출발
        {
            if (settlementUI != null && settlementUI.IsWindowOpen) // 마을 정산 창이 열려 있는지 확인
            {
                Debug.LogWarning("[VillageDungeonSelectionUI] 마을 정산을 먼저 완료해야 합니다."); // 정산 미완료 경고 출력
                return; // 던전 출발 중단
            }

            if (selectionManager == null) // 던전 선택 매니저 존재 여부 확인
            {
                return; // 던전 출발 중단
            }

            selectionManager.LoadSelectedDungeon(); // 선택한 던전 Scene 이동 실행
        }

        void OnGUI() // 던전 경로 선택과 출발 및 캠페인 종료 화면 표시
        {
            if (!selectionUnlocked) // 마차 도착 여부 확인
            {
                return; // 도착 전 선택 화면 전체 숨김
            }

            if (!showSelectionPanel) // 던전 선택 패널 표시 여부 확인
            {
                return; // 던전 선택 패널 표시 중단
            }

            if (settlementUI != null && settlementUI.IsWindowOpen) // 마을 정산 창 표시 여부 확인
            {
                return; // 정산 UI와 겹치지 않도록 선택 패널 숨김
            }

            if (selectionManager == null || campaignManager == null) // 필수 매니저 존재 여부 확인
            {
                GUI.Box(new Rect(20f, 20f, 480f, 80f), "던전 선택 매니저 또는 캠페인 매니저가 없습니다."); // 참조 누락 안내 표시
                return; // 던전 선택 패널 표시 중단
            }

            InitializeStyles(); // OnGUI 스타일 초기화

            float width = 900f; // 던전 선택 패널 너비
            float height = 500f; // 던전 선택 패널 높이
            float x = (Screen.width - width) * 0.5f; // 화면 중앙 가로 위치
            float y = (Screen.height - height) * 0.5f; // 화면 중앙 세로 위치

            GUI.Box(new Rect(x, y, width, height), string.Empty); // 던전 선택 패널 배경 표시

            if (campaignManager.State.CampaignWon) // 빚 전액 상환 성공 여부 확인
            {
                DrawCampaignEnd(x, y, width, "빚 상환 완료", "모든 빚을 갚아 더 이상 던전에 갈 필요가 없습니다."); // 캠페인 성공 화면 표시
                return; // 던전 선택 화면 표시 중단
            }

            if (campaignManager.State.CampaignFailed) // 상환 기한 초과 여부 확인
            {
                DrawCampaignEnd(x, y, width, "상환 기한 종료", "마감일까지 빚을 갚지 못해 출발할 수 없습니다."); // 캠페인 실패 화면 표시
                return; // 던전 선택 화면 표시 중단
            }

            DrawRouteSelection(x, y, width); // 진행 중인 캠페인의 던전 선택 화면 표시
        }

        void InitializeStyles() // OnGUI 스타일을 한 번만 생성
        {
            if (titleStyle != null) // 스타일 생성 완료 여부 확인
            {
                return; // 중복 스타일 생성 방지
            }

            titleStyle = new GUIStyle(GUI.skin.label); // 제목 스타일 생성
            titleStyle.fontSize = 27; // 제목 글자 크기 설정
            titleStyle.alignment = TextAnchor.MiddleCenter; // 제목 중앙 정렬

            centerStyle = new GUIStyle(GUI.skin.label); // 상세 스타일 생성
            centerStyle.fontSize = 16; // 상세 글자 크기 설정
            centerStyle.alignment = TextAnchor.MiddleCenter; // 상세 문구 중앙 정렬
            centerStyle.wordWrap = true; // 긴 던전 설명 자동 줄바꿈

            warningStyle = new GUIStyle(GUI.skin.label); // 경고 스타일 생성
            warningStyle.fontSize = 18; // 경고 글자 크기 설정
            warningStyle.alignment = TextAnchor.MiddleCenter; // 경고 문구 중앙 정렬
            warningStyle.normal.textColor = new Color(1f, 0.55f, 0.25f); // 경고 문구를 주황색으로 설정
        }

        void DrawRouteSelection(float x, float y, float width) // 선택 가능한 던전 경로 목록 표시
        {
            GUI.Label(new Rect(x + 10f, y + 15f, width - 20f, 45f), "마차 — 다음 목적지 선택", titleStyle); // 던전 선택 제목 표시
            GUI.Label(new Rect(x + 20f, y + 60f, width - 40f, 25f), $"{campaignManager.State.CurrentDay}일차 / 남은 빚 {campaignManager.State.RemainingDebt}골드", centerStyle); // 현재 날짜와 빚 표시

            float cardWidth = 260f; // 경로 카드 너비
            float cardHeight = 290f; // 경로 카드 높이
            float cardStartX = x + 40f; // 첫 경로 카드 가로 위치
            float cardY = y + 105f; // 경로 카드 세로 위치

            for (int i = 0; i < selectionManager.RouteCount && i < 3; i++) // 최대 세 개 던전 경로 순회
            {
                DungeonRouteData route = selectionManager.GetRoute(i); // 현재 번호의 던전 경로 가져오기

                if (route == null) // 던전 경로 데이터 존재 여부 확인
                {
                    continue; // 비어 있는 경로 건너뜀
                }

                float cardX = cardStartX + i * 280f; // 현재 경로 카드 가로 위치 계산
                bool selected = route == selectionManager.SelectedRoute; // 현재 경로 선택 여부 확인
                string selectedText = selected ? "선택됨" : $"[{i + 1}] 선택"; // 선택 상태 버튼 문구 계산

                GUI.Box(new Rect(cardX, cardY, cardWidth, cardHeight), string.Empty); // 던전 경로 카드 배경 표시
                GUI.Label(new Rect(cardX + 10f, cardY + 15f, cardWidth - 20f, 35f), route.DisplayName, titleStyle); // 던전 이름 표시
                GUI.Label(new Rect(cardX + 10f, cardY + 60f, cardWidth - 20f, 60f), route.Description, centerStyle); // 던전 설명 표시
                GUI.Label(new Rect(cardX + 10f, cardY + 125f, cardWidth - 20f, 22f), $"위험도: {route.DangerLevel}/5", centerStyle); // 던전 위험도 표시
                GUI.Label(new Rect(cardX + 10f, cardY + 150f, cardWidth - 20f, 22f), $"방: {route.RoomCount}개", centerStyle); // 방 개수 표시
                GUI.Label(new Rect(cardX + 10f, cardY + 175f, cardWidth - 20f, 22f), $"보물 가치: {route.RewardValueMultiplier:F2}배", centerStyle); // 보물 가치 배율 표시
                GUI.Label(new Rect(cardX + 10f, cardY + 200f, cardWidth - 20f, 22f), $"몬스터: {route.MonsterSpawnMultiplier:F2}배", centerStyle); // 몬스터 생성 배율 표시
                GUI.Label(new Rect(cardX + 10f, cardY + 225f, cardWidth - 20f, 22f), $"함정: {route.TrapSpawnMultiplier:F2}배", centerStyle); // 함정 생성 배율 표시

                if (GUI.Button(new Rect(cardX + 30f, cardY + 250f, cardWidth - 60f, 30f), selectedText)) // 던전 경로 선택 버튼 입력 확인
                {
                    selectionManager.SelectRoute(i); // 현재 카드의 던전 경로 선택
                }
            }

            DungeonRouteData selectedRoute = selectionManager.SelectedRoute; // 현재 선택한 던전 경로 가져오기
            string departText = selectedRoute != null ? $"[F5] {selectedRoute.DisplayName}으로 출발" : "[F5] 출발"; // 선택 경로를 포함한 출발 문구 계산

            if (GUI.Button(new Rect(x + 250f, y + 420f, width - 500f, 50f), departText)) // 선택한 던전 출발 버튼 입력 확인
            {
                TryDepart(); // 선택한 던전으로 출발 시도
            }
        }

        void DrawCampaignEnd(float x, float y, float width, string title, string message) // 캠페인 종료로 인한 출발 차단 화면 표시
        {
            GUI.Label(new Rect(x + 10f, y + 80f, width - 20f, 50f), title, titleStyle); // 캠페인 종료 제목 표시
            GUI.Label(new Rect(x + 40f, y + 165f, width - 80f, 50f), message, centerStyle); // 캠페인 종료 이유 표시
            GUI.Label(new Rect(x + 40f, y + 250f, width - 80f, 35f), "던전 출발이 차단되었습니다.", warningStyle); // 추가 출발 차단 안내 표시
        }
    }
}