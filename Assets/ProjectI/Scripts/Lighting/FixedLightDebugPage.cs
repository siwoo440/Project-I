using System.Collections.Generic; // 정렬 목록 기능 참조
using System.Text; // 디버그 문자열 조립 기능 참조
using ProjectI.Brightness; // 고정 BrightnessSource 정보 참조
using ProjectI.Diagnostics; // 공통 F1 디버그 페이지 기반 참조

namespace ProjectI.Lighting // 조명 기능 네임스페이스
{
    public sealed class FixedLightDebugPage : DebugPageProvider // 고정 환경 광원 상태를 F1 공통 페이지로 제공
    {
        public override string PageName => "Fixed Light Debug"; // F1 창 페이지 이름
        public override int SortOrder => 40; // Portable Light 다음 페이지로 배치

        public override string BuildDebugText() // 현재 씬 고정 광원 상태 문자열 생성
        {
            List<FixedLightController> lights = new List<FixedLightController>(); // 정렬할 활성 고정 조명 목록 생성

            foreach (FixedLightController light in FixedLightController.Lights) // 현재 활성 고정 조명 전체 순회
            {
                if (light != null) // 유효 고정 조명인지 확인
                {
                    lights.Add(light); // 표시 목록에 추가
                }
            }

            lights.Sort((left, right) => string.CompareOrdinal(left.DisplayName, right.DisplayName)); // 이름 기준 일정한 순서로 정렬
            StringBuilder builder = new StringBuilder(); // 페이지 문자열 버퍼 생성
            builder.AppendLine($"Fixed Lights : {lights.Count}"); // 현재 활성 고정 조명 수 표시
            builder.AppendLine(); // 제목 아래 빈 줄 추가

            foreach (FixedLightController light in lights) // 정렬된 고정 조명 순회
            {
                builder.AppendLine($"[{light.DisplayName}]"); // 고정 조명 이름 표시
                builder.AppendLine($"State : {(light.IsLit ? "Lit" : "Unlit")}"); // 켜짐·꺼짐 상태 표시
                BrightnessSource[] sources = light.BrightnessSources; // 현재 고정 조명의 게임용 광원 목록 조회

                foreach (BrightnessSource source in sources) // 연결 광원 전체 순회
                {
                    if (source == null) // 누락 광원 확인
                    {
                        continue; // 다음 광원 검사
                    }

                    string roomName = source.GetEffectiveArea() == null ? "Outdoor" : source.GetEffectiveArea().AreaName; // 고정 광원 소속 공간 이름 계산
                    builder.AppendLine($"Room : {roomName}"); // 소속 공간 표시
                    builder.AppendLine($"Brightness : {source.Brightness:0.00}"); // 기본 광원 밝기 표시
                    builder.AppendLine($"Range : {source.Range:0.0}m"); // 영향 거리 표시
                }

                builder.AppendLine(); // 다음 조명과 구분할 빈 줄 추가
            }

            if (lights.Count == 0) // 고정 조명이 아직 없는지 확인
            {
                builder.Append("등록된 고정 광원이 없습니다."); // 빈 목록 안내 표시
            }

            return builder.ToString().TrimEnd(); // 마지막 불필요한 줄바꿈 제거 후 반환
        }
    }
}
