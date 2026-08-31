namespace ProjectI.Combat // 공통 전투 시스템 네임스페이스
{
    public enum CombatDamageType // 공통 Damage Pipeline 피해 종류
    {
        Physical, // 일반 물리 피해
        Piercing, // 관통 피해
        Blunt, // 둔중 피해
        Fire, // 화염 피해
        Electric, // 전기 피해
        Poison, // 독 피해
        Fall, // 추락 피해
        Trap, // 함정 피해
        Environment // 기타 환경 피해
    }
}
