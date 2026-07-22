using System.Collections; // 지연 검증 코루틴 기능
using System.Collections.Generic; // 검증 결과 목록 기능
using UnityEngine; // Unity 기본 기능
using UnityEngine.InputSystem; // F9·F10 키 입력 기능
using UnityEngine.SceneManagement; // Scene 전환 감지 기능

namespace ProjectI // 프로젝트 공통 네임스페이스
{
    public class RegressionFlowValidator : MonoBehaviour // 전체 게임 흐름 회귀 검증 도구
    {
        static RegressionFlowValidator instance; // 중복 검증 도구 방지용 인스턴스

        [Header("Scene 이름")] // Inspector Scene 이름 구분
        [Tooltip("메인 메뉴 Scene 이름")][SerializeField] string mainMenuSceneName = "MainMenu"; // 메인 메뉴 Scene 이름
        [Tooltip("정산을 진행하는 마을 Scene 이름")][SerializeField] string villageSceneName = "Village"; // 마을 Scene 이름
        [Tooltip("현재 검증할 대표 던전 Scene 이름")][SerializeField] string dungeonSceneName = "Dungeon_Catacombs"; // 대표 던전 Scene 이름

        [Header("패널 설정")] // Inspector 검증 패널 구분
        [Tooltip("게임 시작 시 회귀 검증 패널 표시 여부")][SerializeField] bool showPanel = true; // 검증 패널 표시 여부
        [Tooltip("Scene 로드 후 검증 전 대기할 프레임 수")][SerializeField][Min(1)] int validationWaitFrames = 2; // Scene 초기화 대기 프레임 수

        readonly List<string> currentErrors = new List<string>(); // 현재 Scene 검증 오류 목록
        readonly List<string> currentWarnings = new List<string>(); // 현재 Scene 검증 경고 목록
        readonly List<string> sessionHistory = new List<string>(); // 전체 회귀 검사 기록
        readonly List<string> sessionErrors = new List<string>(); // 실행 중 Console 오류 기록

        string currentSceneName = string.Empty; // 현재 검증한 Scene 이름

        void Awake() // 회귀 검증 도구 초기화
        {
            if (instance != null && instance != this) // 기존 검증 도구 존재 여부 확인
            {
                Destroy(gameObject); // 중복 검증 도구 제거
                return; // 중복 초기화 중단
            }

            instance = this; // 현재 검증 도구 인스턴스 등록
            transform.SetParent(null); // 영구 오브젝트 적용용 루트 이동
            DontDestroyOnLoad(gameObject); // Scene 전환 후 검증 도구 유지
        }

        void OnEnable() // Scene과 Console 이벤트 연결
        {
            SceneManager.sceneLoaded += HandleSceneLoaded; // Scene 로드 이벤트 연결
            Application.logMessageReceived += HandleLogMessage; // Console 메시지 이벤트 연결
        }

        void Start() // 첫 Scene 검증 시작
        {
            StartCoroutine(ValidateAfterSceneLoad()); // 초기 Scene 지연 검증 실행
        }

        void OnDisable() // Scene과 Console 이벤트 해제
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded; // Scene 로드 이벤트 해제
            Application.logMessageReceived -= HandleLogMessage; // Console 메시지 이벤트 해제
        }

        void OnDestroy() // 인스턴스 참조 정리
        {
            if (instance == this) // 현재 등록된 검증 도구 여부 확인
            {
                instance = null; // 정적 인스턴스 초기화
            }
        }

        void Update() // 검증 단축키 입력 처리
        {
            Keyboard keyboard = Keyboard.current; // 현재 키보드 입력 가져오기

            if (keyboard == null) // 키보드 연결 여부 확인
            {
                return; // 키보드 입력 처리 중단
            }

            if (keyboard.f9Key.wasPressedThisFrame) // F9 입력 여부 확인
            {
                showPanel = !showPanel; // 검증 패널 표시 상태 전환
            }

            if (keyboard.f10Key.wasPressedThisFrame) // F10 입력 여부 확인
            {
                ValidateCurrentScene(); // 현재 Scene 즉시 재검증
            }
        }

        void HandleSceneLoaded(Scene scene, LoadSceneMode loadMode) // 새로운 Scene 로드 처리
        {
            StartCoroutine(ValidateAfterSceneLoad()); // Scene 초기화 후 지연 검증 실행
        }

