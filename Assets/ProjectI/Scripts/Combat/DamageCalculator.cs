using UnityEngine; // 피해량 수치 보정 기능 참조

namespace ProjectI.Combat // 공통 전투 시스템 네임스페이스
{
    public static class DamageCalculator // 향후 방어력·저항·약점을 연결할 공통 피해 계산 단계
    {
        public static float Calculate(DamageInfo damageInfo, IDamageable target) // 현재 규칙 기준 최종 적용 전 피해량 계산
        {
            if (target == null || !target.IsAlive) // 유효하고 생존한 피격 대상 여부 확인
            {
                return 0f; // 유효하지 않은 대상 피해량 0 반환
            }

            return Mathf.Max(0f, damageInfo.BaseDamage); // Day14에서는 기본 피해량을 안전 범위로 보정하여 반환
        }
    }
}
