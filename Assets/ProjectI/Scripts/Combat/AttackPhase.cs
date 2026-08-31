namespace ProjectI.Combat // 공통 전투 시스템 네임스페이스
{
    public enum AttackPhase // 공격 한 번의 공통 진행 단계
    {
        None, // 공격 없음 상태
        Windup, // 공격 준비 단계
        Active, // 실제 피해 판정 활성 단계
        Recovery // 공격 후 회복 단계
    }
}
