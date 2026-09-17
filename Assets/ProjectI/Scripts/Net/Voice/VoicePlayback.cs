using ProjectI.Net.Steam; // Steam 음성 해제
using Steamworks; // Steamworks.NET
using UnityEngine; // 유니티 기본 기능 참조

namespace ProjectI.Net.Voice // 음성 채팅 네임스페이스
{
    [RequireComponent(typeof(AudioSource))] // 소리 위치
    public sealed class VoicePlayback : MonoBehaviour // 43일차: 받은 음성을 3D 위치에서 재생 (버퍼 → 오디오 스레드에서 보간 재생 · 벽 너머 먹먹하게)
    {
        private const int BufferSeconds = 1; // 최대 1초 쌓기 (넘치면 오래된 것 버림)
        private const int PrebufferSamples = VoiceCodec.SampleRate / 10; // 100ms 모이면 재생 시작
        private const int MaxLatencySamples = VoiceCodec.SampleRate * 3 / 10; // 300ms 넘게 쌓이면
        private const int TrimTargetSamples = VoiceCodec.SampleRate * 3 / 20; // 150ms 로 줄임
        private const int ReadChunk = 512; // 오디오 스레드가 한 번에 꺼내는 수
        private const float SpeakingHold = 0.3f; // 마지막 조각 후 말하는 중 표시 시간
        private const float OcclusionInterval = 0.2f; // 벽 확인 간격
        private const float OccludedCutoff = 1100f; // 벽 너머 저역 통과
        private const float OpenCutoff = 22000f; // 열린 공간
        private const float OccludedVolume = 0.55f; // 벽 너머 음량
        public const float HearRange = 30f; // 들리는 최대 거리 (3D)

        private readonly VoiceRingBuffer buffer = new VoiceRingBuffer(VoiceCodec.SampleRate * BufferSeconds); // 받은 표본
        private readonly float[] decoded = new float[VoiceCodec.MaxPacketBytes * 8]; // 해제 표본
        private readonly float[] mix = new float[ReadChunk]; // 오디오 스레드 읽기용 (남은 표본은 다음 콜백에서 이어 씀)
        private int mixCount; // 꺼내 둔 수
        private int mixIndex; // 다음에 쓸 위치
        private byte[] steamPcm = new byte[VoiceCodec.SampleRate * 2]; // Steam 해제 PCM (1초)
        private AudioSource source; // 재생
        private AudioLowPassFilter lowPass; // 벽 너머
        private AudioClip carrier; // 1.0 으로 채운 반복 소리 (3D 음량·방향을 그대로 받기 위함)
        private int outputRate = 48000; // 출력 주파수
        private double phase = 1d; // 보간 위치
        private float previousSample; // 보간 왼쪽
        private float nextSample; // 보간 오른쪽
        private bool waiting = true; // 버퍼 모으는 중
        private volatile float gain = 1f; // 음량 (오디오 스레드가 읽음)
        private float lastPacketTime = -10f; // 마지막 조각 시각
        private float nextOcclusion; // 다음 벽 확인
        private float occlusionVolume = 1f; // 벽 음량
        private float userVolume = 1f; // 대원별·설정 음량

        public bool IsSpeaking => Time.unscaledTime - lastPacketTime < SpeakingHold; // 말하는 중
        public float Level { get; private set; } // 최근 소리 크기 (표시용)
        public int Buffered => buffer.Count; // 검증용

        public static VoicePlayback Create(Transform parent, Vector3 localPosition, bool spatial) // 생성
        {
            GameObject voice = new GameObject("Voice"); // 오브젝트
            voice.transform.SetParent(parent, false); // 부모
            voice.transform.localPosition = localPosition; // 입 위치
            AudioSource audio = voice.AddComponent<AudioSource>(); // 재생 (필터보다 먼저)
            VoicePlayback playback = voice.AddComponent<VoicePlayback>(); // 음성 생성 (필터 체인 첫째)
            playback.lowPass = voice.AddComponent<AudioLowPassFilter>(); // 벽 너머 (음성 뒤)
            playback.lowPass.cutoffFrequency = OpenCutoff; // 열림
            playback.source = audio; // 참조
            playback.SetSpatial(spatial); // 3D·2D
            return playback; // 반환
        }

