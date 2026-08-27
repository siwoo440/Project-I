namespace ProjectI.Brightness // 밝기 시스템 네임스페이스
{
    public enum BrightnessEmissionShape // 게임 로직에서 사용하는 광원 방사 형태
    {
        Omnidirectional, // 횃불처럼 모든 방향으로 퍼지는 광원
        Cone // 랜턴 빔처럼 특정 방향의 원뿔 영역만 강하게 비추는 광원
    }
}
