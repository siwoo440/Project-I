using System.Text; // F1 전력 진단 문자열 조립 기능 참조
using ProjectI.Lighting; // 고정·휴대 조명 진단 상태 참조
using ProjectI.Power; // 발전기·배전반·방·문·스냅샷 상태 참조
using UnityEngine; // 유니티 기본 기능 참조

namespace ProjectI.Diagnostics // 프로젝트 공통 디버그 기능 네임스페이스
{
    public sealed class PowerSystemDebugPage : DebugPageProvider // F1 조명·전력 통합 진단 페이지
    {
        [SerializeField] private PowerLightingStateManager stateManager; // 진단할 Day13 상태 관리자 참조

        public override string PageName => "Power System"; // F1 페이지 표시 이름
        public override int SortOrder => 70; // 기존 밝기·조명 페이지 뒤쪽 정렬 순서

        public void Configure(PowerLightingStateManager targetManager) // Day13 자동 Setup용 진단 페이지 구성
        {
            stateManager = targetManager; // 상태 관리자 참조 저장
        }

        public override string BuildDebugText() // 현재 조명·전력 상태 문자열 생성
        {
            ResolveManager(); // 누락 상태 관리자 자동 조회
            StringBuilder builder = new StringBuilder(1024); // 전력 진단 문자열 버퍼 생성
            builder.AppendLine("POWER / LIGHTING STATE"); // 페이지 제목 출력
            builder.AppendLine("────────────────────────────"); // 구분선 출력

            if (stateManager == null || !stateManager.IsConfigured) // 핵심 상태 관리자 구성 여부 확인
            {
                builder.AppendLine("Day13 State Manager : NOT READY"); // 상태 관리자 미구성 안내
                return builder.ToString(); // 현재 진단 문자열 반환
            }

            AppendGenerator(builder); // 발전기 상태 출력
            AppendBoard(builder); // 메인 배전반과 방 상태 출력
            AppendDoors(builder); // 철제문 상태 출력
            AppendLighting(builder); // 고정·휴대 조명 요약 출력
            AppendSnapshot(builder); // 런타임 스냅샷 상태 출력
            return builder.ToString(); // 완성된 F1 진단 문자열 반환
        }

        private void AppendGenerator(StringBuilder builder) // 발전기 진단 정보 추가
        {
            GeneratorController generator = stateManager.Generator; // 현재 발전기 참조 조회
            builder.AppendLine(); // 발전기 섹션 여백 출력
            builder.AppendLine("[Generator]"); // 발전기 섹션 제목 출력

            if (generator == null) // 발전기 참조 존재 여부 확인
            {
                builder.AppendLine("Missing"); // 발전기 누락 안내
                return; // 발전기 섹션 출력 종료
            }

            builder.AppendLine($"Running : {FormatOnOff(generator.IsRunning)}"); // 발전기 가동 상태 출력
            builder.AppendLine($"Fuel    : {generator.CurrentFuel:0.0} / {generator.MaxFuel:0.0} ({generator.FuelRatio * 100f:0}%)"); // 발전기 연료 상태 출력
        }

        private void AppendBoard(StringBuilder builder) // 배전반과 방 전력 진단 정보 추가
        {
            MainDistributionBoardController board = stateManager.DistributionBoard; // 현재 중앙 배전반 참조 조회
            builder.AppendLine(); // 배전반 섹션 여백 출력
            builder.AppendLine("[Distribution Board]"); // 배전반 섹션 제목 출력

            if (board == null) // 배전반 참조 존재 여부 확인
            {
                builder.AppendLine("Missing"); // 배전반 누락 안내
                return; // 배전반 섹션 출력 종료
            }

            builder.AppendLine($"Main Requested : {FormatOnOff(board.MainPowerRequested)}"); // 메인 스위치 요청 상태 출력
            builder.AppendLine($"Main Actual    : {FormatOnOff(board.FacilityPowerAvailable)}"); // 실제 시설 통전 상태 출력
            RoomPowerZone[] rooms = board.RoomZones; // 배전반 연결 방 목록 조회

            if (rooms == null) // 방 목록 존재 여부 확인
            {
                return; // 방 상세 출력 생략
            }

            for (int index = 0; index < rooms.Length; index++) // 연결된 방 전체 순회
            {
                RoomPowerZone room = rooms[index]; // 현재 방 전력 구역 조회

                if (room == null) // 유효 방 참조 여부 확인
                {
                    builder.AppendLine($"Room {index + 1:00} : MISSING"); // 누락 방 표시
                    continue; // 다음 방 검사
                }

                builder.AppendLine($"{room.DisplayName} : Req {FormatOnOff(room.RequestedPower)} / Actual {FormatOnOff(room.ActualPower)} / Consumers {room.ConsumerCount}"); // 방별 요청·실제 전력과 소비자 수 출력
            }
        }

