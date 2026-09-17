using System.Collections.Generic; // 표본 목록
using ProjectI.Core; // 씬 이름
using ProjectI.Net.Steam; // Steam 음성
using ProjectI.Settings; // 음성 설정
using Steamworks; // Steamworks.NET
using UnityEngine; // 유니티 기본 기능 참조
using UnityEngine.InputSystem; // 누르고 말하기
using UnityEngine.SceneManagement; // 메뉴 확인

namespace ProjectI.Net.Voice // 음성 채팅 네임스페이스
{
    public sealed class VoiceCapture : MonoBehaviour // 43일차: 내 마이크 녹음 → 조각으로 전송 (Steam 음성 우선, 없으면 유니티 마이크 + μ-law)
    {
        private const float SendInterval = 0.05f; // Steam 조각을 모아 보내는 간격 (초당 20번)
        private const int MicFramesPerPacket = 2; // 유니티 마이크: 20ms × 2 = 40ms 씩 전송 (초당 25번)
        private const float ActivationHold = 0.4f; // 항상 켜기: 말이 끊겨도 잠시 계속 전송
        private const float TransmitHold = 0.25f; // 말하는 중 표시 유지

        private static VoiceCapture instance; // 실행 중
        private readonly byte[] steamBuffer = new byte[8192]; // Steam 압축 음성
        private readonly List<byte> steamPending = new List<byte>(); // 보낼 Steam 조각 모음
        private readonly List<float> micSamples = new List<float>(); // 16kHz 로 바꾼 마이크 표본
        private readonly float[] frame = new float[VoiceCodec.FrameSamples * MicFramesPerPacket]; // 보낼 묶음
        private readonly byte[] steamPcm = new byte[VoiceCodec.SampleRate * 2]; // 내 Steam 음성 해제 (크기 표시)
        private readonly float[] levelBuffer = new float[VoiceCodec.SampleRate]; // 크기 계산용
        private float[] micRead = new float[4096]; // 마이크 읽기
        private AudioClip micClip; // 유니티 마이크 녹음
        private string micDevice; // 사용 중인 장치
        private int micPosition; // 마지막 읽은 위치
        private bool steamRecording; // Steam 녹음 중
        private float nextSteamSend; // 다음 Steam 전송
        private float activationUntil; // 항상 켜기 유지
        private float transmitUntil; // 말하는 중 표시
        private VoicePlayback loopback; // 자기 목소리 시험 재생

        public static bool SelfMuted { get; set; } // 내 마이크 끄기 (이번 실행 동안)
        public static bool LoopbackTest { get; set; } // 설정 창 마이크 시험 (내 목소리를 바로 들음)
        public static float InputLevel { get; private set; } // 최근 입력 크기 (0~1)
        public static bool IsTransmitting => instance != null && Time.unscaledTime < instance.transmitUntil; // 전송 중
        public static bool UsingSteam => SteamService.Initialized; // Steam 음성 사용
        public static int PacketsSent { get; private set; } // 검증용
        public static bool PushToTalkHeld => Keyboard.current != null && Keyboard.current.vKey.isPressed; // V 누름

