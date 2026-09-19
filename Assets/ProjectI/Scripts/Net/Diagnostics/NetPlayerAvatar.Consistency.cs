using Unity.Netcode; // 넷코드
using UnityEngine; // 유니티 기본 기능 참조

namespace ProjectI.Net // 협동 네트워크 네임스페이스
{
    public sealed partial class NetPlayerAvatar // 44일차: 참가자 화면 요약 보고 (몸체 주인만 · 방장이 비교)
    {
        private float nextConsistencyReport; // 다음 보고

        private void UpdateConsistencyReport() // 참가자 몸체: 주기적으로 내 화면 요약을 방장에게
        {
            if (!IsOwner || IsServer || Time.unscaledTime < nextConsistencyReport) // 참가자 내 몸체만 · 간격
            {
                return; // 생략
            }

            nextConsistencyReport = Time.unscaledTime + NetConsistency.Interval; // 다음
            CoopSnapshot snapshot = NetConsistency.Capture(); // 내 화면

            if (snapshot.Ready) // 이동 중이 아님
            {
                ConsistencyReportRpc(snapshot); // 보고
            }
        }

        public void ReportConsistencyNow() // 자동 시험: 바로 보고
        {
            nextConsistencyReport = 0f; // 다음 프레임에 보고
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        private void ConsistencyReportRpc(CoopSnapshot snapshot, RpcParams rpcParams = default) // 방장: 참가자 요약 비교
        {
            ulong sender = rpcParams.Receive.SenderClientId; // 대원

            if (sender != OwnerClientId || !NetGuard.Allow(sender, NetChannel.Report)) // 주인 아님·횟수 초과
            {
                return; // 무시
            }

            NetConsistency.Compare(sender, snapshot); // 비교
        }
    }
}
