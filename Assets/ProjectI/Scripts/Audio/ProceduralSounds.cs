using System; // 난수
using System.Collections.Generic; // 캐시
using UnityEngine; // 유니티 기본 기능 참조

namespace ProjectI.Audio // 소리 네임스페이스
{
    public enum SoundCategory // 음량 분류 (설정 창과 연결)
    {
        Music, // 음악
        Sfx, // 효과음
        Ambience, // 환경음
    }

    public enum SoundId // 소리 종류
    {
        UiHover, // 메뉴 항목 강조
        UiClick, // 메뉴 선택
        UiDenied, // 불가
        FootstepStone, // 돌 발소리
        FootstepWood, // 나무 발소리
        FootstepDirt, // 흙 발소리
        Land, // 착지
        ItemPickup, // 줍기
        ItemDrop, // 내려놓기
        SaleBell, // 판매 벨
        WagonBell, // 마차 종
        Purchase, // 구매 (동전)
        Sale, // 판매 (종 + 동전)
        DoorOpen, // 문 열림
        DoorClose, // 문 닫힘
        MenuMusic, // 메인 메뉴 음악 (반복)
        WindLoop, // 바람 (반복)
        FactoryHum, // 공장 굴뚝 기계음 (반복)
        DungeonDrone, // 지하 울림 (반복)
    }

    public static class ProceduralSounds // 외부 음원 없이 코드로 합성하는 임시 소리 (22050Hz 모노)
    {
        public const int SampleRate = 22050; // 샘플레이트
        public const int Variants = 3; // 발소리 등 변형 수
        private static readonly Dictionary<(SoundId, int), AudioClip> Cache = new Dictionary<(SoundId, int), AudioClip>(); // 캐시

        public static bool IsLoop(SoundId id) // 반복 소리 여부
        {
            return id == SoundId.MenuMusic || id == SoundId.WindLoop || id == SoundId.FactoryHum || id == SoundId.DungeonDrone; // 반복
        }

        public static int VariantCount(SoundId id) // 변형 수
        {
            return id == SoundId.FootstepStone || id == SoundId.FootstepWood || id == SoundId.FootstepDirt ? Variants : 1; // 발소리만 여러 개
        }

        public static AudioClip Get(SoundId id, int variant = 0) // 소리 (처음 요청 시 합성)
        {
            variant = Mathf.Clamp(variant, 0, VariantCount(id) - 1); // 범위
            (SoundId, int) key = (id, variant); // 키

            if (Cache.TryGetValue(key, out AudioClip cached) && cached != null) // 캐시
            {
                return cached; // 반환
            }

            float[] data = Synthesize(id, new System.Random(((int)id * 7919) + variant)); // 합성
            AudioClip clip = AudioClip.Create($"Proc_{id}_{variant}", data.Length, 1, SampleRate, false); // 클립
            clip.SetData(data, 0); // 데이터
            Cache[key] = clip; // 저장
            return clip; // 반환
        }

        private static float[] Synthesize(SoundId id, System.Random random) // 종류별 합성
        {
            switch (id)
            {
                case SoundId.UiHover: return Sweep(0.025f, 1500f, 1600f, 0.12f, 60f);
                case SoundId.UiClick: return Sweep(0.06f, 950f, 620f, 0.22f, 45f);
                case SoundId.UiDenied: return Denied();
                case SoundId.FootstepStone: return Footstep(random, 0.75f, 0f, 0.09f);
                case SoundId.FootstepWood: return Footstep(random, 0.35f, 150f, 0.12f);
                case SoundId.FootstepDirt: return Footstep(random, 0.18f, 0f, 0.1f);
                case SoundId.Land: return Thud(random, 0.22f, 95f, 0.55f);
                case SoundId.ItemPickup: return Pickup(random);
                case SoundId.ItemDrop: return Thud(random, 0.16f, 140f, 0.4f);
                case SoundId.SaleBell: return Bell(1320f, 1.4f, 0.35f);
                case SoundId.WagonBell: return Bell(560f, 2.4f, 0.45f);
                case SoundId.Purchase: return Coins(random, 4);
                case SoundId.Sale: return Mix(Bell(1760f, 0.9f, 0.22f), Coins(random, 6), 0.08f);
                case SoundId.DoorOpen: return DoorCreak(random);
                case SoundId.DoorClose: return Thud(random, 0.3f, 80f, 0.6f);
                case SoundId.MenuMusic: return MenuMusic(random);
                case SoundId.WindLoop: return Wind(random);
                case SoundId.FactoryHum: return Factory(random);
                case SoundId.DungeonDrone: return Drone(random);
                default: return new float[SampleRate / 10];
            }
        }

