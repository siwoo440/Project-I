using System.Collections.Generic; // AudioSource 풀과 전자음 캐시 사용
using UnityEngine; // Unity 기본 기능 사용

namespace ProjectI // 프로젝트 공통 네임스페이스
{
    public class AudioManager : MonoBehaviour // 2D와 3D 효과음 재생 및 풀링 관리
    {
        public static AudioManager Instance { get; private set; } // 현재 활성 AudioManager 접근점

        [Header("AudioSource 풀")] // Inspector 오디오 풀 설정 구분
        [SerializeField][Min(1)] int initialPoolSize = 12; // 시작할 때 미리 생성할 AudioSource 수
        [SerializeField][Min(1)] int maximumPoolSize = 32; // 동시에 사용할 수 있는 최대 AudioSource 수

        readonly Queue<AudioSource> availableSources = new Queue<AudioSource>(); // 재사용 대기 중인 AudioSource 목록
        readonly List<AudioSource> activeSources = new List<AudioSource>(); // 현재 재생 중인 AudioSource 목록
        readonly Dictionary<int, AudioClip> cachedToneClips = new Dictionary<int, AudioClip>(); // 임시 전자음 AudioClip 캐시

        int createdSourceCount; // 현재까지 생성된 AudioSource 수

        public int ActiveSourceCount => activeSources.Count; // 현재 재생 중인 AudioSource 수
        public int AvailableSourceCount => availableSources.Count; // 현재 재사용 가능한 AudioSource 수

        void Awake() // AudioManager 싱글톤과 AudioSource 풀 초기화
        {
            if (Instance != null && Instance != this) // 기존 AudioManager 존재 여부 확인
            {
                Destroy(gameObject); // 중복 AudioManager 제거
                return; // 중복 초기화 중단
            }

            Instance = this; // 현재 오브젝트를 전역 인스턴스로 저장
            transform.SetParent(null); // DontDestroyOnLoad 적용을 위해 루트 오브젝트로 분리
            DontDestroyOnLoad(gameObject); // Scene 전환 후에도 AudioManager 유지

            int safeMaximum = Mathf.Max(1, maximumPoolSize); // 최대 풀 크기 안전값 계산
            int safeInitial = Mathf.Clamp(initialPoolSize, 1, safeMaximum); // 초기 풀 크기를 최대값 안으로 제한

            for (int i = 0; i < safeInitial; i++) // 초기 AudioSource 수만큼 반복
            {
                AudioSource source = CreatePooledSource(); // 새로운 AudioSource 생성
                availableSources.Enqueue(source); // 재사용 대기열에 추가
            }

            Debug.Log($"[AudioManager] AudioSource 풀 초기화 — {safeInitial}개"); // 풀 초기화 결과 출력
        }

        void Update() // 재생이 끝난 AudioSource를 풀로 반환
        {
            for (int i = activeSources.Count - 1; i >= 0; i--) // 활성 AudioSource 목록을 역순으로 순회
            {
                AudioSource source = activeSources[i]; // 현재 AudioSource 가져오기

                if (source == null) // AudioSource가 외부에서 제거되었는지 확인
                {
                    activeSources.RemoveAt(i); // 유효하지 않은 참조 제거
                    continue; // 다음 AudioSource 검사
                }

                if (!source.isPlaying) // 효과음 재생 종료 여부 확인
                {
                    activeSources.RemoveAt(i); // 활성 목록에서 제거
                    ReleaseSource(source); // AudioSource를 대기열로 반환
                }
            }
        }

        void OnDestroy() // AudioManager와 생성 전자음 정리
        {
            if (Instance != this) // 현재 오브젝트가 등록된 인스턴스가 아닌지 확인
            {
                return; // 중복 오브젝트의 정리 중단
            }

            foreach (AudioClip clip in cachedToneClips.Values) // 생성된 임시 전자음 목록 순회
            {
                if (clip != null) // AudioClip 존재 여부 확인
                {
                    Destroy(clip); // 런타임 생성 AudioClip 제거
                }
            }

            cachedToneClips.Clear(); // 전자음 캐시 초기화
            Instance = null; // 전역 인스턴스 참조 초기화
        }

        public void Play3D( // 지정된 위치에서 풀링된 3D 효과음 재생
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

            AudioSource source = GetSource(); // 재사용할 AudioSource 가져오기

            if (source == null) // 최대 동시 재생 개수 도달 여부 확인
            {
                return; // 추가 효과음 재생 중단
            }

            source.transform.position = position; // 효과음 발생 위치 적용
            source.clip = clip; // 재생할 효과음 연결
            source.volume = Mathf.Clamp01(volume); // 효과음 음량 제한
            source.playOnAwake = false; // 자동 재생 비활성화
            source.loop = false; // 반복 재생 비활성화
            source.spatialBlend = 1f; // 완전한 3D 위치 기반 재생 적용
            source.rolloffMode = AudioRolloffMode.Logarithmic; // 자연스러운 거리 감쇠 적용
            source.minDistance = Mathf.Max(0.1f, minDistance); // 최소 거리 안전값 적용
            source.maxDistance = Mathf.Max(source.minDistance, maxDistance); // 최대 거리 안전값 적용
            source.dopplerLevel = 0f; // 도플러 효과 비활성화
            source.gameObject.SetActive(true); // 재사용 AudioSource 활성화
            source.Play(); // 효과음 재생 시작
            activeSources.Add(source); // 활성 AudioSource 목록에 추가
        }

