using UnityEngine; // 유니티 Collider와 MonoBehaviour 기능 참조

namespace ProjectI.Combat // 공통 전투 시스템 네임스페이스
{
    public static class DamagePipeline // 모든 공격·함정·환경 피해가 공유하는 공통 처리 진입점
    {
        public static DamageInfo LastDamageInfo { get; private set; } // F1 진단용 마지막 피해 요청 공개
        public static CombatHitResult LastResult { get; private set; } // F1 진단용 마지막 처리 결과 공개
        public static int AppliedHitCount { get; private set; } // 런타임 동안 실제 적용된 피해 횟수 공개
        public static bool HasResult { get; private set; } // 마지막 처리 결과 존재 여부 공개

        public static bool TryApply(DamageInfo damageInfo, IDamageable target, out CombatHitResult result) // 공통 피해 규칙 검사와 실제 피해 적용
        {
            LastDamageInfo = damageInfo; // 마지막 피해 요청 진단 데이터 저장

            if (target == null || target.DamageTransform == null) // 유효 피해 대상 여부 확인
            {
                result = new CombatHitResult(false, null, damageInfo.BaseDamage, 0f, false, "Target Missing", damageInfo.HitPoint); // 대상 누락 결과 생성
                StoreResult(result); // 진단 결과 저장
                return false; // 피해 적용 실패 반환
            }

            GameObject targetObject = target.DamageTransform.gameObject; // 대표 피격 대상 오브젝트 조회

            if (!target.IsAlive) // 이미 사망한 대상 여부 확인
            {
                result = new CombatHitResult(false, targetObject, damageInfo.BaseDamage, 0f, true, "Target Dead", damageInfo.HitPoint); // 사망 대상 차단 결과 생성
                StoreResult(result); // 진단 결과 저장
                return false; // 피해 적용 실패 반환
            }

            if (damageInfo.BaseDamage <= 0f) // 유효 피해량 여부 확인
            {
                result = new CombatHitResult(false, targetObject, damageInfo.BaseDamage, 0f, false, "No Damage", damageInfo.HitPoint); // 피해량 없음 결과 생성
                StoreResult(result); // 진단 결과 저장
                return false; // 피해 적용 실패 반환
            }

            if (!CombatFactionRules.CanDamage(damageInfo.SourceFaction, target.Faction)) // 진영 간 피해 허용 여부 확인
            {
                result = new CombatHitResult(false, targetObject, damageInfo.BaseDamage, 0f, false, "Faction Blocked", damageInfo.HitPoint); // Friendly Fire 등 차단 결과 생성
                StoreResult(result); // 진단 결과 저장
                return false; // 피해 적용 실패 반환
            }

            float resolvedDamage = DamageCalculator.Calculate(damageInfo, target); // 공통 피해 계산 단계에서 최종 피해량 계산
            DamageInfo resolvedInfo = damageInfo.WithBaseDamage(resolvedDamage); // 계산된 피해량을 반영한 최종 피해 데이터 생성
            LastDamageInfo = resolvedInfo; // F1 진단에 계산 완료 피해 요청 저장
            float appliedDamage = Mathf.Max(0f, target.ApplyDamage(resolvedInfo)); // 실제 피해 대상에 계산 완료 피해 적용
            bool fatal = !target.IsAlive; // 피해 적용 후 사망 여부 계산
            result = new CombatHitResult(appliedDamage > 0f, targetObject, resolvedDamage, appliedDamage, fatal, appliedDamage > 0f ? "Applied" : "Rejected", damageInfo.HitPoint); // 최종 처리 결과 생성
            StoreResult(result); // 진단 결과 저장

            if (appliedDamage > 0f) // 실제 체력 감소 여부 확인
            {
                AppliedHitCount++; // 실제 적용된 피해 횟수 증가
                return true; // 피해 적용 성공 반환
            }

            return false; // 대상 내부에서 피해가 거부된 경우 실패 반환
        }

        public static IDamageable FindDamageable(Collider collider) // Collider 부모 계층에서 공통 피해 대상 검색
        {
            if (collider == null) // Collider 유효성 확인
            {
                return null; // 대상 없음 반환
            }

            MonoBehaviour[] behaviours = collider.GetComponentsInParent<MonoBehaviour>(true); // Collider 부모 계층의 기능 컴포넌트 조회

            foreach (MonoBehaviour behaviour in behaviours) // 부모 기능 컴포넌트 순회
            {
                if (behaviour is IDamageable damageable) // 공통 피해 대상 인터페이스 구현 여부 확인
                {
                    return damageable; // 첫 번째 공통 피해 대상 반환
                }
            }

            return null; // 공통 피해 대상 없음 반환
        }

        public static void ResetDiagnostics() // F1 진단용 마지막 결과 초기화
        {
            LastDamageInfo = default; // 마지막 피해 요청 기본값 초기화
            LastResult = default; // 마지막 처리 결과 기본값 초기화
            AppliedHitCount = 0; // 누적 피해 횟수 초기화
            HasResult = false; // 마지막 결과 없음 상태 저장
        }

        private static void StoreResult(CombatHitResult result) // 마지막 처리 결과 저장
        {
            LastResult = result; // F1 진단용 마지막 결과 저장
            HasResult = true; // 결과 존재 상태 활성화
        }
    }
}
