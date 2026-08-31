namespace ProjectI.Monsters // 몬스터 공통 AI 네임스페이스
{
    public enum MonsterState // 모든 몬스터가 공유하는 기본 행동 상태
    {
        Idle = 0, // 목표가 없는 대기 상태
        Suspicious = 1, // 소리나 단서를 감지한 경계 상태
        Investigate = 2, // 마지막 확인 위치나 소리 위치 조사 상태
        Chase = 3, // 시야에 들어온 대상을 추적하는 상태
        Attack = 4, // 공격 준비·발사 상태
        Retreat = 5, // 원거리 몬스터가 거리를 확보하는 후퇴 상태
        Staggered = 6, // 공통 CombatReaction 경직·넉백 반응 상태
        Dead = 7 // 체력이 0이 된 사망 상태
    }
}
