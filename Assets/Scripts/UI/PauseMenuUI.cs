using UnityEngine; // Unity 기본 기능 사용
using UnityEngine.InputSystem; // Keyboard.current의 Esc 입력 사용
using UnityEngine.SceneManagement; // 현재 Scene 확인과 MainMenu 이동 사용

namespace ProjectI // 프로젝트 공통 네임스페이스
{
    public class PauseMenuUI : MonoBehaviour // 게임 중 일시정지 메뉴 관리
    {
        public static PauseMenuUI Instance { get; private set; } // 현재 Scene의 일시정지 메뉴 접근점

        enum PauseConfirmation // 일시정지 메뉴의 확인 창 종류
        {
            None, // 확인 창 없음
            AbandonRun, // 던전 탐험 포기 확인
            ReturnToMainMenu // 메인 메뉴 이동 확인
        }

        [Header("Scene 설정")] // Inspector Scene 이름 설정 구분
        [Tooltip("탐험 포기 버튼을 표시할 던전 Scene 이름")] [SerializeField] string dungeonSceneName = "Dungeon"; // 탐험 포기 버튼을 표시할 던전 Scene 이름
        [Tooltip("메인 메뉴 버튼을 표시할 마을 Scene 이름")] [SerializeField] string villageSceneName = "Village"; // 메인 메뉴 버튼을 표시할 마을 Scene 이름
        [Tooltip("마을에서 돌아갈 메인 메뉴 Scene 이름")] [SerializeField] string mainMenuSceneName = "MainMenu"; // 마을에서 돌아갈 메인 메뉴 Scene 이름

        [Header("설정 화면 연결")] // Inspector 설정 UI 연결 구분
        [Tooltip("일시정지 메뉴에서 사용할 게임 설정 화면")] [SerializeField] GameSettingsUI settingsUI; // 일시정지 메뉴에서 사용할 게임 설정 화면

        bool isPaused; // 현재 게임 일시정지 여부
        bool leavingScene; // 현재 다른 Scene으로 이동 중인지 여부
        PauseConfirmation confirmation = PauseConfirmation.None; // 현재 표시할 확인 창 종류
        string statusMessage = string.Empty; // 일시정지 메뉴 안내 문구

        float previousTimeScale = 1f; // 일시정지 전 게임 시간 배율
        CursorLockMode previousCursorLockMode; // 일시정지 전 마우스 잠금 상태
        bool previousCursorVisible; // 일시정지 전 마우스 표시 상태

        PlayerController lockedController; // 일시정지한 플레이어 이동 컴포넌트
        PlayerInteractor lockedInteractor; // 일시정지한 플레이어 상호작용 컴포넌트
        PlayerCombat lockedCombat; // 일시정지한 플레이어 전투 컴포넌트
        bool controllerWasEnabled; // 일시정지 전 이동 컴포넌트 활성 상태
        bool interactorWasEnabled; // 일시정지 전 상호작용 컴포넌트 활성 상태
        bool combatWasEnabled; // 일시정지 전 전투 컴포넌트 활성 상태

        public bool IsPaused => isPaused; // 현재 일시정지 여부 반환

        void Awake() // 일시정지 메뉴 싱글톤과 설정 화면 참조 설정
        {
            if (Instance != null && Instance != this) // 현재 Scene에 다른 일시정지 메뉴가 있는지 확인
            {
                Destroy(gameObject); // 중복 일시정지 메뉴 제거
                return; // 중복 초기화 중단
            }

            Instance = this; // 현재 오브젝트를 일시정지 메뉴 접근점으로 등록

            if (settingsUI == null) // Inspector 설정 화면 연결 여부 확인
            {
                settingsUI = GetComponent<GameSettingsUI>(); // 같은 오브젝트의 설정 화면 검색
            }

            if (settingsUI == null) // 같은 오브젝트에서 설정 화면을 찾지 못했는지 확인
            {
                settingsUI = FindFirstObjectByType<GameSettingsUI>(); // 현재 Scene 전체에서 설정 화면 검색
            }
        }

