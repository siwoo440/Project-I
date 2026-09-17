using ProjectI.Core; // 씬 이동
using ProjectI.Interaction; // 조작 잠금
using ProjectI.Loop; // 현재 목적지
using ProjectI.Net; // 협동 나가기
using ProjectI.Persistence; // 저장
using UnityEngine; // 유니티 기본 기능 참조
using UnityEngine.SceneManagement; // 씬 로드 이벤트
using UnityEngine.UI; // uGUI

namespace ProjectI.UI // 메뉴·창 UI 네임스페이스
{
    public sealed class PauseMenu : MonoBehaviour // 게임 중 Esc 창 (계속 · 설정 · 메인 메뉴로 · 종료) — 협동 게임이므로 시간은 멈추지 않음
    {
        private Canvas canvas; // 캔버스
        private RectTransform menuRoot; // 버튼 묶음
        private SettingsPanel settingsPanel; // 설정
        private RetroDialog dialog; // 확인 창
        private RetroHover leaveHover; // 메인 메뉴로 / 방 나가기 글자
        private Text hintLabel; // 제목 아래 안내
        private Button inviteButton; // 친구 초대 (Steam 방)
        private Button copyButton; // 41일차: 방 코드·주소 복사
        private Text codeLabel; // 41일차: 방 코드·주소
        private RetroHover copyHover; // 복사 버튼 글자
        private float copyFlashUntil; // "복사됨" 표시 시각

        public static PauseMenu Instance { get; private set; } // 전역 참조
        public bool IsOpen => canvas != null && canvas.gameObject.activeSelf; // 열림 여부

        private void Awake() // 등록
        {
            Instance = this; // 참조
            SceneManager.sceneLoaded += HandleSceneLoaded; // 씬 이동 시 닫기
        }

        private void OnDestroy() // 해제
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded; // 해제

            if (Instance == this) // 현재 참조
            {
                Instance = null; // 정리
            }
        }

        public bool HandlePausePressed() // Esc 처리 (처리했으면 true — PlayerLook 이 커서 전환을 생략)
        {
            if (IsOpen) // 열린 상태
            {
                Back(); // 한 단계 뒤로
                return true; // 처리함
            }

            if (PlayerControlLock.IsLocked) // 판매·구매 창 등 다른 창 (그 창이 Esc 로 닫힘)
            {
                return false; // 기존 커서 전환 유지
            }

            return Open(); // 열기
        }

        public bool Open() // 열기
        {
            PlayerInteractor interactor = FindAnyObjectByType<PlayerInteractor>(); // 플레이어

            if (interactor == null || !PlayerControlLock.Acquire(this, interactor)) // 게임 중이 아니거나 다른 창
            {
                return false; // 실패
            }

            EnsureBuilt(); // 화면
            RetroUi.EnsureEventSystem(); // UI 입력 (씬이 바뀌었을 수 있음)
            canvas.gameObject.SetActive(true); // 표시
            menuRoot.gameObject.SetActive(true); // 버튼
            leaveHover.SetText(NetworkSession.IsHost ? "방 닫고 메인 메뉴로" : NetworkSession.IsGuest ? "방 나가기" : "메인 메뉴로"); // 협동 상태별 글자
            bool inLobby = ProjectI.Net.Steam.SteamLobbyService.InLobby; // Steam 방
            inviteButton.gameObject.SetActive(inLobby); // Steam 방에서만 초대
            copyButton.gameObject.SetActive(!string.IsNullOrEmpty(NetworkSession.ShareCode)); // 알려 줄 값이 있을 때
            ((RectTransform)copyButton.transform).anchoredPosition = new Vector2(110f, inLobby ? InviteRowY - RowStep : InviteRowY); // 초대 버튼이 없으면 한 줄 위
            copyFlashUntil = 0f; // 초기화
            RefreshCodeLabel(); // 코드
            hintLabel.text = NetworkSession.IsOnline ? $"게임은 계속 진행됩니다  ·  {(NetworkSession.IsHost ? "방장" : "참가")} {NetworkSession.PlayerCount}/{NetworkSession.MaxPlayers}명" : "게임은 계속 진행됩니다"; // 안내
            return true; // 성공
        }

        private const float InviteRowY = -660f; // 친구 초대 줄 (버튼 4개 아래)
        private const float RowStep = 70f; // 줄 간격

