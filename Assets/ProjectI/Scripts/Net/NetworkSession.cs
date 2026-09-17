using System; // 이벤트
using System.Collections; // 다시 연결
using System.Collections.Generic; // 42일차: 차단·원정대원 목록
using System.Text; // 접속 데이터
using ProjectI.Core; // 씬 이동
using ProjectI.Loop; // 맵 로더 준비 확인
using ProjectI.Net.Steam; // 40일차 Steam 연결
using ProjectI.Persistence; // 저장 준비 확인
using ProjectI.UI; // 알림
using Steamworks; // Steam ID
using Unity.Netcode; // 넷코드
using Unity.Netcode.Transports.UTP; // 직접 IP 연결
using UnityEngine; // 유니티 기본 기능 참조
using UnityEngine.SceneManagement; // 현재 씬

namespace ProjectI.Net // 협동 네트워크 네임스페이스
{
    public enum SessionMode // 네트워크 참여 방식
    {
        Offline, // 혼자 하기
        Host, // 방장 (서버 + 플레이어)
        Guest, // 참가자
    }

    public enum SessionTransport // 연결 방식
    {
        Direct, // 주소·포트 직접 연결 (개발·같은 네트워크)
        Steam, // Steam 로비 + Steam 중계망 (포트 개방 불필요)
    }

    [DisallowMultipleComponent] // 하나만
    [RequireComponent(typeof(NetworkManager))] // 넷코드 관리자
    [RequireComponent(typeof(UnityTransport))] // 직접 IP 연결
    public sealed class NetworkSession : MonoBehaviour // 방 열기·참가·나가기·연결 끊김 처리 (37일차: 직접 IP, 40일차에 Steam 로비로 교체)
    {
        public const string PrefabResourcePath = "Net/ProjectINetwork"; // 네트워크 관리자 프리팹
        public const string WorldStateResourcePath = "Net/NetWorldState"; // 월드 상태 프리팹
        public const int MaxPlayers = 4; // 최대 인원
        public const ushort DefaultPort = 7777; // 기본 포트
        public const string ProtocolTag = "ProjectI-0.42"; // 접속 확인용 버전 (다르면 거부, 42일차 보안)
        private static NetworkSession instance; // 현재 세션
        private NetworkManager manager; // 넷코드 관리자
        private UnityTransport transport; // 연결 방식
        private bool pendingHost; // 게임 월드 준비 후 방 열기 대기
        private bool leaving; // 스스로 나가는 중
        private ushort hostPort; // 방 포트
        private SteamNetworkTransport steamTransport; // Steam 연결 모듈
        private int reconnectAttempts; // 다시 연결 시도 횟수
        private bool reconnecting; // 다시 연결 중
        private float nextLobbyUpdate; // 로비 정보 갱신 시각
        private const int MaxReconnectAttempts = 3; // 다시 연결 최대 횟수
        private static CSteamID pendingSteamJoin = CSteamID.Nil; // 메뉴로 돌아간 뒤 들어갈 Steam 방
        private static string directAddress = string.Empty; // 41일차: 직접 IP 방 주소 (방장 내 IP:포트 / 참가자 들어간 주소)
        private const int MaxConnectionPayload = 256; // 42일차: 접속 데이터 최대 크기
        private const int MaxPasswordLength = 32; // 42일차: 방 암호 길이
        private const char PayloadSeparator = '\n'; // 버전과 암호 구분
        public const string ReasonClosed = "방장이 방을 닫았습니다"; // 42일차: 방장이 나감
        public const string ReasonKicked = "방장이 방에서 내보냈습니다"; // 42일차: 내보내기
        public const string ReasonBanned = "방장이 이 방에서 차단했습니다"; // 42일차: 차단
        public const string ReasonLocked = "방이 잠겨 있어 들어갈 수 없습니다"; // 42일차: 잠금
        public const string ReasonPassword = "방 암호가 맞지 않습니다"; // 42일차: 암호
        public const string ReasonHostLeft = "방장이 방을 떠났습니다"; // 42일차: Steam 로비 주인이 바뀜
        private static readonly HashSet<string> bannedKeys = new HashSet<string>(); // 42일차: 이번 방에서 차단한 대원 (steam:ID / ip:주소)
        private static readonly HashSet<string> knownKeys = new HashSet<string>(); // 42일차: 이번 방에 들어온 적 있는 대원 (잠가도 다시 연결 허용)
        private static readonly Dictionary<ulong, string> clientKeys = new Dictionary<ulong, string>(); // 42일차: 대원 번호 → 식별 키
        private static string hostPassword = string.Empty; // 42일차: 직접 IP 방 암호
        private static string joinPassword = string.Empty; // 42일차: 참가할 때 보낼 암호

        public static SessionMode Mode { get; private set; } = SessionMode.Offline; // 참여 방식
        public static bool IsOnline => Mode != SessionMode.Offline; // 협동 중
        public static bool IsHost => Mode == SessionMode.Host; // 방장
        public static bool IsGuest => Mode == SessionMode.Guest; // 참가자
        public static SessionTransport Transport { get; private set; } = SessionTransport.Direct; // 현재 연결 방식
        public static bool SteamReady => SteamService.EnsureInitialized(); // Steam 사용 가능
        public static string LocalPlayerName => SteamService.Initialized ? SteamService.PersonaName : string.Empty; // 내 표시 이름 (Steam 이 없으면 번호로 표시)
        public static string PendingMessage { get; private set; } = string.Empty; // 메뉴로 돌아왔을 때 보여 줄 안내
        public static RoomVisibility HostVisibility { get; private set; } = RoomVisibility.Public; // 41일차: 방 공개 범위
        public static bool RoomLocked { get; private set; } // 42일차: 새 입장 막기 (이미 들어왔던 대원의 다시 연결은 허용)
        public static bool HasPassword => !string.IsNullOrEmpty(hostPassword); // 42일차: 직접 IP 방 암호 사용
        public static event Action CrewChanged; // 42일차: 원정대원 목록·잠금 바뀜 (관리 창 갱신)