        void Update() // Esc 입력으로 일시정지 메뉴 열기와 닫기
        {
            Keyboard keyboard = Keyboard.current; // 현재 키보드 입력 가져오기

            if (keyboard == null || !keyboard.escapeKey.wasPressedThisFrame) // Esc 키 입력 여부 확인
            {
                return; // 일시정지 입력 처리 중단
            }

            if (settingsUI != null && settingsUI.IsOpen) // 설정 화면이 열려 있는지 확인
            {
                settingsUI.Close(); // 설정 화면을 닫고 일시정지 메뉴로 복귀
                return; // 일시정지 메뉴 닫기 방지
            }

            if (confirmation != PauseConfirmation.None) // 확인 창이 열려 있는지 확인
            {
                confirmation = PauseConfirmation.None; // 현재 확인 창 닫기
                statusMessage = string.Empty; // 확인 안내 문구 초기화
                return; // 일시정지 메뉴 유지
            }

            if (isPaused) // 현재 게임이 일시정지 상태인지 확인
            {
                ResumeGame(); // 게임 계속하기
            }
            else // 현재 게임이 진행 중인 경우
            {
                PauseGame(); // 게임 일시정지
            }
        }

        void OnDestroy() // Scene 종료 시 일시정지 상태 정리
        {
            if (Instance == this) // 현재 오브젝트가 등록된 일시정지 메뉴인지 확인
            {
                Instance = null; // 전역 일시정지 메뉴 참조 초기화
            }

            if (!leavingScene && isPaused) // 비정상 제거 상태에서 게임이 멈춰 있는지 확인
            {
                RestorePlayerControl(); // 플레이어 컴포넌트 상태 복구
                Time.timeScale = previousTimeScale; // 기존 게임 시간 배율 복구
                Cursor.lockState = previousCursorLockMode; // 기존 마우스 잠금 상태 복구
                Cursor.visible = previousCursorVisible; // 기존 마우스 표시 상태 복구
            }
        }

        bool CanOpenPauseMenu() // 다른 결과 화면과 충돌하지 않는지 확인
        {
            if (RunResultManager.Instance != null && RunResultManager.Instance.HasResult) // 던전 결과 화면 표시 여부 확인
            {
                return false; // 던전 결과 위에 일시정지 메뉴 표시 방지
            }

            if (CampaignManager.Instance != null) // 캠페인 관리자 존재 여부 확인
            {
                CampaignStateData state = CampaignManager.Instance.State; // 현재 캠페인 상태 가져오기

                if (state != null && (state.CampaignWon || state.CampaignFailed)) // 캠페인 결과 화면 표시 여부 확인
                {
                    return false; // 캠페인 결과 위에 일시정지 메뉴 표시 방지
                }

                if (CampaignManager.Instance.HasOpenSettlement) // 빚 납부 화면 진행 여부 확인
                {
                    return false; // 납부 화면과 일시정지 메뉴 중복 방지
                }
            }

            return true; // 일시정지 메뉴 열기 허용
        }

        void PauseGame() // 게임 시간과 플레이어 조작 일시정지
        {
            if (!CanOpenPauseMenu()) // 현재 일시정지 메뉴를 열 수 있는지 확인
            {
                return; // 일시정지 메뉴 열기 중단
            }

            previousTimeScale = Time.timeScale; // 기존 게임 시간 배율 저장
            previousCursorLockMode = Cursor.lockState; // 기존 마우스 잠금 상태 저장
            previousCursorVisible = Cursor.visible; // 기존 마우스 표시 상태 저장

            CaptureAndDisablePlayerControl(); // 플레이어 이동과 전투 및 상호작용 정지

            isPaused = true; // 일시정지 상태 활성화
            confirmation = PauseConfirmation.None; // 이전 확인 창 상태 초기화
            statusMessage = string.Empty; // 이전 안내 문구 초기화
            Time.timeScale = 0f; // 게임 시간 정지
            Cursor.lockState = CursorLockMode.None; // 메뉴 조작을 위해 마우스 잠금 해제
            Cursor.visible = true; // 메뉴 조작을 위해 마우스 커서 표시
        }

