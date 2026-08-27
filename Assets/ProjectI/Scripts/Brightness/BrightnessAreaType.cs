namespace ProjectI.Brightness // 밝기 시스템 네임스페이스
{
    public enum BrightnessAreaType // 현재 밝기 계산 공간 종류
    {
        Outdoor, // 자연광과 외부 광원을 함께 계산하는 외부
        Indoor // 현재 방 영역 내부 광원만 계산하는 내부
    }
}
