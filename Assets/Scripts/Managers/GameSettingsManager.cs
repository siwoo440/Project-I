using System.Collections.Generic; // 사용 가능한 해상도 목록 사용
using UnityEngine; // Unity 기본 기능과 PlayerPrefs 사용

namespace ProjectI // 프로젝트 공통 네임스페이스
{
    public class GameSettingsManager : MonoBehaviour // 화면과 음량 및 마우스 설정 관리
    {
        public static GameSettingsManager Instance { get; private set; } // 현재 활성 설정 관리자 접근점

        const string MouseSensitivityKey = "Settings.MouseSensitivity"; // 마우스 감도 저장 키
        const string MasterVolumeKey = "Settings.MasterVolume"; // 전체 음량 저장 키
        const string BgmVolumeKey = "Settings.BGMVolume"; // 배경음 음량 저장 키
        const string SfxVolumeKey = "Settings.SFXVolume"; // 효과음 음량 저장 키
        const string AmbientVolumeKey = "Settings.AmbientVolume"; // 환경음 음량 저장 키

        const string FullScreenKey = "Settings.FullScreen"; // 전체 화면 저장 키
        const string VSyncKey = "Settings.VSync"; // 수직 동기화 저장 키
        const string ResolutionWidthKey = "Settings.ResolutionWidth"; // 해상도 너비 저장 키
        const string ResolutionHeightKey = "Settings.ResolutionHeight"; // 해상도 높이 저장 키

        const float DefaultMouseSensitivity = 1f; // 기본 마우스 감도 배율

        const float DefaultMasterVolume = 0.8f; // 기본 전체 음량
        const float DefaultBgmVolume = 0.7f; // 기본 배경음 음량
        const float DefaultSfxVolume = 0.8f; // 기본 효과음 음량
        const float DefaultAmbientVolume = 0.7f; // 기본 환경음 음량

        const int DefaultResolutionWidth = 1920; // 기본 해상도 너비
        const int DefaultResolutionHeight = 1080; // 기본 해상도 높이

        readonly List<Vector2Int> availableResolutions = new List<Vector2Int>(); // 중복을 제거한 사용 가능 해상도 목록

        float mouseSensitivityMultiplier = DefaultMouseSensitivity; // 현재 마우스 감도 배율

        float masterVolume = DefaultMasterVolume; // 현재 전체 음량
        float bgmVolume = DefaultBgmVolume; // 현재 배경음 음량
        float sfxVolume = DefaultSfxVolume; // 현재 효과음 음량
        float ambientVolume = DefaultAmbientVolume; // 현재 환경음 음량

        bool fullScreen = true; // 현재 전체 화면 여부
        bool vSync = true; // 현재 수직 동기화 여부
        int resolutionWidth = DefaultResolutionWidth; // 현재 해상도 너비
        int resolutionHeight = DefaultResolutionHeight; // 현재 해상도 높이

        public float MouseSensitivityMultiplier => mouseSensitivityMultiplier; // 현재 마우스 감도 배율 반환
        public float MasterVolume => masterVolume; // 현재 전체 음량 반환
        public float BgmVolume => bgmVolume; // 현재 배경음 음량 반환
        public float SfxVolume => sfxVolume; // 현재 효과음 음량 반환
        public float AmbientVolume => ambientVolume; // 현재 환경음 음량 반환
        public bool FullScreen => fullScreen; // 현재 전체 화면 여부 반환
        public bool VSync => vSync; // 현재 수직 동기화 여부 반환
        public int ResolutionWidth => resolutionWidth; // 현재 해상도 너비 반환
        public int ResolutionHeight => resolutionHeight; // 현재 해상도 높이 반환
        public int ResolutionCount => availableResolutions.Count; // 사용 가능한 해상도 개수 반환

        void Awake() // 설정 관리자 싱글톤과 저장 설정 불러오기
        {
            if (Instance != null && Instance != this) // 기존 설정 관리자 존재 여부 확인
            {
                Destroy(gameObject); // Scene 전환으로 생성된 중복 설정 관리자 제거
                return; // 중복 초기화 중단
            }

            Instance = this; // 현재 오브젝트를 전역 설정 관리자로 등록
            transform.SetParent(null); // 영구 오브젝트 적용을 위해 루트로 분리
            DontDestroyOnLoad(gameObject); // Scene 전환 후에도 설정 관리자 유지

            BuildResolutionList(); // 현재 모니터에서 사용할 수 있는 해상도 목록 생성
            LoadSettings(); // PlayerPrefs에서 기존 설정 불러오기
            ApplyCurrentSettings(); // 불러온 설정을 실제 게임에 적용
        }

        void OnDestroy() // 설정 관리자 싱글톤 참조 정리
        {
            if (Instance == this) // 현재 오브젝트가 등록된 설정 관리자인지 확인
            {
                Instance = null; // 전역 설정 관리자 참조 초기화
            }
        }

