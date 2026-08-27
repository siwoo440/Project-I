using ProjectI.Diagnostics; // 공통 F1 디버그 페이지 기능 참조
using UnityEngine; // 유니티 기본 기능 참조

namespace ProjectI.Brightness // 밝기 시스템 네임스페이스
{
    public sealed class BrightnessDebugHud : DebugPageProvider // 밝기 계산 결과를 공통 F1 디버그 페이지로 제공
    {
        [SerializeField] private PlayerBrightnessSensor sensor; // 표시할 플레이어 밝기 센서

        public override string PageName => "Brightness Debug"; // 공통 디버그 창에 표시할 페이지 이름
        public override int SortOrder => 20; // 플레이어 페이지 다음에 밝기 페이지 배치

        private void Awake() // 밝기 디버그 페이지 초기화
        {
            if (sensor == null) // 센서 참조 누락 확인
            {
                sensor = Object.FindFirstObjectByType<PlayerBrightnessSensor>(); // 현재 씬 플레이어 밝기 센서 자동 조회
            }
        }

        public override string BuildDebugText() // 공통 F1 디버그 창의 밝기 페이지 내용 생성
        {
            if (sensor == null) // 플레이어 밝기 센서 존재 여부 확인
            {
                return "PlayerBrightnessSensor를 찾을 수 없습니다."; // 센서 누락 상태 표시
            }

            BrightnessSample sample = sensor.CurrentSample; // 현재 플레이어 밝기 결과 조회
            return $"Area : {sample.AreaType}\nRoom : {sample.AreaName}\nNatural : {sample.NaturalBrightness:0.00}\nLocal : {sample.LocalBrightness:0.00}\nBrightness : {sample.TotalBrightness:0.00}\nLevel : {sample.Level}"; // 외부·내부와 각 밝기 요소를 페이지 문자열로 반환
        }

        public void Configure(PlayerBrightnessSensor targetSensor) // 에디터 자동 설정용 밝기 센서 지정
        {
            sensor = targetSensor; // 플레이어 밝기 센서 저장
        }
    }
}
