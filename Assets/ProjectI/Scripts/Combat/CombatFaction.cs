namespace ProjectI.Combat // 공통 전투 시스템 네임스페이스
{
    public enum CombatFaction // 피해 주체와 대상의 진영 구분
    {
        Neutral, // 중립 또는 파괴 가능한 공용 대상
        Player, // 플레이어 진영
        Ally, // 플레이어 아군 진영
        Enemy, // 적대 몬스터 진영
        Environment // 함정·추락·환경 위험 진영
    }
}