        public struct CrewEntry // 42일차: 관리 창 한 줄
        {
            public ulong ClientId; // 대원 번호
            public string Name; // 표시 이름
            public int PingMs; // 지연
            public bool IsHost; // 방장
            public bool IsSelf; // 나
        }

        public static List<CrewEntry> GetCrew() // 42일차: 현재 원정대원 목록
        {
            List<CrewEntry> result = new List<CrewEntry>(); // 결과

            if (!IsConnected) // 연결 전
            {
                return result; // 빈 목록
            }

            NetworkManager manager = instance.manager; // 관리자

            foreach (NetPlayerAvatar avatar in NetPlayerAvatar.All) // 몸체
            {
                if (avatar == null || !avatar.IsSpawned) // 정리 중
                {
                    continue; // 다음
                }

                ulong id = avatar.OwnerClientId; // 번호
                int ping = 0; // 지연

                if (manager.IsServer && id != NetworkManager.ServerClientId) // 방장이 본 참가자 지연
                {
                    ping = (int)manager.NetworkConfig.NetworkTransport.GetCurrentRtt(id); // 조회
                }
                else if (!manager.IsServer && id == NetworkManager.ServerClientId) // 참가자가 본 방장 지연
                {
                    ping = (int)manager.NetworkConfig.NetworkTransport.GetCurrentRtt(NetworkManager.ServerClientId); // 조회
                }

                result.Add(new CrewEntry { ClientId = id, Name = avatar.DisplayName, PingMs = ping, IsHost = id == NetworkManager.ServerClientId, IsSelf = id == manager.LocalClientId }); // 추가
            }

            result.Sort((a, b) => a.ClientId.CompareTo(b.ClientId)); // 번호 순
            return result; // 반환
        }

        public static void SetRoomLocked(bool locked) // 42일차: 방 잠그기 (방장)
        {
            if (!IsHost || RoomLocked == locked) // 방장 아님·같음
            {
                return; // 생략
            }

            RoomLocked = locked; // 기록
            DailySnapshotService daily = DailySnapshotService.Instance; // 저장
            SteamLobbyService.UpdateHostData(PlayerCount, daily == null ? 0 : daily.CurrentDay, false); // Steam 검색에서 숨김·표시
            NetWorldState.Announce(locked ? "방장이 방을 잠갔습니다 (새 입장 불가)" : "방장이 방 잠금을 풀었습니다"); // 모두에게
            CrewChanged?.Invoke(); // 관리 창
        }

        public static bool Kick(ulong clientId, bool ban, string reason = null) // 42일차: 대원 내보내기·차단 (방장)
        {
            if (!IsHost || instance == null || !instance.manager.IsServer || clientId == NetworkManager.ServerClientId || !instance.manager.ConnectedClients.ContainsKey(clientId)) // 불가
            {
                return false; // 실패
            }

            NetPlayerAvatar avatar = NetPlayerAvatar.Find(clientId); // 몸체
            string name = avatar == null ? $"원정대원 {clientId + 1}" : avatar.DisplayName; // 이름

            if (ban && clientKeys.TryGetValue(clientId, out string key)) // 차단
            {
                bannedKeys.Add(key); // 기록
            }

            string message = reason ?? (ban ? ReasonBanned : ReasonKicked); // 이유
            Debug.LogWarning($"[Project I] 협동 {(ban ? "차단" : "내보내기")} / {name} / {message}"); // 기록
            instance.manager.DisconnectClient(clientId, message); // 이유를 보낸 뒤 끊기
            NetWorldState.Announce($"{name} — {(ban ? "차단" : "내보냄")}"); // 모두에게
            CrewChanged?.Invoke(); // 관리 창
            return true; // 성공
        }

        public static string ShareCode // 41일차: 친구에게 알려 줄 값 (Steam 방 코드 K7Q-2MX 또는 직접 IP 주소:포트, 없으면 빈칸)
        {
            get
            {
                if (!IsOnline) // 혼자
                {
                    return string.Empty; // 없음
                }

                if (Transport == SessionTransport.Steam) // Steam 방
                {
                    return RoomCode.Format(SteamLobbyService.CurrentCode); // 로비 코드 (로비 생성 전이면 빈칸)
                }

                return directAddress; // 주소
            }
        }

        public static bool ShareIsCode => Transport == SessionTransport.Steam; // 코드인지 주소인지 (표시 문구용)
        public static event Action<string> StatusChanged; // 연결 상태 문구

        public static bool IsConnected => instance != null && instance.manager != null && instance.manager.IsListening && (instance.manager.IsServer || instance.manager.IsConnectedClient); // 연결 완료

        public static int PlayerCount // 현재 인원
        {
            get
            {
                if (!IsConnected) // 연결 전
                {
                    return IsOnline ? 1 : 0; // 자신
                }

                return instance.manager.IsServer ? instance.manager.ConnectedClientsIds.Count : Mathf.Max(1, NetPlayerAvatar.All.Count); // 인원
            }
        }

