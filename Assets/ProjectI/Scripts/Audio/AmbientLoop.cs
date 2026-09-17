using ProjectI.Settings; // 음량 설정
using UnityEngine; // 유니티 기본 기능 참조

namespace ProjectI.Audio // 소리 네임스페이스
{
    [DisallowMultipleComponent] // 한 오브젝트에 하나
    public sealed class AmbientLoop : MonoBehaviour // 반복 음악·환경음 (서서히 커짐, 설정 음량 반영)
    {
        [SerializeField] private SoundId sound = SoundId.WindLoop; // 소리
        [SerializeField] private SoundCategory category = SoundCategory.Ambience; // 음량 분류
        [SerializeField] private float volume = 0.5f; // 기본 음량
        [SerializeField] private bool spatial; // 위치 소리 여부
        [SerializeField] private float maxDistance = 60f; // 위치 소리 최대 거리
        [SerializeField] private float fadeSeconds = 2f; // 서서히 커지는 시간
        private AudioSource source; // 재생기
        private float fade; // 0~1

        public SoundId Sound => sound; // 검증용
        public bool IsPlaying => source != null && source.isPlaying; // 검증용

        public void Configure(SoundId loopSound, SoundCategory loopCategory, float loopVolume, bool isSpatial, float spatialMaxDistance = 60f) // 설정 (추가 직후 호출)
        {
            sound = loopSound; // 소리
            category = loopCategory; // 분류
            volume = loopVolume; // 음량
            spatial = isSpatial; // 위치
            maxDistance = spatialMaxDistance; // 거리
            Restart(); // 반영
        }

        private void OnEnable() // 시작
        {
            GameSettings.Changed += ApplyVolume; // 설정 변경
            Restart(); // 재생
        }

        private void OnDisable() // 정지
        {
            GameSettings.Changed -= ApplyVolume; // 해제

            if (source != null) // 재생기
            {
                source.Stop(); // 정지
            }
        }

        private void Update() // 서서히 커짐
        {
            if (fade >= 1f || source == null) // 완료
            {
                return; // 생략
            }

            fade = Mathf.Min(1f, fade + (Time.unscaledDeltaTime / Mathf.Max(0.01f, fadeSeconds))); // 진행
            ApplyVolume(); // 반영
        }

        private void Restart() // 재생기 준비
        {
            if (!Application.isPlaying || !isActiveAndEnabled) // 편집 모드
            {
                return; // 생략
            }

            if (source == null) // 처음
            {
                source = gameObject.AddComponent<AudioSource>(); // 재생기
                source.playOnAwake = false; // 자동 재생 안 함
                source.dopplerLevel = 0f; // 도플러 없음
            }

            source.clip = ProceduralSounds.Get(sound); // 소리
            source.loop = true; // 반복
            source.spatialBlend = spatial ? 1f : 0f; // 위치
            source.rolloffMode = AudioRolloffMode.Linear; // 선형 감쇠
            source.minDistance = 3f; // 가까운 거리
            source.maxDistance = maxDistance; // 먼 거리
            source.time = Random.Range(0f, source.clip.length * 0.9f); // 여러 곳이 같은 박자로 겹치지 않게
            fade = 0f; // 처음부터 커짐
            ApplyVolume(); // 음량
            source.Play(); // 재생
        }

        private void ApplyVolume() // 음량 반영
        {
            if (source != null) // 재생기
            {
                source.volume = volume * fade * SoundPlayer.CategoryVolume(category); // 음량
            }
        }
    }
}
