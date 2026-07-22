using UnityEngine; // Unity 기본 기능 사용
using UnityEngine.SceneManagement; // MainMenu와 Village Scene 이동 기능 사용

namespace ProjectI // 프로젝트 공통 네임스페이스
{
    public class CampaignEndingUI : MonoBehaviour // 캠페인 성공과 실패 결과 화면 관리
    {
        [Header("Scene 설정")] // Inspector Scene 이름 설정 구분
        [Tooltip("결과 화면에서 돌아갈 메인 메뉴 Scene 이름")] [SerializeField] string mainMenuSceneName = "MainMenu"; // 결과 화면에서 돌아갈 메인 메뉴 Scene 이름
        [Tooltip("새 캠페인을 시작할 마을 Scene 이름")] [SerializeField] string villageSceneName = "Village"; // 새 캠페인을 시작할 마을 Scene 이름

        [Header("화면 문구")] // Inspector 결과 화면 문구 설정 구분
        [Tooltip("캠페인 성공 제목")] [SerializeField] string victoryTitle = "빚을 모두 갚았습니다"; // 캠페인 성공 제목
        [Tooltip("캠페인 실패 제목")] [SerializeField] string defeatTitle = "상환 기한이 끝났습니다"; // 캠페인 실패 제목
        [Tooltip("캠페인 성공 설명")] [SerializeField] string victoryMessage = "도굴단은 빚에서 벗어나 새로운 삶을 얻었습니다."; // 캠페인 성공 설명
        [Tooltip("캠페인 실패 설명")] [SerializeField] string defeatMessage = "도굴단은 기한 안에 빚을 갚지 못했습니다."; // 캠페인 실패 설명

        CampaignManager campaignManager; // 현재 캠페인 상태를 확인할 관리자
        PlayerController lockedController; // 결과 화면에서 정지시킨 플레이어 이동 컴포넌트
        PlayerInteractor lockedInteractor; // 결과 화면에서 정지시킨 플레이어 상호작용 컴포넌트

        bool resultOpen; // 현재 결과 화면 표시 여부
        bool playerControlCaptured; // 플레이어 조작 상태 저장 여부
        bool controllerWasEnabled; // 결과 화면 전 이동 컴포넌트 활성 상태
        bool interactorWasEnabled; // 결과 화면 전 상호작용 컴포넌트 활성 상태
        bool leavingScene; // 결과 화면에서 다른 Scene으로 이동 중인지 여부
        float previousTimeScale = 1f; // 결과 화면 전 게임 시간 배율
        CursorLockMode previousCursorLockMode; // 결과 화면 전 마우스 잠금 상태
        bool previousCursorVisible; // 결과 화면 전 마우스 표시 상태

        void Awake() // 캠페인 관리자 초기 참조 검색
        {
            ResolveCampaignManager(); // 캠페인 관리자 연결 시도
        }

        void Update() // 캠페인 종료 상태 감지와 플레이어 조작 정지
        {
            ResolveCampaignManager(); // Scene 전환 후 캠페인 관리자 참조 갱신

            if (!resultOpen && HasCampaignEnded()) // 결과 화면이 닫혀 있고 캠페인이 종료됐는지 확인
            {
                OpenResult(); // 캠페인 결과 화면 열기
            }

            if (resultOpen) // 현재 결과 화면이 열려 있는지 확인
            {
                TryLockPlayerControl(); // 늦게 생성된 Village Player도 조작 정지
            }
        }

        void OnDestroy() // 오브젝트 제거 시 게임 상태 복구
        {
            if (leavingScene) // 정상적인 Scene 이동 중인지 확인
            {
                return; // Scene 이동 준비에서 이미 복구했으므로 중복 처리 방지
            }

            RestorePlayerControl(); // 플레이어 조작 상태 복구
            Time.timeScale = previousTimeScale; // 결과 화면 전 게임 시간 복구
        }

        void ResolveCampaignManager() // 싱글톤과 현재 Scene에서 캠페인 관리자 검색
        {
            if (CampaignManager.Instance != null) // 전역 캠페인 관리자 존재 여부 확인
            {
                campaignManager = CampaignManager.Instance; // 전역 캠페인 관리자 연결
                return; // 추가 검색 중단
            }

            if (campaignManager == null) // 저장된 관리자 참조 존재 여부 확인
            {
                campaignManager = FindFirstObjectByType<CampaignManager>(); // 현재 Scene에서 캠페인 관리자 검색
            }
        }

        bool HasCampaignEnded() // 캠페인 성공 또는 실패 여부 반환
        {
            if (campaignManager == null || campaignManager.State == null) // 캠페인 상태 존재 여부 확인
            {
                return false; // 종료 상태가 아닌 것으로 반환
            }

            return campaignManager.State.CampaignWon || campaignManager.State.CampaignFailed; // 성공 또는 실패 상태 반환
        }