        public static string TakePendingMessage() // 안내 꺼내기
        {
            string message = PendingMessage; // 문구
            PendingMessage = string.Empty; // 비우기
            return message; // 반환
        }

        public static NetworkSession Ensure() // 네트워크 관리자 준비 (Resources 프리팹)
        {
            if (instance != null) // 이미 있음
            {
                return instance; // 반환
            }

            GameObject prefab = Resources.Load<GameObject>(PrefabResourcePath); // 프리팹

            if (prefab == null) // 없음
            {
                Debug.LogError("[Project I] 네트워크 프리팹이 없습니다 — Project I > Day 37 > Build Network Prefabs 를 실행하세요."); // 안내
                return null; // 실패
            }

            GameObject created = Instantiate(prefab); // 생성 (넷코드 관리자가 스스로 DontDestroyOnLoad)
            created.name = "===ProjectI Network==="; // 이름
            return created.GetComponent<NetworkSession>(); // 반환 (Awake 에서 instance 등록)
        }

        public static bool BeginHost(ushort port, out string error) // 방 열기 예약 (Steam 이 있으면 Steam 방, 없으면 직접 IP)
        {
            return BeginHost(port, RoomVisibility.Public, SteamReady, out error); // 방 열기
        }

        public static bool BeginHost(ushort port, RoomVisibility visibility, out string error) // 41일차: 공개 범위 지정 방 열기 (Steam 이 있으면 Steam 방)
        {
            return BeginHost(port, visibility, SteamReady, out error); // 방 열기
        }

        public static bool BeginHost(ushort port, RoomVisibility visibility, string password, out string error) // 42일차: 직접 IP 방 암호 지정 (Steam 방은 로비 멤버 확인으로 대신)
        {
            hostPassword = CleanPassword(password); // 암호
            bool started = BeginHost(port, visibility, SteamReady, out error); // 방 열기
            hostPassword = started ? hostPassword : string.Empty; // 실패하면 정리
            return started; // 결과
        }

        private static string CleanPassword(string password) // 암호 정리 (앞뒤 공백 제거 · 길이 제한)
        {
            string text = string.IsNullOrWhiteSpace(password) ? string.Empty : password.Trim(); // 정리
            return text.Length > MaxPasswordLength ? text.Substring(0, MaxPasswordLength) : text; // 길이
        }

        private static byte[] BuildPayload(string password) // 접속 데이터 (버전 + 암호)
        {
            return Encoding.UTF8.GetBytes(string.IsNullOrEmpty(password) ? ProtocolTag : ProtocolTag + PayloadSeparator + password); // 데이터
        }

        public static bool BeginHost(ushort port, RoomVisibility visibility, bool useSteam, out string error) // 방 열기 예약 (게임 월드가 준비되면 실제로 열림)
        {
            error = null; // 기본값
            NetworkSession session = Ensure(); // 준비

            if (session == null) // 실패
            {
                error = "네트워크 구성이 없습니다 (Day 37 메뉴 실행 필요)"; // 이유
                return false; // 실패
            }

            if (session.manager.IsListening) // 이미 연결 중
            {
                error = "이미 방에 연결되어 있습니다"; // 이유
                return false; // 실패
            }

            Mode = SessionMode.Host; // 방장
            Transport = useSteam && SteamReady ? SessionTransport.Steam : SessionTransport.Direct; // 연결 방식
            session.hostPort = port; // 포트
            HostVisibility = visibility; // 공개 범위
            ResetRoomSecurity(); // 42일차: 차단·잠금·기록 초기화
            directAddress = string.Empty; // 방이 열리면 기록
            session.transport.SetConnectionData("127.0.0.1", port, "0.0.0.0"); // 모든 주소에서 접속 받음
            session.pendingHost = true; // 월드 준비 후 열기
            Announce(Transport == SessionTransport.Steam ? "Steam 방을 여는 중..." : $"방을 여는 중 — 포트 {port}"); // 상태
            return true; // 성공
        }

        public static bool BeginJoin(string address, ushort port, out string error) // 방 참가 시작 (암호 없음)
        {
            return BeginJoin(address, port, string.Empty, out error); // 참가
        }

        public static bool BeginJoin(string address, ushort port, string password, out string error) // 방 참가 시작 (연결되면 게임 월드를 불러옴)
        {
            error = null; // 기본값
            NetworkSession session = Ensure(); // 준비

            if (session == null) // 실패
            {
                error = "네트워크 구성이 없습니다 (Day 37 메뉴 실행 필요)"; // 이유
                return false; // 실패
            }

            if (session.manager.IsListening) // 이미 연결 중
            {
                error = "이미 방에 연결되어 있습니다"; // 이유
                return false; // 실패
            }

            Mode = SessionMode.Guest; // 참가자
            Transport = SessionTransport.Direct; // 직접 연결
            session.reconnectAttempts = 0; // 초기화
            session.manager.NetworkConfig.NetworkTransport = session.transport; // 연결 방식
            string host = string.IsNullOrWhiteSpace(address) ? "127.0.0.1" : address.Trim(); // 주소
            directAddress = $"{host}:{port}"; // 다른 대원에게 알려 줄 주소
            session.transport.SetConnectionData(host, port); // 주소
            joinPassword = CleanPassword(password); // 42일차: 암호
            session.manager.NetworkConfig.ConnectionData = BuildPayload(joinPassword); // 버전·암호 확인 데이터

            if (!session.manager.StartClient()) // 시작 실패
            {
                Mode = SessionMode.Offline; // 되돌림
                error = "연결을 시작하지 못했습니다 (주소 형식 확인)"; // 이유
                return false; // 실패
            }

            Announce($"{address}:{port} 에 연결하는 중..."); // 상태
            return true; // 성공
        }

