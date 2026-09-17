using System.Collections.Generic; // 표본 목록
using System.Text; // 보고
using ProjectI.Net; // 요청 검사
using ProjectI.Net.Voice; // 음성
using UnityEditor; // 에디터 기능
using UnityEngine; // 유니티 기본 기능

namespace ProjectI.EditorTools // 에디터 도구 네임스페이스
{
    public static class Phase18Day43VoiceCheck // 43일차: 음성 압축·버퍼·변환·전송 한도 자가 시험 (마이크·네트워크 없이)
    {
        private const ulong FakeGuest = 9101; // 시험용 참가자 번호

        [MenuItem("Project I/Day 43/Run Voice Self-Test")] // 메뉴
        public static void Run() // 시험 실행
        {
            StringBuilder report = new StringBuilder(); // 보고
            int passed = 0; // 통과
            int failed = 0; // 실패

            void Check(string name, bool ok) // 한 항목
            {
                report.AppendLine($"{(ok ? "PASS" : "FAIL")}  {name}"); // 기록

                if (ok) // 통과
                {
                    passed++; // 집계
                }
                else
                {
                    failed++; // 집계
                }
            }

            // μ-law 왕복 (440Hz 사인파 · 신호 대 잡음비)
            float[] sine = new float[VoiceCodec.FrameSamples * 2]; // 40ms
            for (int index = 0; index < sine.Length; index++) // 채우기
            {
                sine[index] = 0.5f * Mathf.Sin(2f * Mathf.PI * 440f * index / VoiceCodec.SampleRate); // 사인파
            }

            byte[] packed = new byte[sine.Length]; // 압축
            VoiceCodec.EncodeMuLaw(sine, 0, sine.Length, packed, 0); // 압축
            float[] restored = new float[sine.Length]; // 해제
            int restoredCount = VoiceCodec.DecodeMuLaw(packed, packed.Length, restored); // 해제
            double signal = 0d; // 신호
            double noise = 0d; // 잡음

            for (int index = 0; index < sine.Length; index++) // 비교
            {
                signal += sine[index] * sine[index]; // 누적
                float error = sine[index] - restored[index]; // 차이
                noise += error * error; // 누적
            }

            double snr = 10d * System.Math.Log10(signal / System.Math.Max(noise, 1e-12)); // dB
            Check($"μ-law 왕복 표본 수 ({restoredCount}/{sine.Length})", restoredCount == sine.Length); // 길이
            Check($"μ-law 음질 (신호 대 잡음비 {snr:0.0}dB ≥ 30)", snr >= 30d); // 음질
            Check("μ-law 압축률 (표본당 1바이트 = 16비트의 절반)", packed.Length == sine.Length); // 크기
            float[] silence = new float[4]; // 무음
            byte[] silencePacked = new byte[4]; // 압축
            VoiceCodec.EncodeMuLaw(silence, 0, 4, silencePacked, 0); // 압축
            float[] silenceRestored = new float[4]; // 해제
            VoiceCodec.DecodeMuLaw(silencePacked, 4, silenceRestored); // 해제
            Check("무음은 무음으로", Mathf.Abs(silenceRestored[0]) < 0.001f); // 무음
            byte[] loud = new byte[1]; // 최대
            VoiceCodec.EncodeMuLaw(new[] { 5f }, 0, 1, loud, 0); // 범위 밖 입력
            float[] loudRestored = new float[1]; // 해제
            VoiceCodec.DecodeMuLaw(loud, 1, loudRestored); // 해제
            Check("범위 밖 입력은 최대값으로 자름", loudRestored[0] > 0.95f && loudRestored[0] <= 1f); // 자르기

            // 16비트 PCM
            byte[] pcm = { 0x00, 0x40, 0x00, 0xC0 }; // +16384, -16384
            float[] pcmFloat = new float[2]; // 결과
            VoiceCodec.Pcm16ToFloat(pcm, pcm.Length, pcmFloat); // 변환
            Check("16비트 PCM 변환", Mathf.Abs(pcmFloat[0] - 0.5f) < 0.001f && Mathf.Abs(pcmFloat[1] + 0.5f) < 0.001f); // 값

            // 주파수 변환
            List<float> resampled = new List<float>(); // 결과
            VoiceCodec.Resample(new float[4800], 4800, 48000, resampled); // 48kHz 0.1초
            Check($"48kHz → 16kHz 길이 ({resampled.Count}, 기대 약 1600)", Mathf.Abs(resampled.Count - 1600) <= 2); // 길이

            // 링 버퍼
            VoiceRingBuffer ring = new VoiceRingBuffer(8); // 8칸
            ring.Write(new float[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }, 10); // 넘치게 쓰기
            float[] read = new float[8]; // 읽기
            int readCount = ring.Read(read, 8); // 읽기
            Check("버퍼가 넘치면 오래된 표본을 버림", readCount == 8 && Mathf.Approximately(read[0], 3f) && Mathf.Approximately(read[7], 10f)); // 최신 유지
            Check("다 읽으면 비어 있음", ring.Count == 0 && ring.Read(read, 8) == 0); // 비움
            ring.Write(new float[] { 1, 2, 3, 4, 5, 6 }, 6); // 6개
            int dropped = ring.TrimTo(2); // 2개만 남김
            int kept = ring.Read(read, 8); // 읽기
            Check("지연 줄이기는 오래된 것부터 버림", dropped == 4 && kept == 2 && Mathf.Approximately(read[0], 5f)); // 최신 유지

            // 전송 한도
            NetGuard.Reset(); // 초기화
            int allowed = 0; // 허용
            for (int index = 0; index < 200; index++) // 1초 안에 200조각
            {
                allowed += NetGuard.Allow(FakeGuest, NetChannel.Voice, 0.25f) ? 1 : 0; // 음성
            }

            Check($"음성 폭주 제한 (허용 {allowed}/200, 기대 80)", allowed == 80); // 순간 최대
            Check($"음성 초과는 약한 경고 (내보냄 {NetGuard.TotalKicked}, 120×0.25 = 30 < 40)", NetGuard.TotalKicked == 0); // 바로 내보내지 않음
            NetGuard.Reset(); // 정리

            // 대원별 음량 키
            Check("Steam ID 가 있으면 ID 기준 키", VoicePreferences.KeyOf(76561198000000000UL, "철수") == "s76561198000000000"); // Steam
            Check("Steam ID 가 없으면 이름 기준 키", VoicePreferences.KeyOf(0, "원정대원 2") == "n원정대원 2"); // 이름
            Check("한 조각 최대 크기 2KB", VoiceCodec.MaxPacketBytes == 2048); // 한도

            string header = $"[Project I] 43일차 음성 자가 시험\n결과 {passed} 통과 · {failed} 실패\n"; // 요약

            if (failed == 0) // 모두 통과
            {
                Debug.Log(header + report); // 기록
            }
            else
            {
                Debug.LogError(header + report); // 오류
            }
        }
    }
}
