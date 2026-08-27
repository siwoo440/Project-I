namespace ProjectI.Brightness // 밝기 시스템 네임스페이스
{
    public enum BrightnessSourceType // 광원의 공간 소속 판정 방식
    {
        Fixed, // 부모 구조의 IndoorBrightnessArea를 기준으로 소속을 고정하는 광원
        Portable // 현재 월드 위치를 기준으로 외부 또는 내부 방 소속을 계속 판정하는 이동형 광원
    }
}
