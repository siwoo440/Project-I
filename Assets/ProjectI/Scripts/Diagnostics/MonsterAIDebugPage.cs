using System.Text; // F1 몬스터 AI 진단 문자열 조립 기능 참조
using ProjectI.Monsters; // 몬스터 공통 AI·특수 행동·청각·화살 기능 참조
using UnityEngine; // 런타임 오브젝트 검색 기능 참조

namespace ProjectI.Diagnostics // 프로젝트 공통 디버그 기능 네임스페이스
{
    public sealed class MonsterAIDebugPage : DebugPageProvider // F1 몬스터 감지·추적·공격·특수 규칙 상태 진단 페이지
    {
        public override string PageName => "Monster AI"; // F1 페이지 표시 이름
        public override int SortOrder => 100; // 근접·원거리 전투 페이지 뒤 AI 페이지 정렬 순서

        public override string BuildDebugText() // 현재 활성 몬스터 전체 AI 진단 문자열 생성
        {
            MonsterBrain[] brains = Object.FindObjectsByType<MonsterBrain>(FindObjectsInactive.Exclude, FindObjectsSortMode.None); // 공통 Brain을 사용하는 부패한 망자·궁수·변신 미믹 조회
            SmilingStatueBehavior[] statues = Object.FindObjectsByType<SmilingStatueBehavior>(FindObjectsInactive.Exclude, FindObjectsSortMode.None); // 활성 웃는 석상 규칙 행동 조회
            ChestMimicBehavior[] mimics = Object.FindObjectsByType<ChestMimicBehavior>(FindObjectsInactive.Exclude, FindObjectsSortMode.None); // 활성 상자 미믹 위장·변신 행동 조회
            StringBuilder builder = new StringBuilder(4200); // 4종 몬스터 상세 진단 문자열 버퍼 생성
            builder.AppendLine("MONSTER AI"); // 페이지 제목 출력
            builder.AppendLine("────────────────────────────"); // 구분선 출력
            builder.AppendLine($"Brain Agents     : {brains.Length}"); // 공통 Brain 활성 몬스터 수 출력
            builder.AppendLine($"Smiling Statues  : {statues.Length}"); // 웃는 석상 수 출력
            builder.AppendLine($"Chest Mimics     : {mimics.Length}"); // 상자 미믹 수 출력
            builder.AppendLine($"Enemy Arrows     : {MonsterArrowProjectile.ActiveProjectileCount}"); // 현재 날아가거나 박힌 적 화살 수 출력

            if (MonsterNoiseSystem.HasNoise) // 런타임 마지막 소음 존재 여부 확인
            {
                MonsterNoiseEvent noise = MonsterNoiseSystem.LastNoise; // 마지막 공통 소음 데이터 조회
                builder.AppendLine($"Last Noise       : {noise.Label} / {noise.Radius:0.0}m"); // 마지막 소음 종류와 전달 거리 출력
            }
            else // 소음 이력 없음 처리
            {
                builder.AppendLine("Last Noise       : None"); // 소음 없음 안내 출력
            }

            if (brains.Length == 0 && statues.Length == 0 && mimics.Length == 0) // Play Mode 몬스터 미소환 여부 확인
            {
                builder.AppendLine(); // 여백 출력
                builder.AppendLine("No active Day17 monster. Play Mode spawn을 확인하세요."); // 소환 상태 안내 출력
                return builder.ToString(); // 현재 진단 문자열 반환
            }

            for (int index = 0; index < brains.Length; index++) // 공통 Brain 몬스터 전체 진단 순회
            {
                MonsterBrain brain = brains[index]; // 현재 몬스터 AI 조회

                if (brain == null) // 유효 몬스터 참조 여부 확인
                {
                    continue; // 누락 인스턴스 건너뜀
                }

                AppendBrainMonster(builder, brain, index + 1); // 단일 공통 AI 몬스터 상세 상태 출력
            }

            for (int index = 0; index < statues.Length; index++) // 웃는 석상 규칙 상태 전체 출력
            {
                AppendStatue(builder, statues[index], index + 1); // 석상 관찰·거리·공격 상태 출력
            }

            for (int index = 0; index < mimics.Length; index++) // 상자 미믹 규칙 상태 전체 출력
            {
                AppendMimic(builder, mimics[index], index + 1); // 미믹 위장·변신·공통 AI 상태 출력
            }

            return builder.ToString(); // 완성된 진단 문자열 반환
        }

