using ProjectI.Brightness; // 기존 자연광 게임 밝기 컨트롤러 참조
using UnityEngine; // Light·Transform·Time 기능 참조

namespace ProjectI.TimeOfDay // 시간대 시스템 네임스페이스
{
    public sealed class GameTimeController : MonoBehaviour // 24시간 진행과 태양·달 게임 밝기·시각 조명을 함께 관리
    {
        [SerializeField] private NaturalLightController naturalLightController; // 기존 Outdoor 자연광 값 저장 대상
        [SerializeField] private Light sunLight; // 실제 화면용 태양 Directional Light
        [SerializeField] private Light moonLight; // 실제 화면용 달 Directional Light
        [SerializeField, Range(0f, 24f)] private float startHour = 12f; // 플레이 시작 기본 시간
        [SerializeField] private float realSecondsPerGameMinute = 1f; // 현실 몇 초가 게임 1분인지 설정
        [SerializeField] private bool timePaused; // 게임 시간 흐름 일시정지 여부
        [SerializeField] private float sunVisualMaximumIntensity = 1.20f; // 정오 태양 Directional Light 최대 강도
        [SerializeField] private float moonVisualMaximumIntensity = 0.22f; // 밤 달 Directional Light 최대 강도
        [SerializeField] private float sunAzimuth = -30f; // 테스트 맵 기준 태양의 좌우 진행 방향
        private float currentHour; // 런타임 현재 게임 시간

        public float StartHour => startHour; // Validator와 디버그용 시작 시간 공개
        public float CurrentHour => currentHour; // 현재 0~24 게임 시간 공개
        public float RealSecondsPerGameMinute => realSecondsPerGameMinute; // 현재 게임 시간 배속 설정 공개
        public bool IsPaused => timePaused; // 현재 시간 정지 상태 공개
        public GameTimePhase CurrentPhase => GameTimeProfile.EvaluatePhase(currentHour); // 현재 새벽·낮·저녁·밤 단계 공개
        public NaturalLightController NaturalLight => naturalLightController; // 기존 자연광 컨트롤러 공개
        public Light SunLight => sunLight; // 태양 화면 Light 공개
        public Light MoonLight => moonLight; // 달 화면 Light 공개

        private void Awake() // 런타임 시간 시스템 초기화
        {
            ResolveReferences(); // 기존 자연광과 태양·달 Light 참조 확보
            currentHour = GameTimeProfile.NormalizeHour(startHour); // 저장된 시작 시간을 런타임 현재 시간으로 적용
            ApplyCurrentTime(); // 시작 시간의 게임 밝기와 화면 조명 즉시 적용
        }

        private void Update() // 매 프레임 게임 시간 진행
        {
            if (timePaused) // 시간 흐름이 정지됐는지 확인
            {
                return; // 현재 시간을 유지
            }

            float secondsPerMinute = Mathf.Max(0.05f, realSecondsPerGameMinute); // 0 또는 지나치게 빠른 값 방지
            float gameMinutesDelta = UnityEngine.Time.deltaTime / secondsPerMinute; // 이번 프레임에 흐를 게임 분 계산
            currentHour = GameTimeProfile.NormalizeHour(currentHour + (gameMinutesDelta / 60f)); // 게임 분을 시간 단위로 더하고 하루 범위로 순환
            ApplyCurrentTime(); // 새 시간에 맞춰 자연광과 Directional Light 갱신
        }

        public void Configure(NaturalLightController naturalController, Light targetSunLight, Light targetMoonLight, float targetStartHour, float secondsPerGameMinute) // 에디터 자동 Setup용 전체 참조와 기본값 지정
        {
            naturalLightController = naturalController; // 기존 자연광 컨트롤러 저장
            sunLight = targetSunLight; // 태양 화면 Light 저장
            moonLight = targetMoonLight; // 달 화면 Light 저장
            startHour = GameTimeProfile.NormalizeHour(targetStartHour); // 시작 시간을 하루 범위로 저장
            realSecondsPerGameMinute = Mathf.Clamp(secondsPerGameMinute, 0.05f, 60f); // 게임 1분당 현실 초 안전 범위 적용
            currentHour = startHour; // 에디터 구성 직후 현재 시간을 시작 시간과 일치
            ApplyCurrentTime(); // 구성 직후 화면과 게임 자연광 상태 반영
        }

        public void SetTime(float hour) // 디버그·이후 게임 시스템에서 현재 시간을 즉시 변경
        {
            currentHour = GameTimeProfile.NormalizeHour(hour); // 지정 시간을 0~24 범위로 저장
            ApplyCurrentTime(); // 변경 시간의 자연광과 화면 조명 즉시 적용
        }

        public void SetPaused(bool paused) // 외부 시스템에서 시간 흐름 일시정지 상태 지정
        {
            timePaused = paused; // 지정된 정지 상태 저장
        }

