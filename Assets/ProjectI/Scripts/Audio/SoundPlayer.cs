using System.Collections.Generic; // 재생기 목록
using ProjectI.Settings; // 음량 설정
using UnityEngine; // 유니티 기본 기능 참조

namespace ProjectI.Audio // 소리 네임스페이스
{
    public static class SoundPlayer // 효과음 재생 (재생기 재사용 · 분류별 음량)
    {
        private const int MaxSources = 24; // 동시 재생 최대
        private static readonly List<AudioSource> Sources = new List<AudioSource>(); // 재생기
        private static Transform host; // 재생기 묶음
        private static int nextSteal; // 가득 찼을 때 다시 쓸 재생기

        public static int PlayCount { get; private set; } // 재생 요청 수 (검증용)
        public static SoundId LastPlayed { get; private set; } // 마지막 소리 (검증용)

        public static float CategoryVolume(SoundCategory category) // 분류 음량 (전체 음량은 AudioListener 가 곱함)
        {
            switch (category)
            {
                case SoundCategory.Music: return GameSettings.MusicVolume;
                case SoundCategory.Ambience: return GameSettings.AmbienceVolume;
                default: return GameSettings.SfxVolume;
            }
        }

        public static void Play(SoundId id, float volume = 1f, float pitch = 1f) // 화면 소리 (위치 없음)
        {
            AudioSource source = Acquire(); // 재생기

            if (source == null) // 편집 모드 등
            {
                return; // 생략
            }

            source.transform.localPosition = Vector3.zero; // 위치 무관
            source.spatialBlend = 0f; // 2D
            Start(source, id, volume, pitch); // 재생
        }

        public static void PlayAt(SoundId id, Vector3 position, float volume = 1f, float pitchJitter = 0.06f, float maxDistance = 28f) // 위치 소리
        {
            AudioSource source = Acquire(); // 재생기

            if (source == null) // 편집 모드 등
            {
                return; // 생략
            }

            source.transform.position = position; // 위치
            source.spatialBlend = 1f; // 3D
            source.rolloffMode = AudioRolloffMode.Linear; // 거리 감쇠 (선형: 멀면 확실히 안 들림)
            source.minDistance = 1.5f; // 가까운 거리
            source.maxDistance = maxDistance; // 먼 거리
            float pitch = 1f + Random.Range(-pitchJitter, pitchJitter); // 음높이 변형
            Start(source, id, volume, pitch); // 재생
        }

        private static void Start(AudioSource source, SoundId id, float volume, float pitch) // 공통 재생
        {
            int variant = Random.Range(0, ProceduralSounds.VariantCount(id)); // 변형
            source.clip = ProceduralSounds.Get(id, variant); // 소리
            source.pitch = pitch; // 음높이
            source.loop = false; // 한 번
            source.volume = volume * CategoryVolume(SoundCategory.Sfx); // 음량
            source.Play(); // 재생
            PlayCount++; // 기록
            LastPlayed = id; // 기록
        }

        private static AudioSource Acquire() // 쉬고 있는 재생기
        {
            if (!Application.isPlaying) // 편집 모드 (검증 도구 등)
            {
                return null; // 재생 안 함
            }

            if (host == null) // 처음 또는 파괴됨
            {
                GameObject root = new GameObject("===ProjectI Audio==="); // 묶음
                Object.DontDestroyOnLoad(root); // 씬 전환 유지
                host = root.transform; // 저장
                Sources.Clear(); // 이전 목록 정리
            }

            foreach (AudioSource source in Sources) // 쉬는 재생기
            {
                if (source != null && !source.isPlaying) // 쉼
                {
                    return source; // 반환
                }
            }

            if (Sources.Count < MaxSources) // 새로 만들기
            {
                GameObject child = new GameObject("Sfx"); // 재생기
                child.transform.SetParent(host, false); // 부모
                AudioSource created = child.AddComponent<AudioSource>(); // 소리
                created.playOnAwake = false; // 자동 재생 안 함
                created.dopplerLevel = 0f; // 도플러 없음
                Sources.Add(created); // 등록
                return created; // 반환
            }

            nextSteal = (nextSteal + 1) % Sources.Count; // 가장 오래된 순서대로 다시 사용
            return Sources[nextSteal]; // 반환
        }
    }
}