        private static float[] Sweep(float seconds, float fromHz, float toHz, float amplitude, float decay) // 짧은 음 (주파수 미끄러짐)
        {
            float[] data = new float[Mathf.CeilToInt(seconds * SampleRate)]; // 버퍼
            double phase = 0d; // 위상

            for (int i = 0; i < data.Length; i++) // 샘플
            {
                float t = i / (float)SampleRate; // 시간
                float hz = Mathf.Lerp(fromHz, toHz, t / seconds); // 주파수
                phase += 2d * Math.PI * hz / SampleRate; // 위상 진행
                float square = Mathf.Sign(Mathf.Sin((float)phase)) * 0.3f + Mathf.Sin((float)phase) * 0.7f; // 약간 각진 소리
                data[i] = square * amplitude * Mathf.Exp(-t * decay) * Attack(t, 0.002f); // 감쇠
            }

            return data; // 반환
        }

        private static float[] Denied() // 낮은 두 번 버저
        {
            float[] data = new float[(int)(0.28f * SampleRate)]; // 버퍼

            for (int i = 0; i < data.Length; i++) // 샘플
            {
                float t = i / (float)SampleRate; // 시간
                bool on = t < 0.09f || (t > 0.14f && t < 0.24f); // 두 번
                float square = Mathf.Sign(Mathf.Sin(2f * Mathf.PI * 170f * t)); // 사각파
                data[i] = on ? square * 0.12f : 0f; // 출력
            }

            return Smooth(data, 0.35f); // 날카로움 줄임
        }

        private static float[] Footstep(System.Random random, float brightness, float thumpHz, float seconds) // 발소리 (잡음 + 선택적 울림)
        {
            float[] data = new float[(int)(seconds * SampleRate)]; // 버퍼
            float low = 0f; // 저역 필터 상태
            float cutoff = Mathf.Lerp(0.04f, 0.6f, brightness) * (0.85f + (float)random.NextDouble() * 0.3f); // 밝기 변형
            float gain = 0.55f + (float)random.NextDouble() * 0.2f; // 세기 변형

            for (int i = 0; i < data.Length; i++) // 샘플
            {
                float t = i / (float)SampleRate; // 시간
                float noise = (float)(random.NextDouble() * 2d - 1d); // 잡음
                low += cutoff * (noise - low); // 저역 통과
                float thump = thumpHz > 0f ? Mathf.Sin(2f * Mathf.PI * thumpHz * t) * Mathf.Exp(-t * 40f) * 0.6f : 0f; // 나무 울림
                float scuff = t > 0.035f ? low * 0.35f * Mathf.Exp(-(t - 0.035f) * 50f) : 0f; // 두 번째 끌림
                data[i] = ((low * Mathf.Exp(-t * 70f)) + scuff + thump) * gain * Attack(t, 0.003f); // 합성
            }

            return data; // 반환
        }

        private static float[] Thud(System.Random random, float seconds, float hz, float amplitude) // 둔탁한 충돌음
        {
            float[] data = new float[(int)(seconds * SampleRate)]; // 버퍼
            float low = 0f; // 필터

            for (int i = 0; i < data.Length; i++) // 샘플
            {
                float t = i / (float)SampleRate; // 시간
                low += 0.08f * ((float)(random.NextDouble() * 2d - 1d) - low); // 어두운 잡음
                float body = Mathf.Sin(2f * Mathf.PI * hz * t * (1f - t * 0.8f)); // 떨어지는 울림
                data[i] = ((body * 0.7f) + (low * 0.9f)) * amplitude * Mathf.Exp(-t * 18f) * Attack(t, 0.002f); // 합성
            }

            return data; // 반환
        }

