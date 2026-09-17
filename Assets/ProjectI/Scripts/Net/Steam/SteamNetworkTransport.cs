using System; // 배열
using System.Collections.Generic; // 연결 목록
using System.Runtime.InteropServices; // 네이티브 메모리
using Steamworks; // Steamworks.NET
using Unity.Netcode; // 넷코드
using UnityEngine; // 유니티 기본 기능 참조

namespace ProjectI.Net.Steam // Steam 연결 네임스페이스
{
    public sealed class SteamNetworkTransport : NetworkTransport // 40일차: Steam 중계망(SteamNetworkingSockets P2P) 넷코드 연결 모듈 — 포트 개방 없이 Steam ID 로 연결
    {
        private const int MaxMessagesPerPoll = 64; // 한 번에 받는 메시지 수
        private const ulong ServerId = 0; // 넷코드가 보는 방장 번호
        public const string AdmissionWait = "wait"; // 42일차: 로비 입장 확인을 기다림
        private const float AdmissionTimeout = 6f; // 로비 멤버로 보일 때까지 기다리는 시간
        private const int MaxMessagesPerSecond = 3000; // 연결당 초당 메시지 한도 (넘으면 끊음)
        private const int MaxMessageBytes = 512 * 1024; // 메시지 하나 최대 크기

        private readonly Dictionary<ulong, HSteamNetConnection> connections = new Dictionary<ulong, HSteamNetConnection>(); // 번호 → 연결
        private readonly Dictionary<HSteamNetConnection, ulong> ids = new Dictionary<HSteamNetConnection, ulong>(); // 연결 → 번호
        private readonly Queue<(NetworkEvent kind, ulong id)> events = new Queue<(NetworkEvent kind, ulong id)>(); // 연결·끊김 알림
        private readonly IntPtr[] messageBuffer = new IntPtr[MaxMessagesPerPoll]; // 수신 버퍼
        private readonly Queue<(ulong id, byte[] data)> incoming = new Queue<(ulong id, byte[] data)>(); // 받은 데이터
        private Callback<SteamNetConnectionStatusChangedCallback_t> statusCallback; // 연결 상태 콜백
        private HSteamListenSocket listenSocket = HSteamListenSocket.Invalid; // 방장 수신 소켓
        private HSteamNetConnection serverConnection = HSteamNetConnection.Invalid; // 참가자 → 방장 연결
        private bool isServer; // 방장 여부
        private ulong nextClientId = 1; // 다음 대원 번호
        private NetworkManager owner; // 넷코드 관리자 (번호 변환)
        private readonly Dictionary<HSteamNetConnection, CSteamID> peers = new Dictionary<HSteamNetConnection, CSteamID>(); // 42일차: 연결 → 상대 Steam ID
        private readonly List<(HSteamNetConnection connection, CSteamID peer, float deadline)> pendingAccept = new List<(HSteamNetConnection, CSteamID, float)>(); // 로비 입장 확인 대기
        private readonly Dictionary<HSteamNetConnection, TrafficWindow> traffic = new Dictionary<HSteamNetConnection, TrafficWindow>(); // 연결별 초당 메시지 수
        private readonly List<KeyValuePair<HSteamNetConnection, ulong>> receiveList = new List<KeyValuePair<HSteamNetConnection, ulong>>(); // 받기 순회용 복사본 (받는 중 끊을 수 있음)

        private struct TrafficWindow // 1초 단위 메시지 수
        {
            public int Count; // 수
            public float Start; // 시작 시각
        }

        public Func<CSteamID, string> Admission { get; set; } // 42일차: 들어오는 연결 판정 (null 허용 · AdmissionWait 대기 · 그 외 거절 이유)

        public CSteamID HostId { get; set; } = CSteamID.Nil; // 참가할 방장 Steam ID
        public override ulong ServerClientId => ServerId; // 방장 번호
        public override bool IsSupported => Application.platform == RuntimePlatform.WindowsPlayer || Application.platform == RuntimePlatform.WindowsEditor || Application.platform == RuntimePlatform.OSXPlayer || Application.platform == RuntimePlatform.OSXEditor || Application.platform == RuntimePlatform.LinuxPlayer || Application.platform == RuntimePlatform.LinuxEditor; // PC 만

        public override void Initialize(NetworkManager networkManager = null) // 준비
        {
            SteamService.EnsureInitialized(); // Steam
            owner = networkManager; // 번호 변환용
            statusCallback ??= Callback<SteamNetConnectionStatusChangedCallback_t>.Create(OnConnectionStatusChanged); // 콜백 등록
        }

