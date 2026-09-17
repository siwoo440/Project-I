using ProjectI.Economy; // 공동 자금·채무
using ProjectI.Interaction; // 조사 안내·조작 잠금
using ProjectI.Loop; // 원정 결과
using ProjectI.Net; // 협동 인원
using ProjectI.Persistence; // 일차·단계
using ProjectI.Player; // 체력·기력·시점
using ProjectI.TimeOfDay; // 시각
using UnityEngine; // 유니티 기본 기능 참조
using UnityEngine.SceneManagement; // 씬 이동
using UnityEngine.UI; // uGUI

namespace ProjectI.UI // 메뉴·창 UI 네임스페이스
{
    public sealed class GameHud : MonoBehaviour // 게임 중 정식 HUD (일차·시각·자금·채무 / 안내 / 조준점·조사 / 체력·기력 / 알림)
    {
        private const float SearchInterval = 0.5f; // 참조 다시 찾는 간격
        private Canvas canvas; // 캔버스
        private Text dayLabel; // 일차·단계
        private Text clockLabel; // 시각
        private Text fundsLabel; // 공동 자금
        private Text debtLabel; // 채무
        private Text netLabel; // 협동 인원
        private RectTransform statusPanel; // 상태 상자
        private Text guideLabel; // 할 일 안내
        private RectTransform reportBox; // 원정 결과
        private Text reportLabel; // 원정 결과 글자
        private RectTransform crosshair; // 조준점
        private RectTransform promptBox; // 조사 안내
        private Text promptLabel; // 조사 안내 글자
        private RectTransform holdTrack; // 길게 누르기 막대 바탕
        private RectTransform holdFill; // 길게 누르기 진행
        private RectTransform healthFill; // 체력 막대
        private Text healthLabel; // 체력 수치
        private RectTransform staminaFill; // 기력 막대
        private Text staminaLabel; // 기력 수치
        private Image staminaImage; // 기력 색
        private RectTransform noticeBox; // 알림
        private Text noticeLabel; // 알림 글자
        private Text voiceLabel; // 43일차: 내 마이크 상태
        private float noticeUntil; // 알림 종료
        private float nextSearch; // 다음 참조 검색
        private PlayerInteractor interactor; // 플레이어
        private PlayerHealth health; // 체력
        private PlayerStamina stamina; // 기력
        private PlayerLook look; // 시점
        private GameTimeController clock; // 시각
        private CampaignEconomy economy; // 공동 자금
        private DebtLedger ledger; // 채무
        private ExpeditionReportTracker reportTracker; // 원정 결과
        private int lastFunds = -1; // 마지막 공동 자금 (사무소가 내려가도 표시)
        private string lastDebt = string.Empty; // 마지막 채무 문구

        public static GameHud Instance { get; private set; } // 전역 참조
        public bool IsVisible => canvas != null && canvas.gameObject.activeSelf; // 검증용
        public string PromptText => promptLabel == null || !promptBox.gameObject.activeSelf ? string.Empty : promptLabel.text; // 검증용
        public string FundsText => fundsLabel == null ? string.Empty : fundsLabel.text; // 검증용
        public string DayText => dayLabel == null ? string.Empty : dayLabel.text; // 검증용
        public string NoticeText => noticeBox != null && noticeBox.gameObject.activeSelf ? noticeLabel.text : string.Empty; // 검증용

        public static void ShowNotice(string text, float seconds = 2.5f) // 화면 가운데 아래 짧은 알림
        {
            if (Instance == null || string.IsNullOrEmpty(text)) // HUD 없음
            {
                return; // 생략
            }

            Instance.EnsureBuilt(); // 화면
            Instance.noticeLabel.text = text; // 문구
            Instance.noticeUntil = Time.unscaledTime + seconds; // 표시 시간
            Instance.noticeBox.gameObject.SetActive(true); // 표시
        }

        private void Awake() // 등록
        {
            Instance = this; // 참조
            SceneManager.sceneLoaded += HandleSceneLoaded; // 씬 교체
        }

