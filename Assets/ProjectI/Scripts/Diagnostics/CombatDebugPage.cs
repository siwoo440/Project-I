using System.Text; // F1 전투 진단 문자열 조립 기능 참조
using ProjectI.Combat; // 공통 전투 상태와 Damage Pipeline 참조
using UnityEngine; // 유니티 오브젝트 기능 참조

namespace ProjectI.Diagnostics // 프로젝트 공통 디버그 기능 네임스페이스
{
    public sealed class CombatDebugPage : DebugPageProvider // F1 공통 전투·피해 진단 페이지
    {
        [SerializeField] private CombatController combatController; // 진단할 플레이어 공통 전투 제어기

        public override string PageName => "Combat"; // F1 페이지 표시 이름
        public override int SortOrder => 80; // 기존 Power System 뒤쪽 전투 페이지 정렬 순서

        public void Configure(CombatController controller) // Day14 자동 Setup용 전투 진단 페이지 구성
        {
            combatController = controller; // 플레이어 공통 전투 제어기 연결
        }

        public override string BuildDebugText() // 현재 공통 전투 상태 진단 문자열 생성
        {
            ResolveController(); // 누락 전투 제어기 자동 조회
            StringBuilder builder = new StringBuilder(1400); // 전투 진단 문자열 버퍼 생성
            builder.AppendLine("COMBAT SYSTEM"); // 페이지 제목 출력
            builder.AppendLine("────────────────────────────"); // 구분선 출력

            if (combatController == null) // 플레이어 공통 전투 제어기 존재 여부 확인
            {
                builder.AppendLine("CombatController : NOT READY"); // 전투 제어기 누락 안내
                return builder.ToString(); // 현재 진단 문자열 반환
            }

            AppendCombatState(builder); // 전투 상태와 공격 단계 출력
            AppendWeapon(builder); // 현재 공격 무기와 스태미나 출력
            AppendDamagePipeline(builder); // 마지막 공통 피해 처리 결과 출력
            AppendTargets(builder); // 시험 더미 진영과 현재 체력 출력
            AppendCollision(builder); // 마지막 벽 충돌 정보 출력
            AppendFactionRules(builder); // 기본 진영 피해 규칙 요약 출력
            return builder.ToString(); // 완성된 전투 진단 문자열 반환
        }

        private void AppendCombatState(StringBuilder builder) // 현재 전투 상태 정보 추가
        {
            builder.AppendLine(); // 상태 섹션 여백 출력
            builder.AppendLine("[Combat State]"); // 상태 섹션 제목 출력
            builder.AppendLine($"State     : {combatController.State}"); // 현재 공통 전투 상태 출력
            builder.AppendLine($"Phase     : {combatController.Phase}"); // 현재 공격 단계 출력
            builder.AppendLine($"Progress  : {combatController.PhaseProgress * 100f:0}%"); // 현재 공격 단계 진행률 출력
            builder.AppendLine($"Attack ID : {combatController.AttackId}"); // 현재 공격 식별값 출력

            if (!string.IsNullOrWhiteSpace(combatController.LastFailureReason)) // 마지막 공격 시작 실패 사유 존재 여부 확인
            {
                builder.AppendLine($"Last Fail : {combatController.LastFailureReason}"); // 마지막 공격 실패 사유 출력
            }
        }

        private void AppendWeapon(StringBuilder builder) // 현재 무기와 플레이어 자원 정보 추가
        {
            MeleeWeaponItem weapon = combatController.ActiveWeapon; // 현재 공격 중 근접 무기 조회
            AttackDefinition definition = weapon == null ? null : weapon.AttackDefinition; // 현재 무기 공격 데이터 조회
            builder.AppendLine(); // 무기 섹션 여백 출력
            builder.AppendLine("[Attack / Movement]"); // 공격과 이동 섹션 제목 출력
            builder.AppendLine($"Weapon         : {(weapon == null ? "None" : weapon.name)}"); // 현재 공격 무기 이름 출력
            builder.AppendLine($"Attack Data    : {(definition == null ? "None" : definition.DisplayName)}"); // 현재 공격 데이터 이름 출력

            if (definition != null) // 공격 데이터 존재 여부 확인
            {
                builder.AppendLine($"Damage         : {definition.BaseDamage:0.0} / {definition.DamageType}"); // 기본 피해량과 피해 종류 출력
                builder.AppendLine($"Stamina Cost   : {definition.StaminaCost:0.0}"); // 공격 스태미나 비용 출력
                builder.AppendLine($"Move Multiplier: {definition.MovementMultiplier:0.00}"); // 공격 중 이동 배율 출력
            }

            if (combatController.Stamina != null) // 기존 플레이어 스태미나 연결 여부 확인
            {
                builder.AppendLine($"Stamina        : {combatController.Stamina.CurrentStamina:0.0} / {combatController.Stamina.MaxStamina:0.0}"); // 현재·최대 스태미나 출력
            }

            if (combatController.Movement != null) // 기존 플레이어 이동 연결 여부 확인
            {
                builder.AppendLine($"Move Modifier  : {combatController.Movement.ExternalSpeedMultiplier:0.00}"); // 현재 외부 이동 속도 배율 출력
                builder.AppendLine($"Sprint Allowed : {(combatController.Movement.ExternalSprintAllowed ? "YES" : "NO")}"); // 공격 중 달리기 제한 상태 출력
            }
        }

