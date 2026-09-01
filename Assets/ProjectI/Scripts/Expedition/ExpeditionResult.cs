namespace ProjectI.Expedition // 원정 결과 기능 네임스페이스
{
    public enum ExpeditionResult // Day22 기본 원정 귀환 결과
    {
        None = 0, // 아직 결과가 확정되지 않은 상태
        NormalReturn = 1, // 모든 현재 플레이어가 생존한 정상 귀환
        PartialReturn = 2, // 일부 플레이어만 생존한 부분 귀환
        Failed = 3 // 생존 플레이어가 없는 원정 실패
    }
}