        private void AppendDoors(StringBuilder builder) // 철제문 진단 정보 추가
        {
            MainDistributionBoardController board = stateManager.DistributionBoard; // 현재 중앙 배전반 참조 조회
            builder.AppendLine(); // 철제문 섹션 여백 출력
            builder.AppendLine("[Powered Iron Doors]"); // 철제문 섹션 제목 출력
            PoweredIronDoor[] doors = board == null ? null : board.ControlledDoors; // 배전반 연결 철제문 목록 조회

            if (doors == null || doors.Length == 0) // 철제문 목록 존재 여부 확인
            {
                builder.AppendLine("None"); // 연결 문 없음 안내
                return; // 철제문 섹션 출력 종료
            }

            foreach (PoweredIronDoor door in doors) // 연결된 철제문 전체 순회
            {
                if (door == null) // 유효 철제문 참조 여부 확인
                {
                    builder.AppendLine("Missing Door"); // 누락 철제문 표시
                    continue; // 다음 문 검사
                }

                builder.AppendLine($"{door.DisplayName} : {door.State} / Power {FormatOnOff(door.HasPower)}"); // 철제문 이동 상태와 통전 여부 출력
            }
        }

        private void AppendLighting(StringBuilder builder) // 고정·휴대 조명 진단 정보 추가
        {
            FixedLightController[] fixedLights = stateManager.FixedLights; // 고정 조명 목록 조회
            PortableLightItem[] portableLights = stateManager.PortableLights; // 휴대 조명 목록 조회
            int fixedCount = fixedLights == null ? 0 : fixedLights.Length; // 전체 고정 조명 개수 계산
            int fixedLitCount = 0; // 현재 켜진 고정 조명 개수 초기화
            int portableCount = portableLights == null ? 0 : portableLights.Length; // 전체 휴대 조명 개수 계산
            int portableEmittingCount = 0; // 현재 실제 발광 중 휴대 조명 개수 초기화

            if (fixedLights != null) // 고정 조명 배열 존재 여부 확인
            {
                foreach (FixedLightController light in fixedLights) // 고정 조명 전체 순회
                {
                    if (light != null && light.IsLit) // 유효하면서 점화된 고정 조명 확인
                    {
                        fixedLitCount++; // 점화된 고정 조명 개수 증가
                    }
                }
            }

            if (portableLights != null) // 휴대 조명 배열 존재 여부 확인
            {
                foreach (PortableLightItem light in portableLights) // 휴대 조명 전체 순회
                {
                    if (light != null && light.IsEmitting) // 유효하면서 실제 발광 중 조명 확인
                    {
                        portableEmittingCount++; // 발광 휴대 조명 개수 증가
                    }
                }
            }

            builder.AppendLine(); // 조명 섹션 여백 출력
            builder.AppendLine("[Lighting]"); // 조명 섹션 제목 출력
            builder.AppendLine($"Fixed Lights    : {fixedLitCount} ON / {fixedCount} Total"); // 고정 조명 점화 개수 출력
            builder.AppendLine($"Portable Lights : {portableEmittingCount} Emitting / {portableCount} Total"); // 휴대 조명 발광 개수 출력
        }

        private void AppendSnapshot(StringBuilder builder) // 런타임 스냅샷 진단 정보 추가
        {
            builder.AppendLine(); // 스냅샷 섹션 여백 출력
            builder.AppendLine("[Runtime Snapshot]"); // 스냅샷 섹션 제목 출력
            builder.AppendLine($"Stored : {(stateManager.HasSnapshot ? "YES" : "NO")}"); // 현재 저장 스냅샷 존재 여부 출력

            if (stateManager.HasSnapshot) // 마지막 캡처 존재 여부 확인
            {
                builder.AppendLine($"Captured At : {stateManager.LastCaptureTime:0.00}s"); // 마지막 게임 시간 캡처 시점 출력
            }

            builder.AppendLine("F6 : Capture State"); // 상태 캡처 단축키 안내
            builder.AppendLine("F7 : Restore State"); // 상태 복구 단축키 안내
        }

        private static string FormatOnOff(bool value) // bool 전력 상태 표시 문자열 변환
        {
            return value ? "ON" : "OFF"; // ON 또는 OFF 문자열 반환
        }

        private void ResolveManager() // 상태 관리자 참조 자동 확보
        {
            if (stateManager == null) // 직렬화 상태 관리자 누락 여부 확인
            {
                stateManager = Object.FindFirstObjectByType<PowerLightingStateManager>(); // 현재 씬 Day13 상태 관리자 자동 조회
            }
        }
    }
}