        void OpenResult() // 캠페인 결과 화면 활성화와 마을 진행 정지
        {
            resultOpen = true; // 결과 화면 표시 상태 활성화
            previousTimeScale = Time.timeScale; // 기존 게임 시간 배율 저장
            previousCursorLockMode = Cursor.lockState; // 기존 마우스 잠금 상태 저장
            previousCursorVisible = Cursor.visible; // 기존 마우스 표시 상태 저장

            campaignManager.SetHudVisible(false); // 결과 화면 뒤의 캠페인 HUD 숨김
            Time.timeScale = 0f; // 결과 확인 중 마을 진행 정지
            Cursor.lockState = CursorLockMode.None; // 결과 버튼 조작을 위해 마우스 잠금 해제
            Cursor.visible = true; // 결과 버튼 조작을 위해 마우스 커서 표시

            TryLockPlayerControl(); // 현재 생성된 플레이어 조작 정지

            string resultText = campaignManager.State.CampaignWon ? "성공" : "실패"; // Console에 표시할 결과 문구 계산
            Debug.Log($"[CampaignEnding] 캠페인 {resultText} 결과 화면을 표시합니다."); // 결과 화면 표시 로그 출력
        }

        void TryLockPlayerControl() // 결과 화면 사용 중 플레이어 이동과 상호작용 정지
        {
            if (playerControlCaptured) // 플레이어 조작 상태를 이미 저장했는지 확인
            {
                return; // 중복 조작 상태 저장 방지
            }

            PlayerInteractor foundInteractor = FindFirstObjectByType<PlayerInteractor>(); // 현재 Village Player의 상호작용 컴포넌트 검색
            PlayerController foundController = foundInteractor != null ? foundInteractor.GetComponent<PlayerController>() : FindFirstObjectByType<PlayerController>(); // 플레이어 이동 컴포넌트 검색

            if (foundInteractor == null && foundController == null) // 아직 플레이어가 생성되지 않았는지 확인
            {
                return; // 다음 프레임에 다시 검색
            }

            lockedInteractor = foundInteractor; // 검색한 상호작용 컴포넌트 저장
            lockedController = foundController; // 검색한 이동 컴포넌트 저장
            interactorWasEnabled = lockedInteractor != null && lockedInteractor.enabled; // 기존 상호작용 활성 상태 저장
            controllerWasEnabled = lockedController != null && lockedController.enabled; // 기존 이동 활성 상태 저장
            playerControlCaptured = true; // 플레이어 조작 상태 저장 완료 표시

            if (lockedInteractor != null) // 상호작용 컴포넌트 존재 여부 확인
            {
                lockedInteractor.enabled = false; // 결과 화면 중 추가 상호작용 차단
            }

            if (lockedController != null) // 이동 컴포넌트 존재 여부 확인
            {
                lockedController.enabled = false; // 결과 화면 중 이동과 시점 조작 차단
            }
        }

        void RestorePlayerControl() // 결과 화면 전에 저장한 플레이어 조작 상태 복원
        {
            if (!playerControlCaptured) // 저장된 플레이어 조작 상태가 있는지 확인
            {
                return; // 복구할 상태가 없으면 중단
            }

            if (lockedInteractor != null) // 상호작용 컴포넌트 존재 여부 확인
            {
                lockedInteractor.enabled = interactorWasEnabled; // 결과 화면 전 활성 상태 복원
            }

            if (lockedController != null) // 이동 컴포넌트 존재 여부 확인
            {
                lockedController.enabled = controllerWasEnabled; // 결과 화면 전 활성 상태 복원
            }

            lockedInteractor = null; // 저장된 상호작용 컴포넌트 참조 제거
            lockedController = null; // 저장된 이동 컴포넌트 참조 제거
            playerControlCaptured = false; // 플레이어 조작 상태 저장 여부 초기화
        }

        void PrepareSceneTransition() // 결과 화면에서 다른 Scene으로 이동하기 전 상태 정리
        {
            leavingScene = true; // 정상적인 Scene 이동 상태 표시
            RestorePlayerControl(); // 기존 플레이어 조작 상태 복구
            Time.timeScale = 1f; // 다음 Scene이 멈추지 않도록 시간 정상화
            Cursor.lockState = CursorLockMode.None; // 다음 메뉴를 위해 마우스 잠금 해제
            Cursor.visible = true; // 다음 메뉴를 위해 마우스 커서 표시
        }

        void ReturnToMainMenu() // 결과 저장을 유지하고 MainMenu Scene으로 이동
        {
            if (campaignManager != null) // 캠페인 관리자 존재 여부 확인
            {
                campaignManager.SetHudVisible(false); // 메인 메뉴에서 캠페인 HUD 숨김 유지
            }

            PrepareSceneTransition(); // Scene 이동 전 정지 상태 정리
            SceneManager.LoadScene(mainMenuSceneName); // MainMenu Scene 로드
        }

        void StartNewCampaign() // 기존 저장 기록을 초기화하는 새 캠페인 요청
        {
            PrepareSceneTransition(); // Scene 이동 전 정지 상태 정리
            GameStartRequest.Request(GameStartMode.NewGame); // Village에서 처리할 새 게임 요청 등록
            SceneManager.LoadScene(villageSceneName); // Village Scene을 다시 로드
        }

