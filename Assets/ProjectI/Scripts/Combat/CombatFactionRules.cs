namespace ProjectI.Combat // 공통 전투 시스템 네임스페이스
{
    public static class CombatFactionRules // 진영별 기본 피해 허용 규칙
    {
        public static bool CanDamage(CombatFaction source, CombatFaction target) // 공격 진영이 대상 진영을 공격할 수 있는지 판정
        {
            if (source == CombatFaction.Neutral) // 중립 주체 공격 여부 확인
            {
                return false; // 중립은 스스로 공격하지 않는 기본 규칙 적용
            }

            if (target == CombatFaction.Environment) // 환경 자체를 체력 대상으로 취급하는지 확인
            {
                return false; // 기본 환경 진영은 직접 피해 대상에서 제외
            }

            if (source == CombatFaction.Environment) // 환경 피해 주체 여부 확인
            {
                return target != CombatFaction.Environment; // 환경 위험은 플레이어·아군·적·중립 모두 피해 허용
            }

            if (source == target) // 동일 진영 여부 확인
            {
                return false; // 기본 Friendly Fire 차단
            }

            if (target == CombatFaction.Neutral) // 중립 파괴 대상 여부 확인
            {
                return true; // 플레이어·아군·적 모두 중립 대상 공격 허용
            }

            if (source == CombatFaction.Player || source == CombatFaction.Ally) // 플레이어 측 공격 여부 확인
            {
                return target == CombatFaction.Enemy; // 플레이어 측은 적 진영만 공격 허용
            }

            if (source == CombatFaction.Enemy) // 적 진영 공격 여부 확인
            {
                return target == CombatFaction.Player || target == CombatFaction.Ally; // 적은 플레이어와 아군 공격 허용
            }

            return false; // 정의되지 않은 조합 기본 차단
        }
    }
}
