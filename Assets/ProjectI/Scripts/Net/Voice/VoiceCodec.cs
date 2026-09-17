using System; // 배열
using System.Collections.Generic; // 목록
using UnityEngine; // 유니티 기본 기능 참조

namespace ProjectI.Net.Voice // 43일차: 음성 채팅 네임스페이스
{
    public static class VoiceCodec // 음성 압축 (μ-law 8비트 · 16kHz) · 변환 도구
    {
        public const int SampleRate = 16000; // 음성 표본 주파수
        public const int FrameSamples = 320; // 20ms 한 조각
        public const byte CodecMuLaw = 0; // 유니티 마이크 + μ-law (Steam 이 없을 때)
        public const byte CodecSteam = 1; // Steam 음성 (압축은 Steam 이 처리)
        public const int MaxPacketBytes = 2048; // 한 번에 보내는 최대 크기
        private const int Bias = 0x84; // μ-law 치우침
        private const int Clip = 32635; // 최대 크기
        private static readonly short[] DecodeTable = BuildDecodeTable(); // 해제 표

        public static void EncodeMuLaw(float[] samples, int offset, int count, byte[] destination, int destinationOffset) // float → μ-law
        {
            for (int index = 0; index < count; index++) // 표본
            {
                int pcm = (int)(Mathf.Clamp(samples[offset + index], -1f, 1f) * 32767f); // 16비트
                int sign = (pcm >> 8) & 0x80; // 부호

                if (sign != 0) // 음수
                {
                    pcm = -pcm; // 절댓값
                }

                pcm = Math.Min(pcm, Clip) + Bias; // 치우침
                int exponent = 7; // 지수

                for (int mask = 0x4000; (pcm & mask) == 0 && exponent > 0; mask >>= 1) // 최상위 비트 찾기
                {
                    exponent--; // 감소
                }

                int mantissa = (pcm >> (exponent + 3)) & 0x0F; // 가수
                destination[destinationOffset + index] = (byte)~(sign | (exponent << 4) | mantissa); // 비트 반전 저장
            }
        }

        public static int DecodeMuLaw(byte[] source, int count, float[] destination) // μ-law → float (해제한 표본 수)
        {
            int length = Math.Min(count, destination.Length); // 길이

            for (int index = 0; index < length; index++) // 표본
            {
                destination[index] = DecodeTable[source[index]] / 32768f; // 표 조회
            }

            return length; // 반환
        }

        public static int Pcm16ToFloat(byte[] pcm, int byteCount, float[] destination) // 16비트 PCM(리틀 엔디언) → float
        {
            int length = Math.Min(byteCount / 2, destination.Length); // 표본 수

            for (int index = 0; index < length; index++) // 표본
            {
                destination[index] = (short)(pcm[index * 2] | (pcm[index * 2 + 1] << 8)) / 32768f; // 변환
            }

            return length; // 반환
        }

        public static float Rms(float[] samples, int offset, int count) // 소리 크기 (0~1)
        {
            if (count <= 0) // 없음
            {
                return 0f; // 무음
            }

            double sum = 0d; // 제곱합

            for (int index = 0; index < count; index++) // 표본
            {
                float value = samples[offset + index]; // 값
                sum += value * value; // 누적
            }

            return (float)Math.Sqrt(sum / count); // 제곱평균제곱근
        }

        public static void Resample(float[] source, int count, int sourceRate, List<float> destination) // 선형 보간 주파수 변환 (→ 16kHz)
        {
            if (sourceRate == SampleRate) // 같음
            {
                for (int index = 0; index < count; index++) // 그대로
                {
                    destination.Add(source[index]); // 추가
                }

                return; // 종료
            }

            double step = (double)sourceRate / SampleRate; // 원본 간격

            for (double position = 0d; position < count - 1; position += step) // 보간
            {
                int left = (int)position; // 왼쪽
                float fraction = (float)(position - left); // 비율
                destination.Add(source[left] + (source[left + 1] - source[left]) * fraction); // 추가
            }
        }

