using System.Text; // F1 원거리 전투 진단 문자열 구성 기능 참조
using ProjectI.Combat; // 공통 Damage Pipeline 진단 기능 참조
using ProjectI.Combat.Ranged; // 석궁·리볼버 상태 참조
using ProjectI.Items; // 현재 빠른 슬롯 장착 아이템 조회 기능 참조
using UnityEngine; // 유니티 오브젝트 검색 기능 참조

namespace ProjectI.Diagnostics // 프로젝트 공통 디버그 기능 네임스페이스
{
    public sealed class RangedCombatDebugPage : DebugPageProvider // Day16 석궁·리볼버 F1 상태 진단 페이지
    {
        public override string PageName => "Ranged Combat"; // F1 페이지 이름
        public override int SortOrder => 85; // 기존 Combat 페이지 다음쪽 정렬 순서

        public override string BuildDebugText() // 현재 원거리 전투 상태 문자열 생성
        {
            StringBuilder builder = new StringBuilder(); // 진단 텍스트 조립용 문자열 빌더 생성
            PlayerCarryController carry = Object.FindFirstObjectByType<PlayerCarryController>(); // 현재 플레이어 화면 운반 기능 조회
            WorldItem heldItem = carry == null ? null : carry.HeldItem; // 현재 손에 든 빠른 슬롯 아이템 조회
            CrossbowWeaponItem heldCrossbow = heldItem == null ? null : heldItem.GetComponent<CrossbowWeaponItem>(); // 현재 석궁 장착 여부 조회
            RevolverWeaponItem heldRevolver = heldItem == null ? null : heldItem.GetComponent<RevolverWeaponItem>(); // 현재 리볼버 장착 여부 조회
            CrossbowWeaponItem anyCrossbow = heldCrossbow != null ? heldCrossbow : Object.FindFirstObjectByType<CrossbowWeaponItem>(); // 비장착 상태 포함 첫 석궁 조회
            RevolverWeaponItem anyRevolver = heldRevolver != null ? heldRevolver : Object.FindFirstObjectByType<RevolverWeaponItem>(); // 비장착 상태 포함 첫 리볼버 조회
            builder.AppendLine("RANGED COMBAT"); // 진단 제목 추가
            builder.AppendLine(); // 제목 아래 빈 줄 추가
            builder.AppendLine($"Held Weapon : {(heldItem == null ? "None" : heldItem.DisplayName)}"); // 현재 장착 원거리 무기 표시
            builder.AppendLine(); // 섹션 구분 빈 줄 추가
            AppendCrossbow(builder, anyCrossbow); // 석궁 탄약·조준·장전 상태 추가
            builder.AppendLine(); // 섹션 구분 빈 줄 추가
            AppendRevolver(builder, anyRevolver); // 리볼버 6발·탄퍼짐·장전 상태 추가
            builder.AppendLine(); // 섹션 구분 빈 줄 추가
            builder.AppendLine("[Projectile]"); // 볼트 투사체 섹션 제목 추가
            builder.AppendLine($"Active Bolts : {CrossbowBoltProjectile.ActiveProjectileCount}"); // 현재 월드 활성 볼트 수 표시
            builder.AppendLine(); // 섹션 구분 빈 줄 추가
            builder.AppendLine("[Damage Pipeline]"); // 공통 피해 진단 섹션 제목 추가

            if (DamagePipeline.HasResult) // 마지막 공통 피해 결과 존재 여부 확인
            {
                builder.AppendLine($"Last Target : {(DamagePipeline.LastResult.TargetObject == null ? "None" : DamagePipeline.LastResult.TargetObject.name)}"); // 마지막 명중 대상 표시
                builder.AppendLine($"Damage      : {DamagePipeline.LastResult.AppliedDamage:0.0}"); // 실제 적용 피해량 표시
                builder.AppendLine($"Result      : {DamagePipeline.LastResult.Reason}"); // 피해 적용·차단 결과 표시
            }
            else // 아직 원거리·근접 공통 피해 결과가 없는 경우 처리
            {
                builder.AppendLine("Last Result : None"); // 피해 기록 없음 표시
            }

            builder.AppendLine(); // 조작 안내 전 빈 줄 추가
            builder.AppendLine("RMB : Aim / Zoom"); // 우클릭 조준 안내
            builder.AppendLine("LMB : Fire"); // 좌클릭 발사 안내
            builder.AppendLine("R   : Reload"); // R 재장전 안내
            return builder.ToString(); // 완성된 F1 진단 문자열 반환
        }

        private static void AppendCrossbow(StringBuilder builder, CrossbowWeaponItem crossbow) // 석궁 상태 문자열 추가
        {
            builder.AppendLine("[Crossbow]"); // 석궁 섹션 제목 추가

            if (crossbow == null) // 씬 석궁 누락 여부 확인
            {
                builder.AppendLine("Missing"); // 석궁 누락 표시
                return; // 석궁 상태 출력 종료
            }

            builder.AppendLine($"Loaded       : {(crossbow.Loaded ? "YES" : "NO")}"); // 현재 볼트 장전 여부 표시
            builder.AppendLine($"Reserve Bolt : {crossbow.ReserveBolts}"); // 예비 볼트 수 표시
            builder.AppendLine($"Velocity      : {crossbow.ProjectileSpeed:0.0} m/s"); // 포물선 볼트 초기 속도 표시
            builder.AppendLine($"Damage        : {crossbow.BaseDamage:0.0}"); // 석궁 피해량 표시
            builder.AppendLine($"Aiming        : {(crossbow.IsAiming ? "YES" : "NO")}"); // 석궁 조준 상태 표시
            builder.AppendLine($"Aim FOV       : {crossbow.AimFieldOfView:0.0}"); // 석궁 확대 FOV 표시
            builder.AppendLine($"Reload        : {(crossbow.IsReloading ? $"{crossbow.ReloadProgress * 100f:0}%" : "READY")}"); // 석궁 재장전 진행률 표시
        }

        private static void AppendRevolver(StringBuilder builder, RevolverWeaponItem revolver) // 리볼버 상태 문자열 추가
        {
            builder.AppendLine("[Revolver]"); // 리볼버 섹션 제목 추가

            if (revolver == null) // 씬 리볼버 누락 여부 확인
            {
                builder.AppendLine("Missing"); // 리볼버 누락 표시
                return; // 리볼버 상태 출력 종료
            }

            builder.AppendLine($"Cylinder      : {revolver.LoadedRounds} / {revolver.CylinderCapacity}"); // 현재 6발 실린더 상태 표시
            builder.AppendLine($"Reserve Ammo  : {revolver.ReserveRounds}"); // 회수 불가 예비 탄약 수 표시
            builder.AppendLine($"Spread        : {revolver.CurrentSpread:0.00} deg"); // 현재 연사 누적 탄퍼짐 표시
            builder.AppendLine($"Damage        : {revolver.BaseDamage:0.0}"); // 리볼버 피해량 표시
            builder.AppendLine($"Aiming        : {(revolver.IsAiming ? "YES" : "NO")}"); // 리볼버 조준 상태 표시
            builder.AppendLine($"Aim FOV       : {revolver.AimFieldOfView:0.0}"); // 리볼버 조준 FOV 표시
            builder.AppendLine($"Reload        : {(revolver.IsReloading ? $"{revolver.ReloadProgress * 100f:0}%" : "READY")}"); // 리볼버 실린더 장전 진행률 표시
        }
    }
}
