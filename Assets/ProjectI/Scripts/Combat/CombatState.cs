namespace ProjectI.Combat // 공통 전투 시스템 네임스페이스
{
    public enum CombatState // 전투 행위자의 공통 상태
    {
        Idle, // 전투 입력 대기 상태
        Attacking, // 단일 공격 진행 상태
        Cooldown, // 공격 종료 후 다음 공격 입력 차단 상태
        Staggered, // 피격 경직 상태
        Dead // 사망 상태
    }
}