        public void ResumeGame() // 일시정지 해제와 게임 계속하기
        {
            if (!isPaused) // 현재 일시정지 상태인지 확인
            {
                return; // 중복 해제 방지
            }

            if (settingsUI != null && settingsUI.IsOpen) // 설정 화면 표시 여부 확인
            {
                settingsUI.Close(); // 설정 화면 닫기
            }

            RestorePlayerControl(); // 플레이어 컴포넌트 활성 상태 복구
            Time.timeScale = previousTimeScale; // 일시정지 전 게임 시간 배율 복구
            Cursor.lockState = previousCursorLockMode; // 일시정지 전 마우스 잠금 상태 복구
            Cursor.visible = previousCursorVisible; // 일시정지 전 마우스 표시 상태 복구

            isPaused = false; // 일시정지 상태 해제
            confirmation = PauseConfirmation.None; // 확인 창 상태 초기화
            statusMessage = string.Empty; // 안내 문구 초기화
        }

        void CaptureAndDisablePlayerControl() // 플레이어 컴포넌트 활성 상태 저장 후 비활성화
        {
            lockedController = FindFirstObjectByType<PlayerController>(); // 현재 Scene의 플레이어 이동 컴포넌트 검색

            if (lockedController != null) // 플레이어 이동 컴포넌트 존재 여부 확인
            {
                lockedInteractor = lockedController.GetComponent<PlayerInteractor>(); // 같은 플레이어의 상호작용 컴포넌트 검색
                lockedCombat = lockedController.GetComponent<PlayerCombat>(); // 같은 플레이어의 전투 컴포넌트 검색
                controllerWasEnabled = lockedController.enabled; // 기존 이동 컴포넌트 활성 상태 저장
                lockedController.enabled = false; // 플레이어 이동과 시점 조작 정지
            }

            if (lockedInteractor != null) // 플레이어 상호작용 컴포넌트 존재 여부 확인
            {
                interactorWasEnabled = lockedInteractor.enabled; // 기존 상호작용 활성 상태 저장
                lockedInteractor.enabled = false; // 플레이어 상호작용 입력 정지
            }

            if (lockedCombat != null) // 플레이어 전투 컴포넌트 존재 여부 확인
            {
                combatWasEnabled = lockedCombat.enabled; // 기존 전투 컴포넌트 활성 상태 저장
                lockedCombat.enabled = false; // 플레이어 공격과 방어 입력 정지
            }
        }

        void RestorePlayerControl() // 일시정지 전에 저장한 플레이어 컴포넌트 상태 복구
        {
            if (lockedController != null) // 저장된 이동 컴포넌트 존재 여부 확인
            {
                lockedController.enabled = controllerWasEnabled; // 기존 이동 활성 상태 복구
            }

            if (lockedInteractor != null) // 저장된 상호작용 컴포넌트 존재 여부 확인
            {
                lockedInteractor.enabled = interactorWasEnabled; // 기존 상호작용 활성 상태 복구
            }

            if (lockedCombat != null) // 저장된 전투 컴포넌트 존재 여부 확인
            {
                lockedCombat.enabled = combatWasEnabled; // 기존 전투 활성 상태 복구
            }

            lockedController = null; // 저장된 이동 컴포넌트 참조 초기화
            lockedInteractor = null; // 저장된 상호작용 컴포넌트 참조 초기화
            lockedCombat = null; // 저장된 전투 컴포넌트 참조 초기화
        }

        void OpenSettings() // 일시정지 메뉴에서 기존 게임 설정 화면 열기
        {
            if (settingsUI == null) // 게임 설정 화면 존재 여부 확인
            {
                statusMessage = "GameSettingsUI를 찾을 수 없습니다."; // 설정 화면 누락 안내 저장
                return; // 설정 화면 열기 중단
            }

            settingsUI.Open(); // 현재 설정값으로 게임 설정 화면 열기
        }