        void QuitGame() // 에디터 또는 Windows 빌드 종료
        {
            Time.timeScale = 1f; // 게임 종료 전 시간 배율 정상화

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false; // Unity Editor의 Play 모드 종료
#else
            Application.Quit(); // Windows 게임 빌드 종료
#endif
        }

        void OnGUI() // 캠페인 성공 또는 실패 결과 화면 표시
        {
            if (!resultOpen || campaignManager == null || campaignManager.State == null) // 결과 화면 표시 가능 여부 확인
            {
                return; // 결과 화면 표시 중단
            }

            CampaignStateData state = campaignManager.State; // 결과에 표시할 캠페인 상태 가져오기
            bool victory = state.CampaignWon; // 캠페인 성공 여부 저장
            string title = victory ? victoryTitle : defeatTitle; // 성공 또는 실패 제목 계산
            string message = victory ? victoryMessage : defeatMessage; // 성공 또는 실패 설명 계산
            int finalDay = Mathf.Max(1, state.CurrentDay - 1); // 실제 마지막 탐험 날짜 계산

            float width = 680f; // 결과 창 너비
            float height = 560f; // 결과 창 높이
            float x = (Screen.width - width) * 0.5f; // 화면 가로 중앙 위치 계산
            float y = (Screen.height - height) * 0.5f; // 화면 세로 중앙 위치 계산

            GUIStyle titleStyle = new GUIStyle(GUI.skin.label); // 결과 제목용 GUI 스타일 생성
            titleStyle.alignment = TextAnchor.MiddleCenter; // 결과 제목 가운데 정렬
            titleStyle.fontSize = 36; // 결과 제목 글자 크기 설정
            titleStyle.fontStyle = FontStyle.Bold; // 결과 제목 굵게 표시
            titleStyle.normal.textColor = victory ? new Color(0.75f, 1f, 0.65f) : new Color(1f, 0.55f, 0.55f); // 성공과 실패에 맞는 제목 색상 설정

            GUIStyle messageStyle = new GUIStyle(GUI.skin.label); // 결과 설명용 GUI 스타일 생성
            messageStyle.alignment = TextAnchor.MiddleCenter; // 결과 설명 가운데 정렬
            messageStyle.fontSize = 18; // 결과 설명 글자 크기 설정
            messageStyle.wordWrap = true; // 긴 결과 설명 자동 줄바꿈

            GUIStyle recordStyle = new GUIStyle(GUI.skin.label); // 최종 기록용 GUI 스타일 생성
            recordStyle.alignment = TextAnchor.MiddleLeft; // 최종 기록 왼쪽 정렬
            recordStyle.fontSize = 17; // 최종 기록 글자 크기 설정

            GUI.Box(new Rect(0f, 0f, Screen.width, Screen.height), string.Empty); // 전체 화면 입력 차단용 배경 표시
            GUI.Box(new Rect(x, y, width, height), string.Empty); // 캠페인 결과 창 배경 표시
            GUI.Label(new Rect(x + 30f, y + 35f, width - 60f, 60f), title, titleStyle); // 성공 또는 실패 제목 표시
            GUI.Label(new Rect(x + 55f, y + 105f, width - 110f, 60f), message, messageStyle); // 성공 또는 실패 설명 표시

            GUI.Box(new Rect(x + 85f, y + 185f, width - 170f, 175f), "최종 원정 기록"); // 최종 기록 영역 표시
            GUI.Label(new Rect(x + 115f, y + 225f, width - 230f, 25f), $"마지막 탐험 날짜: {finalDay}일차", recordStyle); // 마지막 탐험 날짜 표시
            GUI.Label(new Rect(x + 115f, y + 255f, width - 230f, 25f), $"완료한 탐험: {state.CompletedRuns}회", recordStyle); // 완료한 탐험 횟수 표시
            GUI.Label(new Rect(x + 115f, y + 285f, width - 230f, 25f), $"남은 골드: {state.Gold}골드", recordStyle); // 최종 보유 골드 표시
            GUI.Label(new Rect(x + 115f, y + 315f, width - 230f, 25f), $"남은 빚: {state.RemainingDebt}골드", recordStyle); // 최종 남은 빚 표시

            if (GUI.Button(new Rect(x + 55f, y + 405f, 175f, 55f), "메인 메뉴")) // 메인 메뉴 버튼 입력 확인
            {
                ReturnToMainMenu(); // 결과 저장을 유지하고 메인 메뉴로 이동
            }

            if (GUI.Button(new Rect(x + 252f, y + 405f, 175f, 55f), "새 캠페인")) // 새 캠페인 버튼 입력 확인
            {
                StartNewCampaign(); // 기존 기록을 초기화하는 새 캠페인 시작
            }

            if (GUI.Button(new Rect(x + 449f, y + 405f, 175f, 55f), "게임 종료")) // 게임 종료 버튼 입력 확인
            {
                QuitGame(); // 게임 종료 실행
            }

            GUI.Label(new Rect(x + 65f, y + 485f, width - 130f, 35f), "메인 메뉴로 돌아가도 현재 캠페인 결과는 저장 기록에 남습니다.", messageStyle); // 결과 저장 유지 안내 표시
        }
    }
}