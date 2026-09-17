using System; // 콜백
using System.Collections.Generic; // 목록
using ProjectI.UI; // 서버 목록 항목
using Steamworks; // Steamworks.NET
using UnityEngine; // 유니티 기본 기능 참조

namespace ProjectI.Net.Steam // Steam 연결 네임스페이스
{
    public static class SteamLobbyService // 40일차: Steam 로비 만들기·찾기·들어가기·초대·게임 참가 요청 (41일차: 방 코드)
    {
        public const string GameKey = "game"; // 로비 데이터: 게임 구분
        public const string GameValue = "ProjectI"; // 게임 이름
        public const string VersionKey = "ver"; // 접속 버전
        public const string NameKey = "name"; // 방 이름
        public const string HostKey = "host"; // 방장 Steam ID
        public const string DayKey = "day"; // 일차
        public const string ChallengeKey = "challenge"; // 도전 원정
        public const string LocationKey = "loc"; // 방장 위치 (지연 추정)
        public const string CodeKey = "code"; // 41일차: 방 코드 (정리된 6자리)
        public const string ListedKey = "listed"; // 41일차: 서버 목록 표시 여부 ("1" 공개)

        private static CallResult<LobbyCreated_t> createdResult; // 만들기 결과
        private static CallResult<LobbyEnter_t> enterResult; // 들어가기 결과
        private static CallResult<LobbyMatchList_t> listResult; // 목록 결과
        private static CallResult<LobbyMatchList_t> codeResult; // 코드 찾기 결과
        private static Action<bool, CSteamID, string> pendingCode; // 코드 찾기 콜백 (찾음, 로비, 안내)
        private static string pendingCodeValue = string.Empty; // 찾는 코드
        private static string pendingRoomCode = string.Empty; // 만들 방 코드
        private static RoomVisibility pendingVisibility = RoomVisibility.Public; // 만들 방 공개 범위
        private static Callback<GameLobbyJoinRequested_t> joinRequested; // 친구 목록 "게임 참가"·초대 수락
        private static Action<bool, string> pendingCreate; // 만들기 콜백
        private static Action<bool, CSteamID, string> pendingEnter; // 들어가기 콜백 (성공, 방장, 안내)
        private static Action<IReadOnlyList<ServerListing>> pendingList; // 목록 콜백
        private static string pendingName = string.Empty; // 만들 방 이름

        public static CSteamID CurrentLobby { get; private set; } = CSteamID.Nil; // 현재 로비
        public static bool InLobby => CurrentLobby.IsValid(); // 로비 안
        public static string CurrentCode => InLobby && SteamService.Initialized ? SteamMatchmaking.GetLobbyData(CurrentLobby, CodeKey) : string.Empty; // 현재 방 코드 (정리된 6자리)
        public static bool IsLobbyOwner => InLobby && SteamService.Initialized && SteamMatchmaking.GetLobbyOwner(CurrentLobby) == SteamService.LocalId; // 방장
        public static event Action<CSteamID> JoinRequested; // 친구 참가 요청 (메뉴가 처리)

        public static void ResetStatics() // 플레이 반복 대비 초기화
        {
            createdResult = null; // 콜백
            enterResult = null; // 콜백
            listResult = null; // 콜백
            codeResult = null; // 콜백
            pendingCode = null; // 대기
            joinRequested = null; // 콜백
            pendingCreate = null; // 대기
            pendingEnter = null; // 대기
            pendingList = null; // 대기
            JoinRequested = null; // 구독
            CurrentLobby = CSteamID.Nil; // 로비
        }

        public static bool EnsureCallbacks() // 콜백 준비
        {
            if (!SteamService.EnsureInitialized()) // Steam 없음
            {
                return false; // 실패
            }

            createdResult ??= CallResult<LobbyCreated_t>.Create(OnLobbyCreated); // 만들기
            enterResult ??= CallResult<LobbyEnter_t>.Create(OnLobbyEntered); // 들어가기
            listResult ??= CallResult<LobbyMatchList_t>.Create(OnLobbyList); // 목록
            codeResult ??= CallResult<LobbyMatchList_t>.Create(OnCodeList); // 코드 찾기
            joinRequested ??= Callback<GameLobbyJoinRequested_t>.Create(OnJoinRequested); // 친구 참가
            return true; // 준비됨
        }

