using UnityEngine; // Unity 기본 기능 사용
using UnityEngine.SceneManagement; // Village Scene 이동 기능 사용

namespace ProjectI // 프로젝트 공통 네임스페이스
{
    public class MainMenuUI : MonoBehaviour // 새 게임과 이어하기를 제공하는 임시 메인 메뉴
    {
        [Header("Scene 설정")] // Inspector Scene 설정 구분
        [Tooltip("게임 시작 후 이동할 마을 Scene 이름")] [SerializeField] string villageSceneName = "Village"; // 게임 시작 후 이동할 마을 Scene 이름

        [Header("저장 연결")] // Inspector 저장 관리자 연결 구분
        [Tooltip("저장 파일 존재 여부를 확인할 관리자")] [SerializeField] CampaignSaveManager saveManager; // 저장 파일 존재 여부를 확인할 관리자
        [Tooltip("메인 메뉴에서 열 게임 설정 화면")] [SerializeField] GameSettingsUI settingsUI; // 메인 메뉴에서 열 게임 설정 화면

        [Header("화면 설정")] // Inspector 메인 메뉴 문구 설정 구분
        [Tooltip("메인 메뉴 게임 제목")] [SerializeField] string gameTitle = "PROJECT I"; // 메인 메뉴 게임 제목
        [Tooltip("메인 메뉴 보조 제목")] [SerializeField] string gameSubtitle = "도굴단 원정 기록"; // 메인 메뉴 보조 제목

        bool newGameConfirmation; // 기존 저장 기록 덮어쓰기 확인 여부
        string statusMessage = string.Empty; // 메인 메뉴 안내 문구

        void Awake() // 메인 메뉴 시작 상태 설정
        {
            GameStartRequest.Clear(); // 이전 Scene 이동 요청 초기화

            if (CampaignManager.Instance != null) // 이전 캠페인 관리자가 메인 메뉴에 남아 있는지 확인
            {
                CampaignManager.Instance.SetHudVisible(false); // 메인 메뉴에서 캠페인 HUD 숨김
            }

            ResolveSaveManager(); // 저장 관리자 참조 검색
            if (settingsUI == null) // Inspector에 설정 화면이 연결되지 않았는지 확인
            {
                settingsUI = FindFirstObjectByType<GameSettingsUI>(); // 현재 Scene에서 설정 화면 검색
            }
            Cursor.lockState = CursorLockMode.None; // 메뉴 조작을 위해 마우스 잠금 해제
            Cursor.visible = true; // 메뉴 조작을 위해 마우스 커서 표시
        }

        void ResolveSaveManager() // 싱글톤과 현재 Scene에서 저장 관리자 검색
        {
            if (CampaignSaveManager.Instance != null) // 전역 저장 관리자 존재 여부 확인
            {
                saveManager = CampaignSaveManager.Instance; // 전역 저장 관리자 연결
                return; // 추가 검색 중단
            }

            if (saveManager == null) // Inspector 참조 존재 여부 확인
            {
                saveManager = FindFirstObjectByType<CampaignSaveManager>(); // 현재 Scene에서 저장 관리자 검색
            }
        }

        void RequestNewGame() // 저장 기록 확인 후 새 게임 시작 요청
        {
            ResolveSaveManager(); // 저장 관리자 참조 다시 확인

            if (saveManager != null && saveManager.HasSaveFile && !newGameConfirmation) // 기존 저장 파일과 첫 번째 선택 여부 확인
            {
                newGameConfirmation = true; // 기존 저장 기록 삭제 확인 화면 활성화
                statusMessage = "새 게임을 시작하면 기존 저장 기록이 삭제됩니다."; // 삭제 경고 문구 저장
                return; // 즉시 새 게임을 시작하지 않고 확인 대기
            }

            StartGame(GameStartMode.NewGame); // 새 게임 시작 요청 실행
        }

        void RequestContinueGame() // 기존 저장 기록 이어하기 요청
        {
            ResolveSaveManager(); // 저장 관리자 참조 다시 확인

            if (saveManager == null) // 저장 관리자 존재 여부 확인
            {
                statusMessage = "CampaignSaveManager를 찾을 수 없습니다."; // 관리자 누락 안내 저장
                Debug.LogError("[MainMenu] CampaignSaveManager가 없습니다."); // 관리자 누락 오류 출력
                return; // 이어하기 중단
            }

            if (!saveManager.HasSaveFile) // 저장 파일 존재 여부 확인
            {
                statusMessage = "이어할 저장 기록이 없습니다."; // 저장 파일 없음 안내 저장
                return; // 이어하기 중단
            }

            StartGame(GameStartMode.Continue); // 이어하기 시작 요청 실행
        }

        void StartGame(GameStartMode startMode) // 시작 방식을 등록하고 Village Scene으로 이동
        {
            if (string.IsNullOrWhiteSpace(villageSceneName)) // Village Scene 이름 입력 여부 확인
            {
                statusMessage = "Village Scene 이름이 비어 있습니다."; // Scene 설정 오류 안내 저장
                Debug.LogError("[MainMenu] Village Scene 이름이 비어 있습니다."); // Scene 설정 오류 출력
                return; // Scene 이동 중단
            }

            GameStartRequest.Request(startMode); // Village에서 처리할 시작 방식 등록
            SceneManager.LoadScene(villageSceneName); // Village Scene 로드
        }

        void CancelNewGame() // 새 게임 덮어쓰기 확인 취소
        {
            newGameConfirmation = false; // 덮어쓰기 확인 상태 해제
            statusMessage = string.Empty; // 경고 문구 초기화
        }

