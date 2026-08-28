using ProjectI.Brightness; // 기존 자연광 값 참조
using ProjectI.TimeOfDay; // 게임 시간 컨트롤러와 시간대 참조
using UnityEngine; // Mathf와 Object 조회 기능 참조

namespace ProjectI.Diagnostics // 프로젝트 공통 디버그 기능 네임스페이스
{
    public sealed class NaturalLightDebugPage : DebugPageProvider // F1에서 현재 시간과 태양·달 자연광 상태를 확인
    {
        [SerializeField] private GameTimeController timeController; // 표시할 게임 시간 컨트롤러

        public override string PageName => "Time / Natural Light"; // F1 페이지 이름
        public override int SortOrder => 60; // Light Calculation 다음 페이지로 배치

        private void Awake() // 시간·자연광 디버그 페이지 초기화
        {
            ResolveController(); // 현재 씬 게임 시간 컨트롤러 자동 조회
        }

        public override string BuildDebugText() // 현재 시간과 자연광 상태 문자열 생성
        {
            ResolveController(); // 런타임 참조 누락에 대비해 컨트롤러 확보

            if (timeController == null) // 게임 시간 컨트롤러 존재 여부 확인
            {
                return "GameTimeController를 찾을 수 없습니다."; // 시간 시스템 누락 안내 반환
            }

            NaturalLightController naturalLight = timeController.NaturalLight; // 기존 게임용 자연광 컨트롤러 조회
            float hour = GameTimeProfile.NormalizeHour(timeController.CurrentHour); // 현재 시간을 하루 범위로 보정
            int displayHour = Mathf.FloorToInt(hour); // F1 표시용 시 계산
            int displayMinute = Mathf.FloorToInt((hour - displayHour) * 60f); // F1 표시용 분 계산
            float sunBrightness = naturalLight == null ? 0f : naturalLight.SunBrightness; // 현재 게임용 태양 밝기 조회
            float moonBrightness = naturalLight == null ? 0f : naturalLight.MoonBrightness; // 현재 게임용 달 밝기 조회
            float naturalTotal = naturalLight == null ? 0f : naturalLight.CurrentBrightness; // 현재 Outdoor 자연광 합계 조회
            Light sunLight = timeController.SunLight; // 실제 태양 Directional Light 조회
            Light moonLight = timeController.MoonLight; // 실제 달 Directional Light 조회
            string sunVisual = sunLight == null ? "Missing" : $"{sunLight.intensity:0.00} / {(sunLight.enabled ? "ON" : "OFF")}"; // 태양 화면 Light 강도·활성 상태 문자열 생성
            string moonVisual = moonLight == null ? "Missing" : $"{moonLight.intensity:0.00} / {(moonLight.enabled ? "ON" : "OFF")}"; // 달 화면 Light 강도·활성 상태 문자열 생성

            return $"Time : {displayHour:00}:{displayMinute:00}\nPhase : {timeController.CurrentPhase}\nPaused : {timeController.IsPaused}\n1 Game Minute : {timeController.RealSecondsPerGameMinute:0.00}s\n\nSun Brightness : {sunBrightness:0.000}\nSun Visual : {sunVisual}\n\nMoon Brightness : {moonBrightness:0.000}\nMoon Visual : {moonVisual}\n\nNatural Total : {naturalTotal:0.000}"; // F1 시간·자연광 상세 문자열 반환
        }

        public void Configure(GameTimeController controller) // 에디터 자동 Setup용 시간 컨트롤러 지정
        {
            timeController = controller; // 현재 씬 시간 컨트롤러 저장
        }

        private void ResolveController() // 게임 시간 컨트롤러 자동 확보
        {
            if (timeController == null) // 직렬화된 참조 누락 확인
            {
                timeController = Object.FindFirstObjectByType<GameTimeController>(); // 현재 씬 첫 게임 시간 컨트롤러 조회
            }
        }
    }
}