        private void Update() // 로비 생성이 늦게 끝나도 코드 표시
        {
            if (IsOpen) // 열림
            {
                RefreshCodeLabel(); // 갱신
            }
        }

        private void RefreshCodeLabel() // 방 코드·주소 줄
        {
            string share = NetworkSession.ShareCode; // 알려 줄 값

            if (string.IsNullOrEmpty(share)) // 없음
            {
                codeLabel.text = NetworkSession.IsOnline && NetworkSession.Transport == SessionTransport.Steam ? "방 코드 준비 중..." : string.Empty; // 로비 생성 대기
                copyButton.gameObject.SetActive(false); // 숨김
                return; // 종료
            }

            copyButton.gameObject.SetActive(true); // 표시
            copyHover.SetText(NetworkSession.ShareIsCode ? "코드 복사" : "주소 복사"); // 글자
            string title = NetworkSession.ShareIsCode ? "방 코드" : "주소"; // 제목
            string scope = NetworkSession.ShareIsCode && NetworkSession.IsHost && NetworkSession.HostVisibility == RoomVisibility.CodeOnly ? "  (코드 전용)" : string.Empty; // 공개 범위
            string copied = Time.unscaledTime < copyFlashUntil ? "  ·  복사됨" : string.Empty; // 복사 표시
            codeLabel.text = $"{title}  {share}{scope}{copied}"; // 표시
        }

        private void CopyShareCode() // 코드·주소를 클립보드로
        {
            string share = NetworkSession.ShareCode; // 값

            if (string.IsNullOrEmpty(share)) // 없음
            {
                return; // 생략
            }

            GUIUtility.systemCopyBuffer = share; // 복사
            copyFlashUntil = Time.unscaledTime + 2f; // 표시
            RefreshCodeLabel(); // 갱신
        }

        public void Close() // 닫기 (게임으로)
        {
            if (!IsOpen) // 이미 닫힘
            {
                return; // 생략
            }

            if (settingsPanel.IsOpen) // 설정 열림
            {
                settingsPanel.Close(); // 저장하며 닫기
            }

            canvas.gameObject.SetActive(false); // 숨김
            PlayerControlLock.Release(this); // 조작 복구 (커서 잠금)
        }

        public void Back() // 한 단계 뒤로
        {
            if (dialog.IsOpen) // 확인 창
            {
                dialog.Cancel(); // 취소
            }
            else if (settingsPanel.IsOpen) // 설정
            {
                settingsPanel.Close(); // 버튼 목록으로
            }
            else
            {
                Close(); // 게임으로
            }
        }

        private void EnsureBuilt() // 처음 열 때 구성
        {
            if (canvas != null) // 이미 있음
            {
                return; // 생략
            }

            canvas = RetroUi.CreateCanvas("PauseMenuCanvas", 500, transform); // 다른 HUD 위
            RectTransform root = (RectTransform)canvas.transform; // 루트
            RetroUi.Solid(root, "Shade", new Color(0.02f, 0.015f, 0.01f, 0.72f), true); // 게임 화면 어둡게
            RetroUi.CornerBrackets(root, RetroUi.Orange, 70f, 6f, 26f); // 모서리 꺾쇠

            menuRoot = RetroUi.Rect(root, "Menu"); // 버튼 묶음
            Text title = RetroUi.Label(menuRoot, "Title", "일시정지", 60, RetroUi.Orange, TextAnchor.MiddleLeft); // 제목
            RetroUi.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(120f, -220f), new Vector2(600f, 80f)); // 왼쪽 위
            hintLabel = RetroUi.Label(menuRoot, "Hint", "게임은 계속 진행됩니다", 22, RetroUi.OrangeDim, TextAnchor.MiddleLeft); // 안내
            RetroUi.Place(hintLabel.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(126f, -275f), new Vector2(600f, 34f)); // 제목 아래
            codeLabel = RetroUi.Label(menuRoot, "ShareCode", string.Empty, 28, RetroUi.OrangeBright, TextAnchor.MiddleLeft); // 방 코드·주소
            RetroUi.Place(codeLabel.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(126f, -318f), new Vector2(900f, 40f)); // 안내 아래

            string[] labels = { "계속하기", "설정", "메인 메뉴로", "게임 종료" }; // 버튼
            System.Action[] actions = { Close, OpenSettings, AskMainMenu, AskQuit }; // 동작
            float y = -380f; // 첫 줄

