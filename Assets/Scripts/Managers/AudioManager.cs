using UnityEngine; // Unity 기본 기능 사용

namespace ProjectI // 프로젝트 공통 네임스페이스
{
    public class AudioManager : MonoBehaviour // 2D와 3D 효과음 재생 관리
    {
        public static AudioManager Instance { get; private set; } // 현재 활성 AudioManager 접근점

        void Awake() // AudioManager 싱글톤 초기화
        {
            if (Instance != null && Instance != this) // 기존 AudioManager 존재 여부 확인
            {
                Destroy(gameObject); // 중복 AudioManager 제거
                return; // 중복 초기화 중단
            }

            Instance = this; // 현재 오브젝트를 전역 인스턴스로 저장
            DontDestroyOnLoad(gameObject); // Scene 전환 후에도 AudioManager 유지
            Debug.Log("[AudioManager] 3D 효과음 시스템 초기화"); // 초기화 완료 출력
        }

        public void Play3D( // 지정된 위치에서 3D 효과음 재생
            AudioClip clip, // 재생할 효과음
            Vector3 position, // 효과음 발생 위치
            float volume = 1f, // 효과음 음량
            float minDistance = 1f, // 최대 음량 유지 거리
            float maxDistance = 20f) // 효과음 최대 도달 거리
        {
            if (clip == null) // 효과음 연결 여부 확인
            {
                return; // 재생 처리 중단
            }

            GameObject sourceObject = new GameObject($"Audio_{clip.name}"); // 임시 오디오 오브젝트 생성
            sourceObject.transform.position = position; // 효과음 발생 위치 적용

            AudioSource source = sourceObject.AddComponent<AudioSource>(); // AudioSource 컴포넌트 추가
            source.clip = clip; // 재생할 효과음 연결
            source.volume = Mathf.Clamp01(volume); // 효과음 음량 제한
            source.playOnAwake = false; // 생성 직후 자동 재생 방지
            source.loop = false; // 반복 재생 비활성화
            source.spatialBlend = 1f; // 완전한 3D 위치 기반 재생 적용
            source.rolloffMode = AudioRolloffMode.Logarithmic; // 자연스러운 거리 감쇠 적용
            source.minDistance = Mathf.Max(0.1f, minDistance); // 최소 거리 안전값 적용
            source.maxDistance = Mathf.Max(source.minDistance, maxDistance); // 최대 거리 안전값 적용
            source.dopplerLevel = 0f; // 임시 효과음 도플러 효과 비활성화
            source.Play(); // 효과음 재생 시작

            Destroy(sourceObject, clip.length + 0.2f); // 재생 완료 후 임시 오브젝트 제거
        }

        public void PlayTone3D( // 효과음이 없을 때 임시 3D 전자음 생성
            float frequency, // 전자음 주파수
            float duration, // 전자음 재생시간
            Vector3 position, // 전자음 발생 위치
            float volume = 0.35f, // 전자음 음량
            float maxDistance = 20f) // 전자음 최대 도달 거리
        {
            float safeFrequency = Mathf.Clamp(frequency, 40f, 2000f); // 주파수 안전 범위 적용
            float safeDuration = Mathf.Clamp(duration, 0.05f, 2f); // 재생시간 안전 범위 적용
            const int sampleRate = 44100; // 임시 AudioClip 초당 샘플 수
            int sampleCount = Mathf.CeilToInt(sampleRate * safeDuration); // 전체 오디오 샘플 수 계산
            float[] samples = new float[sampleCount]; // 오디오 샘플 배열 생성

            for (int i = 0; i < sampleCount; i++) // 모든 오디오 샘플 생성
            {
                float time = (float)i / sampleRate; // 현재 샘플 재생 시각 계산
                float progress = (float)i / sampleCount; // 전체 재생 진행률 계산
                float envelope = 1f - progress; // 끝부분으로 갈수록 작아지는 음량 계산
                samples[i] = Mathf.Sin(2f * Mathf.PI * safeFrequency * time) * envelope * 0.5f; // 사인파 전자음 샘플 생성
            }

            AudioClip generatedClip = AudioClip.Create( // 임시 AudioClip 생성
                $"Tone_{safeFrequency:F0}", // 임시 AudioClip 이름
                sampleCount, // 전체 샘플 수
                1, // 모노 채널 사용
                sampleRate, // 초당 샘플 수
                false); // 실시간 스트리밍 비활성화

            generatedClip.SetData(samples, 0); // 생성된 샘플을 AudioClip에 적용
            Play3D(generatedClip, position, volume, 1f, maxDistance); // 생성한 전자음을 3D 위치에서 재생
            Destroy(generatedClip, safeDuration + 0.3f); // 재생 완료 후 임시 AudioClip 제거
        }
    }
}