        private static float[] Pickup(System.Random random) // 줍기 (가볍게 올라가는 소리)
        {
            float[] data = new float[(int)(0.12f * SampleRate)]; // 버퍼
            float low = 0f; // 필터

            for (int i = 0; i < data.Length; i++) // 샘플
            {
                float t = i / (float)SampleRate; // 시간
                low += 0.2f * ((float)(random.NextDouble() * 2d - 1d) - low); // 천 스침
                float tone = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(320f, 560f, t / 0.12f) * t); // 올라가는 음
                data[i] = ((tone * 0.18f) + (low * 0.25f)) * Mathf.Exp(-t * 22f) * Attack(t, 0.005f); // 합성
            }

            return data; // 반환
        }

        private static float[] Bell(float baseHz, float seconds, float amplitude) // 종 (비조화 배음)
        {
            float[] ratios = { 1f, 2.0f, 2.76f, 4.07f, 5.4f }; // 배음 비
            float[] gains = { 1f, 0.5f, 0.45f, 0.25f, 0.15f }; // 배음 세기
            float[] decays = { 2.2f, 3f, 4f, 6f, 8f }; // 배음 감쇠
            float[] data = new float[(int)(seconds * SampleRate)]; // 버퍼

            for (int i = 0; i < data.Length; i++) // 샘플
            {
                float t = i / (float)SampleRate; // 시간
                float sum = 0f; // 합

                for (int p = 0; p < ratios.Length; p++) // 배음
                {
                    sum += Mathf.Sin(2f * Mathf.PI * baseHz * ratios[p] * t) * gains[p] * Mathf.Exp(-t * decays[p] / seconds * 2f); // 배음
                }

                data[i] = sum * amplitude * 0.45f * Attack(t, 0.002f) * Fade(t, seconds, 0.1f); // 합성
            }

            return data; // 반환
        }

        private static float[] Coins(System.Random random, int count) // 동전 짤랑
        {
            float seconds = 0.12f + (count * 0.08f); // 길이
            float[] data = new float[(int)(seconds * SampleRate)]; // 버퍼

            for (int c = 0; c < count; c++) // 동전
            {
                float start = c * 0.065f + (float)random.NextDouble() * 0.02f; // 시작
                float hz = 3000f + (float)random.NextDouble() * 1800f; // 음높이
                float gain = 0.12f + (float)random.NextDouble() * 0.06f; // 세기

                for (int i = (int)(start * SampleRate); i < data.Length; i++) // 샘플
                {
                    float t = (i / (float)SampleRate) - start; // 동전 시간
                    float ring = Mathf.Sin(2f * Mathf.PI * hz * t) + (0.5f * Mathf.Sin(2f * Mathf.PI * hz * 1.52f * t)); // 금속음
                    data[i] += ring * gain * Mathf.Exp(-t * 28f) * Attack(t, 0.001f); // 더하기
                }
            }

            return data; // 반환
        }

        private static float[] DoorCreak(System.Random random) // 삐걱 + 끝 울림
        {
            float seconds = 0.7f; // 길이
            float[] data = new float[(int)(seconds * SampleRate)]; // 버퍼
            double phase = 0d; // 위상
            float band = 0f; // 필터

            for (int i = 0; i < data.Length; i++) // 샘플
            {
                float t = i / (float)SampleRate; // 시간
                float hz = 190f + (60f * Mathf.Sin(t * 5f)) + (float)random.NextDouble() * 20f; // 흔들리는 음높이
                phase += 2d * Math.PI * hz / SampleRate; // 위상
                float saw = (float)((phase / (2d * Math.PI)) % 1d) * 2f - 1f; // 톱니파
                band += 0.25f * (saw - band); // 부드럽게
                float creak = band * 0.16f * Mathf.Clamp01(Mathf.Sin(t / 0.55f * Mathf.PI)) * (t < 0.55f ? 1f : 0f); // 삐걱 구간
                float knock = t > 0.55f ? Mathf.Sin(2f * Mathf.PI * 110f * (t - 0.55f)) * 0.3f * Mathf.Exp(-(t - 0.55f) * 30f) : 0f; // 끝 울림
                data[i] = (creak + knock) * Fade(t, seconds, 0.02f); // 합성
            }

            return data; // 반환
        }

