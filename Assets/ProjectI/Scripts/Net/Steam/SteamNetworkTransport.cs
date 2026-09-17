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

        public CSteamID HostId { get; set; } = CSteamID.Nil; // 참가할 방장 Steam ID
        public override ulong ServerClientId => ServerId; // 방장 번호
        public override bool IsSupported => Application.platform == RuntimePlatform.WindowsPlayer || Application.platform == RuntimePlatform.WindowsEditor || Application.platform == RuntimePlatform.OSXPlayer || Application.platform == RuntimePlatform.OSXEditor || Application.platform == RuntimePlatform.LinuxPlayer || Application.platform == RuntimePlatform.LinuxEditor; // PC 만

        public override void Initialize(NetworkManager networkManager = null) // 준비
        {
            SteamService.EnsureInitialized(); // Steam
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
                SteamNetworkingSockets.CloseConnection(connection, 0, "Disconnected by host", false); // 닫기
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
            HSteamNetConnection connection = isServer ? Lookup(clientId) : serverConnection; // 연결

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

            ids.Clear(); // 정리
            connections.Clear(); // 정리
            DisconnectLocalClient(); // 참가 연결

            if (listenSocket != HSteamListenSocket.Invalid) // 수신 소켓
            {
                SteamNetworkingSockets.CloseListenSocket(listenSocket); // 닫기
                listenSocket = HSteamListenSocket.Invalid; // 정리
            }

            events.Clear(); // 정리
            incoming.Clear(); // 정리
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
                        SteamNetworkingSockets.AcceptConnection(connection); // 수락 (게임 버전·인원은 넷코드 접속 확인에서)
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
                foreach (KeyValuePair<HSteamNetConnection, ulong> pair in ids) // 대원
                {
                    Receive(pair.Key, pair.Value); // 받기
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

            for (int index = 0; index < count; index++) // 메시지
            {
                SteamNetworkingMessage_t message = Marshal.PtrToStructure<SteamNetworkingMessage_t>(messageBuffer[index]); // 내용
                byte[] data = new byte[message.m_cbSize]; // 복사본
                Marshal.Copy(message.m_pData, data, 0, message.m_cbSize); // 복사
                SteamNetworkingMessage_t.Release(messageBuffer[index]); // Steam 메모리 해제
                incoming.Enqueue((id, data)); // 보관
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
