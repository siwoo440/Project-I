using UnityEngine; // Unity 기본 기능과 AudioSource 사용
using UnityEngine.Audio; // AudioMixer와 AudioMixerGroup 사용

namespace ProjectI // 프로젝트 공통 네임스페이스
{
    public class GameAudioManager : MonoBehaviour // BGM과 효과음 및 환경음의 전역 재생과 음량 관리
    {
        public static GameAudioManager Instance { get; private set; } // 현재 활성 오디오 관리자 접근점

        [Header("AudioMixer 연결")] // Inspector AudioMixer 연결 구분
        [Tooltip("전체 오디오 그룹을 관리할 AudioMixer")] [SerializeField] AudioMixer audioMixer; // 전체 오디오 그룹을 관리할 AudioMixer
        [Tooltip("BGM AudioSource 출력 그룹")] [SerializeField] AudioMixerGroup bgmOutputGroup; // BGM AudioSource 출력 그룹
        [Tooltip("SFX AudioSource 출력 그룹")] [SerializeField] AudioMixerGroup sfxOutputGroup; // SFX AudioSource 출력 그룹
        [Tooltip("Ambient AudioSource 출력 그룹")] [SerializeField] AudioMixerGroup ambientOutputGroup; // Ambient AudioSource 출력 그룹

        [Header("전역 AudioSource 연결")] // Inspector AudioSource 연결 구분
        [Tooltip("배경음 반복 재생 AudioSource")] [SerializeField] AudioSource bgmSource; // 배경음 반복 재생 AudioSource
        [Tooltip("환경음 반복 재생 AudioSource")] [SerializeField] AudioSource ambientSource; // 환경음 반복 재생 AudioSource
        [Tooltip("2D 공통 효과음 재생 AudioSource")] [SerializeField] AudioSource sfxSource; // 2D 공통 효과음 재생 AudioSource

        [Header("노출된 Parameter 이름")] // Inspector AudioMixer Parameter 이름 구분
        [Tooltip("Master 볼륨 Parameter 이름")] [SerializeField] string masterVolumeParameter = "MasterVolume"; // Master 볼륨 Parameter 이름
        [Tooltip("BGM 볼륨 Parameter 이름")] [SerializeField] string bgmVolumeParameter = "BGMVolume"; // BGM 볼륨 Parameter 이름
        [Tooltip("SFX 볼륨 Parameter 이름")] [SerializeField] string sfxVolumeParameter = "SFXVolume"; // SFX 볼륨 Parameter 이름
        [Tooltip("Ambient 볼륨 Parameter 이름")] [SerializeField] string ambientVolumeParameter = "AmbientVolume"; // Ambient 볼륨 Parameter 이름

        void Awake() // 오디오 관리자 싱글톤과 AudioSource 초기 설정
        {
            if (Instance != null && Instance != this) // 기존 오디오 관리자 존재 여부 확인
            {
                Destroy(gameObject); // Scene 전환으로 생성된 중복 오디오 관리자 제거
                return; // 중복 초기화 중단
            }

            Instance = this; // 현재 오브젝트를 전역 오디오 관리자로 등록
            transform.SetParent(null); // 영구 오브젝트 적용을 위해 루트로 분리
            DontDestroyOnLoad(gameObject); // Scene 전환 후에도 오디오 관리자 유지

            ConfigureAudioSources(); // 각 AudioSource의 출력 그룹과 기본 상태 설정
            ApplyInitialVolumes(); // 현재 저장 설정 또는 기본 음량 적용
        }

        void OnDestroy() // 오디오 관리자 싱글톤 참조 정리
        {
            if (Instance == this) // 현재 오브젝트가 등록된 오디오 관리자인지 확인
            {
                Instance = null; // 전역 오디오 관리자 참조 초기화
            }
        }

        void ConfigureAudioSources() // 전역 AudioSource의 출력 그룹과 공간 설정 적용
        {
            if (bgmSource != null) // BGM AudioSource 존재 여부 확인
            {
                bgmSource.outputAudioMixerGroup = bgmOutputGroup; // BGM 그룹으로 출력 연결
                bgmSource.playOnAwake = false; // 관리자 호출 전 자동 재생 방지
                bgmSource.loop = true; // 배경음 반복 재생 설정
                bgmSource.spatialBlend = 0f; // 화면 위치와 무관한 2D 사운드 설정
            }

            if (ambientSource != null) // Ambient AudioSource 존재 여부 확인
            {
                ambientSource.outputAudioMixerGroup = ambientOutputGroup; // Ambient 그룹으로 출력 연결
                ambientSource.playOnAwake = false; // 관리자 호출 전 자동 재생 방지
                ambientSource.loop = true; // 환경음 반복 재생 설정
                ambientSource.spatialBlend = 0f; // 전체 환경음용 2D 사운드 설정
            }

            if (sfxSource != null) // SFX AudioSource 존재 여부 확인
            {
                sfxSource.outputAudioMixerGroup = sfxOutputGroup; // SFX 그룹으로 출력 연결
                sfxSource.playOnAwake = false; // 자동 효과음 재생 방지
                sfxSource.loop = false; // 효과음 반복 재생 해제
                sfxSource.spatialBlend = 0f; // UI와 공통 효과음용 2D 사운드 설정
            }
        }

