using ProjectI.Core; // 프로젝트 핵심 기능 참조
using ProjectI.Diagnostics; // 프로젝트 로그 참조
using ProjectI.Net; // 37일차 협동
using ProjectI.UI; // 메뉴 UI
using UnityEngine; // 유니티 기본 기능 참조
using UnityEngine.InputSystem; // Esc 입력
using UnityEngine.UI; // uGUI

namespace ProjectI.Scenes // 씬 기능 네임스페이스
{
    public sealed class MainMenuSceneController : MonoBehaviour // 메인 메뉴 (이어하기 · 서버 · 새 게임 · 설정 · 종료)
    {
        [SerializeField] private MenuCameraRig cameraRig; // 배경 카메라
        private RectTransform menuRoot; // 메뉴 목록 묶음
        private CanvasGroup menuGroup; // 메뉴 투명도
        private Button continueButton; // 이어하기
        private RetroHover continueHover; // 이어하기 글자
        private Text noticeLabel; // 안내 줄
        private ServerListPanel serverPanel; // 서버 목록
        private SettingsPanel settingsPanel; // 설정
        private RetroDialog dialog; // 확인 창
        private bool leaving; // 씬 이동 중
        private int continueDay; // 이어하기 일차

        public ServerListPanel ServerPanel => serverPanel; // 검증용
        public MenuCameraRig CameraRig => cameraRig; // 검증용
        public bool MenuVisible => menuRoot != null && menuRoot.gameObject.activeSelf; // 검증용

        public void Configure(MenuCameraRig rig) // 에디터 배치 도구용
        {
            cameraRig = rig; // 카메라
        }

        private void Start() // 메뉴 구성
        {
            ProjectLog.Log("MainMenu 씬 시작"); // 기록
            GameManager.Instance?.SetState(GameState.MainMenu); // 상태 (에디터에서 바로 열었을 때 포함)
            Cursor.lockState = CursorLockMode.None; // 커서 풀기
            Cursor.visible = true; // 표시
            RetroUi.EnsureEventSystem(); // UI 입력
            Build(); // 화면

            if (cameraRig != null) // 배경 카메라
            {
                cameraRig.SnapTo(MenuCameraPose.Main); // 전경
            }

            NetworkSession.StatusChanged += HandleNetworkStatus; // 연결 상태
            string message = NetworkSession.TakePendingMessage(); // 연결이 끊겨 돌아온 경우

            if (!string.IsNullOrEmpty(message)) // 안내
            {
                noticeLabel.text = message; // 표시
            }
        }

        private void OnDestroy() // 해제
        {
            NetworkSession.StatusChanged -= HandleNetworkStatus; // 해제
        }

        private void HandleNetworkStatus(string text) // 연결 상태 표시
        {
            if (serverPanel != null && serverPanel.IsOpen) // 서버 창
            {
                serverPanel.ShowStatus(text); // 상태 줄
            }

            if (!NetworkSession.IsOnline && !leaving && serverPanel != null) // 연결 실패·종료
            {
                serverPanel.SetConnecting(false); // 다시 허용
            }
        }

        private void HostGame(ushort port) // 방 만들기 (현재 저장으로 방장 시작, 저장이 없으면 1일차)
        {
            if (leaving || Flow() == null) // 이동 중
            {
                return; // 생략
            }

            if (!NetworkSession.BeginHost(port, out string error)) // 실패
            {
                serverPanel.ShowStatus(error); // 이유
                return; // 종료
            }

            leaving = true; // 중복 방지
            serverPanel.SetConnecting(true); // 버튼 잠금
            serverPanel.ShowStatus(continueButton.interactable ? $"{continueDay}일차 저장으로 방을 엽니다..." : "1일차부터 방을 엽니다..."); // 안내
            Flow().ContinueGame(); // 게임 월드 → 준비되면 방 열림
        }

