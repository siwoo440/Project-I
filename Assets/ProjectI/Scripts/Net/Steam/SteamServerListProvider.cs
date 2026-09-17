using System; // 콜백
using System.Collections.Generic; // 목록
using ProjectI.UI; // 서버 목록 규격
using Steamworks; // Steam ID

namespace ProjectI.Net.Steam // Steam 연결 네임스페이스
{
    public sealed class SteamServerListProvider : IServerListProvider // 40일차: 서버 창의 실제 Steam 로비 목록
    {
        public bool IsOnline => true; // 실제 목록

        public void Request(Action<IReadOnlyList<ServerListing>> onResult) // 목록 요청
        {
            SteamLobbyService.RequestList(onResult); // Steam 로비 검색
        }

        public void Join(ServerListing listing, Action<bool, string> onResult) // 참가
        {
            if (!ulong.TryParse(listing.Id, out ulong lobbyId)) // 번호 오류
            {
                onResult?.Invoke(false, "방 번호가 올바르지 않습니다"); // 실패
                return; // 종료
            }

            if (listing.IsFull) // 가득 참
            {
                onResult?.Invoke(false, $"'{listing.Name}' 방이 가득 찼습니다"); // 실패
                return; // 종료
            }

            bool started = NetworkSession.BeginSteamJoin(new CSteamID(lobbyId), out string error); // 참가 시작
            onResult?.Invoke(started, started ? $"'{listing.Name}' 에 들어가는 중..." : error); // 결과
        }
    }
}