        IEnumerator ValidateAfterSceneLoad() // Scene 관리자 초기화 대기
        {
            int safeWaitFrames = Mathf.Max(1, validationWaitFrames); // 최소 대기 프레임 보정

            for (int frame = 0; frame < safeWaitFrames; frame++) // 지정된 프레임 수만큼 반복
            {
                yield return null; // 다음 프레임까지 대기
            }

            ValidateCurrentScene(); // 현재 Scene 전체 검증 실행
        }

        void HandleLogMessage(string condition, string stackTrace, LogType type) // Console 오류 메시지 수집
        {
            bool isFailure = type == LogType.Error || type == LogType.Exception || type == LogType.Assert; // 실패 메시지 여부 계산

            if (!isFailure) // 오류가 아닌 메시지인지 확인
            {
                return; // 일반 로그 수집 중단
            }

            if (condition.StartsWith("[Regression]")) // 검증 도구 자체 메시지 여부 확인
            {
                return; // 자체 메시지 중복 수집 방지
            }

            string issue = $"{type}: {condition}"; // 기록할 오류 문구 생성

            if (sessionErrors.Contains(issue)) // 동일 오류 기록 여부 확인
            {
                return; // 중복 오류 기록 중단
            }

            sessionErrors.Add(issue); // 새로운 Console 오류 기록

            if (sessionErrors.Count > 20) // 최대 기록 개수 초과 여부 확인
            {
                sessionErrors.RemoveAt(0); // 가장 오래된 오류 제거
            }
        }

        public void ValidateCurrentScene() // 현재 Scene의 핵심 흐름 검증
        {
            currentErrors.Clear(); // 이전 Scene 오류 초기화
            currentWarnings.Clear(); // 이전 Scene 경고 초기화
            currentSceneName = SceneManager.GetActiveScene().name; // 현재 Scene 이름 저장

            ValidateBuildScenes(); // Build Profile Scene 등록 검사

            if (currentSceneName == mainMenuSceneName) // 메인 메뉴 Scene 여부 확인
            {
                ValidateMainMenu(); // 메인 메뉴 구성 검사
            }
            else if (currentSceneName == villageSceneName) // 마을 Scene 여부 확인
            {
                ValidateVillage(); // 마을과 정산 구성 검사
            }
            else if (currentSceneName.StartsWith("Dungeon")) // 던전 Scene 여부 확인
            {
                ValidateDungeon(); // 던전 구성 검사
            }
            else // 등록하지 않은 Scene인 경우
            {
                currentWarnings.Add($"검증 규칙이 없는 Scene: {currentSceneName}"); // 미등록 Scene 경고 추가
            }

            RecordValidationResult(); // 현재 검증 결과 기록
        }

        void ValidateBuildScenes() // 필수 Scene의 Build Profile 등록 검사
        {
            CheckSceneLoadable(mainMenuSceneName); // 메인 메뉴 Scene 등록 검사
            CheckSceneLoadable(villageSceneName); // 마을 Scene 등록 검사
            CheckSceneLoadable(dungeonSceneName); // 대표 던전 Scene 등록 검사
        }

        void ValidateMainMenu() // 메인 메뉴 필수 구성 검사
        {
            CheckRequired(FindFirstObjectByType<MainMenuUI>(), "MainMenuUI"); // 메인 메뉴 UI 존재 검사
            CheckRequired(CampaignSaveManager.Instance, "CampaignSaveManager"); // 저장 관리자 존재 검사
            CheckDuplicate<CampaignSaveManager>("CampaignSaveManager"); // 저장 관리자 중복 검사

            if (!Mathf.Approximately(Time.timeScale, 1f)) // 메인 메뉴 시간 배율 검사
            {
                currentErrors.Add($"MainMenu Time Scale 비정상: {Time.timeScale}"); // 시간 배율 오류 추가
            }
        }