        private void OnDestroy() // 해제
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded; // 해제

            if (Instance == this) // 현재 참조
            {
                Instance = null; // 정리
            }
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode) // 씬 교체 시 참조 다시 찾기
        {
            nextSearch = 0f; // 바로 검색

            if (mode == LoadSceneMode.Single) // 게임 ↔ 메뉴
            {
                lastFunds = -1; // 이전 게임 값 정리
                lastDebt = string.Empty; // 정리
            }
        }

        private void LateUpdate() // 매 프레임 갱신
        {
            ResolveReferences(); // 참조

            if (interactor == null) // 게임 중이 아님 (부트·메뉴)
            {
                if (canvas != null && canvas.gameObject.activeSelf) // 표시 중
                {
                    canvas.gameObject.SetActive(false); // 숨김
                }

                return; // 종료
            }

            EnsureBuilt(); // 화면

            if (!canvas.gameObject.activeSelf) // 숨김 상태
            {
                canvas.gameObject.SetActive(true); // 표시
            }

            UpdateStatus(); // 일차·시각·자금
            UpdateCenter(); // 조준점·조사
            UpdateBars(); // 체력·기력
            UpdateNotice(); // 알림
            UpdateVoice(); // 43일차: 마이크 상태
        }

        private void UpdateVoice() // 43일차: 왼쪽 아래 마이크 상태 (협동 중에만)
        {
            bool show = ProjectI.Net.NetworkSession.IsOnline; // 협동 중

            if (voiceLabel.gameObject.activeSelf != show) // 바뀜
            {
                voiceLabel.gameObject.SetActive(show); // 표시
            }

            if (!show) // 혼자
            {
                return; // 종료
            }

            if (!ProjectI.Settings.GameSettings.VoiceEnabled) // 설정에서 끔
            {
                voiceLabel.text = "음성 꺼짐 (설정)"; // 글자
                voiceLabel.color = RetroUi.Disabled; // 색
            }
            else if (ProjectI.Net.Voice.VoiceCapture.SelfMuted) // 마이크 끔
            {
                voiceLabel.text = "마이크 꺼짐 (Esc)"; // 글자
                voiceLabel.color = RetroUi.Red; // 색
            }
            else if (ProjectI.Net.Voice.VoiceCapture.IsTransmitting) // 말하는 중
            {
                voiceLabel.text = "● 말하는 중"; // 글자
                voiceLabel.color = RetroUi.Green; // 색
            }
            else // 대기
            {
                voiceLabel.text = ProjectI.Settings.GameSettings.PushToTalk ? "V 누르고 말하기" : "마이크 켜짐"; // 글자
                voiceLabel.color = RetroUi.OrangeDim; // 색
            }
        }

        private void ResolveReferences() // 필요한 참조 찾기 (없을 때만 가끔)
        {
            if (Time.unscaledTime < nextSearch) // 아직
            {
                return; // 생략
            }

            nextSearch = Time.unscaledTime + SearchInterval; // 다음

            if (interactor == null) // 플레이어
            {
                interactor = FindAnyObjectByType<PlayerInteractor>(); // 조회
                health = interactor == null ? null : interactor.GetComponent<PlayerHealth>(); // 체력
                stamina = interactor == null ? null : interactor.GetComponent<PlayerStamina>(); // 기력
                look = interactor == null ? null : interactor.GetComponent<PlayerLook>(); // 시점
            }

            if (clock == null) // 시각
            {
                clock = FindAnyObjectByType<GameTimeController>(); // 조회
            }

            if (economy == null) // 공동 자금 (사무소에 있을 때만)
            {
                economy = FindAnyObjectByType<CampaignEconomy>(); // 조회
            }

            if (ledger == null) // 채무
            {
                ledger = FindAnyObjectByType<DebtLedger>(); // 조회
            }

            if (reportTracker == null) // 원정 결과
            {
                reportTracker = FindAnyObjectByType<ExpeditionReportTracker>(); // 조회
            }
        }