        private static float[] MenuMusic(System.Random random) // 느린 단조 화음 + 오르골 선율 (16초 반복)
        {
            const float bar = 4f; // 한 마디 (초)
            int[][] chords = { new[] { 57, 60, 64 }, new[] { 53, 57, 60 }, new[] { 48, 52, 55 }, new[] { 52, 56, 59 } }; // Am · F · C · E (MIDI)
            float seconds = bar * chords.Length; // 길이
            int length = (int)(seconds * SampleRate); // 샘플 수
            float[] data = new float[length]; // 버퍼

            for (int c = 0; c < chords.Length; c++) // 화음
            {
                float start = c * bar; // 시작

                for (int n = 0; n < chords[c].Length; n++) // 음
                {
                    float hz = Midi(chords[c][n] - 12); // 한 옥타브 아래 바탕 화음
                    AddTone(data, start - 0.4f, bar + 0.8f, hz, 0.05f, 0.9f, 0.9f, seconds, true); // 천천히 들어오고 나가는 화음
                }

                int[] pattern = { 0, 1, 2, 1, 2, 0, 1, 2 }; // 아르페지오 순서

                for (int step = 0; step < pattern.Length; step++) // 8분음표
                {
                    if (random.NextDouble() < 0.25) // 가끔 쉼
                    {
                        continue; // 쉼
                    }

                    int note = chords[c][pattern[step]] + 12 + (step == 4 && random.NextDouble() < 0.5 ? 12 : 0); // 오르골 음
                    AddTone(data, start + (step * bar / 8f), 1.6f, Midi(note), 0.035f, 0.005f, 1.4f, seconds, false); // 짧게 울림
                }
            }

            return Smooth(data, 0.5f); // 부드럽게
        }

        private static float[] Wind(System.Random random) // 바람 (반복)
        {
            const float seconds = 10f; // 길이
            float[] data = new float[(int)(seconds * SampleRate)]; // 버퍼
            float low = 0f; // 필터
            float lower = 0f; // 필터

            for (int i = 0; i < data.Length; i++) // 샘플
            {
                float t = i / (float)SampleRate; // 시간
                float gust = 0.55f + (0.3f * Mathf.Sin(2f * Mathf.PI * t / seconds)) + (0.15f * Mathf.Sin(2f * Mathf.PI * 3f * t / seconds)); // 반복 주기에 맞춘 돌풍
                float cutoff = 0.01f + (0.03f * gust); // 돌풍에 따라 밝아짐
                low += cutoff * ((float)(random.NextDouble() * 2d - 1d) - low); // 저역
                lower += 0.5f * (low - lower); // 한 번 더
                data[i] = lower * 1.6f * gust; // 출력
            }

            return MakeLoop(data, 1.2f); // 이음새 처리
        }

        private static float[] Factory(System.Random random) // 공장 기계음 (반복)
        {
            const float seconds = 4f; // 길이
            float[] data = new float[(int)(seconds * SampleRate)]; // 버퍼
            float low = 0f; // 필터

            for (int i = 0; i < data.Length; i++) // 샘플
            {
                float t = i / (float)SampleRate; // 시간
                low += 0.02f * ((float)(random.NextDouble() * 2d - 1d) - low); // 증기 잡음
                float hum = (Mathf.Sin(2f * Mathf.PI * 50f * t) * 0.35f) + (Mathf.Sin(2f * Mathf.PI * 100f * t) * 0.15f); // 기계 울림 (주기 정수배)
                float beat = t % 1f; // 1초마다
                float piston = Mathf.Sin(2f * Mathf.PI * 70f * beat) * Mathf.Exp(-beat * 9f) * 0.5f; // 피스톤 쿵
                data[i] = ((hum * 0.4f) + (low * 1.2f) + piston) * 0.5f; // 합성
            }

            return MakeLoop(data, 0.3f); // 이음새 처리
        }