        void QuitGame() // 에디터 또는 빌드에서 게임 종료
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false; // Unity Editor의 Play 모드 종료
#else
            Application.Quit(); // 실제 게임 빌드 종료
#endif
        }

        void OnGUI() // OnGUI 기반 임시 메인 메뉴 표시
        {
            if (settingsUI != null && settingsUI.IsOpen) // 게임 설정 화면이 열려 있는지 확인
            {
                return; // 기존 메인 메뉴와 설정 화면이 겹치지 않도록 표시 중단
            }
            float width = 560f; // 메인 메뉴 창 너비
            float height = 580f; // 설정 버튼을 포함한 메인 메뉴 창 높이
            float x = (Screen.width - width) * 0.5f; // 화면 가로 중앙 위치 계산
            float y = (Screen.height - height) * 0.5f; // 화면 세로 중앙 위치 계산

            GUIStyle titleStyle = new GUIStyle(GUI.skin.label); // 게임 제목용 GUI 스타일 생성
            titleStyle.alignment = TextAnchor.MiddleCenter; // 게임 제목 가운데 정렬
            titleStyle.fontSize = 42; // 게임 제목 글자 크기 설정
            titleStyle.fontStyle = FontStyle.Bold; // 게임 제목 굵게 표시

            GUIStyle subtitleStyle = new GUIStyle(GUI.skin.label); // 보조 제목용 GUI 스타일 생성
            subtitleStyle.alignment = TextAnchor.MiddleCenter; // 보조 제목 가운데 정렬
            subtitleStyle.fontSize = 20; // 보조 제목 글자 크기 설정

            GUIStyle centerStyle = new GUIStyle(GUI.skin.label); // 중앙 안내 문구용 GUI 스타일 생성
            centerStyle.alignment = TextAnchor.MiddleCenter; // 안내 문구 가운데 정렬
            centerStyle.wordWrap = true; // 긴 안내 문구 자동 줄바꿈

            GUI.Box(new Rect(x, y, width, height), string.Empty); // 메인 메뉴 배경 표시
            GUI.Label(new Rect(x + 20f, y + 35f, width - 40f, 60f), gameTitle, titleStyle); // 게임 제목 표시
            GUI.Label(new Rect(x + 20f, y + 95f, width - 40f, 35f), gameSubtitle, subtitleStyle); // 보조 제목 표시

            if (saveManager != null && saveManager.HasSaveFile) // 기존 저장 파일 존재 여부 확인
            {
                GUI.Label(new Rect(x + 45f, y + 145f, width - 90f, 55f), saveManager.GetSaveSummary(), centerStyle); // 저장 기록 요약 표시
            }
            else // 저장 파일이 없는 경우
            {
                GUI.Label(new Rect(x + 45f, y + 145f, width - 90f, 55f), "저장 기록 없음", centerStyle); // 저장 기록 없음 표시
            }

            if (!newGameConfirmation) // 일반 메인 메뉴 상태인지 확인
            {
                if (GUI.Button(new Rect(x + 130f, y + 225f, 300f, 50f), "새 게임")) // 새 게임 버튼 입력 확인
                {
                    RequestNewGame(); // 새 게임 요청 처리
                }

                bool canContinue = saveManager != null && saveManager.HasSaveFile; // 이어하기 가능 여부 계산
                GUI.enabled = canContinue; // 저장 파일이 있을 때만 이어하기 버튼 활성화

                if (GUI.Button(new Rect(x + 130f, y + 290f, 300f, 50f), "이어하기")) // 이어하기 버튼 입력 확인
                {
                    RequestContinueGame(); // 이어하기 요청 처리
                }

                GUI.enabled = true; // 이후 GUI 활성 상태 복원

                if (GUI.Button(new Rect(x + 130f, y + 355f, 300f, 50f), "설정")) // 게임 설정 버튼 입력 확인
                {
                    if (settingsUI != null) // 설정 화면 존재 여부 확인
                    {
                        settingsUI.Open(); // 현재 저장값으로 게임 설정 화면 열기
                    }
                    else // 설정 화면을 찾지 못한 경우
                    {
                        statusMessage = "GameSettingsUI를 찾을 수 없습니다."; // 설정 화면 누락 안내 저장
                    }
                }

                if (GUI.Button(new Rect(x + 130f, y + 420f, 300f, 50f), "게임 종료")) // 게임 종료 버튼 입력 확인
                {
                    QuitGame(); // 게임 종료 실행
                }
            }
            else // 새 게임 저장 기록 삭제 확인 상태인 경우
            {
                GUI.Label(new Rect(x + 55f, y + 215f, width - 110f, 60f), "기존 저장 기록을 삭제하고\n새 게임을 시작하시겠습니까?", centerStyle); // 새 게임 경고 표시

                if (GUI.Button(new Rect(x + 70f, y + 300f, 200f, 50f), "삭제 후 새 게임")) // 새 게임 확정 버튼 입력 확인
                {
                    StartGame(GameStartMode.NewGame); // 새 게임 시작 요청 실행
                }

                if (GUI.Button(new Rect(x + 290f, y + 300f, 200f, 50f), "취소")) // 새 게임 취소 버튼 입력 확인
                {
                    CancelNewGame(); // 덮어쓰기 확인 취소
                }
            }

            if (!string.IsNullOrEmpty(statusMessage)) // 별도 안내 문구 존재 여부 확인
            {
                GUI.Label(new Rect(x + 40f, y + 505f, width - 80f, 40f), statusMessage, centerStyle); // 설정 버튼 아래에 현재 안내 문구 표시
            }
        }
    }
}