        private void UpdateStatus() // 왼쪽 위 상태
        {
            DailySnapshotService service = DailySnapshotService.Instance; // 저장 서비스
            bool ready = service != null && service.IsInitialized; // 준비
            dayLabel.text = ready ? $"{service.CurrentDay}일차  ·  {ExpeditionReportTracker.PhaseTitle(service.DayPhase)}" : "불러오는 중..."; // 일차

            if (clock != null) // 시각
            {
                float hour = Mathf.Repeat(clock.CurrentHour, 24f); // 0~24
                int hours = Mathf.FloorToInt(hour); // 시
                int minutes = Mathf.FloorToInt((hour - hours) * 60f); // 분
                clockLabel.text = $"{hours:00}:{minutes:00}  {PhaseName(clock.CurrentPhase)}"; // 표시
            }
            else
            {
                clockLabel.text = string.Empty; // 없음
            }

            if (economy != null) // 사무소에 있음
            {
                lastFunds = economy.SharedFunds; // 최신 값
            }

            fundsLabel.text = lastFunds < 0 ? "공동 자금  -" : $"공동 자금  {lastFunds:N0}"; // 자금

            if (ledger != null) // 채무
            {
                lastDebt = ledger.IsCompleted ? "채무  모두 상환" : $"채무  {ledger.CurrentPhase}단계 · 남은 {ledger.RemainingDebt:N0}"; // 최신 문구
            }

            debtLabel.text = lastDebt; // 채무
            bool online = NetworkSession.IsOnline; // 협동 중
            netLabel.text = online ? $"{(NetworkSession.IsHost ? "방장" : "참가")}  ·  원정대 {NetworkSession.PlayerCount}/{NetworkSession.MaxPlayers}" : string.Empty; // 인원
            statusPanel.sizeDelta = new Vector2(430f, online ? 206f : 176f); // 줄 수에 맞춤
            guideLabel.text = ready ? ExpeditionReportTracker.PhaseGuide(service.DayPhase) : string.Empty; // 할 일

            ExpeditionReport report = reportTracker == null ? null : reportTracker.LastReport; // 원정 결과
            bool showReport = reportTracker != null && reportTracker.IsReportVisible && report != null; // 표시 여부
            reportBox.gameObject.SetActive(showReport); // 표시

            if (showReport) // 내용
            {
                reportLabel.text = report.Failed
                    ? $"원정 실패  —  잃은 물건 {report.LostOnFailureCount}개 (가치 {report.LostOnFailureValue:N0})"
                    : $"원정 결과  —  귀환 {report.ReturnedCount}개 · 새 회수품 {report.NewLootCount}개 (가치 {report.NewLootValue:N0}) · 두고 온 장비 {report.LostBroughtCount}개"; // 문구
            }
        }

        private void UpdateCenter() // 조준점·조사 안내
        {
            bool locked = look != null && look.IsCursorLocked && !PlayerControlLock.IsLocked; // 조작 중
            crosshair.gameObject.SetActive(locked); // 조준점
            string prompt = locked && interactor.HasTarget ? interactor.PromptText : string.Empty; // 안내
            bool showPrompt = !string.IsNullOrEmpty(prompt); // 표시 여부
            promptBox.gameObject.SetActive(showPrompt); // 표시

            if (!showPrompt) // 없음
            {
                return; // 종료
            }

            promptLabel.text = prompt; // 문구
            bool hold = interactor.CurrentInteractionType == InteractionType.Hold; // 길게 누르기
            holdTrack.gameObject.SetActive(hold); // 막대
            SetFill(holdFill, hold ? interactor.HoldProgress : 0f); // 진행
        }