            for (int index = 0; index < labels.Length; index++) // 버튼
            {
                Button button = RetroUi.TextButton(menuRoot, $"Pause_{labels[index]}", labels[index], 36, actions[index]); // 버튼
                RetroUi.Place((RectTransform)button.transform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(110f, y), new Vector2(460f, 60f)); // 위치
                y -= 70f; // 다음 줄

                if (index == 2) // 메인 메뉴로
                {
                    leaveHover = button.GetComponent<RetroHover>(); // 글자 변경용
                }
            }

            inviteButton = RetroUi.TextButton(menuRoot, "Pause_Invite", "친구 초대 (Steam)", 36, ProjectI.Net.Steam.SteamLobbyService.OpenInviteOverlay); // 친구 초대
            RetroUi.Place((RectTransform)inviteButton.transform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(110f, InviteRowY), new Vector2(460f, 60f)); // 버튼 4개 아래
            copyButton = RetroUi.TextButton(menuRoot, "Pause_CopyCode", "코드 복사", 36, CopyShareCode); // 코드·주소 복사
            RetroUi.Place((RectTransform)copyButton.transform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(110f, InviteRowY - RowStep), new Vector2(460f, 60f)); // 초대 아래
            copyHover = copyButton.GetComponent<RetroHover>(); // 글자 변경용
            settingsPanel = SettingsPanel.Create(root); // 설정
            dialog = RetroDialog.Create(root); // 확인 창
            canvas.gameObject.SetActive(false); // 처음엔 닫힘
        }

        private void OpenSettings() // 설정
        {
            menuRoot.gameObject.SetActive(false); // 버튼 숨김
            settingsPanel.Open(() => menuRoot.gameObject.SetActive(true)); // 닫히면 버튼
        }

        private void AskMainMenu() // 메인 메뉴로
        {
            string message = NetworkSession.IsGuest ? "방에서 나가 메인 메뉴로 갈까요?\n(참가 중에는 저장하지 않습니다)"
                : IsSafeOffice() ? (NetworkSession.IsHost ? "진행 상황을 저장하고 방을 닫을까요?\n참가한 원정대원의 연결이 끊깁니다." : "진행 상황을 저장하고 메인 메뉴로 나갈까요?")
                : "원정(이동) 중입니다. 나가면 오늘 원정은 사무소 출발 전 저장 시점부터 다시 시작합니다.\n메인 메뉴로 나갈까요?"; // 안내
            dialog.Show(message, "메인 메뉴로", LeaveToMainMenu); // 확인
        }

        private void AskQuit() // 종료
        {
            string message = IsSafeOffice() ? "진행 상황을 저장하고 게임을 종료할까요?" : "원정(이동) 중입니다. 종료하면 오늘 원정은 사무소 출발 전 저장 시점부터 다시 시작합니다.\n종료할까요?"; // 안내
            dialog.Show(message, "종료", () => { SaveIfSafe(); NetworkSession.Leave(); if (ProjectServices.TryGet(out SceneFlowManager flow)) flow.QuitGame(); }); // 확인
        }

        private void LeaveToMainMenu() // 저장 후 메뉴 씬
        {
            SaveIfSafe(); // 저장 (참가자는 저장하지 않음)
            NetworkSession.Leave(); // 협동 연결 종료
            Close(); // 조작 잠금 해제 (씬 이동 전)

            if (ProjectServices.TryGet(out SceneFlowManager flow)) // 씬 관리자
            {
                flow.LoadMainMenu(); // 이동
            }
        }

        private static bool IsSafeOffice() // 사무소에 정차 중인지
        {
            PersistentMapLoader loader = PersistentMapLoader.Instance; // 맵 로더
            return loader != null && !loader.IsTransitioning && loader.CurrentDestination == TravelDestination.Office; // 사무소
        }

        private static void SaveIfSafe() // 사무소면 체크포인트 저장
        {
            if (IsSafeOffice() && DailySnapshotService.Instance != null) // 저장 가능
            {
                DailySnapshotService.Instance.SaveSafeOfficeCheckpoint(); // 저장
            }
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode) // 씬 교체 시 닫기
        {
            if (mode == LoadSceneMode.Single && IsOpen) // 단일 로드 (메뉴 이동 등)
            {
                canvas.gameObject.SetActive(false); // 숨김
                PlayerControlLock.Release(this); // 잠금 정리
            }
        }
    }
}
