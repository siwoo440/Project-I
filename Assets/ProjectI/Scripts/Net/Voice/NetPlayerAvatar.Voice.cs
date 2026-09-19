using System.Collections.Generic; // 받을 대원 목록
using ProjectI.Net.Steam; // 내 Steam ID
using ProjectI.Net.Voice; // 음성
using ProjectI.Settings; // 음성 음량
using Unity.Netcode; // 넷코드
using UnityEngine; // 유니티 기본 기능 참조

namespace ProjectI.Net // 협동 네트워크 네임스페이스
{
    public sealed partial class NetPlayerAvatar // 43일차: 음성 전달 (말하는 대원 몸체 → 방장 → 들을 수 있는 대원) — 몸체 프리팹에 이미 있어 프리팹 재생성 불필요
    {
        public const float VoiceHearRange = 40f; // 방장이 전달하는 최대 거리 (3D 재생은 30m 에서 0)
        private static readonly List<ulong> voiceTargets = new List<ulong>(); // 전달 대상 (재사용)
        private readonly NetworkVariable<ulong> steamId = new NetworkVariable<ulong>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner); // 대원별 음량 저장 키
        private VoicePlayback voice; // 이 대원 목소리
        private float nextVoiceSettings; // 다음 음량·3D 갱신
        private bool shownSpeaking; // 이름표에 표시한 말하기 상태

        public static NetPlayerAvatar Local // 내 몸체
        {
            get
            {
                foreach (NetPlayerAvatar avatar in All) // 목록
                {
                    if (avatar != null && avatar.IsSpawned && avatar.IsOwner) // 내 것
                    {
                        return avatar; // 반환
                    }
                }

                return null; // 없음
            }
        }

