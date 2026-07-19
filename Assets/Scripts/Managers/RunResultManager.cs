using UnityEngine; // Unity 기본 기능 사용
using UnityEngine.SceneManagement; // Scene 전환 후 참조 재연결 기능 사용

namespace ProjectI // 프로젝트 공통 네임스페이스
{
    public class RunResultManager : MonoBehaviour // 던전 종료 결과 생성과 Scene 간 보관 담당
    {
        public static RunResultManager Instance { get; private set; } // 현재 활성 결과 매니저 접근점

        [Header("임시 결과 화면")] // Inspector 결과 화면 설정 구분
        [SerializeField] bool showResultPanel = true; // OnGUI 결과 화면 표시 여부

        readonly RunResultData currentResult = new RunResultData(); // 현재 보관 중인 던전 결과

        Wagon wagon; // 확보 아이템과 탈출 상태를 제공하는 마차
        DungeonTimeSystem dungeonTimeSystem; // 제한시간과 진행시간을 제공하는 시스템
        PlayerController playerController; // 최종 사망 이벤트를 제공하는 플레이어
        DungeonGenerator dungeonGenerator; // 던전 생성 시드를 제공하는 생성기

        bool runActive; // 현재 던전 탐험 결과를 받을 수 있는 상태
        float fallbackStartTime; // 시간 시스템 누락 시 사용할 실시간 시작 시각
        GUIStyle titleStyle; // 결과 제목 표시 스타일
        GUIStyle centerStyle; // 결과 상세 표시 스타일

        public RunResultData CurrentResult => currentResult; // 현재 던전 결과 반환
        public bool HasResult => currentResult.HasResult; // 현재 확정된 결과 존재 여부

        void Awake() // 결과 매니저 싱글톤 초기화
        {
            if (Instance != null && Instance != this) // 기존 결과 매니저 존재 여부 확인
            {
                Destroy(gameObject); // 중복 결과 매니저 제거
                return; // 중복 초기화 중단
            }

            Instance = this; // 현재 오브젝트를 전역 결과 매니저로 저장
            transform.SetParent(null); // DontDestroyOnLoad 적용을 위해 루트로 분리
            DontDestroyOnLoad(gameObject); // Scene 전환 후에도 던전 결과 유지
        }

        void OnEnable() // Scene 전환 이벤트 구독
        {
            SceneManager.sceneLoaded += HandleSceneLoaded; // 새로운 Scene 로드 시 참조 재연결
        }

        void Start() // 첫 번째 Scene의 던전 시스템 연결
        {
            ConnectToCurrentScene(); // 현재 Scene의 결과 관련 시스템 검색
        }

        void OnDisable() // Scene 전환 이벤트 구독 해제
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded; // Scene 로드 이벤트 연결 해제
            UnsubscribeRunEvents(); // 현재 던전 종료 이벤트 연결 해제
        }

        void OnDestroy() // 결과 매니저 싱글톤 참조 정리
        {
            if (Instance == this) // 현재 오브젝트가 등록된 결과 매니저인지 확인
            {
                Instance = null; // 전역 결과 매니저 참조 초기화
            }
        }

        void HandleSceneLoaded(Scene scene, LoadSceneMode loadMode) // 새로운 Scene 로드 후 던전 시스템 재검색
        {
            ConnectToCurrentScene(); // 새 Scene의 결과 관련 시스템 연결
        }

        void ConnectToCurrentScene() // 현재 Scene의 던전 결과 관련 시스템 연결
        {
            UnsubscribeRunEvents(); // 이전 Scene의 이벤트 연결 정리

            wagon = FindFirstObjectByType<Wagon>(); // 현재 Scene의 마차 검색
            dungeonTimeSystem = FindFirstObjectByType<DungeonTimeSystem>(); // 현재 Scene의 제한시간 시스템 검색
            playerController = FindFirstObjectByType<PlayerController>(); // 현재 Scene의 플레이어 검색
            dungeonGenerator = FindFirstObjectByType<DungeonGenerator>(); // 현재 Scene의 던전 생성기 검색

            if (wagon != null) // 마차 존재 여부 확인
            {
                wagon.Left += HandleWagonLeft; // 마차 탈출 완료 이벤트 연결
            }

            if (dungeonTimeSystem != null) // 제한시간 시스템 존재 여부 확인
            {
                dungeonTimeSystem.Failed += HandleDeadlineFailure; // 제한시간 유기 실패 이벤트 연결
            }

            if (playerController != null) // 플레이어 존재 여부 확인
            {
                playerController.Died += HandlePlayerDeath; // 최종 사망 이벤트 연결
            }

            runActive = wagon != null && dungeonGenerator != null; // 던전 진행 Scene 여부 판단

            if (runActive) // 새로운 던전 Scene인지 확인
            {
                BeginRun(); // 이전 결과를 지우고 새로운 탐험 시작
            }
        }