        private static float[] Drone(System.Random random) // 지하 울림 (반복)
        {
            const float seconds = 8f; // 길이
            float[] data = new float[(int)(seconds * SampleRate)]; // 버퍼
            float low = 0f; // 필터

            for (int i = 0; i < data.Length; i++) // 샘플
            {
                float t = i / (float)SampleRate; // 시간
                low += 0.004f * ((float)(random.NextDouble() * 2d - 1d) - low); // 아주 낮은 잡음
                float swell = 0.6f + (0.4f * Mathf.Sin(2f * Mathf.PI * t / seconds)); // 느린 부풂
                float tone = (Mathf.Sin(2f * Mathf.PI * 41.25f * t) * 0.5f) + (Mathf.Sin(2f * Mathf.PI * 61.875f * t) * 0.3f); // 낮은 음 두 개 (주기 정수배)
                data[i] = ((tone * 0.3f) + (low * 6f)) * swell * 0.6f; // 합성
            }

            return MakeLoop(data, 1f); // 이음새 처리
        }

        private static void AddTone(float[] data, float start, float duration, float hz, float amplitude, float attack, float release, float loopSeconds, bool pad) // 음 하나 더하기 (반복 길이를 넘으면 앞으로 감음)
        {
            int total = data.Length; // 길이
            int from = Mathf.FloorToInt(start * SampleRate); // 시작 샘플
            int count = Mathf.CeilToInt(duration * SampleRate); // 샘플 수

            for (int k = 0; k < count; k++) // 샘플
            {
                float t = k / (float)SampleRate; // 음 시간
                float envelope = pad ? Mathf.Clamp01(t / attack) * Mathf.Clamp01((duration - t) / release) : Mathf.Clamp01(t / attack) * Mathf.Exp(-t * 3f / release); // 포락선
                float wave = pad ? (Mathf.Sin(2f * Mathf.PI * hz * t) + (0.3f * Mathf.Sin(2f * Mathf.PI * hz * 2.003f * t))) * (0.85f + 0.15f * Mathf.Sin(t * 4f)) : Mathf.Sin(2f * Mathf.PI * hz * t) + (0.25f * Mathf.Sin(2f * Mathf.PI * hz * 3f * t) * Mathf.Exp(-t * 6f)); // 파형
                int index = ((from + k) % total + total) % total; // 반복 위치
                data[index] += wave * envelope * amplitude; // 더하기
            }
        }

        private static float[] MakeLoop(float[] data, float crossfadeSeconds) // 끝부분을 앞부분에 섞어 이음새 제거
        {
            int fade = Mathf.Min(data.Length / 3, (int)(crossfadeSeconds * SampleRate)); // 섞는 길이
            int length = data.Length - fade; // 결과 길이
            float[] result = new float[length]; // 결과
            Array.Copy(data, result, length); // 복사

            for (int i = 0; i < fade; i++) // 앞부분
            {
                float w = i / (float)fade; // 비율
                result[i] = (data[i] * w) + (data[length + i] * (1f - w)); // 끝을 섞음
            }

            return result; // 반환
        }

        private static float[] Mix(float[] a, float[] b, float bDelay) // 두 소리 합치기
        {
            int offset = (int)(bDelay * SampleRate); // 지연
            float[] result = new float[Mathf.Max(a.Length, b.Length + offset)]; // 결과
            Array.Copy(a, result, a.Length); // 첫 소리

            for (int i = 0; i < b.Length; i++) // 두 번째 소리
            {
                result[i + offset] += b[i]; // 더하기
            }

            return result; // 반환
        }

        private static float[] Smooth(float[] data, float amount) // 간단한 저역 필터
        {
            float state = 0f; // 상태

            for (int i = 0; i < data.Length; i++) // 샘플
            {
                state += amount * (data[i] - state); // 필터
                data[i] = state; // 적용
            }

            return data; // 반환
        }

        private static float Attack(float t, float seconds) // 시작 딸깍임 방지
        {
            return Mathf.Clamp01(t / seconds); // 비율
        }

        private static float Fade(float t, float length, float seconds) // 끝 딸깍임 방지
        {
            return Mathf.Clamp01((length - t) / seconds); // 비율
        }

        private static float Midi(int note) // MIDI 번호 → Hz
        {
            return 440f * Mathf.Pow(2f, (note - 69) / 12f); // 변환
        }
    }
}