        public static void Create(string roomName, int maxPlayers, RoomVisibility visibility, string roomCode, Action<bool, string> onDone) // 방장: 로비 만들기 (공개 또는 코드 전용)
        {
            if (!EnsureCallbacks()) // Steam 없음
            {
                onDone?.Invoke(false, SteamService.FailureReason); // 실패
                return; // 종료
            }

            pendingCreate = onDone; // 콜백
            pendingName = string.IsNullOrWhiteSpace(roomName) ? $"{SteamService.PersonaName}의 원정대" : roomName; // 이름
            pendingRoomCode = RoomCode.Normalize(roomCode); // 코드
            pendingVisibility = visibility; // 공개 범위
            ELobbyType type = visibility == RoomVisibility.Public ? ELobbyType.k_ELobbyTypePublic : ELobbyType.k_ELobbyTypeInvisible; // 코드 전용은 검색만 되고 친구 목록에는 안 보임
            SteamAPICall_t call = SteamMatchmaking.CreateLobby(type, maxPlayers); // 요청
            createdResult.Set(call); // 결과 대기
        }

        public static void Enter(CSteamID lobby, Action<bool, CSteamID, string> onDone) // 참가자: 로비 들어가기
        {
            if (!EnsureCallbacks()) // Steam 없음
            {
                onDone?.Invoke(false, CSteamID.Nil, SteamService.FailureReason); // 실패
                return; // 종료
            }

            pendingEnter = onDone; // 콜백
            enterResult.Set(SteamMatchmaking.JoinLobby(lobby)); // 요청
        }

        public static void Leave() // 로비 나가기
        {
            if (InLobby && SteamService.Initialized) // 로비 안
            {
                SteamMatchmaking.LeaveLobby(CurrentLobby); // 나가기
            }

            CurrentLobby = CSteamID.Nil; // 정리
        }

        public static void RequestList(Action<IReadOnlyList<ServerListing>> onDone) // 공개 로비 목록 (같은 게임·같은 버전)
        {
            if (!EnsureCallbacks()) // Steam 없음
            {
                onDone?.Invoke(new List<ServerListing>()); // 빈 목록
                return; // 종료
            }

            pendingList = onDone; // 콜백
            SteamMatchmaking.AddRequestLobbyListStringFilter(GameKey, GameValue, ELobbyComparison.k_ELobbyComparisonEqual); // 같은 게임
            SteamMatchmaking.AddRequestLobbyListStringFilter(VersionKey, NetworkSession.ProtocolTag, ELobbyComparison.k_ELobbyComparisonEqual); // 같은 버전
            SteamMatchmaking.AddRequestLobbyListStringFilter(ListedKey, "1", ELobbyComparison.k_ELobbyComparisonEqual); // 공개 방만 (코드 전용 제외)
            SteamMatchmaking.AddRequestLobbyListDistanceFilter(ELobbyDistanceFilter.k_ELobbyDistanceFilterWorldwide); // 전 세계
            SteamMatchmaking.AddRequestLobbyListResultCountFilter(50); // 최대 50개
            listResult.Set(SteamMatchmaking.RequestLobbyList()); // 요청
        }

        public static void FindByCode(string code, Action<bool, CSteamID, string> onDone) // 41일차: 방 코드로 로비 찾기 (공개·코드 전용 모두)
        {
            if (!EnsureCallbacks()) // Steam 없음
            {
                onDone?.Invoke(false, CSteamID.Nil, $"방 코드는 Steam 연결이 필요합니다 ({SteamService.FailureReason})"); // 실패
                return; // 종료
            }

            pendingCode = onDone; // 콜백
            pendingCodeValue = RoomCode.Normalize(code); // 코드
            SteamMatchmaking.AddRequestLobbyListStringFilter(GameKey, GameValue, ELobbyComparison.k_ELobbyComparisonEqual); // 같은 게임
            SteamMatchmaking.AddRequestLobbyListStringFilter(CodeKey, pendingCodeValue, ELobbyComparison.k_ELobbyComparisonEqual); // 같은 코드 (버전은 결과에서 확인해 이유 안내)
            SteamMatchmaking.AddRequestLobbyListDistanceFilter(ELobbyDistanceFilter.k_ELobbyDistanceFilterWorldwide); // 전 세계
            SteamMatchmaking.AddRequestLobbyListResultCountFilter(10); // 충돌 대비 몇 개
            codeResult.Set(SteamMatchmaking.RequestLobbyList()); // 요청
        }

        public static void UpdateHostData(int players, int day, bool challenge) // 방장: 로비 정보 갱신
        {
            if (!IsLobbyOwner) // 방장 아님
            {
                return; // 생략
            }

            SteamMatchmaking.SetLobbyData(CurrentLobby, DayKey, day.ToString()); // 일차
            SteamMatchmaking.SetLobbyData(CurrentLobby, ChallengeKey, challenge ? "1" : "0"); // 도전 원정
            SteamMatchmaking.SetLobbyJoinable(CurrentLobby, players < NetworkSession.MaxPlayers); // 가득 차면 목록에서 숨김
        }