        public VoicePlayback Voice => voice; // 재생기
        public string VoiceKey => VoicePreferences.KeyOf(steamId.Value, DisplayName); // 음량 저장 키
        public bool IsSpeaking => IsOwner ? VoiceCapture.IsTransmitting : voice != null && voice.IsSpeaking; // 말하는 중
        public int VoicePacketsReceived { get; private set; } // 검증용
        public static int VoicePacketsRelayed { get; private set; } // 44일차: 방장이 받은 참가자 음성 조각 수 (점검용)

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] // 플레이 반복 대비
        private static void ResetVoiceStatics() // 초기화
        {
            VoicePacketsRelayed = 0; // 집계
        }

        private void CreateVoice() // Awake: 입 위치 재생기
        {
            voice = VoicePlayback.Create(transform, new Vector3(0f, 1.6f, 0f), true); // 3D
        }

        private void InitializeVoiceOwner() // 내 몸체: Steam ID 기록
        {
            steamId.Value = SteamService.Initialized ? SteamService.LocalId.m_SteamID : 0UL; // 저장 키
        }

        public bool SendVoice(byte codec, byte[] packet) // 내 음성 조각 전송 (방장이면 바로 전달)
        {
            if (!IsSpawned || !IsOwner || packet == null || packet.Length == 0 || packet.Length > VoiceCodec.MaxPacketBytes) // 불가
            {
                return false; // 실패
            }

            if (IsServer) // 방장
            {
                RelayVoice(codec, packet); // 전달
                return true; // 성공
            }

            VoiceUpRpc(codec, packet); // 방장에게
            return true; // 성공
        }

        [Rpc(SendTo.Server, Delivery = RpcDelivery.Unreliable, InvokePermission = RpcInvokePermission.Owner)]
        private void VoiceUpRpc(byte codec, byte[] packet, RpcParams rpcParams = default) // 방장: 참가자 음성 받기 (몸체 주인만 보낼 수 있음)
        {
            ulong sender = rpcParams.Receive.SenderClientId; // 대원

            if (sender != OwnerClientId || !NetGuard.Allow(sender, NetChannel.Voice, 0.25f)) // 주인 아님·횟수 초과
            {
                return; // 무시
            }

            if (packet == null || packet.Length == 0 || packet.Length > VoiceCodec.MaxPacketBytes || codec > VoiceCodec.CodecSteam) // 잘못된 조각
            {
                NetGuard.Reject(sender, "음성", "잘못된 조각", 2f); // 경고
                return; // 무시
            }

            VoicePacketsRelayed++; // 44일차: 집계
            RelayVoice(codec, packet); // 전달
        }

        private void RelayVoice(byte codec, byte[] packet) // 방장: 들을 수 있는 대원에게만 전달
        {
            voiceTargets.Clear(); // 비우기
            NetPlayerAvatar hostAvatar = null; // 방장 몸체

            foreach (NetPlayerAvatar listener in All) // 대원
            {
                if (listener == null || !listener.IsSpawned || listener == this) // 자신 제외
                {
                    continue; // 다음
                }

                if (listener.OwnerClientId == NetworkManager.ServerClientId) // 방장 (직접 재생)
                {
                    hostAvatar = listener; // 기록
                    continue; // 다음
                }

                if (CanHear(listener, this)) // 들을 수 있음
                {
                    voiceTargets.Add(listener.OwnerClientId); // 추가
                }
            }

            if (voiceTargets.Count > 0) // 대상 있음
            {
                VoiceDownRpc(codec, packet, RpcTarget.Group(voiceTargets, RpcTargetUse.Temp)); // 전달
            }

            if (hostAvatar != null && CanHear(hostAvatar, this)) // 방장도 들음
            {
                PlayVoice(codec, packet); // 재생
            }
        }

        [Rpc(SendTo.SpecifiedInParams, Delivery = RpcDelivery.Unreliable, InvokePermission = RpcInvokePermission.Server)]
        private void VoiceDownRpc(byte codec, byte[] packet, RpcParams rpcParams) // 대원: 방장이 전달한 음성 재생
        {
            PlayVoice(codec, packet); // 재생
        }

        private void PlayVoice(byte codec, byte[] packet) // 내 화면에서 이 대원 목소리 재생
        {
            if (IsOwner || voice == null || !GameSettings.VoiceEnabled || VoicePreferences.IsMuted(VoiceKey)) // 내 목소리·음성 끔·음소거
            {
                return; // 버림
            }

            VoicePacketsReceived++; // 집계
            voice.Push(codec, packet); // 재생
        }

        private static bool CanHear(NetPlayerAvatar listener, NetPlayerAvatar speaker) // 방장 판정: 쓰러짐 규칙 + 같은 맵 거리
        {
            bool speakerDead = speaker.dead.Value; // 말하는 대원 쓰러짐
            bool listenerDead = listener.dead.Value; // 듣는 대원 쓰러짐

            if (speakerDead) // 쓰러진 대원 목소리는 쓰러진 대원만
            {
                return listenerDead; // 결과
            }

            if (listenerDead) // 쓰러진 대원은 모두를 들음
            {
                return true; // 허용
            }

            return speaker.TryGetNetworkWorldPosition(out Vector3 from) && listener.TryGetNetworkWorldPosition(out Vector3 to) && (from - to).sqrMagnitude <= VoiceHearRange * VoiceHearRange; // 같은 맵 거리
        }

        private void UpdateVoice() // LateUpdate: 음량·3D 여부·말하기 표시
        {
            bool speaking = IsSpeaking; // 말하는 중

            if (speaking != shownSpeaking) // 바뀜
            {
                shownSpeaking = speaking; // 기록
                RefreshNameTag(); // 이름표 ♪
            }

            if (voice == null || IsOwner || Time.unscaledTime < nextVoiceSettings) // 간격
            {
                return; // 생략
            }

            nextVoiceSettings = Time.unscaledTime + 0.25f; // 다음
            NetPlayerAvatar local = Local; // 내 몸체
            bool flat = dead.Value || (local != null && local.dead.Value); // 쓰러짐이 섞이면 거리 없이 (2D)
            voice.SetSpatial(!flat); // 적용
            voice.SetUserVolume(GameSettings.VoiceVolume * VoicePreferences.GetVolume(VoiceKey)); // 설정 × 대원별
        }
    }
}