        private void UpdateBars() // 체력·기력
        {
            if (health != null) // 체력
            {
                SetFill(healthFill, health.Normalized); // 막대
                healthLabel.text = $"체력  {health.CurrentHealth:0} / {health.MaxHealth:0}"; // 수치
            }

            if (stamina != null) // 기력
            {
                SetFill(staminaFill, stamina.Normalized); // 막대
                staminaLabel.text = $"기력  {stamina.CurrentStamina:0} / {stamina.MaxStamina:0}"; // 수치
                staminaImage.color = stamina.IsExhausted ? RetroUi.Red : RetroUi.OrangeBright; // 탈진 표시
            }
        }

        private void UpdateNotice() // 알림 시간
        {
            if (noticeBox.gameObject.activeSelf && Time.unscaledTime >= noticeUntil) // 끝남
            {
                noticeBox.gameObject.SetActive(false); // 숨김
            }
        }

        private static void SetFill(RectTransform fill, float value) // 막대 채움
        {
            fill.anchorMax = new Vector2(Mathf.Clamp01(value), 1f); // 비율
        }

        private static string PhaseName(GameTimePhase phase) // 시간대 이름
        {
            switch (phase)
            {
                case GameTimePhase.Dawn: return "새벽";
                case GameTimePhase.Dusk: return "저녁";
                case GameTimePhase.Night: return "밤";
                default: return "낮";
            }
        }

        private void EnsureBuilt() // 처음 표시할 때 구성
        {
            if (canvas != null) // 이미 있음
            {
                return; // 생략
            }

            canvas = RetroUi.CreateCanvas("GameHudCanvas", 100, transform); // 캔버스 (일시정지 창 아래)
            canvas.GetComponent<GraphicRaycaster>().enabled = false; // HUD 는 클릭을 받지 않음
            RectTransform root = (RectTransform)canvas.transform; // 루트

            RectTransform status = Panel(root, "Status", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28f, -28f), new Vector2(430f, 176f)); // 왼쪽 위 상태
            statusPanel = status; // 크기 조절용
            dayLabel = Line(status, "Day", 28, RetroUi.Orange, 14f); // 일차
            clockLabel = Line(status, "Clock", 22, RetroUi.OrangeBright, 56f); // 시각
            fundsLabel = Line(status, "Funds", 24, RetroUi.Orange, 92f); // 자금
            debtLabel = Line(status, "Debt", 20, RetroUi.OrangeDim, 130f); // 채무
            netLabel = Line(status, "Net", 20, RetroUi.Green, 162f); // 협동 인원

            guideLabel = RetroUi.Label(root, "Guide", string.Empty, 20, RetroUi.OrangeDim, TextAnchor.UpperCenter); // 할 일
            RetroUi.Place(guideLabel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(900f, 30f)); // 위 가운데

