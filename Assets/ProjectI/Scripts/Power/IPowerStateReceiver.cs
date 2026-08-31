namespace ProjectI.Power // 전력 상태 수신 기능 네임스페이스
{
    public interface IPowerStateReceiver // 공통 전력 상태 변경 수신 인터페이스
    {
        void OnPowerStateChanged(bool hasPower); // 전력 공급 상태 변경 전달
    }
}