        void RequestAbandonRun() // 던전 탐험 포기 확인 창 열기
        {
            confirmation = PauseConfirmation.AbandonRun; // 탐험 포기 확인 상태 설정
            statusMessage = "현재 들고 있는 물품은 잃고 마차 적재품만 결과에 반영됩니다."; // 탐험 포기 경고 표시
        }

        void ConfirmAbandonRun() // 현재 던전 탐험 포기 결과 확정
        {
            RunResultManager resultManager = RunResultManager.Instance; // 던전 결과 관리자 가져오기

            if (resultManager == null) // 결과 관리자 존재 여부 확인
            {
                statusMessage = "RunResultManager를 찾을 수 없습니다."; // 결과 관리자 누락 안내 저장
                confirmation = PauseConfirmation.None; // 확인 창 상태 초기화
                return; // 탐험 포기 중단
            }

            PrepareForRunResult(); // 일시정지 상태를 해제하고 결과 화면 준비

            if (!resultManager.AbandonCurrentRun()) // 수동 탐험 포기 결과 생성 성공 여부 확인
            {
                statusMessage = "현재 탐험을 포기할 수 없습니다."; // 탐험 포기 실패 안내 저장
            }
        }

        void RequestMainMenu() // Village 상태 확인 후 메인 메뉴 이동 확인
        {
            if (CampaignManager.Instance != null && CampaignManager.Instance.HasOpenSettlement) // 빚 납부 선택 진행 여부 확인
            {
                statusMessage = "빚 납부를 확정한 후 메인 메뉴로 이동할 수 있습니다."; // 이동 제한 안내 저장
                return; // 메인 메뉴 이동 확인 중단
            }

            confirmation = PauseConfirmation.ReturnToMainMenu; // 메인 메뉴 이동 확인 상태 설정
            statusMessage = "현재 마을 상태를 저장하고 메인 메뉴로 이동합니다."; // 메인 메뉴 이동 안내 표시
        }

        void ConfirmReturnToMainMenu() // Village 상태 저장 후 MainMenu Scene으로 이동
        {
            CampaignSaveManager saveManager = CampaignSaveManager.Instance; // 현재 캠페인 저장 관리자 가져오기

            if (saveManager == null) // 저장 관리자 존재 여부 확인
            {
                statusMessage = "CampaignSaveManager를 찾을 수 없습니다."; // 저장 관리자 누락 안내 저장
                confirmation = PauseConfirmation.None; // 확인 창 상태 초기화
                return; // 메인 메뉴 이동 중단
            }

            if (!saveManager.SaveGame()) // 현재 Village 상태 저장 성공 여부 확인
            {
                statusMessage = "저장에 실패하여 메인 메뉴로 이동하지 않았습니다."; // 저장 실패 안내 저장
                confirmation = PauseConfirmation.None; // 확인 창 상태 초기화
                return; // 데이터 손실을 방지하기 위해 이동 중단
            }

            if (CampaignManager.Instance != null) // 캠페인 관리자 존재 여부 확인
            {
                CampaignManager.Instance.SetHudVisible(false); // 메인 메뉴에서 캠페인 HUD 숨김
            }

            PrepareSceneTransition(); // Scene 이동 전 일시정지 상태 정리
            SceneManager.LoadScene(mainMenuSceneName); // MainMenu Scene 로드
        }

        void PrepareForRunResult() // 탐험 포기 결과 화면을 위한 일시정지 상태 정리
        {
            if (settingsUI != null && settingsUI.IsOpen) // 설정 화면 표시 여부 확인
            {
                settingsUI.Close(); // 설정 화면 닫기
            }

            RestorePlayerControl(); // 플레이어 컴포넌트 상태 복구
            isPaused = false; // 일시정지 상태 해제
            confirmation = PauseConfirmation.None; // 확인 창 상태 초기화
            Time.timeScale = 1f; // RunResultManager가 결과를 생성할 수 있도록 시간 복구
            Cursor.lockState = CursorLockMode.None; // 결과 화면을 위해 마우스 잠금 해제
            Cursor.visible = true; // 결과 화면을 위해 마우스 커서 표시
        }