        public static string[] Devices => Microphone.devices; // 마이크 목록

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)] // 게임 시작 시
        private static void Boot() // 녹음 관리자 생성
        {
            if (instance != null) // 이미 있음
            {
                return; // 생략
            }

            GameObject runner = new GameObject("===ProjectI Voice==="); // 오브젝트
            DontDestroyOnLoad(runner); // 유지
            instance = runner.AddComponent<VoiceCapture>(); // 등록
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] // 플레이 반복 대비
        private static void ResetStatics() // 초기화
        {
            instance = null; // 정리
            SelfMuted = false; // 켜짐
            LoopbackTest = false; // 끔
            InputLevel = 0f; // 무음
            PacketsSent = 0; // 집계
        }

        private void OnDestroy() // 정리
        {
            StopAll(); // 녹음 중지

            if (instance == this) // 현재
            {
                instance = null; // 정리
            }
        }

        private void Update() // 녹음·전송
        {
            bool inGame = NetworkSession.IsConnected && SceneManager.GetActiveScene().name != SceneFlowManager.MainMenuSceneName; // 협동 게임 중
            bool active = GameSettings.VoiceEnabled && !SelfMuted && (inGame || LoopbackTest); // 녹음 필요

            if (!active) // 녹음 안 함
            {
                StopAll(); // 중지
                InputLevel = Mathf.MoveTowards(InputLevel, 0f, Time.unscaledDeltaTime * 2f); // 표시 줄이기
                return; // 종료
            }

            bool gate = LoopbackTest || !GameSettings.PushToTalk || PushToTalkHeld; // 누르고 말하기 확인 (시험 중에는 항상)

            if (UsingSteam) // Steam 음성
            {
                StopMicrophone(); // 유니티 마이크 끔
                UpdateSteam(gate); // 처리
            }
            else // 유니티 마이크
            {
                StopSteam(); // Steam 끔
                UpdateMicrophone(gate); // 처리
            }
        }

        // ───────────────────────── Steam ─────────────────────────

        private void UpdateSteam(bool gate) // Steam 녹음 (Steam 이 말소리 감지·압축)
        {
            if (gate && !steamRecording) // 시작
            {
                SteamUser.StartVoiceRecording(); // 녹음
                steamRecording = true; // 기록
            }
            else if (!gate && steamRecording) // 중지 (Steam 이 남은 조각을 잠시 더 줌)
            {
                SteamUser.StopVoiceRecording(); // 중지
                steamRecording = false; // 기록
            }

            EVoiceResult available = SteamUser.GetAvailableVoice(out uint size); // 쌓인 음성

            if (available == EVoiceResult.k_EVoiceResultOK && size > 0) // 있음
            {
                EVoiceResult result = SteamUser.GetVoice(true, steamBuffer, (uint)steamBuffer.Length, out uint written); // 가져오기

                if (result == EVoiceResult.k_EVoiceResultOK && written > 0) // 성공
                {
                    for (int index = 0; index < written; index++) // 모으기
                    {
                        steamPending.Add(steamBuffer[index]); // 추가
                    }

                    MeasureSteam(steamBuffer, (int)written); // 크기 표시
                }
            }
            else
            {
                InputLevel = Mathf.MoveTowards(InputLevel, 0f, Time.unscaledDeltaTime * 2f); // 조용함
            }

            if (steamPending.Count == 0 || Time.unscaledTime < nextSteamSend) // 보낼 것 없음·간격
            {
                return; // 대기
            }

            nextSteamSend = Time.unscaledTime + SendInterval; // 다음

            if (steamPending.Count > VoiceCodec.MaxPacketBytes) // 너무 큼 (오래 멈췄다가 몰림)
            {
                steamPending.Clear(); // 버림
                return; // 종료
            }

            Send(VoiceCodec.CodecSteam, steamPending.ToArray()); // 전송
            steamPending.Clear(); // 비우기
        }

        private void MeasureSteam(byte[] compressed, int length) // 내 Steam 음성 크기 (해제해서 측정·시험 재생)
        {
            byte[] chunk = new byte[length]; // 복사 (해제 함수는 배열 전체 길이를 받음)
            System.Buffer.BlockCopy(compressed, 0, chunk, 0, length); // 복사

            if (SteamUser.DecompressVoice(chunk, (uint)length, steamPcm, (uint)steamPcm.Length, out uint written, (uint)VoiceCodec.SampleRate) == EVoiceResult.k_EVoiceResultOK) // 해제
            {
                int count = VoiceCodec.Pcm16ToFloat(steamPcm, (int)written, levelBuffer); // 변환
                InputLevel = Mathf.Max(VoiceCodec.Rms(levelBuffer, 0, count) * 3f, InputLevel * 0.8f); // 표시 (말소리는 작아서 키움)
            }

            if (LoopbackTest) // 시험
            {
                EnsureLoopback().Push(VoiceCodec.CodecSteam, chunk); // 바로 듣기
            }
        }

        private void StopSteam() // Steam 녹음 중지
        {
            if (steamRecording && SteamService.Initialized) // 녹음 중
            {
                SteamUser.StopVoiceRecording(); // 중지
            }

            steamRecording = false; // 기록
            steamPending.Clear(); // 비우기
        }

        // ───────────────────────── 유니티 마이크 ─────────────────────────

        private void UpdateMicrophone(bool gate) // 유니티 마이크 녹음 → 16kHz → 20ms 조각 → μ-law
        {
            if (!EnsureMicrophone()) // 마이크 없음
            {
                return; // 종료
            }

            int position = Microphone.GetPosition(micDevice); // 현재 위치
            int total = micClip.samples; // 버퍼 길이
            int available = (position - micPosition + total) % total; // 새 표본

            if (available <= 0) // 없음
            {
                return; // 대기
            }

            if (micRead.Length < available) // 읽기 버퍼 부족
            {
                micRead = new float[available]; // 키움
            }

            ReadWrapped(micPosition, available, total); // 읽기 (끝에서 처음으로 이어짐)
            micPosition = position; // 기록
            float micGain = GameSettings.MicGain; // 입력 음량

            for (int index = 0; index < available; index++) // 음량
            {
                micRead[index] *= micGain; // 적용
            }

            VoiceCodec.Resample(micRead, available, micClip.frequency, micSamples); // 16kHz

            while (micSamples.Count >= frame.Length) // 40ms 묶음
            {
                micSamples.CopyTo(0, frame, 0, frame.Length); // 꺼내기
                micSamples.RemoveRange(0, frame.Length); // 제거
                float level = VoiceCodec.Rms(frame, 0, frame.Length); // 크기
                InputLevel = Mathf.Max(level * 3f, InputLevel * 0.8f); // 표시

                if (!GameSettings.PushToTalk && level >= GameSettings.VoiceThreshold) // 항상 켜기: 말소리 감지
                {
                    activationUntil = Time.unscaledTime + ActivationHold; // 유지
                }

                bool speaking = gate && (GameSettings.PushToTalk || LoopbackTest || Time.unscaledTime < activationUntil); // 전송 여부

                if (!speaking) // 조용함
                {
                    continue; // 버림
                }

                byte[] packet = new byte[frame.Length]; // μ-law 1바이트/표본
                VoiceCodec.EncodeMuLaw(frame, 0, frame.Length, packet, 0); // 압축

                if (LoopbackTest) // 시험
                {
                    EnsureLoopback().Push(VoiceCodec.CodecMuLaw, packet); // 바로 듣기
                }
                else
                {
                    Send(VoiceCodec.CodecMuLaw, packet); // 전송
                }
            }

            if (micSamples.Count > VoiceCodec.SampleRate) // 너무 밀림
            {
                micSamples.Clear(); // 버림
            }
        }

        private void ReadWrapped(int start, int count, int total) // 원형 녹음 버퍼 읽기
        {
            int first = Mathf.Min(count, total - start); // 끝까지
            float[] part = new float[first]; // 앞부분
            micClip.GetData(part, start); // 읽기
            System.Array.Copy(part, 0, micRead, 0, first); // 복사

            if (count > first) // 처음부터 이어짐
            {
                float[] rest = new float[count - first]; // 뒷부분
                micClip.GetData(rest, 0); // 읽기
                System.Array.Copy(rest, 0, micRead, first, rest.Length); // 복사
            }
        }

        private bool EnsureMicrophone() // 마이크 시작 (설정 장치, 없으면 기본)
        {
            string wanted = ResolveDevice(); // 장치

            if (micClip != null && micDevice == wanted && Microphone.IsRecording(micDevice)) // 이미 녹음 중
            {
                return true; // 사용
            }

            StopMicrophone(); // 이전 정리

            if (Microphone.devices.Length == 0) // 마이크 없음
            {
                return false; // 사용 불가
            }

            micDevice = wanted; // 장치
            Microphone.GetDeviceCaps(micDevice, out int minFrequency, out int maxFrequency); // 지원 주파수
            int frequency = minFrequency == 0 && maxFrequency == 0 ? VoiceCodec.SampleRate : Mathf.Clamp(VoiceCodec.SampleRate, minFrequency, maxFrequency); // 16kHz 우선
            micClip = Microphone.Start(micDevice, true, 1, frequency); // 1초 원형 녹음
            micPosition = 0; // 처음
            micSamples.Clear(); // 비우기
            Debug.Log($"[Project I] 마이크 시작 / {(string.IsNullOrEmpty(micDevice) ? "기본 장치" : micDevice)} / {frequency}Hz"); // 기록
            return micClip != null; // 결과
        }

        private static string ResolveDevice() // 설정 장치가 없으면 기본 (null)
        {
            string device = GameSettings.MicDevice; // 설정

            foreach (string candidate in Microphone.devices) // 목록
            {
                if (candidate == device) // 있음
                {
                    return device; // 사용
                }
            }

            return null; // 기본 장치
        }

        private void StopMicrophone() // 유니티 마이크 중지
        {
            if (micClip == null) // 없음
            {
                return; // 생략
            }

            Microphone.End(micDevice); // 중지
            Destroy(micClip); // 정리
            micClip = null; // 정리
            micSamples.Clear(); // 비우기
        }

        private void StopAll() // 모든 녹음 중지
        {
            StopSteam(); // Steam
            StopMicrophone(); // 유니티 마이크

            if (loopback != null && !LoopbackTest) // 시험 끝
            {
                Destroy(loopback.gameObject); // 정리
                loopback = null; // 정리
            }
        }

        // ───────────────────────── 전송 ─────────────────────────

        private void Send(byte codec, byte[] packet) // 내 몸체를 통해 방장에게
        {
            transmitUntil = Time.unscaledTime + TransmitHold; // 표시

            if (LoopbackTest) // 시험은 보내지 않음
            {
                return; // 종료
            }

            NetPlayerAvatar local = NetPlayerAvatar.Local; // 내 몸체

            if (local != null && local.SendVoice(codec, packet)) // 전송
            {
                PacketsSent++; // 집계
            }
        }

        private VoicePlayback EnsureLoopback() // 자기 목소리 시험 재생기
        {
            if (loopback == null) // 없음
            {
                loopback = VoicePlayback.Create(transform, Vector3.zero, false); // 2D
            }

            loopback.SetUserVolume(GameSettings.VoiceVolume); // 음량
            return loopback; // 반환
        }
    }
}