        public override bool StartServer() // 방장 시작
        {
            if (!SteamService.EnsureInitialized()) // Steam 없음
            {
                return false; // 실패
            }

            isServer = true; // 방장
            nextClientId = 1; // 번호 초기화
            listenSocket = SteamNetworkingSockets.CreateListenSocketP2P(0, 0, null); // 수신 소켓
            return listenSocket != HSteamListenSocket.Invalid; // 결과
        }

        public override bool StartClient() // 참가 시작
        {
            if (!SteamService.EnsureInitialized() || !HostId.IsValid()) // Steam·방장 없음
            {
                return false; // 실패
            }

            isServer = false; // 참가자
            SteamNetworkingIdentity identity = new SteamNetworkingIdentity(); // 방장 식별
            identity.SetSteamID(HostId); // Steam ID
            serverConnection = SteamNetworkingSockets.ConnectP2P(ref identity, 0, 0, null); // 연결
            return serverConnection != HSteamNetConnection.Invalid; // 결과
        }

        public override void Send(ulong clientId, ArraySegment<byte> payload, NetworkDelivery networkDelivery) // 보내기
        {
            HSteamNetConnection connection = isServer ? Lookup(clientId) : serverConnection; // 대상

            if (connection == HSteamNetConnection.Invalid || payload.Count <= 0) // 없음
            {
                return; // 생략
            }

            int flags = networkDelivery == NetworkDelivery.Unreliable || networkDelivery == NetworkDelivery.UnreliableSequenced
                ? Constants.k_nSteamNetworkingSend_Unreliable | Constants.k_nSteamNetworkingSend_NoNagle
                : Constants.k_nSteamNetworkingSend_Reliable | Constants.k_nSteamNetworkingSend_NoNagle; // 전달 방식

            GCHandle handle = GCHandle.Alloc(payload.Array, GCHandleType.Pinned); // 고정

            try
            {
                IntPtr pointer = IntPtr.Add(handle.AddrOfPinnedObject(), payload.Offset); // 시작 위치
                SteamNetworkingSockets.SendMessageToConnection(connection, pointer, (uint)payload.Count, flags, out long _); // 전송
            }
            finally
            {
                handle.Free(); // 해제
            }
        }

        public override NetworkEvent PollEvent(out ulong clientId, out ArraySegment<byte> payload, out float receiveTime) // 받기
        {
            receiveTime = Time.realtimeSinceStartup; // 시각
            payload = default; // 기본

            if (events.Count > 0) // 연결·끊김 먼저
            {
                (NetworkEvent kind, ulong id) next = events.Dequeue(); // 알림
                clientId = next.id; // 번호
                return next.kind; // 종류
            }

            if (incoming.Count == 0) // 받은 데이터 없음
            {
                ReceiveAll(); // Steam 에서 받기
            }

            if (incoming.Count > 0) // 데이터
            {
                (ulong id, byte[] data) message = incoming.Dequeue(); // 꺼내기
                clientId = message.id; // 보낸 쪽
                payload = new ArraySegment<byte>(message.data); // 내용
                return NetworkEvent.Data; // 데이터
            }

            clientId = 0; // 없음
            return NetworkEvent.Nothing; // 없음
        }

        public override void DisconnectRemoteClient(ulong clientId) // 방장: 대원 끊기
        {
            HSteamNetConnection connection = Lookup(clientId); // 연결

            if (connection != HSteamNetConnection.Invalid) // 있음
            {
                SteamNetworkingSockets.CloseConnection(connection, 0, "Disconnected by host", true); // 닫기 (남은 신뢰 메시지 — 내보낸 이유 — 를 보낸 뒤)
                Forget(connection); // 정리
            }
        }

        public override void DisconnectLocalClient() // 참가자: 나가기
        {
            if (serverConnection != HSteamNetConnection.Invalid) // 연결 중
            {
                SteamNetworkingSockets.CloseConnection(serverConnection, 0, "Left", false); // 닫기
                serverConnection = HSteamNetConnection.Invalid; // 정리
            }
        }

        public override ulong GetCurrentRtt(ulong clientId) // 지연 시간
        {
            ulong transportId = clientId; // 번호

            if (isServer && owner != null) // 넷코드 번호 → 연결 번호
            {
                ulong mapped = owner.GetTransportIdFromClientId(clientId); // 변환
                transportId = mapped == ulong.MaxValue ? clientId : mapped; // 실패하면 그대로
            }

            HSteamNetConnection connection = isServer ? Lookup(transportId) : serverConnection; // 연결

            if (connection == HSteamNetConnection.Invalid) // 없음
            {
                return 0; // 모름
            }

            SteamNetConnectionRealTimeStatus_t status = default; // 상태
            SteamNetConnectionRealTimeLaneStatus_t lane = default; // 레인
            SteamNetworkingSockets.GetConnectionRealTimeStatus(connection, ref status, 0, ref lane); // 조회
            return (ulong)Mathf.Max(0, status.m_nPing); // 지연
        }

