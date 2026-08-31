using System.Text; // F1 함정 진단 문자열 조립 참조
using ProjectI.Traps; // Day18 함정 상태·피해 정보 참조
using UnityEngine; // 런타임 오브젝트 검색 기능 참조

namespace ProjectI.Diagnostics // 프로젝트 공통 디버그 기능 네임스페이스
{
    public sealed class TrapDebugPage : DebugPageProvider // 바닥·천장 가시·도끼·압력판 상태를 보여주는 F1 Trap 페이지
    {
        public override string PageName => "Trap"; // F1 페이지 이름
        public override int SortOrder => 110; // Monster AI 뒤 함정 페이지 정렬 순서

        public override string BuildDebugText() // 현재 활성 함정 전체 진단 문자열 생성
        {
            TrapControllerBase[] traps = Object.FindObjectsByType<TrapControllerBase>(FindObjectsInactive.Exclude, FindObjectsSortMode.None); // 활성 함정 Controller 전체 조회
            PressurePlate[] plates = Object.FindObjectsByType<PressurePlate>(FindObjectsInactive.Exclude, FindObjectsSortMode.None); // 활성 압력판 전체 조회
            StringBuilder builder = new StringBuilder(2600); // 함정 상태 문자열 버퍼 생성
            builder.AppendLine("TRAPS"); // 페이지 제목 출력
            builder.AppendLine("────────────────────────────"); // 구분선 출력
            builder.AppendLine($"Active Controllers : {traps.Length}"); // 활성 함정 수 출력
            builder.AppendLine($"Pressure Plates    : {plates.Length}"); // 압력판 수 출력
            builder.AppendLine($"Last Trap Hit      : {TrapDamageSource.LastTrapName}"); // 마지막 피해 함정 출력
            builder.AppendLine($"Last Target        : {TrapDamageSource.LastTargetName}"); // 마지막 피해 대상 출력
            builder.AppendLine($"Last Damage        : {TrapDamageSource.LastAppliedDamage:0.0}"); // 마지막 적용 피해량 출력

            for (int index = 0; index < traps.Length; index++) // 함정 Controller 전체 순회
            {
                TrapControllerBase trap = traps[index]; // 현재 함정 조회
                builder.AppendLine(); // 함정 섹션 여백 출력
                builder.AppendLine($"[T{index + 1:00}] {trap.DisplayName}"); // 함정 번호·이름 출력
                builder.AppendLine($"State      : {trap.State}"); // 현재 상태 출력
                builder.AppendLine($"Activation : {trap.ActivationSequence}"); // 누적 작동 횟수 출력
                builder.AppendLine($"TriggeredBy: {(trap.LastTriggerSource == null ? "Auto/None" : trap.LastTriggerSource.name)}"); // 마지막 작동 주체 출력

                if (trap.DamageSource != null) // 피해 소스 존재 여부 확인
                {
                    builder.AppendLine($"Damage     : {trap.DamageSource.Damage:0} / Stagger {trap.DamageSource.StaggerPower:0}"); // 피해·경직 수치 출력
                    builder.AppendLine($"Hit Window : {(trap.DamageSource.IsActive ? "ACTIVE" : "OFF")} / Targets {trap.DamageSource.DamagedTargetCount}"); // 현재 피해 창·피격 대상 수 출력
                }
            }

            for (int index = 0; index < plates.Length; index++) // 압력판 전체 순회
            {
                PressurePlate plate = plates[index]; // 현재 압력판 조회
                builder.AppendLine(); // 압력판 섹션 여백 출력
                builder.AppendLine($"[P{index + 1:00}] {plate.name}"); // 압력판 이름 출력
                builder.AppendLine($"Pressed    : {(plate.IsPressed ? "YES" : "NO")} / Occupants {plate.OccupantCount}"); // 눌림 상태·점유 행위자 수 출력
                builder.AppendLine($"Linked     : {(plate.LinkedTraps == null ? 0 : plate.LinkedTraps.Length)} trap(s)"); // 연결 함정 수 출력
            }

            return builder.ToString(); // 완성된 진단 문자열 반환
        }
    }
}