        void ValidateVillage() // 마을과 정산 필수 구성 검사
        {
            CheckRequired(CampaignManager.Instance, "CampaignManager"); // 캠페인 관리자 존재 검사
            CheckRequired(CampaignSaveManager.Instance, "CampaignSaveManager"); // 저장 관리자 존재 검사
            CheckRequired(VillageShopManager.Instance, "VillageShopManager"); // 상점 관리자 존재 검사
            CheckRequired(DungeonSelectionManager.Instance, "DungeonSelectionManager"); // 던전 선택 관리자 존재 검사
            CheckRequired(RunResultManager.Instance, "RunResultManager"); // 원정 결과 관리자 존재 검사
            CheckRequired(FindFirstObjectByType<VillageSettlementUI>(), "VillageSettlementUI"); // 정산 UI 존재 검사
            CheckRequired(FindFirstObjectByType<VillageCampaignFlowUI>(), "VillageCampaignFlowUI"); // 캠페인 흐름 UI 존재 검사
            CheckRequired(FindFirstObjectByType<VillageDungeonSelectionUI>(), "VillageDungeonSelectionUI"); // 던전 선택 UI 존재 검사

            CheckDuplicate<CampaignManager>("CampaignManager"); // 캠페인 관리자 중복 검사
            CheckDuplicate<CampaignSaveManager>("CampaignSaveManager"); // 저장 관리자 중복 검사
            CheckDuplicate<VillageShopManager>("VillageShopManager"); // 상점 관리자 중복 검사
            CheckDuplicate<DungeonSelectionManager>("DungeonSelectionManager"); // 던전 선택 관리자 중복 검사
            CheckDuplicate<RunResultManager>("RunResultManager"); // 결과 관리자 중복 검사

            if (!Mathf.Approximately(Time.timeScale, 1f)) // 마을 시간 배율 검사
            {
                currentErrors.Add($"Village Time Scale 비정상: {Time.timeScale}"); // 시간 배율 오류 추가
            }
        }

        void ValidateDungeon() // 던전 필수 구성 검사
        {
            CheckRequired(CampaignManager.Instance, "CampaignManager"); // 캠페인 관리자 유지 검사
            CheckRequired(DungeonSelectionManager.Instance, "DungeonSelectionManager"); // 선택 관리자 유지 검사
            CheckRequired(RunResultManager.Instance, "RunResultManager"); // 결과 관리자 유지 검사
            CheckRequired(FindFirstObjectByType<DungeonGenerator>(), "DungeonGenerator"); // 던전 생성기 존재 검사
            CheckRequired(FindFirstObjectByType<DungeonTimeSystem>(), "DungeonTimeSystem"); // 제한시간 시스템 존재 검사
            CheckRequired(FindFirstObjectByType<PlayerController>(), "PlayerController"); // 플레이어 존재 검사
            CheckRequired(FindFirstObjectByType<Wagon>(), "Wagon"); // 마차 존재 검사

            VerticalSliceValidator verticalValidator = FindFirstObjectByType<VerticalSliceValidator>(); // 기존 던전 검증 도구 검색
            CheckRequired(verticalValidator, "VerticalSliceValidator"); // 기존 검증 도구 존재 검사

            if (verticalValidator != null && verticalValidator.ErrorCount > 0) // 기존 던전 검증 오류 여부 확인
            {
                currentErrors.Add($"VerticalSliceValidator 오류: {verticalValidator.ErrorCount}개"); // 기존 검증 오류 추가
            }

            RunResultManager resultManager = RunResultManager.Instance; // 현재 결과 관리자 가져오기

            if (resultManager != null && resultManager.HasResult && !Mathf.Approximately(Time.timeScale, 0f)) // 결과 화면 정지 상태 검사
            {
                currentErrors.Add($"결과 확정 후 Time Scale 비정상: {Time.timeScale}"); // 결과 화면 시간 오류 추가
            }

            CheckDuplicate<CampaignManager>("CampaignManager"); // 캠페인 관리자 중복 검사
            CheckDuplicate<DungeonSelectionManager>("DungeonSelectionManager"); // 선택 관리자 중복 검사
            CheckDuplicate<RunResultManager>("RunResultManager"); // 결과 관리자 중복 검사
        }

        void CheckSceneLoadable(string sceneName) // 지정된 Scene 등록 여부 검사
        {
            if (string.IsNullOrWhiteSpace(sceneName)) // Scene 이름 입력 여부 확인
            {
                currentErrors.Add("검증할 Scene 이름이 비어 있음"); // 빈 Scene 이름 오류 추가
                return; // Scene 검사 중단
            }

            if (!Application.CanStreamedLevelBeLoaded(sceneName)) // Build Profile 등록 여부 확인
            {
                currentErrors.Add($"Build Profile Scene 누락: {sceneName}"); // Scene 등록 누락 오류 추가
            }
        }