        public static bool BeginSteamJoin(CSteamID lobby, out string error) // Steam 방 참가 시작 (로비 → 방장 Steam ID → 연결)
        {
            error = null; // 기본값
            NetworkSession session = Ensure(); // 준비

            if (session == null || !SteamReady) // 준비 안 됨
            {
                error = session == null ? "네트워크 구성이 없습니다 (Day 37 메뉴 실행 필요)" : $"Steam 을 사용할 수 없습니다 — {SteamService.FailureReason}"; // 이유
                return false; // 실패
            }

            if (session.manager.IsListening || IsOnline) // 이미 연결 중
            {
                error = "이미 방에 연결되어 있습니다"; // 이유
                return false; // 실패
            }

            Mode = SessionMode.Guest; // 참가자
            Transport = SessionTransport.Steam; // Steam
            session.reconnectAttempts = 0; // 초기화
            joinPassword = string.Empty; // Steam 방은 암호 없음
            Announce("Steam 방에 들어가는 중..."); // 상태
            SteamLobbyService.Enter(lobby, (ok, host, message) => session.HandleLobbyEntered(ok, host, message)); // 로비
            return true; // 시작
        }

        public static bool BeginCodeJoin(string input, out string error) // 41일차: 방 코드로 참가 (코드 → Steam 로비 찾기 → 들어가기 → 연결)
        {
            error = null; // 기본값

            if (!RoomCode.TryParseCode(input, out string code)) // 형식
            {
                error = $"방 코드는 {RoomCode.Length}자리입니다 (예: K7Q-2MX · 숫자 0·1, 글자 O·I 는 쓰지 않음)"; // 이유
                return false; // 실패
            }

            NetworkSession session = Ensure(); // 준비

            if (session == null || !SteamReady) // 준비 안 됨
            {
                error = session == null ? "네트워크 구성이 없습니다 (Day 37 메뉴 실행 필요)" : $"방 코드는 Steam 이 필요합니다 — 주소로 참가하세요 ({SteamService.FailureReason})"; // 이유
                return false; // 실패
            }

            if (session.manager.IsListening || IsOnline) // 이미 연결 중
            {
                error = "이미 방에 연결되어 있습니다"; // 이유
                return false; // 실패
            }

            Mode = SessionMode.Guest; // 참가자 (찾는 동안 Esc 로 취소 가능)
            Transport = SessionTransport.Steam; // Steam
            session.reconnectAttempts = 0; // 초기화
            joinPassword = string.Empty; // Steam 방은 암호 없음
            Announce($"방 {RoomCode.Format(code)} 을 찾는 중..."); // 상태
            SteamLobbyService.FindByCode(code, (found, lobby, message) => session.HandleCodeFound(found, lobby, message)); // 찾기
            return true; // 시작
        }

        public static void QueueSteamJoin(CSteamID lobby) // 친구 초대·게임 참가 요청 (게임 중이면 메뉴로 돌아간 뒤 참가)
        {
            if (SteamLobbyService.CurrentLobby == lobby && IsOnline) // 이미 그 방
            {
                return; // 생략
            }

            if (IsOnline) // 다른 방
            {
                Leave(); // 나가기
            }

            if (SceneManager.GetActiveScene().name == SceneFlowManager.MainMenuSceneName) // 메뉴
            {
                if (!BeginSteamJoin(lobby, out string error)) // 바로 참가
                {
                    PendingMessage = error; // 안내
                    Announce(error); // 상태
                }

                return; // 종료
            }

            pendingSteamJoin = lobby; // 메뉴에서 참가
            DailySnapshotService service = DailySnapshotService.Instance; // 저장
            PersistentMapLoader loader = PersistentMapLoader.Instance; // 맵 로더

            if (service != null && loader != null && !loader.IsTransitioning && loader.CurrentDestination == TravelDestination.Office) // 사무소
            {
                service.SaveSafeOfficeCheckpoint(); // 저장
            }

            if (ProjectServices.TryGet(out SceneFlowManager flow)) // 씬 관리자
            {
                flow.LoadMainMenu(); // 메뉴로
            }
        }

        public static bool TakePendingSteamJoin(out CSteamID lobby) // 메뉴: 대기 중인 Steam 참가
        {
            lobby = pendingSteamJoin; // 방
            pendingSteamJoin = CSteamID.Nil; // 비우기
            return lobby.IsValid(); // 결과
        }

        public static void Leave() // 방 나가기 (방장이면 방 닫기)
        {
            if (instance == null) // 세션 없음
            {
                Mode = SessionMode.Offline; // 정리
                return; // 종료
            }

            instance.pendingHost = false; // 예약 취소
            instance.reconnecting = false; // 다시 연결 중단
            instance.StopAllCoroutines(); // 진행 중인 다시 연결 중단
            SteamLobbyService.Leave(); // Steam 방 나가기

            if (instance.manager != null && instance.manager.IsListening) // 연결 중
            {
                instance.leaving = true; // 스스로 나감

                if (instance.manager.IsServer && instance.manager.ConnectedClientsIds.Count > 1) // 42일차: 방장 — 참가자에게 이유를 보낸 뒤 종료
                {
                    foreach (ulong clientId in new List<ulong>(instance.manager.ConnectedClientsIds)) // 참가자
                    {
                        if (clientId != NetworkManager.ServerClientId) // 방장 제외
                        {
                            instance.manager.DisconnectClient(clientId, ReasonClosed); // 이유 전송 (프레임 끝에 끊김)
                        }
                    }

                    instance.StartCoroutine(instance.ShutdownAfterFarewell()); // 두 프레임 뒤 종료
                }
                else
                {
                    instance.manager.Shutdown(); // 종료 (생성된 네트워크 오브젝트 정리)
                }
            }

            Mode = SessionMode.Offline; // 혼자 하기
            RoomLocked = false; // 잠금 해제
            hostPassword = string.Empty; // 암호 정리
            Announce("연결을 종료했습니다"); // 상태
        }