        void BuildResolutionList() // 중복을 제거한 지원 해상도 목록 생성
        {
            availableResolutions.Clear(); // 기존 해상도 목록 초기화
            Resolution[] resolutions = Screen.resolutions; // 현재 모니터가 지원하는 해상도 목록 가져오기

            foreach (Resolution resolution in resolutions) // 모든 지원 해상도 순회
            {
                Vector2Int size = new Vector2Int(resolution.width, resolution.height); // 현재 해상도의 너비와 높이 저장

                if (!availableResolutions.Contains(size)) // 같은 너비와 높이가 이미 등록됐는지 확인
                {
                    availableResolutions.Add(size); // 중복되지 않은 해상도 목록에 추가
                }
            }

            if (availableResolutions.Count == 0) // Unity Editor 등에서 해상도 목록을 가져오지 못했는지 확인
            {
                availableResolutions.Add(new Vector2Int(1280, 720)); // 기본 HD 해상도 추가
                availableResolutions.Add(new Vector2Int(1600, 900)); // 기본 HD+ 해상도 추가
                availableResolutions.Add(new Vector2Int(1920, 1080)); // 기본 Full HD 해상도 추가
            }
        }

        void LoadSettings() // PlayerPrefs에 저장된 게임 설정 불러오기
        {
            mouseSensitivityMultiplier = PlayerPrefs.GetFloat(MouseSensitivityKey, DefaultMouseSensitivity); // 저장된 마우스 감도 불러오기

            masterVolume = PlayerPrefs.GetFloat(MasterVolumeKey, DefaultMasterVolume); // 저장된 전체 음량 불러오기
            bgmVolume = PlayerPrefs.GetFloat(BgmVolumeKey, DefaultBgmVolume); // 저장된 배경음 음량 불러오기
            sfxVolume = PlayerPrefs.GetFloat(SfxVolumeKey, DefaultSfxVolume); // 저장된 효과음 음량 불러오기
            ambientVolume = PlayerPrefs.GetFloat(AmbientVolumeKey, DefaultAmbientVolume); // 저장된 환경음 음량 불러오기

            fullScreen = PlayerPrefs.GetInt(FullScreenKey, 1) == 1; // 저장된 전체 화면 여부 불러오기
            vSync = PlayerPrefs.GetInt(VSyncKey, 1) == 1; // 저장된 수직 동기화 여부 불러오기
            resolutionWidth = PlayerPrefs.GetInt(ResolutionWidthKey, DefaultResolutionWidth); // 저장된 해상도 너비 불러오기
            resolutionHeight = PlayerPrefs.GetInt(ResolutionHeightKey, DefaultResolutionHeight); // 저장된 해상도 높이 불러오기

            mouseSensitivityMultiplier = Mathf.Clamp(mouseSensitivityMultiplier, 0.25f, 3f); // 불러온 마우스 감도 범위 제한
            masterVolume = Mathf.Clamp01(masterVolume); // 불러온 전체 음량을 0에서 1 사이로 제한
        }

        public void SaveAndApply(float sensitivity, float master, float bgm, float sfx, float ambient, bool useFullScreen, bool useVSync, int width, int height) // 전달받은 설정 저장과 적용
        {
            mouseSensitivityMultiplier = Mathf.Clamp(sensitivity, 0.25f, 3f); // 마우스 감도 안전 범위 적용
            masterVolume = Mathf.Clamp01(master); // 전체 음량 안전 범위 적용
            bgmVolume = Mathf.Clamp01(bgm); // 배경음 음량 안전 범위 적용
            sfxVolume = Mathf.Clamp01(sfx); // 효과음 음량 안전 범위 적용
            ambientVolume = Mathf.Clamp01(ambient); // 환경음 음량 안전 범위 적용
            fullScreen = useFullScreen; // 전체 화면 설정 적용
            vSync = useVSync; // 수직 동기화 설정 적용
            resolutionWidth = Mathf.Max(640, width); // 해상도 너비 최소값 제한
            resolutionHeight = Mathf.Max(480, height); // 해상도 높이 최소값 제한

            SaveSettings(); // 현재 설정을 PlayerPrefs에 저장
            ApplyCurrentSettings(); // 저장한 설정을 실제 게임에 적용

            Debug.Log($"[Settings] 설정 저장 — Master {masterVolume * 100f:F0}%, BGM {bgmVolume * 100f:F0}%, SFX {sfxVolume * 100f:F0}%, Ambient {ambientVolume * 100f:F0}%"); // 오디오 설정 저장 결과 출력
        }