        public void SetRealSecondsPerGameMinute(float seconds) // 외부 또는 디버그 시스템에서 시간 진행 속도 변경
        {
            realSecondsPerGameMinute = Mathf.Clamp(seconds, 0.05f, 60f); // 안전 범위로 시간 배율 설정
        }

        private void ApplyCurrentTime() // 현재 시간의 게임용 자연광과 실제 Directional Light 상태 동기화
        {
            float sunBrightness = GameTimeProfile.EvaluateSunBrightness(currentHour); // 현재 시간 태양 게임 밝기 계산
            float moonBrightness = GameTimeProfile.EvaluateMoonBrightness(currentHour); // 현재 시간 달 게임 밝기 계산

            if (naturalLightController != null) // 기존 자연광 컨트롤러 존재 여부 확인
            {
                naturalLightController.SetSunBrightness(sunBrightness); // 기존 Outdoor 태양 밝기에 현재 시간 결과 적용
                naturalLightController.SetMoonBrightness(moonBrightness); // 기존 Outdoor 달 밝기에 현재 시간 결과 적용
            }

            ApplySunVisual(sunBrightness); // 실제 태양 Directional Light 강도와 방향 적용
            ApplyMoonVisual(moonBrightness); // 실제 달 Directional Light 강도와 방향 적용
        }

        private void ApplySunVisual(float sunBrightness) // 태양 화면 Directional Light 갱신
        {
            if (sunLight == null) // 태양 Light 존재 여부 확인
            {
                return; // 화면 태양 처리 생략
            }

            float normalized = GameTimeProfile.MaximumSunBrightness <= 0f ? 0f : Mathf.Clamp01(sunBrightness / GameTimeProfile.MaximumSunBrightness); // 게임 태양 밝기를 화면 강도용 0~1로 변환
            sunLight.intensity = normalized * sunVisualMaximumIntensity; // 정오 최대 강도를 기준으로 화면 태양 세기 설정
            sunLight.enabled = sunBrightness > 0.001f; // 태양 게임 밝기가 사실상 0이면 실제 Light 비활성화
            float sunAngle = ((currentHour - 6f) / 24f) * 360f; // 06시를 동쪽 수평선 기준으로 하루 360도 태양 각도 계산
            sunLight.transform.rotation = Quaternion.Euler(sunAngle, sunAzimuth, 0f); // 시간에 따라 태양 Directional Light 방향 회전
        }

        private void ApplyMoonVisual(float moonBrightness) // 달 화면 Directional Light 갱신
        {
            if (moonLight == null) // 달 Light 존재 여부 확인
            {
                return; // 화면 달 처리 생략
            }

            float normalized = GameTimeProfile.MaximumMoonBrightness <= 0f ? 0f : Mathf.Clamp01(moonBrightness / GameTimeProfile.MaximumMoonBrightness); // 게임 달 밝기를 화면 강도용 0~1로 변환
            moonLight.intensity = normalized * moonVisualMaximumIntensity; // 밤 최대 강도를 기준으로 화면 달빛 세기 설정
            moonLight.enabled = moonBrightness > 0.001f; // 낮처럼 달 게임 밝기가 0이면 실제 Light 비활성화
            float moonAngle = (((currentHour - 6f) / 24f) * 360f) + 180f; // 태양 반대편을 기준으로 달의 하루 회전 각도 계산
            moonLight.transform.rotation = Quaternion.Euler(moonAngle, sunAzimuth + 20f, 0f); // 태양과 다른 방위에서 달 Directional Light 회전
        }

        private void ResolveReferences() // 런타임 누락 참조 자동 복구
        {
            if (naturalLightController == null) // 기존 자연광 컨트롤러 참조 누락 확인
            {
                naturalLightController = Object.FindFirstObjectByType<NaturalLightController>(); // 현재 씬 자연광 컨트롤러 자동 조회
            }
        }

        private void OnValidate() // 에디터 인스펙터 값 안전 범위 보정
        {
            startHour = GameTimeProfile.NormalizeHour(startHour); // 시작 시간을 하루 범위로 보정
            realSecondsPerGameMinute = Mathf.Clamp(realSecondsPerGameMinute, 0.05f, 60f); // 시간 진행 속도 안전 범위 보정
            sunVisualMaximumIntensity = Mathf.Max(0f, sunVisualMaximumIntensity); // 태양 화면 강도 음수 방지
            moonVisualMaximumIntensity = Mathf.Max(0f, moonVisualMaximumIntensity); // 달 화면 강도 음수 방지

            if (!Application.isPlaying) // Edit Mode에서 인스펙터 값을 바꾸는 중인지 확인
            {
                currentHour = startHour; // 에디터 미리보기 시간을 시작 시간과 일치
                ApplyCurrentTime(); // 가능한 참조가 있으면 에디터 화면에도 자연광 상태 반영
            }
        }
    }
}
