using System.Collections.Generic; // 광원 계산 결과 정렬 목록 기능 참조
using System.Text; // F1 페이지 문자열 조립 기능 참조
using ProjectI.Brightness; // 밝기 센서·광원·영역 기능 참조
using UnityEngine; // 유니티 기본 기능 참조

namespace ProjectI.Diagnostics // 프로젝트 공통 디버그 기능 네임스페이스
{
    public sealed class LightCalculationDebugPage : DebugPageProvider // 현재 플레이어 위치에서 모든 광원 계산 과정을 보여주는 F1 페이지
    {
        [SerializeField] private PlayerBrightnessSensor sensor; // 현재 위치와 최종 밝기 결과를 제공할 플레이어 센서

        public override string PageName => "Light Calculation"; // F1 페이지 이름
        public override int SortOrder => 50; // 고정 광원 페이지 다음 마지막 진단 페이지로 배치

        private void Awake() // 광원 계산 페이지 초기화
        {
            ResolveSensor(); // 플레이어 밝기 센서 자동 조회
        }

        public override string BuildDebugText() // 현재 플레이어 위치의 모든 광원별 기여도와 합산 결과 생성
        {
            ResolveSensor(); // 씬 재시작 또는 참조 누락에 대비해 센서 확보

            if (sensor == null) // 플레이어 밝기 센서 존재 여부 확인
            {
                return "PlayerBrightnessSensor를 찾을 수 없습니다."; // 센서 누락 상태 표시
            }

            sensor.SampleNow(); // F1 상세 계산 페이지를 보는 동안 현재 위치 밝기를 즉시 다시 계산
            Vector3 samplePosition = sensor.transform.position; // 현재 플레이어 위치 조회
            IndoorBrightnessArea targetArea = IndoorBrightnessArea.FindContaining(samplePosition); // 현재 Outdoor 또는 Indoor 방 판정
            BrightnessSample sample = sensor.CurrentSample; // 기존 BrightnessManager가 계산한 최종 결과 조회
            List<LightContributionDebugInfo> results = new List<LightContributionDebugInfo>(); // 광원별 진단 결과 목록 생성
            float rawLocalTotal = 0f; // Clamp 전 실제 개별 광원 기여 합계 초기화

            foreach (BrightnessSource source in BrightnessSource.Sources) // 현재 활성 등록된 모든 게임용 광원 순회
            {
                if (source == null) // 유효하지 않은 광원 확인
                {
                    continue; // 다음 광원 검사
                }

                LightContributionDebugInfo info = BrightnessDebugUtility.Evaluate(source, samplePosition, targetArea); // 현재 플레이어 위치 기준 실제 광원 기여와 제외 이유 계산
                results.Add(info); // 상세 목록에 현재 광원 결과 추가
                rawLocalTotal += info.Contribution; // 실제 기여 밝기를 Clamp 전 합계에 누적
            }

            results.Sort((left, right) => string.CompareOrdinal(left.DisplayName, right.DisplayName)); // 이름 기준 일정한 순서로 광원 결과 정렬
            StringBuilder builder = new StringBuilder(); // 페이지 내용 문자열 버퍼 생성
            builder.AppendLine($"Area : {sample.AreaType}"); // 현재 외부·내부 종류 표시
            builder.AppendLine($"Room : {sample.AreaName}"); // 현재 방 이름 표시
            builder.AppendLine($"Position : {samplePosition.x:0.0}, {samplePosition.y:0.0}, {samplePosition.z:0.0}"); // 계산 기준 플레이어 월드 위치 표시
            builder.AppendLine(); // 영역 정보와 광원 목록 구분
            builder.AppendLine($"Sources : {results.Count}"); // 등록된 전체 게임용 광원 수 표시

            foreach (LightContributionDebugInfo info in results) // 광원별 계산 결과 순회
            {
                builder.AppendLine($"{info.DisplayName} : {info.Contribution:0.000}  [{FormatStatus(info.Status)}]  {info.Distance:0.0}m"); // 실제 기여도·제외 이유·거리 한 줄 표시
            }

            builder.AppendLine(); // 광원 목록과 합계 구분
            builder.AppendLine($"Raw Local Sum : {rawLocalTotal:0.000}"); // 개별 광원 기여도 Clamp 전 합계 표시
            builder.AppendLine($"Local Total : {Mathf.Clamp01(rawLocalTotal):0.000}"); // 게임 규칙과 같은 0~1 Local 합계 표시
            builder.AppendLine($"Natural Total : {sample.NaturalBrightness:0.000}"); // 현재 자연광 기여도 표시
            builder.AppendLine($"Final : {sample.TotalBrightness:0.000}"); // BrightnessManager 최종 밝기 표시
            builder.Append($"Level : {sample.Level}"); // 최종 밝기 등급 표시
            return builder.ToString(); // 완성된 상세 계산 페이지 반환
        }

        public void Configure(PlayerBrightnessSensor targetSensor) // 에디터 자동 구성용 플레이어 센서 지정
        {
            sensor = targetSensor; // 밝기 센서 참조 저장
        }

        private void ResolveSensor() // 현재 씬의 플레이어 밝기 센서 확보
        {
            if (sensor == null) // 직렬화된 센서 참조 누락 확인
            {
                sensor = Object.FindFirstObjectByType<PlayerBrightnessSensor>(); // 현재 씬 첫 플레이어 밝기 센서 자동 조회
            }
        }

        private static string FormatStatus(LightContributionStatus status) // 내부 enum을 읽기 쉬운 F1 표시 문자열로 변환
        {
            switch (status) // 현재 광원 진단 상태 분기
            {
                case LightContributionStatus.Active: // 실제 기여 중 상태
                    return "Active"; // 정상 기여 문구 반환
                case LightContributionStatus.Disabled: // 꺼진 상태
                    return "Disabled"; // 꺼짐 문구 반환
                case LightContributionStatus.OutOfRange: // 거리 범위 밖 상태
                    return "Out Of Range"; // 거리 제외 문구 반환
                case LightContributionStatus.DifferentArea: // 다른 방 또는 외부 상태
                    return "Different Area"; // 공간 제외 문구 반환
                case LightContributionStatus.OutsideCone: // 방향성 빔 바깥 상태
                    return "Outside Cone"; // 방향 제외 문구 반환
                default: // 정의되지 않은 상태
                    return status.ToString(); // enum 이름 자체 반환
            }
        }
    }
}