        private static void AppendBrainMonster(StringBuilder builder, MonsterBrain brain, int index) // 공통 Brain 기반 몬스터 감지·이동·공격 정보 추가
        {
            MonsterData data = brain.Data; // 몬스터 데이터 조회
            MonsterSensor sensor = brain.Sensor; // 감각 기능 조회
            MonsterTargetSelector selector = brain.TargetSelector; // 대상·기억 기능 조회
            CorruptedUndeadArcherAttack rangedAttack = brain.RangedAttack; // 궁수 원거리 공격 기능 조회
            MonsterMeleeAttack meleeAttack = brain.MeleeAttack; // 부패한 망자·미믹 근접 공격 기능 조회
            builder.AppendLine(); // 몬스터 섹션 여백 출력
            builder.AppendLine($"[B{index:00}] {(data == null ? brain.name : data.DisplayName)} / {(data == null ? "Unknown" : data.Archetype.ToString())}"); // 몬스터 번호·이름·유형 출력
            builder.AppendLine($"State      : {(brain.enabled ? brain.State.ToString() : "Brain Disabled")} / {brain.StateAge:0.0}s"); // 현재 AI 상태와 활성 여부 출력

            if (brain.Health != null) // 공통 체력 존재 여부 확인
            {
                builder.AppendLine($"HP         : {brain.Health.CurrentHealth:0}/{brain.Health.MaxHealth:0}"); // 현재·최대 체력 출력
            }

            builder.AppendLine($"Target     : {(brain.CurrentTarget == null ? "None" : brain.CurrentTarget.name)}"); // 현재 추적 대상 출력
            builder.AppendLine($"Distance   : {(float.IsPositiveInfinity(brain.DistanceToTarget) ? "-" : brain.DistanceToTarget.ToString("0.0") + "m")}"); // 현재 대상까지 거리 출력

            if (sensor != null) // 감각 기능 존재 여부 확인
            {
                builder.AppendLine($"Visible    : {(sensor.HasVisibleTarget ? "YES" : "NO")}"); // 현재 직접 시야 확보 여부 출력
                builder.AppendLine($"Heard      : {(sensor.HasRecentNoise ? sensor.LastHeardLabel : "None")}"); // 최근 청각 단서 출력
            }

            if (selector != null) // 대상 기억 기능 존재 여부 확인
            {
                Vector3 last = selector.LastKnownPosition; // 마지막 확인 위치 조회
                builder.AppendLine($"Memory     : {(selector.HasMemory ? selector.MemoryRemaining.ToString("0.0") + "s" : "None")}"); // 마지막 위치 기억 남은 시간 출력
                builder.AppendLine($"Last Pos   : {last.x:0.0}, {last.y:0.0}, {last.z:0.0}"); // 마지막 확인 위치 좌표 출력
            }

            if (data != null) // 몬스터 행동 데이터 존재 여부 확인
            {
                builder.AppendLine($"Vision     : {data.VisionRange:0}m / {data.VisionAngle:0}°"); // 시야 거리·각도 출력
                builder.AppendLine($"Attack     : <= {data.AttackRange:0.0}m / DMG {data.AttackDamage:0}"); // 공격 거리와 피해량 출력
            }

            if (rangedAttack != null) // 궁수 원거리 공격 기능 존재 여부 확인
            {
                builder.AppendLine($"Ranged Aim : {(rangedAttack.IsBusy ? rangedAttack.AimProgress * 100f : 0f):0}%"); // 현재 시위 당김 진행률 출력
                builder.AppendLine($"Cooldown   : {rangedAttack.CooldownRemaining:0.00}s"); // 다음 화살 공격까지 남은 시간 출력
                builder.AppendLine($"Arrow Speed: {rangedAttack.ProjectileSpeed:0.0}m/s"); // 몬스터 화살 탄속 출력
            }

            if (meleeAttack != null) // 근접 공격 기능 존재 여부 확인
            {
                builder.AppendLine($"Melee      : {(meleeAttack.IsBusy ? meleeAttack.AttackProgress * 100f : 0f):0}% / CD {meleeAttack.CooldownRemaining:0.00}s"); // 현재 근접 공격 진행률·쿨타임 출력
            }
        }

        private static void AppendStatue(StringBuilder builder, SmilingStatueBehavior statue, int index) // 웃는 석상 관찰 규칙 상태 출력
        {
            if (statue == null) // 유효 석상 참조 여부 확인
            {
                return; // 누락 인스턴스 건너뜀
            }

            builder.AppendLine(); // 석상 섹션 여백 출력
            builder.AppendLine($"[S{index:00}] 웃는 석상"); // 석상 번호 출력
            builder.AppendLine($"Observed   : {(statue.IsObserved ? "YES - FROZEN" : "NO - ACTIVE")}"); // 현재 관찰·정지 여부 출력
            builder.AppendLine($"Distance   : {(float.IsPositiveInfinity(statue.DistanceToTarget) ? "-" : statue.DistanceToTarget.ToString("0.0") + "m")}"); // 플레이어 거리 출력

            builder.AppendLine($"HP         : NONE / INVULNERABLE"); // 웃는 석상은 체력·피격·사망 개념이 없음을 표시
            builder.AppendLine($"Damageable : NO"); // 플레이어 공격으로 타격되지 않는 영구 불사 상태 표시

            if (statue.Data != null) // 석상 데이터 존재 여부 확인
            {
                builder.AppendLine($"Move       : {statue.Data.ChaseSpeed:0.0}m/s when unseen"); // 관찰되지 않을 때 추적 속도 출력
                builder.AppendLine($"Attack     : {statue.Data.AttackDamage:0} / <= {statue.Data.AttackRange:0.0}m"); // 공격 피해·거리 출력
            }
        }

        private static void AppendMimic(StringBuilder builder, ChestMimicBehavior mimic, int index) // 상자 미믹 위장·변신 상태 출력
        {
            if (mimic == null) // 유효 미믹 참조 여부 확인
            {
                return; // 누락 인스턴스 건너뜀
            }

            builder.AppendLine(); // 미믹 섹션 여백 출력
            builder.AppendLine($"[M{index:00}] 상자 미믹"); // 미믹 번호 출력
            builder.AppendLine($"Disguised  : {(mimic.IsDisguised ? "YES" : "NO")}"); // 현재 상자 위장 여부 출력
            builder.AppendLine($"Revealing  : {(mimic.IsRevealing ? mimic.RevealProgress * 100f : 0f):0}%"); // 현재 변신 진행률 출력

            if (mimic.Health != null) // 미믹 체력 존재 여부 확인
            {
                builder.AppendLine($"HP         : {mimic.Health.CurrentHealth:0}/{mimic.Health.MaxHealth:0}"); // 현재·최대 체력 출력
            }

            if (mimic.Brain != null) // 변신 후 공통 AI Brain 존재 여부 확인
            {
                builder.AppendLine($"Brain      : {(mimic.Brain.enabled ? mimic.Brain.State.ToString() : "Dormant")}"); // 위장/변신 후 AI 활성 상태 출력
            }
        }
    }
}