        void PrepareSceneTransition() // 다른 Scene으로 이동하기 전 일시정지 상태 정리
        {
            leavingScene = true; // 정상적인 Scene 이동 상태 표시

            if (settingsUI != null && settingsUI.IsOpen) // 설정 화면 표시 여부 확인
            {
                settingsUI.Close(); // 설정 화면 닫기
            }

            RestorePlayerControl(); // 플레이어 컴포넌트 상태 복구
            isPaused = false; // 일시정지 상태 해제
            confirmation = PauseConfirmation.None; // 확인 창 상태 초기화
            Time.timeScale = 1f; // 다음 Scene을 위해 게임 시간 복구
            Cursor.lockState = CursorLockMode.None; // MainMenu 조작을 위해 마우스 잠금 해제
            Cursor.visible = true; // MainMenu 조작을 위해 마우스 커서 표시
        }

        bool IsDungeonScene() // 현재 Scene이 Dungeon인지 반환
        {
            return SceneManager.GetActiveScene().name == dungeonSceneName; // 현재 Scene 이름과 Dungeon 이름 비교
        }

        bool IsVillageScene() // 현재 Scene이 Village인지 반환
        {
            return SceneManager.GetActiveScene().name == villageSceneName; // 현재 Scene 이름과 Village 이름 비교
        }

        void OnGUI() // OnGUI 기반 일시정지 메뉴 표시
        {
            if (!isPaused) // 현재 일시정지 상태인지 확인
            {
                return; // 일시정지 메뉴 표시 중단
            }

            if (settingsUI != null && settingsUI.IsOpen) // 게임 설정 화면이 열려 있는지 확인
            {
                return; // 설정 화면과 일시정지 메뉴 중복 표시 방지
            }

            if (confirmation != PauseConfirmation.None) // 확인 창 표시 여부 확인
            {
                DrawConfirmationWindow(); // 탐험 포기 또는 메인 메뉴 이동 확인 창 표시
                return; // 기본 일시정지 메뉴 표시 중단
            }

            float width = 520f; // 일시정지 메뉴 너비
            float height = 500f; // 일시정지 메뉴 높이
            float x = (Screen.width - width) * 0.5f; // 화면 가로 중앙 위치 계산
            float y = (Screen.height - height) * 0.5f; // 화면 세로 중앙 위치 계산

            GUIStyle titleStyle = new GUIStyle(GUI.skin.label); // 일시정지 제목용 GUI 스타일 생성
            titleStyle.alignment = TextAnchor.MiddleCenter; // 일시정지 제목 가운데 정렬
            titleStyle.fontSize = 34; // 일시정지 제목 글자 크기 설정
            titleStyle.fontStyle = FontStyle.Bold; // 일시정지 제목 굵게 표시

            GUIStyle centerStyle = new GUIStyle(GUI.skin.label); // 중앙 안내 문구용 GUI 스타일 생성
            centerStyle.alignment = TextAnchor.MiddleCenter; // 안내 문구 가운데 정렬
            centerStyle.wordWrap = true; // 긴 안내 문구 자동 줄바꿈

            GUI.Box(new Rect(0f, 0f, Screen.width, Screen.height), string.Empty); // 게임 화면 입력 차단용 전체 배경 표시
            GUI.Box(new Rect(x, y, width, height), string.Empty); // 일시정지 메뉴 배경 표시
            GUI.Label(new Rect(x + 20f, y + 30f, width - 40f, 55f), "일시정지", titleStyle); // 일시정지 제목 표시

            if (GUI.Button(new Rect(x + 110f, y + 115f, 300f, 50f), "계속하기")) // 계속하기 버튼 입력 확인
            {
                ResumeGame(); // 게임 일시정지 해제
            }

            if (GUI.Button(new Rect(x + 110f, y + 180f, 300f, 50f), "설정")) // 설정 버튼 입력 확인
            {
                OpenSettings(); // 기존 게임 설정 화면 열기
            }

            if (IsDungeonScene()) // 현재 Scene이 Dungeon인지 확인
            {
                if (GUI.Button(new Rect(x + 110f, y + 245f, 300f, 50f), "탐험 포기 후 마을 복귀")) // 탐험 포기 버튼 입력 확인
                {
                    RequestAbandonRun(); // 탐험 포기 확인 창 열기
                }
            }
            else if (IsVillageScene()) // 현재 Scene이 Village인지 확인
            {
                if (GUI.Button(new Rect(x + 110f, y + 245f, 300f, 50f), "저장 후 메인 메뉴")) // 메인 메뉴 이동 버튼 입력 확인
                {
                    RequestMainMenu(); // 메인 메뉴 이동 확인 창 열기
                }
            }

            GUI.Label(new Rect(x + 55f, y + 335f, width - 110f, 60f), statusMessage, centerStyle); // 현재 안내 문구 표시
            GUI.Label(new Rect(x + 55f, y + 420f, width - 110f, 30f), "Esc를 누르면 게임으로 돌아갑니다.", centerStyle); // Esc 조작 안내 표시
        }

