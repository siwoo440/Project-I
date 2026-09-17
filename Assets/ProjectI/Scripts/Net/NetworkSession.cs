using System; // 이벤트
using System.Text; // 접속 데이터
using ProjectI.Core; // 씬 이동
using ProjectI.Loop; // 맵 로더 준비 확인
using ProjectI.Persistence; // 저장 준비 확인
using ProjectI.UI; // 알림
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

    [DisallowMultipleComponent] // 하나만
    [RequireComponent(typeof(NetworkManager))] // 넷코드 관리자
    [RequireComponent(typeof(UnityTransport))] // 직접 IP 연결
    public sealed class NetworkSession : MonoBehaviour // 방 열기·참가·나가기·연결 끊김 처리 (37일차: 직접 IP, 40일차에 Steam 로비로 교체)
    {
        public const string PrefabResourcePath = "Net/ProjectINetwork"; // 네트워크 관리자 프리팹
        public const string WorldStateResourcePath = "Net/NetWorldState"; // 월드 상태 프리팹
        public const int MaxPlayers = 4; // 최대 인원
        public const ushort DefaultPort = 7777; // 기본 포트
        public const string ProtocolTag = "ProjectI-0.37"; // 접속 확인용 버전 (다르면 거부)
        private static NetworkSession instance; // 현재 세션
        private NetworkManager manager; // 넷코드 관리자
        private UnityTransport transport; // 연결 방식
        private bool pendingHost; // 게임 월드 준비 후 방 열기 대기
        private bool leaving; // 스스로 나가는 중
        private ushort hostPort; // 방 포트

        public static SessionMode Mode { get; private set; } = SessionMode.Offline; // 참여 방식
        public static bool IsOnline => Mode != SessionMode.Offline; // 협동 중
        public static bool IsHost => Mode == SessionMode.Host; // 방장
        public static bool IsGuest => Mode == SessionMode.Guest; // 참가자
        public static string PendingMessage { get; private set; } = string.Empty; // 메뉴로 돌아왔을 때 보여 줄 안내
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

        public static bool BeginHost(ushort port, out string error) // 방 열기 예약 (게임 월드가 준비되면 실제로 열림)
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
            session.hostPort = port; // 포트
            session.transport.SetConnectionData("127.0.0.1", port, "0.0.0.0"); // 모든 주소에서 접속 받음
            session.pendingHost = true; // 월드 준비 후 열기
            Announce($"방을 여는 중 — 포트 {port}"); // 상태
            return true; // 성공
        }

        public static bool BeginJoin(string address, ushort port, out string error) // 방 참가 시작 (연결되면 게임 월드를 불러옴)
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
            session.transport.SetConnectionData(string.IsNullOrWhiteSpace(address) ? "127.0.0.1" : address.Trim(), port); // 주소
            session.manager.NetworkConfig.ConnectionData = Encoding.UTF8.GetBytes(ProtocolTag); // 버전 확인 데이터

            if (!session.manager.StartClient()) // 시작 실패
            {
                Mode = SessionMode.Offline; // 되돌림
                error = "연결을 시작하지 못했습니다 (주소 형식 확인)"; // 이유
                return false; // 실패
            }

            Announce($"{address}:{port} 에 연결하는 중..."); // 상태
            return true; // 성공
        }

        public static void Leave() // 방 나가기 (방장이면 방 닫기)
        {
            if (instance == null) // 세션 없음
            {
                Mode = SessionMode.Offline; // 정리
                return; // 종료
            }

            instance.pendingHost = false; // 예약 취소

            if (instance.manager != null && instance.manager.IsListening) // 연결 중
            {
                instance.leaving = true; // 스스로 나감
                instance.manager.Shutdown(); // 종료 (생성된 네트워크 오브젝트 정리)
            }

            Mode = SessionMode.Offline; // 혼자 하기
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

        private void Update() // 방 열기 예약 처리
        {
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
            manager.NetworkConfig.ConnectionData = Encoding.UTF8.GetBytes(ProtocolTag); // 방장 자신의 접속 확인 데이터

            if (!manager.StartHost()) // 실패
            {
                Mode = SessionMode.Offline; // 혼자 하기로
                Announce($"방을 열지 못했습니다 — 포트 {hostPort} 가 사용 중인지 확인하세요"); // 안내
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

            Announce($"방 열림 — 포트 {hostPort}"); // 상태
            GameHud.ShowNotice($"방을 열었습니다 — 포트 {hostPort}", 4f); // 화면 안내
        }

        private void ApproveConnection(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response) // 접속 확인
        {
            string tag = request.Payload == null ? string.Empty : Encoding.UTF8.GetString(request.Payload); // 버전

            if (tag != ProtocolTag) // 버전 다름
            {
                response.Approved = false; // 거부
                response.Reason = $"게임 버전이 다릅니다 (방장 {ProtocolTag})"; // 이유
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
            response.Pending = false; // 즉시
        }

        private void HandleClientConnected(ulong clientId) // 접속
        {
            if (Mode == SessionMode.Guest && clientId == manager.LocalClientId) // 내가 참가 완료
            {
                Announce("연결됨 — 원정 사무소로 이동합니다"); // 상태

                if (ProjectServices.TryGet(out SceneFlowManager flow)) // 씬 관리자
                {
                    flow.LoadExplorationOffice(); // 게임 월드 불러오기 (저장은 읽지 않음)
                }

                return; // 종료
            }

            if (Mode == SessionMode.Host && clientId != manager.LocalClientId) // 다른 대원 참가
            {
                GameHud.ShowNotice($"원정대원 {clientId + 1} 참가 ({manager.ConnectedClientsIds.Count}/{MaxPlayers})", 3f); // 안내
            }
        }

        private void HandleClientDisconnected(ulong clientId) // 끊김
        {
            if (Mode == SessionMode.Host) // 방장
            {
                if (clientId != manager.LocalClientId) // 다른 대원
                {
                    GameHud.ShowNotice($"원정대원 {clientId + 1} 나감", 3f); // 안내
                }

                return; // 종료
            }

            if (Mode == SessionMode.Guest && (clientId == manager.LocalClientId || clientId == NetworkManager.ServerClientId)) // 내 연결 끊김
            {
                LostConnection(string.IsNullOrEmpty(manager.DisconnectReason) ? "방장과의 연결이 끊겼습니다" : manager.DisconnectReason); // 처리
            }
        }

        private void HandleClientStopped(bool wasHost) // 참가자 종료
        {
            if (Mode == SessionMode.Guest) // 참가 중이었음
            {
                LostConnection(string.IsNullOrEmpty(manager.DisconnectReason) ? "방에 연결하지 못했습니다" : manager.DisconnectReason); // 처리
            }
        }

        private void HandleTransportFailure() // 연결 실패
        {
            if (Mode != SessionMode.Offline) // 연결 중이었음
            {
                LostConnection("네트워크 오류로 연결이 끊겼습니다"); // 처리
            }
        }

        private void LostConnection(string reason) // 연결을 잃음 → 메인 메뉴
        {
            bool selfLeave = leaving; // 스스로 나감
            leaving = false; // 초기화
            Mode = SessionMode.Offline; // 혼자 하기
            pendingHost = false; // 예약 취소

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

        private static void Announce(string text) // 상태 문구 알림
        {
            StatusChanged?.Invoke(text); // 알림
        }
    }
}