        void CheckRequired(UnityEngine.Object target, string objectName) // 필수 오브젝트 존재 검사
        {
            if (target == null) // 전달된 오브젝트 존재 여부 확인
            {
                currentErrors.Add($"{objectName} 없음"); // 필수 오브젝트 누락 오류 추가
            }
        }

        void CheckDuplicate<T>(string objectName) where T : Component // 동일 컴포넌트 중복 검사
        {
            int count = FindObjectsByType<T>(FindObjectsSortMode.None).Length; // 활성 컴포넌트 개수 계산

            if (count > 1) // 중복 컴포넌트 존재 여부 확인
            {
                currentErrors.Add($"{objectName} 중복: {count}개"); // 중복 컴포넌트 오류 추가
            }
        }

        void RecordValidationResult() // 현재 Scene 검증 결과 기록
        {
            string result = currentErrors.Count == 0 ? "통과" : "실패"; // 검증 결과 문구 계산
            string history = $"{currentSceneName}: {result} / 오류 {currentErrors.Count} / 경고 {currentWarnings.Count}"; // 기록 문구 생성
            sessionHistory.Add(history); // 전체 회귀 기록 추가

            if (sessionHistory.Count > 12) // 최대 기록 개수 초과 여부 확인
            {
                sessionHistory.RemoveAt(0); // 가장 오래된 기록 제거
            }

            if (currentErrors.Count == 0) // 현재 Scene 오류 없음 여부 확인
            {
                Debug.Log($"[Regression] {history}"); // 검증 통과 Console 출력
            }
            else // 현재 Scene 오류 존재
            {
                Debug.LogWarning($"[Regression] {history}"); // 검증 실패 Console 출력
            }
        }

        void OnGUI() // 회귀 검증 결과 패널 표시
        {
            if (!showPanel) // 패널 표시 여부 확인
            {
                return; // 패널 표시 중단
            }

            float panelHeight = Mathf.Min(Screen.height - 20f, 620f); // 화면에 맞는 패널 높이 계산
            GUILayout.BeginArea(new Rect(10f, 10f, 500f, panelHeight), GUI.skin.box); // 검증 패널 영역 시작
            GUILayout.Label("46일차 전체 흐름 회귀 검증"); // 검증 패널 제목 표시
            GUILayout.Label($"현재 Scene: {currentSceneName}"); // 현재 Scene 이름 표시
            GUILayout.Label($"현재 결과: 오류 {currentErrors.Count} / 경고 {currentWarnings.Count}"); // 현재 오류와 경고 수 표시
            GUILayout.Label($"실행 중 Console 오류: {sessionErrors.Count}"); // 전체 Console 오류 수 표시
            GUILayout.Label("[F9] 패널 표시 전환 / [F10] 현재 Scene 재검증"); // 단축키 안내 표시
            GUILayout.Space(8f); // 항목 사이 여백 추가

            foreach (string error in currentErrors) // 현재 Scene 오류 순회
            {
                GUILayout.Label($"오류: {error}"); // 현재 Scene 오류 표시
            }

            foreach (string warning in currentWarnings) // 현재 Scene 경고 순회
            {
                GUILayout.Label($"경고: {warning}"); // 현재 Scene 경고 표시
            }

            GUILayout.Space(8f); // 항목 사이 여백 추가
            GUILayout.Label("Scene 검증 기록"); // 회귀 기록 제목 표시

            foreach (string history in sessionHistory) // 전체 Scene 기록 순회
            {
                GUILayout.Label(history); // Scene 검증 기록 표시
            }

            if (sessionErrors.Count > 0) // Console 오류 존재 여부 확인
            {
                GUILayout.Space(8f); // 항목 사이 여백 추가
                GUILayout.Label("최근 Console 오류"); // Console 오류 제목 표시
                int firstIndex = Mathf.Max(0, sessionErrors.Count - 5); // 표시할 첫 오류 번호 계산

                for (int index = firstIndex; index < sessionErrors.Count; index++) // 최근 오류 순회
                {
                    GUILayout.Label(sessionErrors[index]); // 최근 Console 오류 표시
                }
            }

            GUILayout.EndArea(); // 검증 패널 영역 종료
        }
    }
}