        private void JoinGame(string address, ushort port) // 주소로 참가
        {
            if (leaving) // 이동 중
            {
                return; // 생략
            }

            if (!NetworkSession.BeginJoin(address, port, out string error)) // 실패
            {
                serverPanel.ShowStatus(error); // 이유
                return; // 종료
            }

            serverPanel.SetConnecting(true); // 버튼 잠금 (연결되면 세션이 게임 월드를 불러옴)
        }

        private void Update() // Esc 뒤로 가기
        {
            Keyboard keyboard = Keyboard.current; // 키보드

            if (keyboard == null || !keyboard.escapeKey.wasPressedThisFrame || leaving) // 입력 없음
            {
                return; // 생략
            }

            if (dialog.IsOpen) // 확인 창
            {
                dialog.Cancel(); // 취소
            }
            else if (settingsPanel.IsOpen) // 설정
            {
                settingsPanel.Close(); // 닫기
            }
            else if (serverPanel.IsOpen) // 서버 목록
            {
                if (NetworkSession.IsGuest) // 연결 시도 중
                {
                    NetworkSession.Leave(); // 취소
                    serverPanel.SetConnecting(false); // 다시 허용
                    return; // 창은 유지
                }

                serverPanel.Close(); // 닫기 → 카메라 복귀
            }
        }

        public void OpenServers() // 서버 목록 (카메라 이동 후 창 표시)
        {
            if (leaving || cameraRig != null && cameraRig.IsMoving) // 이동 중
            {
                return; // 생략
            }

            SetMenuVisible(false); // 메뉴 숨김

            if (cameraRig == null) // 카메라 없음
            {
                serverPanel.Open(); // 바로 표시
                return; // 종료
            }

            cameraRig.MoveTo(MenuCameraPose.Servers, () => serverPanel.Open()); // 이동 후 표시
        }

        private void Build() // 화면 구성
        {
            Canvas canvas = RetroUi.CreateCanvas("MainMenuCanvas", 10, transform); // 캔버스
            RectTransform root = (RectTransform)canvas.transform; // 루트
            RetroUi.CornerBrackets(root, RetroUi.Orange, 70f, 6f, 26f); // 모서리 꺾쇠

            menuRoot = RetroUi.Rect(root, "Menu"); // 메뉴 묶음
            menuGroup = menuRoot.gameObject.AddComponent<CanvasGroup>(); // 투명도
            Image gradient = RetroUi.Solid(menuRoot, "LeftShade", new Color(0f, 0f, 0f, 0.55f)); // 왼쪽 어둡게 (글자 가독성)
            gradient.rectTransform.anchorMax = new Vector2(0.36f, 1f); // 화면 왼쪽 36%

            Text title = RetroUi.Label(menuRoot, "Title", "PROJECT  I", 84, RetroUi.Orange, TextAnchor.MiddleLeft); // 제목
            RetroUi.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(120f, -210f), new Vector2(700f, 110f)); // 왼쪽 위
            Text subtitle = RetroUi.Label(menuRoot, "Subtitle", "원정 사무소  ·  지하 회수 작업", 26, RetroUi.OrangeDim, TextAnchor.MiddleLeft); // 부제
            RetroUi.Place(subtitle.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(126f, -278f), new Vector2(700f, 40f)); // 제목 아래

            string[] labels = { "이어하기", "서버", "새 게임", "설정", "종료" }; // 메뉴 (이어하기 아래 서버)
            System.Action[] actions = { Continue, OpenServers, NewGame, OpenSettings, Quit }; // 동작
            float y = -400f; // 첫 줄

            for (int index = 0; index < labels.Length; index++) // 버튼
            {
                Button button = RetroUi.TextButton(menuRoot, $"Menu_{labels[index]}", labels[index], 38, actions[index]); // 버튼
                RetroUi.Place((RectTransform)button.transform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(110f, y), new Vector2(460f, 62f)); // 위치
                y -= 72f; // 다음 줄

                if (index == 0) // 이어하기
                {
                    continueButton = button; // 저장
                    continueHover = button.GetComponent<RetroHover>(); // 글자
                }
            }