        private static void AppendDamagePipeline(StringBuilder builder) // 공통 Damage Pipeline 마지막 처리 결과 추가
        {
            builder.AppendLine(); // 피해 섹션 여백 출력
            builder.AppendLine("[Damage Pipeline]"); // 피해 섹션 제목 출력
            builder.AppendLine($"Applied Hits : {DamagePipeline.AppliedHitCount}"); // 런타임 실제 피해 적용 횟수 출력

            if (!DamagePipeline.HasResult) // 마지막 Damage Pipeline 결과 존재 여부 확인
            {
                builder.AppendLine("Last Result  : None"); // 피해 처리 이력 없음 출력
                return; // 피해 상세 출력 종료
            }

            CombatHitResult result = DamagePipeline.LastResult; // 마지막 공통 피해 처리 결과 조회
            DamageInfo info = DamagePipeline.LastDamageInfo; // 마지막 공통 피해 요청 데이터 조회
            builder.AppendLine($"Source Faction: {info.SourceFaction}"); // 마지막 피해 주체 진영 출력
            builder.AppendLine($"Damage Type   : {info.DamageType}"); // 마지막 피해 종류 출력
            builder.AppendLine($"Target        : {(result.TargetObject == null ? "None" : result.TargetObject.name)}"); // 마지막 피격 대상 이름 출력
            builder.AppendLine($"Requested     : {result.RequestedDamage:0.0}"); // 요청 피해량 출력
            builder.AppendLine($"Applied       : {result.AppliedDamage:0.0}"); // 실제 적용 피해량 출력
            builder.AppendLine($"Allowed       : {(result.Allowed ? "YES" : "NO")}"); // 진영·상태 규칙 피해 허용 여부 출력
            builder.AppendLine($"Fatal         : {(result.Fatal ? "YES" : "NO")}"); // 마지막 피해 사망 여부 출력
            builder.AppendLine($"Reason        : {result.Reason}"); // 마지막 피해 처리 사유 출력
        }

        private static void AppendTargets(StringBuilder builder) // 현재 공통 피해 시험 대상 정보 추가
        {
            CombatHealth[] targets = Object.FindObjectsByType<CombatHealth>(FindObjectsInactive.Include, FindObjectsSortMode.None); // 현재 씬 공통 전투 체력 대상 조회
            builder.AppendLine(); // 대상 섹션 여백 출력
            builder.AppendLine("[Combat Targets]"); // 대상 섹션 제목 출력

            if (targets.Length == 0) // 공통 피해 시험 대상 존재 여부 확인
            {
                builder.AppendLine("None"); // 대상 없음 안내
                return; // 대상 상세 출력 종료
            }

            foreach (CombatHealth target in targets) // 현재 씬 공통 피해 대상 전체 순회
            {
                if (target == null) // 유효 대상 여부 확인
                {
                    continue; // 누락 대상 건너뜀
                }

                builder.AppendLine($"{target.DisplayName} : {target.Faction} / HP {target.CurrentHealth:0.0}/{target.MaxHealth:0.0}"); // 대상 진영과 현재 체력 출력
            }
        }

        private void AppendCollision(StringBuilder builder) // 근접 무기 벽 충돌 정보 추가
        {
            builder.AppendLine(); // 충돌 섹션 여백 출력
            builder.AppendLine("[Melee Collision]"); // 근접 충돌 섹션 제목 출력
            builder.AppendLine($"Last Wall : {(combatController.LastWallObject == null ? "None" : combatController.LastWallObject.name)}"); // 마지막 벽 충돌 오브젝트 출력

            if (combatController.LastWallObject != null) // 마지막 벽 충돌 존재 여부 확인
            {
                Vector3 point = combatController.LastWallPoint; // 마지막 벽 충돌 위치 조회
                builder.AppendLine($"Wall Point: {point.x:0.00}, {point.y:0.00}, {point.z:0.00}"); // 마지막 벽 충돌 월드 좌표 출력
            }
        }

        private static void AppendFactionRules(StringBuilder builder) // 핵심 진영 공격 규칙 요약 추가
        {
            builder.AppendLine(); // 진영 섹션 여백 출력
            builder.AppendLine("[Faction Rules]"); // 진영 규칙 섹션 제목 출력
            builder.AppendLine($"Player → Enemy : {(CombatFactionRules.CanDamage(CombatFaction.Player, CombatFaction.Enemy) ? "YES" : "NO")}"); // 플레이어 대 적 피해 규칙 출력
            builder.AppendLine($"Player → Ally  : {(CombatFactionRules.CanDamage(CombatFaction.Player, CombatFaction.Ally) ? "YES" : "NO")}"); // 플레이어 대 아군 피해 규칙 출력
            builder.AppendLine($"Enemy → Player : {(CombatFactionRules.CanDamage(CombatFaction.Enemy, CombatFaction.Player) ? "YES" : "NO")}"); // 적 대 플레이어 피해 규칙 출력
            builder.AppendLine($"Env → Player   : {(CombatFactionRules.CanDamage(CombatFaction.Environment, CombatFaction.Player) ? "YES" : "NO")}"); // 환경 대 플레이어 피해 규칙 출력
        }

        private void ResolveController() // 플레이어 공통 전투 제어기 자동 확보
        {
            if (combatController == null) // 직렬화 전투 제어기 누락 여부 확인
            {
                combatController = Object.FindFirstObjectByType<CombatController>(); // 현재 씬 첫 CombatController 자동 조회
            }
        }
    }
}
