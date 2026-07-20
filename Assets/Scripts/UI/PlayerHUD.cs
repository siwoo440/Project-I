using TMPro; // TextMeshPro UI 기능 사용
using UnityEngine; // Unity 기본 기능 사용
using UnityEngine.UI; // Canvas Image 기능 사용

namespace ProjectI // 프로젝트 공통 네임스페이스
{
    public class PlayerHUD : MonoBehaviour // 던전 플레이어 HUD 관리 컴포넌트
    {
        [Header("게임 참조")] // 게임 시스템 참조 구분
        [SerializeField] PlayerController playerController; // 체력과 스태미너를 가져올 플레이어
        [SerializeField] PlayerInteractor playerInteractor; // 상호작용 문구를 가져올 플레이어 상호작용 컴포넌트
        [SerializeField] DungeonTimeSystem dungeonTimeSystem; // 던전 시간 정보를 가져올 시스템
        [SerializeField] LightSystem lightSystem; // 현재 밝기 정보를 가져올 시스템

        [Header("체력 UI")] // 체력 UI 참조 구분
        [SerializeField] Image healthFill; // 체력 게이지 채움 이미지
        [SerializeField] TMP_Text healthValueText; // 현재 체력 수치 글자

        [Header("스태미너 UI")] // 스태미너 UI 참조 구분
        [SerializeField] Image staminaFill; // 스태미너 게이지 채움 이미지
        [SerializeField] TMP_Text staminaValueText; // 현재 스태미너 수치 글자

        [Header("던전 정보 UI")] // 던전 정보 UI 참조 구분
        [SerializeField] TMP_Text timeText; // 던전 시간과 남은 시간 글자
        [SerializeField] TMP_Text brightnessText; // 현재 밝기와 밝기 단계 글자

        [Header("안내 UI")] // 안내 UI 참조 구분
        [SerializeField] TMP_Text interactionText; // 현재 상호작용 문구
        [SerializeField] TMP_Text controlGuideText; // 기본 조작 안내 문구

        [Header("색상")] // HUD 색상 설정 구분
        [SerializeField] Color healthColor = new Color(0.72f, 0.15f, 0.12f, 1f); // 정상 체력 색상
        [SerializeField] Color healthDangerColor = new Color(1f, 0.05f, 0.03f, 1f); // 낮은 체력 경고 색상
        [SerializeField] Color staminaColor = new Color(0.16f, 0.68f, 0.52f, 1f); // 정상 스태미너 색상
        [SerializeField] Color staminaDangerColor = new Color(0.72f, 0.58f, 0.08f, 1f); // 낮은 스태미너 경고 색상
        [SerializeField] Color normalTextColor = new Color(0.92f, 0.9f, 0.82f, 1f); // 일반 정보 글자 색상
        [SerializeField] Color timeWarningColor = new Color(1f, 0.2f, 0.12f, 1f); // 제한시간 경고 글자 색상
        [SerializeField] Color darkBrightnessColor = new Color(0.45f, 0.48f, 0.55f, 1f); // 어두운 방 밝기 글자 색상
        [SerializeField] Color brightBrightnessColor = new Color(1f, 0.82f, 0.34f, 1f); // 밝은 방 밝기 글자 색상

        float nextReferenceSearchTime; // 다음 Scene 참조 검색 시각

        void Awake() // HUD의 초기 참조와 고정 문구 설정
        {
            ResolveSceneReferences(); // 현재 Scene의 게임 시스템 검색
            SetControlGuide(); // 기본 조작 안내 문구 설정
        }

        void Update() // 매 프레임 HUD 상태 갱신
        {
            if (Time.unscaledTime >= nextReferenceSearchTime) // Scene 참조 재검색 시각 확인
            {
                ResolveSceneReferences(); // 아직 없는 Scene 참조 다시 검색
                nextReferenceSearchTime = Time.unscaledTime + 1f; // 다음 검색을 1초 후로 예약
            }

            UpdatePlayerStatus(); // 체력과 스태미너 갱신
            UpdateDungeonInformation(); // 던전 시간과 밝기 갱신
            UpdateInteractionPrompt(); // 현재 상호작용 문구 갱신
        }

        void ResolveSceneReferences() // 현재 Scene에서 필요한 게임 컴포넌트 검색
        {
            if (playerController == null) // 플레이어 참조 존재 여부 확인
            {
                playerController = FindFirstObjectByType<PlayerController>(); // 현재 플레이어 검색
            }

            if (playerInteractor == null) // 플레이어 상호작용 참조 존재 여부 확인
            {
                playerInteractor = FindFirstObjectByType<PlayerInteractor>(); // 현재 플레이어 상호작용 컴포넌트 검색
            }

            if (dungeonTimeSystem == null) // 던전 시간 시스템 참조 존재 여부 확인
            {
                dungeonTimeSystem = FindFirstObjectByType<DungeonTimeSystem>(); // 현재 던전 시간 시스템 검색
            }

            if (lightSystem == null) // 밝기 시스템 참조 존재 여부 확인
            {
                lightSystem = FindFirstObjectByType<LightSystem>(); // 현재 플레이어 밝기 시스템 검색
            }
        }