        public static void OpenInviteOverlay() // 친구 초대 창
        {
            if (InLobby && SteamService.Initialized) // 로비 안
            {
                SteamFriends.ActivateGameOverlayInviteDialog(CurrentLobby); // Steam 오버레이
            }
        }

        public static bool TryGetLaunchLobby(out CSteamID lobby) // 초대로 게임을 켠 경우 (+connect_lobby <ID>)
        {
            lobby = CSteamID.Nil; // 기본
            string[] args = Environment.GetCommandLineArgs(); // 실행 인자

            for (int index = 0; index < args.Length - 1; index++) // 인자
            {
                if (args[index] == "+connect_lobby" && ulong.TryParse(args[index + 1], out ulong id)) // 로비 번호
                {
                    lobby = new CSteamID(id); // 로비
                    return lobby.IsValid(); // 결과
                }
            }

            return false; // 없음
        }

        private static void OnLobbyCreated(LobbyCreated_t result, bool ioFailure) // 만들기 결과
        {
            Action<bool, string> callback = pendingCreate; // 콜백
            pendingCreate = null; // 정리

            if (ioFailure || result.m_eResult != EResult.k_EResultOK) // 실패
            {
                callback?.Invoke(false, $"Steam 방을 만들지 못했습니다 ({result.m_eResult})"); // 실패
                return; // 종료
            }

            CurrentLobby = new CSteamID(result.m_ulSteamIDLobby); // 로비
            SteamMatchmaking.SetLobbyData(CurrentLobby, GameKey, GameValue); // 게임
            SteamMatchmaking.SetLobbyData(CurrentLobby, VersionKey, NetworkSession.ProtocolTag); // 버전
            SteamMatchmaking.SetLobbyData(CurrentLobby, NameKey, pendingName); // 이름
            SteamMatchmaking.SetLobbyData(CurrentLobby, HostKey, SteamService.LocalId.m_SteamID.ToString()); // 방장
            SteamMatchmaking.SetLobbyData(CurrentLobby, LocationKey, LocalPingLocation()); // 위치
            SteamMatchmaking.SetLobbyData(CurrentLobby, CodeKey, pendingRoomCode); // 방 코드
            SteamMatchmaking.SetLobbyData(CurrentLobby, ListedKey, pendingVisibility == RoomVisibility.Public ? "1" : "0"); // 목록 표시
            Debug.Log($"[Project I] Steam 로비 생성 / {pendingName} / 코드 {RoomCode.Format(pendingRoomCode)} / {pendingVisibility} / {CurrentLobby.m_SteamID}"); // 기록
            callback?.Invoke(true, "Steam 방이 열렸습니다"); // 성공
        }

        private static void OnLobbyEntered(LobbyEnter_t result, bool ioFailure) // 들어가기 결과
        {
            Action<bool, CSteamID, string> callback = pendingEnter; // 콜백
            pendingEnter = null; // 정리
            CSteamID lobby = new CSteamID(result.m_ulSteamIDLobby); // 로비

            if (ioFailure || result.m_EChatRoomEnterResponse != (uint)EChatRoomEnterResponse.k_EChatRoomEnterResponseSuccess) // 실패
            {
                callback?.Invoke(false, CSteamID.Nil, "방에 들어가지 못했습니다 (가득 찼거나 닫힌 방)"); // 실패
                return; // 종료
            }

            if (SteamMatchmaking.GetLobbyData(lobby, VersionKey) != NetworkSession.ProtocolTag) // 버전 다름
            {
                SteamMatchmaking.LeaveLobby(lobby); // 나가기
                callback?.Invoke(false, CSteamID.Nil, $"게임 버전이 다릅니다 (방 {SteamMatchmaking.GetLobbyData(lobby, VersionKey)})"); // 실패
                return; // 종료
            }

            CurrentLobby = lobby; // 기록
            CSteamID host = ulong.TryParse(SteamMatchmaking.GetLobbyData(lobby, HostKey), out ulong hostId) ? new CSteamID(hostId) : SteamMatchmaking.GetLobbyOwner(lobby); // 방장
            callback?.Invoke(host.IsValid(), host, host.IsValid() ? "방장에게 연결하는 중..." : "방장 정보를 찾지 못했습니다"); // 결과
        }