        void ApplyInitialVolumes() // 설정 관리자 또는 기본값으로 AudioMixer 음량 초기화
        {
            GameSettingsManager settingsManager = GameSettingsManager.Instance; // 현재 설정 관리자 가져오기

            if (settingsManager != null) // 설정 관리자 존재 여부 확인
            {
                ApplyVolumes( // 저장된 설정 음량 적용
                    settingsManager.MasterVolume, // 저장된 전체 음량 전달
                    settingsManager.BgmVolume, // 저장된 배경음 음량 전달
                    settingsManager.SfxVolume, // 저장된 효과음 음량 전달
                    settingsManager.AmbientVolume); // 저장된 환경음 음량 전달

                return; // 기본 음량 적용 중단
            }

            ApplyVolumes(0.8f, 0.7f, 0.8f, 0.7f); // 설정 관리자가 없으면 기본 음량 적용
        }

        public void ApplyVolumes(float master, float bgm, float sfx, float ambient) // 네 AudioMixer 그룹의 음량 적용
        {
            if (audioMixer == null) // AudioMixer 연결 여부 확인
            {
                Debug.LogError("[Audio] AudioMixer가 연결되지 않았습니다."); // AudioMixer 누락 오류 출력
                return; // 음량 적용 중단
            }

            SetMixerVolume(masterVolumeParameter, master); // Master 그룹 음량 적용
            SetMixerVolume(bgmVolumeParameter, bgm); // BGM 그룹 음량 적용
            SetMixerVolume(sfxVolumeParameter, sfx); // SFX 그룹 음량 적용
            SetMixerVolume(ambientVolumeParameter, ambient); // Ambient 그룹 음량 적용
        }

        void SetMixerVolume(string parameterName, float linearVolume) // 0에서 1 사이의 음량을 데시벨로 변환해 적용
        {
            float safeVolume = Mathf.Clamp01(linearVolume); // 전달받은 음량을 0에서 1 사이로 제한
            float decibel = safeVolume <= 0.0001f ? -80f : Mathf.Log10(safeVolume) * 20f; // 선형 음량을 AudioMixer 데시벨로 변환

            if (!audioMixer.SetFloat(parameterName, decibel)) // 지정한 Exposed Parameter에 데시벨 적용 시도
            {
                Debug.LogWarning($"[Audio] AudioMixer Parameter를 찾을 수 없습니다: {parameterName}"); // Parameter 이름 오류 출력
            }
        }

        public void PlayBgm(AudioClip clip, bool loop = true) // 새로운 배경음 재생
        {
            if (bgmSource == null || clip == null) // BGM AudioSource와 AudioClip 존재 여부 확인
            {
                return; // 배경음 재생 중단
            }

            if (bgmSource.clip == clip && bgmSource.isPlaying) // 같은 배경음이 이미 재생 중인지 확인
            {
                return; // 같은 배경음 중복 재생 방지
            }

            bgmSource.Stop(); // 기존 배경음 정지
            bgmSource.clip = clip; // 새로운 배경음 연결
            bgmSource.loop = loop; // 전달받은 반복 재생 여부 적용
            bgmSource.Play(); // 새로운 배경음 재생
        }

        public void StopBgm() // 현재 배경음 정지
        {
            if (bgmSource == null) // BGM AudioSource 존재 여부 확인
            {
                return; // 배경음 정지 중단
            }

            bgmSource.Stop(); // 현재 배경음 재생 정지
            bgmSource.clip = null; // 기존 배경음 참조 제거
        }

        public void PlayAmbient(AudioClip clip, bool loop = true) // 새로운 전체 환경음 재생
        {
            if (ambientSource == null || clip == null) // Ambient AudioSource와 AudioClip 존재 여부 확인
            {
                return; // 환경음 재생 중단
            }

            if (ambientSource.clip == clip && ambientSource.isPlaying) // 같은 환경음이 이미 재생 중인지 확인
            {
                return; // 같은 환경음 중복 재생 방지
            }

            ambientSource.Stop(); // 기존 환경음 정지
            ambientSource.clip = clip; // 새로운 환경음 연결
            ambientSource.loop = loop; // 전달받은 반복 재생 여부 적용
            ambientSource.Play(); // 새로운 환경음 재생
        }

        public void StopAmbient() // 현재 전체 환경음 정지
        {
            if (ambientSource == null) // Ambient AudioSource 존재 여부 확인
            {
                return; // 환경음 정지 중단
            }

            ambientSource.Stop(); // 현재 환경음 재생 정지
            ambientSource.clip = null; // 기존 환경음 참조 제거
        }

        public void PlaySfx(AudioClip clip, float volumeScale = 1f) // 위치와 무관한 공통 효과음 한 번 재생
        {
            if (sfxSource == null || clip == null) // SFX AudioSource와 AudioClip 존재 여부 확인
            {
                return; // 효과음 재생 중단
            }

            float safeVolumeScale = Mathf.Clamp01(volumeScale); // 개별 효과음 음량을 0에서 1 사이로 제한
            sfxSource.PlayOneShot(clip, safeVolumeScale); // 기존 효과음을 끊지 않고 새 효과음 재생
        }

        public void StopAllAudio() // Scene 종료 또는 테스트용 전체 오디오 정지
        {
            if (bgmSource != null) // BGM AudioSource 존재 여부 확인
            {
                bgmSource.Stop(); // 현재 배경음 정지
            }

            if (ambientSource != null) // Ambient AudioSource 존재 여부 확인
            {
                ambientSource.Stop(); // 현재 환경음 정지
            }

            if (sfxSource != null) // SFX AudioSource 존재 여부 확인
            {
                sfxSource.Stop(); // 현재 공통 효과음 정지
            }
        }
    }
}