        void UpdatePlayerStatus() // 플레이어 체력과 스태미너 UI 갱신
        {
            if (playerController == null) // 플레이어 참조 존재 여부 확인
            {
                UpdateBar(healthFill, healthValueText, 0f, 1f, healthColor, healthDangerColor); // 체력 UI를 빈 상태로 표시
                UpdateBar(staminaFill, staminaValueText, 0f, 1f, staminaColor, staminaDangerColor); // 스태미너 UI를 빈 상태로 표시
                return; // 플레이어 상태 갱신 중단
            }

            UpdateBar(healthFill, healthValueText, playerController.CurrentHealth, playerController.MaxHealth, healthColor, healthDangerColor); // 현재 체력 UI 갱신
            UpdateBar(staminaFill, staminaValueText, playerController.CurrentStamina, playerController.MaxStamina, staminaColor, staminaDangerColor); // 현재 스태미너 UI 갱신
        }

        void UpdateBar(Image fillImage, TMP_Text valueText, float currentValue, float maximumValue, Color normalColor, Color dangerColor) // 공통 게이지와 수치 갱신
        {
            float safeMaximum = Mathf.Max(1f, maximumValue); // 0으로 나누는 상황 방지
            float ratio = Mathf.Clamp01(currentValue / safeMaximum); // 현재 수치 비율 계산

            if (fillImage != null) // 채움 이미지 존재 여부 확인
            {
                fillImage.fillAmount = ratio; // 현재 비율을 게이지에 적용
                fillImage.color = Color.Lerp(dangerColor, normalColor, ratio); // 남은 비율에 따라 게이지 색상 변경
            }

            if (valueText != null) // 수치 글자 존재 여부 확인
            {
                valueText.text = $"{currentValue:F0} / {maximumValue:F0}"; // 현재 수치와 최대 수치 표시
            }
        }

        void UpdateDungeonInformation() // 던전 시간과 밝기 정보 갱신
        {
            if (dungeonTimeSystem != null && timeText != null) // 던전 시간 시스템과 UI 존재 여부 확인
            {
                int gameHour = Mathf.FloorToInt(dungeonTimeSystem.GameHour); // 현재 던전 시 계산
                int gameMinute = Mathf.Clamp(Mathf.FloorToInt((dungeonTimeSystem.GameHour - gameHour) * 60f), 0, 59); // 현재 던전 분 계산
                int remainingSeconds = Mathf.CeilToInt(dungeonTimeSystem.RemainingSeconds); // 남은 현실시간 정수 변환
                int remainingMinute = remainingSeconds / 60; // 남은 분 계산
                int remainingSecond = remainingSeconds % 60; // 남은 초 계산

                timeText.text = $"던전 시간 {gameHour:00}:{gameMinute:00}\n남은 시간 {remainingMinute:00}:{remainingSecond:00}"; // 던전 시간과 남은 시간 표시
                timeText.color = remainingSeconds <= 60 ? timeWarningColor : normalTextColor; // 마지막 1분에 경고 색상 적용
            }

            if (lightSystem != null && brightnessText != null) // 밝기 시스템과 UI 존재 여부 확인
            {
                float brightnessRatio = Mathf.Clamp01(lightSystem.CurrentBrightness / 100f); // 현재 밝기 비율 계산

                brightnessText.text = $"밝기 {lightSystem.CurrentBrightness:F0} / 100 · {lightSystem.Stage}"; // 현재 밝기와 단계 표시
                brightnessText.color = Color.Lerp(darkBrightnessColor, brightBrightnessColor, brightnessRatio); // 밝기에 따라 글자 색상 변경
            }
        }

        void UpdateInteractionPrompt() // 현재 상호작용 문구 표시 상태 갱신
        {
            if (interactionText == null) // 상호작용 UI 존재 여부 확인
            {
                return; // 상호작용 UI 갱신 중단
            }

            string prompt = playerInteractor != null ? playerInteractor.CurrentPrompt : string.Empty; // 현재 상호작용 문구 가져오기
            bool hasPrompt = !string.IsNullOrWhiteSpace(prompt); // 표시할 문구 존재 여부 확인

            interactionText.gameObject.SetActive(hasPrompt); // 상호작용 대상이 있을 때만 문구 활성화

            if (hasPrompt) // 표시할 상호작용 문구 존재 여부 확인
            {
                interactionText.text = prompt; // 현재 상호작용 문구 적용
            }
        }

        void SetControlGuide() // 기본 조작 안내 문구 설정
        {
            if (controlGuideText == null) // 조작 안내 UI 존재 여부 확인
            {
                return; // 조작 안내 설정 중단
            }

            controlGuideText.text = "WASD 이동  ·  Shift 달리기  ·  Space 점프  ·  Ctrl 앉기  ·  E 상호작용  ·  Q 버리기  ·  R 사용  ·  F 광원"; // 기본 조작 안내 표시
        }
    }
}