        void DrawConfirmationWindow() // 탐험 포기 또는 메인 메뉴 이동 재확인 화면 표시
        {
            float width = 560f; // 확인 창 너비
            float height = 330f; // 확인 창 높이
            float x = (Screen.width - width) * 0.5f; // 화면 가로 중앙 위치 계산
            float y = (Screen.height - height) * 0.5f; // 화면 세로 중앙 위치 계산

            GUIStyle titleStyle = new GUIStyle(GUI.skin.label); // 확인 제목용 GUI 스타일 생성
            titleStyle.alignment = TextAnchor.MiddleCenter; // 확인 제목 가운데 정렬
            titleStyle.fontSize = 26; // 확인 제목 글자 크기 설정
            titleStyle.fontStyle = FontStyle.Bold; // 확인 제목 굵게 표시

            GUIStyle centerStyle = new GUIStyle(GUI.skin.label); // 확인 설명용 GUI 스타일 생성
            centerStyle.alignment = TextAnchor.MiddleCenter; // 확인 설명 가운데 정렬
            centerStyle.wordWrap = true; // 긴 확인 설명 자동 줄바꿈

            bool abandoning = confirmation == PauseConfirmation.AbandonRun; // 현재 탐험 포기 확인인지 계산
            string title = abandoning ? "탐험을 포기하시겠습니까?" : "메인 메뉴로 이동하시겠습니까?"; // 확인 종류에 맞는 제목 계산

            GUI.Box(new Rect(0f, 0f, Screen.width, Screen.height), string.Empty); // 게임 화면 입력 차단용 전체 배경 표시
            GUI.Box(new Rect(x, y, width, height), string.Empty); // 확인 창 배경 표시
            GUI.Label(new Rect(x + 25f, y + 30f, width - 50f, 50f), title, titleStyle); // 확인 창 제목 표시
            GUI.Label(new Rect(x + 55f, y + 100f, width - 110f, 70f), statusMessage, centerStyle); // 현재 확인 내용 표시

            if (GUI.Button(new Rect(x + 70f, y + 215f, 190f, 50f), "확인")) // 확인 버튼 입력 확인
            {
                if (abandoning) // 탐험 포기 확인 종류인지 확인
                {
                    ConfirmAbandonRun(); // 수동 탐험 포기 결과 생성
                }
                else // 메인 메뉴 이동 확인인 경우
                {
                    ConfirmReturnToMainMenu(); // Village 저장 후 MainMenu 이동
                }
            }

            if (GUI.Button(new Rect(x + 300f, y + 215f, 190f, 50f), "취소")) // 취소 버튼 입력 확인
            {
                confirmation = PauseConfirmation.None; // 확인 창 닫기
                statusMessage = string.Empty; // 확인 안내 문구 초기화
            }

            GUI.Label(new Rect(x + 50f, y + 285f, width - 100f, 25f), "Esc를 눌러 취소할 수도 있습니다.", centerStyle); // Esc 취소 안내 표시
        }
    }
}