        void SaveSettings() // 현재 설정값을 PlayerPrefs에 기록
        {
            PlayerPrefs.SetFloat(MouseSensitivityKey, mouseSensitivityMultiplier); // 마우스 감도 저장

            PlayerPrefs.SetFloat(MasterVolumeKey, masterVolume); // 전체 음량 저장
            PlayerPrefs.SetFloat(BgmVolumeKey, bgmVolume); // 배경음 음량 저장
            PlayerPrefs.SetFloat(SfxVolumeKey, sfxVolume); // 효과음 음량 저장
            PlayerPrefs.SetFloat(AmbientVolumeKey, ambientVolume); // 환경음 음량 저장

            PlayerPrefs.SetInt(FullScreenKey, fullScreen ? 1 : 0); // 전체 화면 여부 저장
            PlayerPrefs.SetInt(VSyncKey, vSync ? 1 : 0); // 수직 동기화 여부 저장
            PlayerPrefs.SetInt(ResolutionWidthKey, resolutionWidth); // 해상도 너비 저장
            PlayerPrefs.SetInt(ResolutionHeightKey, resolutionHeight); // 해상도 높이 저장
            PlayerPrefs.Save(); // PlayerPrefs 변경 내용을 저장 장치에 기록
        }

        void ApplyCurrentSettings() // 현재 설정을 Unity 화면과 음량에 적용
        {
            AudioListener.volume = 1f; // AudioMixer와 음량이 이중 적용되지 않도록 Listener 음량 고정

            if (GameAudioManager.Instance != null) // 전역 오디오 관리자 존재 여부 확인
            {
                GameAudioManager.Instance.ApplyVolumes(masterVolume, bgmVolume, sfxVolume, ambientVolume); // 저장된 네 종류의 음량을 AudioMixer에 적용
            }

            QualitySettings.vSyncCount = vSync ? 1 : 0; // 수직 동기화 활성 또는 비활성 적용

            FullScreenMode screenMode = fullScreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed; // 전체 화면 여부에 맞는 화면 모드 계산
            Screen.SetResolution(resolutionWidth, resolutionHeight, screenMode); // 선택한 해상도와 화면 모드 적용
        }

        public Vector2Int GetResolution(int index) // 지정한 번호의 해상도 반환
        {
            if (availableResolutions.Count == 0) // 해상도 목록 존재 여부 확인
            {
                return new Vector2Int(resolutionWidth, resolutionHeight); // 현재 설정 해상도 반환
            }

            int safeIndex = Mathf.Clamp(index, 0, availableResolutions.Count - 1); // 해상도 번호 범위 제한
            return availableResolutions[safeIndex]; // 지정한 해상도 반환
        }

        public int FindResolutionIndex(int width, int height) // 너비와 높이가 일치하는 해상도 번호 검색
        {
            for (int i = 0; i < availableResolutions.Count; i++) // 모든 지원 해상도 순회
            {
                Vector2Int resolution = availableResolutions[i]; // 현재 확인할 해상도 가져오기

                if (resolution.x == width && resolution.y == height) // 너비와 높이 일치 여부 확인
                {
                    return i; // 일치하는 해상도 번호 반환
                }
            }

            return FindNearestResolutionIndex(width, height); // 정확히 일치하지 않으면 가장 가까운 해상도 번호 반환
        }

        int FindNearestResolutionIndex(int width, int height) // 목표 크기와 가장 가까운 해상도 번호 검색
        {
            int nearestIndex = 0; // 가장 가까운 해상도 번호 초기화
            int nearestDifference = int.MaxValue; // 가장 작은 해상도 차이 초기화

            for (int i = 0; i < availableResolutions.Count; i++) // 모든 지원 해상도 순회
            {
                Vector2Int resolution = availableResolutions[i]; // 현재 비교할 해상도 가져오기
                int difference = Mathf.Abs(resolution.x - width) + Mathf.Abs(resolution.y - height); // 목표 해상도와 크기 차이 계산

                if (difference < nearestDifference) // 기존 결과보다 더 가까운지 확인
                {
                    nearestDifference = difference; // 가장 작은 차이 갱신
                    nearestIndex = i; // 가장 가까운 해상도 번호 갱신
                }
            }

            return nearestIndex; // 가장 가까운 해상도 번호 반환
        }

        [ContextMenu("게임 설정 초기화")] // Inspector 우클릭 기본 설정 테스트 메뉴 등록
        public void ResetToDefaults() // 기본 게임 설정 저장과 적용
        {
            int defaultResolutionIndex = FindResolutionIndex(DefaultResolutionWidth, DefaultResolutionHeight); // 기본 해상도와 가장 가까운 번호 검색
            Vector2Int defaultResolution = GetResolution(defaultResolutionIndex); // 실제 지원하는 기본 해상도 가져오기

            SaveAndApply(DefaultMouseSensitivity, DefaultMasterVolume, DefaultBgmVolume, DefaultSfxVolume, DefaultAmbientVolume, 
                         true, true, defaultResolution.x, defaultResolution.y); 
            // 기본 게임과 오디오 설정 저장 및 적용
        }
    }
}