        public override void Shutdown() // 종료
        {
            foreach (HSteamNetConnection connection in ids.Keys) // 대원 연결
            {
                SteamNetworkingSockets.CloseConnection(connection, 0, "Shutdown", false); // 닫기
            }

            foreach ((HSteamNetConnection connection, CSteamID peer, float deadline) pending in pendingAccept) // 확인 대기 연결
            {
                SteamNetworkingSockets.CloseConnection(pending.connection, 0, "Shutdown", false); // 닫기
            }

            ids.Clear(); // 정리
            connections.Clear(); // 정리
            peers.Clear(); // 정리
            pendingAccept.Clear(); // 정리
            traffic.Clear(); // 정리
            DisconnectLocalClient(); // 참가 연결

            if (listenSocket != HSteamListenSocket.Invalid) // 수신 소켓
            {
                SteamNetworkingSockets.CloseListenSocket(listenSocket); // 닫기
                listenSocket = HSteamListenSocket.Invalid; // 정리
            }

            events.Clear(); // 정리
            incoming.Clear(); // 정리
        }

        public bool TryGetPeer(ulong clientId, out CSteamID peer) // 42일차: 넷코드 대원 번호 → Steam ID
        {
            peer = CSteamID.Nil; // 기본
            ulong transportId = owner == null ? clientId : owner.GetTransportIdFromClientId(clientId); // 번호 변환
            HSteamNetConnection connection = Lookup(transportId == ulong.MaxValue ? clientId : transportId); // 연결
            return connection != HSteamNetConnection.Invalid && peers.TryGetValue(connection, out peer); // 결과
        }

        private void Update() // 42일차: 로비 입장 확인 대기 처리
        {
            for (int index = pendingAccept.Count - 1; index >= 0; index--) // 대기 연결
            {
                (HSteamNetConnection connection, CSteamID peer, float deadline) pending = pendingAccept[index]; // 연결
                string verdict = Admission?.Invoke(pending.peer); // 다시 판정

                if (verdict == AdmissionWait && Time.unscaledTime < pending.deadline) // 계속 대기
                {
                    continue; // 다음
                }

                pendingAccept.RemoveAt(index); // 대기 끝
                Decide(pending.connection, pending.peer, verdict == AdmissionWait ? "Not a lobby member" : verdict); // 수락 또는 거절
            }
        }

        private void Decide(HSteamNetConnection connection, CSteamID peer, string verdict) // 수락·거절
        {
            if (verdict == null) // 허용
            {
                peers[connection] = peer; // 상대 기록
                SteamNetworkingSockets.AcceptConnection(connection); // 수락 (버전·암호·인원은 넷코드 접속 확인에서)
                return; // 종료
            }

            Debug.LogWarning($"[Project I] Steam 연결 거절 / {peer.m_SteamID} / {verdict}"); // 기록
            SteamNetworkingSockets.CloseConnection(connection, 0, verdict, false); // 거절
        }

        private void OnDestroy() // 파괴
        {
            statusCallback?.Dispose(); // 콜백 해제
            statusCallback = null; // 정리
        }