            reportBox = Panel(root, "Report", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -70f), new Vector2(1000f, 56f)); // 원정 결과
            reportLabel = RetroUi.Label(reportBox, "Text", string.Empty, 22, RetroUi.OrangeBright, TextAnchor.MiddleCenter); // 글자
            reportBox.gameObject.SetActive(false); // 처음엔 숨김

            crosshair = RetroUi.Rect(root, "Crosshair"); // 조준점
            RetroUi.Place(crosshair, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(20f, 20f)); // 가운데
            Image vertical = RetroUi.Solid(crosshair, "V", RetroUi.OrangeBright); // 세로
            RetroUi.Place(vertical.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(2f, 16f)); // 크기
            Image horizontal = RetroUi.Solid(crosshair, "H", RetroUi.OrangeBright); // 가로
            RetroUi.Place(horizontal.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(16f, 2f)); // 크기

            promptBox = Panel(root, "Prompt", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(560f, 64f)); // 조사 안내
            promptLabel = RetroUi.Label(promptBox, "Text", string.Empty, 24, RetroUi.OrangeBright, TextAnchor.UpperCenter); // 글자
            RetroUi.Stretch(promptLabel.rectTransform, 12f, 12f, 10f, 0f); // 여백
            holdTrack = RetroUi.Rect(promptBox, "HoldTrack"); // 막대 바탕
            RetroUi.Stretch(holdTrack, 30f, 30f, 44f, 10f); // 아래쪽
            RetroUi.Solid(holdTrack, "Back", new Color(0f, 0f, 0f, 0.5f)); // 바탕
            holdFill = RetroUi.Solid(holdTrack, "Fill", RetroUi.Orange).rectTransform; // 진행
            promptBox.gameObject.SetActive(false); // 처음엔 숨김

            healthFill = Bar(root, "Health", 118f, RetroUi.Red, out healthLabel, out _); // 체력
            staminaFill = Bar(root, "Stamina", 70f, RetroUi.OrangeBright, out staminaLabel, out staminaImage); // 기력
            voiceLabel = RetroUi.Label(root, "Voice", string.Empty, 20, RetroUi.OrangeDim, TextAnchor.LowerLeft); // 43일차: 마이크 상태
            RetroUi.Place(voiceLabel.rectTransform, Vector2.zero, Vector2.zero, new Vector2(32f, 164f), new Vector2(420f, 28f)); // 체력 막대 위
            voiceLabel.gameObject.AddComponent<Shadow>().effectColor = new Color(0f, 0f, 0f, 0.9f); // 읽기 쉽게
            voiceLabel.gameObject.SetActive(false); // 협동 중에만

            noticeBox = Panel(root, "Notice", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 1f), new Vector2(0f, -130f), new Vector2(640f, 48f)); // 알림
            noticeLabel = RetroUi.Label(noticeBox, "Text", string.Empty, 22, RetroUi.OrangeBright, TextAnchor.MiddleCenter); // 글자
            noticeBox.gameObject.SetActive(false); // 처음엔 숨김
            canvas.gameObject.SetActive(false); // 게임 중에만 표시
        }

        private static RectTransform Panel(RectTransform root, string name, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size) // 테두리 있는 반투명 상자
        {
            RectTransform panel = RetroUi.Rect(root, name); // 상자
            RetroUi.Place(panel, anchor, pivot, position, size); // 배치
            RetroUi.Solid(panel, "Fill", new Color(0.03f, 0.02f, 0.015f, 0.62f)); // 바탕
            RetroUi.Frame(panel, RetroUi.OrangeDim, 2f); // 테두리
            return panel; // 반환
        }

        private static Text Line(RectTransform panel, string name, int size, Color color, float top) // 상태 줄
        {
            Text line = RetroUi.Label(panel, name, string.Empty, size, color, TextAnchor.UpperLeft); // 글자
            RetroUi.Place(line.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(18f, -top), new Vector2(400f, size + 8f)); // 위치
            return line; // 반환
        }

        private static RectTransform Bar(RectTransform root, string name, float bottom, Color color, out Text label, out Image fillImage) // 왼쪽 아래 막대
        {
            RectTransform bar = RetroUi.Rect(root, name); // 막대
            RetroUi.Place(bar, Vector2.zero, Vector2.zero, new Vector2(28f, bottom), new Vector2(420f, 36f)); // 위치
            RetroUi.Solid(bar, "Back", new Color(0f, 0f, 0f, 0.55f)); // 바탕
            RectTransform inner = RetroUi.Rect(bar, "Inner"); // 채움 영역
            RetroUi.Stretch(inner, 4f, 4f, 4f, 4f); // 여백
            fillImage = RetroUi.Solid(inner, "Fill", new Color(color.r, color.g, color.b, 0.8f)); // 채움
            RetroUi.Frame(bar, RetroUi.OrangeDim, 2f); // 테두리
            label = RetroUi.Label(bar, "Label", string.Empty, 20, Color.white, TextAnchor.MiddleLeft); // 수치
            RetroUi.Stretch(label.rectTransform, 12f, 12f, 0f, 0f); // 여백
            label.GetComponent<Text>().gameObject.AddComponent<Shadow>().effectColor = new Color(0f, 0f, 0f, 0.9f); // 읽기 쉽게
            return fillImage.rectTransform; // 채움
        }
    }
}