        void UnsubscribeRunEvents() // 이전 Scene의 던전 종료 이벤트 연결 해제
        {
            if (wagon != null) // 기존 마차 참조 존재 여부 확인
            {
                wagon.Left -= HandleWagonLeft; // 마차 탈출 이벤트 연결 해제
            }

            if (dungeonTimeSystem != null) // 기존 제한시간 시스템 존재 여부 확인
            {
                dungeonTimeSystem.Failed -= HandleDeadlineFailure; // 제한시간 실패 이벤트 연결 해제
            }

            if (playerController != null) // 기존 플레이어 참조 존재 여부 확인
            {
                playerController.Died -= HandlePlayerDeath; // 최종 사망 이벤트 연결 해제
            }
        }

        void BeginRun() // 새로운 던전 탐험 결과 기록 시작
        {
            currentResult.Clear(); // 이전 던전 결과 초기화
            fallbackStartTime = Time.realtimeSinceStartup; // 실시간 시작 시각 저장
            runActive = true; // 결과 기록 가능 상태 활성화
            Debug.Log("[RunResult] 새로운 던전 결과 기록을 시작합니다."); // 결과 기록 시작 출력
        }

        void HandleWagonLeft() // 마차 탈출 성공 결과 처리
        {
            bool deadlineExtraction = dungeonTimeSystem != null && dungeonTimeSystem.IsLocked; // 제한시간 자동 탈출 여부 확인
            RunEndReason reason = deadlineExtraction ? RunEndReason.DeadlineExtraction : RunEndReason.ManualExtraction; // 탈출 방식에 맞는 원인 결정
            CompleteRun(true, reason); // 탈출 성공 결과 확정
        }

        void HandleDeadlineFailure() // 제한시간 유기 실패 결과 처리
        {
            CompleteRun(false, RunEndReason.DeadlineAbandoned); // 제한시간 유기 결과 확정
        }

        void HandlePlayerDeath() // 부활 불가 최종 사망 결과 처리
        {
            CompleteRun(false, RunEndReason.PlayerDeath); // 플레이어 사망 결과 확정
        }

        void CompleteRun(bool escaped, RunEndReason reason) // 현재 던전 결과를 한 번만 확정
        {
            if (!runActive || currentResult.HasResult) // 이미 결과가 확정됐거나 던전 Scene이 아닌지 확인
            {
                return; // 중복 결과 처리 방지
            }

            int securedItemCount = wagon != null ? wagon.SecuredCount : 0; // 마차의 전체 확보 아이템 수 가져오기
            int securedTreasureCount = wagon != null ? wagon.SecuredTreasureCount : 0; // 마차의 확보 보물 수 가져오기
            int securedValue = wagon != null ? wagon.SecuredValue : 0; // 마차의 확보 보물 가치 가져오기
            float elapsedSeconds = dungeonTimeSystem != null ? dungeonTimeSystem.ElapsedSeconds : Time.realtimeSinceStartup - fallbackStartTime; // 던전 진행시간 가져오기
            int dungeonSeed = dungeonGenerator != null ? dungeonGenerator.CurrentSeed : 0; // 던전 생성 시드 가져오기

            currentResult.SetResult( // 현재 탐험의 최종 결과 저장
                escaped, // 탈출 성공 여부 전달
                reason, // 종료 원인 전달
                securedItemCount, // 전체 확보 아이템 수 전달
                securedTreasureCount, // 확보 보물 수 전달
                securedValue, // 확보 보물 가치 전달
                elapsedSeconds, // 던전 진행시간 전달
                dungeonSeed); // 던전 생성 시드 전달

            runActive = false; // 추가 결과 기록 차단
            Cursor.lockState = CursorLockMode.None; // 결과 확인을 위해 커서 잠금 해제
            Cursor.visible = true; // 마우스 커서 표시
            Time.timeScale = 0f; // 던전 진행 정지

            Debug.Log($"[RunResult] 결과 확정 — {currentResult.GetEndReasonText()}, 보물 {securedTreasureCount}개, 가치 {securedValue}골드"); // 확정 결과 출력
        }