        private void Awake() // 준비
        {
            source = source != null ? source : GetComponent<AudioSource>(); // 재생
            outputRate = AudioSettings.outputSampleRate; // 출력 주파수 (오디오 스레드에서 읽지 않도록 미리)
            source.playOnAwake = false; // 직접 시작
            source.loop = true; // 반복
            source.dopplerLevel = 0f; // 도플러 없음
            source.rolloffMode = AudioRolloffMode.Linear; // 선형 감쇠
            source.minDistance = 2f; // 2m 까지 최대
            source.maxDistance = HearRange; // 30m 에서 0
        }

        private void Start() // 재생 시작
        {
            carrier = AudioClip.Create("VoiceCarrier", outputRate, 1, outputRate, false); // 1초
            float[] ones = new float[outputRate]; // 1.0

            for (int index = 0; index < ones.Length; index++) // 채우기
            {
                ones[index] = 1f; // 값
            }

            carrier.SetData(ones, 0); // 적용
            source.clip = carrier; // 반복 소리
            source.Play(); // 재생 (음성이 없으면 필터가 0 으로 만듦)
        }

        private void OnDestroy() // 정리
        {
            if (carrier != null) // 소리
            {
                Destroy(carrier); // 해제
            }
        }

        public void SetSpatial(bool spatial) // 3D(거리·방향) 또는 2D(쓰러진 대원끼리·자기 시험)
        {
            if (source == null) // 준비 전
            {
                source = GetComponent<AudioSource>(); // 재생
            }

            source.spatialBlend = spatial ? 1f : 0f; // 적용
        }

        public void SetUserVolume(float volume) // 설정 음량 × 대원별 음량
        {
            userVolume = Mathf.Max(0f, volume); // 기록
            gain = userVolume * occlusionVolume; // 적용
        }

        public void Push(byte codec, byte[] data) // 받은 조각 해제 → 버퍼
        {
            if (data == null || data.Length == 0) // 없음
            {
                return; // 생략
            }

            int produced = 0; // 해제 표본 수

            if (codec == VoiceCodec.CodecMuLaw) // μ-law
            {
                produced = VoiceCodec.DecodeMuLaw(data, data.Length, decoded); // 해제
            }
            else if (codec == VoiceCodec.CodecSteam && SteamService.Initialized) // Steam
            {
                EVoiceResult result = SteamUser.DecompressVoice(data, (uint)data.Length, steamPcm, (uint)steamPcm.Length, out uint written, (uint)VoiceCodec.SampleRate); // 해제

                if (result == EVoiceResult.k_EVoiceResultBufferTooSmall) // 버퍼 부족
                {
                    steamPcm = new byte[steamPcm.Length * 2]; // 키움
                    result = SteamUser.DecompressVoice(data, (uint)data.Length, steamPcm, (uint)steamPcm.Length, out written, (uint)VoiceCodec.SampleRate); // 다시
                }

                if (result == EVoiceResult.k_EVoiceResultOK) // 성공
                {
                    produced = VoiceCodec.Pcm16ToFloat(steamPcm, (int)written, decoded); // 변환
                }
            }

            if (produced <= 0) // 해제 실패
            {
                return; // 생략
            }

            Level = VoiceCodec.Rms(decoded, 0, produced); // 크기
            buffer.Write(decoded, produced); // 저장
            buffer.TrimTo(buffer.Count > MaxLatencySamples ? TrimTargetSamples : int.MaxValue); // 보내는 쪽이 빨라 밀리면 지연 줄이기
            lastPacketTime = Time.unscaledTime; // 기록
        }

