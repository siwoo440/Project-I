namespace ProjectI.Monsters // 몬스터 공통 AI 네임스페이스
{
    public enum MonsterArchetype // Day17 테스트 몬스터 종류 구분
    {
        CorruptedUndead = 0, // 플레이어를 추적해 근접 공격하는 기본 부패한 망자
        CorruptedUndeadArcher = 1, // 거리를 유지하며 활을 발사하는 부패한 망자 궁수
        SmilingStatue = 2, // 플레이어에게 관찰되면 멈추는 웃는 석상
        ChestMimic = 3 // 상자로 위장했다가 근접하면 변신·추적하는 미믹
    }
}
