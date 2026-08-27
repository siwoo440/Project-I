using System.Collections.Generic; // 휴대 조명 정렬 목록 기능 참조
using System.Text; // F1 디버그 페이지 문자열 조립 기능 참조
using ProjectI.Diagnostics; // 공통 F1 디버그 페이지 기반 기능 참조

namespace ProjectI.Lighting // 휴대 조명 기능 네임스페이스
{
    public sealed class PortableLightDebugPage : DebugPageProvider // 횃불·랜턴 상태를 공통 F1 디버그 목록에 자동 등록
    {
        public override string PageName => "Portable Light Debug"; // F1 창 상단 페이지 이름
        public override int SortOrder => 30; // Player 10, Brightness 20 다음 세 번째 페이지로 배치

        public override string BuildDebugText() // 현재 씬의 모든 휴대 조명 상태 문자열 생성
        {
            List<PortableLightItem> lights = new List<PortableLightItem>(); // 표시할 휴대 조명 임시 정렬 목록 생성

            foreach (PortableLightItem light in PortableLightItem.Lights) // 활성 휴대 조명 전체 순회
            {
                if (light != null) // 파괴되지 않은 유효 조명인지 확인
                {
                    lights.Add(light); // 표시 목록에 현재 조명 추가
                }
            }

            lights.Sort(CompareLights); // 아이템 이름 기준으로 일정한 순서 정렬
            StringBuilder builder = new StringBuilder(); // 여러 조명 상태를 효율적으로 조립할 문자열 버퍼 생성
            builder.AppendLine($"Portable Lights : {lights.Count}"); // 현재 활성 휴대 조명 수 표시
            builder.AppendLine(); // 제목과 개별 상태 사이 빈 줄 추가

            if (lights.Count == 0) // 씬에 휴대 조명이 하나도 없는지 확인
            {
                builder.Append("등록된 휴대 조명이 없습니다."); // 빈 목록 안내 문구 표시
                return builder.ToString(); // 빈 상태 페이지 문자열 반환
            }

            foreach (PortableLightItem light in lights) // 정렬된 휴대 조명 순회
            {
                string displayName = light.WorldItem == null ? light.name : light.WorldItem.DisplayName; // 슬롯 표시 이름 또는 오브젝트 이름 결정
                string areaName = light.BrightnessSource == null ? "None" : ResolveAreaName(light); // 현재 광원의 외부 또는 방 이름 계산
                builder.AppendLine($"[{displayName}]"); // 현재 휴대 조명 이름 표시
                builder.AppendLine($"State : {light.State}"); // 점화·보관·빈 연료 상태 표시
                builder.AppendLine($"Fuel : {light.CurrentFuel:0.0} / {light.MaxFuel:0.0} ({light.NormalizedFuel * 100f:0}%)"); // 현재 연료와 백분율 표시
                builder.AppendLine($"Emitting : {light.IsEmitting}"); // 실제 Unity Light와 게임 광원 활성 여부 표시
                builder.AppendLine($"Area : {areaName}"); // 현재 위치 기준 외부 또는 내부 방 소속 표시
                builder.AppendLine(); // 다음 조명과 구분할 빈 줄 추가
            }

            return builder.ToString().TrimEnd(); // 마지막 불필요한 줄바꿈을 제거한 페이지 문자열 반환
        }

        private static int CompareLights(PortableLightItem left, PortableLightItem right) // 휴대 조명 이름 정렬 규칙
        {
            string leftName = left.WorldItem == null ? left.name : left.WorldItem.DisplayName; // 왼쪽 아이템 표시 이름 결정
            string rightName = right.WorldItem == null ? right.name : right.WorldItem.DisplayName; // 오른쪽 아이템 표시 이름 결정
            return string.CompareOrdinal(leftName, rightName); // 이름 오름차순 비교 결과 반환
        }

        private static string ResolveAreaName(PortableLightItem light) // 이동형 광원의 현재 외부 또는 내부 방 이름 계산
        {
            var area = light.BrightnessSource.GetEffectiveArea(); // 현재 월드 위치를 기준으로 유효 내부 방 조회
            return area == null ? "Outdoor" : area.AreaName; // 방이 없으면 Outdoor, 있으면 해당 방 이름 반환
        }
    }
}