        private void Update() // 벽 확인
        {
            if (Time.unscaledTime < nextOcclusion) // 간격
            {
                return; // 생략
            }

            nextOcclusion = Time.unscaledTime + OcclusionInterval; // 다음

            if (lowPass == null) // Create 밖에서 붙은 경우
            {
                lowPass = GetComponent<AudioLowPassFilter>(); // 조회
                lowPass = lowPass != null ? lowPass : gameObject.AddComponent<AudioLowPassFilter>(); // 없으면 추가 (음성 뒤)
            }

            AudioListener listener = FindListener(); // 듣는 위치
            bool occluded = false; // 가림

            if (listener != null && source.spatialBlend > 0.5f && IsSpeaking) // 3D 로 말하는 중
            {
                Vector3 from = listener.transform.position; // 귀
                Vector3 to = transform.position; // 입
                occluded = Physics.Linecast(from, to, out RaycastHit hit, ~0, QueryTriggerInteraction.Ignore) && !hit.transform.IsChildOf(transform.parent != null ? transform.parent : transform); // 사이에 벽 (말하는 몸체 제외)
            }

            lowPass.cutoffFrequency = occluded ? OccludedCutoff : OpenCutoff; // 필터
            occlusionVolume = occluded ? OccludedVolume : 1f; // 음량
            gain = userVolume * occlusionVolume; // 적용
        }

        private static AudioListener cachedListener; // 듣는 위치 (카메라)

        private static AudioListener FindListener() // 활성 AudioListener
        {
            if (cachedListener == null || !cachedListener.isActiveAndEnabled) // 없음·꺼짐
            {
                cachedListener = FindAnyObjectByType<AudioListener>(); // 조회
            }

            return cachedListener; // 반환
        }

        private void OnAudioFilterRead(float[] data, int channels) // 오디오 스레드: 1.0 반복 소리 × 음성 (3D 음량·방향 유지)
        {
            int frames = data.Length / channels; // 프레임 수
            double step = (double)VoiceCodec.SampleRate / outputRate; // 입력 간격
            float volume = gain; // 음량

            if (waiting) // 버퍼 모으는 중
            {
                if (buffer.Count < PrebufferSamples) // 부족
                {
                    System.Array.Clear(data, 0, data.Length); // 무음
                    return; // 종료
                }

                waiting = false; // 재생 시작
                phase = 1d; // 처음부터
                previousSample = 0f; // 초기화
                nextSample = 0f; // 초기화
            }

            bool starved = false; // 버퍼 바닥

            for (int frame = 0; frame < frames; frame++) // 출력 프레임
            {
                while (phase >= 1d) // 다음 입력 표본
                {
                    previousSample = nextSample; // 왼쪽
                    nextSample = NextInput(ref starved); // 오른쪽 (부족하면 무음)
                    phase -= 1d; // 보간 위치
                }

                float sample = (previousSample + (nextSample - previousSample) * (float)phase) * volume; // 보간
                phase += step; // 진행
                int offset = frame * channels; // 위치

                for (int channel = 0; channel < channels; channel++) // 채널
                {
                    data[offset + channel] *= sample; // 3D 음량·방향이 들어간 1.0 에 곱함
                }
            }

            if (starved) // 버퍼 바닥
            {
                waiting = true; // 다시 모으기
            }
        }

        private float NextInput(ref bool starved) // 오디오 스레드: 입력 표본 하나 (꺼내 둔 것부터)
        {
            if (mixIndex >= mixCount) // 다 씀
            {
                mixCount = buffer.Read(mix, ReadChunk); // 더 꺼내기
                mixIndex = 0; // 처음

                if (mixCount == 0) // 없음
                {
                    starved = true; // 바닥
                    return 0f; // 무음
                }
            }

            return mix[mixIndex++]; // 다음 표본
        }
    }
}