        public static string CrewBlockReason() // 마차 출발 전 동료 탑승 확인 (모두 탔으면 null)
        {
            if (!IsOnline) // 혼자
            {
                return null; // 확인 불필요
            }

            int waiting = 0; // 안 탄 인원

            foreach (NetPlayerAvatar avatar in NetPlayerAvatar.All) // 원정대원
            {
                if (avatar != null && !avatar.IsLocalOwner && !avatar.IsDeadRemote && !avatar.IsAboard) // 살아 있는 다른 대원이 안 탐
                {
                    waiting++; // 집계
                }
            }

            return waiting == 0 ? null : $"모든 원정대원이 마차 창고에 타야 합니다 ({waiting}명 대기 중)"; // 결과
        }

        public static bool AllCrewDead() // 원정대 전원 사망 여부 (혼자면 true)
        {
            foreach (NetPlayerAvatar avatar in NetPlayerAvatar.All) // 원정대원
            {
                if (avatar != null && !avatar.IsLocalOwner && !avatar.IsDeadRemote) // 살아 있는 다른 대원
                {
                    return false; // 아직 생존자 있음
                }
            }

            return true; // 모두 쓰러짐 (자신의 상태는 호출한 쪽이 확인)
        }

        private IEnumerator ShutdownAfterFarewell() // 42일차: 방장 — 내보낸 이유가 전달될 시간을 준 뒤 종료
        {
            yield return null; // 이유 메시지·끊기 처리 (넷코드 프레임 끝)
            yield return null; // 전송
            leaving = true; // 스스로 나감

            if (manager.IsListening) // 아직 열림
            {
                manager.Shutdown(); // 종료
            }
        }

        private static void ResetRoomSecurity() // 42일차: 새 방 — 차단·잠금·기록 초기화
        {
            bannedKeys.Clear(); // 차단
            knownKeys.Clear(); // 기록
            clientKeys.Clear(); // 기록
            RoomLocked = false; // 잠금
            NetGuard.Reset(); // 요청 검사
        }

        private string ClientKey(ulong clientId) // 대원 식별 키 (Steam ID 또는 IP 주소)
        {
            if (manager.NetworkConfig.NetworkTransport == steamTransport) // Steam
            {
                return steamTransport.TryGetPeer(clientId, out CSteamID peer) ? $"steam:{peer.m_SteamID}" : null; // Steam ID
            }

            string endpoint = transport.GetEndpoint(clientId).Address; // "주소:포트"
            int colon = endpoint == null ? -1 : endpoint.LastIndexOf(':'); // 포트 구분
            return colon > 0 ? $"ip:{endpoint.Substring(0, colon)}" : null; // 주소
        }

        private string SteamAdmission(CSteamID peer) // 42일차: Steam 연결 들어오기 전 판정 (null 허용)
        {
            if (Mode != SessionMode.Host || !manager.IsServer) // 방장 아님
            {
                return "Not hosting"; // 거절
            }

            string key = $"steam:{peer.m_SteamID}"; // 키

            if (bannedKeys.Contains(key)) // 차단
            {
                return "Banned"; // 거절
            }

            if (RoomLocked && !knownKeys.Contains(key)) // 잠금 (처음 오는 대원)
            {
                return "Room locked"; // 거절
            }

            if (!SteamLobbyService.InLobby) // 로비 준비 전
            {
                return SteamTransportWait(); // 잠시 대기
            }

            return SteamLobbyService.IsMember(peer) ? null : SteamTransportWait(); // 로비 멤버만 (입장 반영 대기)
        }

        private string ServerReason() // 42일차: 방장이 보낸 끊김 이유 (넷코드가 만든 일반 끊김 문구는 제외, 없으면 null)
        {
            string reason = manager == null ? null : manager.DisconnectReason; // 이유
            return string.IsNullOrEmpty(reason) || reason.StartsWith("[Disconnect Event]") ? null : reason; // 결과
        }

        private static string SteamTransportWait() // 대기 판정
        {
            return SteamNetworkTransport.AdmissionWait; // 대기
        }

        private void Awake() // 등록
        {
            if (instance != null && instance != this) // 중복
            {
                Destroy(gameObject); // 제거
                return; // 종료
            }

            instance = this; // 등록
            manager = GetComponent<NetworkManager>(); // 관리자
            transport = GetComponent<UnityTransport>(); // 연결 방식
            steamTransport = GetComponent<SteamNetworkTransport>(); // Steam 연결 모듈
            steamTransport = steamTransport != null ? steamTransport : gameObject.AddComponent<SteamNetworkTransport>(); // 없으면 추가
            steamTransport.Admission = SteamAdmission; // 42일차: 로비 멤버·차단·잠금 확인
            manager.NetworkConfig.NetworkTransport = transport; // 연결 방식 지정
            manager.NetworkConfig.ConnectionApproval = true; // 접속 확인 사용
            manager.NetworkConfig.EnableSceneManagement = false; // 씬 교체는 Project I 맵 로더가 직접 맞춤
            manager.ConnectionApprovalCallback = ApproveConnection; // 접속 확인
            manager.OnClientConnectedCallback += HandleClientConnected; // 접속
            manager.OnClientDisconnectCallback += HandleClientDisconnected; // 끊김
            manager.OnClientStopped += HandleClientStopped; // 참가자 종료
            manager.OnTransportFailure += HandleTransportFailure; // 연결 실패
        }