        private static short[] BuildDecodeTable() // μ-law 해제 표 (256개)
        {
            short[] table = new short[256]; // 표

            for (int value = 0; value < 256; value++) // 모든 바이트
            {
                int inverted = ~value & 0xFF; // 반전
                int sign = inverted & 0x80; // 부호
                int exponent = (inverted >> 4) & 0x07; // 지수
                int mantissa = inverted & 0x0F; // 가수
                int magnitude = (((mantissa << 3) + Bias) << exponent) - Bias; // 크기
                table[value] = (short)(sign != 0 ? -magnitude : magnitude); // 저장
            }

            return table; // 반환
        }
    }

    public sealed class VoiceRingBuffer // 스레드 안전 음성 버퍼 (넘치면 오래된 표본을 버림)
    {
        private readonly float[] samples; // 저장소
        private readonly object gate = new object(); // 잠금
        private int readIndex; // 읽을 위치
        private int count; // 들어 있는 수

        public VoiceRingBuffer(int capacity) // 생성
        {
            samples = new float[Math.Max(1, capacity)]; // 저장소
        }

        public int Capacity => samples.Length; // 크기

        public int Count // 들어 있는 수
        {
            get
            {
                lock (gate) // 잠금
                {
                    return count; // 반환
                }
            }
        }

        public void Write(float[] source, int length) // 쓰기
        {
            lock (gate) // 잠금
            {
                for (int index = 0; index < length; index++) // 표본
                {
                    if (count == samples.Length) // 가득 참
                    {
                        readIndex = (readIndex + 1) % samples.Length; // 가장 오래된 것 버림
                        count--; // 감소
                    }

                    samples[(readIndex + count) % samples.Length] = source[index]; // 저장
                    count++; // 증가
                }
            }
        }

        public int Read(float[] destination, int length) // 읽기 (읽은 수)
        {
            lock (gate) // 잠금
            {
                int take = Math.Min(length, count); // 가능한 수

                for (int index = 0; index < take; index++) // 표본
                {
                    destination[index] = samples[readIndex]; // 복사
                    readIndex = (readIndex + 1) % samples.Length; // 다음
                }

                count -= take; // 감소
                return take; // 반환
            }
        }

        public int TrimTo(int maxCount) // 너무 쌓이면 오래된 표본을 버려 지연을 줄임 (버린 수)
        {
            lock (gate) // 잠금
            {
                int drop = Math.Max(0, count - maxCount); // 버릴 수
                readIndex = (readIndex + drop) % samples.Length; // 건너뜀
                count -= drop; // 감소
                return drop; // 반환
            }
        }

        public void Clear() // 비우기
        {
            lock (gate) // 잠금
            {
                readIndex = 0; // 처음
                count = 0; // 없음
            }
        }
    }

    public static class VoicePreferences // 대원별 음량·음소거 (내 컴퓨터에만 저장)
    {
        private const string Prefix = "ProjectI.Voice."; // 저장 키
        public const float MaxPlayerVolume = 2f; // 대원별 최대 200%

        public static string KeyOf(ulong steamId, string displayName) // 저장 키 (Steam ID 우선, 없으면 이름)
        {
            return steamId != 0 ? $"s{steamId}" : $"n{displayName}"; // 키
        }

        public static float GetVolume(string key) // 대원 음량 (기본 100%)
        {
            return Mathf.Clamp(PlayerPrefs.GetFloat(Prefix + "Volume." + key, 1f), 0f, MaxPlayerVolume); // 값
        }

        public static void SetVolume(string key, float value) // 대원 음량 저장
        {
            PlayerPrefs.SetFloat(Prefix + "Volume." + key, Mathf.Clamp(value, 0f, MaxPlayerVolume)); // 저장
        }

        public static bool IsMuted(string key) // 음소거 여부
        {
            return PlayerPrefs.GetInt(Prefix + "Muted." + key, 0) == 1; // 값
        }

        public static void SetMuted(string key, bool muted) // 음소거 저장
        {
            PlayerPrefs.SetInt(Prefix + "Muted." + key, muted ? 1 : 0); // 저장
            PlayerPrefs.Save(); // 기록
        }
    }
}
