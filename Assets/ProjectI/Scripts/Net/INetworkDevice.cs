namespace ProjectI.Net // 협동 네트워크 네임스페이스
{
    public interface INetworkDevice // 39일차: 상태 번호 하나로 모든 대원 화면에서 같게 맞추는 장치 (전력·승강기·잠긴 문)
    {
        int NetworkState { get; } // 현재 상태 번호 (-1 = 맞출 상태 없음)
        void ApplyNetworkState(int state); // 받은 상태 적용 (다시 알리지 않음)
    }
}