        private void OnDestroy() // 해제
        {
            if (manager != null) // 관리자
            {
                manager.OnClientConnectedCallback -= HandleClientConnected; // 해제
                manager.OnClientDisconnectCallback -= HandleClientDisconnected; // 해제
                manager.OnClientStopped -= HandleClientStopped; // 해제
                manager.OnTransportFailure -= HandleTransportFailure; // 해제
            }

            if (instance == this) // 현재 세션
            {
                instance = null; // 정리
                Mode = SessionMode.Offline; // 혼자 하기
            }
        }

        private void Update() // 방 열기 예약 · Steam 로비 정보 갱신
        {
            if (Mode == SessionMode.Host) // 42일차: 거절 요약 기록
            {
                NetGuard.Tick(); // 요약
            }

            if (Mode == SessionMode.Host && Transport == SessionTransport.Steam && SteamLobbyService.InLobby && Time.unscaledTime >= nextLobbyUpdate) // Steam 방장
            {
                nextLobbyUpdate = Time.unscaledTime + 5f; // 5초마다
                DailySnapshotService daily = DailySnapshotService.Instance; // 저장
                SteamLobbyService.UpdateHostData(PlayerCount, daily == null ? 0 : daily.CurrentDay, false); // 인원·일차
            }

            if (!pendingHost) // 예약 없음
            {
                return; // 종료
            }

            PersistentMapLoader loader = PersistentMapLoader.Instance; // 맵 로더
            DailySnapshotService service = DailySnapshotService.Instance; // 저장

            if (loader == null || loader.IsTransitioning || service == null || !service.IsInitialized || service.IsRestoreInProgress) // 월드 준비 전
            {
                return; // 대기
            }

            pendingHost = false; // 예약 소비
            StartHostNow(); // 방 열기
        }

        private void StartHostNow() // 실제 방 열기
        {
            manager.NetworkConfig.ConnectionData = BuildPayload(hostPassword); // 방장 자신의 접속 확인 데이터
            manager.NetworkConfig.NetworkTransport = Transport == SessionTransport.Steam ? steamTransport : transport; // 연결 방식

            if (Transport == SessionTransport.Steam) // 42일차: Steam 방은 암호 대신 로비 멤버 확인
            {
                hostPassword = string.Empty; // 사용 안 함
                manager.NetworkConfig.ConnectionData = BuildPayload(string.Empty); // 버전만
            }

            if (!manager.StartHost()) // 실패
            {
                Mode = SessionMode.Offline; // 혼자 하기로
                Announce(Transport == SessionTransport.Steam ? "Steam 방을 열지 못했습니다" : $"방을 열지 못했습니다 — 포트 {hostPort} 가 사용 중인지 확인하세요"); // 안내
                GameHud.ShowNotice("방을 열지 못해 혼자 하기로 진행합니다", 4f); // 화면 안내
                return; // 종료
            }

            GameObject prefab = Resources.Load<GameObject>(WorldStateResourcePath); // 월드 상태 프리팹

            if (prefab != null) // 있음
            {
                NetworkObject.InstantiateAndSpawn(prefab, manager); // 방장 소유로 생성
            }
            else
            {
                Debug.LogError("[Project I] NetWorldState 프리팹이 없습니다 — Day 37 메뉴를 실행하세요."); // 오류
            }

            if (Transport == SessionTransport.Steam) // Steam 방
            {
                string code = RoomCode.Generate(); // 방 코드
                SteamLobbyService.Create(null, MaxPlayers, HostVisibility, code, (ok, message) => // 로비 만들기
                {
                    Announce(message); // 상태
                    string scope = HostVisibility == RoomVisibility.Public ? "공개" : "코드 전용"; // 공개 범위
                    GameHud.ShowNotice(ok ? $"{scope} 방 코드 {RoomCode.Format(code)} — Esc 창에서 복사·초대" : $"{message} (연결은 유지)", 6f); // 안내
                });
                return; // 종료
            }

            directAddress = $"{RoomCode.LocalIPv4()}:{hostPort}"; // 같은 네트워크 주소
            Announce($"방 열림 — {directAddress}"); // 상태
            GameHud.ShowNotice($"방을 열었습니다 — 주소 {directAddress}{(HasPassword ? " · 암호 사용" : string.Empty)} (Esc 창에서 복사)", 6f); // 화면 안내
        }

        private void HandleCodeFound(bool found, CSteamID lobby, string message) // 코드로 로비를 찾음 → 들어가기
        {
            if (Mode != SessionMode.Guest || Transport != SessionTransport.Steam) // 그사이 취소
            {
                return; // 종료
            }

            if (!found) // 없음
            {
                Mode = SessionMode.Offline; // 되돌림 (메뉴가 버튼을 다시 허용)
                Announce(message); // 이유
                return; // 종료
            }

            Announce(message); // 상태
            SteamLobbyService.Enter(lobby, HandleLobbyEntered); // 로비
        }