        public void PlayTone3D( // 효과음이 없을 때 캐시된 임시 3D 전자음 재생
            float frequency, // 전자음 주파수
            float duration, // 전자음 재생시간
            Vector3 position, // 전자음 발생 위치
            float volume = 0.35f, // 전자음 음량
            float maxDistance = 20f) // 전자음 최대 도달 거리
        {
            float safeFrequency = Mathf.Clamp(frequency, 40f, 2000f); // 주파수 안전 범위 적용
            float safeDuration = Mathf.Clamp(duration, 0.05f, 2f); // 재생시간 안전 범위 적용
            AudioClip toneClip = GetOrCreateToneClip(safeFrequency, safeDuration); // 동일한 전자음 캐시 가져오기
            Play3D(toneClip, position, volume, 1f, maxDistance); // 캐시된 전자음을 3D 위치에서 재생
        }

        AudioSource GetSource() // 재사용할 AudioSource를 가져오거나 새로 생성
        {
            while (availableSources.Count > 0) // 대기 AudioSource가 있는 동안 반복
            {
                AudioSource source = availableSources.Dequeue(); // 대기열의 AudioSource 가져오기

                if (source != null) // AudioSource가 외부에서 제거되지 않았는지 확인
                {
                    return source; // 재사용할 AudioSource 반환
                }
            }

            int safeMaximum = Mathf.Max(1, maximumPoolSize); // 최대 풀 크기 안전값 계산

            if (createdSourceCount >= safeMaximum) // 최대 AudioSource 수 도달 여부 확인
            {
                return null; // 추가 생성 없이 null 반환
            }

            return CreatePooledSource(); // 새로운 AudioSource 생성 후 반환
        }

        AudioSource CreatePooledSource() // 풀에서 사용할 AudioSource 생성
        {
            createdSourceCount++; // 생성된 AudioSource 수 증가

            GameObject sourceObject = new GameObject($"PooledAudio_{createdSourceCount}"); // Hierarchy 확인용 오디오 오브젝트 생성
            sourceObject.transform.SetParent(transform); // AudioManager 자식으로 연결
            sourceObject.transform.localPosition = Vector3.zero; // 로컬 위치 초기화
            sourceObject.transform.localRotation = Quaternion.identity; // 로컬 회전 초기화
            sourceObject.transform.localScale = Vector3.one; // 로컬 크기 초기화

            AudioSource source = sourceObject.AddComponent<AudioSource>(); // AudioSource 컴포넌트 추가
            source.playOnAwake = false; // 자동 재생 비활성화
            source.loop = false; // 반복 재생 비활성화
            sourceObject.SetActive(false); // 대기 상태로 비활성화

            return source; // 생성된 AudioSource 반환
        }

        void ReleaseSource(AudioSource source) // 재생이 끝난 AudioSource를 풀로 반환
        {
            source.Stop(); // 남은 효과음 재생 중지
            source.clip = null; // 기존 AudioClip 참조 해제
            source.transform.localPosition = Vector3.zero; // 대기 위치 초기화
            source.gameObject.SetActive(false); // AudioSource 오브젝트 비활성화
            availableSources.Enqueue(source); // 재사용 대기열에 추가
        }

        AudioClip GetOrCreateToneClip(float frequency, float duration) // 동일한 임시 전자음을 캐시에서 반환
        {
            int roundedFrequency = Mathf.RoundToInt(frequency); // 주파수를 정수 단위로 변환
            int durationMilliseconds = Mathf.RoundToInt(duration * 1000f); // 재생시간을 밀리초로 변환
            int cacheKey = roundedFrequency * 10000 + durationMilliseconds; // 주파수와 길이를 조합한 캐시 키 생성

            if (cachedToneClips.TryGetValue(cacheKey, out AudioClip cachedClip)) // 동일한 전자음 캐시 존재 여부 확인
            {
                return cachedClip; // 기존 AudioClip 반환
            }

            const int sampleRate = 44100; // 임시 AudioClip 초당 샘플 수
            int sampleCount = Mathf.CeilToInt(sampleRate * duration); // 전체 샘플 수 계산
            float[] samples = new float[sampleCount]; // 오디오 샘플 배열 생성

            for (int i = 0; i < sampleCount; i++) // 모든 오디오 샘플 생성
            {
                float time = (float)i / sampleRate; // 현재 샘플 재생 시각 계산
                float progress = (float)i / sampleCount; // 전체 재생 진행률 계산
                float envelope = 1f - progress; // 끝부분으로 갈수록 작아지는 음량 계산
                samples[i] = Mathf.Sin(2f * Mathf.PI * frequency * time) * envelope * 0.5f; // 사인파 전자음 생성
            }

            AudioClip createdClip = AudioClip.Create( // 런타임 AudioClip 생성
                $"Tone_{roundedFrequency}_{durationMilliseconds}", // 캐시 확인용 AudioClip 이름
                sampleCount, // 전체 샘플 수
                1, // 모노 채널 사용
                sampleRate, // 초당 샘플 수
                false); // 스트리밍 비활성화

            createdClip.SetData(samples, 0); // 생성된 샘플을 AudioClip에 적용
            cachedToneClips.Add(cacheKey, createdClip); // 전자음 캐시에 추가

            return createdClip; // 생성한 AudioClip 반환
        }
    }
}