            noticeLabel = RetroUi.Label(menuRoot, "Notice", string.Empty, 22, RetroUi.OrangeBright, TextAnchor.MiddleLeft); // 안내
            RetroUi.Place(noticeLabel.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0.5f), new Vector2(126f, 150f), new Vector2(900f, 34f)); // 아래
            Text version = RetroUi.Label(root, "Version", $"v{Application.version}", 22, RetroUi.OrangeDim, TextAnchor.LowerLeft); // 버전
            RetroUi.Place(version.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(40f, 40f), new Vector2(300f, 30f)); // 왼쪽 아래

            serverPanel = ServerListPanel.Create(root, new SampleServerListProvider()); // 서버 목록
            serverPanel.Closed += ReturnFromServers; // 닫힘
            serverPanel.HostRequested += HostGame; // 방 만들기
            serverPanel.JoinRequested += JoinGame; // 참가
            settingsPanel = SettingsPanel.Create(root); // 설정
            dialog = RetroDialog.Create(root); // 확인 창
            RefreshContinue(); // 이어하기 상태
        }

        private void RefreshContinue() // 저장 확인
        {
            SceneFlowManager flow = Flow(); // 씬 관리자
            bool available = flow != null && flow.TryGetContinueDay(out continueDay); // 저장 여부
            continueButton.interactable = available; // 저장 없으면 끔
            continueHover.SetText(available ? $"이어하기  ({continueDay}일차)" : "이어하기"); // 일차 표시
        }

        private void SetMenuVisible(bool visible) // 메뉴 표시
        {
            menuRoot.gameObject.SetActive(visible); // 표시

            if (visible) // 다시 보임
            {
                menuGroup.alpha = 1f; // 불투명
                noticeLabel.text = string.Empty; // 안내 정리
            }
        }

        private void ReturnFromServers() // 서버 목록 닫힘 → 카메라 복귀 후 메뉴
        {
            if (cameraRig == null) // 카메라 없음
            {
                SetMenuVisible(true); // 바로 표시
                return; // 종료
            }

            cameraRig.MoveTo(MenuCameraPose.Main, () => SetMenuVisible(true)); // 복귀 후 표시
        }

        private void Continue() // 이어하기
        {
            SceneFlowManager flow = Flow(); // 씬 관리자

            if (flow == null || leaving) // 없음
            {
                return; // 생략
            }

            leaving = true; // 중복 방지
            noticeLabel.text = $"{continueDay}일차 사무소로 이동합니다..."; // 안내
            flow.ContinueGame(); // 이동
        }

        private void NewGame() // 새 게임
        {
            if (Flow() == null || leaving) // 없음
            {
                return; // 생략
            }

            if (continueButton.interactable) // 기존 저장 있음
            {
                dialog.Show($"{continueDay}일차까지의 진행을 보관하고 1일차부터 새로 시작할까요?\n(기존 저장은 SavesArchive 폴더로 옮겨집니다)", "새로 시작", StartNewGame); // 확인
                return; // 대기
            }

            StartNewGame(); // 바로 시작
        }

        private void StartNewGame() // 새 게임 실행
        {
            leaving = true; // 중복 방지
            noticeLabel.text = "1일차 사무소로 이동합니다..."; // 안내

            if (!Flow().StartNewGame()) // 보관 실패
            {
                leaving = false; // 다시 허용
                noticeLabel.text = "기존 저장을 옮기지 못해 새 게임을 시작하지 않았습니다. (파일이 사용 중인지 확인)"; // 안내
            }
        }

        private void OpenSettings() // 설정
        {
            SetMenuVisible(false); // 메뉴 숨김
            settingsPanel.Open(() => SetMenuVisible(true)); // 닫히면 메뉴
        }

        private void Quit() // 종료
        {
            dialog.Show("게임을 종료할까요?", "종료", () => Flow()?.QuitGame()); // 확인
        }

        private static SceneFlowManager Flow() // 씬 관리자
        {
            if (!ProjectServices.TryGet(out SceneFlowManager flow)) // 없음
            {
                ProjectLog.Error("SceneFlowManager가 없습니다."); // 오류
            }

            return flow; // 반환
        }
    }
}