        private void HandleLobbyEntered(bool ok, CSteamID host, string message) // Steam 로비에 들어감 → 방장에게 연결
        {
            Announce(message); // 상태

            if (!ok || Mode != SessionMode.Guest) // 실패·취소
            {
                if (Mode == SessionMode.Guest) // 참가 중이었음
                {
                    Mode = SessionMode.Offline; // 되돌림
                    SteamLobbyService.Leave(); // 정리
                    Announce(message); // 메뉴가 버튼을 다시 허용 (Offline 상태로)
                }
                else if (ok) // 기다리는 사이 취소됨
                {
                    SteamLobbyService.Leave(); // 들어간 로비 정리
                }

                return; // 종료
            }

            steamTransport.HostId = host; // 방장
            manager.NetworkConfig.NetworkTransport = steamTransport; // Steam 연결
            manager.NetworkConfig.ConnectionData = BuildPayload(string.Empty); // 버전 확인 데이터

            if (!manager.StartClient()) // 시작 실패
            {
                Mode = SessionMode.Offline; // 되돌림
                SteamLobbyService.Leave(); // 정리
                Announce("방장에게 연결하지 못했습니다"); // 안내
            }
        }

        private void ApproveConnection(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response) // 접속 확인
        {
            response.Pending = false; // 즉시 판정

            if (request.ClientNetworkId == NetworkManager.ServerClientId) // 방장 자신
            {
                response.Approved = true; // 허용
                response.CreatePlayerObject = true; // 몸체
                return; // 종료
            }

            if (request.Payload == null || request.Payload.Length > MaxConnectionPayload) // 42일차: 비정상 데이터
            {
                response.Approved = false; // 거부
                response.Reason = "접속 데이터가 올바르지 않습니다"; // 이유
                return; // 종료
            }

            string payload = Encoding.UTF8.GetString(request.Payload); // 버전 [+ 암호]
            int separator = payload.IndexOf(PayloadSeparator); // 구분
            string tag = separator < 0 ? payload : payload.Substring(0, separator); // 버전
            string password = separator < 0 ? string.Empty : payload.Substring(separator + 1); // 암호

            if (tag != ProtocolTag) // 버전 다름
            {
                response.Approved = false; // 거부
                response.Reason = $"게임 버전이 다릅니다 (방장 {ProtocolTag})"; // 이유
                return; // 종료
            }

            string key = ClientKey(request.ClientNetworkId); // 42일차: 대원 식별 (Steam ID·IP)

            if (key == null || bannedKeys.Contains(key)) // 식별 불가·차단
            {
                response.Approved = false; // 거부
                response.Reason = key == null ? "접속자를 확인하지 못했습니다" : ReasonBanned; // 이유
                return; // 종료
            }

            if (RoomLocked && !knownKeys.Contains(key)) // 잠금
            {
                response.Approved = false; // 거부
                response.Reason = ReasonLocked; // 이유
                return; // 종료
            }

            if (Transport == SessionTransport.Steam && !SteamLobbyService.IsMember(new CSteamID(ulong.Parse(key.Substring(6))))) // Steam 로비 멤버가 아님
            {
                response.Approved = false; // 거부
                response.Reason = "Steam 방을 거쳐 들어와야 합니다"; // 이유
                return; // 종료
            }

            if (Transport == SessionTransport.Direct && HasPassword && password != hostPassword) // 암호 틀림
            {
                response.Approved = false; // 거부
                response.Reason = ReasonPassword; // 이유
                Debug.LogWarning($"[Project I] 협동 암호 불일치 / {key}"); // 기록
                return; // 종료
            }

            if (request.ClientNetworkId != NetworkManager.ServerClientId && manager.ConnectedClientsIds.Count >= MaxPlayers) // 가득 참
            {
                response.Approved = false; // 거부
                response.Reason = $"방이 가득 찼습니다 ({MaxPlayers}명)"; // 이유
                return; // 종료
            }

            response.Approved = true; // 허용
            response.CreatePlayerObject = true; // 원정대원 표시 오브젝트 생성
            clientKeys[request.ClientNetworkId] = key; // 기록
            knownKeys.Add(key); // 잠가도 다시 연결 허용
        }

        private void HandleClientConnected(ulong clientId) // 접속
        {
            if (Mode == SessionMode.Guest && clientId == manager.LocalClientId) // 내가 참가 완료
            {
                reconnectAttempts = 0; // 초기화

                if (SceneManager.GetActiveScene().name != SceneFlowManager.MainMenuSceneName && PersistentMapLoader.Instance != null) // 게임 중 다시 연결됨
                {
                    GameHud.ShowNotice("다시 연결됨 — 방장 상태로 맞추는 중", 3f); // 안내
                    return; // 월드 유지 (아이템·전투 상태는 전체 목록으로 다시 맞춤)
                }

                Announce("연결됨 — 원정 사무소로 이동합니다"); // 상태

                if (ProjectServices.TryGet(out SceneFlowManager flow)) // 씬 관리자
                {
                    flow.LoadExplorationOffice(); // 게임 월드 불러오기 (저장은 읽지 않음)
                }

                return; // 종료
            }

            if (Mode == SessionMode.Host && clientId != manager.LocalClientId) // 다른 대원 참가
            {
                GameHud.ShowNotice($"원정대원 참가 ({manager.ConnectedClientsIds.Count}/{MaxPlayers})", 3f); // 안내 (이름은 몸체 이름표)
            }

            CrewChanged?.Invoke(); // 42일차: 관리 창
        }