        public void ClearCurrentResult() // 28일차 정산 완료 후 현재 결과 초기화
        {
            currentResult.Clear(); // 보관 중인 결과 데이터 초기화
        }

        void OnGUI() // 확정된 던전 결과를 임시 화면에 표시
        {
            if (!showResultPanel || !currentResult.HasResult) // 결과 화면 표시 가능 여부 확인
            {
                return; // 결과 화면 표시 중단
            }

            if (titleStyle == null) // 결과 표시 스타일 초기화 여부 확인
            {
                titleStyle = new GUIStyle(GUI.skin.label); // 결과 제목 스타일 생성
                titleStyle.fontSize = 26; // 결과 제목 글자 크기 설정
                titleStyle.alignment = TextAnchor.MiddleCenter; // 결과 제목 중앙 정렬
                centerStyle = new GUIStyle(GUI.skin.label); // 결과 상세 스타일 생성
                centerStyle.fontSize = 16; // 결과 상세 글자 크기 설정
                centerStyle.alignment = TextAnchor.MiddleCenter; // 결과 상세 중앙 정렬
            }

            float width = 520f; // 결과 패널 너비
            float height = 270f; // 결과 패널 높이
            float x = (Screen.width - width) * 0.5f; // 화면 중앙 가로 위치
            float y = (Screen.height - height) * 0.5f; // 화면 중앙 세로 위치
            int totalMinutes = Mathf.FloorToInt(currentResult.ElapsedSeconds / 60f); // 진행시간의 분 계산
            int totalSeconds = Mathf.FloorToInt(currentResult.ElapsedSeconds % 60f); // 진행시간의 초 계산
            string title = currentResult.Escaped ? "던전 종료 — 탈출 성공" : "던전 종료 — 탈출 실패"; // 탈출 결과 제목 계산

            GUI.Box(new Rect(x, y, width, height), string.Empty); // 결과 패널 배경 표시
            GUI.Label(new Rect(x + 10f, y + 15f, width - 20f, 40f), title, titleStyle); // 탈출 결과 제목 표시
            GUI.Label(new Rect(x + 10f, y + 65f, width - 20f, 25f), $"종료 원인: {currentResult.GetEndReasonText()}", centerStyle); // 종료 원인 표시
            GUI.Label(new Rect(x + 10f, y + 95f, width - 20f, 25f), $"확보 아이템: {currentResult.SecuredItemCount}개", centerStyle); // 전체 확보 아이템 수 표시
            GUI.Label(new Rect(x + 10f, y + 125f, width - 20f, 25f), $"확보 보물: {currentResult.SecuredTreasureCount}개", centerStyle); // 확보 보물 수 표시
            GUI.Label(new Rect(x + 10f, y + 155f, width - 20f, 25f), $"확보 가치: {currentResult.SecuredValue}골드", centerStyle); // 확보 가치 표시
            GUI.Label(new Rect(x + 10f, y + 185f, width - 20f, 25f), $"진행시간: {totalMinutes:00}:{totalSeconds:00}", centerStyle); // 던전 진행시간 표시
            GUI.Label(new Rect(x + 10f, y + 215f, width - 20f, 25f), $"던전 시드: {currentResult.DungeonSeed}", centerStyle); // 던전 생성 시드 표시
            GUI.Label(new Rect(x + 10f, y + 240f, width - 20f, 20f), "28일차에 마을 정산 화면과 연결", centerStyle); // 다음 작업 안내 표시
        }
    }
}