        private void OnConnectionStatusChanged(SteamNetConnectionStatusChangedCallback_t update) // Steam 연결 상태 변화
        {
            HSteamNetConnection connection = update.m_hConn; // 연결
            ESteamNetworkingConnectionState state = update.m_info.m_eState; // 새 상태

            switch (state)
            {
                case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connecting:
                    if (isServer && update.m_info.m_hListenSocket == listenSocket) // 방장에게 들어오는 연결
                    {
                        CSteamID peer = update.m_info.m_identityRemote.GetSteamID(); // 상대 Steam ID
                        string verdict = peer.IsValid() ? Admission?.Invoke(peer) : "Invalid identity"; // 42일차: 차단·잠금·로비 멤버 확인

                        if (verdict == AdmissionWait) // 로비 입장 반영 대기
                        {
                            pendingAccept.Add((connection, peer, Time.unscaledTime + AdmissionTimeout)); // 대기
                        }
                        else
                        {
                            Decide(connection, peer, verdict); // 수락·거절
                        }
                    }
                    break;

                case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connected:
                    if (isServer) // 방장
                    {
                        ulong id = nextClientId++; // 새 번호
                        connections[id] = connection; // 기록
                        ids[connection] = id; // 기록
                        events.Enqueue((NetworkEvent.Connect, id)); // 알림
                    }
                    else if (connection == serverConnection) // 참가자
                    {
                        events.Enqueue((NetworkEvent.Connect, ServerId)); // 알림
                    }
                    break;

                case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ClosedByPeer:
                case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ProblemDetectedLocally:
                    pendingAccept.RemoveAll(pending => pending.connection == connection); // 대기 중 끊김
                    peers.Remove(connection); // 정리
                    traffic.Remove(connection); // 정리

                    if (isServer && ids.TryGetValue(connection, out ulong closedId)) // 방장: 대원 끊김
                    {
                        Forget(connection); // 정리
                        events.Enqueue((NetworkEvent.Disconnect, closedId)); // 알림
                    }
                    else if (!isServer && connection == serverConnection) // 참가자: 방장 끊김
                    {
                        serverConnection = HSteamNetConnection.Invalid; // 정리
                        events.Enqueue((NetworkEvent.Disconnect, ServerId)); // 알림
                    }

                    SteamNetworkingSockets.CloseConnection(connection, 0, "Closed", false); // Steam 쪽 정리
                    break;
            }
        }

        private void ReceiveAll() // 모든 연결에서 메시지 받기
        {
            if (isServer) // 방장
            {
                receiveList.Clear(); // 복사본
                receiveList.AddRange(ids); // 현재 연결

                foreach (KeyValuePair<HSteamNetConnection, ulong> pair in receiveList) // 대원
                {
                    Receive(pair.Key, pair.Value); // 받기 (폭주면 그 자리에서 끊음)
                }

                return; // 종료
            }

            if (serverConnection != HSteamNetConnection.Invalid) // 참가자
            {
                Receive(serverConnection, ServerId); // 받기
            }
        }

        private void Receive(HSteamNetConnection connection, ulong id) // 연결 하나에서 받기
        {
            int count = SteamNetworkingSockets.ReceiveMessagesOnConnection(connection, messageBuffer, MaxMessagesPerPoll); // 받기
            bool flood = false; // 폭주·과대 메시지

            for (int index = 0; index < count; index++) // 메시지
            {
                SteamNetworkingMessage_t message = Marshal.PtrToStructure<SteamNetworkingMessage_t>(messageBuffer[index]); // 내용

                if (!flood && message.m_cbSize > 0 && message.m_cbSize <= MaxMessageBytes) // 정상 크기
                {
                    byte[] data = new byte[message.m_cbSize]; // 복사본
                    Marshal.Copy(message.m_pData, data, 0, message.m_cbSize); // 복사
                    incoming.Enqueue((id, data)); // 보관
                }
                else if (message.m_cbSize > MaxMessageBytes) // 너무 큼
                {
                    flood = true; // 끊기
                }

                SteamNetworkingMessage_t.Release(messageBuffer[index]); // Steam 메모리 해제 (버리는 메시지 포함)
            }

            if (isServer && count > 0) // 방장: 초당 메시지 수 확인
            {
                if (!traffic.TryGetValue(connection, out TrafficWindow window) || Time.unscaledTime - window.Start >= 1f) // 처음·새 1초
                {
                    window = new TrafficWindow { Count = 0, Start = Time.unscaledTime }; // 초기화
                }

                window.Count += count; // 누적
                traffic[connection] = window; // 저장
                flood |= window.Count > MaxMessagesPerSecond; // 한도 초과
            }

            if (flood && isServer) // 42일차: 폭주 연결 끊기
            {
                Debug.LogWarning($"[Project I] Steam 연결 폭주 차단 / 대원 번호 {id}"); // 기록
                SteamNetworkingSockets.CloseConnection(connection, 0, "Message flood", false); // 닫기
                Forget(connection); // 정리
                peers.Remove(connection); // 정리
                traffic.Remove(connection); // 정리
                events.Enqueue((NetworkEvent.Disconnect, id)); // 넷코드에 끊김 알림
            }
        }

        private HSteamNetConnection Lookup(ulong clientId) // 번호 → 연결
        {
            return connections.TryGetValue(clientId, out HSteamNetConnection connection) ? connection : HSteamNetConnection.Invalid; // 결과
        }

        private void Forget(HSteamNetConnection connection) // 연결 기록 삭제
        {
            if (ids.TryGetValue(connection, out ulong id)) // 있음
            {
                ids.Remove(connection); // 삭제
                connections.Remove(id); // 삭제
            }
        }
    }
}