        private void HandleClientDisconnected(ulong clientId) // 끊김
        {
            if (Mode == SessionMode.Host) // 방장
            {
                if (clientId != manager.LocalClientId) // 다른 대원
                {
                    GameHud.ShowNotice($"원정대원 한 명이 나갔습니다 ({Mathf.Max(1, manager.ConnectedClientsIds.Count - 1)}/{MaxPlayers})", 3f); // 안내
                    NetGuard.Forget(clientId); // 42일차: 요청 기록 정리
                    NetItemSync.ForgetClient(clientId); // 무기·열쇠 기록 정리
                    clientKeys.Remove(clientId); // 식별 정리
                }

                CrewChanged?.Invoke(); // 관리 창
                return; // 종료
            }

            if (Mode == SessionMode.Guest && (clientId == manager.LocalClientId || clientId == NetworkManager.ServerClientId)) // 내 연결 끊김
            {
                LostConnection(ServerReason() ?? "방장과의 연결이 끊겼습니다"); // 처리 (42일차: 방장이 보낸 이유만 표시)
            }
        }

        private void HandleClientStopped(bool wasHost) // 참가자 종료
        {
            if (Mode == SessionMode.Guest) // 참가 중이었음
            {
                LostConnection(ServerReason() ?? "방에 연결하지 못했습니다"); // 처리
            }
        }

        private void HandleTransportFailure() // 연결 실패
        {
            if (Mode != SessionMode.Offline) // 연결 중이었음
            {
                LostConnection("네트워크 오류로 연결이 끊겼습니다"); // 처리
            }
        }

        private void LostConnection(string reason) // 연결을 잃음 → (게임 중이면 다시 연결) → 메인 메뉴
        {
            bool selfLeave = leaving; // 스스로 나감
            leaving = false; // 초기화

            if (reconnecting) // 이미 다시 연결 처리 중
            {
                return; // 중복 무시
            }

            bool inGame = SceneManager.GetActiveScene().name != SceneFlowManager.MainMenuSceneName && PersistentMapLoader.Instance != null; // 게임 중
            bool hostDecided = ServerReason() != null; // 42일차: 방장이 이유를 보냄 (닫음·내보냄·차단·잠금·암호·버전) → 다시 연결하지 않음
            bool hostLeft = Transport == SessionTransport.Steam && SteamLobbyService.HostLeft(steamTransport.HostId); // 42일차: 방장이 Steam 로비를 떠남

            if (hostLeft && !hostDecided) // 이유 없이 사라진 방장
            {
                reason = ReasonHostLeft; // 안내
            }

            bool canRetry = !selfLeave && !hostDecided && !hostLeft && Mode == SessionMode.Guest && inGame && reconnectAttempts < MaxReconnectAttempts && (Transport == SessionTransport.Direct || SteamLobbyService.InLobby); // 다시 연결 가능

            if (canRetry) // 다시 연결
            {
                reconnectAttempts++; // 횟수
                StartCoroutine(ReconnectRoutine(reason)); // 시도
                return; // 종료
            }

            Mode = SessionMode.Offline; // 혼자 하기
            pendingHost = false; // 예약 취소
            SteamLobbyService.Leave(); // Steam 방 정리

            if (selfLeave) // 스스로 나간 경우
            {
                return; // 안내 불필요
            }

            PendingMessage = reason; // 메뉴에서 보여 줄 안내
            Announce(reason); // 상태
            Debug.LogWarning($"[Project I] 협동 연결 종료 / {reason}"); // 기록

            if (manager.IsListening) // 아직 정리 안 됨
            {
                manager.Shutdown(); // 정리
            }

            if (SceneManager.GetActiveScene().name != SceneFlowManager.MainMenuSceneName && ProjectServices.TryGet(out SceneFlowManager flow)) // 게임 중
            {
                flow.LoadMainMenu(); // 메인 메뉴로
            }
        }

        private IEnumerator ReconnectRoutine(string reason) // 참가자: 잠깐 끊긴 연결 다시 잇기
        {
            reconnecting = true; // 처리 중
            Debug.LogWarning($"[Project I] 협동 연결 끊김 / {reason} / 다시 연결 {reconnectAttempts}/{MaxReconnectAttempts}"); // 기록
            GameHud.ShowNotice($"연결 끊김 — 다시 연결 중 ({reconnectAttempts}/{MaxReconnectAttempts})", 4f); // 안내

            if (manager.IsListening) // 정리
            {
                manager.Shutdown(); // 종료
            }

            float waitUntil = Time.unscaledTime + 4f; // 최대 대기

            while (manager.ShutdownInProgress && Time.unscaledTime < waitUntil) // 종료 대기
            {
                yield return null; // 다음 프레임
            }

            yield return new WaitForSecondsRealtime(2f); // 잠시 뒤 시도
            reconnecting = false; // 다음 끊김은 다시 판단

            if (Mode != SessionMode.Guest) // 그사이 나감
            {
                yield break; // 종료
            }

            manager.NetworkConfig.NetworkTransport = Transport == SessionTransport.Steam ? steamTransport : transport; // 같은 연결 방식
            manager.NetworkConfig.ConnectionData = BuildPayload(Transport == SessionTransport.Steam ? string.Empty : joinPassword); // 버전·암호 확인 데이터

            if (!manager.StartClient()) // 시작 실패
            {
                LostConnection(reason); // 다음 판단 (횟수 초과면 메뉴로)
            }
        }

        private static void Announce(string text) // 상태 문구 알림
        {
            StatusChanged?.Invoke(text); // 알림
        }
    }
}