        private static void OnLobbyList(LobbyMatchList_t result, bool ioFailure) // 목록 결과
        {
            Action<IReadOnlyList<ServerListing>> callback = pendingList; // 콜백
            pendingList = null; // 정리
            List<ServerListing> listings = new List<ServerListing>(); // 결과

            for (int index = 0; !ioFailure && index < result.m_nLobbiesMatching; index++) // 로비
            {
                CSteamID lobby = SteamMatchmaking.GetLobbyByIndex(index); // 로비
                string name = SteamMatchmaking.GetLobbyData(lobby, NameKey); // 이름
                int.TryParse(SteamMatchmaking.GetLobbyData(lobby, DayKey), out int day); // 일차
                listings.Add(new ServerListing
                {
                    Id = lobby.m_SteamID.ToString(), // 로비 번호
                    Name = string.IsNullOrWhiteSpace(name) ? "이름 없는 원정대" : (day > 0 ? $"{name}  ·  {day}일차" : name), // 이름
                    Players = SteamMatchmaking.GetNumLobbyMembers(lobby), // 인원
                    MaxPlayers = Mathf.Max(1, SteamMatchmaking.GetLobbyMemberLimit(lobby)), // 최대
                    ChallengeMode = SteamMatchmaking.GetLobbyData(lobby, ChallengeKey) == "1", // 도전 원정
                    PingMs = EstimatePing(SteamMatchmaking.GetLobbyData(lobby, LocationKey)), // 지연
                    Region = "STEAM", // 지역 표시
                });
            }

            callback?.Invoke(listings); // 전달
        }

        private static void OnCodeList(LobbyMatchList_t result, bool ioFailure) // 코드 찾기 결과
        {
            Action<bool, CSteamID, string> callback = pendingCode; // 콜백
            pendingCode = null; // 정리
            string shown = RoomCode.Format(pendingCodeValue); // 표시 코드

            if (ioFailure) // 통신 실패
            {
                callback?.Invoke(false, CSteamID.Nil, "Steam 에서 방을 찾지 못했습니다 (연결 확인)"); // 실패
                return; // 종료
            }

            string otherVersion = null; // 버전만 다른 방

            for (int index = 0; index < result.m_nLobbiesMatching; index++) // 같은 코드 로비
            {
                CSteamID lobby = SteamMatchmaking.GetLobbyByIndex(index); // 로비
                string version = SteamMatchmaking.GetLobbyData(lobby, VersionKey); // 버전

                if (version != NetworkSession.ProtocolTag) // 버전 다름
                {
                    otherVersion = version; // 기록
                    continue; // 다음
                }

                if (SteamMatchmaking.GetNumLobbyMembers(lobby) >= Mathf.Max(1, SteamMatchmaking.GetLobbyMemberLimit(lobby))) // 가득 참
                {
                    callback?.Invoke(false, CSteamID.Nil, $"방 {shown} 이 가득 찼습니다"); // 실패
                    return; // 종료
                }

                Debug.Log($"[Project I] 방 코드 {shown} → 로비 {lobby.m_SteamID}"); // 기록
                callback?.Invoke(true, lobby, $"방 {shown} 을 찾았습니다 — 들어가는 중..."); // 찾음
                return; // 종료
            }

            callback?.Invoke(false, CSteamID.Nil, otherVersion != null
                ? $"방 {shown} 의 게임 버전이 다릅니다 (방 {otherVersion} · 내 {NetworkSession.ProtocolTag})"
                : $"코드 {shown} 인 방이 없습니다 (닫혔거나 가득 찬 방)"); // 실패
        }

        private static void OnJoinRequested(GameLobbyJoinRequested_t request) // 친구 목록 "게임 참가"·초대 수락
        {
            Debug.Log($"[Project I] Steam 참가 요청 / 로비 {request.m_steamIDLobby.m_SteamID}"); // 기록
            JoinRequested?.Invoke(request.m_steamIDLobby); // 메뉴·세션이 처리
        }

        private static string LocalPingLocation() // 내 위치 문자열 (지연 추정용)
        {
            SteamNetworkingUtils.GetLocalPingLocation(out SteamNetworkPingLocation_t location); // 위치
            SteamNetworkingUtils.ConvertPingLocationToString(ref location, out string text, Constants.k_cchMaxSteamNetworkingPingLocationString); // 문자열
            return text ?? string.Empty; // 반환
        }

        private static int EstimatePing(string locationText) // 방장까지 예상 지연 (모르면 999)
        {
            if (string.IsNullOrEmpty(locationText) || !SteamNetworkingUtils.ParsePingLocationString(locationText, out SteamNetworkPingLocation_t location)) // 없음
            {
                return 999; // 모름
            }

            int ping = SteamNetworkingUtils.EstimatePingTimeFromLocalHost(ref location); // 추정
            return ping < 0 ? 999 : ping; // 반환
        }
    }
}
